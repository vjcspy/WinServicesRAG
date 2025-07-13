using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Services;
namespace WinServicesRAG.Core.Processing;

/// <summary>
///     Job processing result
/// </summary>
public class JobProcessingResult
{
    public string JobName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ImageName { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
/// <summary>
///     Interface for job processing engine
/// </summary>
public interface IJobProcessingEngine : IDisposable
{

    /// <summary>
    ///     Observable stream of job processing results
    /// </summary>
    IObservable<JobProcessingResult> ProcessingResults { get; }

    /// <summary>
    ///     Observable stream of errors during processing
    /// </summary>
    IObservable<Exception> ProcessingErrors { get; }

    /// <summary>
    ///     Current status of the processing engine
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    ///     Starts the job processing engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    void Start(CancellationToken cancellationToken = default(CancellationToken));

    /// <summary>
    ///     Stops the job processing engine
    /// </summary>
    void Stop();
}
/// <summary>
///     Abstract base class for job processing engines using Reactive Extensions
/// </summary>
public abstract class JobProcessingEngineBase : IJobProcessingEngine
{
    private readonly IApiClient _apiClient;
    private readonly CompositeDisposable _disposables;
    private readonly ILogger _logger;
    private readonly Subject<Exception> _processingErrors;
    private readonly Subject<JobProcessingResult> _processingResults;
    private bool _disposed;
    private IDisposable? _pollingSubscription;

    protected JobProcessingEngineBase(IApiClient apiClient, ILogger logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(paramName: nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(paramName: nameof(logger));
        _disposables = new CompositeDisposable();
        _processingResults = new Subject<JobProcessingResult>();
        _processingErrors = new Subject<Exception>();

        _disposables.Add(item: _processingResults);
        _disposables.Add(item: _processingErrors);
    }

    /// <summary>
    ///     Gets the job status to filter for when polling
    /// </summary>
    protected abstract string TargetJobStatus { get; }

    /// <summary>
    ///     Gets the job type to filter for (optional)
    /// </summary>
    protected virtual string? TargetJobType
    {
        get
        {
            return null;
        }
    }

    /// <summary>
    ///     Gets the polling interval
    /// </summary>
    protected virtual TimeSpan PollingInterval
    {
        get
        {
            return TimeSpan.FromSeconds(seconds: 5);
        }
    }

    public IObservable<JobProcessingResult> ProcessingResults
    {
        get
        {
            return _processingResults.AsObservable();
        }
    }
    public IObservable<Exception> ProcessingErrors
    {
        get
        {
            return _processingErrors.AsObservable();
        }
    }
    public bool IsRunning
    {
        get
        {
            return _pollingSubscription != null;
        }
    }

    public virtual void Start(CancellationToken cancellationToken = default(CancellationToken))
    {
        if (_disposed)
            throw new ObjectDisposedException(objectName: nameof(JobProcessingEngineBase));

        if (IsRunning)
        {
            _logger.LogWarning(message: "Job processing engine is already running");
            return;
        }

        _logger.LogInformation(message: "Starting job processing engine with polling interval {PollingInterval}", PollingInterval);

        // Create the main processing stream using Rx.NET
        _pollingSubscription = Observable
            .Interval(period: PollingInterval)
            .StartWith(0) // Start immediately
            .SelectMany(selector: _ => GetJobFromApi(cancellationToken: cancellationToken))
            .Where(predicate: job => job != null)
            .SelectMany(selector: job => ProcessJobWithErrorHandling(job: job!, cancellationToken: cancellationToken))
            .Subscribe(
                onNext: result =>
                {
                    _logger.LogDebug(message: "Job {JobId} processing completed: {Success}", result.JobName, result.Success);
                    _processingResults.OnNext(value: result);
                },
                onError: error =>
                {
                    _logger.LogError(exception: error, message: "Critical error in job processing stream");
                    _processingErrors.OnNext(value: error);
                },
                onCompleted: () =>
                {
                    _logger.LogInformation(message: "Job processing stream completed");
                });

        _disposables.Add(item: _pollingSubscription);
        _logger.LogInformation(message: "Job processing engine started successfully");
    }

    public virtual void Stop()
    {
        if (!IsRunning)
        {
            _logger.LogWarning(message: "Job processing engine is not running");
            return;
        }

        _logger.LogInformation(message: "Stopping job processing engine");

        _pollingSubscription?.Dispose();
        _pollingSubscription = null;

        _logger.LogInformation(message: "Job processing engine stopped");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(obj: this);
    }

    /// <summary>
    ///     Processes a single job
    /// </summary>
    /// <param name="job">Job to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    protected abstract Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken);

    private async Task<JobModel?> GetJobFromApi(CancellationToken cancellationToken)
    {
        try
        {
            JobModel? job = await _apiClient.GetJobAsync(jobName: "PBF_EXAM", cancellationToken: cancellationToken);

            if (job != null)
            {
                _logger.LogDebug(message: "Retrieved job with status {Status}", job.Status);
            }

            return job;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(message: "Job polling was cancelled");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to retrieve jobs from API");
            return null;
        }
    }

    private IObservable<JobProcessingResult> ProcessJobWithErrorHandling(JobModel job, CancellationToken cancellationToken)
    {
        return Observable.FromAsync(functionAsync: () => ProcessJobAsync(job: job, cancellationToken: cancellationToken))
            .Retry(retryCount: 3) // Retry up to 3 times on failure
            .Catch<JobProcessingResult, Exception>(handler: ex =>
            {
                _logger.LogError(exception: ex, message: "Failed to process job {JobId} after retries", job.Id);

                // Update job status to ERROR via API
                return Observable.FromAsync(functionAsync: async () =>
                {
                    await _apiClient.UpdateJobStatusAsync(jobName: job.Id, status: JobStatus.Error, errorMessage: ex.Message, cancellationToken: cancellationToken);

                    return new JobProcessingResult
                    {
                        JobName = job.Id,
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                });
            })
            .Finally(finallyAction: () =>
            {
                // Cleanup resources if needed
                _logger.LogDebug(message: "Completed processing job {JobId}", job.Id);
            });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Stop();
            _disposables?.Dispose();
            _disposed = true;
        }
    }
}
