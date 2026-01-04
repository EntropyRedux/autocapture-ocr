# AutoCapture OCR v2.0 - Quick Launch Script

Write-Host "Starting AutoCapture OCR v2.0..." -ForegroundColor Cyan

$dotnetPath = "C:\Users\mbula\Projects\Dependencies\dotnet\dotnet.exe"
$projectPath = "App\App.csproj"

if (-not (Test-Path $dotnetPath)) {
    Write-Host "Error: .NET executable not found at $dotnetPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $projectPath)) {
    Write-Host "Error: Project file not found at $projectPath" -ForegroundColor Red
    exit 1
}

Write-Host "Running application..." -ForegroundColor Green
& $dotnetPath run --project $projectPath
