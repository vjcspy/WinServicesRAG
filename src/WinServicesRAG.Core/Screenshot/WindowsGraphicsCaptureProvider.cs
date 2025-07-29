using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Windows Graphics Capture API provider using native C++ DLL.
///     High-performance screen capture with clean borders and modern Windows 10+ API.
///     Integrates with ScreenCaptureDLL.dll for optimal performance and compatibility.
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
    private static extern int CaptureScreenWithOptions([MarshalAs(UnmanagedType.LPWStr)] string outputPath, int hideBorder, int hideCursor);

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
            // var version = Environment.OSVersion.Version;
            // if (version.Major < 10 || (version.Major == 10 && version.Build < 18362))
            // {
            //     _logger?.LogWarning("Windows Graphics Capture API requires Windows 10 Build 1903 or later");
            //     return false;
            // }

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
        _logger?.LogInformation("Taking screenshot using Windows Graphics Capture API (native DLL)");
        
        string tempFilePath = string.Empty;
        
        try
        {
            // Generate temporary file path
            tempFilePath = Path.Combine(Path.GetTempPath(), $"screenshot_{Guid.NewGuid():N}.png");
            
            // Call native DLL with clean capture settings (hidden border, hidden cursor)
            var result = (ErrorCode)CaptureScreenWithOptions(tempFilePath, hideBorder: 1, hideCursor: 1);
            
            if (result == ErrorCode.Success)
            {
                // Read the captured file into byte array
                if (File.Exists(tempFilePath))
                {
                    byte[] imageData = File.ReadAllBytes(tempFilePath);
                    _logger?.LogInformation("Screenshot captured successfully. Size: {Size} bytes", imageData.Length);
                    return imageData;
                }
                else
                {
                    _logger?.LogError("Capture reported success but file was not created: {Path}", tempFilePath);
                    return null;
                }
            }
            else
            {
                // Get error description from DLL
                var errorPtr = GetErrorDescription((int)result);
                string errorDesc = errorPtr != IntPtr.Zero ? Marshal.PtrToStringUni(errorPtr) ?? result.ToString() : result.ToString();
                
                _logger?.LogError("Screenshot capture failed. Error: {ErrorCode} - {Description}", result, errorDesc);
                
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
            _logger?.LogError(ex, "Unexpected error during Windows Graphics Capture API screenshot");
            return null;
        }
        finally
        {
            // Clean up temporary file
            try
            {
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete temporary screenshot file: {Path}", tempFilePath);
            }
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
