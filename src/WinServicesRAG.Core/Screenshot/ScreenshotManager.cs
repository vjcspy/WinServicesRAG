using Microsoft.Extensions.Logging;
namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Manages screenshot providers with fallback strategy.
///     Tries providers in order of preference until one succeeds.
/// </summary>
public class ScreenshotManager : IScreenshotManager
{
    private readonly ILogger _logger;
    private readonly List<IScreenshotProvider> _providers;
    private bool _isDisposed;

    public ScreenshotManager(ILogger<ScreenshotManager> logger)
    {
        _logger = logger;
        _providers =
        [
        ];
        InitializeProviders();
    }

    /// <summary>
    ///     Available screenshot providers in order of preference
    /// </summary>
    public IReadOnlyList<IScreenshotProvider> Providers
    {
        get
        {
            return _providers.AsReadOnly();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        foreach (IScreenshotProvider provider in _providers)
        {
            try
            {
                provider.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(exception: ex, message: "Error disposing provider {ProviderName}", provider.ProviderName);
            }
        }

        _providers.Clear();
        _isDisposed = true;
        GC.SuppressFinalize(obj: this);
    }

    public Task<ScreenshotResult> TakeScreenshotAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }
    public Task<ScreenshotResult> TakeScreenshotAsync(string providerName, CancellationToken cancellationToken = default(CancellationToken))
    {
        throw new NotImplementedException();
    }
    /// <summary>
    ///     Gets information about all providers and their availability
    /// </summary>
    /// <returns>Dictionary of provider names and their availability status</returns>
    public Dictionary<string, bool> GetProviderStatus()
    {
        var status = new Dictionary<string, bool>();

        foreach (IScreenshotProvider provider in _providers)
        {
            try
            {
                status[key: provider.ProviderName] = provider.IsAvailable();
            }
            catch (Exception ex)
            {
                _logger.LogError(exception: ex, message: "Failed to check availability for provider {ProviderName}", provider.ProviderName);
                status[key: provider.ProviderName] = false;
            }
        }

        return status;
    }
    public List<ProviderInfo> GetProviderInfo()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Takes a screenshot using the first available provider
    /// </summary>
    /// <returns>PNG image data as byte array, or null if all providers fail</returns>
    public byte[]? TakeScreenshot()
    {
        if (_isDisposed)
        {
            _logger.LogError(message: "ScreenshotManager has been disposed");
            return null;
        }

        _logger.LogDebug(message: "Attempting to take screenshot using {ProviderCount} providers", _providers.Count);

        foreach (IScreenshotProvider provider in _providers)
        {
            try
            {
                _logger.LogDebug(message: "Trying provider: {ProviderName}", provider.ProviderName);

                if (!provider.IsAvailable())
                {
                    _logger.LogDebug(message: "Provider {ProviderName} is not available", provider.ProviderName);
                    continue;
                }

                byte[]? screenshot = provider.TakeScreenshot();
                if (screenshot != null && screenshot.Length > 0)
                {
                    _logger.LogInformation(message: "Successfully captured screenshot using {ProviderName}: {Size} bytes",
                        provider.ProviderName, screenshot.Length);
                    return screenshot;
                }

                _logger.LogWarning(message: "Provider {ProviderName} returned null or empty screenshot", provider.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(exception: ex, message: "Provider {ProviderName} failed with exception", provider.ProviderName);
                // Continue to next provider
            }
        }

        _logger.LogError(message: "All screenshot providers failed");
        return null;
    }

    /// <summary>
    ///     Force a specific provider to be used
    /// </summary>
    /// <param name="providerName">Name of the provider to use</param>
    /// <returns>Screenshot data or null if provider not found or fails</returns>
    public byte[]? TakeScreenshotWithProvider(string providerName)
    {
        IScreenshotProvider? provider = _providers.FirstOrDefault(predicate: p =>
            string.Equals(a: p.ProviderName, b: providerName, comparisonType: StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            _logger.LogError(message: "Provider {ProviderName} not found", providerName);
            return null;
        }

        _logger.LogInformation(message: "Using specific provider: {ProviderName}", providerName);

        try
        {
            if (!provider.IsAvailable())
            {
                _logger.LogError(message: "Provider {ProviderName} is not available", providerName);
                return null;
            }

            return provider.TakeScreenshot();
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Provider {ProviderName} failed", providerName);
            return null;
        }
    }

    private void InitializeProviders()
    {
        // Add providers in order of preference
        // 1. DirectX Desktop Duplication API - Best performance and compatibility for Windows 11
        _providers.Add(item: new DirectXScreenshotProvider(logger: _logger));

        // 2. Windows Graphics Capture API - Modern, now implemented and working
        _providers.Add(item: new GDI(logger: _logger));

        // 3. WinAPI (BitBlt) - Fallback, works everywhere but limited capability
        _providers.Add(item: new WinApiScreenshotProvider(logger: _logger));

        _logger.LogInformation(message: "Initialized {ProviderCount} screenshot providers", _providers.Count);
    }
}
