<#
.SYNOPSIS
  Compile et installe le plugin HDT "HS Copilot Collector".

.DESCRIPTION
  - Compile src/HsCopilot.Collector en Release (net472, contre la version de HDT installée).
  - Copie la DLL dans %AppData%\HearthstoneDeckTracker\Plugins\HsCopilot\ (dossier surveillé par HDT,
    synchronisé vers <installation HDT>\Plugins au démarrage de HDT).
  - Avec -Enable : active le plugin dans plugins.xml (une copie de sauvegarde est créée).
  HDT doit être fermé pendant l'installation ; redémarrez-le ensuite.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\install-plugin.ps1 -Enable
#>
param(
    [switch]$Enable,
    [string]$HdtInstallDir = (Join-Path $env:LOCALAPPDATA "HearthstoneDeckTracker\current"),
    [string]$HdtDataDir = (Join-Path $env:APPDATA "HearthstoneDeckTracker")
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\HsCopilot.Collector\HsCopilot.Collector.csproj"

if (Get-Process -Name "HearthstoneDeckTracker" -ErrorAction SilentlyContinue) {
    throw "Hearthstone Deck Tracker est en cours d'exécution. Fermez-le avant l'installation."
}
if (-not (Test-Path (Join-Path $HdtInstallDir "HearthstoneDeckTracker.exe"))) {
    throw "HDT introuvable dans '$HdtInstallDir'. Utilisez -HdtInstallDir."
}

Write-Host "Compilation du plugin contre $HdtInstallDir ..."
dotnet build $project -c Release "-p:HDTInstallDir=$HdtInstallDir" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "La compilation a échoué." }

$dll = Join-Path $root "src\HsCopilot.Collector\bin\Release\net472\HsCopilot.Collector.dll"
$target = Join-Path $HdtDataDir "Plugins\HsCopilot"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $dll $target -Force
Write-Host "Plugin copié dans $target"

if ($Enable) {
    $settingsPath = Join-Path $HdtDataDir "plugins.xml"
    $relative = "Plugins/HsCopilot/HsCopilot.Collector.dll"   # chemin relatif au dossier d'installation de HDT
    $name = "HS Copilot Collector"
    if (Test-Path $settingsPath) {
        # La sauvegarde n'est créée qu'une fois : elle conserve l'état d'avant la toute première installation.
        if (-not (Test-Path "$settingsPath.hscopilot-backup")) { Copy-Item $settingsPath "$settingsPath.hscopilot-backup" }
        [xml]$xml = Get-Content $settingsPath -Raw -Encoding UTF8
    } else {
        [xml]$xml = '<?xml version="1.0" encoding="utf-8"?><ArrayOfPluginSettings xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" />'
    }
    $rootNode = $xml.DocumentElement
    $existing = @($rootNode.ChildNodes | Where-Object { $_.Name -eq "PluginSettings" -and $_.Name -ne $null -and $_.SelectSingleNode("Name").InnerText -eq $name })
    foreach ($node in $existing) { [void]$rootNode.RemoveChild($node) }
    $entry = $xml.CreateElement("PluginSettings")
    foreach ($pair in @(@("FileName", $relative), @("IsEnabled", "true"), @("Name", $name))) {
        $child = $xml.CreateElement($pair[0]); $child.InnerText = $pair[1]; [void]$entry.AppendChild($child)
    }
    [void]$rootNode.AppendChild($entry)
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writer = [System.Xml.XmlWriter]::Create($settingsPath, $settings)
    $xml.Save($writer); $writer.Close()
    Write-Host "Plugin activé dans $settingsPath (sauvegarde : plugins.xml.hscopilot-backup)"
} else {
    Write-Host "Activez-le dans HDT : Options > Tracker > Plugins > cocher 'HS Copilot Collector'."
}
Write-Host "Redémarrez Hearthstone Deck Tracker, puis lancez HsCopilot.App."
