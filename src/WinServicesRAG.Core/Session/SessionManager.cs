using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.ComponentModel;
using WinServicesRAG.Core.Session;

namespace WinServicesRAG.Core.Session;

/// <summary>
/// Windows Session Manager implementation using WTS (Windows Terminal Services) APIs
/// </summary>
public class SessionManager : ISessionManager, IDisposable
{
    private readonly ILogger<SessionManager> _logger;
    private bool _isMonitoring;
    private bool _disposed;

    // WTS API Constants
    private const int WTS_CURRENT_SERVER_HANDLE = 0;
    private const int NOTIFY_FOR_ALL_SESSIONS = 1;
    private const int NOTIFY_FOR_THIS_SESSION = 0;

    // Session change notifications
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_CONSOLE_CONNECT = 0x1;
    private const int WTS_CONSOLE_DISCONNECT = 0x2;
    private const int WTS_REMOTE_CONNECT = 0x3;
    private const int WTS_REMOTE_DISCONNECT = 0x4;
    private const int WTS_SESSION_LOGON = 0x5;
    private const int WTS_SESSION_LOGOFF = 0x6;
    private const int WTS_SESSION_LOCK = 0x7;
    private const int WTS_SESSION_UNLOCK = 0x8;

    // WTS API Structures
    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public int SessionId;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pWinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }

    private enum WTS_INFO_CLASS
    {
        WTSInitialProgram = 0,
        WTSApplicationName = 1,
        WTSWorkingDirectory = 2,
        WTSOEMId = 3,
        WTSSessionId = 4,
        WTSUserName = 5,
        WTSWinStationName = 6,
        WTSDomainName = 7,
        WTSConnectState = 8,
        WTSClientBuildNumber = 9,
        WTSClientName = 10,
        WTSClientDirectory = 11,
        WTSClientProductId = 12,
        WTSClientHardwareId = 13,
        WTSClientAddress = 14,
        WTSClientDisplay = 15,
        WTSClientProtocolType = 16,
        WTSIdleTime = 17,
        WTSLogonTime = 18,
        WTSIncomingBytes = 19,
        WTSOutgoingBytes = 20,
        WTSIncomingFrames = 21,
        WTSOutgoingFrames = 22,
        WTSClientInfo = 23,
        WTSSessionInfo = 24,
        WTSSessionInfoEx = 25,
        WTSConfigInfo = 26,
        WTSValidationInfo = 27,
        WTSSessionAddressV4 = 28,
        WTSIsRemoteSession = 29
    }

    // P/Invoke declarations
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int Reserved,
        int Version,
        ref IntPtr ppSessionInfo,
        ref int pCount);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        WTS_INFO_CLASS wtsInfoClass,
        out IntPtr ppBuffer,
        out int pBytesReturned);

    [DllImport("kernel32.dll")]
    private static extern int WTSGetActiveConsoleSessionId();

    public event EventHandler<SessionEventArgs>? SessionChanged;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
    }

    public async Task<List<SessionInfo>> GetActiveUserSessionsAsync()
    {
        var sessions = new List<SessionInfo>();

        try
        {
            IntPtr sessionInfoPtr = IntPtr.Zero;
            int sessionCount = 0;

            if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref sessionInfoPtr, ref sessionCount))
            {
                var sessionInfoSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                var currentSession = sessionInfoPtr;

                for (int i = 0; i < sessionCount; i++)
                {
                    var sessionInfo = Marshal.PtrToStructure<WTS_SESSION_INFO>(currentSession);
                    
                    // Only include user sessions (skip console, services, etc.)
                    if (sessionInfo.SessionId > 0 && IsUserSession(sessionInfo))
                    {
                        var sessionDetails = await GetSessionDetailsAsync(sessionInfo.SessionId);
                        if (sessionDetails != null)
                        {
                            sessions.Add(sessionDetails);
                        }
                    }

                    currentSession = IntPtr.Add(currentSession, sessionInfoSize);
                }

                WTSFreeMemory(sessionInfoPtr);
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to enumerate sessions. Error: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating sessions");
        }

        _logger.LogDebug("Found {Count} active user sessions", sessions.Count);
        return sessions;
    }

    public async Task<SessionInfo?> GetConsoleSessionAsync()
    {
        try
        {
            var consoleSessionId = WTSGetActiveConsoleSessionId();
            if (consoleSessionId != -1)
            {
                return await GetSessionDetailsAsync(consoleSessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting console session");
        }

        return null;
    }

    public bool IsSessionActive(int sessionId)
    {
        try
        {
            return GetSessionState(sessionId) == WTS_CONNECTSTATE_CLASS.WTSActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking session {SessionId} status", sessionId);
            return false;
        }
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(int sessionId)
    {
        return await GetSessionDetailsAsync(sessionId);
    }

    public async Task StartMonitoringAsync()
    {
        if (_isMonitoring)
        {
            _logger.LogWarning("Session monitoring is already started");
            return;
        }

        try
        {
            _isMonitoring = true;
            _logger.LogInformation("Session monitoring started");

            // Start background monitoring task
            _ = Task.Run(async () => await MonitorSessionChangesAsync());

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start session monitoring");
            _isMonitoring = false;
            throw;
        }
    }

    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
        {
            return;
        }

        try
        {
            _isMonitoring = false;
            _logger.LogInformation("Session monitoring stopped");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping session monitoring");
        }
    }

    private async Task MonitorSessionChangesAsync()
    {
        var lastSessionStates = new Dictionary<int, WTS_CONNECTSTATE_CLASS>();

        while (_isMonitoring && !_disposed)
        {
            try
            {
                var currentSessions = await GetActiveUserSessionsAsync();
                var currentSessionStates = new Dictionary<int, WTS_CONNECTSTATE_CLASS>();

                // Check for session changes
                foreach (var session in currentSessions)
                {
                    var state = GetSessionState(session.SessionId);
                    currentSessionStates[session.SessionId] = state;

                    if (lastSessionStates.TryGetValue(session.SessionId, out var lastState))
                    {
                        if (lastState != state)
                        {
                            // Session state changed
                            OnSessionStateChanged(session, state, lastState);
                        }
                    }
                    else
                    {
                        // New session detected
                        if (state == WTS_CONNECTSTATE_CLASS.WTSActive)
                        {
                            OnSessionLogon(session);
                        }
                    }
                }

                // Check for sessions that disappeared
                foreach (var lastSession in lastSessionStates.Keys.ToList())
                {
                    if (!currentSessionStates.ContainsKey(lastSession))
                    {
                        // Session ended
                        var sessionInfo = await GetSessionDetailsAsync(lastSession);
                        if (sessionInfo != null)
                        {
                            OnSessionLogoff(sessionInfo);
                        }
                    }
                }

                lastSessionStates = currentSessionStates;
                await Task.Delay(TimeSpan.FromSeconds(5)); // Poll every 5 seconds
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in session monitoring loop");
                await Task.Delay(TimeSpan.FromSeconds(10)); // Wait longer on error
            }
        }
    }

    private void OnSessionStateChanged(SessionInfo sessionInfo, WTS_CONNECTSTATE_CLASS newState, WTS_CONNECTSTATE_CLASS oldState)
    {
        _logger.LogDebug("Session {SessionId} state changed: {OldState} -> {NewState}", 
            sessionInfo.SessionId, oldState, newState);

        SessionChangeType? changeType = null;

        if (newState == WTS_CONNECTSTATE_CLASS.WTSActive && oldState != WTS_CONNECTSTATE_CLASS.WTSActive)
        {
            changeType = SessionChangeType.SessionLogon;
        }
        else if (oldState == WTS_CONNECTSTATE_CLASS.WTSActive && newState != WTS_CONNECTSTATE_CLASS.WTSActive)
        {
            changeType = SessionChangeType.SessionLogoff;
        }

        if (changeType.HasValue)
        {
            SessionChanged?.Invoke(this, new SessionEventArgs(sessionInfo, changeType.Value));
        }
    }

    private void OnSessionLogon(SessionInfo sessionInfo)
    {
        _logger.LogInformation("Session logon detected: {SessionId} - {UserName}", 
            sessionInfo.SessionId, sessionInfo.UserName);
        SessionChanged?.Invoke(this, new SessionEventArgs(sessionInfo, SessionChangeType.SessionLogon));
    }

    private void OnSessionLogoff(SessionInfo sessionInfo)
    {
        _logger.LogInformation("Session logoff detected: {SessionId} - {UserName}", 
            sessionInfo.SessionId, sessionInfo.UserName);
        SessionChanged?.Invoke(this, new SessionEventArgs(sessionInfo, SessionChangeType.SessionLogoff));
    }

    private bool IsUserSession(WTS_SESSION_INFO sessionInfo)
    {
        // Filter out system sessions and console
        return sessionInfo.State == WTS_CONNECTSTATE_CLASS.WTSActive ||
               sessionInfo.State == WTS_CONNECTSTATE_CLASS.WTSConnected ||
               sessionInfo.State == WTS_CONNECTSTATE_CLASS.WTSDisconnected;
    }

    private WTS_CONNECTSTATE_CLASS GetSessionState(int sessionId)
    {
        try
        {
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSConnectState, 
                out IntPtr buffer, out int bytesReturned))
            {
                var state = Marshal.ReadInt32(buffer);
                WTSFreeMemory(buffer);
                return (WTS_CONNECTSTATE_CLASS)state;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session {SessionId} state", sessionId);
        }

        return WTS_CONNECTSTATE_CLASS.WTSDown;
    }

    private async Task<SessionInfo?> GetSessionDetailsAsync(int sessionId)
    {
        try
        {
            await Task.Delay(100);
            var userName = GetSessionUserName(sessionId);
            var domainName = GetSessionDomainName(sessionId);
            var state = GetSessionState(sessionId);

            if (string.IsNullOrEmpty(userName))
            {
                return null; // Skip sessions without user names
            }

            return new SessionInfo
            {
                SessionId = sessionId,
                UserName = userName,
                Domain = domainName,
                State = ConvertSessionState(state),
                IsConsole = sessionId == WTSGetActiveConsoleSessionId(),
                LastActivity = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session details for session {SessionId}", sessionId);
            return null;
        }
    }

    private string GetSessionUserName(int sessionId)
    {
        try
        {
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSUserName, 
                out IntPtr buffer, out int bytesReturned))
            {
                var userName = Marshal.PtrToStringAuto(buffer) ?? string.Empty;
                WTSFreeMemory(buffer);
                return userName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user name for session {SessionId}", sessionId);
        }

        return string.Empty;
    }

    private string GetSessionDomainName(int sessionId)
    {
        try
        {
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSDomainName, 
                out IntPtr buffer, out int bytesReturned))
            {
                var domainName = Marshal.PtrToStringAuto(buffer) ?? string.Empty;
                WTSFreeMemory(buffer);
                return domainName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting domain name for session {SessionId}", sessionId);
        }

        return string.Empty;
    }

    private static SessionState ConvertSessionState(WTS_CONNECTSTATE_CLASS wtsState)
    {
        return wtsState switch
        {
            WTS_CONNECTSTATE_CLASS.WTSActive => SessionState.Active,
            WTS_CONNECTSTATE_CLASS.WTSConnected => SessionState.Connected,
            WTS_CONNECTSTATE_CLASS.WTSConnectQuery => SessionState.ConnectQuery,
            WTS_CONNECTSTATE_CLASS.WTSShadow => SessionState.Shadow,
            WTS_CONNECTSTATE_CLASS.WTSDisconnected => SessionState.Disconnected,
            WTS_CONNECTSTATE_CLASS.WTSIdle => SessionState.Idle,
            WTS_CONNECTSTATE_CLASS.WTSListen => SessionState.Listen,
            WTS_CONNECTSTATE_CLASS.WTSReset => SessionState.Reset,
            WTS_CONNECTSTATE_CLASS.WTSDown => SessionState.Down,
            WTS_CONNECTSTATE_CLASS.WTSInit => SessionState.Init,
            _ => SessionState.Down
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _isMonitoring = false;
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
