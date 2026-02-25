# Deploy SmartLab to Raspberry Pi
# This script deploys the published application to a Raspberry Pi

$REMOTE_USER = "smartlab"
$REMOTE_HOST = "192.168.237.150"
$REMOTE_PATH = "~"
$APP_NAME = "smartlab_sqlite"
$LOCAL_PUBLISH_DIR = "./bin/publish_wsl"

Write-Host "Deploying SmartLab to Raspberry Pi..." -ForegroundColor Green

# Step 1: Stop running instance and remove old versions
Write-Host "Stopping running instance and cleaning up old version..." -ForegroundColor Yellow
ssh -i "c:\Users\matth\.ssh\smartlab-wsl" "${REMOTE_USER}@${REMOTE_HOST}" "pkill -f ${APP_NAME} 2>/dev/null || true; sleep 2; rm -rf ~/${APP_NAME} ~/temp_publish 2>/dev/null || true"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Warning: Could not connect or cleanup on remote host" -ForegroundColor Yellow
}

Write-Host "Waiting for processes to terminate..." -ForegroundColor Yellow
Start-Sleep -Seconds 1

# Step 2: Copy published files to Raspberry Pi
Write-Host "Copying files to Raspberry Pi..." -ForegroundColor Yellow
scp -i "c:\Users\matth\.ssh\smartlab-wsl" -r "${LOCAL_PUBLISH_DIR}" "${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_PATH}/temp_publish"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to copy files" -ForegroundColor Red
    exit 1
}

# Step 3: Rename and set permissions
Write-Host "Renaming and setting permissions..." -ForegroundColor Yellow
ssh -i "c:\Users\matth\.ssh\smartlab-wsl" "${REMOTE_USER}@${REMOTE_HOST}" "rm -rf ~/${APP_NAME} && mv ~/temp_publish ~/${APP_NAME} && chmod +x ~/${APP_NAME}/* 2>/dev/null || true && find ~/${APP_NAME} -type f -name 'smartlab' -exec chmod +x {} \;"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to rename or set permissions" -ForegroundColor Red
    exit 1
}

Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host "Application deployed to: ${REMOTE_PATH}/${APP_NAME}" -ForegroundColor Cyan

# Step 4: Execute startup script
Write-Host ""
Write-Host "Executing startup.sh..." -ForegroundColor Yellow
ssh -i "c:\Users\matth\.ssh\smartlab-wsl" "${REMOTE_USER}@${REMOTE_HOST}" "cd ~ && bash ~/startup.sh"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Startup script executed successfully!" -ForegroundColor Green
} else {
    Write-Host "Warning: Startup script execution failed or returned non-zero exit code" -ForegroundColor Yellow
}
