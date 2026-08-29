[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ScriptName
)

$ErrorActionPreference = "Stop"

$pipelineRoot = Split-Path -Parent $PSCommandPath
$blenderExe = "E:\SteamLibrary\steamapps\common\Blender\blender.exe"

if ([System.IO.Path]::GetFileName($ScriptName) -ne $ScriptName) {
    Write-Error "Pass a script filename from the scripts directory, for example: test_connection.py"
    exit 2
}

$targetScript = Join-Path $pipelineRoot (Join-Path "scripts" $ScriptName)

if (-not (Test-Path -LiteralPath $blenderExe -PathType Leaf)) {
    Write-Error "Blender executable was not found: $blenderExe"
    exit 2
}

if (-not (Test-Path -LiteralPath $targetScript -PathType Leaf)) {
    Write-Error "Blender Python script was not found: $targetScript"
    exit 2
}

& $blenderExe --background --python-exit-code 1 --python $targetScript
$blenderExitCode = $LASTEXITCODE
exit $blenderExitCode
