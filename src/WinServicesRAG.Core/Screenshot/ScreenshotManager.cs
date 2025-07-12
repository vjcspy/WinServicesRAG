using Microsoft.Extensions.Logging;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Manages screenshot providers with fallback strategy.
///     Tries providers in order of preference until one succeeds.
/// </summary>
public class ScreenshotManager : IDisposable
{
    private readonly List<IScreenshotProvider> _providers;
    private readonly ILogger? _logger;
    private bool _isDisposed;

    public ScreenshotManager(ILogger? logger = null)
    {
        _logger = logger;
        _providers = new List<IScreenshotProvider>();
        InitializeProviders();
    }

    /// <summary>
    /// Available screenshot providers in order of preference
    /// </summary>
    public IReadOnlyList<IScreenshotProvider> Providers => _providers.AsReadOnly();

    /// <summary>
    /// Takes a screenshot using the first available provider
    /// </summary>
    /// <returns>PNG image data as byte array, or null if all providers fail</returns>
    public byte[]? TakeScreenshot()
    {
        if (_isDisposed)
        {
            _logger?.LogError("ScreenshotManager has been disposed");
            return null;
        }

        _logger?.LogDebug("Attempting to take screenshot using {ProviderCount} providers", _providers.Count);

        foreach (var provider in _providers)
        {
            try
            {
                _logger?.LogDebug("Trying provider: {ProviderName}", provider.ProviderName);

                if (!provider.IsAvailable())
                {
                    _logger?.LogDebug("Provider {ProviderName} is not available", provider.ProviderName);
                    continue;
                }

                var screenshot = provider.TakeScreenshot();
                if (screenshot != null && screenshot.Length > 0)
                {
                    _logger?.LogInformation("Successfully captured screenshot using {ProviderName}: {Size} bytes", 
                                          provider.ProviderName, screenshot.Length);
                    return screenshot;
                }

                _logger?.LogWarning("Provider {ProviderName} returned null or empty screenshot", provider.ProviderName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Provider {ProviderName} failed with exception", provider.ProviderName);
                // Continue to next provider
            }
        }

        _logger?.LogError("All screenshot providers failed");
        return null;
    }

    /// <summary>
    /// Gets information about all providers and their availability
    /// </summary>
    /// <returns>Dictionary of provider names and their availability status</returns>
    public Dictionary<string, bool> GetProviderStatus()
    {
        var status = new Dictionary<string, bool>();
        
        foreach (var provider in _providers)
        {
            try
            {
                status[provider.ProviderName] = provider.IsAvailable();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to check availability for provider {ProviderName}", provider.ProviderName);
                status[provider.ProviderName] = false;
            }
        }

        return status;
    }

    /// <summary>
    /// Force a specific provider to be used
    /// </summary>
    /// <param name="providerName">Name of the provider to use</param>
    /// <returns>Screenshot data or null if provider not found or fails</returns>
    public byte[]? TakeScreenshotWithProvider(string providerName)
    {
        var provider = _providers.FirstOrDefault(p => 
            string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            _logger?.LogError("Provider {ProviderName} not found", providerName);
            return null;
        }

        _logger?.LogInformation("Using specific provider: {ProviderName}", providerName);

        try
        {
            if (!provider.IsAvailable())
            {
                _logger?.LogError("Provider {ProviderName} is not available", providerName);
                return null;
            }

            return provider.TakeScreenshot();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Provider {ProviderName} failed", providerName);
            return null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        foreach (var provider in _providers)
        {
            try
            {
                provider.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing provider {ProviderName}", provider.ProviderName);
            }
        }

        _providers.Clear();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private void InitializeProviders()
    {
        // Add providers in order of preference
        // 1. DirectX Desktop Duplication API - Best performance and compatibility for Windows 11
        _providers.Add(new DirectXScreenshotProvider(_logger));

        // 2. Windows Graphics Capture API - Modern but currently disabled due to .NET compatibility
        _providers.Add(new WindowsGraphicsCaptureProvider(_logger));

        // 3. WinAPI (BitBlt) - Fallback, works everywhere but limited capability
        _providers.Add(new WinApiScreenshotProvider(_logger));

        _logger?.LogInformation("Initialized {ProviderCount} screenshot providers", _providers.Count);
    }
}
