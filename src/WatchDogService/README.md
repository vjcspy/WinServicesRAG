# WatchdogService - Interactive Service Implementation

## Overview

WatchdogService implements the **Interactive Service** pattern to solve the screenshot capture limitation in Windows Service (Session 0) environment. This service automatically manages ScreenshotCapture processes in user sessions where desktop access is available.

## Architecture

```
Session 0 (System)               Session 1+ (User Desktop)
┌─────────────────────┐         ┌─────────────────────────┐
│   WatchdogService   │◄────────┤  ScreenshotCapture.exe  │
│   (Windows Service) │         │  (Interactive Process)  │
│                     │         │                         │
│ • Session Monitor   │         │ • Desktop Access        │
│ • Process Manager   │         │ • Screenshot Capture    │
│ • Health Check      │         │ • API Communication     │
│ • Smart Path        │         │ • Job Processing        │
└─────────────────────┘         └─────────────────────────┘
```

## Smart Path Resolution

The `WatchdogServiceConfig` class automatically detects the ScreenshotCapture executable location using a smart search algorithm:

### Search Order

1. **Same directory** as WatchdogService (production deployment)
2. **ScreenshotService subdirectory** (publish output structure)
3. **Sibling directory** in development environment
4. **Development bin folders** (Debug/Release)
5. **Published output** directory structure
6. **Absolute path fallback** for known development environment

### Configuration

```json
{
  "WatchdogService": {
    "ScreenshotExecutablePath": null,  // Auto-detect if null/empty
    "IpcPipeName": "WinServicesRAG_IPC",
    "HeartbeatIntervalSeconds": 30,
    "ProcessRestartDelaySeconds": 5,
    "EnableMultiSession": true,
    "MaxRestartAttempts": 3,
    "ProcessStartupTimeoutSeconds": 30
  }
}
```

### Manual Override

To override auto-detection, specify explicit path:

```json
{
  "WatchdogService": {
    "ScreenshotExecutablePath": "C:\\MyCustomPath\\ScreenshotCapture.exe"
  }
}
```

## Service Responsibilities

### Session Management
- **Monitor Windows sessions** using WTS APIs
- **Detect user logon/logoff** events
- **Handle RDP connections** and disconnections
- **Support multiple concurrent sessions**

### Process Lifecycle
- **Auto-spawn** ScreenshotCapture when user logs in
- **Auto-terminate** when user logs out
- **Crash recovery** with configurable retry attempts
- **Health monitoring** via heartbeat mechanism

### Deployment Scenarios

#### Development Environment
```
e:\cs\WinServicesRAG\
├── src\WatchdogService\bin\Debug\net9.0\
│   └── WatchdogService.exe
└── src\ScreenshotCapture\bin\Debug\net9.0\
    └── ScreenshotCapture.exe    ← Auto-detected
```

#### Production Deployment
```
C:\Program Files\WinServicesRAG\
├── WatchdogService.exe
└── ScreenshotCapture.exe        ← Auto-detected
```

#### Published Structure
```
e:\cs\WinServicesRAG\publish\
├── WatchdogService\
│   └── WatchdogService.exe
└── ScreenshotService\
    └── ScreenshotCapture.exe    ← Auto-detected
```

## Features

### ✅ Smart Path Resolution
- Automatically finds ScreenshotCapture.exe in common locations
- Supports development, testing, and production environments
- Validates executable existence during startup
- Provides detailed configuration logging

### ✅ Session-Aware Process Management
- Monitors Windows user sessions (WTS APIs)
- Spawns processes only in active user sessions
- Supports console sessions and RDP sessions
- Handles session transitions gracefully

### ✅ Robust Error Handling
- Configurable restart attempts with exponential backoff
- Graceful degradation when executable not found
- Comprehensive logging for troubleshooting
- Process crash detection and recovery

### ✅ Configuration Validation
- Validates all configuration parameters at startup
- Provides meaningful error messages
- Configuration summary logging
- Runtime path resolution verification

## Usage

### Build 
```powershell
 cd src\WatchdogService
 dotnet publish -c Release --output ..\..\publish\WatchdogService
```

### Windows Service Installation
```powershell
# Use ps1
.\install-watchdog-service.ps1

# Install WatchdogService
sc.exe create WatchdogService binPath="C:\Path\To\WatchdogService.exe" start=auto
net start WatchdogService

# Check logs
Get-EventLog -LogName Application -Source WatchdogService | Format-List
```

### Troubleshooting

#### Executable Not Found
```
[ERROR] ScreenshotCapture executable not found at: C:\Path\To\ScreenshotCapture.exe
```
**Solution**: Build ScreenshotCapture project or specify correct path in configuration

#### Session Detection Issues
```
[WARNING] No active user sessions found
```
**Solution**: Ensure user is logged in and session monitoring is working

#### Process Launch Failures
```
[ERROR] Failed to launch ScreenshotCapture in session 1: Access denied
```
**Solution**: Ensure WatchdogService runs with sufficient privileges (LocalSystem)

## Logging

All operations are logged with structured logging:
- **Configuration validation** and path resolution
- **Session change events** (logon/logoff)
- **Process lifecycle** (start/stop/crash)
- **Health monitoring** and restart attempts
- **Error conditions** with detailed context

## Dependencies

- **WinServicesRAG.Core.Configuration**: Configuration management
- **WinServicesRAG.Core.Session**: Session monitoring
- **WinServicesRAG.Core.Process**: Process management
- **Microsoft.Extensions.Hosting**: Background service framework
- **Microsoft.Extensions.Logging**: Structured logging

## Future Enhancements

### IPC Communication
- Named pipes for real-time communication
- Job distribution and result collection
- Health monitoring and status reporting

### Advanced Features
- Multi-user session load balancing
- Process priority management
- Resource usage monitoring
- Security token impersonation

---

**Note**: This implementation provides the foundation for Interactive Service pattern. ScreenshotCapture processes running in user sessions will have full desktop access, enabling all screenshot providers (DirectX, Windows Graphics Capture API, WinAPI) to function correctly.
