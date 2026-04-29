param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot "..\src\OpenNFS.XdrGen\xdrgen.json"),
    [switch]$Check
)

$projectPath = Join-Path $PSScriptRoot "..\src\OpenNFS.XdrGen\OpenNFS.XdrGen.csproj"
$arguments = @(
    "run",
    "--project",
    $projectPath,
    "--",
    "--config",
    $ConfigurationPath
)

if ($Check)
{
    $arguments += "--check"
}

dotnet @arguments
exit $LASTEXITCODE

