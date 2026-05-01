<#
.SYNOPSIS
Builds and runs the OpenNFS Kerberos probe against the live test KDC.

.DESCRIPTION
The probe authenticates as alice@EXAMPLE.TEST via the alice.keytab, drives token exchange
through OpenNfsKerberosMechanism, establishes a Kerberos context, and exercises a
bidirectional krb5p Wrap/Unwrap round-trip. Exits 0 on success.

Prerequisites:
- The OpenNFS test KDC must be running (docker compose up -d in scripts/interop/kerberos/).
- Docker must be available to build and run the probe image.
#>
[CmdletBinding()]
param(
    [switch]$Plan
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$kerberosRoot = Join-Path $repositoryRoot 'scripts/interop/kerberos'
$dockerfile = Join-Path $kerberosRoot 'probe/Dockerfile'
$krb5ConfPath = Join-Path $kerberosRoot 'krb5.conf'
$keytabsPath = Join-Path $kerberosRoot 'keytabs'

if ($Plan) {
    Write-Output "Plan-mode validation:"
    Write-Output "  Repository root: $repositoryRoot"
    Write-Output "  Kerberos root:   $kerberosRoot"
    Write-Output "  Probe Dockerfile: $dockerfile"
    Write-Output "  krb5.conf:        $krb5ConfPath"
    Write-Output "  keytabs:          $keytabsPath"
    Write-Output "  Target image:     opennfs/kerberos-probe:latest"
    return
}

# Confirm prereqs.
if (-not (Test-Path -LiteralPath $dockerfile)) {
    throw "Probe Dockerfile not found at $dockerfile"
}

$kdcStatus = docker ps --filter 'name=opennfs-kdc' --format '{{.Status}}'
if ([string]::IsNullOrWhiteSpace($kdcStatus)) {
    throw "opennfs-kdc container is not running. Start with 'docker compose up -d' in $kerberosRoot."
}

# Build the probe image.
Push-Location $repositoryRoot
try {
    docker build -f scripts/interop/kerberos/probe/Dockerfile -t opennfs/kerberos-probe:latest . | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "docker build failed for opennfs/kerberos-probe."
    }
}
finally {
    Pop-Location
}

# Run the probe attached to the kerberos_default network so it can reach the KDC.
# Drive the run via cmd.exe so PowerShell's NativeCommandError handling does not abort the
# script when the probe writes benign 'Using default cache' diagnostics to stderr.
$stdoutLog = [System.IO.Path]::GetTempFileName()
$stderrLog = [System.IO.Path]::GetTempFileName()
$probeExitCode = 0
$probeOutput = ''
try {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'docker'
    $startInfo.Arguments = "run --rm --network kerberos_default" `
        + " -v `"${krb5ConfPath}:/krb5/krb5.conf:ro`"" `
        + " -v `"${keytabsPath}:/keytabs:ro`"" `
        + " opennfs/kerberos-probe:latest"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $probeExitCode = $process.ExitCode

    $probeOutput = $stdout + $stderr
}
finally {
    Remove-Item -LiteralPath $stdoutLog -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrLog -ErrorAction SilentlyContinue
}

$probeText = $probeOutput
Write-Output $probeText

if ($probeExitCode -ne 0) {
    throw "Kerberos probe failed (exit $probeExitCode)."
}

if ($probeText -notmatch 'PROBE: krb5p bidirectional round-trip OK') {
    throw "Probe output did not surface the expected krb5p success marker."
}

if ($probeText -notmatch 'initiator-principal=alice@EXAMPLE\.TEST') {
    throw "Probe output did not surface the expected initiator principal."
}

Write-Output "Kerberos probe PASS."
