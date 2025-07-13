using System.Text.Json.Serialization;
namespace WinServicesRAG.Core.Models;

/// <summary>
///     Represents a job from the API server
/// </summary>
public class JobModel
{
    [JsonPropertyName(name: "job_name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName(name: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName(name: "data")]
    public Dictionary<string, object>? Data { get; set; }

    [JsonPropertyName(name: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName(name: "updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName(name: "error_message")]
    public string? ErrorMessage { get; set; }
}
/// <summary>
///     Request model for updating job status
/// </summary>
public class UpdateJobStatusRequest
{
    [JsonPropertyName(name: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName(name: "error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName(name: "data")]
    public Dictionary<string, object>? Data { get; set; }
}
/// <summary>
///     Response model for image upload
/// </summary>
public class ImageUploadResponse
{
    [JsonPropertyName(name: "filename")]
    public string FileName { get; set; } = string.Empty;
}
