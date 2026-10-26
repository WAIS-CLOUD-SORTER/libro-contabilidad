@echo off
setlocal
cd /d "%~dp0"
set "DATA_DIR=%LOCALAPPDATA%\Libro\data"
set "PORT=3000"
set "HOSTNAME=127.0.0.1"
set "NODE_ENV=production"
if not exist "%LOCALAPPDATA%\Libro" mkdir "%LOCALAPPDATA%\Libro"
if not exist "%DATA_DIR%" mkdir "%DATA_DIR%"
echo [%date% %time%] Iniciando Libro> "%LOCALAPPDATA%\Libro\server.log"
"%~dp0node.exe" "%~dp0server.js">> "%LOCALAPPDATA%\Libro\server.log" 2>&1
