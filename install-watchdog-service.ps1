# Install WatchdogService as Windows Service
# Run this script as Administrator

param(
    [string]$ServicePath = "E:\cs\WinServicesRAG\publish\WatchdogService\WatchdogService.exe",
    [string]$ServiceName = "WatchdogService",
    [string]$DisplayName = "WinServicesRAG Watchdog Service",
    [string]$Description = "Monitors the device for a better user experience"
)

Write-Host "=== Installing WatchdogService ===" -ForegroundColor Green
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

# Check if executable exists
if (-not (Test-Path $ServicePath)) {
    Write-Error "Service executable not found: $ServicePath"
    Write-Host "Please build and publish the WatchdogService first:" -ForegroundColor Red
    Write-Host "  cd src\WatchdogService" -ForegroundColor Red
    Write-Host "  dotnet publish -c Release --output ..\..\publish\WatchdogService" -ForegroundColor Red
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
        Write-Host "3. Login as user to test ScreenshotCapture spawning" -ForegroundColor White
        Write-Host "4. Check Event Log: Get-EventLog -LogName Application -Source WatchdogService" -ForegroundColor White
    } else {
        Write-Error "Failed to start service. Check logs for details."
        sc.exe query $ServiceName
    }
} else {
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
