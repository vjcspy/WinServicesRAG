using WinServicesRAG.Core.Observer;
using WinServicesRAG.Core.Processing;
using WinServicesRAG.Core.Services;

namespace WorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IJobProcessingEngine _jobProcessingEngine;
    private readonly IApiClient _apiClient;

    public Worker(ILogger<Worker> logger, IJobProcessingEngine jobProcessingEngine, IApiClient apiClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobProcessingEngine = jobProcessingEngine ?? throw new ArgumentNullException(nameof(jobProcessingEngine));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerService starting execution");

        try
        {
            // Check API connectivity on startup
            var isHealthy = await _apiClient.HealthCheckAsync(stoppingToken);
            if (!isHealthy)
            {
                _logger.LogWarning("API health check failed, but continuing with service startup");
            }
            else
            {
                _logger.LogInformation("API health check successful");
            }

            // Subscribe to processing results and errors using observers
            var resultSubscription = _jobProcessingEngine.ProcessingResults.Subscribe(
                new JobResultObserver(_logger));

            var errorSubscription = _jobProcessingEngine.ProcessingErrors.Subscribe(
                new ProcessingErrorObserver(_logger));

            try
            {
                // Start the job processing engine
                _logger.LogInformation("Starting job processing engine");
                _jobProcessingEngine.Start(stoppingToken);

                // Keep the service running while monitoring the processing engine
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!_jobProcessingEngine.IsRunning)
                    {
                        _logger.LogWarning("Job processing engine is not running, attempting to restart");
                        _jobProcessingEngine.Start(stoppingToken);
                    }

                    // Log periodic status
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("WorkerService running at: {Time}, Processing engine status: {IsRunning}", 
                            DateTimeOffset.Now, _jobProcessingEngine.IsRunning);
                    }

                    await Task.Delay(10000, stoppingToken); // Check every 10 seconds
                }
            }
            finally
            {
                resultSubscription?.Dispose();
                errorSubscription?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WorkerService execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in WorkerService execution");
            throw;
        }
        finally
        {
            _logger.LogInformation("Stopping job processing engine");
            _jobProcessingEngine.Stop();
        }
    }

    public override void Dispose()
    {
        _jobProcessingEngine?.Dispose();
        base.Dispose();
    }
}


/// <summary>
/// Observer for processing errors
/// </summary>
internal class ProcessingErrorObserver : IObserver<Exception>
{
    private readonly ILogger _logger;

    public ProcessingErrorObserver(ILogger logger)
    {
        _logger = logger;
    }

    public void OnNext(Exception error)
    {
        _logger.LogError(error, "Critical error in job processing engine");
    }

    public void OnError(Exception error)
    {
        _logger.LogError(error, "Error in processing error stream");
    }

    public void OnCompleted()
    {
        _logger.LogInformation("Processing error stream completed");
    }
}
