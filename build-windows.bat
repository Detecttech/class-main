@echo off
setlocal enabledelayedexpansion

set UNITY_EXE=D:\Program Files\Editor\6000.5.7f1\Editor\Unity.exe
set PROJECT_PATH=D:\game\game-client
set LOG_FILE=D:\game\unity-windows-build.log
set BUILD_PATH=D:\temp\quizbattle-build-test\QuizBattle.exe

echo === QuizBattle Windows build ===

echo Closing any running Unity Editor instance (required - it locks the project)...
taskkill /IM Unity.exe /F >nul 2>&1

echo Building... (this can take several minutes)
"%UNITY_EXE%" -batchmode -projectPath "%PROJECT_PATH%" -buildWindows64Player "%BUILD_PATH%" -quit -logFile "%LOG_FILE%"

findstr /C:"error CS" "%LOG_FILE%" >nul
if %ERRORLEVEL% EQU 0 (
    echo.
    echo BUILD FAILED - compile errors found:
    findstr /C:"error CS" "%LOG_FILE%"
    exit /b 1
)

if not exist "%BUILD_PATH%" (
    echo.
    echo BUILD FAILED - no executable produced. See %LOG_FILE% for details.
    exit /b 1
)

echo.
echo BUILD SUCCEEDED
echo Executable: %BUILD_PATH%
echo.

set /p RUNIT="Launch it now? (y/n): "
if /I "%RUNIT%"=="y" (
    start "" "%BUILD_PATH%"
)

endlocal
