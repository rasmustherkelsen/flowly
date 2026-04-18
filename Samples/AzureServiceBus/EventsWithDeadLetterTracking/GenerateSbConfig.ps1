# Builds and installs the latest Flowly.Tool globally, then generates sbconfig.json
# from both the Sender and Receiver projects.
#
# Run once before starting the emulator:
#   ./GenerateSbConfig.ps1
#
# Re-run whenever message contracts change.

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path "$scriptDir/../../.."
$toolProject = Join-Path $repoRoot "Flowly.Tool/Flowly.Tool.csproj"
$packOutput = Join-Path $repoRoot "Flowly.Tool/bin/Release"

Write-Host "Packing Flowly.Tool..."
dotnet pack $toolProject -c Release --nologo -o $packOutput -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installing Flowly.Tool globally..."
dotnet tool uninstall --global Flowly.Tool 2>$null
dotnet tool install --global Flowly.Tool --add-source $packOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Generating sbconfig.json from Sender, Receiver and ReceiverNoRetry..."
flowly azure-service-bus emulator-config `
    --project "$scriptDir/Sender" `
    --project "$scriptDir/Receiver" `
    --project "$scriptDir/ReceiverNoRetry" `
    --namespace "sbemulatorns" `
    --output "$scriptDir/sbconfig.json"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done. sbconfig.json has been written to $scriptDir."
