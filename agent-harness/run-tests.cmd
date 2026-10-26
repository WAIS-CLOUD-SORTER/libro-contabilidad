@echo off
setlocal EnableExtensions
set "PYTHONUTF8=1"
set "HERE=%~dp0"
set "DLL=%HERE%Libro.Cli\bin\Release\net8.0-windows\libro-cli.dll"

if not exist "%DLL%" (
  echo ERROR: No existe la compilacion Release.
  echo Ejecuta: powershell -ExecutionPolicy Bypass -File "%HERE%build.ps1"
  exit /b 1
)

python -m unittest discover -s "%HERE%tests" -v
exit /b %ERRORLEVEL%
