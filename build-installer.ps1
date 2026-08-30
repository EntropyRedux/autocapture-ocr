# AutoCapture OCR - Installer Build Script
# Usage: .\build-installer.ps1 [-DotNetPath <path>] [-IsccPath <path>] [-Configuration <Release|Debug>]

param(
    [string]$DotNetPath    = "C:\Program Files\dotnet\dotnet.exe",
    [string]$IsccPath      = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root    = $PSScriptRoot
$AppProj = Join-Path $Root "App\App.csproj"
$IssFile = Join-Path $Root "Installer\AutoCaptureOCR.iss"
$OutDir  = Join-Path $Root "publish\win-x64"

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host ">> $msg" -ForegroundColor Cyan
}
function Write-Ok([string]$msg) {
    Write-Host "   OK: $msg" -ForegroundColor Green
}
function Write-Fail([string]$msg) {
    Write-Host "   FAIL: $msg" -ForegroundColor Red
    exit 1
}

# -- 1. Validate prerequisites
Write-Step "Checking prerequisites"

if (-not (Test-Path $DotNetPath)) {
    Write-Fail ".NET executable not found at: $DotNetPath`n   Pass -DotNetPath to override."
}
Write-Ok ".NET found: $DotNetPath"

if (-not (Test-Path $IsccPath)) {
    Write-Fail "Inno Setup compiler not found at: $IsccPath`n   Install Inno Setup 6 from https://jrsoftware.org/isinfo.php`n   Or pass -IsccPath to override."
}
Write-Ok "Inno Setup found: $IsccPath"

# -- 2. Clean previous publish output
Write-Step "Cleaning publish directory"
if (Test-Path $OutDir) {
    Remove-Item $OutDir -Recurse -Force
    Write-Ok "Removed: $OutDir"
} else {
    Write-Ok "Nothing to clean"
}

# -- 3. Publish (self-contained, win-x64)
Write-Step "Publishing App (self-contained, win-x64, $Configuration)"

& $DotNetPath publish $AppProj `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:TrimSelf=false `
    --output $OutDir

if ($LASTEXITCODE -ne 0) {
    Write-Fail "dotnet publish failed (exit code $LASTEXITCODE)"
}
Write-Ok "Published to: $OutDir"

$ExePath = Join-Path $OutDir "AutoCaptureOCR.exe"
if (-not (Test-Path $ExePath)) {
    Write-Fail "Expected executable not found: $ExePath"
}
Write-Ok "Executable verified: AutoCaptureOCR.exe"

# -- 4. Compile Inno Setup installer
Write-Step "Compiling installer with Inno Setup"

& $IsccPath $IssFile

if ($LASTEXITCODE -ne 0) {
    Write-Fail "Inno Setup compilation failed (exit code $LASTEXITCODE)"
}

# -- 5. Report output
Write-Step "Done"
$DistDir  = Join-Path $Root "dist"
$Installer = Get-ChildItem $DistDir -Filter "*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($Installer) {
    $SizeMB = [math]::Round($Installer.Length / 1MB, 1)
    Write-Host ""
    Write-Host "   Installer: $($Installer.FullName)" -ForegroundColor Yellow
    Write-Host "   Size:      ${SizeMB} MB" -ForegroundColor Yellow
} else {
    Write-Fail "Installer not found in dist\ - check Inno Setup output"
}

Write-Host ""
Write-Host "Build complete." -ForegroundColor Green
