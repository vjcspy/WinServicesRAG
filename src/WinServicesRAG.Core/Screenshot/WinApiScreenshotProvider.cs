using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Traditional WinAPI (GDI) screenshot provider.
///     Wide compatibility, works on all Windows versions.
///     Uses BitBlt for desktop capture and PrintWindow for specific windows.
/// </summary>
public class WinApiScreenshotProvider : IScreenshotProvider, IDisposable
{
    private readonly ILogger? _logger;
    private bool _isDisposed;

    public WinApiScreenshotProvider(ILogger? logger = null)
    {
        _logger = logger;
    }

    public string ProviderName => "WinAPI (BitBlt + PrintWindow)";

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public bool IsAvailable()
    {
        try
        {
            // WinAPI is available on all Windows versions
            var desktopWindow = GetDesktopWindow();
            bool isAvailable = desktopWindow != IntPtr.Zero;
            
            _logger?.LogInformation("WinAPI screenshot provider availability: {IsAvailable}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check WinAPI availability");
            return false;
        }
    }

    public byte[]? TakeScreenshot()
    {
        try
        {
            _logger?.LogDebug("Attempting to capture screenshot using WinAPI BitBlt");

            // Get screen dimensions
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            if (screenWidth <= 0 || screenHeight <= 0)
            {
                _logger?.LogError("Invalid screen dimensions: {Width}x{Height}", screenWidth, screenHeight);
                return null;
            }

            // Get device context for the desktop
            IntPtr desktopDC = GetDC(IntPtr.Zero);
            if (desktopDC == IntPtr.Zero)
            {
                _logger?.LogError("Failed to get desktop device context");
                return null;
            }

            try
            {
                // Create a compatible device context and bitmap
                IntPtr memoryDC = CreateCompatibleDC(desktopDC);
                if (memoryDC == IntPtr.Zero)
                {
                    _logger?.LogError("Failed to create compatible device context");
                    return null;
                }

                try
                {
                    IntPtr bitmap = CreateCompatibleBitmap(desktopDC, screenWidth, screenHeight);
                    if (bitmap == IntPtr.Zero)
                    {
                        _logger?.LogError("Failed to create compatible bitmap");
                        return null;
                    }

                    try
                    {
                        // Select the bitmap into the memory device context
                        IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

                        // Copy the desktop to the bitmap
                        bool success = BitBlt(memoryDC, 0, 0, screenWidth, screenHeight, 
                                            desktopDC, 0, 0, SRCCOPY);

                        if (!success)
                        {
                            _logger?.LogError("BitBlt operation failed");
                            return null;
                        }

                        // Convert to .NET Bitmap and save as PNG
                        var image = Image.FromHbitmap(bitmap);
                        using (image)
                        {
                            using var memoryStream = new MemoryStream();
                            image.Save(memoryStream, ImageFormat.Png);
                            _logger?.LogDebug("Successfully captured screenshot using WinAPI BitBlt: {Size} bytes", 
                                            memoryStream.Length);
                            return memoryStream.ToArray();
                        }
                    }
                    finally
                    {
                        DeleteObject(bitmap);
                    }
                }
                finally
                {
                    DeleteDC(memoryDC);
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, desktopDC);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to capture screenshot using WinAPI");
            return null;
        }
    }

    // WinAPI constants
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint SRCCOPY = 0x00CC0020;

    // WinAPI function imports
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGdiObj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, 
                                     IntPtr hSrcDC, int xSrc, int ySrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
