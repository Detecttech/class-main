@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo     QuizBattle - Google Cloud Run Deployment Setup
echo ========================================================

where gcloud >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo Error: 'gcloud' CLI is not found in PATH.
    echo Please install the Google Cloud SDK from https://cloud.google.com/sdk/docs/install
    echo or run the deployment directly in Google Cloud Shell.
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('gcloud config get-value project 2^>nul') do set PROJECT_ID=%%i
if "%PROJECT_ID%"=="" (
    set /p PROJECT_ID="Enter your Google Cloud Project ID: "
    gcloud config set project "!PROJECT_ID!"
) else (
    echo Using active GCP Project: !PROJECT_ID!
    set /p INPUT_PROJECT="Press Enter to continue with '!PROJECT_ID!' or enter a different Project ID: "
    if not "!INPUT_PROJECT!"=="" (
        set PROJECT_ID=!INPUT_PROJECT!
        gcloud config set project "!PROJECT_ID!"
    )
)

set REGION=us-central1
set /p INPUT_REGION="Enter GCP Region [default: us-central1]: "
if not "!INPUT_REGION!"=="" set REGION=!INPUT_REGION!

set SERVICE_NAME=quizbattle
set REPO_NAME=quizbattle-repo
set IMAGE_NAME=quizbattle-server
set IMAGE_TAG=%REGION%-docker.pkg.dev/%PROJECT_ID%/%REPO_NAME%/%IMAGE_NAME%:latest

echo.
echo Enabling required Google Cloud APIs...
call gcloud services enable run.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com --project="%PROJECT_ID%"

echo.
echo Ensuring Artifact Registry repository exists...
call gcloud artifacts repositories describe "%REPO_NAME%" --location="%REGION%" --project="%PROJECT_ID%" >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo Creating Artifact Registry repository '%REPO_NAME%' in %REGION%...
    call gcloud artifacts repositories create "%REPO_NAME%" --repository-format=docker --location="%REGION%" --description="Docker repository for QuizBattle" --project="%PROJECT_ID%"
)

set JWT_SECRET=quizbattle_jwt_%RANDOM%_%RANDOM%_%RANDOM%

echo.
echo Building container image via Google Cloud Build...
call gcloud builds submit --tag "%IMAGE_TAG%" --project="%PROJECT_ID%" .

echo.
echo Deploying to Google Cloud Run...
call gcloud run deploy "%SERVICE_NAME%" --image "%IMAGE_TAG%" --platform managed --region "%REGION%" --allow-unauthenticated --min-instances 1 --max-instances 1 --memory 512Mi --cpu 1 --timeout 3600s --set-env-vars "MODE=wan,SERVER_NAME=Classroom QuizBattle,JWT_SECRET=%JWT_SECRET%" --project="%PROJECT_ID%"

for /f "tokens=*" %%i in ('gcloud run services describe "%SERVICE_NAME%" --platform managed --region "%REGION%" --project "%PROJECT_ID%" --format="value(status.url)"') do set SERVICE_URL=%%i

echo.
echo ========================================================
echo     QuizBattle Successfully Deployed to Cloud Run!
echo ========================================================
echo Public Service URL: %SERVICE_URL%
echo.
echo Teacher Portal (Web Dashboard):
echo    %SERVICE_URL%/
echo.
echo Students Play in Browser (WebGL):
echo    %SERVICE_URL%/play
echo ========================================================

endlocal
