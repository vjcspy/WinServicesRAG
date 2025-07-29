using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     GDI-based screenshot provider.
///     Traditional Windows GDI API that provides reliable screen capture functionality.
///     Implemented using P/Invoke for maximum compatibility with .NET 9.
/// </summary>
public class GDI(ILogger logger) : IScreenshotProvider
{
    // BitBlt constants
    private const int SRCCOPY = 0x00CC0020;
    private bool _isDisposed;

    public string ProviderName
    {
        get
        {
            return "GDI";
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            GC.SuppressFinalize(obj: this);
        }
    }

    public bool IsAvailable()
    {
        try
        {
            // Check if running on Windows (GDI is Windows-specific)
            if (!OperatingSystem.IsWindows())
            {
                logger.LogWarning(message: "GDI screenshot provider is only available on Windows");
                return false;
            }

            // Basic test to ensure GDI functions are accessible
            IntPtr desktopWindow = GetDesktopWindow();
            if (desktopWindow == IntPtr.Zero)
            {
                logger.LogWarning(message: "GDI screenshot provider cannot access desktop window");
                return false;
            }

            logger.LogInformation(message: "GDI screenshot provider is available");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(exception: ex, message: "Error checking GDI screenshot provider availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        if (_isDisposed)
        {
            logger.LogError(message: "GDI screenshot provider has been disposed");
            return null;
        }

        try
        {
            logger.LogDebug(message: "Starting GDI screenshot capture");

            // Get desktop window and its dimensions
            IntPtr desktopWindow = GetDesktopWindow();
            if (desktopWindow == IntPtr.Zero)
            {
                logger.LogError(message: "Failed to get desktop window handle");
                return null;
            }

            if (!GetWindowRect(hWnd: desktopWindow, lpRect: out RECT desktopRect))
            {
                logger.LogError(message: "Failed to get desktop window rectangle");
                return null;
            }

            int screenWidth = desktopRect.Right - desktopRect.Left;
            int screenHeight = desktopRect.Bottom - desktopRect.Top;

            logger.LogDebug(message: $"Desktop dimensions: {screenWidth}x{screenHeight}");

            // Get desktop DC
            IntPtr desktopDC = GetDC(hWnd: desktopWindow);
            if (desktopDC == IntPtr.Zero)
            {
                logger.LogError(message: "Failed to get desktop device context");
                return null;
            }

            try
            {
                // Create compatible DC and bitmap
                IntPtr memoryDC = CreateCompatibleDC(hDC: desktopDC);
                if (memoryDC == IntPtr.Zero)
                {
                    logger.LogError(message: "Failed to create compatible device context");
                    return null;
                }

                try
                {
                    IntPtr bitmap = CreateCompatibleBitmap(hDC: desktopDC, nWidth: screenWidth, nHeight: screenHeight);
                    if (bitmap == IntPtr.Zero)
                    {
                        logger.LogError(message: "Failed to create compatible bitmap");
                        return null;
                    }

                    try
                    {
                        // Select bitmap into memory DC
                        SelectObject(hDC: memoryDC, hgdiobj: bitmap);

                        // Copy screen to memory DC
                        bool success = BitBlt(hObject: memoryDC, nXDest: 0, nYDest: 0, nWidth: screenWidth, nHeight: screenHeight, hObjectSource: desktopDC, nXSrc: 0, nYSrc: 0, dwRop: SRCCOPY);
                        if (!success)
                        {
                            logger.LogError(message: "Failed to copy screen content using BitBlt");
                            return null;
                        }

                        // Convert to managed bitmap and save to byte array
                        using (Bitmap managedBitmap = Image.FromHbitmap(hbitmap: bitmap))
                        {
                            using (MemoryStream memoryStream = new MemoryStream())
                            {
                                managedBitmap.Save(stream: memoryStream, format: ImageFormat.Png);
                                byte[] result = memoryStream.ToArray();

                                logger.LogInformation(message: $"GDI screenshot completed successfully, size: {result.Length} bytes");
                                return result;
                            }
                        }
                    }
                    finally
                    {
                        DeleteObject(hObject: bitmap);
                    }
                }
                finally
                {
                    DeleteDC(hDC: memoryDC);
                }
            }
            finally
            {
                ReleaseDC(hWnd: desktopWindow, hDC: desktopDC);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(exception: ex, message: "Error taking screenshot with GDI");
            return null;
        }
    }

    // P/Invoke declarations for GDI API
    [DllImport(dllName: "user32.dll", SetLastError = true)]
    private static extern IntPtr GetDesktopWindow();

    [DllImport(dllName: "user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport(dllName: "user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport(dllName: "user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport(dllName: "gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport(dllName: "gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport(dllName: "gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hgdiobj);

    [DllImport(dllName: "gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

    [DllImport(dllName: "gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport(dllName: "gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(layoutKind: LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
