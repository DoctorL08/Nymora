# Nymora — Icônes de Sort Ghostra

Généré le 15 mai 2026 — PixelLab MCP + Claude Sonnet  
**17 icônes** · 128×128px · PNG 32-bit RGBA · Pixel art dark fantasy

---

## Spécifications techniques

| Paramètre | Valeur |
|---|---|
| Résolution | 128×128 px |
| Format | PNG RGBA transparent |
| Style | Pixel art dark fantasy (réf. Blasphemous, Children of Morta) |
| Couleur accent | `#6F8FA8` → `#9BB8CC` → `#D0E8F5` bleu spectral éthéré |
| Palette spectrale | `#2c3f4f` → `#6f8fa8` → `#9bb8cc` → `#d0e8f5` |
| Ombres profondes | `#392a5e`, `#4a3d6e` violet sombre |
| Fond | `#0c0b0e` / `#121922` noir bleuté |
| Or signature | `#c9a227` (Exécution Spectrale uniquement) |
| Unity import | Filter Mode: Point · Compression: None · PPU: 128 |

---

## Passif

| Fichier | Sort | Description visuelle |
|---|---|---|
| `icon_passif_angle_mort.png` | **L'Angle Mort** (Passif) | Cercle cérémoniel de clones spectraux bleu glacé entourant figure centrale — 3 stades de densité de leurres |

---

## Sorts Offensifs (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_lame_spectrale.png` | **Lame Spectrale** | 3 PA | Lame fantôme bleue translucide en backstab dorsal — +50/80 bonus selon angle |
| `icon_frappe_fantome.png` | **Frappe Fantôme** | 4 PA | Engage par téléport avec traîne spectrale bleue + impact 200 dmg |
| `icon_lame_vorace_spectrale.png` | **Lame Vorace Spectrale** | 3 PA | Lame bleue tranchant plaie ouverte rouge — contraste bleu/rouge du combo |
| `icon_saigne_ame.png` | **Saigne-Âme** | 4 PA | Lame spectrale plongeant dans cible avec "200/70" lisibles — finisher consommant plaie |
| `icon_danse_des_lames.png` | **Danse des Lames** | 5 PA | Étoile de 8 lames spectrales en cyclone AoE — toutes directions touchées |

---

## Sorts Tactiques (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_replique_fantome.png` | **Réplique Fantôme** | 3 PA | Clone spectral lumineux bleu-blanc dans cadre sombre — leurre trompeur parfait |
| `icon_pas_dans_lombre.png` | **Pas dans l'Ombre** | 2 PA | Deux positions reliées par traîne bleue — départ (leurre) + arrivée (téléport) |
| `icon_volte_face.png` | **Volte-Face** | 2 PA | Figure encapuchonnée avec vortex rotatif bleu + flèches circulaires — rotation 180° forcée |
| `icon_dague_lancee.png` | **Dague Lancée** | 1 PA | Dague fantôme en vol avec traîne spectrale + "80 dmg + Turn" |
| `icon_marque_de_lombre.png` | **Marque de l'Ombre** | 2 PA | Corps couvert de sigils bleu-violet brillants — sceau d'amplification +20 dmg |

---

## Sorts de Survie (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_voile_spectral.png` | **Voile Spectral** | 2 PA | Figure balayée par vague spectrale purifiante bleue — DoT effacés instantanément |
| `icon_linceul_dombres.png` | **Linceul d'Ombres** | 3 PA | Masse sombre avec bords bleus acérés comme épines — bouclier 130 HP qui mord |
| `icon_pas_de_lau_dela.png` | **Pas de l'Au-Delà** | 2 PA | Fantôme translucide traversant cible solide avec "60" de dégâts dorsaux |
| `icon_replique_protectrice.png` | **Réplique Protectrice** | 3 PA | Guerrier spectral solide avec bouclier — clone tank 200 HP (plus opaque qu'offensif) |
| `icon_dernier_pas.png` | **Dernier Pas** | 4 PA | Cadre gothique + explosion bleue spectrale — évasion ultime 200 HP + téléport |

---

## Sort Signature

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_execution_spectrale.png` | **Exécution Spectrale** ✦ SIGNATURE | 3 PA | "EXÉCUTION SPECTRALE — 350 DORSAL FATAL" + assassin spectral + **CADRE DORÉ** `#c9a227` |

> ✦ Le sort Signature se distingue par son cadre doré. Coûte 3/3 LEURRES actifs. Inflige 350 dégâts si dorsal — rate et consomme quand même si pas dorsal. Le coup le plus risqué du jeu.

---

## Notes pour Lorenzo

- Cleanup palette recommandé avant intégration Unity
- Icônes à placer dans : `Assets/_Nymora/Art/Sprites/Spells/Ghostra/`
- Naming convention respectée : `icon_<spell_id>.png` en snake_case
- Palette bleu spectral éthéré (#6F8FA8 → #D0E8F5) cohérente sur toutes les icônes
- Exécution Spectrale cadre doré distinctif du sort signature
- `icon_replique_protectrice.png` (clone tank) visuellement plus solide/opaque que `icon_replique_fantome.png` (leurre offensif) — distinction intentionnelle
- Cohérence de style maintenu avec Soulrender, Nightseer, Colossar et Necram
- Ghostra est la 5ème et dernière classe — **PROJET COMPLET**
