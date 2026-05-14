Set-StrictMode -Version Latest

function Resolve-InteropRepositoryRoot {
    param(
        [string]$RepositoryRoot,
        [string]$ScriptRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        return (Resolve-Path $RepositoryRoot).Path
    }

    $directory = [System.IO.DirectoryInfo]::new((Resolve-Path $ScriptRoot).Path)
    while ($null -ne $directory) {
        if (Test-Path (Join-Path $directory.FullName "OPENNFS.md") -PathType Leaf) {
            return $directory.FullName
        }

        $directory = $directory.Parent
    }

    throw "Could not locate the OpenNFS repository root from '$ScriptRoot'."
}

function Ensure-InteropDirectory {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "A non-empty path is required."
    }

    $resolved = [System.IO.Path]::GetFullPath($Path)
    New-Item -ItemType Directory -Force -Path $resolved | Out-Null
    return $resolved
}

function Resolve-InteropPath {
    param(
        [string]$BasePath,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "A non-empty path is required."
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function New-InteropTempDirectory {
    param(
        [string]$RootName,
        [string]$ScenarioName
    )

    $path = Join-Path ([System.IO.Path]::GetTempPath()) "$RootName.$ScenarioName.$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    return $path
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Invoke-InteropProcess {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [int]$TimeoutSeconds = 300
    )

    $stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) "opennfs-interop-stdout-$([Guid]::NewGuid().ToString('N')).log"
    $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) "opennfs-interop-stderr-$([Guid]::NewGuid().ToString('N')).log"

    try {
        Push-Location $WorkingDirectory
        try {
            & $FilePath @Arguments 1> $stdoutPath 2> $stderrPath
            $exitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }

        return [pscustomobject]@{
            ExitCode       = $exitCode
            StandardOutput = if (Test-Path $stdoutPath) { Get-Content -Raw -Path $stdoutPath } else { "" }
            StandardError  = if (Test-Path $stderrPath) { Get-Content -Raw -Path $stderrPath } else { "" }
            CommandLine    = "$FilePath $($Arguments -join ' ')"
        }
    }
    finally {
        if (Test-Path $stdoutPath) {
            Remove-Item -Force $stdoutPath
        }

        if (Test-Path $stderrPath) {
            Remove-Item -Force $stderrPath
        }
    }
}

function Ensure-LinuxClientInteropImage {
    param([string]$RepositoryRoot)

    $image = "opennfs-test/linux-nfs-client:local"
    $inspect = Invoke-InteropProcess -FilePath "docker" -Arguments @("image", "inspect", $image) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 60
    if ($inspect.ExitCode -eq 0) {
        return $image
    }

    $contextDirectory = Join-Path $RepositoryRoot "scripts/interop/linux/nfs-client"
    $build = Invoke-InteropProcess -FilePath "docker" -Arguments @("build", "--tag", $image, $contextDirectory) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 600
    $verify = Invoke-InteropProcess -FilePath "docker" -Arguments @("image", "inspect", $image) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 60
    if ($verify.ExitCode -ne 0) {
        throw "Failed to build the Linux NFS client image.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$($build.StandardOutput)$([Environment]::NewLine)stderr:$([Environment]::NewLine)$($build.StandardError)"
    }

    return $image
}

function Ensure-LinuxNfs41InteropImage {
    param([string]$RepositoryRoot)

    $image = "opennfs-test/linux-nfs-server-ganesha-v4:local"
    $inspect = Invoke-InteropProcess -FilePath "docker" -Arguments @("image", "inspect", $image) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 60
    if ($inspect.ExitCode -eq 0) {
        return $image
    }

    $contextDirectory = Join-Path $RepositoryRoot "scripts/interop/linux/nfs-server-ganesha-v4"
    $build = Invoke-InteropProcess -FilePath "docker" -Arguments @("build", "--no-cache", "--tag", $image, $contextDirectory) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 600
    $verify = Invoke-InteropProcess -FilePath "docker" -Arguments @("image", "inspect", $image) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 60
    if ($verify.ExitCode -ne 0) {
        throw "Failed to build the Linux NFSv4.1 server image.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$($build.StandardOutput)$([Environment]::NewLine)stderr:$([Environment]::NewLine)$($build.StandardError)"
    }

    return $image
}

