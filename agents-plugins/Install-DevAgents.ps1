#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path $PSScriptRoot
$lock = Join-Path $root 'apm.lock.yaml'
if (!(Test-Path -LiteralPath $lock))
{
    throw 'A committed apm.lock.yaml is required to restore development agents.'
}

$snapshot = [System.IO.Path]::GetTempFileName()
Copy-Item -LiteralPath $lock -Destination $snapshot
Push-Location $root
try
{
    # ponytail: APM 0.31 frozen preflight rejects cold marketplace dependencies
    # when MCP state exists, even with --only apm. Use --frozen once upstream fixes it.
    apm install --target copilot --only apm
    if ($LASTEXITCODE -ne 0)
    {
        throw "APM development agent installation failed (exit $LASTEXITCODE)."
    }
    git -c core.autocrlf=false diff --no-index --ignore-cr-at-eol -- $snapshot $lock
    if ($LASTEXITCODE -ne 0)
    {
        throw 'APM restore changed the lockfile. Review intentional dependency changes and commit the updated lockfile, then retry. See the Contributing.md maintenance commands.'
    }
}
finally
{
    Pop-Location
    Remove-Item -LiteralPath $snapshot -Force
}
