# Construye el instalador nativo de Libro (WPF/.NET 8) con Inno Setup.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File installer\build-native.ps1

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "desktop\Libro.Desktop\Libro.Desktop.csproj"
$AppDir = Join-Path $PSScriptRoot "native-app"
$Iss = Join-Path $PSScriptRoot "libro-native.iss"
$DistDir = Join-Path $Root "dist"
$Version = "2.0.0"

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

if (-not (Test-Path (Join-Path $AppDir "Libro.exe"))) { throw "No se generó Libro.exe." }
if (-not (Test-Path (Join-Path $AppDir "e_sqlcipher.dll"))) { throw "Falta e_sqlcipher.dll." }
if (Get-ChildItem $AppDir -Recurse -File -Filter "*.db" -ErrorAction SilentlyContinue) {
    throw "La carpeta publicada contiene una base de datos; no debe distribuirse."
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
& $Iscc $Iss
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
