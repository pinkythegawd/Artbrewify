@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [error] dotnet not found on PATH. Install the .NET 8 SDK.
  exit /b 1
)

echo [1/3] Publishing single-file portable exe...
dotnet publish src\Artbrewify -c Release -p:PublishProfile=SingleFile
if errorlevel 1 exit /b 1

echo [2/3] Publishing self-contained folder (installer payload)...
dotnet publish src\Artbrewify -c Release -p:PublishProfile=Portable
if errorlevel 1 exit /b 1

set "ISCC="
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC (
  echo [error] Inno Setup 6 not found. Install it: winget install JRSoftware.InnoSetup
  exit /b 1
)

echo [3/3] Building installer...
"%ISCC%" installer\Artbrewify.iss
if errorlevel 1 exit /b 1

echo.
echo Done.
echo   Portable exe : artifacts\singlefile\Artbrewify.exe
echo   Installer    : artifacts\installer\Artbrewify-1.0.1-Setup.exe
endlocal