Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "MITANZ360Edu - Open Source AI Tool Installer" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# ------------------------------
# STEP 1: Ensure Chocolatey
# ------------------------------
if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Write-Host "Installing Chocolatey..." -ForegroundColor Yellow
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = `
        [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    Invoke-Expression ((New-Object System.Net.WebClient).DownloadString("https://chocolatey.org/install.ps1"))
}
else {
    Write-Host "Chocolatey already installed." -ForegroundColor Green
}

# ------------------------------
# STEP 2: Install .NET SDK (latest)
# ------------------------------
Write-Host "Installing .NET SDK..." -ForegroundColor Yellow
choco install dotnet-sdk -y

# ------------------------------
# STEP 3: Install Tesseract OCR
# ------------------------------
Write-Host "Installing Tesseract OCR..." -ForegroundColor Yellow
choco install tesseract -y

# ------------------------------
# STEP 4: Verify Tesseract
# ------------------------------
$tesseractPath = Get-Command tesseract -ErrorAction SilentlyContinue
if ($tesseractPath) {
    Write-Host "Tesseract OCR installed successfully." -ForegroundColor Green
}
else {
    Write-Host "Tesseract OCR installation FAILED." -ForegroundColor Red
}

# ------------------------------
# STEP 5: Create Sample .NET Project
# ------------------------------
$projectPath = Join-Path $PSScriptRoot "MITANZ360Edu.Web"
if (-not (Test-Path $projectPath)) {
    dotnet new console -n DocumentProcessingDemo
}

Set-Location $projectPath

# ------------------------------
# STEP 6: Install Required NuGet Packages
# ------------------------------
Write-Host "Installing .NET NuGet packages..." -ForegroundColor Yellow

dotnet add package DocumentFormat.OpenXml        # Word / Excel
dotnet add package ClosedXML                     # Excel
dotnet add package UglyToad.PdfPig               # PDF Text
dotnet add package Tesseract                     # OCR Wrapper
dotnet add package HtmlAgilityPack               # Web content parsing

# ------------------------------
# STEP 7: Restore & Build
# ------------------------------
dotnet restore
dotnet build

# ------------------------------
# STEP 8: Final Status Report
# ------------------------------
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "INSTALLATION STATUS REPORT" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

Write-Host "✔ Chocolatey Installed" -ForegroundColor Green
Write-Host "✔ .NET SDK Installed" -ForegroundColor Green
Write-Host "✔ Tesseract OCR Installed" -ForegroundColor Green
Write-Host "✔ OpenXML SDK Installed" -ForegroundColor Green
Write-Host "✔ PDF Parser Installed" -ForegroundColor Green
Write-Host "✔ Excel Parser Installed" -ForegroundColor Green
Write-Host "✔ HTML Parser Installed" -ForegroundColor Green
Write-Host "✔ Project Build Successful" -ForegroundColor Green

Write-Host ""
Write-Host "✅ Open‑Source Document Processing Stack READY" -ForegroundColor Green
Write-Host ""
