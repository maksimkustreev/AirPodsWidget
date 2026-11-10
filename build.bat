@echo off
setlocal
cd /d "%~dp0"

echo ========================================
echo   Building AirPodsWidget for Windows
 echo ========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: .NET 8 SDK was not found.
    echo Install it from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

if not exist "AirPodsWidget.csproj" (
    echo ERROR: AirPodsWidget.csproj was not found in this folder.
    pause
    exit /b 1
)

if not exist "app.manifest" (
    echo ^<?xml version="1.0" encoding="utf-8"?^> > app.manifest
    echo ^<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1"^> >> app.manifest
    echo   ^<trustInfo xmlns="urn:schemas-microsoft-com:asm.v3"^> >> app.manifest
    echo     ^<security^>^<requestedPrivileges^> >> app.manifest
    echo       ^<requestedExecutionLevel level="asInvoker" uiAccess="false" /^> >> app.manifest
    echo     ^</requestedPrivileges^>^</security^> >> app.manifest
    echo   ^</trustInfo^> >> app.manifest
    echo ^</assembly^> >> app.manifest
)

echo Restoring packages...
dotnet restore "AirPodsWidget.csproj"
if errorlevel 1 goto :fail

echo.
echo Publishing win-x64...
dotnet publish "AirPodsWidget.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish"
if errorlevel 1 goto :fail

echo.
echo ========================================
echo BUILD SUCCESSFUL
echo Output: %CD%\publish\AirPodsWidget.exe
echo ========================================
pause
exit /b 0

:fail
echo.
echo ========================================
echo BUILD FAILED
 echo ========================================
pause
exit /b 1
