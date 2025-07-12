using Microsoft.Extensions.Logging;
using SharpGen.Runtime;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using MapFlags = Vortice.Direct3D11.MapFlags;
using ResultCode = Vortice.DXGI.ResultCode;
namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     DirectX Desktop Duplication API provider using modern Vortice.Windows.
///     High performance captures almost everything, including full-screen apps.
///     Optimized for Windows 11 with a modern DirectX wrapper.
/// </summary>
public class DirectXScreenshotProvider(ILogger logger) : IScreenshotProvider
{
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _deviceContext;
    private bool _isDisposed;
    private IDXGIOutputDuplication? _outputDuplication;

    public string ProviderName
    {
        get
        {
            return "DirectX";
        }
    }

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
        GC.SuppressFinalize(obj: this);
    }

    public bool IsAvailable()
    {
        try
        {
            // // Check current session info
            // uint sessionId = GetCurrentSessionId();
            // _logger?.LogDebug(message: "Current session ID: {SessionId}", sessionId);
            //
            // // Session 0 is the system session - DirectX Desktop Duplication doesn't work there
            // if (sessionId == 0)
            // {
            //     _logger?.LogWarning(message: "DirectX Desktop Duplication API not available in Session 0 (system session)");
            //     return false;
            // }

            // Try to initialize DirectX components
            InitializeDirectX();
            bool isAvailable = _outputDuplication != null;

            logger?.LogInformation(message: "DirectX Desktop Duplication API availability: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            logger?.LogError(exception: ex, message: "Failed to check DirectX availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        logger.LogInformation(message: "Taking screenshot using DirectX Desktop Duplication API");
        try
        {
            if (_outputDuplication == null)
            {
                logger?.LogWarning(message: "DirectX not initialized, attempting to initialize...");
                InitializeDirectX();
            }

            if (_device == null || _outputDuplication == null || _deviceContext == null)
            {
                logger?.LogError(message: "DirectX components not properly initialized");
                return null;
            }

            logger?.LogDebug(message: "Attempting to acquire frame from DirectX Desktop Duplication API");

            // Try to acquire the next frame
            Result result = _outputDuplication.AcquireNextFrame(timeoutInMilliseconds: 5000, frameInfo: out OutduplFrameInfo _frameInfo, desktopResource: out IDXGIResource? desktopResource);

            if (result.Failure)
            {
                if (result == ResultCode.AccessLost)
                {
                    logger?.LogWarning(message: "Access lost, attempting to reinitialize DirectX");
                    ReinitializeDirectX();
                    return null;
                }
                if (result == ResultCode.WaitTimeout)
                {
                    logger?.LogDebug(message: "No new frame available (timeout)");
                    return null;
                }
                if (result == ResultCode.InvalidCall)
                {
                    logger?.LogError(message: "Invalid call to AcquireNextFrame");
                    return null;
                }
                logger?.LogError(message: "Failed to acquire frame: {Result}", result);
                return null;
            }

            try
            {
                using (desktopResource)
                {
                    ID3D11Texture2D? desktopImage = desktopResource.QueryInterface<ID3D11Texture2D>();
                    using (desktopImage)
                    {
                        if (desktopImage == null)
                        {
                            logger?.LogError(message: "Failed to query desktop texture interface");
                            return null;
                        }

                        Texture2DDescription textureDesc = desktopImage.Description;

                        // Create a staging texture to read the data
                        Texture2DDescription stagingDesc = new Texture2DDescription
                        {
                            Width = textureDesc.Width,
                            Height = textureDesc.Height,
                            MipLevels = 1,
                            ArraySize = 1,
                            Format = textureDesc.Format,
                            SampleDescription = new SampleDescription(count: 1, quality: 0),
                            Usage = ResourceUsage.Staging,
                            CPUAccessFlags = CpuAccessFlags.Read,
                            MiscFlags = ResourceOptionFlags.None
                        };

                        using ID3D11Texture2D stagingTexture = _device.CreateTexture2D(description: stagingDesc);

                        // Copy desktop image to staging texture
                        _deviceContext.CopyResource(dstResource: stagingTexture, srcResource: desktopImage);

                        // Map the staging texture to read pixel data
                        MappedSubresource mappedResource = _deviceContext.Map(resource: stagingTexture, subresource: 0, mode: MapMode.Read, flags: MapFlags.None);

                        try
                        {
                            return ConvertToImage(mappedResource: mappedResource, width: textureDesc.Width, height: textureDesc.Height);
                        }
                        finally
                        {
                            _deviceContext.Unmap(resource: stagingTexture, subresource: 0);
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
            logger?.LogError(exception: ex, message: "Failed to capture screenshot using DirectX");
            return null;
        }
    }

    private void InitializeDirectX()
    {
        try
        {
            // Create D3D11 device
            D3D11.D3D11CreateDevice(
                adapter: null,
                driverType: DriverType.Hardware,
                flags: DeviceCreationFlags.None,
                featureLevels: null!,
                device: out _device,
                immediateContext: out _deviceContext);

            // Get DXGI device
            using IDXGIDevice dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using IDXGIAdapter dxgiAdapter = dxgiDevice.GetAdapter();

            // Get the first output (primary display)
            Result result = dxgiAdapter.EnumOutputs(output: 0, outputOut: out IDXGIOutput? dxgiOutput);
            if (result.Failure || dxgiOutput == null)
            {
                throw new InvalidOperationException(message: "Failed to enumerate DXGI outputs");
            }

            using (dxgiOutput)
            {
                using IDXGIOutput1 dxgiOutput1 = dxgiOutput.QueryInterface<IDXGIOutput1>();

                // Duplicate the output
                _outputDuplication = dxgiOutput1.DuplicateOutput(device: _device);
            }

            logger?.LogInformation(message: "DirectX Desktop Duplication API initialized successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(exception: ex, message: "Failed to initialize DirectX");
            Dispose();
            throw;
        }
    }

    private void ReinitializeDirectX()
    {
        try
        {
            logger?.LogInformation(message: "Reinitializing DirectX Desktop Duplication API");

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
            logger?.LogError(exception: ex, message: "Failed to reinitialize DirectX");
        }
    }

    private unsafe byte[] ConvertToImage(MappedSubresource mappedResource, int width, int height)
    {
        using Bitmap bitmap = new Bitmap(width: width, height: height, format: PixelFormat.Format32bppArgb);
        BitmapData bitmapData = bitmap.LockBits(
            rect: new Rectangle(x: 0, y: 0, width: width, height: height),
            flags: ImageLockMode.WriteOnly,
            format: PixelFormat.Format32bppArgb);

        try
        {
            IntPtr sourcePtr = mappedResource.DataPointer;
            IntPtr destPtr = bitmapData.Scan0;
            int sourceRowPitch = mappedResource.RowPitch;
            int destRowPitch = bitmapData.Stride;

            for (int row = 0; row < height; row++)
            {
                Buffer.MemoryCopy(
                    source: (void*)(sourcePtr + row * sourceRowPitch),
                    destination: (void*)(destPtr + row * destRowPitch),
                    destinationSizeInBytes: destRowPitch,
                    sourceBytesToCopy: Math.Min(val1: sourceRowPitch, val2: destRowPitch));
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapdata: bitmapData);
        }

        using MemoryStream memoryStream = new MemoryStream();
        bitmap.Save(stream: memoryStream, format: ImageFormat.Png);
        return memoryStream.ToArray();
    }

    [DllImport(dllName: "kernel32.dll")]
    private static extern uint GetCurrentSessionId();
}
