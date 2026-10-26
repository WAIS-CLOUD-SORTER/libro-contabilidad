param(
    [switch]$Test,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Here "Libro.Cli\Libro.Cli.csproj"

$Dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $Dotnet)) {
    $Dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $Dotnet) {
        throw "No se encontro .NET 8. Instala el SDK/runtime o define el PATH."
    }
}

if ($Clean) {
    & $Dotnet clean $Project -c Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Compilando Libro CLI..." -ForegroundColor Cyan
& $Dotnet build $Project -c Release --nologo -v:minimal
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Listo: $Here\libro-cli.cmd" -ForegroundColor Green

if ($Test) {
    Write-Host "Ejecutando pruebas end-to-end..." -ForegroundColor Cyan
    & python -m unittest discover -s (Join-Path $Here "tests") -v
    exit $LASTEXITCODE
}

exit 0
