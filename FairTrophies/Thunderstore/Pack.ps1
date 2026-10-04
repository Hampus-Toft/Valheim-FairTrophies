param(
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [Parameter(Mandatory = $true)][string]$TargetPath,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [Parameter(Mandatory = $true)][string]$Namespace
)

$ErrorActionPreference = "Stop"

# PluginVersion in FairTrophiesPlugin.cs is the single source of truth for the mod's version, so the
# Thunderstore listing can never drift from the version BepInEx actually loads.
$pluginCsPath = Join-Path $ProjectDir "FairTrophiesPlugin.cs"
$pluginCs = Get-Content $pluginCsPath -Raw
$versionMatch = [regex]::Match($pluginCs, 'PluginVersion\s*=\s*"([^"]+)"')
if (-not $versionMatch.Success) {
    throw "Could not find PluginVersion in $pluginCsPath"
}
$version = $versionMatch.Groups[1].Value

$thunderstoreDir = Join-Path $ProjectDir "Thunderstore"
$templatePath = Join-Path $thunderstoreDir "manifest.template.json"
$manifestJson = (Get-Content $templatePath -Raw).Replace("__VERSION__", $version)
$manifestObj = $manifestJson | ConvertFrom-Json

$stagingDir = Join-Path $OutDir "staging"
if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir | Out-Null

# Thunderstore rejects a UTF-8 BOM in manifest.json, so write the bytes explicitly.
[System.IO.File]::WriteAllText((Join-Path $stagingDir "manifest.json"), $manifestJson, (New-Object System.Text.UTF8Encoding $false))
Copy-Item (Join-Path $thunderstoreDir "icon.png") $stagingDir
Copy-Item (Join-Path $thunderstoreDir "README.md") $stagingDir
Copy-Item (Join-Path $thunderstoreDir "CHANGELOG.md") $stagingDir
Copy-Item $TargetPath $stagingDir

$zipName = "$Namespace-$($manifestObj.name)-$version.zip"
$zipPath = Join-Path $OutDir $zipName
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath

Write-Host "Thunderstore package written to $zipPath (version $version)"
