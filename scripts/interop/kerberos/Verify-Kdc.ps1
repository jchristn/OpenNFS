<#
.SYNOPSIS
Verifies the OpenNFS Kerberos KDC fixture is healthy and issues tickets.

.DESCRIPTION
Runs against the docker-compose KDC defined in scripts/interop/kerberos/. Confirms
the container is healthy, principals exist, keytabs are exported to the host, and
the KDC can issue both a TGT and a service ticket. Used by the eventual RPCSEC_GSS
test infrastructure to gate Kerberos-backed cases on KDC availability.

.PARAMETER Plan
When set, validates the harness wiring without contacting the KDC.
#>
[CmdletBinding()]
param(
    [switch]$Plan
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$kerberosRoot = Split-Path -Parent $PSCommandPath
$keytabRoot = Join-Path $kerberosRoot 'keytabs'
$expectedKeytabs = @('alice.keytab', 'bob.keytab', 'sample.keytab')

if ($Plan) {
    Write-Output "Plan-mode validation:"
    Write-Output "  Kerberos root: $kerberosRoot"
    Write-Output "  Keytab output: $keytabRoot"
    Write-Output "  Expected keytabs: $($expectedKeytabs -join ', ')"
    return
}

# Confirm container is running and healthy.
$status = docker ps --filter 'name=opennfs-kdc' --format '{{.Names}}|{{.Status}}'
if ([string]::IsNullOrWhiteSpace($status)) {
    throw "opennfs-kdc container is not running. Start with 'docker compose up -d' from $kerberosRoot."
}

if ($status -notmatch 'healthy') {
    throw "opennfs-kdc is running but not healthy: $status"
}

# Confirm keytabs landed on host.
foreach ($kt in $expectedKeytabs) {
    $path = Join-Path $keytabRoot $kt
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Expected keytab $kt is missing from $keytabRoot."
    }

    $size = (Get-Item -LiteralPath $path).Length
    if ($size -le 0) {
        throw "Keytab $kt is empty."
    }
}

# Run a kinit + service-ticket round-trip inside the container.
$kinitProbe = @"
echo 'alice-password' | kinit -V alice@EXAMPLE.TEST 2>&1 || exit 11
kvno -k /keytabs/sample.keytab nfs/sample.example.test@EXAMPLE.TEST 2>&1 || exit 12
klist 2>&1
"@

$probeOutput = docker exec opennfs-kdc sh -c $kinitProbe 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "KDC ticket probe failed (exit $LASTEXITCODE):`n$probeOutput"
}

$probeText = ($probeOutput | Out-String)
if ($probeText -notmatch 'krbtgt/EXAMPLE\.TEST') {
    throw "KDC probe did not surface a TGT for alice. Output: $probeText"
}

if ($probeText -notmatch 'nfs/sample\.example\.test') {
    throw "KDC probe did not surface a service ticket for the sample NFS principal. Output: $probeText"
}

Write-Output "KDC fixture verified: TGT + service ticket round-trip OK."
Write-Output "Realm: EXAMPLE.TEST"
Write-Output "Principals: alice@EXAMPLE.TEST, bob@EXAMPLE.TEST, nfs/sample.example.test@EXAMPLE.TEST"
Write-Output "Keytabs: $keytabRoot"
