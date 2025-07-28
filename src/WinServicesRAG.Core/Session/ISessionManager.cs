namespace WinServicesRAG.Core.Session;

/// <summary>
/// Interface for managing Windows sessions and detecting session changes
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Gets all active user sessions
    /// </summary>
    /// <returns>List of active user sessions</returns>
    Task<List<SessionInfo>> GetActiveUserSessionsAsync();

    /// <summary>
    /// Gets the console session (Session 1, typically the main desktop)
    /// </summary>
    /// <returns>Console session info or null if not found</returns>
    Task<SessionInfo?> GetConsoleSessionAsync();

    /// <summary>
    /// Checks if a specific session is active
    /// </summary>
    /// <param name="sessionId">Session ID to check</param>
    /// <returns>True if session is active</returns>
    bool IsSessionActive(int sessionId);

    /// <summary>
    /// Gets session information for a specific session ID
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Session info or null if not found</returns>
    Task<SessionInfo?> GetSessionInfoAsync(int sessionId);

    /// <summary>
    /// Event raised when session state changes (logon, logoff, etc.)
    /// </summary>
    event EventHandler<SessionEventArgs>? SessionChanged;

    /// <summary>
    /// Starts monitoring session changes
    /// </summary>
    /// <returns>Task representing the monitoring operation</returns>
    Task StartMonitoringAsync();

    /// <summary>
    /// Stops monitoring session changes
    /// </summary>
    /// <returns>Task representing the stop operation</returns>
    Task StopMonitoringAsync();
}
