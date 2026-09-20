#ifndef AppVersion
  #error AppVersion must be supplied by scripts/build-installer.ps1
#endif

#ifndef AppNumericVersion
  #error AppNumericVersion must be supplied by scripts/build-installer.ps1
#endif

[Setup]
AppId={{AEF6D778-50DE-4813-8CEA-F853D83AE36E}
AppMutex=Local\QuietShelf.SingleInstance
AppName=一页 Yiye
AppVersion={#AppVersion}
AppVerName=一页 Yiye {#AppVersion}
AppPublisher=Yiye contributors
AppPublisherURL=https://github.com/Anle-He/Yiye
AppSupportURL=https://github.com/Anle-He/Yiye/issues
AppUpdatesURL=https://github.com/Anle-He/Yiye/releases
DefaultDirName={localappdata}\Programs\Yiye
DefaultGroupName=一页 Yiye
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=Yiye-Setup-{#AppVersion}
SetupIconFile=..\src\Yiye.App\Assets\app-icon.ico
UninstallDisplayIcon={app}\Yiye.App.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppNumericVersion}
VersionInfoCompany=Yiye contributors
VersionInfoDescription=一页 Yiye Installer
VersionInfoProductName=一页 Yiye
VersionInfoProductVersion={#AppNumericVersion}

[Languages]
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\一页 Yiye"; Filename: "{app}\Yiye.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\一页 Yiye"; Filename: "{app}\Yiye.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{app}\QuietShelf.App.exe"
Type: files; Name: "{app}\QuietShelf.App.dll"
Type: files; Name: "{app}\QuietShelf.App.deps.json"
Type: files; Name: "{app}\QuietShelf.App.runtimeconfig.json"
Type: files; Name: "{app}\QuietShelf.App.pdb"
Type: files; Name: "{autoprograms}\QuietShelf.lnk"
Type: files; Name: "{autodesktop}\QuietShelf.lnk"

[Run]
Filename: "{app}\Yiye.App.exe"; Description: "{cm:LaunchProgram,一页 Yiye}"; Flags: nowait postinstall skipifsilent
