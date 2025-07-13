namespace WinServicesRAG.Core.Screenshot;

/// <summary>
/// Screenshot capture result
/// </summary>
public class ScreenshotResult
{
    public bool Success { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProviderUsed { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Interface for screenshot management operations
/// </summary>
public interface IScreenshotManager : IDisposable
{
    /// <summary>
    /// Takes a screenshot using the first available provider
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Screenshot result</returns>
    Task<ScreenshotResult> TakeScreenshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a screenshot using a specific provider
    /// </summary>
    /// <param name="providerName">Name of the provider to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Screenshot result</returns>
    Task<ScreenshotResult> TakeScreenshotAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of all available providers
    /// </summary>
    /// <returns>Dictionary of provider names and their availability status</returns>
    Dictionary<string, bool> GetProviderStatus();

    /// <summary>
    /// Gets information about available providers
    /// </summary>
    /// <returns>List of provider information</returns>
    List<ProviderInfo> GetProviderInfo();
}

/// <summary>
/// Information about a screenshot provider
/// </summary>
public class ProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
