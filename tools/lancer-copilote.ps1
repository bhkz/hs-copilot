<#
.SYNOPSIS
  Lance HS Copilot : fenêtre de diagnostic + overlay de conseils.

.DESCRIPTION
  - Ferme proprement une instance déjà ouverte (ses rapports sont enregistrés à la fermeture).
  - Recompile en Release si l'application n'existe pas, si le code a changé depuis la dernière compilation, ou avec -Rebuild.
  - Vérifie la clé OpenAI (variable OPENAI_API_KEY) et la présence du plugin HDT, sans jamais afficher la clé.
  - Démarre HsCopilot.App. Lance ensuite HDT puis Hearthstone.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\lancer-copilote.ps1
#>
param([switch]$Rebuild)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "src\HsCopilot.App\bin\Release\net10.0-windows\HsCopilot.App.exe"

$running = Get-Process -Name "HsCopilot.App" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Une instance de HS Copilot est ouverte : fermeture propre (rapports enregistrés)..."
    foreach ($p in $running) { [void]$p.CloseMainWindow() }
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Process -Name "HsCopilot.App" -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
    if (Get-Process -Name "HsCopilot.App" -ErrorAction SilentlyContinue) {
        throw "HS Copilot ne s'est pas fermé. Ferme-le à la main puis relance ce script."
    }
}

$stale = $false
if (Test-Path $exe) {
    $built = (Get-Item $exe).LastWriteTime
    $newest = Get-ChildItem (Join-Path $root "src"), (Join-Path $root "config") -Recurse -File -Include *.cs, *.xaml, *.csproj, *.md |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($newest -and $newest.LastWriteTime -gt $built) { $stale = $true }
}

if ($Rebuild -or $stale -or -not (Test-Path $exe)) {
    Write-Host "Compilation de HS Copilot (environ une minute)..."
    dotnet build (Join-Path $root "HsCopilot.sln") -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "La compilation a échoué : relance avec -Rebuild et lis les erreurs ci-dessus." }
}

$key = [Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")
if (-not $key) { $key = $env:OPENAI_API_KEY }
$claude = [Environment]::GetEnvironmentVariable("ANTHROPIC_API_KEY", "User")
if (-not $claude) { $claude = $env:ANTHROPIC_API_KEY }
if ($key) {
    Write-Host ("Clé OpenAI : présente (se termine par " + $key.Substring($key.Length - 4) + ").") -ForegroundColor Green
} else {
    Write-Host "Clé OpenAI : absente (modèles gpt-… indisponibles). Commande : setx OPENAI_API_KEY ""ta-clé"" puis relance ce script."
}
if ($claude) {
    Write-Host ("Clé Anthropic : présente (se termine par " + $claude.Substring($claude.Length - 4) + ").") -ForegroundColor Green
} else {
    Write-Host "Clé Anthropic : absente (modèles claude-… indisponibles). Commande : setx ANTHROPIC_API_KEY ""ta-clé"" puis relance ce script."
}
if (-not $key -and -not $claude) { Write-Warning "Aucune clé : le conseiller restera inactif." }

$plugin = Join-Path $env:APPDATA "HearthstoneDeckTracker\Plugins\HsCopilot\HsCopilot.Collector.dll"
if (Test-Path $plugin) {
    Write-Host "Plugin HDT : installé." -ForegroundColor Green
} else {
    Write-Warning "Plugin HDT absent : HS Copilot lira directement le Power.log. Pour l'installer : tools\install-plugin.ps1 -Enable"
}

Write-Host "Démarrage de HS Copilot. Ensuite : lance HDT, puis Hearthstone en mode fenêtré ou fenêtré sans bordure."
Write-Host "Raccourcis : Ctrl+Alt+H afficher/masquer l'overlay, Ctrl+Alt+R (ou F10) réanalyser, Ctrl+Alt+P pause."
# setx n'atteint pas cette fenêtre si elle était déjà ouverte : les clés enregistrées sont transmises à l'application lancée.
if ($key) { $env:OPENAI_API_KEY = $key }
if ($claude) { $env:ANTHROPIC_API_KEY = $claude }
Start-Process -FilePath $exe -WorkingDirectory $root
