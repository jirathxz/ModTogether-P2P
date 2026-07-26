@echo off
setlocal EnableDelayedExpansion
title ModTogether Universal Build Tool

echo.
echo =====================================================================
echo                 ModTogether - Universal Mod Manager
echo                           BUILD SYSTEM
echo =====================================================================
echo.

cd /d "%~dp0\ModTogetherUniversal"

:: Step 0: Ensure dist folder exists and kill running instances
echo [*] Cleaning dist folder and closing running instances and build servers...
taskkill /F /FI "IMAGENAME eq ModTogether*" /T >nul 2>&1
taskkill /F /IM ModTogether_Universal_Standalone_x64.exe /T >nul 2>&1
taskkill /F /IM ModTogether_Universal_Lightweight_x64.exe /T >nul 2>&1
taskkill /F /IM ModTogetherUniversal.exe /T >nul 2>&1
dotnet build-server shutdown >nul 2>&1

if exist "*_wpftmp.csproj" del /Q "*_wpftmp.csproj" >nul 2>&1
if exist "..\dist" rmdir /s /q "..\dist" >nul 2>&1
mkdir "..\dist"

:: Step 1: Restore
echo.
echo [*] [1/3] Restoring project dependencies...
dotnet restore "ModTogetherUniversal.csproj"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: Dependency restore failed!
    goto :error
)
echo [+] Restore completed successfully.

:: Step 2: Build Lightweight
echo.
echo [*] [2/3] Building Lightweight Edition (Requires .NET 8, ~10MB)...
echo     Running dotnet publish...
dotnet publish "ModTogetherUniversal.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false -p:DebugType=none -p:DebugSymbols=false -o "..\dist\Lightweight" -v m
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: Lightweight build failed!
    goto :error
)
echo [+] Lightweight build completed successfully.

:: Step 3: Build Standalone
echo.
echo [*] [3/3] Building Standalone Edition (No .NET required, ~85MB)...
echo     Running dotnet publish...
dotnet publish "ModTogetherUniversal.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false -p:DebugType=none -p:DebugSymbols=false -o "..\dist\Standalone" -v m
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: Standalone build failed!
    goto :error
)
echo [+] Standalone build completed successfully.

:: Step 4: Finalize
echo.
echo [*] Finalizing build and organizing output...
ping 127.0.0.1 -n 2 > nul

echo Building ModTogether Plugins...
cd ..
dotnet build "ModTogether.Plugins.MHW\ModTogether.Plugins.MHW.csproj" -c Release
dotnet build "ModTogether.Plugins.MHWilds\ModTogether.Plugins.MHWilds.csproj" -c Release

:: Create output subfolders
if not exist "dist\Standalone\Plugins" mkdir "dist\Standalone\Plugins"
if not exist "dist\Lightweight\Plugins" mkdir "dist\Lightweight\Plugins"

:: Copy Plugin DLLs
copy /Y "ModTogether.Plugins.MHW\bin\Release\net8.0-windows\ModTogether.Plugins.MHW.dll" "dist\Standalone\Plugins\" >nul
copy /Y "ModTogether.Plugins.MHW\bin\Release\net8.0-windows\ModTogether.Plugins.MHW.dll" "dist\Lightweight\Plugins\" >nul
copy /Y "ModTogether.Plugins.MHWilds\bin\Release\net8.0-windows\ModTogether.Plugins.MHWilds.dll" "dist\Standalone\Plugins\" >nul
copy /Y "ModTogether.Plugins.MHWilds\bin\Release\net8.0-windows\ModTogether.Plugins.MHWilds.dll" "dist\Lightweight\Plugins\" >nul

cd "ModTogetherUniversal"

:: Rename Standalone executable
if exist "..\dist\Standalone\ModTogetherUniversal.exe" (
    move /y "..\dist\Standalone\ModTogetherUniversal.exe" "..\dist\Standalone\ModTogether_Universal_Standalone_x64.exe" >nul
)

:: Rename Lightweight executable
if exist "..\dist\Lightweight\ModTogetherUniversal.exe" (
    move /y "..\dist\Lightweight\ModTogetherUniversal.exe" "..\dist\Lightweight\ModTogether_Universal_Lightweight_x64.exe" >nul
)

:: Copy Plugins assets to both folders
xcopy /E /I /Y "Plugins" "..\dist\Standalone\Plugins" >nul
xcopy /E /I /Y "Plugins" "..\dist\Lightweight\Plugins" >nul
if exist "..\dist\Standalone\Plugins\ModTogether.API.dll" del /Q "..\dist\Standalone\Plugins\ModTogether.API.dll"
if exist "..\dist\Lightweight\Plugins\ModTogether.API.dll" del /Q "..\dist\Lightweight\Plugins\ModTogether.API.dll"


echo.
echo =====================================================================
echo   [SUCCESS] Build finished without errors.
echo =====================================================================
echo   Output files organized into 'dist' folder:
echo    - Standalone:  dist\Standalone\ModTogether_Universal_Standalone_x64.exe
echo    - Lightweight: dist\Lightweight\ModTogether_Universal_Lightweight_x64.exe
echo =====================================================================
echo.
pause
exit /b 0

:error
echo.
echo =====================================================================
echo   [FAILED] Build process was aborted due to errors.
echo =====================================================================
echo.
pause
exit /b 1
