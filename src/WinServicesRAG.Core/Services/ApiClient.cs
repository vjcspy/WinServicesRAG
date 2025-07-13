using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Value;
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
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

        ConfigureHttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<JobModel?> GetJobAsync(string jobName, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrEmpty(jobName))
            throw new ArgumentException("Job ID cannot be null or empty", nameof(jobName));

        try
        {
            _logger.LogDebug("Getting job {JobId}", jobName);

            JobModel? response = await ExecuteWithRetryAsync(operation: async () =>
            {
                HttpResponseMessage httpResponse = await _httpClient.GetAsync($"/{CommonValue.JOB_URL_PATH}/{jobName}", cancellationToken);
                return await ProcessHttpResponseAsync<JobModel>(httpResponse);
            }, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job {JobId}", jobName);
            return null;
        }
    }

    // public async Task<List<JobModel>> GetJobsAsync(string status, string? jobType = null, int limit = 100, CancellationToken cancellationToken = default(CancellationToken))
    // {
    //     if (string.IsNullOrEmpty(value: status))
    //         throw new ArgumentException(message: "Status cannot be null or empty", paramName: nameof(status));
    //
    //     try
    //     {
    //         _logger.LogDebug(message: "Getting jobs with status {Status}, type {JobType}, limit {Limit}", status, jobType ?? "any", limit);
    //
    //         var queryParams = new List<string>
    //         {
    //             $"status={Uri.EscapeDataString(stringToEscape: status)}",
    //             $"limit={limit}"
    //         };
    //
    //         if (!string.IsNullOrEmpty(value: jobType))
    //         {
    //             queryParams.Add(item: $"type={Uri.EscapeDataString(stringToEscape: jobType)}");
    //         }
    //
    //         var queryString = string.Join(separator: "&", values: queryParams);
    //
    //         var response = await ExecuteWithRetryAsync(operation: async () =>
    //         {
    //             HttpResponseMessage httpResponse = await _httpClient.GetAsync(requestUri: $"/api/jobs?{queryString}", cancellationToken: cancellationToken);
    //             return await ProcessHttpResponseAsync<ApiResponse<JobListResponse>>(response: httpResponse);
    //         }, cancellationToken: cancellationToken);
    //
    //         if (response?.Success == true && response.Data?.Jobs != null)
    //         {
    //             _logger.LogDebug(message: "Successfully retrieved {Count} jobs with status {Status}", response.Data.Jobs.Count, status);
    //             return response.Data.Jobs;
    //         }
    //
    //         _logger.LogWarning(message: "No jobs found with status {Status} or API returned unsuccessful response", status);
    //         return new List<JobModel>();
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(exception: ex, message: "Failed to get jobs with status {Status}", status);
    //         return new List<JobModel>();
    //     }
    // }

    public async Task<ImageUploadResponse> UploadImageAsync(byte[] imageData, string fileName, string contentType = "image/png", CancellationToken cancellationToken = default(CancellationToken))
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

            ImageUploadResponse? response = await ExecuteWithRetryAsync(operation: async () =>
            {
                using var form = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(imageData);

                imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(imageContent, "file", fileName);

                HttpResponseMessage httpResponse = await _httpClient.PostAsync("/upload/image", form, cancellationToken);
                return await ProcessHttpResponseAsync<ImageUploadResponse>(httpResponse);
            }, cancellationToken);

            if (response == null) throw new InvalidOperationException("Failed to upload image: No response received");

            _logger.LogDebug("Successfully uploaded image {FileName}", fileName);
            return response;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image {FileName}", fileName);
            throw;
        }
    }

    public async Task<bool> UpdateJobStatusAsync(string jobName, string status, string? imageName = null, string? errorMessage = null, Dictionary<string, object>? data = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrEmpty(jobName))
            throw new ArgumentException("Job Name cannot be null or empty", nameof(jobName));

        if (string.IsNullOrEmpty(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));

        try
        {
            _logger.LogDebug("Updating job {JobName} status to {Status}", jobName, status);

            var updateRequest = new UpdateJobStatusRequest
            {
                Status = status,
                ErrorMessage = errorMessage,
                Data = data
            };

            JobModel? response = await ExecuteWithRetryAsync(operation: async () =>
            {
                string json = JsonSerializer.Serialize(updateRequest, _jsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage httpResponse = await _httpClient.PatchAsync($"/{CommonValue.JOB_URL_PATH}/{jobName}", content, cancellationToken);
                return await ProcessHttpResponseAsync<JobModel>(httpResponse);
            }, cancellationToken);

            if (response != null)
            {
                _logger.LogDebug("Successfully updated job {JobName} status to {Status}", jobName, status);
                return true;
            }

            _logger.LogWarning("Failed to update job {JobName}", jobName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job {JobName} status to {Status}", jobName, status);
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
        string content = await response.Content.ReadAsStringAsync();

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

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
