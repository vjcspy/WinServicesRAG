using System.Diagnostics;
namespace WorkerService.Screenshot;

/// <summary>
///     Screenshot manager that tries multiple providers in order of preference
///     Follows the strategy pattern with fallback mechanism
/// </summary>
public class ScreenshotManager
{
    private readonly object _lock = new object();
    private readonly List<IScreenshotProvider> _providers;

    public ScreenshotManager()
    {
        // Initialize providers in order of preference as specified in README
        _providers = new List<IScreenshotProvider>
        {
            // new WindowsGraphicsCaptureProvider(),  // Solution 1: Most secure and modern (commented out for now)
            new DirectXScreenshotProvider(),      // Solution 2: High performance
            new WinApiScreenshotProvider()         // Solution 3: Most compatible fallback
        };
    }

    /// <summary>
    ///     Takes a screenshot using the first available provider
    /// </summary>
    /// <returns>Screenshot as PNG byte array, or null if all providers fail</returns>
    public byte[]? TakeScreenshot()
    {
        lock (_lock)
        {
            foreach (IScreenshotProvider provider in _providers)
            {
                try
                {
                    // Check if provider is available on current system
                    if (!provider.IsAvailable())
                    {
                        Console.WriteLine(value: $"Provider {provider.ProviderName} is not available, trying next...");
                        continue;
                    }

                    Console.WriteLine(value: $"Attempting screenshot with {provider.ProviderName}");

                    // Try to take screenshot
                    byte[]? result = provider.TakeScreenshot();
                    if (result != null && result.Length > 0)
                    {
                        Console.WriteLine(value: $"Successfully captured screenshot with {provider.ProviderName}");
                        return result;
                    }

                    Console.WriteLine(value: $"Provider {provider.ProviderName} returned null/empty result, trying next...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(value: $"Provider {provider.ProviderName} failed with exception: {ex.Message}");
                    // Continue to next provider
                }
            }

            Console.WriteLine(value: "All screenshot providers failed");
            return null;
        }
    }

    public byte[]? TakeScreenShotByProvider(int providerIndex)
    {
        if (providerIndex < 0 || providerIndex >= _providers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(providerIndex), "Invalid provider index");
        }

        IScreenshotProvider provider = _providers[providerIndex];
        try
        {
            if (!provider.IsAvailable())
            {
                Console.WriteLine($"Provider {provider.ProviderName} is not available");
                return null;
            }

            return provider.TakeScreenshot();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Provider {provider.ProviderName} failed with exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets information about available providers on the current system
    /// </summary>
    /// <returns>List of provider information</returns>
    public List<ProviderInfo> GetProviderStatus()
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

            result.Add(item: info);
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
                    Stopwatch stopwatch = Stopwatch.StartNew();
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

            results.Add(item: testResult);
        }

        return results;
    }

    /// <summary>
    ///     Cleanup resources if needed
    /// </summary>
    public void Dispose()
    {
        foreach (IScreenshotProvider provider in _providers)
        {
            if (provider is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
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
