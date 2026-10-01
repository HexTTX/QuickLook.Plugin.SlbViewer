param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

Write-Host "Building QuickLook.Plugin.SlbViewer ($Configuration)..." -ForegroundColor Cyan
dotnet build QuickLook.Plugin.SlbViewer.csproj -c $Configuration

$outDir = Join-Path $scriptDir "bin\$Configuration"
$pluginName = "QuickLook.Plugin.SlbViewer"
$zipPath = Join-Path $scriptDir "$pluginName.zip"
$qlpluginPath = Join-Path $scriptDir "$pluginName.qlplugin"

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
if (Test-Path $qlpluginPath) { Remove-Item $qlpluginPath -Force }

Write-Host "Packaging $pluginName.qlplugin..." -ForegroundColor Cyan

$filesToPack = Get-ChildItem -Path $outDir -Include @("QuickLook.Plugin.*.dll", "QuickLook.Plugin.Metadata.config", "*.config") -Recurse | Where-Object { $_.Name -notmatch "QuickLook\.Common\.dll" }

Compress-Archive -Path $filesToPack.FullName -DestinationPath $zipPath -Force
Move-Item -Path $zipPath -Destination $qlpluginPath -Force

Write-Host "Successfully generated: $qlpluginPath" -ForegroundColor Green
Get-Item $qlpluginPath | Select-Object Name, Length, LastWriteTime
