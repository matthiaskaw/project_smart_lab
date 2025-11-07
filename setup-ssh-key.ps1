# Setup SSH Key Authentication for Raspberry Pi
# This script helps set up passwordless SSH authentication

$REMOTE_USER = "smartlab"
$REMOTE_HOST = "192.168.0.100"
$SSH_KEY_PATH = "$HOME\.ssh\id_rsa"

Write-Host "=== SSH Key Authentication Setup ===" -ForegroundColor Green
Write-Host ""

# Step 1: Check if SSH key already exists
if (Test-Path $SSH_KEY_PATH) {
    Write-Host "SSH key already exists at: $SSH_KEY_PATH" -ForegroundColor Yellow
    $response = Read-Host "Do you want to use the existing key? (y/n)"
    if ($response -ne "y") {
        Write-Host "Please backup your existing key and delete it, then run this script again." -ForegroundColor Red
        exit 1
    }
} else {
    # Step 2: Generate SSH key
    Write-Host "Generating new SSH key..." -ForegroundColor Yellow
    Write-Host "Press Enter when prompted for passphrase (or set one for extra security)" -ForegroundColor Cyan
    ssh-keygen -t rsa -b 4096 -f $SSH_KEY_PATH

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to generate SSH key" -ForegroundColor Red
        exit 1
    }
    Write-Host "SSH key generated successfully!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Step 2: Copying public key to Raspberry Pi..." -ForegroundColor Yellow
Write-Host "You will be prompted for the password of ${REMOTE_USER}@${REMOTE_HOST}" -ForegroundColor Cyan
Write-Host ""

# Step 3: Copy public key to Raspberry Pi
$publicKey = Get-Content "${SSH_KEY_PATH}.pub"

# Use ssh to add the key to authorized_keys
ssh "${REMOTE_USER}@${REMOTE_HOST}" @"
mkdir -p ~/.ssh
chmod 700 ~/.ssh
echo '$publicKey' >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
"@

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to copy key to Raspberry Pi" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative method:" -ForegroundColor Yellow
    Write-Host "1. Copy the following key:" -ForegroundColor Cyan
    Write-Host $publicKey -ForegroundColor White
    Write-Host ""
    Write-Host "2. Manually SSH to your Pi: ssh ${REMOTE_USER}@${REMOTE_HOST}" -ForegroundColor Cyan
    Write-Host "3. Run these commands:" -ForegroundColor Cyan
    Write-Host "   mkdir -p ~/.ssh" -ForegroundColor White
    Write-Host "   echo 'PASTE_KEY_HERE' >> ~/.ssh/authorized_keys" -ForegroundColor White
    Write-Host "   chmod 700 ~/.ssh" -ForegroundColor White
    Write-Host "   chmod 600 ~/.ssh/authorized_keys" -ForegroundColor White
    exit 1
}

Write-Host ""
Write-Host "Step 3: Testing SSH connection..." -ForegroundColor Yellow
ssh -o BatchMode=yes "${REMOTE_USER}@${REMOTE_HOST}" "echo 'SSH key authentication successful!'"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "=== Setup Complete ===" -ForegroundColor Green
    Write-Host "You can now SSH to your Raspberry Pi without a password!" -ForegroundColor Green
    Write-Host "Test with: ssh ${REMOTE_USER}@${REMOTE_HOST}" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "Warning: SSH connection test failed" -ForegroundColor Red
    Write-Host "You may need to check SSH server configuration on the Raspberry Pi" -ForegroundColor Yellow
}
