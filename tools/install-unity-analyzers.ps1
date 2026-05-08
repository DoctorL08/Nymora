# =============================================================================
# Nymora — Installation Microsoft.Unity.Analyzers
# Telecharge le DLL depuis NuGet et l'installe dans Assets/Plugins/Analyzers/
# avec le label "RoslynAnalyzer" (necessaire pour qu'Unity le pick comme analyzer).
#
# Usage : powershell -ExecutionPolicy Bypass -File tools\install-unity-analyzers.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginsDir = Join-Path $repoRoot "Assets\Plugins\Analyzers"
$pkgName = "microsoft.unity.analyzers"

# Hard-coded fallback version (utilisee si la query NuGet echoue)
$fallbackVersion = "1.21.0"

Write-Host "[Nymora] Installation Microsoft.Unity.Analyzers" -ForegroundColor Cyan

# 1) Recuperer la derniere version stable depuis NuGet
$version = $null
try {
    $idx = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$pkgName/index.json" -TimeoutSec 15
    $version = $idx.versions | Where-Object { $_ -notmatch '-' } | Select-Object -Last 1
    Write-Host "  Derniere version stable : $version" -ForegroundColor Green
} catch {
    Write-Host "  [WARN] NuGet API inaccessible, fallback version $fallbackVersion" -ForegroundColor Yellow
    $version = $fallbackVersion
}

# 2) Telecharger le .nupkg (zip)
$tmpRoot = Join-Path $env:TEMP "nymora-analyzer-install"
if (Test-Path $tmpRoot) { Remove-Item $tmpRoot -Recurse -Force }
New-Item -ItemType Directory -Path $tmpRoot -Force | Out-Null

$nupkgPath = Join-Path $tmpRoot "$pkgName.$version.zip"
$nupkgUrl = "https://api.nuget.org/v3-flatcontainer/$pkgName/$version/$pkgName.$version.nupkg"
Write-Host "  Telechargement depuis $nupkgUrl"
Invoke-WebRequest -Uri $nupkgUrl -OutFile $nupkgPath -UseBasicParsing

# 3) Extraire
$extractDir = Join-Path $tmpRoot "extracted"
Expand-Archive -Path $nupkgPath -DestinationPath $extractDir -Force

# 4) Trouver le DLL netstandard2.0
$dllSrc = Get-ChildItem -Path "$extractDir\analyzers\dotnet\cs" -Filter "Microsoft.Unity.Analyzers.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $dllSrc) {
    $dllSrc = Get-ChildItem -Path $extractDir -Filter "Microsoft.Unity.Analyzers.dll" -Recurse | Select-Object -First 1
}
if (-not $dllSrc) {
    Write-Host "[ERREUR] DLL introuvable dans le package. Contenu :" -ForegroundColor Red
    Get-ChildItem $extractDir -Recurse | ForEach-Object { Write-Host "    $_" }
    exit 1
}

# 5) Copier vers Assets/Plugins/Analyzers/
if (-not (Test-Path $pluginsDir)) { New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null }
$dllDst = Join-Path $pluginsDir "Microsoft.Unity.Analyzers.dll"
Copy-Item -Path $dllSrc.FullName -Destination $dllDst -Force
Write-Host "  DLL copie : $dllDst" -ForegroundColor Green

# 6) Generer le .meta avec label RoslynAnalyzer
$guid = [guid]::NewGuid().ToString("N")
$metaPath = "$dllDst.meta"
$metaContent = @"
fileFormatVersion: 2
guid: $guid
labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 1
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData: ''
  assetBundleName: ''
  assetBundleVariant: ''
"@
[System.IO.File]::WriteAllText($metaPath, $metaContent, [System.Text.UTF8Encoding]::new($false))
Write-Host "  Meta cree avec GUID $guid" -ForegroundColor Green

# 7) Cleanup
Remove-Item $tmpRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "[Nymora] Installation terminee. Version : $version" -ForegroundColor Cyan
Write-Host ""
Write-Host "Prochaine etape :" -ForegroundColor Yellow
Write-Host "  1. Reviens dans Unity et fais Ctrl+R"
Write-Host "  2. Verifie le DLL dans Assets/Plugins/Analyzers/Microsoft.Unity.Analyzers.dll"
Write-Host "  3. Dans son Inspector, le label 'RoslynAnalyzer' doit apparaitre"
Write-Host "  4. Tente d'ecrire 'private void Update() { }' (vide) dans un script"
Write-Host "  5. Visual Studio doit afficher le warning UNT0001 'Empty Unity message'"
