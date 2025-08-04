# WinServicesRAG - Quick Start Guide

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 9 SDK
- Visual Studio 2022 or VS Code
- Admin privileges (for some testing scenarios)

### Build & Run

#### 1. Build All Projects
```bash
# From solution root
dotnet build
```

#### 2. Test ScreenshotCapture (Recommended First Step)

ScreenshotCapture có CLI mode để test trực tiếp mà không cần setup services.

##### Basic Commands:

```bash
# Navigate to ScreenshotCapture project
cd src\ScreenshotCapture

# Check provider status
dotnet run -- cli --status

# Take a screenshot with default settings
dotnet run -- cli

# Take screenshot với custom output
dotnet run -- cli --output "my_screenshot.png"

# Verbose logging để debug
dotnet run -- cli --verbose

# Force specific provider
dotnet run -- cli --provider "WinAPI" --verbose

# Show help
dotnet run -- cli --help
```

##### Expected Output:
```
=== Windows System Monitoring Service ===
System Monitoring Provider Status:
===================================
DirectX Desktop Duplication API (Vortice.Windows): ✗ Not Available (Session 0)
Windows Graphics Capture API (Disabled): ✗ Not Available  
WinAPI (BitBlt + PrintWindow): ✓ Available

Taking screenshot...
Screenshot saved successfully: screenshot_20250712_155030.png
File size: 219,846 bytes
```

#### 3. Service Mode (Background Service Simulation)

```bash
# Run in service mode for testing (background service simulation)
dotnet run -- service --verbose

# Run with custom work directory
dotnet run -- service --work-dir "C:\Temp\WinServicesRAG" --poll-interval 10
```

#### 4. Windows Service Installation (Production Mode)

For production deployment, install as actual Windows Service:

##### Build and Publish
```bash
# From solution root
dotnet build --configuration Release

# Publish ScreenshotCapture
cd src\ScreenshotCapture
dotnet publish -c Release --self-contained false --output ..\..\publish\ScreenshotService
```

##### Install Windows Service
```powershell
# Open PowerShell as Administrator
cd E:\cs\WinServicesRAG

# Install service
.\install-service.ps1
```

##### Manage Windows Service
```powershell
# Check service status
Get-Service ScreenshotCaptureService

# Start/Stop/Restart service
Start-Service ScreenshotCaptureService
Stop-Service ScreenshotCaptureService
Restart-Service ScreenshotCaptureService

# View logs
Get-Content "D:\Documents\Temporary\WinServicesRAG\logs\screenshot-capture-*.log" -Tail 20

# Uninstall service
.\uninstall-service.ps1
```

