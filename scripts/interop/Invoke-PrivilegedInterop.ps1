[CmdletBinding()]
param(
    [string]$RepositoryRoot = "",
    [string]$ResultsDirectory = "artifacts\\interop-privileged",
    [string]$TouchstoneResultsPath = "",
    [switch]$PlanOnly
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "InteropHarness.ps1")

$RepositoryRoot = Resolve-InteropRepositoryRoot -RepositoryRoot $RepositoryRoot -ScriptRoot $PSScriptRoot
$ResultsDirectory = Ensure-InteropDirectory (Resolve-InteropPath -BasePath $RepositoryRoot -Path $ResultsDirectory)
if ([string]::IsNullOrWhiteSpace($TouchstoneResultsPath)) {
    $TouchstoneResultsPath = Join-Path $ResultsDirectory "touchstone-results.json"
}
else {
    $TouchstoneResultsPath = Resolve-InteropPath -BasePath $RepositoryRoot -Path $TouchstoneResultsPath
}

$requiredCases = @(
    "InteropSuites.LinuxKernelClientMountsOpenNfsServer",
    "InteropSuites.LinuxKernelClientMountsSampleOpenNfsServerArtifact",
    "InteropSuites.OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServer",
    "InteropSuites.OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServerOverNfs40",
    "ReplaySuites.DisconnectReplayRecovery",
    "NlmSuites.ReclaimAfterServerRestartPositive",
    "NfsV40Suites.ReclaimAfterLeaseRecovery"
)

$command = "dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --no-build --framework net8.0 -- --results $TouchstoneResultsPath"
$manifestPath = Join-Path $ResultsDirectory "privileged-interop-manifest.json"

if ($PlanOnly) {
    Write-Host "suite=privileged-interop"
    Write-Host "repositoryRoot=$RepositoryRoot"
    Write-Host "resultsDirectory=$ResultsDirectory"
    Write-Host "touchstoneResultsPath=$TouchstoneResultsPath"
    Write-Host "command=$command"
    Write-Host "requiredCases=$($requiredCases -join ',')"
    return
}

Push-Location $RepositoryRoot
try {
    if (-not (Test-Path $TouchstoneResultsPath -PathType Leaf)) {
        $runner = Invoke-InteropProcess `
            -FilePath "dotnet" `
            -Arguments @("run", "--project", "src/Test.Automated/Test.Automated.csproj", "-c", "Release", "--no-build", "--", "--results", $TouchstoneResultsPath) `
            -WorkingDirectory $RepositoryRoot `
            -TimeoutSeconds 1800

        if ($runner.ExitCode -ne 0) {
            throw "Privileged interop runner failed.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$($runner.StandardOutput)$([Environment]::NewLine)stderr:$([Environment]::NewLine)$($runner.StandardError)"
        }
    }

    if (-not (Test-Path $TouchstoneResultsPath -PathType Leaf)) {
        throw "The privileged interop results file '$TouchstoneResultsPath' was not created."
    }

    $results = Get-Content -Raw -Path $TouchstoneResultsPath | ConvertFrom-Json
    $indexedResults = @{}
    foreach ($result in $results) {
        $indexedResults[$result.testId] = $result
    }

    $validatedCases = @()
    foreach ($caseId in $requiredCases) {
        if (-not $indexedResults.ContainsKey($caseId)) {
            throw "The privileged interop results did not contain the required case '$caseId'."
        }

        $caseResult = $indexedResults[$caseId]
        if (-not $caseResult.success -or $caseResult.skipped) {
            throw "The privileged interop case '$caseId' did not pass cleanly."
        }

        $validatedCases += [pscustomobject]@{
            testId     = $caseResult.testId
            durationMs = $caseResult.durationMs
            success    = $caseResult.success
            skipped    = $caseResult.skipped
        }
    }

    Write-InteropManifest -Path $manifestPath -ManifestObject @{
        suite = "privileged-interop"
        validatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        resultsDirectory = $ResultsDirectory
        touchstoneResultsPath = $TouchstoneResultsPath
        command = $command
        requiredCases = $requiredCases
        validatedCases = $validatedCases
        totalResults = @($results).Count
    }
}
finally {
    Pop-Location
}
