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
    # Linux kernel client → OpenNFS server (NFSv3 mount/read/write).
    "InteropSuites.LinuxKernelClientMountsOpenNfsServer",
    # Linux kernel client → packaged sample artifact.
    "InteropSuites.LinuxKernelClientMountsSampleOpenNfsServerArtifact",
    # OpenNFS client → Linux kernel knfsd over NFSv3.
    "InteropSuites.OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServer",
    # OpenNFS client → Linux kernel knfsd over NFSv4.0.
    "InteropSuites.OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServerOverNfs40",
    # Reboot-like recovery: post-disconnect replay, NLM grace reclaim, NFSv4.0
    # lease-expiry recovery, and NFSv4.1 cached-reply replay across a fresh TCP.
    "ReplaySuites.DisconnectReplayRecovery",
    "NlmSuites.ReclaimAfterServerRestartPositive",
    "NfsV40Suites.ReclaimAfterLeaseRecovery",
    "NfsV41Suites.SessionReplayAfterReconnect",
    # Kerberos-sensitive scenarios. These run against the live MIT KDC fixture
    # under scripts/interop/kerberos/. They cover krb5 (auth-only), krb5i
    # (integrity), krb5p (privacy), context establishment, integrity-failure
    # rejection, and the server's mechanism wiring.
    "SecuritySuites.Krb5ReadWrite",
    "SecuritySuites.Krb5iDetectsTamper",
    "SecuritySuites.Krb5pEncryptsPayload",
    "SecuritySuites.RpcSecGssContextEstablishment",
    "SecuritySuites.RpcSecGssIntegrityFailureRejected",
    "SecuritySuites.ServerBuilderRegistersRpcSecGssMechanism"
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