function Start-LinuxNfs41InteropServer {
    param([string]$RepositoryRoot)

    $image = Ensure-LinuxNfs41InteropImage -RepositoryRoot $RepositoryRoot
    $containerName = "opennfs-interop-linux-nfs41-" + [Guid]::NewGuid().ToString("N")

    try {
        $run = Invoke-InteropProcess -FilePath "docker" -Arguments @(
            "run",
            "--detach",
            "--name",
            $containerName,
            "--publish",
            "127.0.0.1::2049",
            "--cap-add",
            "DAC_READ_SEARCH",
            "--tmpfs",
            "/export-real:rw,mode=0777,size=16m",
            $image
        ) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 120

        if ($run.ExitCode -ne 0) {
            throw "Failed to start the Linux NFSv4.1 server container.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$($run.StandardOutput)$([Environment]::NewLine)stderr:$([Environment]::NewLine)$($run.StandardError)"
        }

        $serverPort = Wait-InteropContainerPublishedPort -RepositoryRoot $RepositoryRoot -ContainerName $containerName -ContainerPort "2049/tcp"
        Wait-TcpEndpointReady -HostName "127.0.0.1" -Port $serverPort

        return [pscustomobject]@{
            ContainerName = $containerName
            ServerHost = "host.docker.internal"
            ServerPort = $serverPort
            Image = $image
        }
    }
    catch {
        $logs = ""
        try {
            $logs = Get-InteropContainerLogs -RepositoryRoot $RepositoryRoot -ContainerName $containerName
        }
        catch {
        }

        Remove-InteropContainer -RepositoryRoot $RepositoryRoot -ContainerName $containerName

        if ([string]::IsNullOrWhiteSpace($logs)) {
            throw
        }

        throw "$($_.Exception.Message)$([Environment]::NewLine)logs:$([Environment]::NewLine)$logs"
    }
}

function Get-InteropContainerInspectValue {
    param(
        [string]$RepositoryRoot,
        [string]$ContainerName,
        [string]$Template
    )

    $result = Invoke-InteropProcess -FilePath "docker" -Arguments @(
        "inspect",
        "--format",
        $Template,
        $ContainerName
    ) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 30

    if ($result.ExitCode -ne 0) {
        throw "docker inspect failed for '$ContainerName'.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$($result.StandardOutput)$([Environment]::NewLine)stderr:$([Environment]::NewLine)$($result.StandardError)"
    }

    return $result.StandardOutput.Trim()
}

function Wait-InteropContainerPublishedPort {
    param(
        [string]$RepositoryRoot,
        [string]$ContainerName,
        [string]$ContainerPort
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    $parsedPort = 0
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $portResult = Invoke-InteropProcess -FilePath "docker" -Arguments @("port", $ContainerName, $ContainerPort) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 30
        if ($portResult.ExitCode -eq 0) {
            $hostPortLine = $portResult.StandardOutput.Trim()
            if (-not [string]::IsNullOrWhiteSpace($hostPortLine)) {
                $hostPort = ($hostPortLine -split ":")[-1].Trim()
                if ([int]::TryParse($hostPort, [ref]$parsedPort)) {
                    return $parsedPort
                }
            }
        }

        $status = Get-InteropContainerInspectValue `
            -RepositoryRoot $RepositoryRoot `
            -ContainerName $ContainerName `
            -Template "{{ .State.Status }}"
        if ([string]::Equals($status, "exited", [System.StringComparison]::OrdinalIgnoreCase) -or [string]::Equals($status, "dead", [System.StringComparison]::OrdinalIgnoreCase)) {
            $exitCode = Get-InteropContainerInspectValue `
                -RepositoryRoot $RepositoryRoot `
                -ContainerName $ContainerName `
                -Template "{{ .State.ExitCode }}"
            $logs = Get-InteropContainerLogs -RepositoryRoot $RepositoryRoot -ContainerName $ContainerName

            throw "The Linux NFSv4.1 server container '$ContainerName' exited before Docker published $ContainerPort.$([Environment]::NewLine)status: $status$([Environment]::NewLine)exit code: $exitCode$([Environment]::NewLine)logs:$([Environment]::NewLine)$logs"
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for Docker to publish $ContainerPort for Linux NFSv4.1 server container '$ContainerName'."
}

function Get-InteropContainerLogs {
    param(
        [string]$RepositoryRoot,
        [string]$ContainerName
    )

    $result = Invoke-InteropProcess -FilePath "docker" -Arguments @("logs", $ContainerName) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 30
    return $result.StandardOutput + $([Environment]::NewLine) + $result.StandardError
}

function Remove-InteropContainer {
    param(
        [string]$RepositoryRoot,
        [string]$ContainerName
    )

    if ([string]::IsNullOrWhiteSpace($ContainerName)) {
        return
    }

    try {
        $null = Invoke-InteropProcess -FilePath "docker" -Arguments @("rm", "--force", $ContainerName) -WorkingDirectory $RepositoryRoot -TimeoutSeconds 30
    }
    catch {
    }
}

function Wait-TcpEndpointReady {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutSeconds = 20
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastException = $null

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $connectTask = $client.ConnectAsync($HostName, $Port)
            if (-not $connectTask.Wait([TimeSpan]::FromSeconds(2))) {
                throw [System.TimeoutException]::new("Timed out connecting to ${HostName}:$Port.")
            }

            return
        }
        catch {
            $lastException = $_.Exception
            Start-Sleep -Milliseconds 250
        }
        finally {
            $client.Dispose()
        }
    }

    if ($null -ne $lastException) {
        throw "Timed out waiting for TCP endpoint ${HostName}:$Port to accept connections. Last error: $($lastException.Message)"
    }

    throw "Timed out waiting for TCP endpoint ${HostName}:$Port to accept connections."
}

