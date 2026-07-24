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

:: Step 0: Ensure dist folder exists
if not exist "..\dist" mkdir "..\dist"

:: Step 1: Restore
echo.
echo [*] [1/3] Restoring project dependencies...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: Dependency restore failed!
    goto :error
)
echo [+] Restore completed successfully.

:: Step 2: Build Standalone
echo.
echo [*] [2/3] Building Standalone Edition (No .NET required, ~85MB)...
echo     Running dotnet publish...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=none -p:DebugSymbols=false -o "..\dist\Portable" -v m
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: Standalone build failed!
    goto :error
)
echo [+] Standalone build completed successfully.

:: Step 3: Build Lightweight
echo.
echo [*] [3/3] Building Lightweight Edition (Requires .NET 8, ~10MB)...
echo     Running dotnet publish...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=none -p:DebugSymbols=false -o "..\dist\Lightweight" -v m
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: Lightweight build failed!
    goto :error
)
echo [+] Lightweight build completed successfully.

:: Step 4: Finalize
echo.
echo [*] Finalizing build and organizing output...
ping 127.0.0.1 -n 2 > nul

echo Building ModTogether.Extensions.MHW...
cd ..
dotnet build "ModTogether.Extensions.MHW\ModTogether.Extensions.MHW.csproj" -c Release
if not exist "dist\Extensions" mkdir "dist\Extensions"
copy /Y "ModTogether.Extensions.MHW\bin\Release\net8.0-windows\ModTogether.Extensions.MHW.dll" "dist\Extensions\"
cd "ModTogetherUniversal"

if exist "..\dist\Portable\ModTogetherUniversal.exe" (
    move /y "..\dist\Portable\ModTogetherUniversal.exe" "..\dist\ModTogether_Universal_Standalone_x64.exe" >nul
    move /y "..\dist\Portable\*.*" "..\dist\" >nul 2>&1
)
if exist "..\dist\Lightweight\ModTogetherUniversal.exe" (
    move /y "..\dist\Lightweight\ModTogetherUniversal.exe" "..\dist\ModTogether_Universal_Lightweight_x64.exe" >nul
    move /y "..\dist\Lightweight\*.*" "..\dist\" >nul 2>&1
)

:: Cleanup intermediate folders
if exist "..\dist\Portable" rmdir /s /q "..\dist\Portable"
if exist "..\dist\Lightweight" rmdir /s /q "..\dist\Lightweight"

:: Copy Extensions folder
if not exist "..\dist\Extensions" mkdir "..\dist\Extensions"
xcopy /E /I /Y "Extensions" "..\dist\Extensions" >nul
if exist "..\dist\Extensions\ModTogether.API.dll" del /Q "..\dist\Extensions\ModTogether.API.dll"

echo.
echo =====================================================================
echo   [SUCCESS] Build finished without errors.
echo =====================================================================
echo   Output files located in 'dist' folder:
echo    - ModTogether_Universal_Standalone_x64.exe
echo    - ModTogether_Universal_Lightweight_x64.exe
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
