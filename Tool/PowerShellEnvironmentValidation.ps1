# =========================================================
# MITANZ360Edu PowerShell Environment Validation
# =========================================================

Clear-Host

Write-Host ""
Write-Host "====================================================="
Write-Host " MITANZ360Edu PowerShell Environment Check"
Write-Host "====================================================="
Write-Host ""

# =========================================================
# CHECK POWERSHELL VERSION
# =========================================================

Write-Host "Checking PowerShell Version..."
Write-Host ""

$PSVersion = $PSVersionTable.PSVersion

Write-Host "PowerShell Version:"
Write-Host $PSVersion

if ($PSVersion.Major -ge 7)
{
    Write-Host ""
    Write-Host "SUCCESS: PowerShell 7+ Detected"
}
else
{
    Write-Host ""
    Write-Host "ERROR: PowerShell 7 Required"
}

# =========================================================
# CHECK NUGET
# =========================================================

Write-Host ""
Write-Host "====================================================="
Write-Host " CHECKING NUGET"
Write-Host "====================================================="
Write-Host ""

$NuGet = Get-PackageProvider `
    -Name NuGet `
    -ErrorAction SilentlyContinue

if ($null -ne $NuGet)
{
    Write-Host "SUCCESS: NuGet Installed"

    Write-Host ""
    Write-Host "Version:"
    Write-Host $NuGet.Version
}
else
{
    Write-Host "ERROR: NuGet NOT Installed"

    Write-Host ""
    Write-Host "RUN:"
    Write-Host "Install-PackageProvider -Name NuGet -Force"
}

# =========================================================
# CHECK PSGALLERY
# =========================================================

Write-Host ""
Write-Host "====================================================="
Write-Host " CHECKING PSGALLERY"
Write-Host "====================================================="
Write-Host ""

$PSGallery = Get-PSRepository `
    -Name PSGallery `
    -ErrorAction SilentlyContinue

if ($null -ne $PSGallery)
{
    Write-Host "SUCCESS: PSGallery Found"

    Write-Host ""
    Write-Host "Installation Policy:"
    Write-Host $PSGallery.InstallationPolicy
}
else
{
    Write-Host "ERROR: PSGallery NOT Found"
}

# =========================================================
# CHECK PNP POWERSHELL
# =========================================================

Write-Host ""
Write-Host "====================================================="
Write-Host " CHECKING PNP POWERSHELL"
Write-Host "====================================================="
Write-Host ""

$PnPModule = Get-Module `
    PnP.PowerShell `
    -ListAvailable `
    -ErrorAction SilentlyContinue

if ($null -ne $PnPModule)
{
    Write-Host "SUCCESS: PnP.PowerShell Installed"

    Write-Host ""
    Write-Host "Installed Version:"
    Write-Host $PnPModule.Version
}
else
{
    Write-Host "ERROR: PnP.PowerShell NOT Installed"

    Write-Host ""
    Write-Host "RUN:"
    Write-Host "Install-Module PnP.PowerShell -Scope CurrentUser"
}

# =========================================================
# CHECK CONNECT-PNPONLINE
# =========================================================

Write-Host ""
Write-Host "====================================================="
Write-Host " CHECKING CONNECT-PNPONLINE"
Write-Host "====================================================="
Write-Host ""

$PnPCommand = Get-Command `
    Connect-PnPOnline `
    -ErrorAction SilentlyContinue

if ($null -ne $PnPCommand)
{
    Write-Host "SUCCESS: Connect-PnPOnline Available"
}
else
{
    Write-Host "ERROR: Connect-PnPOnline NOT Available"

    Write-Host ""
    Write-Host "TRY:"
    Write-Host "Import-Module PnP.PowerShell"
}

# =========================================================
# CHECK EXECUTION POLICY
# =========================================================

Write-Host ""
Write-Host "====================================================="
Write-Host " CHECKING EXECUTION POLICY"
Write-Host "====================================================="
Write-Host ""

$ExecutionPolicy = Get-ExecutionPolicy

Write-Host "Execution Policy:"
Write-Host $ExecutionPolicy

if (
    $ExecutionPolicy -eq "RemoteSigned" `
    -or
    $ExecutionPolicy -eq "Unrestricted"
)
{
    Write-Host ""
    Write-Host "SUCCESS: Script Execution Allowed"
}
else
{
    Write-Host ""
    Write-Host "WARNING: Script Execution May Be Blocked"

    Write-Host ""
    Write-Host "RUN:"
    Write-Host "Set-ExecutionPolicy RemoteSigned -Scope CurrentUser"
}

# =========================================================
# FINAL STATUS
# =========================================================

Write-Host ""
Write-Host "====================================================="
Write-Host " VALIDATION COMPLETE"
Write-Host "====================================================="
Write-Host ""

Write-Host "If ALL checks show SUCCESS,"
Write-Host "your environment is ready."
Write-Host ""
