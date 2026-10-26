# Construye el APK de Libro Ventas sin Gradle: usa directamente las herramientas
# de la SDK de Android (aapt2, javac, d8, zipalign, apksigner).
#
# Requisitos (ver README → "App para Android"):
#   - JDK 17
#   - Android SDK con platform-tools, platforms;android-34 y build-tools;34.0.0
#
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File android\build-apk.ps1
#
# Variables opcionales:
#   ANDROID_TOOLCHAIN  carpeta con jdk\ y sdk\ (por defecto C:\Proyectos\android-toolchain)
#   ANDROID_HOME       ruta del SDK
#   JAVA_HOME          ruta del JDK
#   LIBRO_APK_PASS     contraseña del keystore (por defecto libroventas)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Root = Split-Path -Parent $PSScriptRoot
$Project = $PSScriptRoot
$AppDir = Join-Path $Project "LibroVentas"
$Build = Join-Path $Project "build"
$Dist = Join-Path $Root "dist"

$Toolchain = if ($env:ANDROID_TOOLCHAIN) { $env:ANDROID_TOOLCHAIN } else { "C:\Proyectos\android-toolchain" }
$Sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $Toolchain "sdk" }
$Jdk = if ($env:JAVA_HOME) { $env:JAVA_HOME } else { Join-Path $Toolchain "jdk" }

$BuildTools = Join-Path $Sdk "build-tools\34.0.0"
$AndroidJar = Join-Path $Sdk "platforms\android-34\android.jar"
$JavaC = Join-Path $Jdk "bin\javac.exe"
$KeyTool = Join-Path $Jdk "bin\keytool.exe"

foreach ($tool in @($AndroidJar, "$BuildTools\aapt2.exe", "$BuildTools\d8.bat",
                    "$BuildTools\zipalign.exe", "$BuildTools\apksigner.bat", $JavaC, $KeyTool)) {
    if (-not (Test-Path $tool)) { throw "Falta $tool. Revisa la instalación del SDK o define ANDROID_HOME/JAVA_HOME." }
}
$env:JAVA_HOME = $Jdk

# La versión vive en el .csproj de la aplicación: una sola fuente.
$csproj = Join-Path $Root "desktop\Libro.Desktop\Libro.Desktop.csproj"
$version = (Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value.Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Versión inválida en el .csproj: $version" }
$parts = $version.Split('.')
$versionCode = [int]$parts[0] * 10000 + [int]$parts[1] * 100 + [int]$parts[2]

Write-Host "Libro Ventas $version (versionCode $versionCode)" -ForegroundColor Cyan

if (Test-Path $Build) { Remove-Item $Build -Recurse -Force }
foreach ($dir in @("gen", "classes", "dex")) { New-Item -ItemType Directory -Path (Join-Path $Build $dir) -Force | Out-Null }

Write-Host "1/6 Compilando recursos (aapt2)..." -ForegroundColor Cyan
& "$BuildTools\aapt2.exe" compile --dir (Join-Path $AppDir "res") -o (Join-Path $Build "res.zip")
if ($LASTEXITCODE -ne 0) { throw "aapt2 compile falló." }

Write-Host "2/6 Enlazando manifiesto y recursos..." -ForegroundColor Cyan
$unsigned = Join-Path $Build "app-unsigned.apk"
& "$BuildTools\aapt2.exe" link -o $unsigned `
    -I $AndroidJar `
    --manifest (Join-Path $AppDir "AndroidManifest.xml") `
    (Join-Path $Build "res.zip") `
    --java (Join-Path $Build "gen") `
    --min-sdk-version 24 --target-sdk-version 34 `
    --version-code $versionCode --version-name $version
if ($LASTEXITCODE -ne 0) { throw "aapt2 link falló." }

Write-Host "3/6 Compilando Java (javac)..." -ForegroundColor Cyan
$sources = @()
$sources += Get-ChildItem (Join-Path $AppDir "java") -Recurse -Filter *.java | ForEach-Object { $_.FullName }
$sources += Get-ChildItem (Join-Path $Build "gen") -Recurse -Filter *.java | ForEach-Object { $_.FullName }
& $JavaC -encoding UTF-8 -source 8 -target 8 -nowarn `
    -classpath $AndroidJar -d (Join-Path $Build "classes") @sources
if ($LASTEXITCODE -ne 0) { throw "javac falló." }

Write-Host "4/6 Generando classes.dex (d8)..." -ForegroundColor Cyan
$classes = Get-ChildItem (Join-Path $Build "classes") -Recurse -Filter *.class | ForEach-Object { $_.FullName }
& "$BuildTools\d8.bat" --lib $AndroidJar --min-api 24 --output (Join-Path $Build "dex") @classes
if ($LASTEXITCODE -ne 0) { throw "d8 falló." }

# Empaquetar el dex dentro del APK que generó aapt2.
$dex = Join-Path $Build "dex\classes.dex"
if (-not (Test-Path $dex)) { throw "d8 no generó classes.dex." }
$zip = [System.IO.Compression.ZipFile]::Open($unsigned, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dex, "classes.dex") | Out-Null
} finally {
    $zip.Dispose()
}

Write-Host "5/6 Alineando (zipalign)..." -ForegroundColor Cyan
$aligned = Join-Path $Build "app-aligned.apk"
& "$BuildTools\zipalign.exe" -f -p 4 $unsigned $aligned
if ($LASTEXITCODE -ne 0) { throw "zipalign falló." }

Write-Host "6/6 Firmando (apksigner)..." -ForegroundColor Cyan
$keystore = Join-Path $Toolchain "libro-ventas.keystore"
$storePass = if ($env:LIBRO_APK_PASS) { $env:LIBRO_APK_PASS } else { "libroventas" }
$alias = "libro"

if (-not (Test-Path $keystore)) {
    Write-Host "  Creando keystore nuevo en $keystore" -ForegroundColor Yellow
    & $KeyTool -genkeypair -keystore $keystore -alias $alias -keyalg RSA -keysize 2048 `
        -validity 10000 -storepass $storePass -keypass $storePass `
        -dname "CN=Libro Ventas, OU=Libro, O=Libro, C=MX"
    if ($LASTEXITCODE -ne 0) { throw "keytool falló." }
}

if (-not (Test-Path $Dist)) { New-Item -ItemType Directory -Path $Dist -Force | Out-Null }
$output = Join-Path $Dist "LibroVentas-$version.apk"

& "$BuildTools\apksigner.bat" sign `
    --ks $keystore --ks-key-alias $alias `
    --ks-pass "pass:$storePass" --key-pass "pass:$storePass" `
    --out $output $aligned
if ($LASTEXITCODE -ne 0) { throw "apksigner falló." }

& "$BuildTools\apksigner.bat" verify --print-certs $output | Select-Object -First 4

$apk = Get-Item $output
Write-Host ""
Write-Host ("APK:     " + $apk.FullName)
Write-Host ("Tamaño:  {0:N0} bytes" -f $apk.Length)
Write-Host ("SHA256:  " + (Get-FileHash $output -Algorithm SHA256).Hash)
Write-Host "Listo." -ForegroundColor Green
