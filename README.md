# AutoCapture-OCR

**Project-Based Screen Capture with Automated OCR**

A complete rebuild of AutoCapture-OCR featuring project-based organization, metadata templates, analytics, and global hotkeys.

---

## ✨ Features

### 📸 Capture Management
- **Interactive Region Selection**: Drag-to-select with real-time dimensions
- **Fullscreen Capture**: Capture entire screen with one click
- **Background OCR Processing**: Non-blocking queue with Windows OCR engine
- **Thumbnail Gallery**: Visual preview of all captures

### 📁 Project Organization
- **Project-Based Workflow**: Organize captures into projects and sessions
- **Session Management**: Group related captures within projects
- **Persistent Storage**: All data saved to JSON for easy portability

### 🏷️ Metadata System
- **Flexible Templates**: Create custom metadata templates
- **6 Field Types**: Text, MultilineText, Number, Date, Dropdown, Checkbox
- **Built-in Templates**:
  - Basic Capture (Category + Notes)
  - UI Documentation (Element tracking)
  - Receipt Tracking (Expense management)
  - Code Documentation (Code snippet tracking)

### 📊 Analytics & Export
- **Visual Dashboard**: Comprehensive analytics with charts
- **Export to JSON**: Full structured data with optional GZIP compression
- **Export to CSV**: Spreadsheet-compatible format
- **Customizable Export**: Toggle OCR, metadata, bounding boxes, thumbnails

### ⌨️ Productivity Features
- **Global Hotkeys**:
  - `Ctrl+Shift+C` - Capture region
  - `Ctrl+Shift+F` - Capture fullscreen
  - `Ctrl+Shift+P` - Process queue
- **Toast Notifications**: Visual feedback for all actions
- **Image Viewer**: Full-size view with OCR text and navigation

---

## 🚀 Quick Start

### Prerequisites
- Windows 10/11 (version 22621 or higher)
- .NET 9.0 Runtime

### Build & Run

```bash
# Navigate to the App folder
cd Repo/AutoCapture-OCR/dev/App

# Run the application
dotnet run
```

### First Use

1. **Create a Project**: Click "➕ New Project" in the toolbar
2. **Create a Session**: Click "📁 New Session" to start capturing
3. **Capture**: Press `Ctrl+Shift+C` and select a screen region
4. **View Results**: Click any thumbnail to view full image and OCR text
5. **Add Metadata**: Click "Edit Metadata" in the image viewer
6. **Analyze**: Click "📊 Analytics" to view statistics
7. **Export**: Click "💾 Export" to save data

---

## 📂 Project Structure

```
dev/
├── Core/                    # Business logic library
│   ├── Models/             # Data models
│   ├── Services/           # Business services
│   ├── Export/             # JSON/CSV exporters
│   ├── OCR/                # OCR engines
│   └── Capture/            # Capture functionality
│
└── App/                    # WPF application
    ├── ViewModels/         # MVVM ViewModels
    ├── Views/              # XAML views
    ├── Services/           # UI services (hotkeys, notifications)
    └── Converters/         # XAML converters
```

---

## ⌨️ Keyboard Shortcuts

### Global (Work Anywhere)
- `Ctrl+Shift+C` - Capture region
- `Ctrl+Shift+F` - Capture fullscreen
- `Ctrl+Shift+P` - Process queue

### In Application
- `Escape` - Close dialogs/overlays
- `Enter` - Confirm dialogs
- `Arrow Keys` - Navigate in image viewer

---

## 💾 Data Storage

### Application Data
```
%AppData%\AutoCaptureOCR\
├── projects.json           # Project index
├── metadata_templates.json # User templates
└── config.yaml            # Settings
```

### Project Files
```
[Project Save Path]/
├── project.json           # Project metadata
└── captures/              # Images + OCR results
    ├── capture_*.png
    └── capture_*_ocr.txt
```

---

## 📊 Analytics Metrics

The dashboard provides comprehensive statistics:

- **Total Captures** & **Success Rate**
- **Average OCR Confidence**
- **Words & Lines Extracted**
- **Status Breakdown** (Completed/Processing/Failed)
- **Confidence Distribution** (High/Medium/Low)
- **Date Range** & **OCR Engine Usage**

---

## 📦 Export Formats

### JSON Export
Full structured data with all details, optionally compressed with GZIP.

### CSV Export
Spreadsheet-compatible tabular format for Excel/Google Sheets.

### Export Options
- ☑️ OCR results
- ☑️ Metadata
- ☑️ Bounding boxes (JSON only)
- ☑️ Thumbnails as Base64 (JSON only)
- ☑️ GZIP compression (JSON only)

---

## 🛠️ Development

### Tech Stack
- **.NET 9.0** (net9.0-windows10.0.22621)
- **WPF** for desktop UI
- **MVVM** architecture
- **Windows.Media.Ocr** for text recognition
- **Newtonsoft.Json** for serialization
- **YamlDotNet** for configuration

### Build from Source

```bash
cd Repo/AutoCapture-OCR/dev
dotnet restore
dotnet build --configuration Release
```

---

## 📋 Development Phases

All phases complete ✅

### Phase 1: MVP Foundation
- Project/session management
- Screen capture & OCR
- Background processing
- Image viewer

### Phase 2: Metadata System
- Template engine
- Built-in templates
- Dynamic UI generation
- Validation

### Phase 3: Export & Analytics
- JSON/CSV export
- Analytics calculation
- Visual dashboard

### Phase 4: Hotkeys & Polish
- Global hotkeys
- Toast notifications
- User feedback

---

## 🐛 Troubleshooting

**OCR Not Working:**
- Install Windows OCR language pack
- Settings → Time & Language → Language → Add features → OCR

**Hotkeys Not Registering:**
- Check for conflicts with other applications
- Try restarting the application

**Export Fails:**
- Check disk space and folder permissions
- Verify antivirus isn't blocking file access

---

## 📝 Session Log

See [SESSION_LOG.md](SESSION_LOG.md) for detailed development history.

---

## 📜 License

---

**AutoCapture-OCR v2.0** - Capture, Organize, Analyze
