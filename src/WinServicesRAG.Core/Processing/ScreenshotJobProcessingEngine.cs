using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Services;
namespace WinServicesRAG.Core.Processing;

/// <summary>
///     Job processing engine specifically for screenshot capture tasks
/// </summary>
public class ScreenshotJobProcessingEngine(
    IApiClient apiClient,
    IScreenshotManager screenshotManager,
    ILogger<ScreenshotJobProcessingEngine> logger) : JobProcessingEngineBase(apiClient: apiClient, logger: logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(paramName: nameof(apiClient));
    private readonly ILogger<ScreenshotJobProcessingEngine> _logger = logger ?? throw new ArgumentNullException(paramName: nameof(logger));
    private readonly IScreenshotManager _screenshotManager = screenshotManager ?? throw new ArgumentNullException(paramName: nameof(screenshotManager));

    protected override async Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(message: "Processing screenshot job {JobName}", job.Name);

        try
        {
            var captureMode = job.Data?["capture_mode"].ToString() ?? "all";
            // Take screenshot
            ScreenshotResult screenshotResult = await _screenshotManager.TakeScreenshotAsync(cancellationToken: cancellationToken);

            if (!screenshotResult.Success || screenshotResult.ImageData == null)
            {
                string errorMessage = screenshotResult.ErrorMessage ?? "Unknown screenshot error";
                _logger.LogError(message: "Screenshot failed for job {JobId}: {Error}", job.Name, errorMessage);

                return new JobProcessingResult
                {
                    JobName = job.Name,
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            // Generate file name
            var fileName = $"screenshot_{job.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";

            _logger.LogDebug(message: "Screenshot captured for job {JobId}, uploading as {FileName} ({Size} bytes)",
                job.Name, fileName, screenshotResult.ImageData.Length);

            // Upload image and update job status
            bool uploadSuccess = await _apiClient.UploadImageAndUpdateJobAsync(jobId: job.Name, imageData: screenshotResult.ImageData, fileName: fileName, cancellationToken: cancellationToken);

            if (uploadSuccess)
            {
                _logger.LogInformation(message: "Successfully processed screenshot job {JobId}, uploaded as {FileName}", job.Name, fileName);

                return new JobProcessingResult
                {
                    JobName = job.Name,
                    Success = true,
                    ImageName = fileName
                };
            }
            _logger.LogError(message: "Failed to upload screenshot for job {JobId}", job.Name);

            return new JobProcessingResult
            {
                JobName = job.Name,
                Success = false,
                ErrorMessage = "Failed to upload screenshot to API server"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Exception occurred while processing screenshot job {JobId}", job.Name);

            return new JobProcessingResult
            {
                JobName = job.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
