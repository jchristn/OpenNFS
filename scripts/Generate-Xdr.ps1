param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot "..\src\OpenNFS.XdrGen\xdrgen.json"),
    [switch]$Check
)

$projectPath = Join-Path $PSScriptRoot "..\src\OpenNFS.XdrGen\OpenNFS.XdrGen.csproj"
$arguments = @(
    "run",
    "--project",
    $projectPath,
    "--framework",
    "net8.0",
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

