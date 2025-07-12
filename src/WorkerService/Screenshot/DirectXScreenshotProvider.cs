using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Drawing;
using System.Drawing.Imaging;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.DXGI.Resource;
using ResultCode = SharpDX.DXGI.ResultCode;

namespace WorkerService.Screenshot;

/// <summary>
///     DirectX Desktop Duplication API provider.
///     High performance, captures almost everything including full-screen apps.
/// </summary>
public class DirectXScreenshotProvider : IScreenshotProvider, IDisposable
{
    private Texture2D? _desktopTexture;

    private Device? _device;
    private bool _isDisposed;
    private OutputDuplication? _outputDuplication;

    public void Dispose()
    {
        if (_isDisposed) return;

        _desktopTexture?.Dispose();
        _desktopTexture = null;

        _outputDuplication?.Dispose();
        _outputDuplication = null;

        _device?.Dispose();
        _device = null;

        _isDisposed = true;
        GC.SuppressFinalize(obj: this);
    }
    public string ProviderName
    {
        get
        {
            return "DirectX Desktop Duplication API";
        }
    }

    public bool IsAvailable()
    {
        try
        {
            Initialize();
            return _outputDuplication != null;
        }
        catch
        {
            return false;
        }
        finally
        {
            Dispose();
        }
    }

    public byte[]? TakeScreenshot()
    {
        try
        {
            Initialize();

            if (_device == null || _outputDuplication == null)
            {
                throw new InvalidOperationException(message: "DXGI components not initialized.");
            }

            Result result = _outputDuplication.TryAcquireNextFrame(timeoutInMilliseconds: 1000, frameInfoRef: out _, desktopResourceOut: out Resource desktopResource);

            if (!result.Success)
            {
                // If the error is DXGI_ERROR_WAIT_TIMEOUT, it's not a fatal error.
                // But for a single screenshot, we can consider it a failure.
                if (result.Code != ResultCode.WaitTimeout.Result.Code)
                {
                    // For other errors, re-initialize on next attempt
                    Dispose();
                }
                return null;
            }

            using (desktopResource)
            using (Texture2D? desktopImage = desktopResource.QueryInterface<Texture2D>())
            {
                // Copy the desktop image to a staging texture
                Texture2DDescription textureDesc = desktopImage.Description;
                textureDesc.Usage = ResourceUsage.Staging;
                textureDesc.CpuAccessFlags = CpuAccessFlags.Read;
                textureDesc.OptionFlags = ResourceOptionFlags.None;
                textureDesc.BindFlags = BindFlags.None;

                using (Texture2D stagingTexture = new Texture2D(device: _device, description: textureDesc))
                {
                    _device.ImmediateContext.CopyResource(source: desktopImage, destination: stagingTexture);

                    // Map the staging texture to access the pixel data
                    DataBox dataBox = _device.ImmediateContext.MapSubresource(resourceRef: stagingTexture, subresource: 0, mapType: MapMode.Read, mapFlags: MapFlags.None);

                    try
                    {
                        int width = textureDesc.Width;
                        int height = textureDesc.Height;
                        int stride = dataBox.RowPitch;
                        IntPtr dataPtr = dataBox.DataPointer;

                        // Create a GDI+ bitmap from the raw BGRA data
                        using (Bitmap bitmap = new Bitmap(width: width, height: height, stride: stride, format: PixelFormat.Format32bppRgb, scan0: dataPtr))
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                bitmap.Save(stream: ms, format: ImageFormat.Png);
                                return ms.ToArray();
                            }
                        }
                    }
                    finally
                    {
                        _device.ImmediateContext.UnmapSubresource(resourceRef: stagingTexture, subresource: 0);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(value: $"[Error] DirectX Provider failed: {ex.Message}");
            // Clean up in case of error to allow re-initialization
            Dispose();
            return null;
        }
        finally
        {
            // Release the frame after we are done with it
            _outputDuplication?.ReleaseFrame();
        }
    }

    private void Initialize()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(objectName: nameof(DirectXScreenshotProvider));
        }

        if (_outputDuplication != null) return;

        try
        {
            using (Factory1 factory = new Factory1())
            using (Adapter1? adapter = factory.GetAdapter1(index: 0))  // 0 = default adapter
            using (Output? output = adapter.GetOutput(outputIndex: 0)) // 0 = primary output
            {
                _device = new Device(adapter: adapter, flags: DeviceCreationFlags.BgraSupport);

                using (Output1? output1 = output.QueryInterface<Output1>())
                {
                    _outputDuplication = output1.DuplicateOutput(deviceRef: _device);
                }
            }
        }
        catch (Exception ex)
        {
            Dispose(); // Clean up partial initializations
            throw new Exception(message: "Failed to initialize DirectX components.", innerException: ex);
        }
    }
}
