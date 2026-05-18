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
$releaseChecklistPath = Join-Path $RepositoryRoot "docs\\release-checklist.md"

foreach ($requiredPath in @($readmePath, $releaseChecklistPath)) {
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
    "OpenNFS is an ALPHA repository.",
    "subject to change without notice.",
    "Only very limited compatibility testing has been done relative to the eventual support bar.",
    "No protocol version is yet claimed as supported on a release branch.",
    "Out of scope:",
    "OpenNFS does not implement pNFS. There is no plan to add it; this is an explicit non-goal, not a deferral.",
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

$releaseChecklist = Get-Content -Path $releaseChecklistPath -Raw
$requiredChecklistTokens = @(
    "Use this checklist before any branch claims that a protocol version or security mode is supported.",
    "No placeholder implementation, placeholder test gate, or skipped conformance requirement remains for any claimed feature."
)

$missingChecklistTokens = New-Object System.Collections.Generic.List[string]
foreach ($token in $requiredChecklistTokens) {
    if ($releaseChecklist.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        $missingChecklistTokens.Add($token)
    }
}

if ($missingChecklistTokens.Count -gt 0) {
    Write-Host "Release checklist honesty validation is missing required gate language:" -ForegroundColor Red
    foreach ($token in $missingChecklistTokens) {
        Write-Host " - $token"
    }

    throw "Repository honesty validation failed."
}

Write-Host "Repository honesty validation passed for '$RepositoryRoot'."
