# ============================================
# MITANZ360EDU Full Source Backup
# ============================================

$SourceFolder = "D:\MITANZ360EDU-System\GitHub\mitanz360edu"
$BackupFolder = "D:\MITANZ360EDU-System\Dev-Backup"

# Create backup folder if missing
if (!(Test-Path $BackupFolder))
{
    New-Item -ItemType Directory -Path $BackupFolder -Force | Out-Null
}

# Format: 02Jun26-Full Backup.zip
$DateStamp = Get-Date -Format "ddMMMyy"
$BackupName = "$DateStamp-Full Backup.zip"

$ZipFile = Join-Path $BackupFolder $BackupName

# Remove existing backup with same name
if (Test-Path $ZipFile)
{
    Remove-Item $ZipFile -Force
}

# Temporary staging folder
$TempFolder = Join-Path $env:TEMP ("MITANZ360EDU_Backup_" + [guid]::NewGuid())

New-Item -ItemType Directory -Path $TempFolder -Force | Out-Null

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Creating MITANZ360EDU Backup..." -ForegroundColor Green
Write-Host "Source : $SourceFolder"
Write-Host "Target : $ZipFile"
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Copy source excluding build artifacts
robocopy `
    $SourceFolder `
    $TempFolder `
    /E `
    /XD `
        "bin" `
        "obj" `
        ".vs" `
        "node_modules" `
        "Dev-Backup" `
    /NFL /NDL /NJH /NJS /NC /NS | Out-Null

# Create ZIP
Compress-Archive `
    -Path "$TempFolder\*" `
    -DestinationPath $ZipFile `
    -CompressionLevel Optimal

# Cleanup
Remove-Item $TempFolder -Recurse -Force

Write-Host ""
Write-Host "Backup completed successfully." -ForegroundColor Green
Write-Host "File: $ZipFile" -ForegroundColor Yellow
Write-Host ""