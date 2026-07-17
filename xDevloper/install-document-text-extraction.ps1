Write-Host "===============================================" -ForegroundColor Cyan
Write-Host " MITANZ360Edu - Document Text Extraction Setup" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# Ensure correct project directory
if (-not (Test-Path "*.csproj")) {
    Write-Error "❌ No .csproj found. Run this script from the Web project folder."
    exit 1
}

Write-Host "📦 Installing FREE document processing libraries..." -ForegroundColor Yellow

# PDF (Free, MIT)
dotnet add package UglyToad.PdfPig

# Word / PowerPoint (Microsoft-official)
dotnet add package DocumentFormat.OpenXml

# OCR (Free, Apache 2.0)
dotnet add package Tesseract

# Encoding support (required for PDFs on Windows)
dotnet add package System.Text.Encoding.CodePages

Write-Host ""
Write-Host "✅ NuGet packages installed successfully." -ForegroundColor Green

Write-Host ""
Write-Host "📌 IMPORTANT POST-INSTALL STEP" -ForegroundColor Magenta
Write-Host "Register code page provider at app startup:" -ForegroundColor Magenta
Write-Host ""
Write-Host '   System.Text.Encoding.RegisterProvider(' -ForegroundColor White
Write-Host '       System.Text.CodePagesEncodingProvider.Instance);' -ForegroundColor White

Write-Host ""
Write-Host "📁 For OCR, ensure tessdata folder exists:" -ForegroundColor Magenta
Write-Host "   ./tessdata/eng.traineddata" -ForegroundColor White

Write-Host ""
Write-Host "🚀 Ready for multi-format document extraction!" -ForegroundColor Cyan