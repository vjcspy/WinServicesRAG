using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Windows Graphics Capture API provider.
///     Newest API, good for capturing modern applications.
///     NOTE: This provider is currently disabled as it requires Windows Runtime components
///     that are not compatible with .NET 9.0 targeting in this configuration.
/// </summary>
public class WindowsGraphicsCaptureProvider : IScreenshotProvider, IDisposable
{
    /// <summary>
    ///     Name of the provider for identification and logging
    /// </summary>
    public string ProviderName => "Windows Graphics Capture API (Disabled)";

    /// <summary>
    ///     Dispose pattern implementation
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose in disabled state
    }

    /// <summary>
    ///     Checks if this provider is available on the current system
    /// </summary>
    /// <returns>True if provider can be used, false otherwise</returns>
    public bool IsAvailable()
    {
        // Check current session info
        uint sessionId = GetCurrentSessionId();
        Console.WriteLine($"[WinGraphicsCapture] Current session ID: {sessionId}");
        
        // Session 0 is the system session, user sessions are 1+
        if (sessionId == 0)
        {
            Console.WriteLine("[WinGraphicsCapture] WARNING: Running in Session 0 (System). Graphics Capture may not work properly.");
            return false; // Explicitly return false for Session 0
        }

        // Currently disabled due to WinRT compatibility issues
        Console.WriteLine("[WinGraphicsCapture] Provider is currently disabled due to WinRT compatibility issues with .NET 9.0");
        return false;
    }

    /// <summary>
    ///     Captures a screenshot of the primary display
    /// </summary>
    /// <returns>PNG image data as byte array, or null if capture fails</returns>
    public byte[]? TakeScreenshot()
    {
        Console.WriteLine("[WinGraphicsCapture] Provider is currently disabled");
        return null;
    }

    // P/Invoke declarations
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
