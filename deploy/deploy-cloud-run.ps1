# ==============================================================================
# QuizBattle - Google Cloud Run PowerShell Deployment Script
# ==============================================================================

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "    QuizBattle - Google Cloud Run Deployment Setup" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

if (-not (Get-Command gcloud -ErrorAction SilentlyContinue)) {
    Write-Error "'gcloud' CLI is not found in PATH. Please install Google Cloud SDK: https://cloud.google.com/sdk/docs/install or run in Google Cloud Shell."
    exit 1
}

$projectId = (gcloud config get-value project 2>$null)
if ([string]::IsNullOrWhiteSpace($projectId) -or $projectId -eq "(unset)") {
    $projectId = Read-Host "Enter your Google Cloud Project ID"
    gcloud config set project $projectId
} else {
    Write-Host "Using active GCP Project: $projectId" -ForegroundColor Yellow
    $inputProject = Read-Host "Press Enter to continue with '$projectId' or enter a different Project ID"
    if (-not [string]::IsNullOrWhiteSpace($inputProject)) {
        $projectId = $inputProject
        gcloud config set project $projectId
    }
}

$region = Read-Host "Enter GCP Region [default: us-central1]"
if ([string]::IsNullOrWhiteSpace($region)) { $region = "us-central1" }

$serviceName = "quizbattle"
$repoName = "quizbattle-repo"
$imageName = "quizbattle-server"
$imageTag = "$region-docker.pkg.dev/$projectId/$repoName/${imageName}:latest"

Write-Host "`nEnabling required Google Cloud APIs..." -ForegroundColor Cyan
gcloud services enable run.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com --project=$projectId

Write-Host "`nEnsuring Artifact Registry repository exists..." -ForegroundColor Cyan
$repoExists = gcloud artifacts repositories describe $repoName --location=$region --project=$projectId 2>$null
if (-not $repoExists) {
    Write-Host "Creating Artifact Registry repository '$repoName' in $region..."
    gcloud artifacts repositories create $repoName --repository-format=docker --location=$region --description="Docker repository for QuizBattle" --project=$projectId
}

$jwtSecret = "quizbattle_jwt_" + (Get-Random) + "_" + (Get-Random)

Write-Host "`nBuilding container image via Google Cloud Build..." -ForegroundColor Cyan
gcloud builds submit --tag $imageTag --project=$projectId .

Write-Host "`nDeploying to Google Cloud Run..." -ForegroundColor Cyan
gcloud run deploy $serviceName `
    --image $imageTag `
    --platform managed `
    --region $region `
    --allow-unauthenticated `
    --min-instances 1 `
    --max-instances 1 `
    --memory 512Mi `
    --cpu 1 `
    --timeout 3600s `
    --set-env-vars "MODE=wan,SERVER_NAME=Classroom QuizBattle,JWT_SECRET=$jwtSecret" `
    --project=$projectId

$serviceUrl = (gcloud run services describe $serviceName --platform managed --region $region --project $projectId --format 'value(status.url)')

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "    QuizBattle Successfully Deployed to Cloud Run!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host "Public Service URL: $serviceUrl" -ForegroundColor Yellow
Write-Host "`nTeacher Portal (Web Dashboard):"
Write-Host "   $serviceUrl/" -ForegroundColor Cyan
Write-Host "`nStudents Play in Browser (WebGL):"
Write-Host "   $serviceUrl/play" -ForegroundColor Cyan
Write-Host "`nMobile / Desktop Client Connection Endpoint:"
Write-Host "   Host: $($serviceUrl.Replace('https://', ''))" -ForegroundColor White
Write-Host "   Port: 443 (HTTPS/WSS)" -ForegroundColor White
Write-Host "========================================================" -ForegroundColor Green
