@echo off
setlocal EnableExtensions
set "HERE=%~dp0"
set "DLL=%HERE%Libro.Cli\bin\Release\net8.0-windows\libro-cli.dll"

if not exist "%DLL%" (
  echo ERROR: No se encontro libro-cli.dll.
  echo Compila primero con:
  echo   powershell -ExecutionPolicy Bypass -File "%HERE%build.ps1"
  exit /b 1
)

if defined LIBRO_CLI_DOTNET (
  set "DOTNET=%LIBRO_CLI_DOTNET%"
) else (
  set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
  if not exist "%DOTNET%" (
    where dotnet >nul 2>nul
    if errorlevel 1 (
      echo ERROR: No se encontro dotnet. Instala .NET 8 o define LIBRO_CLI_DOTNET.
      exit /b 1
    )
    set "DOTNET=dotnet"
  )
)

"%DOTNET%" "%DLL%" %*
exit /b %ERRORLEVEL%
