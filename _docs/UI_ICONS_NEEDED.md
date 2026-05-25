# 🎨 Icônes UI — Menu « Échap » (refonte M0→M8)

> **MAJ 25 mai 2026 :** décision Lorenzo — icônes faites **en SVG par Claude** (package
> `com.unity.vectorgraphics` ajouté), sauf les **monnaies** = visuels existants de Lorenzo
> (**ash** = Nymos, **blood** = Shards). Voir « État » en bas.



> Scan du code du menu (`Assets/_Nymora/Scripts/Hub/Menu/`) au 25 mai 2026.
> Liste tout ce qui est aujourd'hui **placeholder** (carré coloré dessiné en code) ou **glyphe Unicode**
> (◆ ◇ ✓ ○ ● ‹ ›) et qui gagnerait à devenir une vraie icône.
>
> **Convention de livraison :**
> - Format **PNG transparent**, carré, exporté **propre** (cf règle : pas de bidouille scale côté code).
> - Dépôt dans **`Assets/_Nymora/Art/UI/Icons/`** (le dossier existe déjà).
> - Nommage : **`ui_icon_<nom>.png`** (aligné sur `ui_cadre_*` / `ui_image_*` existants).
> - Style : **monochrome / line clair** cohérent avec la DA du menu (monochrome, font Ari, coins arrondis), couleur neutre claire (la teinte active/inactive est gérée par code).
> - Une fois livrées, **je les câble** (la fabrique `HubMenuUIFactory` pose aujourd'hui un placeholder ; les glyphes seront remplacés par des `Image`).

---

## 🟥 P1 — Nécessaires (placeholders visibles actuellement)

### Barre haute — onglets (5)  · taille cible **38×38** (`TabIconSize`), monochrome
Actuellement : carré plein gris (placeholder `MakeTabButton`).
| Fichier | Onglet | Idée visuelle |
|---|---|---|
| `ui_icon_social.png` | Social | deux silhouettes / bulle de chat |
| `ui_icon_progression.png` | Progression | trophée ou étoile/graphique |
| `ui_icon_settings.png` | Paramètres | engrenage |
| `ui_icon_report_bug.png` | Report bug | insecte (bug) |
| `ui_icon_logout.png` | Déconnexion | porte + flèche sortante |

### Monnaies (2)  · taille cible **~32×32**
Actuellement : glyphes texte `◆` (doré) / `◇` (cyan). Utilisées dans le wallet hub, la boutique, les récompenses (quêtes / Battle Pass).
| Fichier | Monnaie | Idée visuelle |
|---|---|---|
| `ui_icon_nymos.png` | Nymos | pièce / monnaie dorée |
| `ui_icon_shards.png` | Shards | éclat / gemme bleu glace |

### Cadenas (1)  · taille cible **~24×24**
Actuellement : texte « Verrouillé » / « Premium ». Pour le Battle Pass (paliers verrouillés / piste premium) et tout contenu locké.
| Fichier | Usage | Idée visuelle |
|---|---|---|
| `ui_icon_lock.png` | Verrouillé / Premium | cadenas fermé |

---

## 🟦 P2 — Confort (remplacent des glyphes, optionnel)

### Pastilles d'état (3)  · **~22×22**
Aujourd'hui en glyphes Unicode (rendus par la font, acceptables mais inégaux).
| Fichier | Usage actuel | Glyphe remplacé |
|---|---|---|
| `ui_icon_check.png` | Réclamé / Possédé / Équipé | `✓` |
| `ui_icon_achievement_locked.png` | Succès verrouillé | `○` |
| `ui_icon_premium.png` | Tag premium Battle Pass | `●` |

### Onglets Cosmétique / catégories Boutique (4)  · **~28×28**
Aujourd'hui texte seul. Icônes par type = plus lisible.
`ui_icon_type_skin.png` (skins) · `ui_icon_type_pet.png` (familiers) · `ui_icon_type_title.png` (titres) · `ui_icon_type_banner.png` (bannières) · (option `ui_icon_type_emote.png` pour les emotes boutique)

---

## ✅ Déjà couvert (NE PAS reproduire)
- **Illustrations de cartes** (accueil + modes Arène) : `Art/UI/MenuCards/ui_cadre_*` (arene / personnage / battlepass / boutique / 2vs2 / 3vs3) + `ui_image_*` (classes). Ce sont des illustrations plein cadre, pas des icônes.
- **Bouton hamburger ☰** : dessiné en code (3 barres) — OK, pas besoin d'asset (icône possible plus tard si souhaité).
- **Pastille online ami** (rond plein vert/gris) : générée en code — OK.
- **Chevrons ‹ ›** (sélecteurs résolution/classe + bouton Retour) : glyphes, restent en texte (OK).
- **Cadres de rareté boutique** : couleur (or/violet/bleu/gris), pas d'icône.

---

## 📌 Récap priorité
1. **8 icônes P1** : 5 onglets + 2 monnaies + cadenas → débloquent le rendu « propre » du menu.
2. **P2** au fil de l'eau (états + types cosmétiques).

> Quand Kyami dépose les PNG dans `Art/UI/Icons/`, ping-moi : je les importe (sprite, transparent) et je les câble dans `HubMenuUIFactory` / les écrans concernés (remplacement des placeholders + glyphes).

---

## 🟢 ÉTAT (25 mai 2026)

**Fait — SVG créés par Claude** dans `Assets/_Nymora/Resources/UI/Icons/` (style line monochrome blanc, teinté par code) :
`ui_icon_social` · `ui_icon_progression` · `ui_icon_settings` · `ui_icon_report_bug` · `ui_icon_logout`
· `ui_icon_lock` · `ui_icon_check` · `ui_icon_premium`
· `ui_icon_type_skin` · `ui_icon_type_pet` · `ui_icon_type_title` · `ui_icon_type_banner` · `ui_icon_type_emote`

Package `com.unity.vectorgraphics` ajouté au manifest (import SVG → Sprite).
**Câblé :** les 5 icônes d'onglets de la barre haute (`HubMenuShell.AddTab` via `HubMenuUIFactory.LoadIcon`).
**Pas encore câblé** (icônes prêtes) : lock/check/premium (remplacent des glyphes), icônes de type cosmétique.

**À FAIRE PAR LORENZO — monnaies (ash / blood) :**
- Déposer `ash_shard.png` → `Assets/_Nymora/Resources/UI/Icons/ui_icon_nymos.png`
- Déposer `blood_shard.png` → `Assets/_Nymora/Resources/UI/Icons/ui_icon_shards.png`
- Puis ping Claude → câblage du wallet hub + boutique + récompenses (Nymos=ash, Shards=blood).
