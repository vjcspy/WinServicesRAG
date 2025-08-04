using System.Runtime.InteropServices;

namespace WinServicesRAG.Core.Configuration;

/// <summary>
/// Configuration for WatchdogService with smart path resolution
/// </summary>
public class WatchdogServiceConfig
{
    public const string SectionName = "WatchdogService";
    public const string ScreenshotCaptureLogFileName = "xxx_service";
    public const string WatchdogLogFileName = "watchdog_service";

    /// <summary>
    /// Path to ScreenshotCapture executable. If not specified, will auto-detect.
    /// </summary>
    public string? ScreenshotExecutablePath { get; set; }

    /// <summary>
    /// Named pipe name for IPC communication
    /// </summary>
    public string IpcPipeName { get; set; } = "WinServicesRAG_IPC";

    /// <summary>
    /// Heartbeat interval in seconds for health monitoring
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Delay before restarting crashed process in seconds
    /// </summary>
    public int ProcessRestartDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Enable support for multiple user sessions (RDP scenarios)
    /// </summary>
    public bool EnableMultiSession { get; set; } = true;

    /// <summary>
    /// Maximum number of restart attempts before giving up
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 3;

    /// <summary>
    /// Timeout for process startup in seconds
    /// </summary>
    public int ProcessStartupTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets the resolved path to SystemMonitor executable with smart detection
    /// </summary>
    /// <returns>Full path to SystemMonitor.exe</returns>
    public string GetResolvedScreenshotExecutablePath()
    {
        if (!string.IsNullOrEmpty(ScreenshotExecutablePath) && File.Exists(ScreenshotExecutablePath))
        {
            return Path.GetFullPath(ScreenshotExecutablePath);
        }

        return ResolveScreenshotExecutablePath();
    }

    /// <summary>
    /// Smart resolution of SystemMonitor.exe path based on common deployment scenarios
    /// </summary>
    /// <returns>Best guess path to SystemMonitor.exe</returns>
    private static string ResolveScreenshotExecutablePath()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? "SystemMonitor.exe" 
            : "SystemMonitor";

        // Define search paths in order of preference
        var searchPaths = new[]
        {
            // 1. Same directory as WatchdogService (production deployment)
            Path.Combine(baseDirectory, executableName),
            
            // 2. ScreenshotService subdirectory (publish output structure)
            Path.Combine(baseDirectory, "ScreenshotService", executableName),
            
            // 3. Sibling directory in development environment
            Path.Combine(baseDirectory, "..", "ScreenshotCapture", executableName),
            Path.Combine(baseDirectory, "..", "..", "ScreenshotCapture", "bin", "Debug", "net9.0", executableName),
            Path.Combine(baseDirectory, "..", "..", "ScreenshotCapture", "bin", "Release", "net9.0", executableName),
            
            // 4. Published output directory structure
            Path.Combine(baseDirectory, "..", "..", "publish", "ScreenshotService", executableName),
            
            // 5. Development workspace structure
            Path.Combine(baseDirectory, "..", "..", "..", "ScreenshotCapture", "bin", "Debug", "net9.0", executableName),
            Path.Combine(baseDirectory, "..", "..", "..", "ScreenshotCapture", "bin", "Release", "net9.0", executableName),
            
            // 6. Absolute path fallback for known development environment
            @"e:\cs\WinServicesRAG\src\ScreenshotCapture\bin\Debug\net9.0\" + executableName,
            @"e:\cs\WinServicesRAG\src\ScreenshotCapture\bin\Release\net9.0\" + executableName,
            @"e:\cs\WinServicesRAG\publish\ScreenshotService\" + executableName
        };

        // Find first existing path
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        // If not found, return the most likely production path
        var fallbackPath = Path.Combine(baseDirectory, executableName);
        return Path.GetFullPath(fallbackPath);
    }

    /// <summary>
    /// Validates the configuration and returns any issues found
    /// </summary>
    /// <returns>List of validation errors, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        var executablePath = GetResolvedScreenshotExecutablePath();
        if (!File.Exists(executablePath))
        {
            errors.Add($"SystemMonitor executable not found at: {executablePath}");
        }

        if (HeartbeatIntervalSeconds <= 0)
        {
            errors.Add("HeartbeatIntervalSeconds must be greater than 0");
        }

        if (ProcessRestartDelaySeconds < 0)
        {
            errors.Add("ProcessRestartDelaySeconds must be greater than or equal to 0");
        }

        if (MaxRestartAttempts <= 0)
        {
            errors.Add("MaxRestartAttempts must be greater than 0");
        }

        if (ProcessStartupTimeoutSeconds <= 0)
        {
            errors.Add("ProcessStartupTimeoutSeconds must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(IpcPipeName))
        {
            errors.Add("IpcPipeName cannot be empty");
        }

        return errors;
    }

    /// <summary>
    /// Gets configuration information for logging/debugging
    /// </summary>
    /// <returns>Configuration summary</returns>
    public string GetConfigurationSummary()
    {
        var executablePath = GetResolvedScreenshotExecutablePath();
        var executableExists = File.Exists(executablePath);

        return $@"WatchdogService Configuration:
- SystemMonitor Path: {executablePath} [{(executableExists ? "EXISTS" : "NOT FOUND")}]
- IPC Pipe Name: {IpcPipeName}
- Heartbeat Interval: {HeartbeatIntervalSeconds}s
- Restart Delay: {ProcessRestartDelaySeconds}s
- Max Restart Attempts: {MaxRestartAttempts}
- Startup Timeout: {ProcessStartupTimeoutSeconds}s
- Multi-Session Support: {EnableMultiSession}";
    }
}
