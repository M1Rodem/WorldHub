#define AppName "WorldHub"

#ifndef AppVersion
  #error "AppVersion is not defined. Run Inno Setup through Build-Release.ps1 with /DAppVersion=<version>."
#endif

[Setup]

AppId={{WorldHub-App}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=WorldHub

DefaultDirName={autopf}\WorldHub
DefaultGroupName=WorldHub

OutputDir=..\artifacts
OutputBaseFilename=WorldHub-Setup-v{#AppVersion}

ArchitecturesInstallIn64BitMode=x64compatible

Compression=lzma
SolidCompression=yes

WizardStyle=modern

UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\WorldHub.App.exe

PrivilegesRequired=admin


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