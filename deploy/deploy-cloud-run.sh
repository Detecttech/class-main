#!/usr/bin/env bash
set -e

# ==============================================================================
# QuizBattle - Google Cloud Run 1-Click Deployment Script
# ==============================================================================

echo "========================================================"
echo "    QuizBattle - Google Cloud Run Deployment Setup"
echo "========================================================"

# Check for gcloud CLI
if ! command -v gcloud &> /dev/null; then
    echo "Error: 'gcloud' CLI is not installed or not in PATH."
    echo "Please install the Google Cloud SDK or run this inside Google Cloud Shell:"
    echo "https://cloud.google.com/sdk/docs/install"
    exit 1
fi

# Detect or select GCP Project ID
PROJECT_ID=$(gcloud config get-value project 2>/dev/null || echo "")
if [ -z "$PROJECT_ID" ] || [ "$PROJECT_ID" = "(unset)" ]; then
    echo "No default GCP project found in gcloud config."
    read -rp "Enter your Google Cloud Project ID: " PROJECT_ID
    gcloud config set project "$PROJECT_ID"
else
    echo "Using active GCP Project: $PROJECT_ID"
    read -rp "Press Enter to continue with '$PROJECT_ID' or enter a different Project ID: " INPUT_PROJECT
    if [ -n "$INPUT_PROJECT" ]; then
        PROJECT_ID="$INPUT_PROJECT"
        gcloud config set project "$PROJECT_ID"
    fi
fi

# Region selection (defaults to us-central1)
REGION="${GCP_REGION:-us-central1}"
read -rp "Enter GCP Region [$REGION]: " INPUT_REGION
REGION="${INPUT_REGION:-$REGION}"

SERVICE_NAME="${SERVICE_NAME:-quizbattle}"
REPO_NAME="quizbattle-repo"
IMAGE_NAME="quizbattle-server"
IMAGE_TAG="$REGION-docker.pkg.dev/$PROJECT_ID/$REPO_NAME/$IMAGE_NAME:latest"

echo ""
echo "Enabling necessary Google Cloud APIs..."
gcloud services enable \
    run.googleapis.com \
    artifactregistry.googleapis.com \
    cloudbuild.googleapis.com \
    --project="$PROJECT_ID"

echo ""
echo "Ensuring Artifact Registry repository exists..."
if ! gcloud artifacts repositories describe "$REPO_NAME" --location="$REGION" --project="$PROJECT_ID" &>/dev/null; then
    echo "Creating Artifact Registry repository '$REPO_NAME' in $REGION..."
    gcloud artifacts repositories create "$REPO_NAME" \
        --repository-format=docker \
        --location="$REGION" \
        --description="Docker repository for QuizBattle" \
        --project="$PROJECT_ID"
fi

# Generate or reuse JWT Secret
if [ -z "$JWT_SECRET" ]; then
    JWT_SECRET=$(openssl rand -hex 24 2>/dev/null || head -c 24 /dev/urandom | xxd -p 2>/dev/null || echo "quizbattle_jwt_sec_$(date +%s)")
fi

echo ""
echo "Building container image using Google Cloud Build..."
gcloud builds submit --tag "$IMAGE_TAG" --project="$PROJECT_ID" .

echo ""
echo "Deploying container to Google Cloud Run..."
gcloud run deploy "$SERVICE_NAME" \
    --image "$IMAGE_TAG" \
    --platform managed \
    --region "$REGION" \
    --allow-unauthenticated \
    --min-instances 1 \
    --max-instances 1 \
    --memory 512Mi \
    --cpu 1 \
    --timeout 3600s \
    --set-env-vars "MODE=wan,SERVER_NAME=Classroom QuizBattle,JWT_SECRET=$JWT_SECRET" \
    --project "$PROJECT_ID"

SERVICE_URL=$(gcloud run services describe "$SERVICE_NAME" --platform managed --region "$REGION" --project "$PROJECT_ID" --format 'value(status.url)')

echo ""
echo "========================================================"
echo "    QuizBattle Successfully Deployed to Cloud Run!"
echo "========================================================"
echo "Public Service URL: $SERVICE_URL"
echo ""
echo "Teacher Portal (Web Dashboard):"
echo "   $SERVICE_URL/"
echo ""
echo "Students Play in Browser (WebGL):"
echo "   $SERVICE_URL/play"
echo ""
echo "Mobile / Desktop Client Connection Endpoint:"
echo "   Host: ${SERVICE_URL#https://}"
echo "   Port: 443 (HTTPS/WSS)"
echo "========================================================"
