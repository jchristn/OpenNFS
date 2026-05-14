[CmdletBinding()]
param(
    [string]$RepositoryRoot = "",
    [string]$ResultsDirectory = "artifacts\\release-validation",
    [switch]$IncludePack,
    [switch]$PlanOnly
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
}

if (-not (Test-Path $RepositoryRoot -PathType Container)) {
    throw "Repository root '$RepositoryRoot' does not exist."
}

$releaseChecklistPath = Join-Path $RepositoryRoot "docs\\release-checklist.md"
$skipScriptPath = Join-Path $PSScriptRoot "Assert-NoSkippedTests.ps1"
$checklistScriptPath = Join-Path $PSScriptRoot "Assert-ReleaseChecklist.ps1"
$conformanceArtifactsScriptPath = Join-Path $PSScriptRoot "Assert-ConformanceArtifacts.ps1"
$honestyScriptPath = Join-Path $PSScriptRoot "Assert-RepositoryHonesty.ps1"
$generateXdrScriptPath = Join-Path $RepositoryRoot "scripts\\Generate-Xdr.ps1"

$requiredWorkflowPaths = @(
    ".github/workflows/build.yaml",
    ".github/workflows/test.yaml",
    ".github/workflows/interop-hosted.yaml",
    ".github/workflows/interop-selfhosted.yaml",
    ".github/workflows/pynfs.yaml"
)

$requiredHarnessPaths = @(
    "scripts/interop/Invoke-PrivilegedInterop.ps1",
    "scripts/interop/pjdfstest/Invoke-Pjdfstest.ps1",
    "scripts/interop/connectathon/Invoke-Connectathon.ps1",
    "scripts/interop/pynfs/Invoke-Pynfs.ps1",
    "scripts/release/Assert-ConformanceArtifacts.ps1"
)

foreach ($relativePath in $requiredWorkflowPaths + $requiredHarnessPaths) {
    $fullPath = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path $fullPath -PathType Leaf)) {
        throw "Required release-validation artifact '$relativePath' is missing."
    }
}

& $checklistScriptPath -ChecklistPath $releaseChecklistPath
& $skipScriptPath -RepositoryRoot $RepositoryRoot
& $honestyScriptPath -RepositoryRoot $RepositoryRoot

$localCommands = @(
    "dotnet build src/OpenNFS.sln -c Release -m:1",
    "PowerShell ./scripts/Generate-Xdr.ps1 -Check",
    "PowerShell ./scripts/release/Assert-RepositoryHonesty.ps1",
    "PowerShell ./scripts/release/Assert-ConformanceArtifacts.ps1 -ResultsDirectory artifacts",
    "dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --no-build --framework net8.0 -- --results artifacts/touchstone-results.json",
    "dotnet test src/Test.Xunit/Test.Xunit.csproj -c Release --no-build",
    "dotnet test src/Test.Nunit/Test.Nunit.csproj -c Release --no-build"
)

if ($IncludePack) {
    $localCommands += "dotnet pack src/OpenNFS.Server/OpenNFS.Server.csproj -c Release"
    $localCommands += "dotnet pack src/OpenNFS.Client/OpenNFS.Client.csproj -c Release"
}

$externalPlans = @(
    "pwsh ./scripts/interop/Invoke-PrivilegedInterop.ps1 -PlanOnly -ResultsDirectory artifacts/interop-privileged",
    "pwsh ./scripts/interop/pjdfstest/Invoke-Pjdfstest.ps1 -PlanOnly -ProtocolVersion NfsV3 -ServerHost sample-host -ExportPath /export -MountPoint /mnt/opennfs -ResultsDirectory artifacts/pjdfstest",
    "pwsh ./scripts/interop/connectathon/Invoke-Connectathon.ps1 -PlanOnly -ProtocolVersion NfsV3 -ServerHost sample-host -ExportPath /export -MountPoint /mnt/opennfs -ResultsDirectory artifacts/connectathon",
    "pwsh ./scripts/interop/pynfs/Invoke-Pynfs.ps1 -MinorVersion 0 -ExportPath /exports/sample -ResultsDirectory artifacts/pynfs-v40 -SuiteRoot scripts/interop/pynfs/external -UseSampleServer",
    "pwsh ./scripts/interop/pynfs/Invoke-Pynfs.ps1 -PlanOnly -MinorVersion 1 -ExportPath /export -ResultsDirectory artifacts/pynfs-v41 -UseLinuxNfs41Server"
)

if ($PlanOnly) {
    Write-Host "Release validation plan for '$RepositoryRoot':"
    Write-Host "Checklist: $releaseChecklistPath"
    Write-Host "Results:   $ResultsDirectory"
    Write-Host "Local gates:"
    foreach ($command in $localCommands) {
        Write-Host " - $command"
    }

    Write-Host "External conformance harnesses:"
    foreach ($plan in $externalPlans) {
        Write-Host " - $plan"
    }

    return
}

& $conformanceArtifactsScriptPath -RepositoryRoot $RepositoryRoot -ResultsDirectory "artifacts"

New-Item -ItemType Directory -Force -Path (Join-Path $RepositoryRoot $ResultsDirectory) | Out-Null

Push-Location $RepositoryRoot
try {
    & dotnet build src/OpenNFS.sln -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

    & $generateXdrScriptPath -Check

    & dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --no-build --framework net8.0 -- --results artifacts/touchstone-results.json
    if ($LASTEXITCODE -ne 0) { throw "Touchstone automated runner failed." }

    & dotnet test src/Test.Xunit/Test.Xunit.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "xUnit adapter failed." }

    & dotnet test src/Test.Nunit/Test.Nunit.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "NUnit adapter failed." }

    if ($IncludePack) {
        & dotnet pack src/OpenNFS.Server/OpenNFS.Server.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw "OpenNFS.Server pack failed." }

        & dotnet pack src/OpenNFS.Client/OpenNFS.Client.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw "OpenNFS.Client pack failed." }
    }
}
finally {
    Pop-Location
}

Write-Host "Release validation passed."
