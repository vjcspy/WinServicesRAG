using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Helper;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Services;
using WinServicesRAG.Core.Value;
namespace WinServicesRAG.Core.Processing;

/// <summary>
///     Job processing engine specifically for screenshot capture tasks
/// </summary>
public class ScreenshotJobProcessingEngine(
    IApiClient apiClient,
    IScreenshotManager screenshotManager,
    IImageCompressionService imageCompressionService,
    ILogger<ScreenshotJobProcessingEngine> logger) : JobProcessingEngineBase(apiClient, logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly IImageCompressionService _imageCompressionService = imageCompressionService ?? throw new ArgumentNullException(nameof(imageCompressionService));
    private readonly ILogger<ScreenshotJobProcessingEngine> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IScreenshotManager _screenshotManager = screenshotManager ?? throw new ArgumentNullException(nameof(screenshotManager));


    protected override async Task<JobProcessingResult> ProcessJobAsync(JobModel job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing screenshot job {JobName}", job.Name);

        try
        {
            var runtimeMode = RuntimeDataHelper.GetData(CommonValue.RUNTIME_MODE_KEY, CommonValue.RUNTIME_MODE_CLI).ToString()!;
            string captureMode = job.Data?.GetValueOrDefault(CommonValue.CAPTURE_MODE)?.ToString() ?? "all";
            var captureWithProvider = job.Data?.GetValueOrDefault(CommonValue.CAPTURE_WITH_PROVIDER)?.ToString();

            switch (captureMode)
            {
                // case "all":
                //     var allScreenshots = await _screenshotManager.TakeScreenshotAllProviderAsync(cancellationToken: cancellationToken);
                //     return new JobProcessingResult
                //     {
                //         JobName = job.Name,
                //         Success = allScreenshots.Success,
                //         ImageName = allScreenshots.ImageName,
                //         ErrorMessage = allScreenshots.ErrorMessage
                //     }

                case "specific":
                    if (captureWithProvider == null)
                    {
                        return new JobProcessingResult
                        {
                            JobName = job.Name,
                            Success = false,
                            Message = "Capture provider must be specified for 'specific' capture mode"
                        };
                    }

                    if (job.Data?.GetValueOrDefault(CommonValue.GetImageProviderKey(runtimeMode, captureWithProvider)) != null)
                    {
                        _logger.LogInformation("Capture provider '{CaptureProvider}' is already specified in job data for job {JobName}",
                            captureWithProvider, job.Name);
                        return new JobProcessingResult
                        {
                            JobName = job.Name,
                            Success = true,
                            Message = $"Capture provider '{captureWithProvider}' is already specified in job data"
                        };
                    }

                    ScreenshotResult screenshotResult = await _screenshotManager.TakeScreenshotAsync(captureWithProvider, cancellationToken);
                    if (!screenshotResult.Success || screenshotResult.ImageData == null)
                    {
                        string errorMessage = screenshotResult.ErrorMessage ?? "Unknown screenshot error";
                        _logger.LogError("Screenshot failed for job {JobId}: {Error}", job.Name, errorMessage);

                        return new JobProcessingResult
                        {
                            JobName = job.Name,
                            Success = false,
                            Message = errorMessage
                        };
                    }

                    // Generate file name
                    var fileName = $"screenshot_{job.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
                    _logger.LogInformation("Screenshot captured for job {JobName}, uploading as {FileName} ({Size} bytes)",
                        job.Name, fileName, screenshotResult.ImageData.Length);

                    // Apply image compression before upload
                    byte[] finalImageData = screenshotResult.ImageData;
                    var finalContentType = "image/png";

                    try
                    {
                        CompressionResult compressionResult = await _imageCompressionService.CompressForUploadAsync(screenshotResult.ImageData, 75, "jpg");
                        if (compressionResult.Success && compressionResult.ImageData.Length > 0)
                        {
                            finalImageData = compressionResult.ImageData;
                            finalContentType = compressionResult.ContentType;

                            _logger.LogInformation("Image compression applied for job {JobName}: {OriginalSize} → {CompressedSize} bytes ({ReductionPercentage:F1}% reduction)",
                                job.Name, compressionResult.OriginalSize, compressionResult.CompressedSize, compressionResult.SizeReductionPercentage);

                            // Update filename extension if format changed
                            if (compressionResult.ContentType == "image/jpeg" && fileName.EndsWith(".png"))
                            {
                                fileName = fileName.Replace(".png", ".jpg");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Image compression failed for job {JobName}, using original image: {ErrorMessage}",
                                job.Name, compressionResult.ErrorMessage);
                        }
                    }
                    catch (Exception compressionEx)
                    {
                        _logger.LogError(compressionEx, "Error during image compression for job {JobName}, using original image", job.Name);
                    }

                    try
                    {
                        ImageUploadResponse uploadResponse = await _apiClient.UploadImageAsync(finalImageData, fileName, finalContentType, cancellationToken);

                        bool updateSuccess = await _apiClient.UpdateJobStatusAsync(
                            job.Name,
                            JobStatus.AgentScreenCapture,
                            uploadResponse.FileName,
                            data: new Dictionary<string, object>
                            {
                                { CommonValue.GetImageProviderKey(runtimeMode, captureWithProvider), uploadResponse.FileName }
                            },
                            cancellationToken: cancellationToken);

                        if (updateSuccess)
                        {
                            return new JobProcessingResult
                            {
                                JobName = job.Name,
                                Success = true,
                                ImageName = fileName
                            };
                        }

                        return new JobProcessingResult
                        {
                            JobName = job.Name,
                            Success = false,
                            Message = "Failed to update job status after screenshot upload"
                        };


                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload screenshot for job {JobName}", job.Name);
                        return new JobProcessingResult
                        {
                            JobName = job.Name,
                            Success = false,
                            Message = "Failed to upload screenshot to API server"
                        };
                    }

                default:
                    return new JobProcessingResult
                    {
                        JobName = job.Name,
                        Success = false,
                        Message = $"Unsupported capture mode: {captureMode}"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while processing screenshot job {JobId}", job.Name);

            return new JobProcessingResult
            {
                JobName = job.Name,
                Success = false,
                Message = ex.Message
            };
        }
    }
}
