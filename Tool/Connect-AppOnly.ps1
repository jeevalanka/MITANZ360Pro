Write-Host "=== CONNECT TEST START ==="

$ErrorActionPreference = "Stop"

Import-Module PnP.PowerShell -Force

try {
    Write-Host "Connecting with app-only certificate..."

    Connect-PnPOnline -Url "https://mitga.sharepoint.com/sites/MITANZ360Edu" -Tenant "9079c019-45c8-498d-b758-c75201acf33c" -ClientId "ded710dd-1bb7-42b9-88f0-92b91459aa5f" -Thumbprint "69C8ECD75D6228853A11CCFFD2E1315F48DF0363" -Scopes "https://mitga.sharepoint.com/.default"

    Write-Host "✅ CONNECT OK"
}
catch {
    Write-Host "❌ CONNECT FAILED"
    Write-Host $_.Exception.Message
    exit 1
}

try {
    Write-Host "=== LIST TEST ==="
    Get-PnPList | Select Title
    Write-Host "✅ Get-PnPList OK"
}
catch {
    Write-Host "❌ Get-PnPList FAILED"
    Write-Host $_.Exception.Message
    exit 2
}

Write-Host "=== CONNECT TEST COMPLETE ==="