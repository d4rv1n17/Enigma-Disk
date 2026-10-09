@echo off
cd /d "%~dp0"
echo Building Enigma Disk (debug)...
dotnet build EnigmaDisk.csproj -c Debug -nologo "-clp:NoSummary;ErrorsOnly" > build.log 2>&1
if errorlevel 1 (
  echo FAILED>> build.log
  echo Build failed, see build.log
  timeout /t 3 >nul
  exit /b 1
)
echo OK>> build.log
start "" "%~dp0bin\Debug\net10.0-windows\EnigmaDisk.exe"
