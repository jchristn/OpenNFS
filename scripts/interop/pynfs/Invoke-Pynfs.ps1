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
    [switch]$UseLinuxNfs41Server,
    [switch]$PlanOnly
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "..\\InteropHarness.ps1")

function Initialize-PynfsV40SampleTree {
    param(
        [string]$SourcePath,
        [string]$ExportPath
    )

    if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        throw "A source path is required to seed the default pynfs NFSv4.0 tree."
    }

    if ([string]::IsNullOrWhiteSpace($ExportPath)) {
        throw "An export path is required to seed the default pynfs NFSv4.0 tree."
    }

    $trimmedExportPath = $ExportPath.Trim("/").Replace("/", [System.IO.Path]::DirectorySeparatorChar)
    $exportSourcePath = if ([string]::IsNullOrWhiteSpace($trimmedExportPath)) { $SourcePath } else { Ensure-InteropDirectory (Join-Path $SourcePath $trimmedExportPath) }
    $tmpDirectory = Ensure-InteropDirectory (Join-Path $exportSourcePath "tmp")
    $treeDirectory = Ensure-InteropDirectory (Join-Path $exportSourcePath "tree")
    [System.IO.File]::WriteAllBytes((Join-Path $treeDirectory "file"), [System.Text.Encoding]::ASCII.GetBytes("This is the file test data."))
}

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    throw "ResultsDirectory is required."
}

if ($UseSampleServer -and $UseLinuxNfs41Server) {
    throw "-UseSampleServer and -UseLinuxNfs41Server are mutually exclusive."
}

if ($UseLinuxNfs41Server) {
    if ($MinorVersion -ne 1) {
        throw "The local Linux NFSv4.1 bootstrap only supports pynfs NFSv4.1 execution."
    }

    if ([string]::Equals($ExportPath, "/exports/sample", [System.StringComparison]::Ordinal)) {
        $ExportPath = "/export"
    }

    if ([string]::IsNullOrWhiteSpace($ServerHost)) {
        $ServerHost = "host.docker.internal"
    }
}

$RepositoryRoot = Resolve-InteropRepositoryRoot -RepositoryRoot $RepositoryRoot -ScriptRoot $PSScriptRoot
$ResultsDirectory = Ensure-InteropDirectory (Resolve-InteropPath -BasePath $RepositoryRoot -Path $ResultsDirectory)
$manifestPath = Join-Path $ResultsDirectory "pynfs-manifest.json"
$resultsJsonPath = Join-Path $ResultsDirectory "pynfs-results.json"
$bootstrapStdoutLogPath = Join-Path $ResultsDirectory "bootstrap-stdout.log"
$bootstrapStderrLogPath = Join-Path $ResultsDirectory "bootstrap-stderr.log"

