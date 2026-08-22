# ==============================================================================
# Package QuizBattle for Cloud Deployment (Creates lightweight zip)
# ==============================================================================

$outputZip = "d:\game\quizbattle-cloud.zip"
$tempDir = "d:\game\.cloud-package-temp"

Write-Host "Creating lightweight Cloud Deployment package..." -ForegroundColor Cyan

if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
if (Test-Path $outputZip) { Remove-Item -Force $outputZip }

New-Item -ItemType Directory -Path $tempDir | Out-Null

# 1. Copy Docker & Cloud Build files
Copy-Item "d:\game\Dockerfile" "$tempDir\"
Copy-Item "d:\game\.dockerignore" "$tempDir\"
Copy-Item "d:\game\cloudbuild.yaml" "$tempDir\"

# 2. Copy deploy scripts
Copy-Item -Recurse "d:\game\deploy" "$tempDir\deploy"

# 3. Copy shared assets
Copy-Item -Recurse "d:\game\shared" "$tempDir\shared"

# 4. Copy game-client/webgl-build (pre-compiled game)
New-Item -ItemType Directory -Path "$tempDir\game-client" | Out-Null
if (Test-Path "d:\game\game-client\webgl-build") {
    Copy-Item -Recurse "d:\game\game-client\webgl-build" "$tempDir\game-client\webgl-build"
}

# 5. Copy server (excluding node_modules and .db files)
New-Item -ItemType Directory -Path "$tempDir\server" | Out-Null
Copy-Item -Recurse "d:\game\server\src" "$tempDir\server\src"
Copy-Item "d:\game\server\package.json" "$tempDir\server\"
Copy-Item "d:\game\server\package-lock.json" "$tempDir\server\"
Copy-Item "d:\game\server\tsconfig.json" "$tempDir\server\"

# 6. Copy server/web-portal (excluding node_modules)
New-Item -ItemType Directory -Path "$tempDir\server\web-portal" | Out-Null
Copy-Item -Recurse "d:\game\server\web-portal\src" "$tempDir\server\web-portal\src"
Copy-Item -Recurse "d:\game\server\web-portal\public" "$tempDir\server\web-portal\public"
Copy-Item "d:\game\server\web-portal\index.html" "$tempDir\server\web-portal\"
Copy-Item "d:\game\server\web-portal\package.json" "$tempDir\server\web-portal\"
Copy-Item "d:\game\server\web-portal\package-lock.json" "$tempDir\server\web-portal\"
Copy-Item "d:\game\server\web-portal\tsconfig*.json" "$tempDir\server\web-portal\"
Copy-Item "d:\game\server\web-portal\vite.config.ts" "$tempDir\server\web-portal\"

# Create the zip archive
Compress-Archive -Path "$tempDir\*" -DestinationPath $outputZip -CompressionLevel Optimal

# Cleanup temp folder
Remove-Item -Recurse -Force $tempDir

$zipSize = (Get-Item $outputZip).Length / 1MB
Write-Host "`nPackage created successfully!" -ForegroundColor Green
Write-Host "File: $outputZip" -ForegroundColor Yellow
Write-Host "Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host "Ready to upload to Google Cloud Shell in seconds!`n" -ForegroundColor Cyan
