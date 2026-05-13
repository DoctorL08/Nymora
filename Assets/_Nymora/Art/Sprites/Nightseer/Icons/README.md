# Nymora — Icônes de Sort Nightseer

Généré le 13 mai 2026 — PixelLab MCP + Claude Sonnet  
**17 icônes** · 128×128px · PNG 32-bit RGBA · Pixel art dark fantasy

---

## Spécifications techniques

| Paramètre | Valeur |
|---|---|
| Résolution | 128×128 px |
| Format | PNG RGBA transparent |
| Style | Pixel art dark fantasy (réf. Blasphemous, Children of Morta) |
| Couleur accent | `#6A4FB6` violet mystique (+ `#8f7ad4`, `#c4b6f0`) |
| Fond | `#0c0b0e` / `#121922` noir-bleu nuit |
| Palette signature | `#c9a227` or (Traquenard uniquement) |
| Unity import | Filter Mode: Point · Compression: None · PPU: 128 |

---

## Passif

| Fichier | Sort | Description visuelle |
|---|---|---|
| `icon_passif_oeil_qui_nest_pas.png` | **L'Œil Qui N'Est Pas** (Passif) | Grand œil mystique violet omniscient entouré de 3 runes de marques (réticule, brume, empreinte) |

---

## Sorts Offensifs (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_tir_precis.png` | **Tir Précis** | 3 PA | Flèche enchantée à pointe violette avec réticule de visée à longue portée |
| `icon_volee_depines.png` | **Volée d'Épines** | 4 PA | Trois flèches d'épines en ligne + filet de ronces au sol en fin de trajectoire |
| `icon_detonation_onirique.png` | **Détonation Onirique** | 4 PA | Explosion onirique violette fragmentée en zone 2x2, particules éthérées déchirant le voile |
| `icon_frappe_de_lombre.png` | **Frappe de l'Ombre** | 4 PA | Silhouette encapuchonnée frappant une cible en mouvement (empreintes visibles) |
| `icon_salve_mortelle.png` | **Salve Mortelle** | 5 PA | Croix de tirs violets convergeant de 4 directions, barrage qui déchire tous les voiles |

---

## Sorts Tactiques (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_marque_du_chasseur.png` | **Marque du Chasseur** | 1 PA | Œil/réticule de visée violet avec lignes runiques et silhouette de proie marquée |
| `icon_filet_de_ronces.png` | **Filet de Ronces** | 2 PA | Réseau de ronces sombres à demi-voilé sous brume violet-pourpre |
| `icon_champ_de_mines.png` | **Champ de Mines** | 4 PA | Trois mines violettes en triangle, chacune à demi-cachée dans le brouillard d'ombre |
| `icon_bourrasque.png` | **Bourrasque** | 3 PA | Silhouette ennemie projetée latéralement par un vortex de vent violet-indigo |
| `icon_souffle_glacial.png` | **Souffle Glacial** | 3 PA | Cristaux de givre bleu-violet spiralant en croix défensive — anti-mêlée |

---

## Sorts de Survie (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_voile_dombre.png` | **Voile d'Ombre** | 3 PA | Figure encapuchonnée se dissolvant en brume violette, afterglow spectral restant |
| `icon_pas_furtif.png` | **Pas Furtif** | 2 PA | Piste d'empreintes violettes menant vers une case Voilée en brume |
| `icon_camouflage_ronces.png` | **Camouflage Ronces** | 3 PA | Bouclier circulaire de ronces épineuses avec lumière violette filtrant à travers |
| `icon_seve_sauvage.png` | **Sève Sauvage** | 3 PA | Racines/ronces alimentant un orbe de soin violet — conditionnel au réseau de pièges |
| `icon_evanescence.png` | **Évanescence** | 4 PA | Figure explosant en éclats de lumière violet-pourpre, case quittée en brume Voilée |

---

## Sort Signature

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_traquenard.png` | **Traquenard** ✦ SIGNATURE | 2 PA | Œil mystique violet, chaînes de paralysie ligorant l'ennemi — **CADRE DORÉ** `#c9a227` |

> ✦ Le sort Signature se distingue par son cadre doré. Débloqué à 4/4 PR. Téléportation + 280 dégâts + Paralysie.

---

## Notes pour Lorenzo

- Cleanup palette recommandé avant intégration Unity (anti-aliasing PixelLab sur les bords)
- Icônes à placer dans : `Assets/_Nymora/Art/Sprites/Spells/Nightseer/`
- Naming convention respectée : `icon_<spell_id>.png` en snake_case
- Toutes les icônes sont conformes à la charte Nightseer (#6A4FB6 violet, fond noir-bleu #121922)
- Traquenard distingué par cadre doré — visuellement asymétrique au Tranche-Âme du Soulrender
- Cohérence de style maintenue entre les deux classes (pixel art dark fantasy, Blasphemous-inspired)