$resolvedSuiteRoot = if ([string]::IsNullOrWhiteSpace($SuiteRoot)) { "" } else { [System.IO.Path]::GetFullPath($SuiteRoot) }
$effectiveEntryPoint = if ([string]::IsNullOrWhiteSpace($EntryPoint) -and $MinorVersion -eq 0 -and -not [string]::IsNullOrWhiteSpace($resolvedSuiteRoot)) { "nfs4.0/testserver.py" } else { $EntryPoint }
$plannedServerHost = if ($UseSampleServer -and [string]::IsNullOrWhiteSpace($ServerHost)) { "host.docker.internal" } else { $ServerHost }
$plannedServerPort = if ($ServerPort -gt 0) { [string]$ServerPort } else { "<dynamic-port>" }
$suiteCommand = if ([string]::IsNullOrWhiteSpace($resolvedSuiteRoot)) {
    "<suite-root-required>"
}
else {
    try {
        Resolve-PynfsCommand `
            -SuiteRoot $resolvedSuiteRoot `
            -MinorVersion $MinorVersion `
            -EntryPoint $EntryPoint `
            -EntryPointArguments $EntryPointArguments `
            -ServerHost $plannedServerHost `
            -ServerPort $plannedServerPort `
            -ExportPath $ExportPath `
            -ResultsJsonPath "/results/pynfs-results.json"
    }
    catch {
        "<entry-point-required>"
    }
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
Write-Host "entryPoint=$effectiveEntryPoint"
Write-Host "command=$suiteCommand"

if ($PlanOnly) {
    return
}

if ([string]::IsNullOrWhiteSpace($resolvedSuiteRoot) -or -not (Test-Path $resolvedSuiteRoot -PathType Container)) {
    throw "PYNFS_ROOT or -SuiteRoot must point to a pynfs checkout before execution."
}

$sampleServer = $null
$linuxNfs41Server = $null
try {
    if ($UseSampleServer) {
        if ($MinorVersion -ne 0) {
            throw "The local sample-server bootstrap currently supports only NFSv4.0 for pynfs execution."
        }

        if ([string]::IsNullOrWhiteSpace($EntryPoint) -and (-not $EntryPointArguments -or $EntryPointArguments.Count -eq 0)) {
            Initialize-PynfsV40SampleTree -SourcePath (Join-Path $ResultsDirectory "sample\source") -ExportPath $ExportPath
        }

        $sampleServer = Start-SampleInteropServer -RepositoryRoot $RepositoryRoot -ResultsDirectory $ResultsDirectory -ExportPath $ExportPath
        $ServerHost = "host.docker.internal"
        $ServerPort = $sampleServer.Nfs40Port
    }
    elseif ($UseLinuxNfs41Server) {
        $linuxNfs41Server = Start-LinuxNfs41InteropServer -RepositoryRoot $RepositoryRoot
        $ServerHost = $linuxNfs41Server.ServerHost
        $ServerPort = $linuxNfs41Server.ServerPort
    }
    elseif ([string]::IsNullOrWhiteSpace($ServerHost) -or $ServerPort -le 0) {
        throw "ServerHost and ServerPort are required unless -UseSampleServer or -UseLinuxNfs41Server is specified."
    }

    $suiteCommand = Resolve-PynfsCommand `
        -SuiteRoot $resolvedSuiteRoot `
        -MinorVersion $MinorVersion `
        -EntryPoint $EntryPoint `
        -EntryPointArguments $EntryPointArguments `
        -ServerHost $ServerHost `
        -ServerPort ([string]$ServerPort) `
        -ExportPath $ExportPath `
        -ResultsJsonPath "/results/pynfs-results.json"
    $bootstrapRuntimeSuite = (Test-Path (Join-Path $resolvedSuiteRoot "setup.py") -PathType Leaf)
    $containerResult = Invoke-PynfsContainer `
        -RepositoryRoot $RepositoryRoot `
        -SuiteRoot $resolvedSuiteRoot `
        -ResultsDirectory $ResultsDirectory `
        -ServerHost $ServerHost `
        -ServerPort $ServerPort `
        -ExportPath $ExportPath `
        -MinorVersion $MinorVersion `
        -SuiteCommand $suiteCommand `
        -BootstrapRuntimeSuite:$bootstrapRuntimeSuite

    $stdoutLogPath = Join-Path $ResultsDirectory "stdout.log"
    $stderrLogPath = Join-Path $ResultsDirectory "stderr.log"
    $stdout = if (Test-Path $stdoutLogPath) { Get-Content -Raw -Path $stdoutLogPath } else { "" }
    $stderr = if (Test-Path $stderrLogPath) { Get-Content -Raw -Path $stderrLogPath } else { "" }
    $bootstrapStdout = if (Test-Path $bootstrapStdoutLogPath) { Get-Content -Raw -Path $bootstrapStdoutLogPath } else { "" }
    $bootstrapStderr = if (Test-Path $bootstrapStderrLogPath) { Get-Content -Raw -Path $bootstrapStderrLogPath } else { "" }
    $serverLogPath = Join-Path $ResultsDirectory "server.log"
    $failureCount = $null
    $skippedCount = $null
    $selectedSkippedCount = $null
    $selectedFailureCount = $null
    $selectedWarnedCount = $null
    $selectedPassedCount = $null
    $effectiveExitCode = $containerResult.ExitCode
    if (Test-Path $resultsJsonPath) {
        $resultsJson = Get-Content -Raw -Path $resultsJsonPath | ConvertFrom-Json
        $failureCount = [int]$resultsJson.failures
        $skippedCount = [int]$resultsJson.skipped
        if ($failureCount -gt 0) {
            $effectiveExitCode = 1
        }
    }

    $summaryMatch = [System.Text.RegularExpressions.Regex]::Match($stdout, "Of those:\s+(?<skipped>\d+)\s+Skipped,\s+(?<failed>\d+)\s+Failed,\s+(?<warned>\d+)\s+Warned,\s+(?<passed>\d+)\s+Passed", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($summaryMatch.Success) {
        $selectedSkippedCount = [int]$summaryMatch.Groups["skipped"].Value
        $selectedFailureCount = [int]$summaryMatch.Groups["failed"].Value
        $selectedWarnedCount = [int]$summaryMatch.Groups["warned"].Value
        $selectedPassedCount = [int]$summaryMatch.Groups["passed"].Value
        if ($selectedSkippedCount -gt 0 -or $selectedFailureCount -gt 0) {
            $effectiveExitCode = 1
        }
    }

    if ($linuxNfs41Server) {
        try {
            Get-InteropContainerLogs -RepositoryRoot $RepositoryRoot -ContainerName $linuxNfs41Server.ContainerName | Set-Content -Encoding UTF8 -Path $serverLogPath
        }
        catch {
        }
    }

    Write-InteropManifest -Path $manifestPath -ManifestObject @{
        suite = "pynfs"
        minorVersion = $MinorVersion
        serverHost = $ServerHost
        serverPort = $ServerPort
        exportPath = $ExportPath
        suiteRoot = $resolvedSuiteRoot
        entryPoint = $effectiveEntryPoint
        entryPointArguments = $EntryPointArguments
        command = $suiteCommand
        containerExitCode = $containerResult.ExitCode
        exitCode = $effectiveExitCode
        resultsJsonPath = if (Test-Path $resultsJsonPath) { $resultsJsonPath } else { $null }
        failureCount = $failureCount
        skippedCount = $skippedCount
        selectedSkippedCount = $selectedSkippedCount
        selectedFailureCount = $selectedFailureCount
        selectedWarnedCount = $selectedWarnedCount
        selectedPassedCount = $selectedPassedCount
        useSampleServer = [bool]$UseSampleServer
        useLinuxNfs41Server = [bool]$UseLinuxNfs41Server
        bootstrappedServerContainerName = if ($linuxNfs41Server) { $linuxNfs41Server.ContainerName } else { $null }
        stdoutLogPath = $stdoutLogPath
        stderrLogPath = $stderrLogPath
        bootstrapStdoutLogPath = if (Test-Path $bootstrapStdoutLogPath) { $bootstrapStdoutLogPath } else { $null }
        bootstrapStderrLogPath = if (Test-Path $bootstrapStderrLogPath) { $bootstrapStderrLogPath } else { $null }
        serverLogPath = if (Test-Path $serverLogPath) { $serverLogPath } else { $null }
        validatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    }

    if ($effectiveExitCode -ne 0) {
        $serverLogs = if (Test-Path $serverLogPath) { Get-Content -Raw -Path $serverLogPath } else { "" }
        throw "pynfs execution failed.$([Environment]::NewLine)bootstrap stdout:$([Environment]::NewLine)$bootstrapStdout$([Environment]::NewLine)bootstrap stderr:$([Environment]::NewLine)$bootstrapStderr$([Environment]::NewLine)stdout:$([Environment]::NewLine)$stdout$([Environment]::NewLine)stderr:$([Environment]::NewLine)$stderr$([Environment]::NewLine)server:$([Environment]::NewLine)$serverLogs"
    }
}
finally {
    Stop-SampleInteropServer -ServerProcess $sampleServer
    if ($linuxNfs41Server) {
        Remove-InteropContainer -RepositoryRoot $RepositoryRoot -ContainerName $linuxNfs41Server.ContainerName
    }
}
