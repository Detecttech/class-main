# Hosting QuizBattle on Google Cloud Platform (GCP)

This guide walks you through hosting the complete **QuizBattle** stack on Google Cloud.

QuizBattle's single container / server process hosts everything in one place:
- **Teacher Web Portal** (React SPA) at `https://<your-host>/`
- **Student WebGL Browser Game** at `https://<your-host>/play`
- **Real-Time Multiplayer WebSocket Engine** at `wss://<your-host>/ws`
- **REST APIs & SQLite Database** at `https://<your-host>/api/*`

---

## Deployment Options at a Glance

| Feature | Option 1: Google Cloud Run (Recommended) | Option 2: Google Compute Engine (VM) |
|---|---|---|
| **Architecture** | Serverless Container | Dedicated Linux VM (`e2-micro` / `e2-small`) |
| **Cost** | Free tier (2M req/mo), pennies/month for typical classes | Always-Free eligible in `us-central1` |
| **HTTPS / WSS** | **Automatic** Google-managed SSL (`*.run.app` & custom domains) | Automated Let's Encrypt via Caddy reverse proxy |
| **WebSocket Support** | Built-in (up to 60 min continuous session timeout) | Persistent native connection |
| **Maintenance** | **Zero** (no OS updates, automatic health checks) | Managed via PM2 + systemd |

---

## Option 1: Deploying to Google Cloud Run (Recommended)

Google Cloud Run runs the QuizBattle multi-stage container with automatic HTTPS and WebSocket streaming.

### Method A: In 1-Click via Google Cloud Shell (No local installation needed)

1. Open [Google Cloud Console](https://console.cloud.google.com/).
2. Click the **Activate Cloud Shell** button (top right icon `>_`).
3. Clone or upload your repository to Cloud Shell:
   ```bash
   git clone <your-repo-url> quizbattle
   cd quizbattle
   ```
4. Run the automated deployment script:
   ```bash
   chmod +x deploy/deploy-cloud-run.sh
   ./deploy/deploy-cloud-run.sh
   ```
5. Follow the prompt to choose your project and region (e.g. `us-central1`).
6. When complete, the script outputs your public HTTPS URL (e.g., `https://quizbattle-xxxx-uc.a.run.app`).

---

### Method B: From Your Local Machine (Windows / Mac / Linux)

#### Prerequisites
- Install the [Google Cloud SDK (gcloud CLI)](https://cloud.google.com/sdk/docs/install).
- Authenticate with your Google account:
  ```bash
  gcloud auth login
  ```

#### On Windows (PowerShell or Command Prompt)
```powershell
# PowerShell
.\deploy\deploy-cloud-run.ps1

# Or Command Prompt (.bat)
deploy\deploy-cloud-run.bat
```

#### On Linux / macOS
```bash
chmod +x deploy/deploy-cloud-run.sh
./deploy/deploy-cloud-run.sh
```

---

### Manual `gcloud` Command Reference for Cloud Run

If you prefer running raw commands:

```bash
# 1. Enable GCP Services
gcloud services enable run.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com

# 2. Build & Submit Container to Artifact Registry
gcloud builds submit --tag us-central1-docker.pkg.dev/$PROJECT_ID/quizbattle-repo/quizbattle-server:latest .

# 3. Deploy to Cloud Run
gcloud run deploy quizbattle \
  --image us-central1-docker.pkg.dev/$PROJECT_ID/quizbattle-repo/quizbattle-server:latest \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated \
  --min-instances 1 \
  --max-instances 1 \
  --memory 512Mi \
  --cpu 1 \
  --timeout 3600s \
  --set-env-vars "MODE=wan,SERVER_NAME=Classroom QuizBattle,JWT_SECRET=your-secret-here"
```

---

## Option 2: Deploying to Google Compute Engine (Always-Free VM)

If you prefer a classic virtual machine with dedicated persistent disk storage:

### Automated VM Provisioning & Setup

#### On Linux / Cloud Shell / macOS:
```bash
chmod +x deploy/deploy-gce-vm.sh
./deploy/deploy-gce-vm.sh
```

#### On Windows:
```cmd
deploy\deploy-gce-vm.bat
```

This script automatically:
1. Provisions an `e2-micro` Debian 12 VM.
2. Opens firewall ports `80`, `443`, and `7777`.
3. Runs the startup script to install Node.js 20, Caddy, PM2, and builds the application.
4. Starts QuizBattle under PM2 with automatic reboot persistence.
5. Configures Caddy to terminate TLS and reverse proxy WebSocket/HTTP traffic.

---

## How Players and Teachers Connect

Once deployed, you will receive a public URL or IP address (e.g. `https://quizbattle-abc123-uc.a.run.app` or `http://34.120.45.67`).

### 1. Teacher Management Portal
Open your browser and navigate to:
```
https://<your-cloud-url>/
```
- Create classes, manage rosters, create custom question banks, start multiplayer live matches, and view student progress.

### 2. Browser Play (Unity WebGL)
Students can play directly in any desktop or mobile browser without installing anything:
```
https://<your-cloud-url>/play
```
- The WebGL client automatically detects the cloud host and connects to the server over secure WebSocket (`wss://`).

### 3. Native Client Play (Windows / Android APK)
Students on the standalone desktop or mobile build:
1. Open the QuizBattle app.
2. On the **Connect** screen, enter:
   - **Server IP / Domain**: `<your-cloud-url-without-https>` (e.g. `quizbattle-abc123-uc.a.run.app` or `34.120.45.67`)
   - **Port**: `443` (for HTTPS/Cloud Run) or `80` / `7777` (for Compute Engine).
3. Tap **Connect**!

---

## Environment Variables Reference

Configure these in Cloud Run (via `--set-env-vars`) or on your VM:

| Variable | Default | Description |
|---|---|---|
| `PORT` | `7777` (Cloud Run sets `8080`) | HTTP + WebSocket listening port |
| `MODE` | `wan` | Set to `wan` for internet hosting |
| `JWT_SECRET` | *(Random string)* | Signs teacher & student authentication tokens |
| `SERVER_NAME` | `Classroom QuizBattle` | Name displayed on `/api/server/info` |
| `DB_PATH` | `/app/server/data/quizbattle.db` | Path to SQLite database file |

---

## Updating and Redeploying

When you make changes to questions, game code, or the web portal:

1. **Rebuild the WebGL client (optional, if client changed)**:
   ```cmd
   build-browser.bat
   ```
2. **Redeploy to Cloud Run**:
   ```bash
   ./deploy/deploy-cloud-run.sh
   ```
   Cloud Run handles zero-downtime rolling deployment automatically!
