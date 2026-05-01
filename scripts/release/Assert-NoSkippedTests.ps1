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

$files = Get-ChildItem -Path $RepositoryRoot -Recurse -File -Include *.cs,*.csx |
    Where-Object {
        $_.FullName -notmatch "[\\/](bin|obj|artifacts|\.git)[\\/]"
    }

$suiteFiles = $files | Where-Object { $_.Name -like "*Suites.cs" }

$findings = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    foreach ($pattern in @(
            @{ Name = "xUnit or NUnit Skip property"; Regex = "\bSkip\s*=" },
            @{ Name = "NUnit Ignore attribute"; Regex = "\[Ignore(?:\(|\])" },
            @{ Name = "NUnit Explicit attribute"; Regex = "\[Explicit(?:\(|\])" })) {
        $matches = Select-String -Path $file.FullName -Pattern $pattern.Regex -AllMatches
        foreach ($match in $matches) {
            $findings.Add(("{0}:{1}: {2}: {3}" -f $file.FullName, $match.LineNumber, $pattern.Name, $match.Line.Trim()))
        }
    }
}

foreach ($file in $suiteFiles) {
    $matches = Select-String -Path $file.FullName -Pattern 'skipReason\s*:\s*"' -AllMatches
    foreach ($match in $matches) {
        $findings.Add(("{0}:{1}: Static Touchstone skipReason: {2}" -f $file.FullName, $match.LineNumber, $match.Line.Trim()))
    }
}

if ($findings.Count -gt 0) {
    Write-Host "Explicit skipped-test markers are not allowed in this repository:" -ForegroundColor Red
    foreach ($finding in $findings) {
        Write-Host " - $finding"
    }

    throw "Skipped-test validation failed."
}

Write-Host "Skipped-test validation passed for '$RepositoryRoot'."
