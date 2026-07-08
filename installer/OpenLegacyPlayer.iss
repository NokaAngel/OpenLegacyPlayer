; -- OpenLegacy Player — Inno Setup script --------------------------------
; Build the app first:
;   dotnet publish src/OpenLegacyPlayer -c Release -r win-x64 --self-contained true ^
;       -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
; Then compile this script with Inno Setup 6+ (iscc installer\OpenLegacyPlayer.iss).
; The GitHub Actions release workflow does both automatically.

#ifndef MyAppVersion
  #define MyAppVersion "0.3.0"
#endif
#define MyAppName "OpenLegacy Player"
#define MyAppExeName "OpenLegacyPlayer.exe"
#define MyAppPublisher "NokaAngel"
#define MyAppURL "https://github.com/NokaAngel/OpenLegacyPlayer"

[Setup]
AppId={{8C4C2E5A-6E1B-4F0D-9A3C-2B7D1E5F8A90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=OpenLegacyPlayer-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequiredOverridesAllowed=dialog
; The in-app auto-updater runs this setup silently while exiting the app;
; force-close handles any instance that is still holding files at that moment.
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\src\OpenLegacyPlayer\bin\Release\net10.0-windows\win-x64\publish\*"; \
    DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; ---------------------------------------------------------------------------
; File associations. This registers OpenLegacy Player as a *capable* handler
; and advertises it in Windows' "Default apps" — modern Windows still requires
; the user to confirm the default, which the app links them to from Settings.
; HKA = HKLM for machine installs, HKCU for per-user installs.
; ---------------------------------------------------------------------------
[Registry]
; The ProgID that describes how to open a media file with us.
Root: HKA; Subkey: "Software\Classes\OpenLegacyPlayer.Audio"; ValueType: string; ValueName: ""; ValueData: "Audio file"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\OpenLegacyPlayer.Audio\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\OpenLegacyPlayer.Audio\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Advertise each audio extension against the ProgID (adds us to "Open with",
; without hijacking whatever the user currently has as default).
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: string; ValueName: "OpenLegacyPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue

; Capabilities block, so we show up as a choice in Settings > Default apps.
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "A Frutiger Aero music player."
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aac"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wav"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wma"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogg"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".opus"; ValueData: "OpenLegacyPlayer.Audio"
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\{#MyAppName}\Capabilities"; Flags: uninsdeletevalue

[Run]
; Interactive installs: offer to launch on the finish page.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
    Flags: nowait postinstall skipifsilent
; Silent installs (the in-app auto-updater): relaunch the app automatically.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait skipifnotsilent
