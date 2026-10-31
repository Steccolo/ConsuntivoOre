; Installer di Consuntivo Ore (Inno Setup 6).
; Viene compilato da build.ps1 dopo "dotnet publish".

#define MyAppName "Consuntivo Ore"
#define MyAppVersion "1.0.0"
#define MyAppExe "ConsuntivoOre.exe"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{8F3C2B1A-5D4E-4A7B-9C61-2E0F3D9A7B15}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Stefano Ballarini
DefaultDirName={autopf}\ConsuntivoOre
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Installazione per l'utente corrente: nessun permesso di amministratore richiesto.
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=ConsuntivoOre-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Rileva l'app in esecuzione (mutex creato in Program.cs) e chiede di chiuderla.
AppMutex=ConsuntivoOreMutex
CloseApplications=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "autostart"; Description: "Avvia automaticamente all'accensione del PC"; GroupDescription: "Opzioni:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "ConsuntivoOre"; ValueData: """{app}\{#MyAppExe}"" --startup"; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#MyAppExe}"; Flags: runhidden; RunOnceId: "StopApp"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Se l'utente ha deselezionato l'avvio automatico, rimuove un'eventuale voce precedente.
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('autostart') then
    RegDeleteValue(HKEY_CURRENT_USER, '{#RunKey}', 'ConsuntivoOre');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // La voce può essere stata creata anche dalle impostazioni dell'app.
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, '{#RunKey}', 'ConsuntivoOre');
end;
