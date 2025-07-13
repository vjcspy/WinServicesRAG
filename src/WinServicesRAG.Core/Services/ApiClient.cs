using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinServicesRAG.Core.Models;
namespace WinServicesRAG.Core.Services;

/// <summary>
///     HTTP-based API client implementation with retry logic and error handling
/// </summary>
public sealed class ApiClient : IApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ApiClient> _logger;
    private readonly ApiClientOptions _options;
    private bool _disposed;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger, IOptions<ApiClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(paramName: nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(paramName: nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(paramName: nameof(options));

        ConfigureHttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<JobModel?> GetJobAsync(string jobId, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrEmpty(value: jobId))
            throw new ArgumentException(message: "Job ID cannot be null or empty", paramName: nameof(jobId));

        try
        {
            _logger.LogDebug(message: "Getting job {JobId}", jobId);

            var response = await ExecuteWithRetryAsync(operation: async () =>
            {
                HttpResponseMessage httpResponse = await _httpClient.GetAsync(requestUri: $"/api/jobs/{jobId}", cancellationToken: cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<JobModel>>(response: httpResponse);
            }, cancellationToken: cancellationToken);

            if (response?.Success == true && response.Data != null)
            {
                _logger.LogDebug(message: "Successfully retrieved job {JobId} with status {Status}", jobId, response.Data.Status);
                return response.Data;
            }

            _logger.LogWarning(message: "Job {JobId} not found or API returned unsuccessful response", jobId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to get job {JobId}", jobId);
            return null;
        }
    }

    public async Task<List<JobModel>> GetJobsAsync(string status, string? jobType = null, int limit = 100, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrEmpty(value: status))
            throw new ArgumentException(message: "Status cannot be null or empty", paramName: nameof(status));

        try
        {
            _logger.LogDebug(message: "Getting jobs with status {Status}, type {JobType}, limit {Limit}", status, jobType ?? "any", limit);

            var queryParams = new List<string>
            {
                $"status={Uri.EscapeDataString(stringToEscape: status)}",
                $"limit={limit}"
            };

            if (!string.IsNullOrEmpty(value: jobType))
            {
                queryParams.Add(item: $"type={Uri.EscapeDataString(stringToEscape: jobType)}");
            }

            var queryString = string.Join(separator: "&", values: queryParams);

            var response = await ExecuteWithRetryAsync(operation: async () =>
            {
                HttpResponseMessage httpResponse = await _httpClient.GetAsync(requestUri: $"/api/jobs?{queryString}", cancellationToken: cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<JobListResponse>>(response: httpResponse);
            }, cancellationToken: cancellationToken);

            if (response?.Success == true && response.Data?.Jobs != null)
            {
                _logger.LogDebug(message: "Successfully retrieved {Count} jobs with status {Status}", response.Data.Jobs.Count, status);
                return response.Data.Jobs;
            }

            _logger.LogWarning(message: "No jobs found with status {Status} or API returned unsuccessful response", status);
            return new List<JobModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to get jobs with status {Status}", status);
            return new List<JobModel>();
        }
    }

    public async Task<ImageUploadResponse> UploadImageAsync(byte[] imageData, string fileName, string contentType = "image/png", CancellationToken cancellationToken = default(CancellationToken))
    {
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentException(message: "Image data cannot be null or empty", paramName: nameof(imageData));

        if (string.IsNullOrEmpty(value: fileName))
            throw new ArgumentException(message: "File name cannot be null or empty", paramName: nameof(fileName));

        if (imageData.Length > _options.MaxUploadSizeBytes)
            throw new ArgumentException(message: $"Image size ({imageData.Length} bytes) exceeds maximum allowed size ({_options.MaxUploadSizeBytes} bytes)", paramName: nameof(imageData));

        try
        {
            _logger.LogDebug(message: "Uploading image {FileName} ({Size} bytes)", fileName, imageData.Length);

            var response = await ExecuteWithRetryAsync(operation: async () =>
            {
                using var form = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(content: imageData);

                imageContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType: contentType);
                form.Add(content: imageContent, name: "file", fileName: fileName);

                HttpResponseMessage httpResponse = await _httpClient.PostAsync(requestUri: "/api/upload/image", content: form, cancellationToken: cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<ImageUploadResponse>>(response: httpResponse);
            }, cancellationToken: cancellationToken);

            if (response?.Success == true && response.Data != null)
            {
                _logger.LogDebug(message: "Successfully uploaded image {FileName}", fileName);
                return response.Data;
            }

            throw new InvalidOperationException(message: $"Failed to upload image: {response?.Error ?? "Unknown error"}");
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to upload image {FileName}", fileName);
            throw;
        }
    }

    public async Task<bool> UpdateJobStatusAsync(string jobId, string status, string? imageName = null, string? errorMessage = null, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrEmpty(value: jobId))
            throw new ArgumentException(message: "Job ID cannot be null or empty", paramName: nameof(jobId));

        if (string.IsNullOrEmpty(value: status))
            throw new ArgumentException(message: "Status cannot be null or empty", paramName: nameof(status));

        try
        {
            _logger.LogDebug(message: "Updating job {JobId} status to {Status}", jobId, status);

            var updateRequest = new UpdateJobStatusRequest
            {
                Status = status,
                ImageName = imageName,
                ErrorMessage = errorMessage,
                Metadata = metadata
            };

            var response = await ExecuteWithRetryAsync(operation: async () =>
            {
                string json = JsonSerializer.Serialize(value: updateRequest, options: _jsonOptions);
                using var content = new StringContent(content: json, encoding: Encoding.UTF8, mediaType: "application/json");

                HttpResponseMessage httpResponse = await _httpClient.PatchAsync(requestUri: $"/api/jobs/{jobId}", content: content, cancellationToken: cancellationToken);
                return await ProcessHttpResponseAsync<ApiResponse<object>>(response: httpResponse);
            }, cancellationToken: cancellationToken);

            if (response?.Success == true)
            {
                _logger.LogDebug(message: "Successfully updated job {JobId} status to {Status}", jobId, status);
                return true;
            }

            _logger.LogWarning(message: "Failed to update job {JobId} status: {Error}", jobId, response?.Error ?? "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to update job {JobId} status to {Status}", jobId, status);
            return false;
        }
    }

    public async Task<bool> UploadImageAndUpdateJobAsync(string jobId, byte[] imageData, string fileName, CancellationToken cancellationToken = default(CancellationToken))
    {
        try
        {
            _logger.LogDebug(message: "Uploading image and updating job {JobId}", jobId);

            // Upload image first
            ImageUploadResponse uploadResponse = await UploadImageAsync(imageData: imageData, fileName: fileName, contentType: "image/png", cancellationToken: cancellationToken);

            // Update job status with uploaded image name
            bool updateSuccess = await UpdateJobStatusAsync(jobId: jobId, status: JobStatus.TakeScreenshotSuccess, imageName: uploadResponse.FileName, cancellationToken: cancellationToken);

            if (updateSuccess)
            {
                _logger.LogInformation(message: "Successfully uploaded image and updated job {JobId}", jobId);
                return true;
            }

            _logger.LogWarning(message: "Image uploaded but failed to update job {JobId} status", jobId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to upload image and update job {JobId}", jobId);

            // Try to update job status to error
            try
            {
                await UpdateJobStatusAsync(jobId: jobId, status: JobStatus.Error, errorMessage: ex.Message, cancellationToken: cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(exception: updateEx, message: "Failed to update job {JobId} status to error after upload failure", jobId);
            }

            return false;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        try
        {
            _logger.LogDebug(message: "Performing API health check");

            HttpResponseMessage response = await _httpClient.GetAsync(requestUri: "/api/health", cancellationToken: cancellationToken);
            bool isHealthy = response.IsSuccessStatusCode;

            _logger.LogDebug(message: "API health check result: {IsHealthy}", isHealthy);
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(exception: ex, message: "API health check failed");
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(obj: this);
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(uriString: _options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(seconds: _options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add(name: "User-Agent", value: _options.UserAgent);

        if (!string.IsNullOrEmpty(value: _options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add(name: "Authorization", value: $"Bearer {_options.ApiKey}");
        }
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<T?>> operation, CancellationToken cancellationToken) where T : class
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= _options.RetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (attempt < _options.RetryAttempts)
            {
                lastException = ex;
                _logger.LogWarning(exception: ex, message: "HTTP request failed on attempt {Attempt}/{MaxAttempts}, retrying in {DelayMs}ms",
                    attempt, _options.RetryAttempts, _options.RetryDelayMs);

                await Task.Delay(millisecondsDelay: _options.RetryDelayMs, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < _options.RetryAttempts)
            {
                lastException = ex;
                _logger.LogWarning(exception: ex, message: "Request timed out on attempt {Attempt}/{MaxAttempts}, retrying in {DelayMs}ms",
                    attempt, _options.RetryAttempts, _options.RetryDelayMs);

                await Task.Delay(millisecondsDelay: _options.RetryDelayMs, cancellationToken: cancellationToken);
            }
        }

        if (lastException != null)
            throw lastException;

        return null;
    }

    private async Task<T?> ProcessHttpResponseAsync<T>(HttpResponseMessage response) where T : class
    {
        string content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(message: "HTTP request failed with status {StatusCode}: {Content}", response.StatusCode, content);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            throw new HttpRequestException(message: $"HTTP {response.StatusCode}: {content}");
        }

        if (string.IsNullOrEmpty(value: content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json: content, options: _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(exception: ex, message: "Failed to deserialize response: {Content}", content);
            throw;
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
