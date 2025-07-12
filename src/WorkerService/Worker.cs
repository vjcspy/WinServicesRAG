using WorkerService.Screenshot;

namespace WorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ScreenshotManager _screenshotManager;
    private bool _testCompleted = false;

    public Worker(ILogger<Worker> logger, ScreenshotManager screenshotManager)
    {
        _logger = logger;
        _screenshotManager = screenshotManager;
    }

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
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScreenshotTests");
            Directory.CreateDirectory(testDir);
            _logger.LogInformation("Test output directory: {TestDir}", testDir);

            // Test 1: Get provider status
            _logger.LogInformation("--- Provider Availability Test ---");
            var providerStatus = _screenshotManager.GetProviderStatus();
            foreach (var status in providerStatus)
            {
                _logger.LogInformation("Provider: {Name}, Available: {Available}, Error: {Error}", 
                    status.Name, status.IsAvailable, status.ErrorMessage ?? "None");
            }

            // Test 2: Run detailed tests on all providers
            _logger.LogInformation("--- Detailed Provider Tests ---");
            var testResults = _screenshotManager.TestAllProviders();
            foreach (var result in testResults)
            {
                _logger.LogInformation("Test Result: {Result}", result.ToString());
            }

            // Test 3: Take a screenshot using the manager (fallback logic)
            _logger.LogInformation("--- Screenshot Capture Test ---");
            byte[]? screenshot = _screenshotManager.TakeScreenshot();
            
            if (screenshot != null && screenshot.Length > 0)
            {
                // Save the screenshot to file
                string fileName = $"test_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string filePath = Path.Combine(testDir, fileName);
                
                await File.WriteAllBytesAsync(filePath, screenshot);
                _logger.LogInformation("✅ Screenshot captured successfully! Size: {Size} bytes, Saved to: {FilePath}", 
                    screenshot.Length, filePath);
            }
            else
            {
                _logger.LogError("❌ Failed to capture screenshot - all providers failed");
            }

            // Test 4: Performance test
            _logger.LogInformation("--- Performance Test ---");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 3; i++)
            {
                var perfScreenshot = _screenshotManager.TakeScreenshot();
                if (perfScreenshot != null)
                {
                    _logger.LogInformation("Performance test {Count}: {Size} bytes", i + 1, perfScreenshot.Length);
                }
                await Task.Delay(100); // Small delay between captures
            }
            stopwatch.Stop();
            _logger.LogInformation("3 screenshots took {ElapsedMs}ms total (avg: {AvgMs}ms)", 
                stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds / 3.0);

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
