[CmdletBinding()]
param(
    [ValidateSet(0, 1)]
    [int]$MinorVersion,
    [string]$ServerHost = "",
    [string]$ExportPath = "/exports/sample",
    [string]$ResultsDirectory,
    [string]$SuiteRoot = $env:PYNFS_ROOT,
    [string]$EntryPoint = "",
    [string[]]$EntryPointArguments = @(),
    [int]$ServerPort = 0,
    [string]$RepositoryRoot = "",
    [switch]$UseSampleServer,
    [switch]$PlanOnly
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "..\\InteropHarness.ps1")

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    throw "ResultsDirectory is required."
}

$RepositoryRoot = Resolve-InteropRepositoryRoot -RepositoryRoot $RepositoryRoot -ScriptRoot $PSScriptRoot
$ResultsDirectory = Ensure-InteropDirectory (Resolve-InteropPath -BasePath $RepositoryRoot -Path $ResultsDirectory)
$manifestPath = Join-Path $ResultsDirectory "pynfs-manifest.json"

$resolvedSuiteRoot = if ([string]::IsNullOrWhiteSpace($SuiteRoot)) { "" } else { [System.IO.Path]::GetFullPath($SuiteRoot) }
$suiteCommand = if ([string]::IsNullOrWhiteSpace($resolvedSuiteRoot) -or [string]::IsNullOrWhiteSpace($EntryPoint)) {
    "<entry-point-required>"
}
else {
    Resolve-PynfsCommand -SuiteRoot $resolvedSuiteRoot -EntryPoint $EntryPoint -EntryPointArguments $EntryPointArguments
}

if ($UseSampleServer) {
    $ServerHost = "host.docker.internal"
}

Write-Host "suite=pynfs"
Write-Host "minorVersion=$MinorVersion"
Write-Host "serverHost=$ServerHost"
Write-Host "exportPath=$ExportPath"
Write-Host "resultsDirectory=$ResultsDirectory"
Write-Host "suiteRoot=$resolvedSuiteRoot"
Write-Host "entryPoint=$EntryPoint"
Write-Host "command=$suiteCommand"

if ($PlanOnly) {
    return
}

if ([string]::IsNullOrWhiteSpace($resolvedSuiteRoot) -or -not (Test-Path $resolvedSuiteRoot -PathType Container)) {
    throw "PYNFS_ROOT or -SuiteRoot must point to a pynfs checkout before execution."
}

$sampleServer = $null
try {
    if ($UseSampleServer) {
        if ($MinorVersion -ne 0) {
            throw "The local sample-server bootstrap currently supports only NFSv4.0 for pynfs execution."
        }

        $sampleServer = Start-SampleInteropServer -RepositoryRoot $RepositoryRoot -ResultsDirectory $ResultsDirectory -ExportPath $ExportPath
        $ServerHost = "host.docker.internal"
        $ServerPort = $sampleServer.Nfs40Port
    }
    elseif ([string]::IsNullOrWhiteSpace($ServerHost) -or $ServerPort -le 0) {
        throw "ServerHost and ServerPort are required unless -UseSampleServer is specified."
    }

    $suiteCommand = Resolve-PynfsCommand -SuiteRoot $resolvedSuiteRoot -EntryPoint $EntryPoint -EntryPointArguments $EntryPointArguments
    $containerResult = Invoke-PynfsContainer `
        -RepositoryRoot $RepositoryRoot `
        -SuiteRoot $resolvedSuiteRoot `
        -ResultsDirectory $ResultsDirectory `
        -ServerHost $ServerHost `
        -ServerPort $ServerPort `
        -ExportPath $ExportPath `
        -MinorVersion $MinorVersion `
        -SuiteCommand $suiteCommand

    $stdoutLogPath = Join-Path $ResultsDirectory "stdout.log"
    $stderrLogPath = Join-Path $ResultsDirectory "stderr.log"
    $stdout = if (Test-Path $stdoutLogPath) { Get-Content -Raw -Path $stdoutLogPath } else { "" }
    $stderr = if (Test-Path $stderrLogPath) { Get-Content -Raw -Path $stderrLogPath } else { "" }

    Write-InteropManifest -Path $manifestPath -ManifestObject @{
        suite = "pynfs"
        minorVersion = $MinorVersion
        serverHost = $ServerHost
        serverPort = $ServerPort
        exportPath = $ExportPath
        suiteRoot = $resolvedSuiteRoot
        entryPoint = $EntryPoint
        entryPointArguments = $EntryPointArguments
        command = $suiteCommand
        useSampleServer = [bool]$UseSampleServer
        exitCode = $containerResult.ExitCode
        stdoutLogPath = $stdoutLogPath
        stderrLogPath = $stderrLogPath
        validatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    }

    if ($containerResult.ExitCode -ne 0) {
        throw "pynfs execution failed.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$stdout$([Environment]::NewLine)stderr:$([Environment]::NewLine)$stderr"
    }
}
finally {
    Stop-SampleInteropServer -ServerProcess $sampleServer
}
