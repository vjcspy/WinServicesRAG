using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Windows Graphics Capture API provider.
///     Modern Windows 10+ API that allows safe and efficient screen recording.
///     Implemented using P/Invoke for maximum compatibility with .NET 9.
/// </summary>
public class GDI(ILogger logger) : IScreenshotProvider
{

    // Windows Graphics Capture API constants and structs
    private const int ERROR_SUCCESS = 0;

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
            // Check if running on Windows 10 Build 1803+ (10.0.17134)
            Version version = Environment.OSVersion.Version;
            if (version.Major < 10 || version.Major == 10 && version.Build < 17134)
            {
                logger.LogWarning(message: "Windows Graphics Capture API requires Windows 10 Build 1803 or later");
                return false;
            }

            // Check if DWM composition is enabled
            if (DwmIsCompositionEnabled(pfEnabled: out bool isEnabled) == ERROR_SUCCESS && isEnabled)
            {
                logger.LogInformation(message: "Windows Graphics Capture API is available (DWM composition enabled)");
                return true;
            }
            logger.LogWarning(message: "Windows Graphics Capture API requires DWM composition to be enabled");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(exception: ex, message: "Error checking Windows Graphics Capture API availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        if (_isDisposed)
        {
            logger.LogError(message: "WindowsGraphicsCaptureProvider has been disposed");
            return null;
        }

        try
        {
            logger.LogDebug(message: "Starting Windows Graphics Capture API screenshot");

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

                                logger.LogInformation(message: $"Windows Graphics Capture API screenshot completed successfully, size: {result.Length} bytes");
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
            logger.LogError(exception: ex, message: "Error taking screenshot with Windows Graphics Capture API");
            return null;
        }
    }

    // P/Invoke declarations for Windows Graphics Capture API
    [DllImport(dllName: "dwmapi.dll", SetLastError = true)]
    private static extern int DwmIsCompositionEnabled([MarshalAs(unmanagedType: UnmanagedType.Bool)] out bool pfEnabled);

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
