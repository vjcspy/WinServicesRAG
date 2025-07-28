namespace WinServicesRAG.Core.Process;

/// <summary>
/// Information about a launched process
/// </summary>
public class ProcessInfo
{
    public int ProcessId { get; set; }
    public int SessionId { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public bool IsRunning { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Interface for launching processes in user sessions
/// </summary>
public interface IUserSessionProcessLauncher
{
    /// <summary>
    /// Launches a process in the specified user session
    /// </summary>
    /// <param name="sessionId">Target session ID</param>
    /// <param name="executablePath">Full path to executable</param>
    /// <param name="arguments">Command line arguments</param>
    /// <returns>Process information</returns>
    Task<ProcessInfo> LaunchInUserSessionAsync(int sessionId, string executablePath, string arguments = "");

    /// <summary>
    /// Terminates a process by process ID
    /// </summary>
    /// <param name="processId">Process ID to terminate</param>
    /// <returns>True if successfully terminated</returns>
    Task<bool> TerminateProcessAsync(int processId);

    /// <summary>
    /// Checks if a process is still running
    /// </summary>
    /// <param name="processId">Process ID to check</param>
    /// <returns>True if process is running</returns>
    bool IsProcessRunning(int processId);

    /// <summary>
    /// Gets process information by process ID
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <returns>Process info or null if not found</returns>
    Task<ProcessInfo?> GetProcessInfoAsync(int processId);

    /// <summary>
    /// Kills all processes with the specified executable name in a session
    /// </summary>
    /// <param name="sessionId">Target session ID</param>
    /// <param name="executableName">Executable name (without path)</param>
    /// <returns>Number of processes terminated</returns>
    Task<int> KillProcessesByNameAsync(int sessionId, string executableName);
}
