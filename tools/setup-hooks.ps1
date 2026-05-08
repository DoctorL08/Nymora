# =============================================================================
# Nymora — Installation des hooks Git
# Copie les hooks versionnes (tools/git-hooks/) vers .git/hooks/
# A relancer apres chaque clone du repo.
# =============================================================================

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repo "tools\git-hooks"
$dst = Join-Path $repo ".git\hooks"

if (-not (Test-Path "$repo\.git")) {
    Write-Host "ERREUR : pas de repo Git detecte a $repo" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $dst)) {
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
}

$hooks = @("commit-msg", "pre-commit")
foreach ($h in $hooks) {
    $srcFile = Join-Path $src $h
    $dstFile = Join-Path $dst $h
    if (Test-Path $srcFile) {
        Copy-Item -Path $srcFile -Destination $dstFile -Force
        Write-Host "  [OK] $h installe" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] $h introuvable dans tools/git-hooks/" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Hooks Git installes dans .git/hooks/" -ForegroundColor Cyan
Write-Host "Pour tester : tente un commit avec un mauvais message ('test')."
