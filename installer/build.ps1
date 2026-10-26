# Construye el instalador de Windows de Libro.
# Uso: powershell -ExecutionPolicy Bypass -File installer\build.ps1

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$AppDir = Join-Path $PSScriptRoot "app"
$DistDir = Join-Path $Root "dist"
$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$Iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
$Version = "1.0.0"

if (-not (Test-Path $Csc)) { throw "No se encontró csc.exe de .NET Framework." }
if (-not (Test-Path $Iscc)) { throw "No se encontró Inno Setup 6 (ISCC.exe)." }

$listening = Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue
if ($listening) {
  throw "El puerto 3000 está en uso. Detén la aplicación (o el servidor de desarrollo) antes de compilar."
}

Write-Host "1/6 Compilando Next.js en modo standalone..." -ForegroundColor Cyan
Push-Location $Root
try {
  & npm.cmd run build
  if ($LASTEXITCODE -ne 0) { throw "Falló npm run build." }
} finally {
  Pop-Location
}

# Nunca dejes la base real dentro del standalone, aunque el trazador de Next
# la haya incluido por las lecturas dinámicas de fs.
$StandaloneLiveData = Join-Path $Root ".next\standalone\data"
if (Test-Path $StandaloneLiveData) { Remove-Item $StandaloneLiveData -Recurse -Force }
Get-ChildItem (Join-Path $Root ".next\standalone") -Filter "*.db*" -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "2/6 Preparando archivos de la aplicación..." -ForegroundColor Cyan
if (Test-Path $AppDir) { Remove-Item $AppDir -Recurse -Force }
New-Item -ItemType Directory -Path $AppDir | Out-Null

$Standalone = Join-Path $Root ".next\standalone"
Copy-Item (Join-Path $Standalone "*") $AppDir -Recurse -Force
$StandaloneData = Join-Path $AppDir "data"
if (Test-Path $StandaloneData) { Remove-Item $StandaloneData -Recurse -Force }
Get-ChildItem $AppDir -Filter "*.db*" -File -ErrorAction SilentlyContinue | Remove-Item -Force
$DbLeaks = Get-ChildItem -Path $AppDir -Recurse -File -Include *.db,*.db-wal,*.db-shm -ErrorAction SilentlyContinue
if ($DbLeaks) { throw "El staging contiene archivos de base de datos: $($DbLeaks.FullName -join ', ')" }
if (Test-Path (Join-Path $AppDir "data")) { throw "El staging contiene una carpeta data." }
$CriticalMetadata = Join-Path $AppDir "node_modules\next\dist\lib\metadata\get-metadata-route.js"
if (-not (Test-Path $CriticalMetadata)) { throw "El standalone no incluye get-metadata-route.js. Revisa next.config.ts y vuelve a compilar." }

$StaticTarget = Join-Path $AppDir ".next\static"
New-Item -ItemType Directory -Path $StaticTarget -Force | Out-Null
Copy-Item (Join-Path $Root ".next\static\*") $StaticTarget -Recurse -Force

$NodeExe = (Get-Command node -ErrorAction Stop).Source
Copy-Item $NodeExe (Join-Path $AppDir "node.exe") -Force

Write-Host "3/6 Generando icono y lanzador..." -ForegroundColor Cyan
& $Csc /nologo /target:exe /platform:x64 /codepage:65001 /out:"$PSScriptRoot\IconBuilder.exe" "$PSScriptRoot\IconBuilder.cs" /reference:System.Drawing.dll
if ($LASTEXITCODE -ne 0) { throw "No se pudo compilar IconBuilder.cs." }
& "$PSScriptRoot\IconBuilder.exe" "$PSScriptRoot\libro.ico"
if ($LASTEXITCODE -ne 0) { throw "No se pudo generar libro.ico." }
Remove-Item "$PSScriptRoot\IconBuilder.exe" -Force -ErrorAction SilentlyContinue

& $Csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /win32icon:"$PSScriptRoot\libro.ico" /out:"$PSScriptRoot\Libro.exe" "$PSScriptRoot\Launcher.cs" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Net.Http.dll
if ($LASTEXITCODE -ne 0) { throw "No se pudo compilar Launcher.cs." }

Copy-Item "$PSScriptRoot\Libro.exe" $AppDir -Force
Copy-Item "$PSScriptRoot\libro.ico" $AppDir -Force

$CmdSource = Get-Content -Raw "$PSScriptRoot\start-server.cmd"
$CmdSource = $CmdSource -replace "`r?`n", "`r`n"
[IO.File]::WriteAllText((Join-Path $AppDir "start-server.cmd"), $CmdSource, [Text.Encoding]::ASCII)

$Leeme = @"
Libro · Contabilidad
====================

Abrir la aplicación
-------------------
Usa el acceso directo "Libro" del escritorio o del menú Inicio.
La aplicación arranca un servidor local y abre el navegador en:
http://127.0.0.1:3000

Detener el servidor
-------------------
Usa "Detener Libro" en el menú Inicio.
También puedes cerrar la sesión de Windows; el servidor se detiene al apagar
la computadora.

Dónde están tus datos
---------------------
%LOCALAPPDATA%\Libro\data\contabilidad.db
Respaldos:
%LOCALAPPDATA%\Libro\data\respaldos

Copia esa carpeta a una USB o a la nube con frecuencia.
El desinstalador te pregunta si quieres conservar o borrar los datos.

Acceso desde otra computadora de la red
---------------------------------------
Por seguridad, el instalador deja el servidor escuchando solo en esta
computadora (127.0.0.1). Para acceso por LAN, edita start-server.cmd y cambia
HOSTNAME=127.0.0.1 por HOSTNAME=0.0.0.0 en una red de confianza. Consulta el
README del proyecto para más detalles.

Notas
-----
- Las facturas son documentos internos: no timbran CFDI.
- La nómina y los impuestos son estimaciones de control interno.
- Esta aplicación no sustituye la asesoría de un contador.

Si el servidor no arranca, revisa:
%LOCALAPPDATA%\Libro\server.log
"@
[IO.File]::WriteAllText((Join-Path $AppDir "LEEME.txt"), $Leeme, (New-Object Text.UTF8Encoding($false)))

Write-Host "4/6 Empaquetando con Inno Setup..." -ForegroundColor Cyan
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir | Out-Null }
& $Iscc "$PSScriptRoot\libro.iss"
if ($LASTEXITCODE -ne 0) { throw "Falló Inno Setup." }

$Installer = Join-Path $DistDir "Libro-Setup-$Version.exe"
if (-not (Test-Path $Installer)) { throw "No se generó el instalador esperado." }

Write-Host "5/6 Verificando contenido..." -ForegroundColor Cyan
$sizeMb = [math]::Round((Get-Item $Installer).Length / 1MB, 1)
$hash = (Get-FileHash $Installer -Algorithm SHA256).Hash
"$hash  Libro-Setup-$Version.exe" | Set-Content -Path "$Installer.sha256" -Encoding ASCII
Write-Host "6/6 Listo: $Installer ($sizeMb MB)" -ForegroundColor Green
Write-Host "SHA256: $hash" -ForegroundColor Green
Write-Host "Checksum: $Installer.sha256" -ForegroundColor Green
