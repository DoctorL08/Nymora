# Nymora — Icônes de Sort Soulrender

Généré le 11 mai 2026 — PixelLab MCP + Claude Sonnet  
**17 icônes** · 128×128px · PNG 32-bit RGBA · Pixel art dark fantasy

---

## Spécifications techniques

| Paramètre | Valeur |
|---|---|
| Résolution | 128×128 px |
| Format | PNG RGBA transparent |
| Style | Pixel art dark fantasy (réf. Blasphemous, Children of Morta) |
| Couleur accent | `#B22222` rouge sang (+ `#d84a42`, `#e85a3c`) |
| Fond | `#0c0b0e` noir profond |
| Palette signature | `#c9a227` or (Âme Lacérée uniquement) |
| Unity import | Filter Mode: Point · Compression: None · PPU: 128 |

---

## Passif

| Fichier | Sort | Description visuelle |
|---|---|---|
| `icon_passif_hemoglyphe.png` | **Hémoglyphe** (Passif) | Rune angulaire rouge sang type toile sur fond sombre, gouttes de sang en bas |

---

## Sorts Offensifs (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_tranche_ame.png` | **Tranche-Âme** | 3 PA | Lame recourbée tranchant une silhouette d'âme spectrale en deux, éclats de sang |
| `icon_ouvre_plaie.png` | **Ouvre-Plaie** | 2 PA | Plaie circulaire béante avec lignes de sang rayonnantes, effet DoT actif |
| `icon_charge_brutale.png` | **Charge Brutale** | 2 PA | Silhouette berserker en plein dash, aura Vapeur Carmin cramoisie dans le sillage |
| `icon_detonation_sanglante.png` | **Détonation Sanglante** | 3 PA | Explosion en croix rouge sang, 4 bras rayonnant du centre AoE |
| `icon_curee.png` | **Curée** | 2 PA | Poing blindé frappant avec éclaboussures de sang, dualité or/rouge |

---

## Sorts Tactiques (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_empoignade.png` | **Empoignade** | 2 PA | Gantelet saisissant une petite cible via des tentacules de sang rouge |
| `icon_pacte_de_sang.png` | **Pacte de Sang** | 1 PA | Lame traversant la paume, énergie rouge montant en flammes, sceau rituel |
| `icon_marque_de_carnage.png` | **Marque de Carnage** | 2 PA | Croix de sang rouge dégoulinante angulaire — correspond exactement au visuel Bible |
| `icon_rugissement.png` | **Rugissement** | 2 PA | Crâne berserker bouche ouverte, cercle d'ondes de choc cramoisies AoE |
| `icon_rage_insatiable.png` | **Rage Insatiable** | 2 PA | Deux poings croisés entourés d'ornements circulaires rouge-sang en boucle infinie |

---

## Sorts de Survie (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_riposte_carmin.png` | **Riposte Carmin** | 2 PA | Vambrace défensif entouré d'épines cramoisies rayonnantes (piège contre-attaque) |
| `icon_cauterisation.png` | **Cautérisation** | 2 PA | Mains tenant des flammes orange-ambre dans un cadre runique sombre |
| `icon_peau_de_fer.png` | **Peau de Fer** | 3 PA | Plastron de métal sombre avec fissures rouge sang entre les plaques d'armure |
| `icon_seve_vive.png` | **Sève Vive** | 2 PA | Gantelet tendant un orbe de vie enflammé dans un cadre organique circulaire |
| `icon_dernier_souffle.png` | **Dernier Souffle** | 4 PA | Silhouette sombre se relevant au centre d'une explosion rouge sang dramatique |

---

## Sort Signature

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_ame_laceree.png` | **Âme Lacérée** ✦ SIGNATURE | 3 PA | Âme spectrale lacérée par des marques cramoisies — **CADRE DORÉ** `#c9a227` sur les bords |

> ✦ Le sort Signature se distingue des autres par son cadre doré. Slot séparé du deck, débloqué à 5/5 HG.

---

## Notes pour Lorenzo

- Cleanup palette recommandé avant intégration Unity (anti-aliasing PixelLab possible sur les bords)
- Icônes à placer dans : `Assets/_Nymora/Art/Sprites/Spells/Soulrender/`
- Naming convention respectée : `icon_<spell_id>.png` en snake_case
- Toutes les icônes sont conformes à la charte Soulrender (#B22222 accent, fond noir profond)
- Âme Lacérée identifiable immédiatement grâce au cadre doré distinctif
