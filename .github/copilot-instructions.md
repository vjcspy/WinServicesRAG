# Copilot Instructions for WinServicesRAG

## Project Overview

This is a **Windows monitoring system** built with C# (.NET 9) featuring a dual-service architecture for screenshot capture and system monitoring. The project implements a tamper-resistant design with **session-aware process management** and **native C++ integration** for high-performance screen capture.

## Architecture (3-Component System)

1. **ScreenshotCapture** - Unified service/console app with dual operation modes
2. **WatchdogService** - Session 0 service that monitors and manages ScreenshotCapture 
3. **WinServicesRAG.Core** - Shared library containing common functionality

### Key Design Pattern: Dual-Mode Architecture

ScreenshotCapture operates in two distinct modes:
- **Service Mode**: `dotnet run -- service --hide-console` (production)
- **CLI Mode**: `dotnet run -- cli --status` (testing/debugging)
- **User Session Mode**: `dotnet run -- user-session --session-id 1` (managed by WatchdogService)

## Critical Developer Workflows

### Building & Testing
```bash
# Build everything
dotnet build WindowsServicesRAG.sln --configuration Release

# Test screenshot providers (always run this first)
cd src/ScreenshotCapture
dotnet run -- cli --status --verbose

# Test individual screenshot provider
dotnet run -- cli --provider "WGC" --output "D:\temp\test.png"

# Run as service (production mode)
dotnet run -- service --hide-console --work-dir "C:\ProgramData\WinServicesRAG"
```

### Service Installation (PowerShell as Admin)
```powershell
.\install-service.ps1          # Install ScreenshotCapture service
.\install-watchdog-service.ps1 # Install WatchdogService
```

## Code Patterns & Conventions

### Reactive Extensions (Rx.NET) Pattern
The core processing uses **Observable streams** for job polling and processing:

```csharp
// In JobProcessingEngineBase.cs - key pattern for all background processing
Observable.Interval(TimeSpan.FromSeconds(pollInterval))
    .SelectMany(_ => GetJobsObservable())
    .Where(job => job.Status == "PENDING" || job.Status == "TAKE_SCREEN_SHOT")
    .SelectMany(job => ProcessJobObservable(job))
    .Subscribe(result => HandleResult(result));
```

### Dependency Injection Setup
Use these extension methods in `Program.cs`:

```csharp
// Core services registration
services.AddWinServicesRAGCore(configuration);
services.AddScreenshotServices(configuration);
services.ValidateConfiguration(); // Always validate config
```

### Command Pattern (System.CommandLine)
ScreenshotCapture uses command-line parsing for mode selection. See `Commands/CommandSetup.cs` for the pattern.

### Session Management Pattern
WatchdogService implements **session-aware process management**:
- Runs in Session 0 (system service)
- Manages ScreenshotCapture processes in user sessions
- Handles session switches (logoff/logon, RDP)

## Native C++ Integration

### Screenshot Providers (Fallback Chain)
1. **Windows Graphics Capture API (WGC)** - Primary (direct memory capture)
2. **DirectX Desktop Duplication** - High performance
3. **GDI** - Compatibility fallback
4. **WinAPI (BitBlt)** - Last resort

Key files:
- `WinServicesRAG.Core/Screenshot/` - C# interfaces
- `WinServicesRAG.Core/native/` - Native DLL integration
- `cpp/ScreenCaptureApp/` - C++ implementation

### Memory Management Pattern
Native screenshots use **zero-copy architecture**:
```csharp
// Pattern: Direct memory → Marshal.Copy → byte array
// No temporary files, direct PNG encoding to memory
```

## Configuration Patterns

### appsettings.json Structure
```json
{
  "ApiClient": {
    "BaseUrl": "https://api.server.com",
    "TimeoutSeconds": 30,
    "RetryAttempts": 3
  },
  "JobProcessing": {
    "PollIntervalSeconds": 5,
    "WorkingDirectory": "C:\\ProgramData\\WinServicesRAG"
  }
}
```

### Logging Convention
- **Serilog** for structured logging
- **File rolling**: Daily rotation, 7-day retention
- **Debug output**: DebugView compatibility
- **Event Log**: Windows Service events

## Critical Integration Points

### API Communication
- **HttpClientFactory** for connection pooling
- **Retry logic** with exponential backoff (3 attempts)
- **Multipart form uploads** for screenshot data
- **Snake_case JSON** serialization

### Process Management
- **WatchdogService** monitors via `Process.GetProcessesByName()`
- **Session detection** using Windows Terminal Services API
- **Graceful shutdown** on session logoff
- **Auto-restart** with configurable retry attempts

## Debugging & Troubleshooting

### Log Locations
- Development: `logs/screenshot-capture-.log`
- Production: `C:\ProgramData\WinServicesRAG\logs\`
- Event Log: Windows Event Viewer → Application

### Common Debug Commands
```bash
# Check all screenshot providers
dotnet run -- cli --status --verbose

# Test specific provider
dotnet run -- cli --provider "DirectX" --verbose

# Check WatchdogService status (PowerShell as Admin)
Get-Service "WatchdogService" | Format-List
```

### Deployment Structure
- Published binaries: `publish/ScreenshotService/`
- Native DLL: `ScreenCaptureDLL.dll` (must be in same directory)
- Configuration: `appsettings.json` (deployment-specific)

## Working Protocol

### 1. Step-by-Step Planning  
Before taking any action, **think carefully and provide a detailed step-by-step plan** outlining everything you intend to do.  
- Present the *entire plan first*, formatted as a clear, ordered list.  
- Wait for my review, comments, or requested updates.
- **Do not proceed with any code editing or execution until I have explicitly confirmed and approved your plan.**

> You are permitted to read files, search the internet, or gather information as needed to prepare your plan or for review purposes.  
> However, you must not update, edit, or create any code or documentation files until I provide explicit confirmation.

---

### 2. Documentation Template  
After the plan is finalized and approved:  
- **Follow the template in `devdocs/TEMPLATE.md`.**
- Create or update documentation in the `devdocs` folder as per the template.
- This ensures a standardized approach to planning, implementation, and documentation.

---

### 3. Changelog Update  
**Only** when I state that the ticket is completed and explicitly ask you to update the changelog, you must:  
- Update the `CHANGELOG.md` file in the `devdocs` directory with a brief description of your changes.
- Use the following format for the CHANGELOG entry:
    ```
    [YYYYMMDDHHMM] Brief description of change ([details](path/to/ticket-file.md))
    ```

---

**Do not perform any actions outside this protocol. Await my instructions at each step.**

## Project-Specific Anti-Patterns

- **Don't** use synchronous HTTP calls (use async/await throughout)
- **Don't** create temporary screenshot files (use memory streams)
- **Don't** ignore session context (services behave differently in Session 0 vs user session)
- **Don't** modify process management without understanding session implications
