[CmdletBinding()]
param(
    [ValidateSet("NfsV3", "NfsV40")]
    [string]$ProtocolVersion,
    [string]$ServerHost = "",
    [string]$ExportPath = "/exports/sample",
    [string]$MountPoint = "/mnt/opennfs",
    [string]$ResultsDirectory,
    [string]$SuiteRoot = $env:CONNECTATHON_ROOT,
    [string]$Subset = "general",
    [int]$MountPort = 0,
    [int]$NfsPort = 0,
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
$manifestPath = Join-Path $ResultsDirectory "connectathon-manifest.json"

$resolvedSuiteRoot = if ([string]::IsNullOrWhiteSpace($SuiteRoot)) { "" } else { [System.IO.Path]::GetFullPath($SuiteRoot) }
$suiteCommand = if ([string]::IsNullOrWhiteSpace($resolvedSuiteRoot)) { "<suite-root-required>" } else { Resolve-CthonSuiteCommand -SuiteRoot $resolvedSuiteRoot -Subset $Subset }

if ($UseSampleServer) {
    $ServerHost = "host.docker.internal"
}

Write-Host "suite=connectathon"
Write-Host "protocol=$ProtocolVersion"
Write-Host "serverHost=$ServerHost"
Write-Host "exportPath=$ExportPath"
Write-Host "mountPoint=$MountPoint"
Write-Host "subset=$Subset"
Write-Host "resultsDirectory=$ResultsDirectory"
Write-Host "suiteRoot=$resolvedSuiteRoot"
Write-Host "command=mount + prove"

if ($PlanOnly) {
    return
}

if ([string]::IsNullOrWhiteSpace($resolvedSuiteRoot) -or -not (Test-Path $resolvedSuiteRoot -PathType Container)) {
    throw "CONNECTATHON_ROOT or -SuiteRoot must point to a Connectathon checkout before execution."
}

$sampleServer = $null
try {
    if ($UseSampleServer) {
        $sampleServer = Start-SampleInteropServer -RepositoryRoot $RepositoryRoot -ResultsDirectory $ResultsDirectory -ExportPath $ExportPath
        $ServerHost = "host.docker.internal"
        $MountPort = $sampleServer.MountPort
        $NfsPort = $sampleServer.NfsPort
    }
    elseif ([string]::IsNullOrWhiteSpace($ServerHost) -or $MountPort -le 0 -or $NfsPort -le 0) {
        throw "ServerHost, MountPort, and NfsPort are required unless -UseSampleServer is specified."
    }

    $suiteCommand = Resolve-CthonSuiteCommand -SuiteRoot $resolvedSuiteRoot -Subset $Subset
    $containerResult = Invoke-MountedSuiteContainer `
        -RepositoryRoot $RepositoryRoot `
        -ProtocolVersion $ProtocolVersion `
        -SuiteRoot $resolvedSuiteRoot `
        -ResultsDirectory $ResultsDirectory `
        -ServerHost $ServerHost `
        -MountPort $MountPort `
        -NfsPort $NfsPort `
        -ExportPath $ExportPath `
        -MountPoint $MountPoint `
        -SuiteCommand $suiteCommand

    $stdoutLogPath = Join-Path $ResultsDirectory "stdout.log"
    $stderrLogPath = Join-Path $ResultsDirectory "stderr.log"
    $stdout = if (Test-Path $stdoutLogPath) { Get-Content -Raw -Path $stdoutLogPath } else { "" }
    $stderr = if (Test-Path $stderrLogPath) { Get-Content -Raw -Path $stderrLogPath } else { "" }

    Write-InteropManifest -Path $manifestPath -ManifestObject @{
        suite = "connectathon"
        protocolVersion = $ProtocolVersion
        serverHost = $ServerHost
        mountPort = $MountPort
        nfsPort = $NfsPort
        exportPath = $ExportPath
        mountPoint = $MountPoint
        suiteRoot = $resolvedSuiteRoot
        subset = $Subset
        command = $suiteCommand
        useSampleServer = [bool]$UseSampleServer
        exitCode = $containerResult.ExitCode
        stdoutLogPath = $stdoutLogPath
        stderrLogPath = $stderrLogPath
        validatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    }

    if ($containerResult.ExitCode -ne 0) {
        throw "Connectathon execution failed.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$stdout$([Environment]::NewLine)stderr:$([Environment]::NewLine)$stderr"
    }
}
finally {
    Stop-SampleInteropServer -ServerProcess $sampleServer
}
