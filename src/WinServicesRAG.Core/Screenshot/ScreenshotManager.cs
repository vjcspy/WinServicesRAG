using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Screenshot;

namespace WinServicesRAG.Core.Screenshot;

/// <summary>
///     Screenshot manager that handles multiple providers with fallback logic.
/// </summary>
public class ScreenshotManager : IDisposable
{
    private readonly List<IScreenshotProvider> _providers;
    private readonly ILogger<ScreenshotManager>? _logger;
    private bool _isDisposed;

    public ScreenshotManager(ILogger<ScreenshotManager>? logger = null)
    {
        _logger = logger;
        _providers = new List<IScreenshotProvider>
        {
            new WindowsGraphicsCaptureProvider(), // Newest and most compatible
            new DirectXScreenshotProvider(),      // Hardware accelerated, fast
            new WinApiScreenshotProvider()        // Reliable fallback
        };
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        foreach (IScreenshotProvider provider in _providers)
        {
            provider.Dispose();
        }

        _providers.Clear();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Takes a screenshot using the first available provider.
    /// </summary>
    /// <returns>PNG image data as byte array, or null if all providers fail</returns>
    public byte[]? TakeScreenshot()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ScreenshotManager));
        }

        _logger?.LogInformation("[ScreenshotManager] Starting screenshot capture...");
        Console.WriteLine("[ScreenshotManager] Starting screenshot capture...");

        foreach (IScreenshotProvider provider in _providers)
        {
            try
            {
                _logger?.LogDebug("[ScreenshotManager] Checking provider: {ProviderName}", provider.ProviderName);
                Console.WriteLine($"[ScreenshotManager] Checking provider: {provider.ProviderName}");

                if (!provider.IsAvailable())
                {
                    _logger?.LogDebug("[ScreenshotManager] Provider {ProviderName} is not available, trying next...", provider.ProviderName);
                    Console.WriteLine($"[ScreenshotManager] Provider {provider.ProviderName} is not available, trying next...");
                    continue;
                }

                _logger?.LogInformation("[ScreenshotManager] Using provider: {ProviderName}", provider.ProviderName);
                Console.WriteLine($"[ScreenshotManager] Using provider: {provider.ProviderName}");
                byte[]? screenshot = provider.TakeScreenshot();

                if (screenshot != null && screenshot.Length > 0)
                {
                    _logger?.LogInformation("[ScreenshotManager] Screenshot successful with {ProviderName}: {Size} bytes", provider.ProviderName, screenshot.Length);
                    Console.WriteLine($"[ScreenshotManager] Screenshot successful with {provider.ProviderName}: {screenshot.Length} bytes");
                    return screenshot;
                }

                _logger?.LogDebug("[ScreenshotManager] Provider {ProviderName} returned empty result, trying next...", provider.ProviderName);
                Console.WriteLine($"[ScreenshotManager] Provider {provider.ProviderName} returned empty result, trying next...");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ScreenshotManager] Provider {ProviderName} failed", provider.ProviderName);
                Console.WriteLine($"[ScreenshotManager] Provider {provider.ProviderName} failed: {ex.Message}");
                Console.WriteLine($"[ScreenshotManager] Exception type: {ex.GetType().Name}");
                // Continue to next provider
            }
        }

        _logger?.LogError("[ScreenshotManager] All providers failed to capture screenshot");
        Console.WriteLine("[ScreenshotManager] All providers failed to capture screenshot");
        return null;
    }

    /// <summary>
    ///     Gets the status of all providers.
    /// </summary>
    /// <returns>Dictionary with provider names and their availability status</returns>
    public Dictionary<string, bool> GetProviderStatus()
    {
        var status = new Dictionary<string, bool>();

        foreach (IScreenshotProvider provider in _providers)
        {
            try
            {
                status[provider.ProviderName] = provider.IsAvailable();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ScreenshotManager] Error checking {ProviderName} status", provider.ProviderName);
                Console.WriteLine($"[ScreenshotManager] Error checking {provider.ProviderName} status: {ex.Message}");
                status[provider.ProviderName] = false;
            }
        }

        return status;
    }

    /// <summary>
    ///     Forces a specific provider to be used.
    /// </summary>
    /// <param name="providerName">Name of the provider to use</param>
    /// <returns>Screenshot data or null if provider fails</returns>
    public byte[]? TakeScreenshotWithProvider(string providerName)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ScreenshotManager));
        }

        IScreenshotProvider? targetProvider = _providers.FirstOrDefault(p => 
            string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (targetProvider == null)
        {
            _logger?.LogError("[ScreenshotManager] Provider '{ProviderName}' not found", providerName);
            Console.WriteLine($"[ScreenshotManager] Provider '{providerName}' not found");
            return null;
        }

        try
        {
            _logger?.LogInformation("[ScreenshotManager] Forcing use of provider: {ProviderName}", targetProvider.ProviderName);
            Console.WriteLine($"[ScreenshotManager] Forcing use of provider: {targetProvider.ProviderName}");
            
            if (!targetProvider.IsAvailable())
            {
                _logger?.LogWarning("[ScreenshotManager] Provider {ProviderName} is not available", targetProvider.ProviderName);
                Console.WriteLine($"[ScreenshotManager] Provider {targetProvider.ProviderName} is not available");
                return null;
            }

            return targetProvider.TakeScreenshot();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[ScreenshotManager] Forced provider {ProviderName} failed", targetProvider.ProviderName);
            Console.WriteLine($"[ScreenshotManager] Forced provider {targetProvider.ProviderName} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Information about a screenshot provider
    /// </summary>
    public class ProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    ///     Detailed test results for a screenshot provider
    /// </summary>
    public class ProviderTestResult
    {
        public string ProviderName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public bool CanCapture { get; set; }
        public long CaptureTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
        public int ImageSizeBytes { get; set; }

        public override string ToString()
        {
            return $"{ProviderName}: Available={IsAvailable}, CanCapture={CanCapture}, " +
                $"Time={CaptureTimeMs}ms, Size={ImageSizeBytes} bytes" +
                (ErrorMessage != null ? $", Error: {ErrorMessage}" : "");
        }
    }

    /// <summary>
    ///     Gets information about available providers on the current system
    /// </summary>
    /// <returns>List of provider information</returns>
    public List<ProviderInfo> GetProviderStatus2()
    {
        var result = new List<ProviderInfo>();

        foreach (IScreenshotProvider provider in _providers)
        {
            ProviderInfo info = new ProviderInfo
            {
                Name = provider.ProviderName,
                IsAvailable = false,
                ErrorMessage = null
            };

            try
            {
                info.IsAvailable = provider.IsAvailable();
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
            }

            result.Add(info);
        }

        return result;
    }

    /// <summary>
    ///     Tests all providers and returns detailed results
    ///     Useful for diagnostics and system capability assessment
    /// </summary>
    /// <returns>Test results for each provider</returns>
    public List<ProviderTestResult> TestAllProviders()
    {
        var results = new List<ProviderTestResult>();

        foreach (IScreenshotProvider provider in _providers)
        {
            ProviderTestResult testResult = new ProviderTestResult
            {
                ProviderName = provider.ProviderName,
                IsAvailable = false,
                CanCapture = false,
                CaptureTimeMs = 0,
                ErrorMessage = null,
                ImageSizeBytes = 0
            };

            try
            {
                // Test availability
                testResult.IsAvailable = provider.IsAvailable();

                if (testResult.IsAvailable)
                {
                    // Test actual capture
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    byte[]? screenshot = provider.TakeScreenshot();
                    stopwatch.Stop();

                    testResult.CaptureTimeMs = stopwatch.ElapsedMilliseconds;

                    if (screenshot != null && screenshot.Length > 0)
                    {
                        testResult.CanCapture = true;
                        testResult.ImageSizeBytes = screenshot.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                testResult.ErrorMessage = ex.Message;
            }

            results.Add(testResult);
        }

        return results;
    }
}
