[CmdletBinding()]
param(
    [string]$ChecklistPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ChecklistPath)) {
    $ChecklistPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path "docs\\release-checklist.md"
}

if (-not (Test-Path $ChecklistPath -PathType Leaf)) {
    throw "Release checklist '$ChecklistPath' does not exist."
}

$content = Get-Content -Path $ChecklistPath -Raw
$requiredTokens = @(
    "dotnet build",
    "Generate-Xdr.ps1 -Check",
    "Test.Automated",
    "Test.Xunit",
    "Test.Nunit",
    "Assert-RepositoryHonesty.ps1",
    "pjdfstest",
    "Connectathon",
    "pynfs",
    "Linux mount",
    "knfsd",
    "nfs-ganesha",
    "Sample.OpenNfsServer"
)

$missing = New-Object System.Collections.Generic.List[string]
foreach ($token in $requiredTokens) {
    if ($content.IndexOf($token, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
        $missing.Add($token)
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Release checklist is missing required support-gate references:" -ForegroundColor Red
    foreach ($token in $missing) {
        Write-Host " - $token"
    }

    throw "Release checklist validation failed."
}

Write-Host "Release checklist validation passed for '$ChecklistPath'."
