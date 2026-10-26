#define MyAppName "Libro"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Libro"
#define MyAppId "{{28083929-8EB6-45E7-A6CD-D27B813BCC34}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName} · Contabilidad
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Libro
DefaultGroupName=Libro
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Libro-Setup-{#MyAppVersion}
SetupIconFile=libro.ico
UninstallDisplayIcon={app}\Libro.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UninstallLogMode=overwrite
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Instalador de Libro · Contabilidad
VersionInfoProductName=Libro · Contabilidad
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "autostart"; Description: "Iniciar Libro al iniciar sesión en Windows"; GroupDescription: "Opciones:"; Flags: unchecked

[Files]
Source: "native-app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{localappdata}\Libro"; Flags: uninsneveruninstall
Name: "{localappdata}\Libro\data"; Flags: uninsneveruninstall

[InstallDelete]
; Limpia la instalación anterior basada en Node/Next.js. La carpeta de datos
; vive fuera de {app} y nunca se toca.
Type: filesandordirs; Name: "{app}\.next"
Type: filesandordirs; Name: "{app}\node_modules"
Type: files; Name: "{app}\node.exe"
Type: files; Name: "{app}\server.js"
Type: files; Name: "{app}\start-server.cmd"
Type: files; Name: "{app}\package.json"
Type: files; Name: "{app}\LEEME.txt"
Type: files; Name: "{app}\libro.ico"
Type: files; Name: "{app}\Libro.exe"
Type: files; Name: "{app}\CommunityToolkit.Mvvm.dll"
; Accesos directos de la versión anterior que ya no aplican.
Type: files; Name: "{group}\Detener Libro.lnk"
Type: files; Name: "{userstartup}\Libro.lnk"
Type: files; Name: "{autodesktop}\Libro.lnk"

[Icons]
Name: "{group}\Libro"; Filename: "{app}\Libro.exe"; WorkingDir: "{app}"; IconFilename: "{app}\libro.ico"; Comment: "Abrir Libro · Contabilidad"
Name: "{group}\Carpeta de datos"; Filename: "{localappdata}\Libro"; IconFilename: "{app}\libro.ico"
Name: "{group}\Desinstalar Libro"; Filename: "{uninstallexe}"; IconFilename: "{app}\libro.ico"
Name: "{autodesktop}\Libro"; Filename: "{app}\Libro.exe"; WorkingDir: "{app}"; IconFilename: "{app}\libro.ico"; Tasks: desktopicon
Name: "{userstartup}\Libro"; Filename: "{app}\Libro.exe"; WorkingDir: "{app}"; Tasks: autostart

[Run]
Filename: "{app}\Libro.exe"; Description: "Abrir Libro ahora"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Solo se elimina la aplicación. Los datos viven en {localappdata}\Libro\data
; y se conservan expresamente al desinstalar.
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{group}"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  // Detén el servidor de la versión anterior antes de borrar node.exe y .next.
  if FileExists(ExpandConstant('{app}\server.js')) and
     FileExists(ExpandConstant('{app}\Libro.exe')) then
  begin
    Exec(ExpandConstant('{app}\Libro.exe'), '--stop --silent', '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1200);
  end;
  Result := '';
end;

procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'Este instalador actualiza Libro a su versión nativa para Windows.' + #13#10#13#10 +
    'Tus datos, usuarios y respaldos se conservan y se cifran automáticamente.' + #13#10 +
    ExpandConstant('{localappdata}\Libro\data') + #13#10#13#10 +
    'No necesitas desinstalar la versión anterior.';
end;
