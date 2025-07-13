using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Services;
namespace WinServicesRAG.Core.Processing;

/// <summary>
///     Job processing engine specifically for screenshot capture tasks
/// </summary>
public class ScreenshotJobProcessingEngine : JobProcessingEngineBase
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<ScreenshotJobProcessingEngine> _logger;
    private readonly IScreenshotManager _screenshotManager;

    public ScreenshotJobProcessingEngine(
        IApiClient apiClient,
        IScreenshotManager screenshotManager,
        ILogger<ScreenshotJobProcessingEngine> logger)
        : base(apiClient: apiClient, logger: logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(paramName: nameof(apiClient));
        _screenshotManager = screenshotManager ?? throw new ArgumentNullException(paramName: nameof(screenshotManager));
        _logger = logger ?? throw new ArgumentNullException(paramName: nameof(logger));
    }

    protected override string TargetJobStatus
    {
        get
        {
            return JobStatus.TakeScreenshot;
        }
    }
    protected override string? TargetJobType
    {
        get
        {
            return JobTypes.Screenshot;
        }
    }

    protected override async Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(message: "Processing screenshot job {JobId}", job.Id);

        try
        {
            // Take screenshot
            ScreenshotResult screenshotResult = await _screenshotManager.TakeScreenshotAsync(cancellationToken: cancellationToken);

            if (!screenshotResult.Success || screenshotResult.ImageData == null)
            {
                string errorMessage = screenshotResult.ErrorMessage ?? "Unknown screenshot error";
                _logger.LogError(message: "Screenshot failed for job {JobId}: {Error}", job.Id, errorMessage);

                return new JobProcessingResult
                {
                    JobName = job.Id,
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            // Generate file name
            var fileName = $"screenshot_{job.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";

            _logger.LogDebug(message: "Screenshot captured for job {JobId}, uploading as {FileName} ({Size} bytes)",
                job.Id, fileName, screenshotResult.ImageData.Length);

            // Upload image and update job status
            bool uploadSuccess = await _apiClient.UploadImageAndUpdateJobAsync(jobId: job.Id, imageData: screenshotResult.ImageData, fileName: fileName, cancellationToken: cancellationToken);

            if (uploadSuccess)
            {
                _logger.LogInformation(message: "Successfully processed screenshot job {JobId}, uploaded as {FileName}", job.Id, fileName);

                return new JobProcessingResult
                {
                    JobName = job.Id,
                    Success = true,
                    ImageName = fileName
                };
            }
            _logger.LogError(message: "Failed to upload screenshot for job {JobId}", job.Id);

            return new JobProcessingResult
            {
                JobName = job.Id,
                Success = false,
                ErrorMessage = "Failed to upload screenshot to API server"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Exception occurred while processing screenshot job {JobId}", job.Id);

            return new JobProcessingResult
            {
                JobName = job.Id,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
/// <summary>
///     Job processing engine for general WorkerService tasks (non-screenshot)
/// </summary>
public class GeneralJobProcessingEngine(
    IApiClient apiClient,
    ILogger<GeneralJobProcessingEngine> logger) : JobProcessingEngineBase(apiClient: apiClient, logger: logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(paramName: nameof(apiClient));
    private readonly ILogger<GeneralJobProcessingEngine> _logger = logger ?? throw new ArgumentNullException(paramName: nameof(logger));

    protected override string TargetJobStatus
    {
        get
        {
            return JobStatus.Pending;
        }
    }
    protected override string? TargetJobType
    {
        get
        {
            return null;
            // Accept all job types except screenshots
        }
    }

    protected override async Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(message: "Processing general job {JobId} of type {JobType}", job.Id, job.Type);

        try
        {
            // Skip screenshot jobs - they should be handled by ScreenshotJobProcessingEngine
            if (job.Type == JobTypes.Screenshot || job.Status == JobStatus.TakeScreenshot)
            {
                _logger.LogDebug(message: "Skipping screenshot job {JobId} - should be handled by ScreenshotJobProcessingEngine", job.Id);

                return new JobProcessingResult
                {
                    JobName = job.Id,
                    Success = true, // Not an error, just not our responsibility
                    ErrorMessage = "Screenshot job delegated to ScreenshotCapture service"
                };
            }

            // Process different job types
            JobProcessingResult result = job.Type switch
            {
                JobTypes.SystemInfo => await ProcessSystemInfoJobAsync(job: job, cancellationToken: cancellationToken),
                JobTypes.FileOperation => await ProcessFileOperationJobAsync(job: job, cancellationToken: cancellationToken),
                JobTypes.CustomCommand => await ProcessCustomCommandJobAsync(job: job, cancellationToken: cancellationToken),
                _ => await ProcessUnknownJobAsync(job: job, cancellationToken: cancellationToken)
            };

            _logger.LogInformation(message: "Successfully processed general job {JobId} of type {JobType}", job.Id, job.Type);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Exception occurred while processing general job {JobId}", job.Id);

            return new JobProcessingResult
            {
                JobName = job.Id,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<JobProcessingResult> ProcessSystemInfoJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogDebug(message: "Processing system info job {JobId}", job.Id);

        // Simulate system info collection
        await Task.Delay(millisecondsDelay: 1000, cancellationToken: cancellationToken);

        bool success = await _apiClient.UpdateJobStatusAsync(jobName: job.Id, status: JobStatus.Completed,
            data: new Dictionary<string, object>
            {
                {
                    "system_info", new
                    {
                        os = Environment.OSVersion.ToString(),
                        machine = Environment.MachineName
                    }
                }
            },
            cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobName = job.Id,
            Success = success,
            ErrorMessage = success ? null : "Failed to update job status"
        };
    }

    private async Task<JobProcessingResult> ProcessFileOperationJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogDebug(message: "Processing file operation job {JobId}", job.Id);

        // Simulate file operation
        await Task.Delay(millisecondsDelay: 500, cancellationToken: cancellationToken);

        bool success = await _apiClient.UpdateJobStatusAsync(jobName: job.Id, status: JobStatus.Completed, cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobName = job.Id,
            Success = success,
            ErrorMessage = success ? null : "Failed to update job status"
        };
    }

    private async Task<JobProcessingResult> ProcessCustomCommandJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogDebug(message: "Processing custom command job {JobId}", job.Id);

        // Simulate custom command execution
        await Task.Delay(millisecondsDelay: 2000, cancellationToken: cancellationToken);

        bool success = await _apiClient.UpdateJobStatusAsync(jobName: job.Id, status: JobStatus.Completed, cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobName = job.Id,
            Success = success,
            ErrorMessage = success ? null : "Failed to update job status"
        };
    }

    private async Task<JobProcessingResult> ProcessUnknownJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogWarning(message: "Processing unknown job type {JobType} for job {JobId}", job.Type, job.Id);

        bool success = await _apiClient.UpdateJobStatusAsync(jobName: job.Id, status: JobStatus.Error,
            errorMessage: $"Unknown job type: {job.Type}",
            cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobName = job.Id,
            Success = false,
            ErrorMessage = $"Unknown job type: {job.Type}"
        };
    }
}
