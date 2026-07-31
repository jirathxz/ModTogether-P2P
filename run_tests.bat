@echo off
setlocal EnableDelayedExpansion
title ModTogether Test Runner

echo =====================================================================
echo                 ModTogether - Universal Mod Manager
echo                           TEST RUNNER
echo =====================================================================
echo.

:: 1. Run Unit Tests & Installers Verification
echo [*] [1/3] Running Integration and Unit Tests...
echo.
dotnet run --project "ModTogether.Tests\ModTogether.Tests.csproj"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: ModTogether.Tests failed!
    goto :error
)

:: 2. Run P2P / Network Simulation Tests
echo.
echo [*] [2/3] Running Simulation Tests (P2P, Network Drop, Large Files)...
echo.
dotnet run --project "ModTogether.Tests.Simulate\ModTogether.Tests.Simulate.csproj"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ERROR: ModTogether.Tests.Simulate failed!
    goto :error
)

:: 3. Run Automated UI Tests
echo.
echo [*] [3/3] Running Automated UI Tests (FlaUI)...
echo.
if exist "dist\Standalone\ModTogether_Universal_Standalone_x64.exe" (
    dotnet run --project "ModTogether.Tests.UI\ModTogether.Tests.UI.csproj"
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo [!] ERROR: ModTogether.Tests.UI failed!
        goto :error
    )
) else (
    echo [~] Skipping UI Tests because Standalone build was not found in dist folder.
    echo     (Please run build_universal.bat first to test the UI)
)

echo.
echo =====================================================================
echo   [SUCCESS] All tests passed successfully.
echo =====================================================================
exit /b 0

:error
echo.
echo =====================================================================
echo   [FAILED] Some tests did not pass. Check the logs above.
echo =====================================================================
exit /b 1
