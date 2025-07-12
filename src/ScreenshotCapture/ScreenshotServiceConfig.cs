namespace ScreenshotCapture;

/// <summary>
/// Configuration for the Screenshot Background Service
/// </summary>
public class ScreenshotServiceConfig
{
    public string WorkDirectory { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
    public bool Verbose { get; set; } = false;
}
