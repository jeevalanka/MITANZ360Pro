# =========================================================
# MITANZ360Edu – LMS Content Library Provisioning
# APP-ONLY | CERTIFICATE AUTH | A1 EDUCATION SAFE
# =========================================================

Clear-Host

# -----------------------------
# CONFIGURATION
# -----------------------------

$TenantId   = "9079c019-45c8-498d-b758-c75201acf33c"
$ClientId   = "ded710dd-1bb7-42b9-88f0-92b91459aa5f"
$SiteUrl    = "https://mitga.sharepoint.com/sites/MITANZ360Edu"

$LibraryName = "LMS_Content_Library"
$SchemaCsv   = ".\LMS_Content_Library_Schema.csv"

# -----------------------------
# CERTIFICATE LOOKUP
# -----------------------------

$Cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -like "*MITANZ360Edu-AppOnly*" } |
        Select-Object -First 1

if (-not $Cert) {
    Write-Host "[FATAL] App-only certificate not found in CurrentUser\My" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Certificate found:" $Cert.Subject

# -----------------------------
# CONNECT (APP-ONLY)
# -----------------------------

Write-Host "============================================================"
Write-Host " CONNECTING TO SHAREPOINT (APP-ONLY)"
Write-Host "============================================================"

try {
    Connect-PnPOnline `
        -Url $SiteUrl `
        -Tenant $TenantId `
        -ClientId $ClientId `
        -Thumbprint $Cert.Thumbprint

    Write-Host "[SUCCESS] App-only connection established" -ForegroundColor Green
}
catch {
    Write-Host "[FATAL] App-only authentication failed" -ForegroundColor Red
    Write-Host $_.Exception.Message
    exit 1
}

# -----------------------------
# LIBRARY PROVISIONING
# -----------------------------

Write-Host ""
Write-Host "============================================================"
Write-Host " LIBRARY VALIDATION"
Write-Host "============================================================"

$List = Get-PnPList -Identity $LibraryName -ErrorAction SilentlyContinue

if (-not $List) {
    Write-Host "[CREATE] Creating library $LibraryName"
    New-PnPList -Title $LibraryName -Template DocumentLibrary
    Write-Host "[SUCCESS] Library created" -ForegroundColor Green
}
else {
    Write-Host "[OK] Library already exists"
}

# -----------------------------
# LOAD SCHEMA CSV
# -----------------------------

Write-Host ""
Write-Host "============================================================"
Write-Host " SCHEMA PROVISIONING"
Write-Host "============================================================"

if (-not (Test-Path $SchemaCsv)) {
    Write-Host "[FATAL] Schema CSV not found: $SchemaCsv" -ForegroundColor Red
    exit 1
}

$Fields = Import-Csv $SchemaCsv

foreach ($Field in $Fields) {

    Write-Host ""
    Write-Host "[FIELD]" $Field.DisplayName

    $Existing = Get-PnPField -List $LibraryName |
                Where-Object { $_.InternalName -eq $Field.InternalName }

    if ($Existing) {
        Write-Host "  - Already exists (skipped)"
        continue
    }

    $Required = ($Field.Required -eq "Yes")
    $Indexed  = ($Field.Indexed  -eq "Yes")

    $Params = @{
        List                = $LibraryName
        DisplayName         = $Field.DisplayName
        InternalName        = $Field.InternalName
        Required            = $Required
        AddToDefaultView    = $true
    }

    switch ($Field.Type) {

        "Text"     { Add-PnPField @Params -Type Text }
        "Note"     { Add-PnPField @Params -Type Note }
        "Number"   { Add-PnPField @Params -Type Number }
        "Boolean"  { Add-PnPField @Params -Type Boolean }
        "DateTime" { Add-PnPField @Params -Type DateTime }

        "Choice" {
            $Choices = $Field.Choices -split "\|"
            Add-PnPField @Params -Type Choice -Choices $Choices
        }

        default {
            Write-Host "  - Unsupported type:" $Field.Type -ForegroundColor Yellow
            continue
        }
    }

    if ($Indexed) {
        Set-PnPField `
            -List $LibraryName `
            -Identity $Field.InternalName `
            -Values @{ Indexed = $true }
    }

    Write-Host "  - Created successfully" -ForegroundColor Green
}

# -----------------------------
# COMPLETE
# -----------------------------

Write-Host ""
Write-Host "============================================================"
Write-Host " PROVISIONING COMPLETE"
Write-Host "============================================================"
Write-Host ""
Write-Host "[SUCCESS] LMS Content Library ready for use"