##### Service Details
- **Service Name:** `ScreenshotCaptureService`
- **Display Name:** `Screenshot Capture Service`
- **Working Directory:** `C:\ProgramData\WinServicesRAG`
- **Auto-restart:** Enabled (restart on crash)
- **Logs:** `D:\Documents\Temporary\WinServicesRAG\logs\`

### 🔧 Troubleshooting

#### DirectX Provider Not Available
- **Session 0**: DirectX Desktop Duplication không hoạt động trong system session
- **User Session**: Chạy trong user session để enable DirectX provider
- **GPU Drivers**: Đảm bảo drivers cập nhật

#### Permission Issues
- Chạy terminal as Administrator nếi gặp access denied
- Check Windows Defender/Antivirus blocking
- For Windows Service: PowerShell phải run as Administrator

#### Windows Service Issues
- **Service won't start**: Check logs in `D:\Documents\Temporary\WinServicesRAG\logs\`
- **Permission denied**: Run PowerShell as Administrator
- **Port conflicts**: Check if other services using same ports
- **Dependencies missing**: Ensure .NET 9 Runtime installed on target machine

#### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### 📁 Project Structure

```
src/
├── ScreenshotCapture/          # ✅ IMPLEMENTED - CLI và Service mode
│   ├── Program.cs              # Entry point với System.CommandLine
│   └── ScreenshotCapture.csproj
├── WinServicesRAG.Core/        # ✅ IMPLEMENTED - Screenshot providers
│   └── Screenshot/
│       ├── IScreenshotProvider.cs
│       ├── DirectXScreenshotProvider.cs    # Vortice.Windows
│       ├── WinApiScreenshotProvider.cs     # BitBlt fallback
│       ├── WindowsGraphicsCaptureProvider.cs # Placeholder
│       └── ScreenshotManager.cs            # Provider manager
├── WorkerService/              # 🚧 TODO - Business logic service
├── WatchdogService/           # 🚧 TODO - Process monitoring
└── WindowsServicesRAG.sln
```

### 🧪 Testing Checklist

- [ ] ✅ CLI mode hoạt động
- [ ] ✅ Provider status check
- [ ] ✅ Screenshot capture (WinAPI)
- [ ] ✅ File output (.png format)
- [ ] ⚠️ DirectX provider (cần user session)
- [ ] ✅ Service mode (development testing)
- [ ] ✅ Windows Service installation
- [ ] ✅ Windows Service auto-restart
- [ ] 🚧 API integration
- [ ] 🚧 Watchdog monitoring

### 📊 Current Implementation Status

| Component | Status | Description |
|-----------|--------|-------------|
| **Core Screenshot Module** | ✅ Complete | 3 providers implemented, fallback strategy |
| **ScreenshotCapture CLI** | ✅ Complete | Full CLI interface, testing ready |
| **DirectX (Vortice.Windows)** | ✅ Working | Modern DirectX wrapper, session-dependent |
| **WinAPI (BitBlt)** | ✅ Working | Reliable fallback, cross-session compatible |
| **Windows Graphics Capture** | ⚠️ Disabled | .NET compatibility issues |
| **Service Mode (Development)** | ✅ Complete | Background service simulation |
| **Windows Service** | ✅ Complete | Production-ready Windows Service |
| **Service Auto-restart** | ✅ Complete | Recovery options configured |
| **WorkerService** | 🚧 TODO | Business logic service |
| **WatchdogService** | 🚧 TODO | Process monitoring service |
| **API Integration** | 🚧 TODO | HTTP client implementation |


### 🐛 Common Issues

**Problem**: `DirectX Desktop Duplication API: ✗ Not Available`
**Solution**: Chạy trong user session (không phải system session)

**Problem**: `Access denied` khi save file
**Solution**: Run as Administrator hoặc đổi output path

**Problem**: Build errors với Vortice.Windows
**Solution**: Ensure .NET 9 SDK installed và nuget packages restored

---

## 🎯 Quick Test Commands

```bash
# Development Testing Sequence
cd src\ScreenshotCapture
dotnet run -- cli --status           # Check providers
dotnet run -- cli --verbose          # Take screenshot with logging
dir *.png                           # Verify output file created

# Service Mode Testing
dotnet run -- service --verbose      # Test service mode locally

# Production Windows Service Testing
.\install-service.ps1                # Install as Windows Service (PowerShell as Admin)
Get-Service ScreenshotCaptureService # Check service status
Get-Content "D:\Documents\Temporary\WinServicesRAG\logs\screenshot-capture-*.log" -Tail 10  # Check logs
```

### 📂 Service File Locations

**Development (dotnet run):**
- Default work dir: `%APPDATA%\WinServicesRAG` or custom via `--work-dir`

**Production Windows Service:**
- Working Directory: `C:\ProgramData\WinServicesRAG\`
- Jobs: `C:\ProgramData\WinServicesRAG\jobs\`
- Screenshots: `C:\ProgramData\WinServicesRAG\screenshots\`
- Results: `C:\ProgramData\WinServicesRAG\results\`
- Logs: `D:\Documents\Temporary\WinServicesRAG\logs\`

Enjoy testing! 🚀
