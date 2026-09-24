#Requires -Version 7.0
[CmdletBinding()]
param(
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$source = Join-Path $PSScriptRoot 'ark-csharp'
$published = Join-Path $PSScriptRoot 'published\ark-csharp'
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())

if ($Check -and !(Test-Path -LiteralPath $published))
{
    throw 'Published ark-csharp plugin is missing. Run apm run plugins:build.'
}

# APM 0.31 embeds a timestamped, path-attested lock when present. This
# source-only plugin has no runtime dependencies; do not invalidate that attestation.
if (Test-Path -LiteralPath (Join-Path $source 'apm.lock.yaml'))
{
    throw 'Pack ark-csharp from its source-only manifest, without a package-local apm.lock.yaml.'
}

New-Item -ItemType Directory -Path $staging | Out-Null
Push-Location $source
try
{
    apm pack --format plugin --marketplace none --output $staging
    if ($LASTEXITCODE -ne 0)
    {
        throw "APM plugin export failed (exit $LASTEXITCODE)."
    }
    $bundles = @(Get-ChildItem -LiteralPath $staging -Directory)
    if ($bundles.Count -ne 1)
    {
        throw 'APM must produce exactly one ark-csharp plugin bundle.'
    }

    $bundle = $bundles[0].FullName
    # APM emits plugin.json at the root. Both Claude and Copilot support
    # .claude-plugin/plugin.json; Claude does not document the root location.
    New-Item -ItemType Directory -Path (Join-Path $bundle '.claude-plugin') | Out-Null
    Move-Item -LiteralPath (Join-Path $bundle 'plugin.json') -Destination (Join-Path $bundle '.claude-plugin\plugin.json')

    if ($Check)
    {
        git -c core.autocrlf=false diff --no-index --ignore-cr-at-eol -- $published $bundle
        if ($LASTEXITCODE -ne 0)
        {
            throw 'Published ark-csharp plugin is stale. Run apm run plugins:build and commit the output.'
        }
    }
    else
    {
        if (Test-Path -LiteralPath $published)
        {
            Remove-Item -LiteralPath $published -Recurse -Force
        }
        New-Item -ItemType Directory -Path (Split-Path $published) -Force | Out-Null
        Move-Item -LiteralPath $bundle -Destination $published
    }

    Set-Location (Split-Path $PSScriptRoot)
    if ($Check)
    {
        apm pack --offline --check-versions --check-clean
    }
    else
    {
        apm pack --offline --check-versions --force
    }
    if ($LASTEXITCODE -ne 0)
    {
        throw "APM marketplace validation failed (exit $LASTEXITCODE)."
    }
}
finally
{
    Pop-Location
    Remove-Item -LiteralPath $staging -Recurse -Force
}
