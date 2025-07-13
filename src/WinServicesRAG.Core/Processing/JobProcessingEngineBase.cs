using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Services;

namespace WinServicesRAG.Core.Processing;

/// <summary>
/// Job processing result
/// </summary>
public class JobProcessingResult
{
    public string JobId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ImageName { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Interface for job processing engine
/// </summary>
public interface IJobProcessingEngine : IDisposable
{
    /// <summary>
    /// Starts the job processing engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    void Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the job processing engine
    /// </summary>
    void Stop();

    /// <summary>
    /// Observable stream of job processing results
    /// </summary>
    IObservable<JobProcessingResult> ProcessingResults { get; }

    /// <summary>
    /// Observable stream of errors during processing
    /// </summary>
    IObservable<Exception> ProcessingErrors { get; }

    /// <summary>
    /// Current status of the processing engine
    /// </summary>
    bool IsRunning { get; }
}

/// <summary>
/// Abstract base class for job processing engines using Reactive Extensions
/// </summary>
public abstract class JobProcessingEngineBase : IJobProcessingEngine
{
    private readonly IApiClient _apiClient;
    private readonly ILogger _logger;
    private readonly CompositeDisposable _disposables;
    private readonly Subject<JobProcessingResult> _processingResults;
    private readonly Subject<Exception> _processingErrors;
    private IDisposable? _pollingSubscription;
    private bool _disposed;

    protected JobProcessingEngineBase(IApiClient apiClient, ILogger logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _disposables = new CompositeDisposable();
        _processingResults = new Subject<JobProcessingResult>();
        _processingErrors = new Subject<Exception>();

        _disposables.Add(_processingResults);
        _disposables.Add(_processingErrors);
    }

    public IObservable<JobProcessingResult> ProcessingResults => _processingResults.AsObservable();
    public IObservable<Exception> ProcessingErrors => _processingErrors.AsObservable();
    public bool IsRunning => _pollingSubscription != null;

    /// <summary>
    /// Gets the job status to filter for when polling
    /// </summary>
    protected abstract string TargetJobStatus { get; }

    /// <summary>
    /// Gets the job type to filter for (optional)
    /// </summary>
    protected virtual string? TargetJobType => null;

    /// <summary>
    /// Gets the polling interval
    /// </summary>
    protected virtual TimeSpan PollingInterval => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Processes a single job
    /// </summary>
    /// <param name="job">Job to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    protected abstract Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken);

    public virtual void Start(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(JobProcessingEngineBase));

        if (IsRunning)
        {
            _logger.LogWarning("Job processing engine is already running");
            return;
        }

        _logger.LogInformation("Starting job processing engine with polling interval {PollingInterval}", PollingInterval);

        // Create the main processing stream using Rx.NET
        _pollingSubscription = Observable
            .Interval(PollingInterval)
            .StartWith(0) // Start immediately
            .SelectMany(_ => GetJobsFromApi(cancellationToken))
            .Where(jobs => jobs.Any()) // Only process when there are jobs
            .SelectMany(jobs => jobs.ToObservable()) // Flatten to individual jobs
            .SelectMany(job => ProcessJobWithErrorHandling(job, cancellationToken))
            .Subscribe(
                result =>
                {
                    _logger.LogDebug("Job {JobId} processing completed: {Success}", result.JobId, result.Success);
                    _processingResults.OnNext(result);
                },
                error =>
                {
                    _logger.LogError(error, "Critical error in job processing stream");
                    _processingErrors.OnNext(error);
                },
                () =>
                {
                    _logger.LogInformation("Job processing stream completed");
                });

        _disposables.Add(_pollingSubscription);
        _logger.LogInformation("Job processing engine started successfully");
    }

    public virtual void Stop()
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Job processing engine is not running");
            return;
        }

        _logger.LogInformation("Stopping job processing engine");

        _pollingSubscription?.Dispose();
        _pollingSubscription = null;

        _logger.LogInformation("Job processing engine stopped");
    }

    private async Task<List<JobModel>> GetJobsFromApi(CancellationToken cancellationToken)
    {
        try
        {
            var jobs = await _apiClient.GetJobsAsync(TargetJobStatus, TargetJobType, 50, cancellationToken);
            
            if (jobs.Any())
            {
                _logger.LogDebug("Retrieved {JobCount} jobs with status {Status}", jobs.Count, TargetJobStatus);
            }

            return jobs;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Job polling was cancelled");
            return new List<JobModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve jobs from API");
            return new List<JobModel>();
        }
    }

    private IObservable<JobProcessingResult> ProcessJobWithErrorHandling(JobModel job, CancellationToken cancellationToken)
    {
        return Observable.FromAsync(() => ProcessJobAsync(job, cancellationToken))
            .Retry(3) // Retry up to 3 times on failure
            .Catch<JobProcessingResult, Exception>(ex =>
            {
                _logger.LogError(ex, "Failed to process job {JobId} after retries", job.Id);
                
                // Update job status to ERROR via API
                return Observable.FromAsync(async () =>
                {
                    await _apiClient.UpdateJobStatusAsync(job.Id, JobStatus.Error, errorMessage: ex.Message, cancellationToken: cancellationToken);
                    
                    return new JobProcessingResult
                    {
                        JobId = job.Id,
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                });
            })
            .Finally(() =>
            {
                // Cleanup resources if needed
                _logger.LogDebug("Completed processing job {JobId}", job.Id);
            });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
