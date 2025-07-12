using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Traditional WinAPI (GDI) screenshot provider.
///     Wide compatibility, works on all Windows versions.
///     Uses BitBlt for desktop capture and PrintWindow for specific windows.
/// </summary>
public class WinApiScreenshotProvider(ILogger logger) : IScreenshotProvider, IDisposable
{

    // WinAPI constants
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint SRCCOPY = 0x00CC0020;
    private readonly ILogger? _logger = logger;
    private bool _isDisposed;

    public string ProviderName
    {
        get
        {
            return "WinAPI";
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
            // WinAPI is available on all Windows versions
            IntPtr desktopWindow = GetDesktopWindow();
            bool isAvailable = desktopWindow != IntPtr.Zero;

            _logger?.LogInformation(message: "WinAPI screenshot provider availability: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger?.LogError(exception: ex, message: "Failed to check WinAPI availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        try
        {
            _logger?.LogDebug(message: "Attempting to capture screenshot using WinAPI BitBlt");

            // Get screen dimensions
            int screenWidth = GetSystemMetrics(nIndex: SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(nIndex: SM_CYSCREEN);

            if (screenWidth <= 0 || screenHeight <= 0)
            {
                _logger?.LogError(message: "Invalid screen dimensions: {Width}x{Height}", screenWidth, screenHeight);
                return null;
            }

            // Get device context for the desktop
            IntPtr desktopDC = GetDC(hWnd: IntPtr.Zero);
            if (desktopDC == IntPtr.Zero)
            {
                _logger?.LogError(message: "Failed to get desktop device context");
                return null;
            }

            try
            {
                // Create a compatible device context and bitmap
                IntPtr memoryDC = CreateCompatibleDC(hDC: desktopDC);
                if (memoryDC == IntPtr.Zero)
                {
                    _logger?.LogError(message: "Failed to create compatible device context");
                    return null;
                }

                try
                {
                    IntPtr bitmap = CreateCompatibleBitmap(hDC: desktopDC, nWidth: screenWidth, nHeight: screenHeight);
                    if (bitmap == IntPtr.Zero)
                    {
                        _logger?.LogError(message: "Failed to create compatible bitmap");
                        return null;
                    }

                    try
                    {
                        // Select the bitmap into the memory device context
                        IntPtr oldBitmap = SelectObject(hDC: memoryDC, hGdiObj: bitmap);

                        // Copy the desktop to the bitmap
                        bool success = BitBlt(hDestDC: memoryDC, x: 0, y: 0, nWidth: screenWidth, nHeight: screenHeight,
                            hSrcDC: desktopDC, xSrc: 0, ySrc: 0, dwRop: SRCCOPY);

                        if (!success)
                        {
                            _logger?.LogError(message: "BitBlt operation failed");
                            return null;
                        }

                        // Convert to .NET Bitmap and save as PNG
                        Bitmap image = Image.FromHbitmap(hbitmap: bitmap);
                        using (image)
                        {
                            using MemoryStream memoryStream = new MemoryStream();
                            image.Save(stream: memoryStream, format: ImageFormat.Png);
                            _logger?.LogDebug(message: "Successfully captured screenshot using WinAPI BitBlt: {Size} bytes",
                                memoryStream.Length);
                            return memoryStream.ToArray();
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
                ReleaseDC(hWnd: IntPtr.Zero, hDC: desktopDC);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(exception: ex, message: "Failed to capture screenshot using WinAPI");
            return null;
        }
    }

    // WinAPI function imports
    [DllImport(dllName: "user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport(dllName: "user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport(dllName: "user32.dll")]
    private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport(dllName: "user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport(dllName: "gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport(dllName: "gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport(dllName: "gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGdiObj);

    [DllImport(dllName: "gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hDestDC,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hSrcDC,
        int xSrc,
        int ySrc,
        uint dwRop);

    [DllImport(dllName: "gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport(dllName: "gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
