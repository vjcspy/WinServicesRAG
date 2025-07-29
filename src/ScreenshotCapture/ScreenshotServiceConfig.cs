namespace ScreenshotCapture;

/// <summary>
/// Configuration for the Screenshot Background Service
/// </summary>
public class ScreenshotServiceConfig
{
    public string WorkDirectory { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
    public bool Verbose { get; set; } = false;

    /// <summary>
    /// Enable image compression before upload
    /// </summary>
    public bool CompressionEnabled { get; set; } = true;

    /// <summary>
    /// JPEG compression quality (1-100, where 100 is highest quality)
    /// </summary>
    public int CompressionQuality { get; set; } = 75;

    /// <summary>
    /// Target compression ratio (0.1-1.0, where 1.0 means no compression)
    /// </summary>
    public double TargetCompressionRatio { get; set; } = 0.5;
}
