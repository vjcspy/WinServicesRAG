using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Windows Graphics Capture API provider using native C++ DLL.
///     High-performance screen capture with clean borders and modern Windows 10+ API.
///     Integrates with ScreenCaptureDLL.dll for optimal performance and compatibility.
///     Uses direct memory capture for maximum performance.
/// </summary>
public class WindowsGraphicsCaptureProvider : IScreenshotProvider, IDisposable
{
    private readonly ILogger _logger;
    private bool _isDisposed;

    // Error codes from ScreenCaptureDLL
    public enum ErrorCode : int
    {
        Success = 0,
        InitializationFailed = 1,
        CaptureItemCreationFailed = 2,
        CaptureSessionFailed = 3,
        TextureProcessingFailed = 4,
        FileSaveFailed = 5,
        TimeoutError = 6,
        InvalidParameter = 97,
        UnknownError = 99
    }

    // P/Invoke declarations for ScreenCaptureDLL.dll
    [DllImport("ScreenCaptureDLL.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CaptureScreenToMemory(out IntPtr outputBuffer, out uint bufferSize, int hideBorder, int hideCursor);

    [DllImport("ScreenCaptureDLL.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern void FreeBuffer(IntPtr buffer);

    [DllImport("ScreenCaptureDLL.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetErrorDescription(int errorCode);

    [DllImport("ScreenCaptureDLL.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetLibraryVersion();

    // P/Invoke for DWM composition check
    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool pfEnabled);
    
    private const int ERROR_SUCCESS = 0;

    public WindowsGraphicsCaptureProvider(ILogger logger)
    {
        _logger = logger;
    }

    public string ProviderName => "WGC";

    public bool IsAvailable()
    {
        try
        {
            // Check Windows version requirement (Windows 10 Build 1903+)
            var version = Environment.OSVersion.Version;
            if (version.Major < 10 || (version.Major == 10 && version.Build < 18362))
            {
                _logger?.LogWarning("Windows Graphics Capture API requires Windows 10 Build 1903 or later");
                return false;
            }

            // Check if DWM composition is enabled
            var dwmResult = DwmIsCompositionEnabled(out bool isEnabled);
            if (dwmResult != ERROR_SUCCESS || !isEnabled)
            {
                _logger?.LogWarning("Windows Graphics Capture API requires DWM composition to be enabled");
                return false;
            }

            // Test DLL availability with a simple call
            try
            {
                var versionPtr = GetLibraryVersion();
                if (versionPtr != IntPtr.Zero)
                {
                    string version_info = Marshal.PtrToStringUni(versionPtr) ?? "Unknown";
                    _logger?.LogInformation("Windows Graphics Capture API is available. DLL Version: {Version}", version_info);
                    return true;
                }
                else
                {
                    _logger?.LogWarning("Failed to get version from ScreenCaptureDLL.dll");
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                _logger?.LogWarning("ScreenCaptureDLL.dll not found. Ensure the DLL is in the application directory");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error testing ScreenCaptureDLL.dll availability");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking Windows Graphics Capture API availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        _logger?.LogInformation("Taking screenshot using Windows Graphics Capture API (direct memory capture)");
        
        try
        {
            // Call native DLL memory capture with clean capture settings (hidden border, hidden cursor)
            var result = (ErrorCode)CaptureScreenToMemory(out IntPtr buffer, out uint bufferSize, hideBorder: 1, hideCursor: 1);
            
            if (result == ErrorCode.Success && buffer != IntPtr.Zero && bufferSize > 0)
            {
                try
                {
                    // Copy data from native memory to managed byte array
                    byte[] imageData = new byte[bufferSize];
                    Marshal.Copy(buffer, imageData, 0, (int)bufferSize);
                    
                    _logger?.LogInformation("Screenshot captured successfully to memory. Size: {Size} bytes", imageData.Length);
                    return imageData;
                }
                finally
                {
                    // Always free the native buffer
                    FreeBuffer(buffer);
                }
            }
            else
            {
                // Free buffer even on error (if allocated)
                if (buffer != IntPtr.Zero)
                {
                    FreeBuffer(buffer);
                }
                
                // Get error description from DLL
                var errorPtr = GetErrorDescription((int)result);
                string errorDesc = errorPtr != IntPtr.Zero ? Marshal.PtrToStringUni(errorPtr) ?? result.ToString() : result.ToString();
                
                _logger?.LogError("Screenshot memory capture failed. Error: {ErrorCode} - {Description}", result, errorDesc);
                
                // Log specific guidance for common errors
                if (result == ErrorCode.TimeoutError)
                {
                    _logger?.LogWarning("Timeout error may be resolved by running with administrator privileges");
                }
                
                return null;
            }
        }
        catch (DllNotFoundException)
        {
            _logger?.LogError("ScreenCaptureDLL.dll not found. Ensure the native DLL is deployed with the application");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during Windows Graphics Capture API memory capture");
            return null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        // No native resources to cleanup - DLL handles its own lifecycle
        
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
