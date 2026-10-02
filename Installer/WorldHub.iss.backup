#define AppName "WorldHub"
#define AppVersion "0.2.3"

[Setup]

AppId={{WorldHub-App}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=WorldHub

DefaultDirName={autopf}\WorldHub
DefaultGroupName=WorldHub

OutputDir=..\artifacts
OutputBaseFilename=WorldHub-Setup-v{#AppVersion}

ArchitecturesInstallIn64BitMode=x64

Compression=lzma
SolidCompression=yes

WizardStyle=modern


[Files]

Source: "..\release\*"; \
DestDir: "{app}"; \
Flags: recursesubdirs ignoreversion


[Icons]

Name: "{group}\WorldHub"; \
Filename: "{app}\WorldHub.App.exe"

Name: "{autodesktop}\WorldHub"; \
Filename: "{app}\WorldHub.App.exe"


[Run]

Filename: "{app}\WorldHub.App.exe"; \
Description: "Запустить WorldHub"; \
Flags: nowait postinstall skipifsilent