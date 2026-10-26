#ifndef AppVersion
  #error AppVersion fehlt. build-installer.ps1 verwenden.
#endif

[Setup]
AppId={{C42AB24B-88E4-4B43-9508-C6E177D1F501}
AppName=Schreib und Tabelle
AppVersion={#AppVersion}
AppPublisher=Dom-Antoni
AppPublisherURL=https://github.com/Dom-Antoni/Byby_Word_and_Excel
AppSupportURL=https://github.com/Dom-Antoni/Byby_Word_and_Excel/issues
AppUpdatesURL=https://github.com/Dom-Antoni/Byby_Word_and_Excel/releases/latest
DefaultDirName={localappdata}\Programs\Schreib-und-Tabelle
DefaultGroupName=Schreib und Tabelle
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\Schreib.exe
SetupIconFile={#PayloadDir}\icons\Schreib.ico
LicenseFile={#SourceRoot}\LICENSE
OutputDir={#InstallerOutput}
#ifdef WebSetup
#ifdef DownloadTest
OutputBaseFilename=Download-Test-Setup
#else
OutputBaseFilename=Schreib-und-Tabelle-Setup
#endif
#else
OutputBaseFilename=Schreib-und-Tabelle-Offline-Setup
#endif
ArchiveExtraction=full
WizardStyle=modern dynamic
Compression=lzma2/fast
SolidCompression=no
ChangesAssociations=yes
DisableWelcomePage=no
DisableProgramGroupPage=yes
CloseApplications=no
RestartApplications=no
AppMutex=Local\BybySchreibRunning,Local\BybyTabelleRunning
SetupMutex=Local\BybySchreibTabelleSetup
UninstallDisplayName=Schreib und Tabelle

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
german.DesktopIcons=Desktop-Symbole für Schreib und Tabelle
german.OpenWith=Apps unter „Öffnen mit“ und in den Standard-App-Einstellungen anbieten
german.ExplorerNew=„Neu → Schreib-Dokument“ und „Neu → Tabelle“ im Explorer anbieten
german.LaunchSchreib=Schreib starten
german.LaunchTabelle=Tabelle starten
german.WebWelcome=Installiert Schreib und Tabelle für dein Windows-Benutzerkonto.%n%nDas vollständige Offline-Paket wird während der Installation einmalig von GitHub heruntergeladen (ca. 320 MB). Danach funktionieren beide Apps offline.
english.DesktopIcons=Desktop shortcuts for Schreib and Tabelle
english.OpenWith=Register apps with Open with and Default apps
english.ExplorerNew=Add New Schreib document and New Tabelle to File Explorer
english.LaunchSchreib=Launch Schreib
english.LaunchTabelle=Launch Tabelle
english.WebWelcome=Installs Schreib and Tabelle for your Windows user account.%n%nThe full offline package is downloaded once from GitHub during installation (about 320 MB). Both apps work offline afterwards.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcons}"
Name: "fileassoc"; Description: "{cm:OpenWith}"
Name: "explorernew"; Description: "{cm:ExplorerNew}"

[Files]
Source: "{#CompilerLicense}"; DestDir: "{app}\installer-licenses"; DestName: "Inno-Setup.txt"; Flags: ignoreversion
#ifdef WebSetup
Source: "{#PayloadURL}"; DestDir: "{app}"; DestName: "payload.zip"; ExternalSize: {#PayloadSize}; Hash: "{#PayloadHash}"; Flags: external download extractarchive ignoreversion recursesubdirs createallsubdirs
#else
Source: "{#PayloadZip}"; DestDir: "{tmp}"; DestName: "payload.zip"; Flags: dontcopy nocompression
Source: "{tmp}\payload.zip"; DestDir: "{app}"; ExternalSize: {#PayloadSize}; Hash: "{#PayloadHash}"; Flags: external extractarchive ignoreversion recursesubdirs createallsubdirs
#endif

[Icons]
Name: "{group}\Schreib"; Filename: "{app}\Schreib.exe"; WorkingDir: "{userdocs}"
Name: "{group}\Tabelle"; Filename: "{app}\Tabelle.exe"; WorkingDir: "{userdocs}"
Name: "{group}\Schreib und Tabelle deinstallieren"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Schreib"; Filename: "{app}\Schreib.exe"; WorkingDir: "{userdocs}"; Tasks: desktopicon
Name: "{autodesktop}\Tabelle"; Filename: "{app}\Tabelle.exe"; WorkingDir: "{userdocs}"; Tasks: desktopicon

[Registry]
#include GeneratedRegistry

[Run]
Filename: "{app}\Schreib.exe"; Description: "{cm:LaunchSchreib}"; Flags: nowait postinstall skipifsilent unchecked
Filename: "{app}\Tabelle.exe"; Description: "{cm:LaunchTabelle}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then MsgBox('Schreib und Tabelle benötigen Microsoft .NET Framework 4.8. Bitte zuerst .NET Framework 4.8 installieren und Setup erneut starten.', mbError, MB_OK);
end;

function OwnTypeAvailable(Extension, ProgId: String): Boolean;
var Current: String;
begin
  Result := (not RegQueryStringValue(HKCU, 'Software\Classes\' + Extension, '', Current)) or (Current = '') or (Current = ProgId);
end;

procedure RemoveOwnDefault(Extension, ProgId: String);
var Current: String;
begin
  if RegQueryStringValue(HKCU, 'Software\Classes\' + Extension, '', Current) and (Current = ProgId) then
    RegDeleteValue(HKCU, 'Software\Classes\' + Extension, '');
  RegDeleteKeyIfEmpty(HKCU, 'Software\Classes\' + Extension + '\OpenWithProgids');
  RegDeleteKeyIfEmpty(HKCU, 'Software\Classes\' + Extension);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then begin
    RemoveOwnDefault('.schreib', 'Byby.Schreib.Document');
    RemoveOwnDefault('.tabelle', 'Byby.Tabelle.Document');
  end;
end;

procedure InitializeWizard;
begin
#ifdef WebSetup
  WizardForm.WelcomeLabel2.Caption := ExpandConstant('{cm:WebWelcome}');
#endif
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
#ifndef WebSetup
  ExtractTemporaryFile('payload.zip');
#endif
end;
