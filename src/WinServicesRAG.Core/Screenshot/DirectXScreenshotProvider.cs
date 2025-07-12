using Vortice.Direct3D11;
using Vortice.DXGI;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     DirectX Desktop Duplication API provider using modern Vortice.Windows.
///     High performance, captures almost everything including full-screen apps.
///     Optimized for Windows 11 with modern DirectX wrapper.
/// </summary>
public class DirectXScreenshotProvider : IScreenshotProvider, IDisposable
{
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _deviceContext;
    private bool _isDisposed;
    private IDXGIOutputDuplication? _outputDuplication;
    private readonly ILogger? _logger;

    public DirectXScreenshotProvider(ILogger? logger = null)
    {
        _logger = logger;
    }

    public string ProviderName => "DirectX Desktop Duplication API (Vortice.Windows)";

    public void Dispose()
    {
        if (_isDisposed) return;

        _outputDuplication?.Dispose();
        _outputDuplication = null;

        _deviceContext?.Dispose();
        _deviceContext = null;

        _device?.Dispose();
        _device = null;

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public bool IsAvailable()
    {
        try
        {
            // Check current session info
            uint sessionId = GetCurrentSessionId();
            _logger?.LogDebug("Current session ID: {SessionId}", sessionId);

            // Session 0 is the system session - DirectX Desktop Duplication doesn't work there
            if (sessionId == 0)
            {
                _logger?.LogWarning("DirectX Desktop Duplication API not available in Session 0 (system session)");
                return false;
            }

            // Try to initialize DirectX components
            InitializeDirectX();
            bool isAvailable = _outputDuplication != null;
            
            _logger?.LogInformation("DirectX Desktop Duplication API availability: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check DirectX availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        try
        {
            if (_outputDuplication == null)
            {
                _logger?.LogWarning("DirectX not initialized, attempting to initialize...");
                InitializeDirectX();
            }

            if (_device == null || _outputDuplication == null || _deviceContext == null)
            {
                _logger?.LogError("DirectX components not properly initialized");
                return null;
            }

            _logger?.LogDebug("Attempting to acquire frame from DirectX Desktop Duplication API");

            // Try to acquire next frame
            var result = _outputDuplication.AcquireNextFrame(5000, out var frameInfo, out var desktopResource);
            
            if (result.Failure)
            {
                if (result == Vortice.DXGI.ResultCode.AccessLost)
                {
                    _logger?.LogWarning("Access lost, attempting to reinitialize DirectX");
                    ReinitializeDirectX();
                    return null;
                }
                else if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    _logger?.LogDebug("No new frame available (timeout)");
                    return null;
                }
                else if (result == Vortice.DXGI.ResultCode.InvalidCall)
                {
                    _logger?.LogError("Invalid call to AcquireNextFrame");
                    return null;
                }
                else
                {
                    _logger?.LogError("Failed to acquire frame: {Result}", result);
                    return null;
                }
            }

            try
            {
                using (desktopResource)
                {
                    var desktopImage = desktopResource.QueryInterface<ID3D11Texture2D>();
                    using (desktopImage)
                    {
                        if (desktopImage == null)
                        {
                            _logger?.LogError("Failed to query desktop texture interface");
                            return null;
                        }

                        var textureDesc = desktopImage.Description;
                        
                        // Create a staging texture to read the data
                        var stagingDesc = new Texture2DDescription
                        {
                            Width = textureDesc.Width,
                            Height = textureDesc.Height,
                            MipLevels = 1,
                            ArraySize = 1,
                            Format = textureDesc.Format,
                            SampleDescription = new SampleDescription(1, 0),
                            Usage = ResourceUsage.Staging,
                            CPUAccessFlags = CpuAccessFlags.Read,
                            MiscFlags = ResourceOptionFlags.None
                        };

                        using var stagingTexture = _device.CreateTexture2D(stagingDesc);
                        
                        // Copy desktop image to staging texture
                        _deviceContext.CopyResource(stagingTexture, desktopImage);

                        // Map the staging texture to read pixel data
                        var mappedResource = _deviceContext.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                        
                        try
                        {
                            return ConvertToImage(mappedResource, textureDesc.Width, textureDesc.Height);
                        }
                        finally
                        {
                            _deviceContext.Unmap(stagingTexture, 0);
                        }
                    }
                }
            }
            finally
            {
                _outputDuplication.ReleaseFrame();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to capture screenshot using DirectX");
            return null;
        }
    }

    private void InitializeDirectX()
    {
        try
        {
            // Create D3D11 device
            D3D11.D3D11CreateDevice(
                null,
                Vortice.Direct3D.DriverType.Hardware,
                DeviceCreationFlags.None,
                null,
                out _device,
                out _deviceContext);

            // Get DXGI device
            using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using var dxgiAdapter = dxgiDevice.GetAdapter();
            
            // Get the first output (primary display)
            var result = dxgiAdapter.EnumOutputs(0, out var dxgiOutput);
            if (result.Failure || dxgiOutput == null)
            {
                throw new InvalidOperationException("Failed to enumerate DXGI outputs");
            }
            
            using (dxgiOutput)
            {
                using var dxgiOutput1 = dxgiOutput.QueryInterface<IDXGIOutput1>();

                // Duplicate the output
                _outputDuplication = dxgiOutput1.DuplicateOutput(_device);
            }
            
            _logger?.LogInformation("DirectX Desktop Duplication API initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize DirectX");
            Dispose();
            throw;
        }
    }

    private void ReinitializeDirectX()
    {
        try
        {
            _logger?.LogInformation("Reinitializing DirectX Desktop Duplication API");
            
            _outputDuplication?.Dispose();
            _outputDuplication = null;
            
            _deviceContext?.Dispose();
            _deviceContext = null;
            
            _device?.Dispose();
            _device = null;

            InitializeDirectX();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reinitialize DirectX");
        }
    }

    private unsafe byte[] ConvertToImage(MappedSubresource mappedResource, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var sourcePtr = mappedResource.DataPointer;
            var destPtr = bitmapData.Scan0;
            var sourceRowPitch = mappedResource.RowPitch;
            var destRowPitch = bitmapData.Stride;

            for (int row = 0; row < height; row++)
            {
                Buffer.MemoryCopy(
                    (void*)(sourcePtr + row * sourceRowPitch),
                    (void*)(destPtr + row * destRowPitch),
                    destRowPitch,
                    Math.Min(sourceRowPitch, destRowPitch));
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        return memoryStream.ToArray();
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentSessionId();
}
