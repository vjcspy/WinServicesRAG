using WinServicesRAG.Core.Screenshot;

namespace WorkerService;

public class Worker(ILogger<Worker> logger, ScreenshotManager screenshotManager) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly ScreenshotManager _screenshotManager = screenshotManager;
    private bool _testCompleted = false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run screenshot test once on startup
        if (!_testCompleted)
        {
            await RunScreenshotTest();
            _testCompleted = true;
        }

        // Continue with normal worker operations
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(5000, stoppingToken); // Increased to 5 seconds to reduce log spam
        }
    }

    private async Task RunScreenshotTest()
    {
        _logger.LogInformation("=== Starting Screenshot Provider Test ===");

        try
        {
            // Create test output directory
            string testDir = @"D:\Documents\Pictures\test_screen_shot";
            Directory.CreateDirectory(testDir);
            _logger.LogInformation("Test output directory: {TestDir}", testDir);

            // Test 1: Get provider status
            _logger.LogInformation("--- Provider Availability Test ---");
            var providerStatus = _screenshotManager.GetProviderStatus();
            foreach (var (name, isAvailable) in providerStatus)
            {
                _logger.LogInformation("Provider: {Name}, Available: {Available}",
                    name, isAvailable);
            }

            foreach (var (name, isAvailable) in providerStatus)
            {
                if (!isAvailable) continue;
                var screenshot = _screenshotManager.TakeScreenshotWithProvider(name);
                if (screenshot != null)
                {
                    string fileName = $"_test_screenshot_{name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string filePath = Path.Combine(testDir, fileName);

                    await File.WriteAllBytesAsync(filePath, screenshot);
                    _logger.LogInformation("✅ Screenshot captured successfully! Size: {Size} bytes, Saved to: {FilePath}",
                        screenshot.Length, filePath);
                }
                else
                {
                    _logger.LogWarning("❌ Screenshot capture failed for provider {ProviderName}", name);
                }
            }


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during screenshot testing");
        }

        _logger.LogInformation("=== Screenshot Provider Test Completed ===");
    }

    public override void Dispose()
    {
        _screenshotManager?.Dispose();
        base.Dispose();
    }
}
