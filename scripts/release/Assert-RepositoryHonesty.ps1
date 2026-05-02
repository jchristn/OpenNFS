[CmdletBinding()]
param(
    [string]$RepositoryRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
}

if (-not (Test-Path $RepositoryRoot -PathType Container)) {
    throw "Repository root '$RepositoryRoot' does not exist."
}

$skipScriptPath = Join-Path $PSScriptRoot "Assert-NoSkippedTests.ps1"
$checklistScriptPath = Join-Path $PSScriptRoot "Assert-ReleaseChecklist.ps1"
$readmePath = Join-Path $RepositoryRoot "README.md"
$openNfsPlanPath = Join-Path $RepositoryRoot "OPENNFS.md"
$releaseChecklistPath = Join-Path $RepositoryRoot "docs\\release-checklist.md"

foreach ($requiredPath in @($readmePath, $openNfsPlanPath)) {
    if (-not (Test-Path $requiredPath -PathType Leaf)) {
        throw "Required repository-honesty artifact '$requiredPath' does not exist."
    }
}

& $skipScriptPath -RepositoryRoot $RepositoryRoot
& $checklistScriptPath -ChecklistPath $releaseChecklistPath

$searchRoots = @(
    (Join-Path $RepositoryRoot "src"),
    (Join-Path $RepositoryRoot "scripts")
) | Where-Object { Test-Path $_ -PathType Container }

$allowedExtensions = @(".cs", ".csproj", ".ps1", ".psm1", ".psd1", ".targets", ".props", ".json")
$bannedPatterns = @(
    @{ Label = "TODO"; Pattern = '\bTODO\b' },
    @{ Label = "NotImplementedException"; Pattern = '\bNotImplementedException\b' },
    @{ Label = "not implemented yet"; Pattern = 'not implemented yet' },
    @{ Label = "placeholder protocol handler"; Pattern = 'placeholder protocol handler' },
    @{ Label = "fake success path"; Pattern = 'fake success' }
)

$violations = New-Object System.Collections.Generic.List[string]
foreach ($searchRoot in $searchRoots) {
    Get-ChildItem -Path $searchRoot -Recurse -File | Where-Object {
        $extension = [System.IO.Path]::GetExtension($_.FullName)
        # Normalize on '/' so the same exclusion rules apply on Windows and Linux runners. Native
        # PowerShell on Windows hands back '\'-separated paths; native PowerShell on Linux hands
        # back '/'-separated paths. The validator must behave identically on both.
        $relativeToRepo = $_.FullName.Substring($RepositoryRoot.Length).TrimStart('\').TrimStart('/').Replace('\', '/')
        $allowedExtensions -contains $extension `
            -and $relativeToRepo -notmatch '(^|/)(bin|obj|Generated)/' `
            -and $relativeToRepo -notmatch '^src/Test\.' `
            -and $relativeToRepo -ne 'scripts/release/Assert-RepositoryHonesty.ps1'
    } | ForEach-Object {
        $relativePath = Resolve-Path -Relative $_.FullName
        foreach ($pattern in $bannedPatterns) {
            $matches = Select-String -Path $_.FullName -Pattern $pattern.Pattern -SimpleMatch:$false
            foreach ($match in $matches) {
                $violations.Add($pattern.Label + " in " + $relativePath + ":" + $match.LineNumber + " -> " + $match.Line.Trim())
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Repository honesty validation found banned placeholder markers:" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host " - $violation"
    }

    throw "Repository honesty validation failed."
}

$readme = Get-Content -Path $readmePath -Raw
$requiredReadmeTokens = @(
    "No protocol version is yet claimed as supported on a release branch.",
    "Broader conformance, CI-hosted interop, wider Linux peer matrices, and release-gate work are still open, so this is not yet a release-support claim.",
    "Deferred from the first supported release:",
    "pNFS advertisement, layout protocols, and data-server flows",
    "RDMA transport"
)

$missingReadmeTokens = New-Object System.Collections.Generic.List[string]
foreach ($token in $requiredReadmeTokens) {
    if ($readme.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        $missingReadmeTokens.Add($token)
    }
}

if ($missingReadmeTokens.Count -gt 0) {
    Write-Host "README honesty validation is missing required disclaimers:" -ForegroundColor Red
    foreach ($token in $missingReadmeTokens) {
        Write-Host " - $token"
    }

    throw "Repository honesty validation failed."
}

$openNfsPlan = Get-Content -Path $openNfsPlanPath -Raw
if ($openNfsPlan.IndexOf("Task: Confirm there is no placeholder code, no version overclaim, and no missing mandatory test gate.", [System.StringComparison]::Ordinal) -lt 0) {
    throw "OPENNFS.md no longer contains the tracked repository-honesty definition-of-done item."
}

Write-Host "Repository honesty validation passed for '$RepositoryRoot'."
