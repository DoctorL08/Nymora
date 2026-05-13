# Nymora — Icônes de Sort Colossar

Généré le 13 mai 2026 — PixelLab MCP + Claude Sonnet  
**17 icônes** · 128×128px · PNG 32-bit RGBA · Pixel art dark fantasy

---

## Spécifications techniques

| Paramètre | Valeur |
|---|---|
| Résolution | 128×128 px |
| Format | PNG RGBA transparent |
| Style | Pixel art dark fantasy (réf. Blasphemous, Children of Morta) |
| Couleur accent | `#7A6B5C` pierre/terre ocre |
| Palette pierre | `#453d35` → `#7a6b5c` → `#9e8f7e` → `#c4b5a4` → `#e8d9c4` |
| Fissures lumineuses | `#8a6b18`, `#c9a227` ambré/or |
| Fond | `#0c0b0e` / `#1f1c18` noir chaud |
| Or signature | `#c9a227` (Effondrement uniquement) |
| Unity import | Filter Mode: Point · Compression: None · PPU: 128 |

---

## Passif

| Fichier | Sort | Description visuelle |
|---|---|---|
| `icon_passif_densite_inerte.png` | **Densité Inerte** (Passif) | Trois piliers de pierre avec fissures ambrées en triangle — fortification passive |

---

## Sorts Offensifs (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_frappe_lourde.png` | **Frappe Lourde** | 3 PA | Poing/colonne de pierre massive frappant avec cible épinglée contre mur |
| `icon_onde_de_choc.png` | **Onde de Choc** | 3 PA | Onde sismique ambrée en croix 4 directions, ennemis projetés contre murs |
| `icon_marteau_punisseur.png` | **Marteau Punisseur** | 4 PA | Marteau de pierre avec fissures écrasant une cible anti-caster |
| `icon_choc_sismique.png` | **Choc Sismique** | 4 PA | Fissure sismique en ligne droite traversant un pilier de pierre |
| `icon_represailles.png` | **Représailles** | 3 PA | Poing de pierre avec bouclier ambré, frappe renvoyée vers l'attaquant |

---

## Sorts Tactiques (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_pilier.png` | **Pilier** | 3 PA | Unique pilier de pierre 200 HP se dressant, fissures de lave ambrées |
| `icon_mur_de_pierre.png` | **Mur de Pierre** | 4 PA | 6 blocs de pierre formant un mur avec joints ambrés — séparateur de map |
| `icon_ancrage.png` | **Ancrage** | 2 PA | Ennemi enraciné dans des chaînes de pierre, immobilisé au sol |
| `icon_provocation.png` | **Provocation** | 2 PA | Poing colossal beckoning, petite silhouette attirée de force vers lui |
| `icon_brisure.png` | **Brisure** | 3 PA | Poing de pierre brisant un bouclier magique en éclats, cible exposée |

---

## Sorts de Survie (5)

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_stoicisme.png` | **Stoïcisme** | 3 PA | Golem de pierre planté dans le sol avec bouclier rocheux, immovable |
| `icon_garde_protectrice.png` | **Garde Protectrice** | 2 PA | Armure de pierre avec flèches ricochant dessus (-30% shield) |
| `icon_ressac_vital.png` | **Ressac Vital** | 2 PA | Corps de pierre fissuré se refermant avec éclat ambré — soin réactif |
| `icon_renvoi_du_bouclier.png` | **Renvoi du Bouclier** | 3 PA | Bouclier miroir renvoyant flèches ET lames vers leurs attaquants |
| `icon_soin_lourd.png` | **Soin Lourd** | 3 PA | Poing de pierre tendu vers allié, lumière de soin ambrée — heal cross-classe |

---

## Sort Signature

| Fichier | Sort | PA | Description visuelle |
|---|---|---|---|
| `icon_effondrement.png` | **Effondrement** ✦ SIGNATURE | 4 PA | Sol se fracturant massivement avec fissures ambrées/dorées explosant — **GLOW OR** `#c9a227` |

> ✦ Le sort Signature se distingue par son glow or spectaculaire. Débloqué à 3/3 FD. AoE rayon 2, 200 dégâts + éjection, zones impraticables 2 tours. Annoncé 1 tour à l'avance.

---

## Notes pour Lorenzo

- Cleanup palette recommandé avant intégration Unity
- Icônes à placer dans : `Assets/_Nymora/Art/Sprites/Spells/Colossar/`
- Naming convention respectée : `icon_<spell_id>.png` en snake_case
- Palette pierre/terre (#7A6B5C accent, tons chauds) cohérente sur toutes les icônes
- Effondrement glow doré distinctif du sort signature
- `icon_soin_lourd.png` est le SEUL heal cross-classe du jeu — peut nécessiter un visuel distinctif en 2v2/3v3
- Cohérence de style maintenu avec Soulrender et Nightseer (pixel art dark fantasy Blasphemous-inspired)
