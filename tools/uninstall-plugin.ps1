<#
.SYNOPSIS
  Désinstalle le plugin HDT "HS Copilot Collector" (HDT doit être fermé).
#>
param(
    [string]$HdtInstallDir = (Join-Path $env:LOCALAPPDATA "HearthstoneDeckTracker\current"),
    [string]$HdtDataDir = (Join-Path $env:APPDATA "HearthstoneDeckTracker")
)
$ErrorActionPreference = "Stop"
if (Get-Process -Name "HearthstoneDeckTracker" -ErrorAction SilentlyContinue) {
    throw "Hearthstone Deck Tracker est en cours d'exécution. Fermez-le d'abord."
}
foreach ($dir in @((Join-Path $HdtDataDir "Plugins\HsCopilot"), (Join-Path $HdtInstallDir "Plugins\HsCopilot"))) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force; Write-Host "Supprimé : $dir" }
}
$settingsPath = Join-Path $HdtDataDir "plugins.xml"
if (Test-Path $settingsPath) {
    [xml]$xml = Get-Content $settingsPath -Raw -Encoding UTF8
    $nodes = @($xml.DocumentElement.ChildNodes | Where-Object { $_.SelectSingleNode("Name").InnerText -eq "HS Copilot Collector" })
    foreach ($n in $nodes) { [void]$xml.DocumentElement.RemoveChild($n) }
    $xml.Save($settingsPath)
    Write-Host "Entrée retirée de $settingsPath"
}