function Start-SampleInteropServer {
    param(
        [string]$RepositoryRoot,
        [string]$ResultsDirectory,
        [string]$ExportPath = "/exports/sample",
        [switch]$DenyMounts
    )

    $sampleRoot = Ensure-InteropDirectory (Join-Path $ResultsDirectory "sample")
    $sourcePath = Ensure-InteropDirectory (Join-Path $sampleRoot "source")
    $stateDirectory = Ensure-InteropDirectory (Join-Path $sampleRoot "state")
    $mappingPath = Join-Path $stateDirectory "handles.json"
    $stdoutPath = Join-Path $sampleRoot "sample-stdout.log"
    $stderrPath = Join-Path $sampleRoot "sample-stderr.log"

    $arguments = @(
        "run",
        "--no-build",
        "-c",
        "Release",
        "--framework",
        "net8.0",
        "--project",
        (Join-Path $RepositoryRoot "src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj"),
        "--",
        "--source-path",
        $sourcePath,
        "--mapping-path",
        $mappingPath,
        "--export-path",
        $ExportPath,
        "--listener-address",
        "0.0.0.0",
        "--mount-port",
        "0",
        "--nfs-port",
        "0",
        "--nfs40-port",
        "0"
    )

    if ($DenyMounts) {
        $arguments += "--deny-mounts"
    }

    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $arguments `
        -WorkingDirectory $RepositoryRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            $stdout = if (Test-Path $stdoutPath) { Get-Content -Raw -Path $stdoutPath } else { "" }
            $stderr = if (Test-Path $stderrPath) { Get-Content -Raw -Path $stderrPath } else { "" }
            throw "Sample.OpenNfsServer exited before announcing readiness.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$stdout$([Environment]::NewLine)stderr:$([Environment]::NewLine)$stderr"
        }

        if (Test-Path $stdoutPath) {
            $stdout = Get-Content -Raw -Path $stdoutPath
            if ($stdout -and $stdout.IndexOf("READY ", [System.StringComparison]::Ordinal) -ge 0) {
                $readyValues = Read-SampleReadyValues -ReadyText $stdout
                return [pscustomobject]@{
                    Process    = $process
                    SourcePath = $sourcePath
                    MappingPath = $mappingPath
                    MountPort  = $readyValues.MountPort
                    NfsPort    = $readyValues.NfsPort
                    Nfs40Port  = $readyValues.Nfs40Port
                    ExportPath = $ExportPath
                    StdoutPath = $stdoutPath
                    StderrPath = $stderrPath
                }
            }
        }

        Start-Sleep -Milliseconds 250
    }

    try {
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }
    catch {
    }

    $stdout = if (Test-Path $stdoutPath) { Get-Content -Raw -Path $stdoutPath } else { "" }
    $stderr = if (Test-Path $stderrPath) { Get-Content -Raw -Path $stderrPath } else { "" }
    throw "Timed out waiting for Sample.OpenNfsServer readiness.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$stdout$([Environment]::NewLine)stderr:$([Environment]::NewLine)$stderr"
}

function Read-SampleReadyValues {
    param([string]$ReadyText)

    $readyLine = $ReadyText -split "(`r`n|`n)" | Where-Object { $_ -like "READY *" } | Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($readyLine)) {
        throw "No READY line was found in the sample server output."
    }

    $values = @{}
    foreach ($token in ($readyLine -split ' ' | Select-Object -Skip 1)) {
        $parts = $token -split '=', 2
        if ($parts.Count -eq 2) {
            $values[$parts[0]] = $parts[1]
        }
    }

    return [pscustomobject]@{
        MountPort = [int]$values["mountPort"]
        NfsPort = [int]$values["nfsPort"]
        Nfs40Port = [int]$values["nfs40Port"]
    }
}

