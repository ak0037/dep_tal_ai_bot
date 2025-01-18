# deploy.ps1

# Force switch to Windows containers
Write-Host "Switching to Windows containers..."
& 'C:\Program Files\Docker\Docker\DockerCli.exe' -SwitchWindowsEngine

# Wait for the switch to complete
Start-Sleep -Seconds 10

# Verify Windows containers
$dockerInfo = docker info
if (-not ($dockerInfo | Select-String -Pattern "windows" -Quiet)) {
    Write-Error "Failed to switch to Windows containers"
    exit 1
}

Write-Host "Building and starting containers..."
docker-compose up -d --build

Write-Host "Verifying deployment..."
docker-compose ps