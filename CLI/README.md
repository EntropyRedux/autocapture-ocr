# AutoCapture-OCR CLI

Command-line interface for AutoCapture-OCR v2.0 - Screen capture, OCR processing, and project management from the terminal.

## Installation

### Build from Source

```bash
cd c:\Users\mbula\Projects\Repo\AutoCapture-OCR\dev
dotnet build CLI/CLI.csproj
```

### Run CLI

```bash
dotnet run --project CLI/CLI.csproj -- [command] [options]
```

### Build Single-File Executable

For easy distribution as a standalone .exe:

```bash
dotnet publish CLI/CLI.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

Output: `CLI/bin/Release/net9.0-windows10.0.22621/win-x64/publish/autocapture.exe`

## Quick Start

```bash
# Get help
autocapture --help

# List all projects
autocapture project list

# Capture fullscreen with OCR
autocapture capture fullscreen --project "My Project" --ocr

# Capture specific region
autocapture capture region --x 100 --y 100 --width 800 --height 600 --ocr

# Run OCR on an image
autocapture ocr run screenshot.png --format json

# Batch OCR on a folder
autocapture ocr batch ./screenshots --export results.md --format markdown

# Export project data
autocapture export captures "My Project" --output data.json --format json
```

## Commands

### Capture Commands

#### Fullscreen Capture
```bash
autocapture capture fullscreen [options]
```

**Options:**
- `--project <name>` - Project name or ID (creates if doesn't exist)
- `--session <name>` - Session name or ID (creates if doesn't exist)
- `--output <path>` - Save directly to file (bypasses project)
- `--ocr` - Run OCR after capture (default: true)

**Examples:**
```bash
# Capture to project
autocapture capture fullscreen --project "Screenshots" --session "Today"

# Capture to file with OCR
autocapture capture fullscreen --output screenshot.png --ocr

# Capture without OCR
autocapture capture fullscreen --project "Test" --ocr false
```

#### Region Capture
```bash
autocapture capture region --x <x> --y <y> --width <w> --height <h> [options]
```

**Required:**
- `--x <int>` - X coordinate (pixels)
- `--y <int>` - Y coordinate (pixels)
- `--width <int>` - Width (pixels)
- `--height <int>` - Height (pixels)

**Options:**
- `--project <name>` - Project name or ID
- `--session <name>` - Session name or ID
- `--output <path>` - Save directly to file
- `--ocr` - Run OCR after capture (default: true)

**Examples:**
```bash
# Capture specific region
autocapture capture region --x 100 --y 100 --width 800 --height 600

# Capture region to file
autocapture capture region --x 0 --y 0 --width 1920 --height 1080 --output full.png

# Multi-monitor: External monitor positioned left (negative X)
autocapture capture region --x -1920 --y 0 --width 800 --height 600
```

**Note:** For multi-monitor setups, use screen coordinates. Monitors to the left of primary have negative X values.

### OCR Commands

#### Single Image OCR
```bash
autocapture ocr run <image-path> [options]
```

**Options:**
- `--format <format>` - Output format: `text` (default), `json`, `yaml`
- `--language <lang>` - OCR language (default: system language)

**Examples:**
```bash
# Extract text
autocapture ocr run screenshot.png

# Get JSON output
autocapture ocr run invoice.png --format json

# Get YAML output
autocapture ocr run receipt.png --format yaml
```

#### Batch OCR
```bash
autocapture ocr batch <folder-path> [options]
```

**Options:**
- `--pattern <pattern>` - File pattern (default: `*.png`)
- `--export <path>` - Export results to file
- `--format <format>` - Export format: `json`, `yaml`, `markdown`, `csv`, `text`
- `--recursive` - Search subfolders

**Examples:**
```bash
# Process all PNGs in folder
autocapture ocr batch ./screenshots

# Export to Markdown
autocapture ocr batch ./images --export report.md --format markdown

# Export to YAML with custom pattern
autocapture ocr batch ./docs --pattern "*.jpg" --export results.yaml --format yaml

# Recursive search
autocapture ocr batch ./archive --pattern "*.png" --recursive --export all.json
```

**Export Formats:**
- **JSON**: Structured data with all metadata
- **YAML**: Human-readable structured format
- **Markdown**: Formatted report with headers and code blocks
- **CSV**: Spreadsheet-compatible format
- **Text**: Plain text with separators

### Project Commands

#### List Projects
```bash
autocapture project list
```

Shows all projects with sessions, captures, and modification dates.

#### Create Project
```bash
autocapture project create <name> [options]
```

**Options:**
- `--description <text>` - Project description

**Examples:**
```bash
autocapture project create "Work Docs"
autocapture project create "Receipts 2026" --description "Tax receipts and invoices"
```

#### Project Info
```bash
autocapture project info <name-or-id>
```

Shows detailed project information including all sessions.

**Examples:**
```bash
autocapture project info "Work Docs"
autocapture project info abc123-guid-here
```

#### Delete Project
```bash
autocapture project delete <name-or-id> [options]
```

**Options:**
- `--force` - Skip confirmation prompt

**Examples:**
```bash
autocapture project delete "Old Project"
autocapture project delete "Test" --force
```

### Export Commands

#### Export Captures
```bash
autocapture export captures <project-name> --output <path> [options]
```

**Options:**
- `--output <path>` - Output file path (required)
- `--format <format>` - Export format: `json` (default), `csv`
- `--text-format <format>` - OCR text format for CSV: `continuous` (default), `lines`, `structured`, `json`

**Text Format Options (CSV only):**
- `continuous` - All text in one line (default, best for Excel/spreadsheets)
- `lines` - Each OCR line on a new line (preserves layout)
- `structured` - Each line prefixed with `[Line N]` (numbered for analysis)
- `json` - Full OCR data as JSON with line numbers and confidence (for programmatic use)

**Examples:**
```bash
# Export to JSON
autocapture export captures "My Project" --output data.json

