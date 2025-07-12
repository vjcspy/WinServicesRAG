using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Windows Graphics Capture API provider.
///     Modern Windows 10+ API - Infrastructure ready for CsWin32 implementation.
///     Currently simplified due to complex WinRT dependencies, but designed for easy future upgrade.
/// </summary>
public class WindowsGraphicsCaptureProvider : IScreenshotProvider, IDisposable
{
    private readonly ILogger _logger;
    private bool _isDisposed;

    // P/Invoke for DWM composition check
    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool pfEnabled);
    
    private const int ERROR_SUCCESS = 0;

    public WindowsGraphicsCaptureProvider(ILogger logger)
    {
        _logger = logger;
    }

    public string ProviderName => "Windows Graphics Capture API";

    public bool IsAvailable()
    {
        try
        {
            // Check Windows version requirement (Windows 10 Build 1803+)
            var version = Environment.OSVersion.Version;
            if (version.Major < 10 || (version.Major == 10 && version.Build < 17134))
            {
                _logger?.LogWarning("Windows Graphics Capture API requires Windows 10 Build 1803 or later");
                return false;
            }

            // Check if DWM composition is enabled
            var dwmResult = DwmIsCompositionEnabled(out bool isEnabled);
            if (dwmResult == ERROR_SUCCESS && isEnabled)
            {
                _logger?.LogInformation("Windows Graphics Capture API is available (DWM composition enabled)");
                
                // TODO: Full CsWin32 implementation pending - temporarily disabled for stability
                _logger?.LogWarning("Windows Graphics Capture API temporarily disabled - CsWin32 implementation in progress");
                return false;
            }
            else
            {
                _logger?.LogWarning("Windows Graphics Capture API requires DWM composition to be enabled");
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
        _logger?.LogWarning("Windows Graphics Capture API is currently disabled - implementation in progress");
        _logger?.LogInformation("Using DirectX or WinAPI providers for reliable screenshot capture");
        return null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        // TODO: Cleanup WinRT resources when implemented
        
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /* 
     * TODO: Future CsWin32 Implementation Structure
     * 
     * Required components:
     * - GraphicsCaptureItem for capture source
     * - Direct3D11CaptureFramePool for frame management  
     * - GraphicsCaptureSession for capture control
     * - DirectX/WinRT interop for texture processing
     * 
     * Implementation steps:
     * 1. Resolve CsWin32 namespace generation issues
     * 2. Setup proper WinRT context and DispatcherQueue  
     * 3. Implement DirectX texture to bitmap conversion
     * 4. Add async capture with proper error handling
     * 5. Integrate with existing provider chain
     */
}
