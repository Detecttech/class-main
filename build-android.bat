@echo off
setlocal enabledelayedexpansion

set UNITY_EXE=D:\Program Files\Editor\6000.5.7f1\Editor\Unity.exe
set PROJECT_PATH=D:\game\game-client
set LOG_FILE=D:\game\unity-android-build.log
set APK_PATH=D:\temp\android-build-check\quizbattle.apk
set ADB_EXE=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe

echo === QuizBattle Android build ===

echo Closing any running Unity Editor instance (required - it locks the project)...
taskkill /IM Unity.exe /F >nul 2>&1

echo Building... (this can take several minutes)
"%UNITY_EXE%" -batchmode -projectPath "%PROJECT_PATH%" -buildTarget Android -executeMethod QuizBattle.EditorTools.Graphics.AndroidBuildCheck.Run -quit -logFile "%LOG_FILE%"

findstr /C:"[AndroidBuildCheck] result=Succeeded" "%LOG_FILE%" >nul
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED - see %LOG_FILE% for details.
    findstr /C:"error CS" "%LOG_FILE%"
    exit /b 1
)

echo.
echo BUILD SUCCEEDED
echo APK: %APK_PATH%
echo.

if not exist "%ADB_EXE%" (
    echo adb not found at %ADB_EXE% - install manually.
    exit /b 0
)

set /p INSTALL="Install to a connected device now? (y/n): "
if /I "%INSTALL%"=="y" (
    "%ADB_EXE%" install -r "%APK_PATH%"
)

endlocal
