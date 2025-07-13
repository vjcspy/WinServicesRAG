using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Processing;
using WinServicesRAG.Core.Services;

namespace ScreenshotCapture;

/// <summary>
/// Background service that processes screenshot jobs continuously
/// </summary>
public class ScreenshotBackgroundService : BackgroundService
{
    private readonly ILogger<ScreenshotBackgroundService> _logger;
    private readonly IScreenshotManager _screenshotManager;
    private readonly IJobProcessingEngine? _jobProcessingEngine;
    private readonly IApiClient _apiClient;
    private readonly ScreenshotServiceConfig _config;

    public ScreenshotBackgroundService(
        ILogger<ScreenshotBackgroundService> logger,
        IScreenshotManager screenshotManager,
        IApiClient apiClient,
        IOptions<ScreenshotServiceConfig> config,
        IJobProcessingEngine? jobProcessingEngine = null)
    {
        _logger = logger;
        _screenshotManager = screenshotManager;
        _apiClient = apiClient;
        _jobProcessingEngine = jobProcessingEngine;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Screenshot Background Service started");
        _logger.LogInformation("Working directory: {WorkDir}", _config.WorkDirectory);
        _logger.LogInformation("Poll interval: {PollInterval} seconds", _config.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScreenshotJobs(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing screenshot jobs");
                await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("Screenshot Background Service stopped");
    }

    private async Task ProcessScreenshotJobs(CancellationToken stoppingToken)
    {
        var jobsDir = Path.Combine(_config.WorkDirectory, "jobs");
        var screenshotsDir = Path.Combine(_config.WorkDirectory, "screenshots");
        var resultsDir = Path.Combine(_config.WorkDirectory, "results");

        // Ensure directories exist
        Directory.CreateDirectory(jobsDir);
        Directory.CreateDirectory(screenshotsDir);
        Directory.CreateDirectory(resultsDir);

        // Look for pending job files
        var jobFiles = Directory.GetFiles(jobsDir, "*.job", SearchOption.TopDirectoryOnly);

        foreach (var jobFile in jobFiles)
        {
            try
            {
                _logger.LogInformation("Processing job file: {JobFile}", Path.GetFileName(jobFile));

                // Read job details (JSON format expected)
                var jobContent = await File.ReadAllTextAsync(jobFile);

                // Simple parsing - in real implementation, use proper JSON deserialization
                var lines = jobContent.Split('\n');
                var jobId = ExtractValue(lines, "job_id");
                var outputFile = Path.Combine(screenshotsDir, $"{jobId}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                _logger.LogInformation("Taking screenshot for job: {JobId}", jobId);

                // Take screenshot
                var screenshotResult = await _screenshotManager.TakeScreenshotAsync(stoppingToken);

                if (screenshotResult.Success && screenshotResult.ImageData != null && screenshotResult.ImageData.Length > 0)
                {
                    // Save screenshot
                    await File.WriteAllBytesAsync(outputFile, screenshotResult.ImageData);

                    // Create result file
                    var resultFile = Path.Combine(resultsDir, $"{jobId}.result");

                    await File.WriteAllTextAsync(resultFile, $@"{{
    ""job_id"": ""{jobId}"",
    ""status"": ""SUCCESS"",
    ""screenshot_path"": ""{outputFile}"",
    ""file_size"": {screenshotResult.ImageData.Length},
    ""timestamp"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}""
}}");

                    _logger.LogInformation("Screenshot completed: {OutputFile} ({Size:N0} bytes)", outputFile, screenshotResult.ImageData.Length);
                }
                else
                {
                    // Create error result file
                    var resultFile = Path.Combine(resultsDir, $"{jobId}.result");

                    await File.WriteAllTextAsync(resultFile, $@"{{
    ""job_id"": ""{jobId}"",
    ""status"": ""ERROR"",
    ""error_message"": ""{screenshotResult.ErrorMessage ?? "Failed to capture screenshot - all providers failed"}"",
    ""timestamp"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}""
}}");

                    _logger.LogWarning("Screenshot failed for job: {JobId}, Error: {Error}", jobId, screenshotResult.ErrorMessage);
                }

                // Remove processed job file
                File.Delete(jobFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job file {JobFile}", jobFile);
            }
        }
    }

    private static string ExtractValue(string[] lines, string key)
    {
        var line = lines.FirstOrDefault(l => l.Trim().StartsWith($"\"{key}\""));
        if (line != null)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var value = line.Substring(colonIndex + 1).Trim().Trim('"', ',');
                return value;
            }
        }
        return string.Empty;
    }
}
