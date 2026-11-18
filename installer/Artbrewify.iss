; Artbrewify installer script (Inno Setup 6)
; Packages the self-contained publish output (artifacts/portable) into a single setup exe.

#define MyAppName "Artbrewify"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "MikePinku (pinkythegawd)"
#define MyAppURL "https://github.com/pinkythegawd/Artbrewify"
#define MyAppExeName "Artbrewify.exe"

[Setup]
AppId={{8E4B7A2C-1F3D-4A9E-B6C5-2D8F0E1A3B47}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright (c) 2026 MikePinku (pinkythegawd)
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=Artbrewify-{#MyAppVersion}-Setup
SetupIconFile=..\src\Artbrewify\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"