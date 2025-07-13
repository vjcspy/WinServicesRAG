using WinServicesRAG.Core.Models;

namespace WinServicesRAG.Core.Services;

/// <summary>
/// Interface for API client operations
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Gets a specific job by ID
    /// </summary>
    /// <param name="jobName">The job identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The job model if found, null otherwise</returns>
    Task<JobModel?> GetJobAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an image file to the server
    /// </summary>
    /// <param name="imageData">Image data as byte array</param>
    /// <param name="fileName">Name of the file</param>
    /// <param name="contentType">MIME type of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload response with file information</returns>
    Task<ImageUploadResponse> UploadImageAsync(byte[] imageData, string fileName, string contentType = "image/png", CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates job status and related information
    /// </summary>
    /// <param name="jobName">Job identifier</param>
    /// <param name="status">New status</param>
    /// <param name="imageName">Optional image name if applicable</param>
    /// <param name="errorMessage">Optional error message</param>
    /// <param name="data">Optional additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateJobStatusAsync(string jobName, string status, string? imageName = null, string? errorMessage = null, Dictionary<string, object>? data = null, CancellationToken cancellationToken = default);
}
