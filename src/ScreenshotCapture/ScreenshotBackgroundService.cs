using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Processing;
using WinServicesRAG.Core.Services;
using WinServicesRAG.Core.Observer;

namespace ScreenshotCapture;

/// <summary>
/// Background service that processes screenshot jobs continuously using JobProcessingEngine
/// </summary>
public class ScreenshotBackgroundService : BackgroundService
{
    private readonly ILogger<ScreenshotBackgroundService> _logger;
    private readonly IJobProcessingEngine _jobProcessingEngine;
    private readonly ScreenshotServiceConfig _config;
    private IDisposable? _subscription;

    public ScreenshotBackgroundService(
        ILogger<ScreenshotBackgroundService> logger,
        IJobProcessingEngine jobProcessingEngine,
        IOptions<ScreenshotServiceConfig> config)
    {
        _logger = logger;
        _jobProcessingEngine = jobProcessingEngine;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Screenshot Background Service started");
        _logger.LogInformation("Working directory: {WorkDir}", _config.WorkDirectory);
        _logger.LogInformation("Poll interval: {PollInterval} seconds", _config.PollIntervalSeconds);

        try
        {
            // Subscribe to job processing results (same as CLI poll mode)
            _logger.LogInformation("Started polling for screenshots...");
            _subscription = _jobProcessingEngine.ProcessingResults.Subscribe(
                observer: new JobResultObserver(logger: _logger));
            
            // Start the job processing engine
            _jobProcessingEngine.Start();

            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Screenshot Background Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Screenshot Background Service");
            throw;
        }
        finally
        {
            // Stop the job processing engine
            _jobProcessingEngine.Stop();
            _subscription?.Dispose();
            _logger.LogInformation("Screenshot Background Service stopped");
        }
    }
}