function Stop-SampleInteropServer {
    param($ServerProcess)

    if ($null -eq $ServerProcess) {
        return
    }

    try {
        if ($ServerProcess.Process -and -not $ServerProcess.Process.HasExited) {
            $ServerProcess.Process.Kill()
            $null = $ServerProcess.Process.WaitForExit(10000)
        }
    }
    catch {
    }
}

function Write-InteropManifest {
    param(
        [string]$Path,
        $ManifestObject
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $ManifestObject | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 -Path $Path
}

function Resolve-PerlSuiteCommand {
    param(
        [string]$SuiteRoot,
        [string]$Subset
    )

    $testsDirectory = Join-Path $SuiteRoot "tests"
    if (-not (Test-Path $testsDirectory -PathType Container)) {
        throw "Expected a 'tests' directory under '$SuiteRoot'."
    }

    if ([string]::Equals($Subset, "all", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "prove -r tests"
    }

    $subsetDirectory = Join-Path $testsDirectory $Subset
    if (Test-Path $subsetDirectory -PathType Container) {
        return "prove -r tests/$Subset"
    }

    $subsetFile = Join-Path $testsDirectory ($Subset + ".t")
    if (Test-Path $subsetFile -PathType Leaf) {
        return "prove tests/$Subset.t"
    }

    throw "The requested subset '$Subset' was not found under '$testsDirectory'."
}

function Resolve-CthonSuiteCommand {
    param(
        [string]$SuiteRoot,
        [string]$Subset
    )

    $runScript = Join-Path $SuiteRoot "runtests"
    if (-not (Test-Path $runScript -PathType Leaf)) {
        $runScript = Join-Path $SuiteRoot "runcthon"
    }

    if (-not (Test-Path $runScript -PathType Leaf)) {
        throw "Connectathon suite root '$SuiteRoot' must contain a 'runtests' or 'runcthon' script."
    }

    $validSubsets = @("basic", "general", "special", "lock", "all")
    if (-not ($validSubsets -contains $Subset)) {
        throw "Connectathon subset '$Subset' is not one of: $($validSubsets -join ', ')."
    }

    $hasMakefile = (Test-Path (Join-Path $SuiteRoot "Makefile") -PathType Leaf) `
        -or (Test-Path (Join-Path $SuiteRoot "Makefile.in") -PathType Leaf)
    $buildPrefix = if ($hasMakefile) { "make && " } else { "" }

    if ([string]::Equals($Subset, "all", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "${buildPrefix}./runtests -a"
    }

    $subsetDirectory = Join-Path $SuiteRoot $Subset
    if (-not (Test-Path $subsetDirectory -PathType Container)) {
        throw "Connectathon suite root '$SuiteRoot' is missing the '$Subset' subdirectory."
    }

    return "${buildPrefix}./runtests -t $Subset"
}

function Resolve-PynfsCommand {
    param(
        [string]$SuiteRoot,
        [string]$EntryPoint,
        [string[]]$EntryPointArguments
    )

    if ([string]::IsNullOrWhiteSpace($EntryPoint)) {
        throw "An explicit -EntryPoint must be supplied for pynfs execution."
    }

    $resolvedEntryPoint = Join-Path $SuiteRoot $EntryPoint
    if (-not (Test-Path $resolvedEntryPoint -PathType Leaf)) {
        throw "The pynfs entry point '$resolvedEntryPoint' does not exist."
    }

    $quotedArguments = @("python3", "/suite/$EntryPoint")
    if ($EntryPointArguments) {
        $quotedArguments += $EntryPointArguments
    }

    return ($quotedArguments -join " ")
}

function Invoke-LinuxClientContainer {
    param(
        [string]$RepositoryRoot,
        [string]$ShellCommand,
        [string[]]$VolumeMounts,
        [int]$TimeoutSeconds = 600
    )

    $image = Ensure-LinuxClientInteropImage -RepositoryRoot $RepositoryRoot
    $containerName = "opennfs-interop-" + [Guid]::NewGuid().ToString("N")

    $arguments = @(
        "run",
        "--rm",
        "--name",
        $containerName,
        "--privileged",
        "--add-host",
        "host.docker.internal:host-gateway"
    )

    foreach ($mount in $VolumeMounts) {
        $arguments += @("--volume", $mount)
    }

    $arguments += @(
        $image,
        $ShellCommand
    )

    return Invoke-InteropProcess -FilePath "docker" -Arguments $arguments -WorkingDirectory $RepositoryRoot -TimeoutSeconds $TimeoutSeconds
}

function Invoke-MountedSuiteContainer {
    param(
        [string]$RepositoryRoot,
        [ValidateSet("NfsV3", "NfsV40")]
        [string]$ProtocolVersion,
        [string]$SuiteRoot,
        [string]$ResultsDirectory,
        [string]$ServerHost,
        [int]$MountPort,
        [int]$NfsPort,
        [string]$ExportPath,
        [string]$MountPoint,
        [string]$SuiteCommand
    )

    $suiteRootPath = [System.IO.Path]::GetFullPath($SuiteRoot)
    $resultsRootPath = Ensure-InteropDirectory $ResultsDirectory

    $mountOptions = if ($ProtocolVersion -eq "NfsV3") {
        "vers=3,proto=tcp,mountproto=tcp,port=$NfsPort,mountport=$MountPort,nolock,soft,timeo=10,retrans=1"
    }
    else {
        "vers=4,minorversion=0,proto=tcp,port=$NfsPort,soft,timeo=10,retrans=1"
    }

    $shellCommand = @"
set -eu
mkdir -p "$MountPoint" /results
mount -t nfs -o $mountOptions ${ServerHost}:$ExportPath "$MountPoint"
cleanup() {
  umount "$MountPoint" >/dev/null 2>&1 || true
}
trap cleanup EXIT
export OPENNFS_MOUNT_POINT="$MountPoint"
export OPENNFS_SERVER_HOST="$ServerHost"
export OPENNFS_EXPORT_PATH="$ExportPath"
export OPENNFS_PROTOCOL_VERSION="$ProtocolVersion"
cd /suite
$SuiteCommand > /results/stdout.log 2> /results/stderr.log
"@

    return Invoke-LinuxClientContainer `
        -RepositoryRoot $RepositoryRoot `
        -ShellCommand $shellCommand `
        -VolumeMounts @(
            "${suiteRootPath}:/suite:ro",
            "${resultsRootPath}:/results"
        )
}

function Invoke-PynfsContainer {
    param(
        [string]$RepositoryRoot,
        [string]$SuiteRoot,
        [string]$ResultsDirectory,
        [string]$ServerHost,
        [int]$ServerPort,
        [string]$ExportPath,
        [int]$MinorVersion,
        [string]$SuiteCommand
    )

    $suiteRootPath = [System.IO.Path]::GetFullPath($SuiteRoot)
    $resultsRootPath = Ensure-InteropDirectory $ResultsDirectory

    $shellCommand = @"
set -eu
mkdir -p /results
export OPENNFS_SERVER_HOST="$ServerHost"
export OPENNFS_SERVER_PORT="$ServerPort"
export OPENNFS_EXPORT_PATH="$ExportPath"
export OPENNFS_MINOR_VERSION="$MinorVersion"
cd /suite
$SuiteCommand > /results/stdout.log 2> /results/stderr.log
"@

    return Invoke-LinuxClientContainer `
        -RepositoryRoot $RepositoryRoot `
        -ShellCommand $shellCommand `
        -VolumeMounts @(
            "${suiteRootPath}:/suite:ro",
            "${resultsRootPath}:/results"
        )
}
