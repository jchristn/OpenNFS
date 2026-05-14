[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [string]$ResultsDirectory,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Utf8NoBomFile
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Contents
    )

    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Contents, $encoding)
}

function Read-LogFile
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not [System.IO.File]::Exists($Path))
    {
        return [string]::Empty
    }

    return [System.IO.File]::ReadAllText($Path)
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))

if ([string]::IsNullOrWhiteSpace($ResultsDirectory))
{
    $ResultsDirectory = Join-Path $repoRoot ("artifacts\manual-smoke\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}
else
{
    $ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
}

[System.IO.Directory]::CreateDirectory($ResultsDirectory) | Out-Null

$sampleRoot = Join-Path $ResultsDirectory "sample"
$sampleDataRoot = Join-Path $sampleRoot "export"
$sampleStateRoot = Join-Path $sampleRoot "state"
$mappingPath = Join-Path $sampleStateRoot "filehandles.json"
$configPath = Join-Path $ResultsDirectory "sample-config.json"
$clientScriptPath = Join-Path $ResultsDirectory "client-commands.txt"
$serverStdOutPath = Join-Path $ResultsDirectory "sample-server.stdout.log"
$serverStdErrPath = Join-Path $ResultsDirectory "sample-server.stderr.log"
$clientStdOutPath = Join-Path $ResultsDirectory "test-client.stdout.log"
$clientStdErrPath = Join-Path $ResultsDirectory "test-client.stderr.log"

$config = [ordered]@{
    serverName = "OpenNFS Manual Smoke"
    exportPath = "/exports/sample"
    listenerAddress = "127.0.0.1"
    sourcePath = $sampleDataRoot
    owner = "sample-owner@example.test"
    ownerGroup = "sample-group@example.test"
    mappingPath = $mappingPath
    mountPort = 0
    nfsPort = 0
    nfs40Port = 0
    denyMounts = $false
}

Write-Utf8NoBomFile -Path $configPath -Contents ($config | ConvertTo-Json -Depth 4)

if (-not $SkipBuild)
{
    $projectsToBuild = @(
        "src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj",
        "src/OpenNFS.TestClient/OpenNFS.TestClient.csproj"
    )

    foreach ($project in $projectsToBuild)
    {
        Write-Host "Building $project..."
        & dotnet build $project -c $Configuration --framework $Framework
        if ($LASTEXITCODE -ne 0)
        {
            throw "dotnet build failed for $project with exit code $LASTEXITCODE."
        }
    }
}

$serverArguments = @(
    "run",
    "--project", "src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj",
    "-c", $Configuration,
    "--framework", $Framework,
    "--",
    "--config", $configPath
)

Write-Host "Starting Sample.OpenNfsServer..."
$serverProcess = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList $serverArguments `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $serverStdOutPath `
    -RedirectStandardError $serverStdErrPath `
    -PassThru `
    -WindowStyle Hidden

try
{
    $readyLine = $null
    $deadline = (Get-Date).AddSeconds(60)

    while ((Get-Date) -lt $deadline)
    {
        if ($serverProcess.HasExited)
        {
            $stdout = Read-LogFile -Path $serverStdOutPath
            $stderr = Read-LogFile -Path $serverStdErrPath
            throw "Sample.OpenNfsServer exited before publishing a READY line.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
        }

        if ([System.IO.File]::Exists($serverStdOutPath))
        {
            $readyLine = Get-Content $serverStdOutPath | Where-Object { $_ -like "READY *" } | Select-Object -Last 1
            if ($readyLine)
            {
                break
            }
        }

        Start-Sleep -Milliseconds 250
    }

    if (-not $readyLine)
    {
        $stdout = Read-LogFile -Path $serverStdOutPath
        $stderr = Read-LogFile -Path $serverStdErrPath
        throw "Timed out waiting for the sample server READY line.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    if ($readyLine -notmatch "^READY mountPort=(\d+) nfsPort=(\d+) nfs40Port=(\d+) exportPath=([^ ]+) kerberos=([^ ]+)$")
    {
        throw "Could not parse the sample server READY line: $readyLine"
    }

    $mountPort = [int]$Matches[1]
    $nfsPort = [int]$Matches[2]
    $exportPath = $Matches[4]

    $clientCommands = @(
        "# OpenNFS sample smoke script",
        "server 127.0.0.1 $nfsPort",
        "mountendpoint 127.0.0.1 $mountPort",
        "auth authsys",
        "authsys opennfs-smoke 501 20 10,11",
        "connect",
        "exports",
        "mount $exportPath",
        "ls /",
        "cat /hello.txt",
        "cat /docs/nested.txt",
        "write /smoke.txt smoke-from-script",
        "cat /smoke.txt",
        "rm /smoke.txt",
        "umount",
        "disconnect"
    )

    Write-Utf8NoBomFile -Path $clientScriptPath -Contents ([string]::Join([Environment]::NewLine, $clientCommands) + [Environment]::NewLine)

    $clientArguments = @(
        "run",
        "--project", "src/OpenNFS.TestClient/OpenNFS.TestClient.csproj",
        "-c", $Configuration,
        "--framework", $Framework,
        "--",
        "--script", $clientScriptPath
    )

    Write-Host "Running scripted OpenNFS.TestClient smoke pass..."
    $clientProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $clientArguments `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $clientStdOutPath `
        -RedirectStandardError $clientStdErrPath `
        -PassThru `
        -Wait `
        -WindowStyle Hidden

    $clientStdOut = Read-LogFile -Path $clientStdOutPath
    $clientStdErr = Read-LogFile -Path $clientStdErrPath

    if ($clientProcess.ExitCode -ne 0)
    {
        throw "OpenNFS.TestClient smoke script failed with exit code $($clientProcess.ExitCode).`nSTDOUT:`n$clientStdOut`nSTDERR:`n$clientStdErr"
    }

    if ($clientStdOut -notmatch "hello-from-sample-opennfs" -or $clientStdOut -notmatch "nested-from-sample-opennfs")
    {
        throw "The smoke pass completed, but the expected seeded sample content was not observed.`nSTDOUT:`n$clientStdOut`nSTDERR:`n$clientStdErr"
    }

    Write-Host ""
    Write-Host "OpenNFS sample smoke passed."
    Write-Host "  Results: $ResultsDirectory"
    Write-Host "  Sample server log: $serverStdOutPath"
    Write-Host "  Test client log : $clientStdOutPath"
    Write-Host ""
    Write-Host $clientStdOut
}
finally
{
    if ($serverProcess -and -not $serverProcess.HasExited)
    {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }
}
