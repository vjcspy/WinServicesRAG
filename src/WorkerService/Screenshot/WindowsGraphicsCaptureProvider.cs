// using System.Runtime.InteropServices;
// using System.Runtime.InteropServices.WindowsRuntime;
// using Windows.Graphics.Capture;
// using Windows.Graphics.DirectX;
// using Windows.Graphics.DirectX.Direct3D11;
// using Windows.Graphics.Imaging;
// using Windows.Storage.Streams;
// using WinRT;
//
// namespace WorkerService.Screenshot
// {
//     /// <summary>
//     /// Windows Graphics Capture API screenshot provider
//     /// Modern, secure, and efficient capture method for Windows 10+
//     /// Recommended by Microsoft, captures most content including UAC and UWP apps
//     /// </summary>
//     public class WindowsGraphicsCaptureProvider : IScreenshotProvider
//     {
//         public string ProviderName => "Windows Graphics Capture API";
//
//         private readonly object _lock = new object();
//         private bool _isCapturing = false;
//
//         public bool IsAvailable()
//         {
//             try
//             {
//                 if (!GraphicsCaptureSession.IsSupported())
//                 {
//                     return false;
//                 }
//
//                 var version = Environment.OSVersion.Version;
//                 if (version.Major < 10 || (version.Major == 10 && version.Build < 17134))
//                 {
//                     return false;
//                 }
//
//                 using var d3dDevice = Direct3D11Helpers.CreateDevice();
//                 if (d3dDevice == null)
//                 {
//                     return false;
//                 }
//
//                 var captureItem = CaptureHelper.CreateItemForPrimaryMonitor();
//                 return captureItem != null;
//             }
//             catch
//             {
//                 return false;
//             }
//         }
//
//         public byte[]? TakeScreenshot()
//         {
//             lock (_lock)
//             {
//                 if (_isCapturing)
//                 {
//                     return null;
//                 }
//                 _isCapturing = true;
//                 try
//                 {
//                     return CaptureScreenAsync().GetAwaiter().GetResult();
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"[Error] Windows Graphics Capture failed: {ex.Message}");
//                     return null;
//                 }
//                 finally
//                 {
//                     _isCapturing = false;
//                 }
//             }
//         }
//
//         private async Task<byte[]?> CaptureScreenAsync()
//         {
//             GraphicsCaptureSession? session = null;
//             Direct3D11CaptureFramePool? framePool = null;
//             IDirect3DDevice? d3dDevice = null;
//
//             try
//             {
//                 var captureItem = CaptureHelper.CreateItemForPrimaryMonitor();
//                 if (captureItem == null)
//                 {
//                     Console.WriteLine("[Error] Failed to create capture item for primary monitor.");
//                     return null;
//                 }
//
//                 d3dDevice = Direct3D11Helpers.CreateDevice();
//                 if (d3dDevice == null)
//                 {
//                      Console.WriteLine("[Error] Failed to create Direct3D device.");
//                     return null;
//                 }
//
//                 framePool = Direct3D11CaptureFramePool.Create(
//                     d3dDevice,
//                     DirectXPixelFormat.B8G8R8A8UIntNormalized,
//                     1,
//                     captureItem.Size);
//
//                 var tcs = new TaskCompletionSource<byte[]?>();
//
//                 framePool.FrameArrived += (sender, args) =>
//                 {
//                     try
//                     {
//                         using var frame = sender.TryGetNextFrame();
//                         if (frame == null)
//                         {
//                             tcs.TrySetResult(null);
//                             return;
//                         }
//                         var result = ProcessFrameAsync(frame).GetAwaiter().GetResult();
//                         tcs.TrySetResult(result);
//                     }
//                     catch (Exception ex)
//                     {
//                         tcs.TrySetException(ex);
//                     }
//                 };
//
//                 session = framePool.CreateCaptureSession(captureItem);
//                 session.StartCapture();
//
//                 var timeoutTask = Task.Delay(5000);
//                 var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
//
//                 if (completedTask == timeoutTask)
//                 {
//                     Console.WriteLine("[Warning] Windows Graphics Capture timed out.");
//                     return null;
//                 }
//
//                 return await tcs.Task;
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"[Error] CaptureScreenAsync error: {ex.ToString()}");
//                 return null;
//             }
//             finally
//             {
//                 session?.Dispose();
//                 framePool?.Dispose();
//                 (d3dDevice as IDisposable)?.Dispose();
//             }
//         }
//
//         private async Task<byte[]?> ProcessFrameAsync(Direct3D11CaptureFrame frame)
//         {
//             try
//             {
//                 var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
//                 return await ConvertSoftwareBitmapToByteArrayAsync(softwareBitmap);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"[Error] Frame processing error: {ex.Message}");
//                 return null;
//             }
//         }
//
//         private async Task<byte[]> ConvertSoftwareBitmapToByteArrayAsync(SoftwareBitmap softwareBitmap)
//         {
//             using var stream = new InMemoryRandomAccessStream();
//             var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
//             encoder.SetSoftwareBitmap(softwareBitmap);
//             await encoder.FlushAsync();
//
//             var bytes = new byte[stream.Size];
//             await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
//             return bytes;
//         }
//     }
//
//     /// <summary>
//     /// Helper class for creating capture items
//     /// </summary>
//     internal static class CaptureHelper
//     {
//         [ComImport]
//         [Guid("79C3F95B-31F7-4EC2-A464-632F5D30E760")]
//         [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
//         private interface IGraphicsCaptureItemInterop
//         {
//             IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
//             IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
//         }
//
//         [DllImport("user32.dll")]
//         private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
//
//         [StructLayout(LayoutKind.Sequential)]
//         private struct POINT { public int X; public int Y; }
//         private const uint MONITOR_DEFAULTTOPRIMARY = 1;
//
//         public static GraphicsCaptureItem? CreateItemForPrimaryMonitor()
//         {
//             IntPtr itemPointer = IntPtr.Zero;
//             try
//             {
//                 var factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
//                 var interop = (IGraphicsCaptureItemInterop)factory;
//
//                 var primaryMonitor = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
//                 if (primaryMonitor == IntPtr.Zero) return null;
//
//                 var itemGuid = typeof(GraphicsCaptureItem).GUID;
//                 itemPointer = interop.CreateForMonitor(primaryMonitor, ref itemGuid);
//
//                 return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
//             }
//             catch
//             {
//                 return null;
//             }
//             finally
//             {
//                 if(itemPointer != IntPtr.Zero)
//                 {
//                     Marshal.Release(itemPointer);
//                 }
//             }
//         }
//     }
//
//     /// <summary>
//     /// Helper for Direct3D operations. This is a critical part for WGC to work.
//     /// </summary>
//     internal static class Direct3D11Helpers
//     {
//         [DllImport("d3d11.dll", SetLastError = true)]
//         private static extern int D3D11CreateDevice(IntPtr pAdapter, int DriverType, IntPtr Software, uint Flags, IntPtr pFeatureLevels, uint FeatureLevels, uint SDKVersion, out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);
//
//         [DllImport("d3d11.dll", SetLastError = true)]
//         private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
//
//         private const int D3D_DRIVER_TYPE_HARDWARE = 1;
//         private const int D3D_DRIVER_TYPE_WARP = 5;
//         private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
//         private const uint D3D11_SDK_VERSION = 7;
//
//         public static IDirect3DDevice? CreateDevice()
//         {
//             IntPtr d3dDevice = IntPtr.Zero;
//             IntPtr d3dContext = IntPtr.Zero;
//             IntPtr dxgiDevice = IntPtr.Zero;
//             IntPtr inspectableDevice = IntPtr.Zero;
//
//             try
//             {
//                 // Try to create a hardware device
//                 int hr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, D3D11_CREATE_DEVICE_BGRA_SUPPORT, IntPtr.Zero, 0, D3D11_SDK_VERSION, out d3dDevice, out _, out d3dContext);
//
//                 // If hardware creation fails, try WARP (software)
//                 if (hr != 0)
//                 {
//                     hr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_WARP, IntPtr.Zero, D3D11_CREATE_DEVICE_BGRA_SUPPORT, IntPtr.Zero, 0, D3D11_SDK_VERSION, out d3dDevice, out _, out d3dContext);
//                 }
//
//                 if (hr != 0) return null;
//
//                 // Query for the IDXGIDevice interface
//                 Guid dxgiDeviceGuid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
//                 if (Marshal.QueryInterface(d3dDevice, ref dxgiDeviceGuid, out dxgiDevice) != 0) return null;
//
//                 // Create the WinRT IDirect3DDevice from the IDXGIDevice
//                 if (CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectableDevice) != 0) return null;
//
//                 return MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice);
//             }
//             catch
//             {
//                 return null;
//             }
//             finally
//             {
//                 // Release all COM pointers
//                 if (inspectableDevice != IntPtr.Zero) Marshal.Release(inspectableDevice);
//                 if (dxgiDevice != IntPtr.Zero) Marshal.Release(dxgiDevice);
//                 if (d3dContext != IntPtr.Zero) Marshal.Release(d3dContext);
//                 if (d3dDevice != IntPtr.Zero) Marshal.Release(d3dDevice);
//             }
//         }
//     }
// }