# Run tests sequentially by framework to avoid database contention
# SqlStateProvider integration tests use EnsureTableAreCreated() which drops/recreates UDT types.
# Running net8.0 and net10.0 in parallel causes conflicts.

param(
    [switch]$NoBuild,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$projectPath = $PSScriptRoot

Write-Host "Running ResourceWatcher Tests - Sequential Framework Execution" -ForegroundColor Cyan
Write-Host "=" * 60

$buildArgs = @()
if ($NoBuild) {
    $buildArgs += "--no-build"
}

# Run net8.0 tests first
Write-Host "`nRunning net8.0 tests..." -ForegroundColor Yellow
dotnet test --project $projectPath --framework net8.0 --configuration $Configuration @buildArgs
$net8Result = $LASTEXITCODE

# Run net10.0 tests
Write-Host "`nRunning net10.0 tests..." -ForegroundColor Yellow  
dotnet test --project $projectPath --framework net10.0 --configuration $Configuration @buildArgs
$net10Result = $LASTEXITCODE

# Summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "Test Summary:" -ForegroundColor Cyan
Write-Host "  net8.0:  $(if ($net8Result -eq 0) { 'PASSED' } else { 'FAILED' })" -ForegroundColor $(if ($net8Result -eq 0) { 'Green' } else { 'Red' })
Write-Host "  net10.0: $(if ($net10Result -eq 0) { 'PASSED' } else { 'FAILED' })" -ForegroundColor $(if ($net10Result -eq 0) { 'Green' } else { 'Red' })

# Exit with failure if any framework failed
if ($net8Result -ne 0 -or $net10Result -ne 0) {
    exit 1
}
