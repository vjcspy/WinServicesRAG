using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace WorkerService.Screenshot;

/// <summary>
///     Fallback provider using classic WinAPI (GDI) calls.
///     Most compatible but may not capture overlays or protected windows.
/// </summary>
public class WinApiScreenshotProvider : IScreenshotProvider
{
    public string ProviderName
    {
        get
        {
            return "WinAPI GDI";
        }
    }

    public bool IsAvailable()
    {
        // GDI is always available on Windows.
        return true;
    }

    public byte[]? TakeScreenshot()
    {
        IntPtr hdcScreen = IntPtr.Zero;
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;

        try
        {
            hdcScreen = User32.GetDC(hWnd: User32.GetDesktopWindow());
            if (hdcScreen == IntPtr.Zero) return null;

            hdcMem = Gdi32.CreateCompatibleDC(hdc: hdcScreen);
            if (hdcMem == IntPtr.Zero) return null;

            int width = User32.GetSystemMetrics(smIndex: SystemMetric.SM_CXSCREEN);
            int height = User32.GetSystemMetrics(smIndex: SystemMetric.SM_CYSCREEN);

            hBitmap = Gdi32.CreateCompatibleBitmap(hdc: hdcScreen, nWidth: width, nHeight: height);
            if (hBitmap == IntPtr.Zero) return null;

            hOldBitmap = Gdi32.SelectObject(hdc: hdcMem, hgdiobj: hBitmap);

            if (!Gdi32.BitBlt(hdcDest: hdcMem, nXDest: 0, nYDest: 0, nWidth: width, nHeight: height, hdcSrc: hdcScreen, nXSrc: 0, nYSrc: 0, dwRop: TernaryRasterOperations.SRCCOPY))
            {
                return null;
            }

            Gdi32.SelectObject(hdc: hdcMem, hgdiobj: hOldBitmap); // Select back the old bitmap

            using (Bitmap bitmap = Image.FromHbitmap(hbitmap: hBitmap))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(stream: ms, format: ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(value: $"[Error] WinAPI Provider failed: {ex.Message}");
            return null;
        }
        finally
        {
            // IMPORTANT: Release all GDI resources
            if (hOldBitmap != IntPtr.Zero) Gdi32.SelectObject(hdc: hdcMem, hgdiobj: hOldBitmap);
            if (hBitmap != IntPtr.Zero) Gdi32.DeleteObject(hObject: hBitmap);
            if (hdcMem != IntPtr.Zero) Gdi32.DeleteDC(hdc: hdcMem);
            if (hdcScreen != IntPtr.Zero) User32.ReleaseDC(hWnd: User32.GetDesktopWindow(), hDC: hdcScreen);
        }
    }

    #region P/Invoke Definitions

    private static class User32
    {
        [DllImport(dllName: "user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport(dllName: "user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport(dllName: "user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport(dllName: "user32.dll")]
        public static extern int GetSystemMetrics(SystemMetric smIndex);
    }

    private static class Gdi32
    {
        [DllImport(dllName: "gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, TernaryRasterOperations dwRop);

        [DllImport(dllName: "gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport(dllName: "gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport(dllName: "gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport(dllName: "gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport(dllName: "gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    }

    private enum SystemMetric
    {
        SM_CXSCREEN = 0,
        SM_CYSCREEN = 1
    }

    private enum TernaryRasterOperations : uint
    {
        SRCCOPY = 0x00CC0020
    }

    #endregion

}
