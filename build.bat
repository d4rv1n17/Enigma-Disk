@echo off
cd /d "%~dp0"
echo [1/2] Building Enigma Disk...
dotnet publish EnigmaDisk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o dist\app > build.log 2>&1
if errorlevel 1 goto failed

echo [2/2] Building the installer...
dotnet build Setup\EnigmaDiskSetup.csproj -c Release -o Setup\out >> build.log 2>&1
if errorlevel 1 goto failed
copy /y Setup\out\EnigmaDiskSetup.exe dist\EnigmaDiskSetup.exe >nul
if exist dist\EnigmaDisk.exe del /q dist\EnigmaDisk.exe

echo BUILD OK>> build.log
echo.
echo Done:
echo   dist\EnigmaDiskSetup.exe  - installer to share
echo   dist\app\EnigmaDisk.exe   - portable app, no install needed
start "" "%~dp0dist"
exit /b 0

:failed
echo BUILD FAILED>> build.log
echo Build failed. Details in build.log
type build.log
pause
exit /b 1
