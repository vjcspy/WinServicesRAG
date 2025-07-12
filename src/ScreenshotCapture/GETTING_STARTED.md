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
=== ScreenshotCapture Independent Service ===
Screenshot Provider Status:
===========================
DirectX Desktop Duplication API (Vortice.Windows): ✗ Not Available (Session 0)
Windows Graphics Capture API (Disabled): ✗ Not Available  
WinAPI (BitBlt + PrintWindow): ✓ Available

Taking screenshot...
Screenshot saved successfully: screenshot_20250712_155030.png
File size: 219,846 bytes
```

#### 3. Service Mode (Background Service)

```bash
# Run in service mode (background service simulation)
dotnet run -- service --verbose

# Run with custom work directory
dotnet run -- service --work-dir "C:\Temp\WinServicesRAG" --poll-interval 10
```

### 🔧 Troubleshooting

#### DirectX Provider Not Available
- **Session 0**: DirectX Desktop Duplication không hoạt động trong system session
- **User Session**: Chạy trong user session để enable DirectX provider
- **GPU Drivers**: Đảm bảo drivers cập nhật

#### Permission Issues
- Chạy terminal as Administrator nếu gặp access denied
- Check Windows Defender/Antivirus blocking

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
- [ ] 🚧 Service mode
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
# Quick test sequence
cd src\ScreenshotCapture
dotnet run -- cli --status           # Check providers
dotnet run -- cli --verbose          # Take screenshot with logging
dir *.png                           # Verify output file created
```

Enjoy testing! 🚀
