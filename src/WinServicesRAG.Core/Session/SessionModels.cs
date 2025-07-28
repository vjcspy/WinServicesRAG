namespace WinServicesRAG.Core.Session;

/// <summary>
/// Represents information about a Windows session
/// </summary>
public class SessionInfo
{
    public int SessionId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public SessionState State { get; set; }
    public bool IsConsole { get; set; }
    public DateTime LastActivity { get; set; }
}

/// <summary>
/// Windows session states
/// </summary>
public enum SessionState
{
    Active = 0,
    Connected = 1,
    ConnectQuery = 2,
    Shadow = 3,
    Disconnected = 4,
    Idle = 5,
    Listen = 6,
    Reset = 7,
    Down = 8,
    Init = 9
}

/// <summary>
/// Session change event arguments
/// </summary>
public class SessionEventArgs : EventArgs
{
    public SessionInfo SessionInfo { get; set; }
    public SessionChangeType ChangeType { get; set; }

    public SessionEventArgs(SessionInfo sessionInfo, SessionChangeType changeType)
    {
        SessionInfo = sessionInfo;
        ChangeType = changeType;
    }
}

/// <summary>
/// Types of session changes
/// </summary>
public enum SessionChangeType
{
    SessionLogon,
    SessionLogoff,
    SessionLock,
    SessionUnlock,
    SessionRemoteConnect,
    SessionRemoteDisconnect
}
