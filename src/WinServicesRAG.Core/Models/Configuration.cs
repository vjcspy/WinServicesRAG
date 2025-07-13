namespace WinServicesRAG.Core.Models;

/// <summary>
/// Configuration options for the API client
/// </summary>
public class ApiClientOptions
{
    public const string SectionName = "ApiClient";

    /// <summary>
    /// Base URL of the API server
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.example.com";

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Timeout for HTTP requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for failed requests
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum file size for uploads in bytes (default: 10MB)
    /// </summary>
    public long MaxUploadSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// User agent string for HTTP requests
    /// </summary>
    public string UserAgent { get; set; } = "WinServicesRAG/1.0";
}

/// <summary>
/// Job status constants
/// </summary>
public static class JobStatus
{
    public const string Pending = "PENDING";
    public const string TakeScreenshot = "TAKE_SCREEN_SHOT";
    public const string TakeScreenshotSuccess = "TAKE_SCREEN_SHOT_SUCCESS";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Error = "ERROR";
    public const string Cancelled = "CANCELLED";
}

/// <summary>
/// Job type constants
/// </summary>
public static class JobTypes
{
    public const string Screenshot = "SCREENSHOT";
    public const string SystemInfo = "SYSTEM_INFO";
    public const string FileOperation = "FILE_OPERATION";
    public const string CustomCommand = "CUSTOM_COMMAND";
}
