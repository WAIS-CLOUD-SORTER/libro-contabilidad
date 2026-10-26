# Construye el instalador nativo de Libro (WPF/.NET 8) con Inno Setup.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File installer\build-native.ps1

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "desktop\Libro.Desktop\Libro.Desktop.csproj"
$AppDir = Join-Path $PSScriptRoot "native-app"
$Iss = Join-Path $PSScriptRoot "libro-native.iss"
$DistDir = Join-Path $Root "dist"

# H-30: la versión vive en un solo lugar (el .csproj) y se propaga al
# instalador. Antes estaba codificada aquí, en el .csproj y en el .iss.
$VersionMatch = Select-String -Path $Project -Pattern '<Version>([^<]+)</Version>'
if (-not $VersionMatch) { throw "No se encontró <Version> en $Project." }
$Version = $VersionMatch.Matches[0].Groups[1].Value.Trim()
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "La versión '$Version' del .csproj no tiene el formato Major.Minor.Patch."
}

$Dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $Dotnet)) {
    $Dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}
if (-not $Dotnet) { throw "No se encontró el SDK de .NET 8." }

$Iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $Iscc)) { throw "No se encontró Inno Setup 6 (ISCC.exe)." }

Write-Host "1/4 Publicando Libro nativo (win-x64, self-contained)..." -ForegroundColor Cyan
if (Test-Path $AppDir) { Remove-Item $AppDir -Recurse -Force }
& $Dotnet publish $Project -c Release -r win-x64 --self-contained true `
    -p:DebugType=None -p:DebugSymbols=false -o $AppDir
if ($LASTEXITCODE -ne 0) { throw "Falló dotnet publish." }

# La app usa español; conservar solo los recursos satélite de ese idioma.
# Se eliminan únicamente carpetas de cultura conocidas, no directorios como
# "runtimes" que pudieran contener dependencias nativas.
$Languages = @(
    "cs", "de", "fr", "it", "ja", "ko", "pl",
    "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant"
)
foreach ($Language in $Languages) {
    $LanguageDir = Join-Path $AppDir $Language
    if (Test-Path $LanguageDir) { Remove-Item $LanguageDir -Recurse -Force }
}

# H-36: los accesos directos apuntan a {app}\libro.ico, pero dotnet publish no
# copia el icono. Sin esta copia el instalador mostraba un icono genérico.
$Icon = Join-Path $PSScriptRoot "libro.ico"
if (-not (Test-Path $Icon)) { throw "Falta installer\libro.ico." }
Copy-Item -LiteralPath $Icon -Destination (Join-Path $AppDir "libro.ico") -Force

if (-not (Test-Path (Join-Path $AppDir "Libro.exe"))) { throw "No se generó Libro.exe." }
if (-not (Test-Path (Join-Path $AppDir "e_sqlcipher.dll"))) { throw "Falta e_sqlcipher.dll." }

# H-35: la guardia anterior sólo miraba *.db. Clave, respaldos y bitácora también
# son datos reales que no deben viajar en el instalador.
$SensitivePatterns = @("*.db", "*.db-wal", "*.db-shm", "*.libro", "*.plano", "clave.bin", "libro.log")
foreach ($Pattern in $SensitivePatterns) {
    $Found = Get-ChildItem $AppDir -Recurse -File -Filter $Pattern -ErrorAction SilentlyContinue
    if ($Found) {
        throw "La carpeta publicada contiene artefactos sensibles ($Pattern): $(@($Found)[0].FullName). No debe distribuirse."
    }
}

# H-41: el repositorio no debe conservar bases planas con datos reales.
$RepoRoot = Split-Path $PSScriptRoot -Parent
foreach ($DataDir in @("data", "backups")) {
    if (Test-Path (Join-Path $RepoRoot $DataDir)) {
        throw "El repositorio contiene la carpeta '$DataDir', que puede tener datos reales en texto plano. Muévela fuera del proyecto antes de compilar."
    }
}

Write-Host "2/4 Ejecutando pruebas internas..." -ForegroundColor Cyan
$SelfTestLog = Join-Path $env:TEMP "libro-selftest-build.log"
if (Test-Path $SelfTestLog) { Remove-Item $SelfTestLog -Force }
$env:LIBRO_SELFTEST_LOG = $SelfTestLog
try {
    $test = Start-Process -FilePath (Join-Path $AppDir "Libro.exe") `
        -ArgumentList "--self-test" -Wait -PassThru
} finally {
    Remove-Item Env:LIBRO_SELFTEST_LOG -ErrorAction SilentlyContinue
}
if ($test.ExitCode -ne 0) { throw "La autoprueba terminó con código $($test.ExitCode)." }
if (-not (Test-Path $SelfTestLog) -or -not (Select-String -Path $SelfTestLog -Pattern "SELFTEST_OK" -Quiet)) {
    throw "La autoprueba no reportó SELFTEST_OK. Revisa $SelfTestLog."
}

Write-Host "3/4 Compilando instalador con Inno Setup..." -ForegroundColor Cyan
& $Iscc "/DMyAppVersion=$Version" $Iss
if ($LASTEXITCODE -ne 0) { throw "Falló Inno Setup." }

$Setup = Join-Path $DistDir "Libro-Setup-$Version.exe"
if (-not (Test-Path $Setup)) { throw "No se generó $Setup." }

Write-Host "4/4 Calculando SHA256..." -ForegroundColor Cyan
$Hash = (Get-FileHash $Setup -Algorithm SHA256).Hash
$HashFile = "$Setup.sha256"
"$Hash  $(Split-Path $Setup -Leaf)" |
    Set-Content -Path $HashFile -Encoding ASCII

Get-Item $Setup | Select-Object FullName, Length, LastWriteTime | Format-List
Get-Content $HashFile
Write-Host "Listo." -ForegroundColor Green
