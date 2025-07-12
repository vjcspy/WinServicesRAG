using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.DXGI.Resource;
using ResultCode = SharpDX.DXGI.ResultCode;

namespace WinServicesRAG.Core.Screenshot;

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

    public string ProviderName => "DirectX Desktop Duplication API";

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
        GC.SuppressFinalize(this);
    }

    public bool IsAvailable()
    {
        try
        {
            // Check current session info
            uint sessionId = GetCurrentSessionId();
            Console.WriteLine($"[DirectX] Current session ID: {sessionId}");
            
            // Session 0 is the system session, user sessions are 1+
            if (sessionId == 0)
            {
                Console.WriteLine("[DirectX] WARNING: Running in Session 0 (System). DirectX Desktop Duplication may not capture user desktop.");
                Console.WriteLine("[DirectX] This is expected for Windows Services. DirectX will capture Session 0 desktop (usually black).");
                return false; // Explicitly return false for Session 0
            }
            
            Initialize();
            bool isAvailable = _outputDuplication != null;
            Console.WriteLine($"[DirectX] IsAvailable check: {isAvailable}");
            return isAvailable;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DirectX] IsAvailable failed: {ex.Message}");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        try
        {
            // Always try to initialize if not already done
            if (_outputDuplication == null)
            {
                Initialize();
            }

            if (_device == null || _outputDuplication == null)
            {
                Console.WriteLine("[DirectX] Device or OutputDuplication is null");
                return null;
            }

            Console.WriteLine("[DirectX] Attempting to acquire next frame...");
            
            // Try with a longer timeout for the first frame
            Result result = _outputDuplication.TryAcquireNextFrame(timeoutInMilliseconds: 5000, frameInfoRef: out _, desktopResourceOut: out Resource desktopResource);

            if (!result.Success)
            {
                Console.WriteLine($"[DirectX] AcquireNextFrame failed with result: {result.Code:X}");
                
                // Check for common DXGI error codes
                if (result.Code == ResultCode.AccessLost.Result.Code)
                {
                    Console.WriteLine("[DirectX] Access lost - display mode change or device removed");
                }
                else if (result.Code == ResultCode.WaitTimeout.Result.Code)
                {
                    Console.WriteLine("[DirectX] Wait timeout - no new frame available");
                }
                else if (result.Code == ResultCode.InvalidCall.Result.Code)
                {
                    Console.WriteLine("[DirectX] Invalid call - duplication may not be initialized properly");
                }
                
                // If the error is DXGI_ERROR_WAIT_TIMEOUT, it's not a fatal error.
                // But for a single screenshot, we can consider it a failure.
                if (result.Code != ResultCode.WaitTimeout.Result.Code)
                {
                    Console.WriteLine("[DirectX] Non-timeout error, disposing and will re-initialize on next attempt");
                    // For other errors, re-initialize on next attempt
                    Dispose();
                }
                return null;
            }

            Console.WriteLine("[DirectX] Frame acquired successfully, processing...");

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

                        Console.WriteLine($"[DirectX] Creating bitmap: {width}x{height}, stride: {stride}");

                        // Copy data to managed array first for safer processing
                        int totalBytes = height * stride;
                        byte[] pixelData = new byte[totalBytes];
                        Marshal.Copy(dataPtr, pixelData, 0, totalBytes);
                        
                        Console.WriteLine($"[DirectX] Copied {totalBytes} bytes of pixel data");
                        
                        // Check if pixel data is all zeros (black screen)
                        bool hasNonZeroPixels = false;
                        for (int i = 0; i < Math.Min(10000, totalBytes); i++) // Check first 10KB
                        {
                            if (pixelData[i] != 0)
                            {
                                hasNonZeroPixels = true;
                                break;
                            }
                        }
                        Console.WriteLine($"[DirectX] Has non-zero pixels in sample: {hasNonZeroPixels}");

                        // Create bitmap and copy data properly
                        using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                        {
                            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), 
                                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                            
                            try
                            {
                                // Copy the data row by row to handle stride differences
                                int bytesPerLine = width * 4;
                                for (int y = 0; y < height; y++)
                                {
                                    int srcOffset = y * stride;
                                    IntPtr dstPtr = bmpData.Scan0 + (y * bmpData.Stride);
                                    
                                    // Copy one row
                                    Marshal.Copy(pixelData, srcOffset, dstPtr, Math.Min(bytesPerLine, bmpData.Stride));
                                }
                            }
                            finally
                            {
                                bitmap.UnlockBits(bmpData);
                            }

                            using (MemoryStream ms = new MemoryStream())
                            {
                                bitmap.Save(stream: ms, format: ImageFormat.Png);
                                byte[] result_bytes = ms.ToArray();
                                Console.WriteLine($"[DirectX] Screenshot completed successfully, size: {result_bytes.Length} bytes");
                                
                                // Additional debug info
                                if (result_bytes.Length < 50000) // Suspiciously small
                                {
                                    Console.WriteLine("[DirectX] WARNING: Screenshot file size is suspiciously small, might be black/empty");
                                }
                                
                                return result_bytes;
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
            Console.WriteLine($"[DirectX] TakeScreenshot failed: {ex.Message}");
            Console.WriteLine($"[DirectX] Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[DirectX] Inner exception: {ex.InnerException.Message}");
            }
            // Clean up in case of error to allow re-initialization
            Dispose();
            return null;
        }
        finally
        {
            // Release the frame after we are done with it
            try
            {
                _outputDuplication?.ReleaseFrame();
                Console.WriteLine("[DirectX] Frame released");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DirectX] Error releasing frame: {ex.Message}");
            }
        }
    }

    private void Initialize()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(DirectXScreenshotProvider));
        }

        if (_outputDuplication != null) 
        {
            Console.WriteLine("[DirectX] Already initialized");
            return;
        }

        Console.WriteLine("[DirectX] Initializing DirectX components...");

        try
        {
            using (Factory1 factory = new Factory1())
            {
                Console.WriteLine($"[DirectX] Created factory, adapter count: {factory.GetAdapterCount1()}");
                
                using (Adapter1? adapter = factory.GetAdapter1(index: 0))  // 0 = default adapter
                {
                    Console.WriteLine($"[DirectX] Using adapter: {adapter.Description.Description}");
                    Console.WriteLine($"[DirectX] Output count: {adapter.GetOutputCount()}");
                    
                    using (Output? output = adapter.GetOutput(outputIndex: 0)) // 0 = primary output
                    {
                        Console.WriteLine($"[DirectX] Using output: {output.Description.DeviceName}");
                        
                        _device = new Device(adapter: adapter, flags: DeviceCreationFlags.BgraSupport);
                        Console.WriteLine("[DirectX] Device created successfully");

                        using (Output1? output1 = output.QueryInterface<Output1>())
                        {
                            _outputDuplication = output1.DuplicateOutput(deviceRef: _device);
                            Console.WriteLine("[DirectX] Output duplication created successfully");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DirectX] Initialization failed: {ex.Message}");
            Console.WriteLine($"[DirectX] Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[DirectX] Inner exception: {ex.InnerException.Message}");
            }
            
            Dispose(); // Clean up partial initializations
            throw new Exception("Failed to initialize DirectX components.", ex);
        }
    }

    // P/Invoke to get current session ID
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();
    
    [DllImport("kernel32.dll")]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);
    
    private static uint GetCurrentSessionId()
    {
        uint processId = GetCurrentProcessId();
        ProcessIdToSessionId(processId, out uint sessionId);
        return sessionId;
    }
}
