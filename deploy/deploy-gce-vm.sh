#!/usr/bin/env bash
set -e

# ==============================================================================
# QuizBattle - Google Compute Engine (Always-Free VM) Deployment Script
# ==============================================================================

echo "========================================================"
echo "    QuizBattle - Google Compute Engine VM Setup"
echo "========================================================"

if ! command -v gcloud &> /dev/null; then
    echo "Error: 'gcloud' CLI is not installed or not in PATH."
    echo "Install Google Cloud SDK from https://cloud.google.com/sdk/docs/install"
    exit 1
fi

PROJECT_ID=$(gcloud config get-value project 2>/dev/null || echo "")
if [ -z "$PROJECT_ID" ] || [ "$PROJECT_ID" = "(unset)" ]; then
    read -rp "Enter your Google Cloud Project ID: " PROJECT_ID
    gcloud config set project "$PROJECT_ID"
else
    echo "Using active GCP Project: $PROJECT_ID"
fi

ZONE="${GCP_ZONE:-us-central1-a}"
read -rp "Enter GCP Zone [$ZONE]: " INPUT_ZONE
ZONE="${INPUT_ZONE:-$ZONE}"

VM_NAME="${VM_NAME:-quizbattle-vm}"
MACHINE_TYPE="e2-micro" # Always-Free tier eligible in us-central1 / us-west1 / us-east1

echo ""
echo "Enabling Compute Engine API..."
gcloud services enable compute.googleapis.com --project="$PROJECT_ID"

echo ""
echo "Configuring firewall rules for HTTP, HTTPS, and Game Ports..."
if ! gcloud compute firewall-rules describe allow-quizbattle --project="$PROJECT_ID" &>/dev/null; then
    gcloud compute firewall-rules create allow-quizbattle \
        --direction=INGRESS \
        --priority=1000 \
        --network=default \
        --action=ALLOW \
        --rules=tcp:80,tcp:443,tcp:7777 \
        --source-ranges=0.0.0.0/0 \
        --target-tags=quizbattle-server \
        --project="$PROJECT_ID"
fi

echo ""
echo "Creating Compute Engine VM ($VM_NAME - $MACHINE_TYPE)..."
if ! gcloud compute instances describe "$VM_NAME" --zone="$ZONE" --project="$PROJECT_ID" &>/dev/null; then
    gcloud compute instances create "$VM_NAME" \
        --zone="$ZONE" \
        --machine-type="$MACHINE_TYPE" \
        --tags=quizbattle-server,http-server,https-server \
        --image-family=debian-12 \
        --image-project=debian-cloud \
        --boot-disk-size=20GB \
        --boot-disk-type=pd-standard \
        --metadata-from-file startup-script=deploy/gce-startup-script.sh \
        --project="$PROJECT_ID"
fi

VM_IP=$(gcloud compute instances describe "$VM_NAME" --zone="$ZONE" --project="$PROJECT_ID" --format='get(networkInterfaces[0].accessConfigs[0].natIP)')

echo ""
echo "Copying repository code to VM..."
gcloud compute scp --recurse . "${VM_NAME}:/tmp/quizbattle-build" --zone="$ZONE" --project="$PROJECT_ID"

echo "Deploying application on VM..."
gcloud compute ssh "$VM_NAME" --zone="$ZONE" --project="$PROJECT_ID" --command="
    sudo mkdir -p /opt/quizbattle && \
    sudo cp -r /tmp/quizbattle-build/* /opt/quizbattle/ && \
    cd /opt/quizbattle/server/web-portal && sudo npm install && sudo npm run build && \
    cd /opt/quizbattle/server && sudo npm install && sudo npm run build && \
    sudo pm2 restart quizbattle || sudo pm2 start dist/index.js --name quizbattle --cwd /opt/quizbattle/server
"

echo ""
echo "========================================================"
echo "    QuizBattle Successfully Deployed to Compute Engine!"
echo "========================================================"
echo "VM External IP: $VM_IP"
echo ""
echo "Teacher Portal (Web Dashboard):"
echo "   http://$VM_IP/"
echo ""
echo "Students Play in Browser (WebGL):"
echo "   http://$VM_IP/play"
echo ""
echo "Mobile / Desktop App Endpoint:"
echo "   Host: $VM_IP"
echo "   Port: 80 (or 7777)"
echo "========================================================"
