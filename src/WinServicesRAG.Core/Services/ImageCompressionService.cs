using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace WinServicesRAG.Core.Services;

/// <summary>
///     Image compression service implementation using System.Drawing
/// </summary>
public class ImageCompressionService : IImageCompressionService
{
    private readonly ILogger<ImageCompressionService> _logger;

    public ImageCompressionService(ILogger<ImageCompressionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CompressionResult> CompressImageAsync(byte[] imageData, CompressionSettings settings)
    {
        if (imageData == null || imageData.Length == 0)
        {
            return new CompressionResult
            {
                Success = false,
                ErrorMessage = "Image data is null or empty"
            };
        }

        return await Task.Run(function: () =>
        {
            try
            {
                _logger.LogDebug("Starting image compression, original size: {OriginalSize} bytes", imageData.Length);

                using var originalStream = new MemoryStream(imageData);
                using var originalBitmap = new Bitmap(originalStream);

                // Determine if we need to resize
                int targetWidth = settings.MaxWidth > 0 && originalBitmap.Width > settings.MaxWidth ? settings.MaxWidth : originalBitmap.Width;
                int targetHeight = settings.MaxHeight > 0 && originalBitmap.Height > settings.MaxHeight ? settings.MaxHeight : originalBitmap.Height;

                // Calculate proportional resize if needed
                if (settings.MaxWidth > 0 || settings.MaxHeight > 0)
                {
                    double scaleX = settings.MaxWidth > 0 ? (double)settings.MaxWidth / originalBitmap.Width : 1.0;
                    double scaleY = settings.MaxHeight > 0 ? (double)settings.MaxHeight / originalBitmap.Height : 1.0;
                    double scale = Math.Min(scaleX, scaleY);

                    if (scale < 1.0)
                    {
                        targetWidth = (int)(originalBitmap.Width * scale);
                        targetHeight = (int)(originalBitmap.Height * scale);
                        _logger.LogDebug("Resizing image from {OriginalWidth}x{OriginalHeight} to {TargetWidth}x{TargetHeight}",
                            originalBitmap.Width, originalBitmap.Height, targetWidth, targetHeight);
                    }
                }

                // Create resized bitmap if needed
                Bitmap workingBitmap;
                if (targetWidth != originalBitmap.Width || targetHeight != originalBitmap.Height)
                {
                    workingBitmap = new Bitmap(targetWidth, targetHeight);
                    using (var graphics = Graphics.FromImage(workingBitmap))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(originalBitmap, 0, 0, targetWidth, targetHeight);
                    }
                }
                else
                {
                    workingBitmap = new Bitmap(originalBitmap);
                }

                try
                {
                    // Try different compression strategies
                    var compressionResults = new List<(byte[] data, string contentType, string method)>();

                    // Strategy 1: Optimized PNG
                    byte[] pngResult = CompressToPng(workingBitmap);
                    compressionResults.Add((pngResult, "image/png", "PNG"));

                    // Strategy 2: JPEG compression (if preferred or as fallback)
                    if (settings.PreferJpegOverPng || settings.JpegQuality < 100)
                    {
                        byte[] jpegResult = CompressToJpeg(workingBitmap, settings.JpegQuality);
                        compressionResults.Add((jpegResult, "image/jpeg", $"JPEG-Q{settings.JpegQuality}"));
                    }

                    // Choose the best result (smallest size, but prefer PNG by default)
                    var bestResult = settings.PreferJpegOverPng
                        ? compressionResults.OrderBy(keySelector: r => r.data.Length).First()
                        : compressionResults.OrderBy(keySelector: r => r.contentType == "image/png" ? 0 : 1)
                            .ThenBy(keySelector: r => r.data.Length).First();

                    var result = new CompressionResult
                    {
                        ImageData = bestResult.data,
                        OriginalSize = imageData.Length,
                        CompressedSize = bestResult.data.Length,
                        ContentType = bestResult.contentType,
                        Success = true
                    };

                    _logger.LogInformation("Image compression completed using {Method}: {OriginalSize} → {CompressedSize} bytes ({ReductionPercentage:F1}% reduction)",
                        bestResult.method, result.OriginalSize, result.CompressedSize, result.SizeReductionPercentage);

                    return result;
                }
                finally
                {
                    if (workingBitmap != originalBitmap)
                    {
                        workingBitmap.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compress image");
                return new CompressionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    OriginalSize = imageData.Length
                };
            }
        });
    }

    public async Task<CompressionResult> CompressForUploadAsync(byte[] imageData, int quality = 75, string format = "png")
    {
        var settings = new CompressionSettings
        {
            JpegQuality = Math.Clamp(quality, 1, 100),
            PreferJpegOverPng = format == "jpg" // Keep PNG as default
        };

        return await CompressImageAsync(imageData, settings);
    }

    private byte[] CompressToPng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();

        // Use PNG encoder with optimized settings
        ImageCodecInfo? pngEncoder = GetEncoder(ImageFormat.Png);
        if (pngEncoder != null)
        {
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionLZW);

            bitmap.Save(stream, pngEncoder, encoderParams);
        }
        else
        {
            // Fallback to standard PNG save
            bitmap.Save(stream, ImageFormat.Png);
        }

        return stream.ToArray();
    }

    private byte[] CompressToJpeg(Bitmap bitmap, int quality)
    {
        using var stream = new MemoryStream();

        ImageCodecInfo? jpegEncoder = GetEncoder(ImageFormat.Jpeg);
        if (jpegEncoder != null)
        {
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            bitmap.Save(stream, jpegEncoder, encoderParams);
        }
        else
        {
            // Fallback to standard JPEG save
            bitmap.Save(stream, ImageFormat.Jpeg);
        }

        return stream.ToArray();
    }

    private ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        return codecs.FirstOrDefault(predicate: codec => codec.FormatID == format.Guid);
    }
}
