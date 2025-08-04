# Install WatchdogService as Windows Service
# Run this script as Administrator

param(
    [string]$ServicePath = "E:\cs\WinServicesRAG\publish\WatchdogService\WatchdogService.exe",
    [string]$ServiceName = "WatchdogService",
    [string]$DisplayName = "Windows System Monitoring Watchdog Service", 
    [string]$Description = "Monitors Windows system components for enhanced system reliability and performance"
)

Write-Host "=== Installing Windows System Monitoring Watchdog Service ===" -ForegroundColor Green
Write-Host "Service Path: $ServicePath" -ForegroundColor Yellow
Write-Host "Service Name: $ServiceName" -ForegroundColor Yellow

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service already exists. Stopping and removing..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Always build both services to ensure latest version
Write-Host "Building services..." -ForegroundColor Yellow

# Build and publish WatchdogService
Write-Host "Building WatchdogService..." -ForegroundColor Cyan
cd src\WatchdogService
dotnet publish -c Release --output ..\..\publish\WatchdogService

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build WatchdogService"
    exit 1
}

# Build and publish ScreenshotCapture (SystemMonitor.exe)
Write-Host "Building SystemMonitor..." -ForegroundColor Cyan
cd ..\ScreenshotCapture
dotnet publish -c Release --output ..\..\publish\ScreenshotService

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build SystemMonitor"
    exit 1
}

# Return to script directory
cd ..\..

Write-Host "✅ Build completed successfully" -ForegroundColor Green

# Verify executable exists after build
if (-not (Test-Path $ServicePath)) {
    Write-Error "Service executable not found after build: $ServicePath"
    exit 1
}

# Create the service
Write-Host "Creating Windows Service..." -ForegroundColor Green
$result = sc.exe create $ServiceName binPath= "`"$ServicePath`"" start= auto obj= "LocalSystem" DisplayName= "`"$DisplayName`""

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Service created successfully" -ForegroundColor Green
    
    # Set service description
    sc.exe description $ServiceName "`"$Description`""
    
    # Configure service recovery options
    Write-Host "Configuring service recovery options..." -ForegroundColor Green
    sc.exe failure $ServiceName reset= 60 actions= restart/5000/restart/10000/restart/20000
    
    # Start the service
    Write-Host "Starting service..." -ForegroundColor Green
    $startResult = sc.exe start $ServiceName
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Service started successfully" -ForegroundColor Green
        
        # Show service status
        Start-Sleep -Seconds 2
        Get-Service -Name $ServiceName | Format-Table Name, Status, StartType
        
        Write-Host "Service installation completed!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Yellow
        Write-Host "1. Check service logs: D:\Documents\Temporary\WinServicesRAG\logs\watchdog-service-*.log" -ForegroundColor White
        Write-Host "2. Monitor service status: Get-Service $ServiceName" -ForegroundColor White
        Write-Host "3. Login as user to test SystemMonitor spawning" -ForegroundColor White
        Write-Host "4. Check Event Log: Get-EventLog -LogName Application -Source WatchdogService" -ForegroundColor White
    }
    else {
        Write-Error "Failed to start service. Check logs for details."
        sc.exe query $ServiceName
    }
}
else {
    Write-Error "Failed to create service"
    Write-Host "Error details:" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

Write-Host ""
Write-Host "Management commands:" -ForegroundColor Cyan
Write-Host "  Start:   Start-Service $ServiceName" -ForegroundColor White
Write-Host "  Stop:    Stop-Service $ServiceName" -ForegroundColor White
Write-Host "  Status:  Get-Service $ServiceName" -ForegroundColor White
Write-Host "  Logs:    Get-Content 'D:\Documents\Temporary\WinServicesRAG\logs\watchdog-service-*.log' -Tail 20" -ForegroundColor White
Write-Host "  Uninstall: .\uninstall-watchdog-service.ps1" -ForegroundColor White
