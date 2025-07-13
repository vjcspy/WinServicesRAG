using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Services;
using WinServicesRAG.Core.Screenshot;

namespace WinServicesRAG.Core.Processing;

/// <summary>
/// Job processing engine specifically for screenshot capture tasks
/// </summary>
public class ScreenshotJobProcessingEngine : JobProcessingEngineBase
{
    private readonly IScreenshotManager _screenshotManager;
    private readonly IApiClient _apiClient;
    private readonly ILogger<ScreenshotJobProcessingEngine> _logger;

    public ScreenshotJobProcessingEngine(
        IApiClient apiClient, 
        IScreenshotManager screenshotManager,
        ILogger<ScreenshotJobProcessingEngine> logger) 
        : base(apiClient, logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _screenshotManager = screenshotManager ?? throw new ArgumentNullException(nameof(screenshotManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override string TargetJobStatus => JobStatus.TakeScreenshot;
    protected override string? TargetJobType => JobTypes.Screenshot;

    protected override async Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing screenshot job {JobId}", job.Id);

        try
        {
            // Take screenshot
            var screenshotResult = await _screenshotManager.TakeScreenshotAsync(cancellationToken);
            
            if (!screenshotResult.Success || screenshotResult.ImageData == null)
            {
                var errorMessage = screenshotResult.ErrorMessage ?? "Unknown screenshot error";
                _logger.LogError("Screenshot failed for job {JobId}: {Error}", job.Id, errorMessage);
                
                return new JobProcessingResult
                {
                    JobId = job.Id,
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            // Generate file name
            var fileName = $"screenshot_{job.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
            
            _logger.LogDebug("Screenshot captured for job {JobId}, uploading as {FileName} ({Size} bytes)", 
                job.Id, fileName, screenshotResult.ImageData.Length);

            // Upload image and update job status
            var uploadSuccess = await _apiClient.UploadImageAndUpdateJobAsync(job.Id, screenshotResult.ImageData, fileName, cancellationToken);

            if (uploadSuccess)
            {
                _logger.LogInformation("Successfully processed screenshot job {JobId}, uploaded as {FileName}", job.Id, fileName);
                
                return new JobProcessingResult
                {
                    JobId = job.Id,
                    Success = true,
                    ImageName = fileName
                };
            }
            else
            {
                _logger.LogError("Failed to upload screenshot for job {JobId}", job.Id);
                
                return new JobProcessingResult
                {
                    JobId = job.Id,
                    Success = false,
                    ErrorMessage = "Failed to upload screenshot to API server"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while processing screenshot job {JobId}", job.Id);
            
            return new JobProcessingResult
            {
                JobId = job.Id,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

/// <summary>
/// Job processing engine for general WorkerService tasks (non-screenshot)
/// </summary>
public class GeneralJobProcessingEngine : JobProcessingEngineBase
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<GeneralJobProcessingEngine> _logger;

    public GeneralJobProcessingEngine(
        IApiClient apiClient,
        ILogger<GeneralJobProcessingEngine> logger) 
        : base(apiClient, logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override string TargetJobStatus => JobStatus.Pending;
    protected override string? TargetJobType => null; // Accept all job types except screenshots

    protected override async Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing general job {JobId} of type {JobType}", job.Id, job.Type);

        try
        {
            // Skip screenshot jobs - they should be handled by ScreenshotJobProcessingEngine
            if (job.Type == JobTypes.Screenshot || job.Status == JobStatus.TakeScreenshot)
            {
                _logger.LogDebug("Skipping screenshot job {JobId} - should be handled by ScreenshotJobProcessingEngine", job.Id);
                
                return new JobProcessingResult
                {
                    JobId = job.Id,
                    Success = true, // Not an error, just not our responsibility
                    ErrorMessage = "Screenshot job delegated to ScreenshotCapture service"
                };
            }

            // Process different job types
            var result = job.Type switch
            {
                JobTypes.SystemInfo => await ProcessSystemInfoJobAsync(job, cancellationToken),
                JobTypes.FileOperation => await ProcessFileOperationJobAsync(job, cancellationToken),
                JobTypes.CustomCommand => await ProcessCustomCommandJobAsync(job, cancellationToken),
                _ => await ProcessUnknownJobAsync(job, cancellationToken)
            };

            _logger.LogInformation("Successfully processed general job {JobId} of type {JobType}", job.Id, job.Type);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while processing general job {JobId}", job.Id);
            
            return new JobProcessingResult
            {
                JobId = job.Id,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<JobProcessingResult> ProcessSystemInfoJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing system info job {JobId}", job.Id);
        
        // Simulate system info collection
        await Task.Delay(1000, cancellationToken);

        var success = await _apiClient.UpdateJobStatusAsync(job.Id, JobStatus.Completed, 
            metadata: new Dictionary<string, object>
            {
                { "system_info", new { os = Environment.OSVersion.ToString(), machine = Environment.MachineName } }
            }, 
            cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobId = job.Id,
            Success = success,
            ErrorMessage = success ? null : "Failed to update job status"
        };
    }

    private async Task<JobProcessingResult> ProcessFileOperationJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing file operation job {JobId}", job.Id);
        
        // Simulate file operation
        await Task.Delay(500, cancellationToken);

        var success = await _apiClient.UpdateJobStatusAsync(job.Id, JobStatus.Completed, cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobId = job.Id,
            Success = success,
            ErrorMessage = success ? null : "Failed to update job status"
        };
    }

    private async Task<JobProcessingResult> ProcessCustomCommandJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing custom command job {JobId}", job.Id);
        
        // Simulate custom command execution
        await Task.Delay(2000, cancellationToken);

        var success = await _apiClient.UpdateJobStatusAsync(job.Id, JobStatus.Completed, cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobId = job.Id,
            Success = success,
            ErrorMessage = success ? null : "Failed to update job status"
        };
    }

    private async Task<JobProcessingResult> ProcessUnknownJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Processing unknown job type {JobType} for job {JobId}", job.Type, job.Id);

        var success = await _apiClient.UpdateJobStatusAsync(job.Id, JobStatus.Error, 
            errorMessage: $"Unknown job type: {job.Type}", 
            cancellationToken: cancellationToken);

        return new JobProcessingResult
        {
            JobId = job.Id,
            Success = false,
            ErrorMessage = $"Unknown job type: {job.Type}"
        };
    }
}
