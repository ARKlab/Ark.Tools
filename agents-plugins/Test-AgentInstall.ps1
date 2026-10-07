#Requires -Version 7.0
# Fails when the committed Copilot and Claude APM outputs differ from a clean
# install of apm.lock.yaml: edited, added or removed skills, agents, hooks or MCP servers.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$outputs = '.agents', '.claude', '.github/agents', '.github/hooks', '.github/mcp.json', '.github/lsp.json', '.mcp.json'
$settings = '.claude/settings.json'

Push-Location (Split-Path $PSScriptRoot -Parent)
try
{
    # Verifies deployed file hashes against the lockfile; blind to files APM never deployed.
    apm audit --ci
    if ($LASTEXITCODE -ne 0)
    {
        throw "APM audit failed (exit $LASTEXITCODE)."
    }

    # Reinstall from scratch so files APM does not own show up as deletions.
    $committedSettings = Get-Content -LiteralPath $settings -Raw
    git ls-files --cached --others --exclude-standard -- $outputs | Remove-Item -Force
    apm install
    if ($LASTEXITCODE -ne 0)
    {
        throw "APM install failed (exit $LASTEXITCODE)."
    }

    # enableAllProjectMcpServers is the only authored setting APM preserves but does not write.
    $expected = $committedSettings | ConvertFrom-Json -AsHashtable
    if ($expected['enableAllProjectMcpServers'] -ne $true)
    {
        throw "$settings must set enableAllProjectMcpServers to true."
    }
    $expected.Remove('enableAllProjectMcpServers')
    $installed = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json -AsHashtable
    if (($expected | ConvertTo-Json -Depth 100) -cne ($installed | ConvertTo-Json -Depth 100))
    {
        throw "$settings differs from the APM install beyond enableAllProjectMcpServers."
    }
    Set-Content -LiteralPath $settings -Value $committedSettings -NoNewline

    $changes = git status --porcelain --untracked-files=all
    if ($changes)
    {
        $changes
        throw 'Committed APM outputs differ from a clean install: run apm install and commit the changes.'
    }
}
finally
{
    Pop-Location
}
