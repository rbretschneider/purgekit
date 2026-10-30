; PurgeKit installer (Inno Setup 6).
; Build: iscc /DAppVersion=0.1.123 /DSourceExe=..\publish\PurgeKit.UI.exe installer\PurgeKit.iss
; Upgrades in place: every build shares the same AppId, so installing a newer version replaces the old one.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\publish\PurgeKit.UI.exe"
#endif

#define AppName "PurgeKit"
#define AppExe "PurgeKit.exe"
#define AppPublisher "Ryan Bretschneider"
#define AppUrl "https://github.com/rbretschneider/purgekit"

[Setup]
AppId={{6B0E4D52-8E1C-4F0B-9C7A-2D5F3B8A9E41}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; PurgeKit scans system folders, so a machine-wide install is the default; /CURRENTUSER installs without admin.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\publish
OutputBaseFilename=PurgeKit-Setup-{#AppVersion}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Interactive installs offer to launch; in-app updates pass /RELAUNCH so PurgeKit comes back after a silent upgrade.
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: ShouldRelaunch

[Messages]
; Moved apps live on other drives behind junctions and keep working without PurgeKit installed.
ConfirmUninstall=Remove %1?%n%nApps you moved to another drive keep working. To bring them back first, use Move Apps → Moved apps → Move back before uninstalling.

[Code]
function ShouldRelaunch: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/RELAUNCH') = 0 then
      Result := True;
end;
