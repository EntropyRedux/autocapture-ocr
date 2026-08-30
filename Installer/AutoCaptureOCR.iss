; AutoCapture OCR v2.0 - Inno Setup Installer Script
; Build with: iscc AutoCaptureOCR.iss
; Prerequisites: Inno Setup 6.x (https://jrsoftware.org/isinfo.php)

#define MyAppName      "AutoCapture OCR"
#define MyAppVersion   "2.0.0"
#define MyAppPublisher "mbula"
#define MyAppExeName   "AutoCaptureOCR.exe"
#define SourceDir      "..\publish\win-x64"
#define OutputDir      "..\dist"

[Setup]
; {{...} is how Inno Setup encodes a literal {GUID} in AppId
AppId={{A3F7C2D1-4B8E-4F9A-B2C3-D4E5F6A7B8C9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

; Install to Program Files by default
DefaultDirName={autopf}\AutoCaptureOCR
DefaultGroupName={#MyAppName}

; Installer output
OutputDir={#OutputDir}
OutputBaseFilename=AutoCaptureOCR-Setup-v{#MyAppVersion}

; Appearance
SetupIconFile=..\App\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
WizardSmallImageFile=compiler:WizClassicSmallImage.bmp

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; Privileges & architecture
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; Versioning
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; Prevent running multiple instances of setup
AppMutex=AutoCaptureOCR_Setup_Mutex

; Close the app if it's running during install
CloseApplications=yes
CloseApplicationsFilter=AutoCaptureOCR.exe
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "Create a &desktop shortcut";          GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupicon";  Description: "Launch automatically at &Windows startup"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; All published application files (self-contained, includes .NET runtime)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu
Name: "{group}\{#MyAppName}";            Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}";  Filename: "{uninstallexe}"

; Desktop (optional task)
Name: "{autodesktop}\{#MyAppName}";      Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Startup (optional task)
Name: "{autostartup}\{#MyAppName}";      Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
; Offer to launch after install
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Uncomment to remove AppData on uninstall (user data will be lost):
; Type: filesandordirs; Name: "{userappdata}\AutoCaptureOCR"
