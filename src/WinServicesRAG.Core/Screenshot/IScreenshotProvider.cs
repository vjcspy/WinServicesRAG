namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Interface for screenshot providers.
///     Each provider implements a different capture method.
/// </summary>
public interface IScreenshotProvider : IDisposable
{
    /// <summary>
    ///     Name of the provider for identification and logging
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    ///     Checks if this provider is available on the current system
    /// </summary>
    /// <returns>True if provider can be used, false otherwise</returns>
    bool IsAvailable();

    /// <summary>
    ///     Captures a screenshot of the primary display
    /// </summary>
    /// <returns>PNG image data as byte array, or null if capture fails</returns>
    byte[]? TakeScreenshot();
}
