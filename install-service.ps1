# Script to install ScreenshotCapture Windows Service
# Run with Administrator privileges

# Path to exe file
$ServicePath = "E:\cs\WinServicesRAG\publish\ScreenshotService\ScreenshotCapture.exe"
$ServiceName = "ScreenshotCaptureService"
$DisplayName = "Screenshot Capture Service"
$Description = "Service for automatic screenshot capture"

Write-Host "=== Installing Windows Service ===" -ForegroundColor Green

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "Service already exists. Stopping and uninstalling..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Create new service
Write-Host "Installing service..." -ForegroundColor Cyan
sc.exe create $ServiceName binPath= $ServicePath DisplayName= $DisplayName start= auto

# Set description
sc.exe description $ServiceName $Description

# Setup recovery options (auto restart on crash)
Write-Host "Setting up recovery options..." -ForegroundColor Cyan
sc.exe failure $ServiceName reset= 0 actions= restart/5000/restart/5000/restart/5000

# Start service
Write-Host "Starting service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

# Check status
Write-Host "=== Service Status ===" -ForegroundColor Green
Get-Service -Name $ServiceName | Format-Table -AutoSize

Write-Host "=== Installation Complete! ===" -ForegroundColor Green
Write-Host "You can manage the service via:" -ForegroundColor White
Write-Host "1. Services.msc (GUI interface)" -ForegroundColor Yellow
Write-Host "2. PowerShell: Get-Service ScreenshotCaptureService" -ForegroundColor Yellow
Write-Host "3. Event Viewer for logs" -ForegroundColor Yellow
