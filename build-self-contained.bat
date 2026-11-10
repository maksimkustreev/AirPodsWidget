@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul || (echo ERROR: .NET 8 SDK not found.& pause & exit /b 1)
dotnet publish AirPodsWidget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-self-contained
if errorlevel 1 (echo BUILD FAILED& pause& exit /b 1)
echo BUILD SUCCESSFUL: publish-self-contained\AirPodsWidget.exe
pause
