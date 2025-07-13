using Microsoft.Extensions.Logging;
using SharpGen.Runtime;
using System.Drawing;
using System.Drawing.Imaging;
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
            // Try to initialize DirectX components
            InitializeDirectX();
            bool isAvailable = _outputDuplication != null;

            logger.LogInformation(message: "DirectX Desktop Duplication API availability: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            logger.LogError(exception: ex, message: "Failed to check DirectX availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        logger.LogInformation(message: "Taking screenshot using DirectX Desktop Duplication API.");
        try
        {
            // Đảm bảo DirectX được khởi tạo. Nếu không, cố gắng khởi tạo.
            // Đây là điểm khởi tạo "On-demand"
            if (_outputDuplication == null)
            {
                logger.LogWarning(message: "DirectX not initialized, attempting to initialize...");
                try
                {
                    InitializeDirectX();
                }
                catch (Exception ex)
                {
                    logger.LogError(exception: ex, message: "Failed to initialize DirectX during screenshot attempt.");
                    return null;
                }
            }

            if (_device == null || _outputDuplication == null || _deviceContext == null)
            {
                logger.LogError(message: "DirectX components not properly initialized after attempt.");
                return null;
            }

            IDXGIResource? desktopResource = null;
            ID3D11Texture2D? desktopImage = null;
            ID3D11Texture2D? stagingTexture = null;
            var mappedResource = default(MappedSubresource);
            Thread.Sleep(millisecondsTimeout: 700); // Giảm độ trễ để tránh lỗi AccessLost do quá trình khởi tạo DirectX
            try
            {
                logger.LogDebug(message: "Attempting to acquire frame from DirectX Desktop Duplication API.");

                // Loop để thử lấy frame, xử lý AccessLost
                for (var i = 0; i < 2; i++) // Thử lại 1 lần nếu AccessLost
                {
                    Result result = _outputDuplication.AcquireNextFrame(
                        timeoutInMilliseconds: 500, // Giảm timeout để phản hồi nhanh hơn nếu không có frame
                        frameInfo: out OutduplFrameInfo _,
                        desktopResource: out desktopResource);

                    if (result.Success)
                    {
                        logger.LogDebug(message: "Successfully acquired frame.");
                        break; // Thành công, thoát vòng lặp
                    }
                    if (result == ResultCode.AccessLost)
                    {
                        logger.LogWarning(message: "Access lost, attempting to reinitialize DirectX.");
                        ReinitializeDirectX();
                        if (_outputDuplication == null) // Reinitialization failed
                        {
                            logger.LogError(message: "DirectX reinitialization failed after AccessLost.");
                            return null;
                        }
                    }
                    else if (result == ResultCode.WaitTimeout)
                    {
                        logger.LogDebug(message: "No new frame available (timeout).");
                        return null;
                    }
                    else
                    {
                        logger.LogError(message: "Failed to acquire frame: {Result}", result);
                        return null;
                    }
                }

                // Nếu desktopResource vẫn null sau vòng lặp, có nghĩa là không lấy được frame
                if (desktopResource == null)
                {
                    logger.LogError(message: "Desktop resource is null after acquiring frame attempts.");
                    return null;
                }

                desktopImage = desktopResource.QueryInterface<ID3D11Texture2D>();

                Texture2DDescription textureDesc = desktopImage.Description;

                // Tạo staging texture
                var stagingDesc = new Texture2DDescription
                {
                    Width = textureDesc.Width,
                    Height = textureDesc.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = textureDesc.Format, // Sử dụng định dạng của DesktopImage
                    SampleDescription = new SampleDescription(count: 1, quality: 0),
                    Usage = ResourceUsage.Staging,
                    CPUAccessFlags = CpuAccessFlags.Read,
                    MiscFlags = ResourceOptionFlags.None
                };

                stagingTexture = _device.CreateTexture2D(description: stagingDesc);
                _deviceContext.CopyResource(dstResource: stagingTexture, srcResource: desktopImage);

                mappedResource = _deviceContext.Map(resource: stagingTexture, subresource: 0, mode: MapMode.Read, flags: MapFlags.None);

                byte[] imageData = ConvertToImage(mappedResource: mappedResource, width: textureDesc.Width, height: textureDesc.Height);
                return imageData;
            }
            catch (SharpGenException sgEx)
            {
                logger.LogError(exception: sgEx, message: "SharpGen exception during DirectX screenshot.");
                // Cân nhắc reinitialize nếu lỗi là do thiết bị bị mất/reset
                if (sgEx.ResultCode == ResultCode.AccessLost || sgEx.ResultCode == ResultCode.DeviceRemoved || sgEx.ResultCode == ResultCode.DeviceReset)
                {
                    logger.LogWarning(message: "Device lost/reset detected, attempting reinitialization.");
                    ReinitializeDirectX();
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(exception: ex, message: "Unexpected error during DirectX screenshot.");
                return null;
            }
            finally
            {
                // Đảm bảo giải phóng tài nguyên một cách an toàn
                if (mappedResource.DataPointer != IntPtr.Zero)
                {
                    _deviceContext?.Unmap(resource: stagingTexture, subresource: 0);
                }
                stagingTexture?.Dispose();
                desktopImage?.Dispose();
                desktopResource?.Dispose();         // Release DXGI Resource acquired
                _outputDuplication?.ReleaseFrame(); // Quan trọng: Luôn release frame đã acquire
            }
        }
        catch
        {
            // ignored
        }
        return null;
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

            logger.LogInformation(message: "DirectX Desktop Duplication API initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(exception: ex, message: "Failed to initialize DirectX");
            Dispose();
            throw;
        }
    }

    private void ReinitializeDirectX()
    {
        try
        {
            logger.LogInformation(message: "Reinitializing DirectX Desktop Duplication API");

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
        using var bitmap = new Bitmap(width: width, height: height, format: PixelFormat.Format32bppArgb);
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

            for (var row = 0; row < height; row++)
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

        using var memoryStream = new MemoryStream();
        bitmap.Save(stream: memoryStream, format: ImageFormat.Png);
        return memoryStream.ToArray();
    }
}
