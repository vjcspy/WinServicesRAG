using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Traditional Windows GDI-based screenshot provider.
///     Most compatible but slower than hardware-accelerated alternatives.
/// </summary>
public class WinApiScreenshotProvider : IScreenshotProvider
{
    public string ProviderName => "Windows GDI (BitBlt)";

    public void Dispose()
    {
        // No resources to dispose for GDI provider
    }

    public bool IsAvailable()
    {
        try
        {
            // Check current session info
            uint sessionId = GetCurrentSessionId();
            Console.WriteLine($"[WinAPI] Current session ID: {sessionId}");
            
            // Session 0 is the system session, user sessions are 1+
            if (sessionId == 0)
            {
                Console.WriteLine("[WinAPI] WARNING: Running in Session 0 (System). GDI capture may show Session 0 desktop (usually black).");
                Console.WriteLine("[WinAPI] This is expected for Windows Services. GDI will capture Session 0 desktop, not user desktop.");
                // Note: We still return true because GDI can technically work in Session 0,
                // it just captures the system session instead of user session
            }

            // GDI is always available on Windows
            // Test by getting screen dimensions
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            bool isAvailable = screenWidth > 0 && screenHeight > 0;
            Console.WriteLine($"[WinAPI] Screen dimensions: {screenWidth}x{screenHeight}, IsAvailable: {isAvailable}");
            
            return isAvailable;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WinAPI] IsAvailable check failed: {ex.Message}");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        IntPtr hdcSrc = IntPtr.Zero;
        IntPtr hdcDest = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;

        try
        {
            Console.WriteLine("[WinAPI] Starting GDI screenshot capture...");

            // Get the device context of the entire screen
            hdcSrc = GetDC(IntPtr.Zero);
            if (hdcSrc == IntPtr.Zero)
            {
                Console.WriteLine("[WinAPI] Failed to get screen device context");
                return null;
            }

            // Get screen dimensions
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            Console.WriteLine($"[WinAPI] Screen dimensions: {screenWidth}x{screenHeight}");

            if (screenWidth <= 0 || screenHeight <= 0)
            {
                Console.WriteLine("[WinAPI] Invalid screen dimensions");
                return null;
            }

            // Create a device context we can copy to
            hdcDest = CreateCompatibleDC(hdcSrc);
            if (hdcDest == IntPtr.Zero)
            {
                Console.WriteLine("[WinAPI] Failed to create compatible device context");
                return null;
            }

            // Create a bitmap we can copy it to, using GetDeviceCaps to get the width/height
            hBitmap = CreateCompatibleBitmap(hdcSrc, screenWidth, screenHeight);
            if (hBitmap == IntPtr.Zero)
            {
                Console.WriteLine("[WinAPI] Failed to create compatible bitmap");
                return null;
            }

            // Select the bitmap object into the device context
            IntPtr hOld = SelectObject(hdcDest, hBitmap);
            if (hOld == IntPtr.Zero)
            {
                Console.WriteLine("[WinAPI] Failed to select bitmap into device context");
                return null;
            }

            // BitBlt screen to bitmap
            bool bitBltResult = BitBlt(hdcDest, 0, 0, screenWidth, screenHeight, hdcSrc, 0, 0, SRCCOPY);
            if (!bitBltResult)
            {
                Console.WriteLine("[WinAPI] BitBlt operation failed");
                return null;
            }

            Console.WriteLine("[WinAPI] BitBlt completed successfully, converting to managed bitmap...");

            // Get a .NET Bitmap object from the unmanaged HBITMAP
            using (Bitmap bitmap = Image.FromHbitmap(hBitmap))
            {
                // Convert to PNG byte array
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    byte[] result = ms.ToArray();
                    Console.WriteLine($"[WinAPI] Screenshot completed successfully, size: {result.Length} bytes");
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WinAPI] Screenshot failed: {ex.Message}");
            Console.WriteLine($"[WinAPI] Exception type: {ex.GetType().Name}");
            return null;
        }
        finally
        {
            // Clean up
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
            if (hdcDest != IntPtr.Zero)
                DeleteDC(hdcDest);
            if (hdcSrc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, hdcSrc);
        }
    }

    // P/Invoke declarations
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();
    
    [DllImport("kernel32.dll")]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    private const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter
    private const int SM_CXSCREEN = 0; // System metric for screen width
    private const int SM_CYSCREEN = 1; // System metric for screen height

    private static uint GetCurrentSessionId()
    {
        uint processId = GetCurrentProcessId();
        ProcessIdToSessionId(processId, out uint sessionId);
        return sessionId;
    }
}
