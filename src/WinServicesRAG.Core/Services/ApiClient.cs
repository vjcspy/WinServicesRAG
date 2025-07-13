using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinServicesRAG.Core.Models;

namespace WinServicesRAG.Core.Services;

/// <summary>
/// HTTP-based API client implementation with retry logic and error handling
/// </summary>
public class ApiClient : IApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly ApiClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger, IOptions<ApiClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        ConfigureHttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
        
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
        }
    }

    public async Task<JobModel?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty", nameof(jobId));

        try
        {
            _logger.LogDebug("Getting job {JobId}", jobId);
            
            var response = await ExecuteWithRetryAsync(async () =>
            {
                var httpResponse = await _httpClient.GetAsync($"/api/jobs/{jobId}", cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<JobModel>>(httpResponse);
            }, cancellationToken);

            if (response?.Success == true && response.Data != null)
            {
                _logger.LogDebug("Successfully retrieved job {JobId} with status {Status}", jobId, response.Data.Status);
                return response.Data;
            }

            _logger.LogWarning("Job {JobId} not found or API returned unsuccessful response", jobId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job {JobId}", jobId);
            return null;
        }
    }

    public async Task<List<JobModel>> GetJobsAsync(string status, string? jobType = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));

        try
        {
            _logger.LogDebug("Getting jobs with status {Status}, type {JobType}, limit {Limit}", status, jobType ?? "any", limit);

            var queryParams = new List<string>
            {
                $"status={Uri.EscapeDataString(status)}",
                $"limit={limit}"
            };

            if (!string.IsNullOrEmpty(jobType))
            {
                queryParams.Add($"type={Uri.EscapeDataString(jobType)}");
            }

            var queryString = string.Join("&", queryParams);
            
            var response = await ExecuteWithRetryAsync(async () =>
            {
                var httpResponse = await _httpClient.GetAsync($"/api/jobs?{queryString}", cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<JobListResponse>>(httpResponse);
            }, cancellationToken);

            if (response?.Success == true && response.Data?.Jobs != null)
            {
                _logger.LogDebug("Successfully retrieved {Count} jobs with status {Status}", response.Data.Jobs.Count, status);
                return response.Data.Jobs;
            }

            _logger.LogWarning("No jobs found with status {Status} or API returned unsuccessful response", status);
            return new List<JobModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get jobs with status {Status}", status);
            return new List<JobModel>();
        }
    }

    public async Task<ImageUploadResponse> UploadImageAsync(byte[] imageData, string fileName, string contentType = "image/png", CancellationToken cancellationToken = default)
    {
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));

        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

        if (imageData.Length > _options.MaxUploadSizeBytes)
            throw new ArgumentException($"Image size ({imageData.Length} bytes) exceeds maximum allowed size ({_options.MaxUploadSizeBytes} bytes)", nameof(imageData));

        try
        {
            _logger.LogDebug("Uploading image {FileName} ({Size} bytes)", fileName, imageData.Length);

            var response = await ExecuteWithRetryAsync(async () =>
            {
                using var form = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(imageData);
                
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                form.Add(imageContent, "file", fileName);

                var httpResponse = await _httpClient.PostAsync("/api/upload/image", form, cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<ImageUploadResponse>>(httpResponse);
            }, cancellationToken);

            if (response?.Success == true && response.Data != null)
            {
                _logger.LogDebug("Successfully uploaded image {FileName}", fileName);
                return response.Data;
            }

            throw new InvalidOperationException($"Failed to upload image: {response?.Error ?? "Unknown error"}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image {FileName}", fileName);
            throw;
        }
    }

    public async Task<bool> UpdateJobStatusAsync(string jobId, string status, string? imageName = null, string? errorMessage = null, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty", nameof(jobId));

        if (string.IsNullOrEmpty(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));

        try
        {
            _logger.LogDebug("Updating job {JobId} status to {Status}", jobId, status);

            var updateRequest = new UpdateJobStatusRequest
            {
                Status = status,
                ImageName = imageName,
                ErrorMessage = errorMessage,
                Metadata = metadata
            };

            var response = await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonSerializer.Serialize(updateRequest, _jsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var httpResponse = await _httpClient.PatchAsync($"/api/jobs/{jobId}", content, cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<object>>(httpResponse);
            }, cancellationToken);

            if (response?.Success == true)
            {
                _logger.LogDebug("Successfully updated job {JobId} status to {Status}", jobId, status);
                return true;
            }

            _logger.LogWarning("Failed to update job {JobId} status: {Error}", jobId, response?.Error ?? "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job {JobId} status to {Status}", jobId, status);
            return false;
        }
    }

    public async Task<bool> UploadImageAndUpdateJobAsync(string jobId, byte[] imageData, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Uploading image and updating job {JobId}", jobId);

            // Upload image first
            var uploadResponse = await UploadImageAsync(imageData, fileName, "image/png", cancellationToken);
            
            // Update job status with uploaded image name
            var updateSuccess = await UpdateJobStatusAsync(jobId, JobStatus.TakeScreenshotSuccess, uploadResponse.FileName, cancellationToken: cancellationToken);

            if (updateSuccess)
            {
                _logger.LogInformation("Successfully uploaded image and updated job {JobId}", jobId);
                return true;
            }

            _logger.LogWarning("Image uploaded but failed to update job {JobId} status", jobId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image and update job {JobId}", jobId);
            
            // Try to update job status to error
            try
            {
                await UpdateJobStatusAsync(jobId, JobStatus.Error, errorMessage: ex.Message, cancellationToken: cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update job {JobId} status to error after upload failure", jobId);
            }

            return false;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Performing API health check");

            var response = await _httpClient.GetAsync("/api/health", cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogDebug("API health check result: {IsHealthy}", isHealthy);
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API health check failed");
            return false;
        }
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<T?>> operation, CancellationToken cancellationToken) where T : class
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= _options.RetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (attempt < _options.RetryAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "HTTP request failed on attempt {Attempt}/{MaxAttempts}, retrying in {DelayMs}ms", 
                    attempt, _options.RetryAttempts, _options.RetryDelayMs);
                
                await Task.Delay(_options.RetryDelayMs, cancellationToken);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < _options.RetryAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Request timed out on attempt {Attempt}/{MaxAttempts}, retrying in {DelayMs}ms", 
                    attempt, _options.RetryAttempts, _options.RetryDelayMs);
                
                await Task.Delay(_options.RetryDelayMs, cancellationToken);
            }
        }

        if (lastException != null)
            throw lastException;

        return null;
    }

    private async Task<T?> ProcessHttpResponseAsync<T>(HttpResponseMessage response) where T : class
    {
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("HTTP request failed with status {StatusCode}: {Content}", response.StatusCode, content);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
                
            throw new HttpRequestException($"HTTP {response.StatusCode}: {content}");
        }

        if (string.IsNullOrEmpty(content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response: {Content}", content);
            throw;
        }
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
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
