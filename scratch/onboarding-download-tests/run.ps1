[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path

Write-Host "=== Step 1: Build onboarding-download-tests ===" -ForegroundColor Cyan
& dotnet build (Join-Path $scriptDir "onboarding-download-tests.csproj") -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build onboarding-download-tests.csproj failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "`n=== Step 2: Run onboarding-download-tests suite ===" -ForegroundColor Cyan
$testDll = Join-Path $scriptDir "bin\Release\net8.0-windows10.0.19041.0\test_onboarding.dll"
& dotnet $testDll
if ($LASTEXITCODE -ne 0) {
    Write-Error "onboarding-download-tests suite failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "`n=== Step 3: Build SDK-only Fixture for PluginSelfTest ===" -ForegroundColor Cyan
$fixtureCsproj = Join-Path $scriptDir "Fixture\Fixture.csproj"
& dotnet build $fixtureCsproj -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build Fixture.csproj failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "`n=== Step 4: Run PluginSelfTest with SDK-only Fixture ===" -ForegroundColor Cyan
$fixtureDll = Join-Path $scriptDir "Fixture\bin\Release\net8.0-windows\TestFixturePlugin.dll"
$evidenceReport = Join-Path $scriptDir "Fixture\bin\Release\net8.0-windows\selftest-evidence.json"
$starPieDll = Join-Path $repoRoot "WinPieGestures\bin\Release\net8.0-windows10.0.19041.0\StarPie.dll"

if (Test-Path $evidenceReport) {
    Remove-Item $evidenceReport -Force
}

& dotnet $starPieDll --plugin-selftest $fixtureDll $evidenceReport --test-instance --skip-invoke
if ($LASTEXITCODE -ne 0) {
    Write-Error "PluginSelfTest failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

if (-not (Test-Path $evidenceReport)) {
    Write-Error "Expected evidence report was not generated at $evidenceReport"
    exit 1
}

Write-Host "`n=== All Onboarding Download Gates Passed Successfully! ===" -ForegroundColor Green
exit 0