# Export to CSV (default continuous format)
autocapture export captures "Screenshots" --output captures.csv --format csv

# Export to CSV with line breaks preserved
autocapture export captures "UI Docs" --output ui-text.csv --format csv --text-format lines

# Export to CSV with line numbers for analysis
autocapture export captures "Testing" --output test-results.csv --format csv --text-format structured

# Export to CSV with full JSON OCR data per row
autocapture export captures "Training Data" --output training.csv --format csv --text-format json
```

#### Export Analytics
```bash
autocapture export analytics <project-name> --output <path>
```

Exports analytics data including capture statistics, OCR confidence, and processing metrics.

**Examples:**
```bash
autocapture export analytics "Work Docs" --output analytics.json
```

### Configuration Commands

#### Show Config
```bash
autocapture config show
```

Displays current configuration in YAML format.

#### Config Path
```bash
autocapture config path
```

Shows the path to the configuration file.

**Example output:**
```
Config file: C:\Users\username\AppData\Roaming\AutoCaptureOCR\config.yaml
File exists
```

## Configuration

The CLI shares the same configuration file as the GUI application:

**Location:** `%AppData%\AutoCaptureOCR\config.yaml`

**Example config.yaml:**
```yaml
app:
  name: AutoCapture-OCR
  version: 2.0.0

capture:
  default_image_format: PNG
  jpeg_quality: 95
  organize_by_date: false

ocr:
  default_language: en-US
  confidence_threshold: 0.7
  enable_preprocessing: true

export:
  include_metadata: true
  include_ocr_results: true
```

Edit this file to customize CLI behavior.

## Automation Examples

### PowerShell Script - Scheduled Screenshots

```powershell
# Capture every hour and run OCR
$project = "Daily Captures"
$session = Get-Date -Format "yyyy-MM-dd"

autocapture capture fullscreen --project $project --session $session --ocr
```

### Batch Script - Process Folder

```batch
@echo off
REM Process all screenshots and generate report
autocapture ocr batch C:\Screenshots --export C:\Reports\ocr-results.md --format markdown
echo OCR processing complete!
```

### PowerShell - Continuous Capture Mode (Manual Loop)

```powershell
# Capture region every 5 seconds (10 times)
$project = "Monitoring"
$session = "Session-" + (Get-Date -Format "HHmmss")

for ($i = 1; $i -le 10; $i++) {
    Write-Host "Capture $i of 10..."
    autocapture capture region --x 0 --y 0 --width 1920 --height 1080 --project $project --session $session
    Start-Sleep -Seconds 5
}

Write-Host "Exporting results..."
autocapture export captures $project --output monitoring.json
```

## Output Examples

### JSON Export Format
```json
{
  "fileName": "screenshot.png",
  "text": "Sample OCR text...",
  "confidence": 0.95,
  "engine": "Windows OCR",
  "processingTime": 1234.5
}
```

### Markdown Export Format
```markdown
# OCR Batch Results

**Processed:** 10 images
**Successful:** 9
**Failed:** 1

## screenshot1.png

**Confidence:** 95%

```
Extracted text here...
```
```

### CSV Export Format
```csv
FileName,Success,Confidence,Text,Error
"invoice.png",True,0.95,"Invoice #12345...","  "
"receipt.png",True,0.87,"Total: $45.67...","  "
```

## Error Handling

The CLI uses exit codes for automation:
- **0**: Success
- **1**: Error (check error message)

**Example error handling in PowerShell:**
```powershell
autocapture ocr run image.png
if ($LASTEXITCODE -ne 0) {
    Write-Error "OCR failed!"
    exit 1
}
```

## Tips & Best Practices

1. **Multi-Monitor Captures**: Use screen coordinates. To find coordinates:
   - Use GUI app's region selector to see coordinates
   - Or calculate based on monitor positions

2. **Batch Processing**: Use `--recursive` carefully with large folders

3. **Project Organization**: Create separate projects for different workflows

4. **OCR Quality**: Higher resolution images = better OCR accuracy

5. **Automation**: Use Task Scheduler (Windows) or cron (if running under WSL) for scheduled captures

## Troubleshooting

### "OCR Engine Not Available"
- Windows OCR requires Windows 10 or later
- Check language pack is installed in Windows Settings

### "Permission Denied" on Capture
- CLI requires active desktop session (can't run headless)
- Run with appropriate user permissions

### Multi-Monitor Capture Issues
- Verify coordinates using: `SystemInformation.VirtualScreen` in .NET
- Remember: Left monitors = negative X, Above monitors = negative Y

## Integration with GUI App

The CLI and GUI app share:
- ✅ Projects and sessions
- ✅ Configuration file
- ✅ OCR results
- ✅ Metadata templates

You can capture via CLI and view/edit in GUI, or vice versa.

## Future Features (Planned)

- Interactive region selector (launch GUI overlay from CLI)
- Watch mode (monitor folder for new images, auto-OCR)
- Webhook/API integration
- Custom export templates
- Smart filename generation based on OCR content

## Contributing

Report issues or suggestions to the project repository.

## License

Part of AutoCapture-OCR v2.0
