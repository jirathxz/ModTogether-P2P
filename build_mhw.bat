@echo off
echo =========================================================
echo   Building MHW Mod Manager EXE using .NET 8 (C#)...
echo =========================================================
cd /d "%~dp0\ModTogetherMHW"

echo.
echo [1/3] Restoring dependencies...
dotnet restore

echo.
echo [2/3] Building Portable / Standalone Edition (Includes .NET 8 Runtime ~85MB)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o "..\dist\Portable"

echo.
echo [3/3] Building Lightweight Edition (Requires .NET 8 Installed ~10MB)...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o "..\dist\Lightweight"

echo.
echo Copying executables to dist root folder...
if exist "..\dist\Portable\ModTogetherMHW.exe" copy /y "..\dist\Portable\ModTogetherMHW.exe" "..\dist\ModTogetherMHW_Standalone_x64.exe" >nul
if exist "..\dist\Lightweight\ModTogetherMHW.exe" copy /y "..\dist\Lightweight\ModTogetherMHW.exe" "..\dist\ModTogetherMHW_Lightweight_x64.exe" >nul

echo.
echo =========================================================
echo BUILD COMPLETE! Output files in 'dist':
echo   1. ModTogetherMHW_Standalone_x64.exe (~85MB - No .NET required)
echo   2. ModTogetherMHW_Lightweight_x64.exe (~10MB - Requires .NET 8)
echo =========================================================
pause
