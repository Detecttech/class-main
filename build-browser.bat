@echo off
setlocal enabledelayedexpansion

set UNITY_EXE=D:\Program Files\Editor\6000.5.7f1\Editor\Unity.exe
set PROJECT_PATH=D:\game\game-client
set LOG_FILE=D:\game\unity-webgl-build.log
set OUTPUT_DIR=D:\game\game-client\webgl-build

echo === QuizBattle browser (WebGL) build ===

echo Closing any running Unity Editor instance (required - it locks the project)...
taskkill /IM Unity.exe /F >nul 2>&1

echo Building... (this can take several minutes)
"%UNITY_EXE%" -batchmode -projectPath "%PROJECT_PATH%" -buildTarget WebGL -executeMethod QuizBattle.EditorTools.Graphics.WebGLBuildCheck.Run -quit -logFile "%LOG_FILE%"

findstr /C:"[WebGLBuildCheck] result=Succeeded" "%LOG_FILE%" >nul
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED - see %LOG_FILE% for details.
    findstr /C:"error CS" "%LOG_FILE%"
    exit /b 1
)

echo.
echo BUILD SUCCEEDED
echo Output: %OUTPUT_DIR%
echo.
echo This is served at http://localhost:7777/play/ once the server is running.
echo Start the server separately with:
echo     cd D:\game\server
echo     npm run dev

endlocal
