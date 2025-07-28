using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinServicesRAG.Core.Configuration;
using WinServicesRAG.Core.Session;
using WinServicesRAG.Core.Process;

namespace WatchdogService;

/// <summary>
/// Enhanced Watchdog Service that manages ScreenshotCapture processes in user sessions
/// Implements Interactive Service pattern for desktop screenshot access
/// </summary>
public class EnhancedWatchdogService : BackgroundService
{
    private readonly ILogger<EnhancedWatchdogService> _logger;
    private readonly WatchdogServiceConfig _config;
    private readonly ISessionManager _sessionManager;
    private readonly IUserSessionProcessLauncher _processLauncher;
    
    // Track managed processes per session
    private readonly Dictionary<int, ProcessInfo> _userProcesses = new();
    private readonly Dictionary<int, int> _restartAttempts = new();
    
    private readonly object _lockObject = new();

    public EnhancedWatchdogService(
        ILogger<EnhancedWatchdogService> logger,
        IOptions<WatchdogServiceConfig> config,
        ISessionManager sessionManager,
        IUserSessionProcessLauncher processLauncher)
    {
        _logger = logger;
        _config = config.Value;
        _sessionManager = sessionManager;
        _processLauncher = processLauncher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Enhanced Watchdog Service starting up...");
        
        // Validate configuration
        var validationErrors = _config.Validate();
        if (validationErrors.Any())
        {
            _logger.LogError("Configuration validation failed:\n{Errors}", string.Join("\n", validationErrors));
            throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }

        // Log configuration summary
        _logger.LogInformation("Configuration:\n{ConfigSummary}", _config.GetConfigurationSummary());

        try
        {
            // Subscribe to session change events
            _sessionManager.SessionChanged += OnSessionChanged;
            
            // Start session monitoring
            await _sessionManager.StartMonitoringAsync();
            _logger.LogInformation("Session monitoring started");

            // Initial scan for active sessions
            await ScanAndManageActiveSessionsAsync();

            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorAndMaintainProcessesAsync();
                    await Task.Delay(TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in main monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Watchdog Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Watchdog Service");
            throw;
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private async void OnSessionChanged(object? sender, SessionEventArgs e)
    {
        try
        {
            _logger.LogInformation("Session change detected: {ChangeType} for Session {SessionId} ({UserName})",
                e.ChangeType, e.SessionInfo.SessionId, e.SessionInfo.UserName);

            switch (e.ChangeType)
            {
                case SessionChangeType.SessionLogon:
                case SessionChangeType.SessionRemoteConnect:
                    await HandleSessionLogonAsync(e.SessionInfo);
                    break;

                case SessionChangeType.SessionLogoff:
                case SessionChangeType.SessionRemoteDisconnect:
                    await HandleSessionLogoffAsync(e.SessionInfo);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session change event");
        }
    }

    private async Task ScanAndManageActiveSessionsAsync()
    {
        try
        {
            var activeSessions = await _sessionManager.GetActiveUserSessionsAsync();
            _logger.LogInformation("Found {Count} active user sessions", activeSessions.Count);

            foreach (var session in activeSessions)
            {
                _logger.LogInformation("Active session: {SessionId} - {UserName}@{Domain} ({State})",
                    session.SessionId, session.UserName, session.Domain, session.State);

                if (ShouldManageSession(session))
                {
                    await EnsureScreenshotProcessAsync(session.SessionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning active sessions");
        }
    }

    private async Task MonitorAndMaintainProcessesAsync()
    {
        lock (_lockObject)
        {
            var sessionsToCheck = _userProcesses.Keys.ToList();
            
            foreach (var sessionId in sessionsToCheck)
            {
                var processInfo = _userProcesses[sessionId];
                
                if (!_processLauncher.IsProcessRunning(processInfo.ProcessId))
                {
                    _logger.LogWarning("ScreenshotCapture process {ProcessId} in session {SessionId} is no longer running",
                        processInfo.ProcessId, sessionId);
                    
                    _userProcesses.Remove(sessionId);
                    
                    // Schedule restart if session is still active
                    _ = Task.Run(async () => await HandleProcessCrashAsync(sessionId));
                }
            }
        }
    }

    private async Task HandleSessionLogonAsync(SessionInfo sessionInfo)
    {
        if (!ShouldManageSession(sessionInfo))
        {
            _logger.LogDebug("Skipping session {SessionId} - not eligible for management", sessionInfo.SessionId);
            return;
        }

        _logger.LogInformation("Starting ScreenshotCapture for new session {SessionId}", sessionInfo.SessionId);
        await EnsureScreenshotProcessAsync(sessionInfo.SessionId);
    }

    private async Task HandleSessionLogoffAsync(SessionInfo sessionInfo)
    {
        _logger.LogInformation("Cleaning up ScreenshotCapture for ending session {SessionId}", sessionInfo.SessionId);
        
        lock (_lockObject)
        {
            if (_userProcesses.TryGetValue(sessionInfo.SessionId, out var processInfo))
            {
                _userProcesses.Remove(sessionInfo.SessionId);
                _restartAttempts.Remove(sessionInfo.SessionId);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _processLauncher.TerminateProcessAsync(processInfo.ProcessId);
                        _logger.LogInformation("Terminated ScreenshotCapture process {ProcessId} for session {SessionId}",
                            processInfo.ProcessId, sessionInfo.SessionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to terminate process {ProcessId}", processInfo.ProcessId);
                    }
                });
            }
        }
    }

    private async Task HandleProcessCrashAsync(int sessionId)
    {
        try
        {
            // Delay before restart
            if (_config.ProcessRestartDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.ProcessRestartDelaySeconds));
            }

            // Check if session is still active
            if (!_sessionManager.IsSessionActive(sessionId))
            {
                _logger.LogInformation("Session {SessionId} is no longer active, skipping process restart", sessionId);
                return;
            }

            // Check restart attempts
            lock (_lockObject)
            {
                var attempts = _restartAttempts.GetValueOrDefault(sessionId, 0);
                if (attempts >= _config.MaxRestartAttempts)
                {
                    _logger.LogError("Max restart attempts ({MaxAttempts}) reached for session {SessionId}, giving up",
                        _config.MaxRestartAttempts, sessionId);
                    _restartAttempts.Remove(sessionId);
                    return;
                }
                _restartAttempts[sessionId] = attempts + 1;
            }

            _logger.LogInformation("Attempting to restart ScreenshotCapture for session {SessionId} (attempt {Attempt}/{MaxAttempts})",
                sessionId, _restartAttempts[sessionId], _config.MaxRestartAttempts);

            await EnsureScreenshotProcessAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling process crash for session {SessionId}", sessionId);
        }
    }

    private async Task EnsureScreenshotProcessAsync(int sessionId)
    {
        try
        {
            var executablePath = _config.GetResolvedScreenshotExecutablePath();
            var arguments = $"user-session --session-id {sessionId}";

            _logger.LogInformation("Launching ScreenshotCapture in session {SessionId}: {Path} {Args}",
                sessionId, executablePath, arguments);

            var processInfo = await _processLauncher.LaunchInUserSessionAsync(sessionId, executablePath, arguments);

            lock (_lockObject)
            {
                _userProcesses[sessionId] = processInfo;
                _restartAttempts[sessionId] = 0; // Reset attempts on successful start
            }

            _logger.LogInformation("Successfully launched ScreenshotCapture process {ProcessId} in session {SessionId}",
                processInfo.ProcessId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch ScreenshotCapture in session {SessionId}", sessionId);
        }
    }

    private bool ShouldManageSession(SessionInfo sessionInfo)
    {
        // Only manage active, connected sessions with actual users
        return sessionInfo.State == SessionState.Active &&
               !string.IsNullOrEmpty(sessionInfo.UserName) &&
               sessionInfo.SessionId > 0; // Skip Session 0 (system session)
    }

    private async Task CleanupAsync()
    {
        try
        {
            _logger.LogInformation("Cleaning up Watchdog Service...");

            // Stop session monitoring
            _sessionManager.SessionChanged -= OnSessionChanged;
            await _sessionManager.StopMonitoringAsync();

            // Terminate all managed processes
            var processesToTerminate = new List<ProcessInfo>();
            lock (_lockObject)
            {
                processesToTerminate.AddRange(_userProcesses.Values);
                _userProcesses.Clear();
                _restartAttempts.Clear();
            }

            foreach (var processInfo in processesToTerminate)
            {
                try
                {
                    await _processLauncher.TerminateProcessAsync(processInfo.ProcessId);
                    _logger.LogInformation("Terminated ScreenshotCapture process {ProcessId}", processInfo.ProcessId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to terminate process {ProcessId} during cleanup", processInfo.ProcessId);
                }
            }

            _logger.LogInformation("Watchdog Service cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }
}
