namespace WinServicesRAG.Core.Value;

public class CommonValue
{

    public const string RUNTIME_MODE_KEY = "RUNTIME_MODE_KEY";
    public const string RUNTIME_MODE_CLI = "CLI";

    // API
    public const string JOB_URL_PATH = "image-question-jobs";

    // JOB DATA KEYS
    private const string IMAGE_PROVIDER_PREFIX = "IMAGE_PROVIDER";
    public const string CAPTURE_MODE = "capture_mode";
    public const string CAPTURE_WITH_PROVIDER = "capture_with_provider";
    public static string GetJobName()
    {
        return "PBF_EXAM";
    }

    public static string GetImageProviderKey(string runtimeMode,string providerName)
    {
        return $"{IMAGE_PROVIDER_PREFIX}|{providerName.ToUpperInvariant()}|{runtimeMode.ToUpperInvariant()}";
    }
}
