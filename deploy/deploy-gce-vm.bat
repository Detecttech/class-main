@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo     QuizBattle - Google Compute Engine VM Setup
echo ========================================================

where gcloud >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo Error: 'gcloud' CLI is not found in PATH.
    echo Please install Google Cloud SDK or run from Google Cloud Shell.
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('gcloud config get-value project 2^>nul') do set PROJECT_ID=%%i
if "%PROJECT_ID%"=="" (
    set /p PROJECT_ID="Enter your Google Cloud Project ID: "
    gcloud config set project "!PROJECT_ID!"
) else (
    echo Using active GCP Project: !PROJECT_ID!
)

set ZONE=us-central1-a
set /p INPUT_ZONE="Enter GCP Zone [default: us-central1-a]: "
if not "!INPUT_ZONE!"=="" set ZONE=!INPUT_ZONE!

set VM_NAME=quizbattle-vm
set MACHINE_TYPE=e2-micro

echo.
echo Enabling Compute Engine API...
call gcloud services enable compute.googleapis.com --project="%PROJECT_ID%"

echo.
echo Configuring firewall rules...
call gcloud compute firewall-rules describe allow-quizbattle --project="%PROJECT_ID%" >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    call gcloud compute firewall-rules create allow-quizbattle --direction=INGRESS --priority=1000 --network=default --action=ALLOW --rules=tcp:80,tcp:443,tcp:7777 --source-ranges=0.0.0.0/0 --target-tags=quizbattle-server --project="%PROJECT_ID%"
)

echo.
echo Creating Compute Engine VM (%VM_NAME%)...
call gcloud compute instances describe "%VM_NAME%" --zone="%ZONE%" --project="%PROJECT_ID%" >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    call gcloud compute instances create "%VM_NAME%" --zone="%ZONE%" --machine-type="%MACHINE_TYPE%" --tags=quizbattle-server,http-server,https-server --image-family=debian-12 --image-project=debian-cloud --boot-disk-size=20GB --boot-disk-type=pd-standard --metadata-from-file startup-script=deploy/gce-startup-script.sh --project="%PROJECT_ID%"
)

for /f "tokens=*" %%i in ('gcloud compute instances describe "%VM_NAME%" --zone="%ZONE%" --project="%PROJECT_ID%" --format="get(networkInterfaces[0].accessConfigs[0].natIP)"') do set VM_IP=%%i

echo.
echo ========================================================
echo     QuizBattle VM Ready on Google Compute Engine!
echo ========================================================
echo VM External IP: %VM_IP%
echo.
echo Teacher Portal (Web Dashboard):
echo    http://%VM_IP%/
echo.
echo Students Play in Browser (WebGL):
echo    http://%VM_IP%/play
echo ========================================================

endlocal
