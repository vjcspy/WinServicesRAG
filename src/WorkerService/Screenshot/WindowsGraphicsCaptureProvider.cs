using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace WorkerService.Screenshot
{
    /// <summary>
    /// Windows Graphics Capture API screenshot provider
    /// Modern, secure, and efficient capture method for Windows 10+
    /// Recommended by Microsoft, captures most content including UAC and UWP apps
    /// </summary>
    public class WindowsGraphicsCaptureProvider : IScreenshotProvider
    {
        public string ProviderName => "Windows Graphics Capture API";

        private readonly object _lock = new object();
        private bool _isCapturing = false;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        public bool IsAvailable()
        {
            try
            {
                // Check if Graphics Capture is supported
                if (!GraphicsCaptureSession.IsSupported())
                    return false;

                // Check Windows version (requires Windows 10 build 17134 or later)
                var version = Environment.OSVersion.Version;
                if (version.Major < 10)
                    return false;

                if (version.Major == 10 && version.Build < 17134)
                    return false;

                // Try to create a Direct3D device
                var d3dDevice = Direct3D11Helpers.CreateDevice();
                if (d3dDevice == null)
                    return false;

                // Try to create a capture item for primary monitor
                var captureItem = CaptureHelper.CreateItemForPrimaryMonitor();
                return captureItem != null;
            }
            catch
            {
                return false;
            }
        }

        public byte[]? TakeScreenshot()
        {
            lock (_lock)
            {
                if (_isCapturing)
                    return null;

                _isCapturing = true;
                try
                {
                    return CaptureScreenAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Windows Graphics Capture failed: {ex.Message}");
                    return null;
                }
                finally
                {
                    _isCapturing = false;
                }
            }
        }

        private async Task<byte[]?> CaptureScreenAsync()
        {
            GraphicsCaptureSession? session = null;
            Direct3D11CaptureFramePool? framePool = null;

            try
            {
                // Create capture item for primary monitor
                var captureItem = CaptureHelper.CreateItemForPrimaryMonitor();
                if (captureItem == null)
                    return null;

                // Create Direct3D device
                var d3dDevice = Direct3D11Helpers.CreateDevice();
                if (d3dDevice == null)
                    return null;

                // Create frame pool
                framePool = Direct3D11CaptureFramePool.Create(
                    d3dDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    1, // number of frames
                    captureItem.Size);

                var tcs = new TaskCompletionSource<byte[]?>();
                var frameArrived = false;

                framePool.FrameArrived += (sender, args) =>
                {
                    if (frameArrived) return;
                    frameArrived = true;

                    try
                    {
                        using var frame = framePool.TryGetNextFrame();
                        if (frame == null)
                        {
                            tcs.SetResult(null);
                            return;
                        }

                        var result = ProcessFrameAsync(frame).GetAwaiter().GetResult();
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                };

                // Start capture session
                session = framePool.CreateCaptureSession(captureItem);
                session.StartCapture();

                // Wait for frame with timeout
                var timeoutTask = Task.Delay(5000); // 5 second timeout
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("Windows Graphics Capture timeout");
                    return null;
                }

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Graphics Capture error: {ex.Message}");
                return null;
            }
            finally
            {
                session?.Dispose();
                framePool?.Dispose();
            }
        }

        private async Task<byte[]?> ProcessFrameAsync(Direct3D11CaptureFrame frame)
        {
            try
            {
                // Get the surface from the frame
                var surface = frame.Surface;

                // Create software bitmap from surface
                var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);

                // Convert to byte array
                return await ConvertSoftwareBitmapToByteArrayAsync(softwareBitmap);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Frame processing error: {ex.Message}");
                return null;
            }
        }

        private async Task<byte[]> ConvertSoftwareBitmapToByteArrayAsync(SoftwareBitmap softwareBitmap)
        {
            using var stream = new InMemoryRandomAccessStream();

            // Create bitmap encoder
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(softwareBitmap);

            await encoder.FlushAsync();

            // Convert to byte array
            var bytes = new byte[stream.Size];
            await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);

            return bytes;
        }
    }

    /// <summary>
    /// Helper class for creating capture items
    /// </summary>
    internal static class CaptureHelper
    {
        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }

        [ComImport]
        [Guid("79C3F95B-31F7-4EC2-A464-632F5D30E760")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
            IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetPrimaryMonitor();

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        public static GraphicsCaptureItem? CreateItemForWindow(IntPtr hwnd)
        {
            try
            {
                var factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
                var interop = factory.As<IGraphicsCaptureItemInterop>();

                var itemGuid = typeof(GraphicsCaptureItem).GUID;
                var itemPointer = interop.CreateForWindow(hwnd, ref itemGuid);

                var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
                return item;
            }
            catch
            {
                return null;
            }
        }

        public static GraphicsCaptureItem? CreateItemForPrimaryMonitor()
        {
            try
            {
                var factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
                var interop = factory.As<IGraphicsCaptureItemInterop>();

                // Get primary monitor handle
                var primaryMonitor = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
                if (primaryMonitor == IntPtr.Zero)
                    return null;

                var itemGuid = typeof(GraphicsCaptureItem).GUID;
                var itemPointer = interop.CreateForMonitor(primaryMonitor, ref itemGuid);

                var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
                return item;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Helper for Direct3D operations
    /// </summary>
    internal static class Direct3D11Helpers
    {
        [DllImport("d3d11.dll", SetLastError = true)]
        private static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int driverType,
            IntPtr software,
            uint flags,
            IntPtr pFeatureLevels,
            uint featureLevels,
            uint sdkVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        private const int D3D_DRIVER_TYPE_HARDWARE = 1;
        private const int D3D_DRIVER_TYPE_WARP = 5;
        private const uint D3D11_SDK_VERSION = 7;

        public static IDirect3DDevice? CreateDevice()
        {
            try
            {
                IntPtr d3dDevice = IntPtr.Zero;
                IntPtr d3dContext = IntPtr.Zero;
                int featureLevel = 0;

                // Try hardware first
                int result = D3D11CreateDevice(
                    IntPtr.Zero,
                    D3D_DRIVER_TYPE_HARDWARE,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    D3D11_SDK_VERSION,
                    out d3dDevice,
                    out featureLevel,
                    out d3dContext);

                // If hardware failed, try WARP
                if (result != 0)
                {
                    result = D3D11CreateDevice(
                        IntPtr.Zero,
                        D3D_DRIVER_TYPE_WARP,
                        IntPtr.Zero,
                        0,
                        IntPtr.Zero,
                        0,
                        D3D11_SDK_VERSION,
                        out d3dDevice,
                        out featureLevel,
                        out d3dContext);
                }

                if (result != 0 || d3dDevice == IntPtr.Zero)
                    return null;

                // Convert to IDirect3DDevice using WinRT interop
                var device = MarshalInterface<IDirect3DDevice>.FromAbi(d3dDevice);
                return device;
            }
            catch
            {
                return null;
            }
        }
    }
}