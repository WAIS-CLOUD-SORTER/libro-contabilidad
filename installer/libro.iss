#define MyAppName "Libro"
#define MyAppVersion "1.0.0"
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
VersionInfoVersion={#MyAppVersion}.0
VersionInfoDescription=Instalador de Libro · Contabilidad
VersionInfoProductName=Libro · Contabilidad
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "autostart"; Description: "Iniciar Libro al iniciar sesión en Windows"; GroupDescription: "Opciones:"; Flags: unchecked

[Files]
Source: "app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{localappdata}\Libro"
Name: "{localappdata}\Libro\data"

[InstallDelete]
Type: filesandordirs; Name: "{app}\.next\server\route-cache"
Type: filesandordirs; Name: "{app}\.next\cache"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\.next\server\route-cache"
Type: filesandordirs; Name: "{app}\.next\cache"
Type: dirifempty; Name: "{app}\.next\server"
Type: dirifempty; Name: "{app}\.next"
Type: dirifempty; Name: "{app}"

[Icons]
Name: "{group}\Libro"; Filename: "{app}\Libro.exe"; WorkingDir: "{app}"; IconFilename: "{app}\libro.ico"; Comment: "Abrir Libro · Contabilidad"
Name: "{group}\Detener Libro"; Filename: "{app}\Libro.exe"; Parameters: "--stop"; WorkingDir: "{app}"; IconFilename: "{app}\libro.ico"; Comment: "Detener el servidor local"
Name: "{group}\Carpeta de datos"; Filename: "{localappdata}\Libro"; IconFilename: "{app}\libro.ico"
Name: "{group}\Desinstalar Libro"; Filename: "{uninstallexe}"; IconFilename: "{app}\libro.ico"
Name: "{autodesktop}\Libro"; Filename: "{app}\Libro.exe"; WorkingDir: "{app}"; IconFilename: "{app}\libro.ico"; Tasks: desktopicon
Name: "{userstartup}\Libro"; Filename: "{app}\Libro.exe"; Parameters: "--startup"; WorkingDir: "{app}"; Tasks: autostart

[Run]
Filename: "{app}\Libro.exe"; Description: "Abrir Libro ahora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\Libro.exe"; Parameters: "--stop --silent"; Flags: runhidden; RunOnceId: "DetenerLibro"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // Si se está actualizando sobre una instalación existente, detén el
    // servidor antes de reemplazar node.exe y los archivos de la aplicación.
    if FileExists(ExpandConstant('{app}\Libro.exe')) then
      Exec(ExpandConstant('{app}\Libro.exe'), '--stop --silent', '',
           SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if SuppressibleMsgBox('¿Quieres conservar la base de datos y los respaldos?' + #13#10 + #13#10 +
              'Si eliges No, se borrarán permanentemente de:' + #13#10 +
              ExpandConstant('{localappdata}\Libro'), mbConfirmation, MB_YESNO or MB_DEFBUTTON1,
              IDYES) = IDNO then
    begin
      DelTree(ExpandConstant('{localappdata}\Libro'), True, True, True);
    end;
  end;
end;
