[CmdletBinding()]
param(
    [string]$RepositoryRoot = "",
    [string]$ResultsDirectory = "artifacts",
    [int]$MaxArtifactAgeDays = 14
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if (-not (Test-Path $RepositoryRoot -PathType Container)) {
    throw "Repository root '$RepositoryRoot' does not exist."
}

if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) {
    $resolvedResultsDirectory = $ResultsDirectory
}
else {
    $resolvedResultsDirectory = Join-Path $RepositoryRoot $ResultsDirectory
}

$minimumValidatedAtUtc = [DateTimeOffset]::UtcNow.AddDays(-1 * $MaxArtifactAgeDays)
$requiredArtifacts = @(
    @{
        Suite = "pjdfstest"
        ManifestPath = Join-Path $resolvedResultsDirectory "pjdfstest\pjdfstest-manifest.json"
    },
    @{
        Suite = "connectathon"
        ManifestPath = Join-Path $resolvedResultsDirectory "connectathon\connectathon-manifest.json"
    },
    @{
        Suite = "pynfs"
        ManifestPath = Join-Path $resolvedResultsDirectory "pynfs\pynfs-manifest.json"
    }
)

foreach ($artifact in $requiredArtifacts) {
    $suite = $artifact.Suite
    $manifestPath = $artifact.ManifestPath

    if (-not (Test-Path $manifestPath -PathType Leaf)) {
        throw "Required conformance artifact for '$suite' is missing at '$manifestPath'."
    }

    $manifest = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
    if ($manifest.suite -ne $suite) {
        throw "Conformance artifact '$manifestPath' has suite '$($manifest.suite)' instead of '$suite'."
    }

    if ($manifest.exitCode -ne 0) {
        throw "Conformance artifact '$manifestPath' did not pass cleanly. exitCode=$($manifest.exitCode)"
    }

    if ([string]::IsNullOrWhiteSpace($manifest.validatedAtUtc)) {
        throw "Conformance artifact '$manifestPath' does not include validatedAtUtc."
    }

    $validatedAtUtc = [DateTimeOffset]::Parse($manifest.validatedAtUtc, [System.Globalization.CultureInfo]::InvariantCulture)
    if ($validatedAtUtc -lt $minimumValidatedAtUtc) {
        throw "Conformance artifact '$manifestPath' is older than $MaxArtifactAgeDays days. validatedAtUtc=$($manifest.validatedAtUtc)"
    }

    if ([string]::IsNullOrWhiteSpace($manifest.suiteRoot)) {
        throw "Conformance artifact '$manifestPath' does not identify the external suite root."
    }

    if ($manifest.suiteRoot.IndexOf("conformance-smoke", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Conformance artifact '$manifestPath' points at a synthetic smoke root, not a real external suite root."
    }
}

Write-Host "Conformance artifact validation passed for '$resolvedResultsDirectory'."
