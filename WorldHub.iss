#ifndef AppName
#define AppName "WorldHub"
#endif

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif


[Setup]

AppId={{WorldHub-App}}

AppName={#AppName}
AppVersion={#AppVersion}

AppPublisher=WorldHub


DefaultDirName={autopf}\WorldHub
DefaultGroupName=WorldHub


UninstallDisplayIcon={app}\WorldHub.App.exe


OutputDir=artifacts
OutputBaseFilename=WorldHub-Setup-v{#AppVersion}


ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible


CloseApplications=yes


Compression=lzma2/ultra64
SolidCompression=yes


WizardStyle=modern


[Tasks]

Name: "desktopicon"; \
Description: "Создать ярлык на рабочем столе"; \
GroupDescription: "Дополнительные задачи:"; \
Flags: unchecked



[Files]

Source: "release\*"; \
DestDir: "{app}"; \
Flags: recursesubdirs ignoreversion



[Icons]

Name: "{group}\WorldHub"; \
Filename: "{app}\WorldHub.App.exe"


Name: "{group}\Удалить WorldHub"; \
Filename: "{uninstallexe}"


Name: "{autodesktop}\WorldHub"; \
Filename: "{app}\WorldHub.App.exe"; \
Tasks: desktopicon



[Run]

Filename: "{app}\WorldHub.App.exe"; \
Description: "Запустить WorldHub"; \
Flags: nowait postinstall skipifsilent