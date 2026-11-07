; Built only from an already verified publish. No dotnet/build commands here.
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef ProductVersion
  #error ProductVersion is required
#endif
#ifndef NumericVersion
  #error NumericVersion is required
#endif
#ifndef Publisher
  #define Publisher ""
#endif

[Setup]
AppId={{616B3283-5A2D-48A9-9D4C-477803C10D74}
AppName=.KID
AppVersion={#ProductVersion}
AppVerName=.KID {#ProductVersion}
AppPublisher={#Publisher}
VersionInfoVersion={#NumericVersion}
VersionInfoProductVersion={#NumericVersion}
VersionInfoProductTextVersion={#ProductVersion}
DefaultDirName={autopf}\KID
DefaultGroupName=.KID
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
UsePreviousPrivileges=yes
DisableProgramGroupPage=yes
UninstallDisplayName=.KID
UninstallDisplayIcon={app}\KID.WPF.IDE.exe
CloseApplications=no
RestartApplications=no
ChangesAssociations=no
OutputDir={#OutputDir}
OutputBaseFilename=KID-{#ProductVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
#ifdef ProductionSigned
SignTool=kid
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\.KID"; Filename: "{app}\KID.WPF.IDE.exe"; WorkingDir: "{app}"

[Registry]
Root: HKA; Subkey: "Software\KID\Installation"; ValueType: string; ValueName: "Version"; ValueData: "{#NumericVersion}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\KID\Installation"; ValueType: string; ValueName: "Path"; ValueData: "{app}"

[Code]
function IdleIDE: Boolean;
var Locator, Service, Processes: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Service.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name="KID.WPF.IDE.exe"');
    Result := Processes.Count = 0;
  except
    Log('Cannot verify running IDE processes; refusing to change installation.');
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Root, OtherRoot: Integer; Previous: String; OldVersion, NewVersion: Int64;
begin
  Result := '';
  if not IdleIDE then begin
    Result := 'Close .KID after saving your work, then run Setup again.';
    exit;
  end;
  if IsAdminInstallMode then begin Root := HKLM64; OtherRoot := HKCU64; end
  else begin Root := HKCU64; OtherRoot := HKLM64; end;
  if RegKeyExists(OtherRoot, 'Software\KID\Installation') then begin
    Result := 'An installation in another scope exists. Remove it before changing installation scope.';
    exit;
  end;
  if RegQueryStringValue(Root, 'Software\KID\Installation', 'Version', Previous) then begin
    if not StrToVersion(Previous, OldVersion) then begin
      Result := 'Cannot read the installed version. Repair or remove the existing installation first.';
      exit;
    end;
    if not StrToVersion('{#NumericVersion}', NewVersion) then begin
      Result := 'Invalid candidate version.';
      exit;
    end;
    if ComparePackedVersion(OldVersion, NewVersion) > 0 then
      Result := 'Downgrade is not supported. The installed version and user data were preserved.';
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := IdleIDE;
  if not Result then
    MsgBox('Close .KID after saving your work, then run Uninstall again.', mbError, MB_OK);
end;
