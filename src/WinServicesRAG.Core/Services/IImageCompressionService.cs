using System;
using System.Threading.Tasks;

namespace WinServicesRAG.Core.Services;

/// <summary>
/// Configuration settings for image compression
/// </summary>
public class CompressionSettings
{
    /// <summary>
    /// JPEG quality level (1-100, where 100 is highest quality)
    /// </summary>
    public int JpegQuality { get; set; } = 75;

    /// <summary>
    /// Maximum width for resizing (0 means no resize)
    /// </summary>
    public int MaxWidth { get; set; } = 0;

    /// <summary>
    /// Maximum height for resizing (0 means no resize)
    /// </summary>
    public int MaxHeight { get; set; } = 0;

    /// <summary>
    /// Whether to prefer JPEG over PNG for compression
    /// </summary>
    public bool PreferJpegOverPng { get; set; } = false;
}

/// <summary>
/// Result of image compression operation
/// </summary>
public class CompressionResult
{
    /// <summary>
    /// Compressed image data
    /// </summary>
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Original image size in bytes
    /// </summary>
    public long OriginalSize { get; set; }

    /// <summary>
    /// Compressed image size in bytes
    /// </summary>
    public long CompressedSize { get; set; }

    /// <summary>
    /// Compression ratio (compressed size / original size)
    /// </summary>
    public double CompressionRatio => OriginalSize > 0 ? (double)CompressedSize / OriginalSize : 1.0;

    /// <summary>
    /// Size reduction percentage
    /// </summary>
    public double SizeReductionPercentage => (1.0 - CompressionRatio) * 100;

    /// <summary>
    /// Final content type after compression
    /// </summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>
    /// Whether compression was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if compression failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Interface for image compression services
/// </summary>
public interface IImageCompressionService
{
    /// <summary>
    /// Compresses an image with the specified settings
    /// </summary>
    /// <param name="imageData">Original image data</param>
    /// <param name="settings">Compression settings</param>
    /// <returns>Compression result</returns>
    Task<CompressionResult> CompressImageAsync(byte[] imageData, CompressionSettings settings);

    /// <summary>
    /// Compresses an image optimally for upload with default settings
    /// </summary>
    /// <param name="imageData">Original image data</param>
    /// <param name="quality">JPEG quality (1-100)</param>
    /// <param name="format"></param>
    /// <returns>Compression result</returns>
    Task<CompressionResult> CompressForUploadAsync(byte[] imageData, int quality = 75, string format = "png");
}
