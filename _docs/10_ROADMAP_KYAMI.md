# 🎨 NYMORA — ROADMAP KYAMI (Direction Artistique)

> **Cadrée le 8 juin 2026**, à partir de la liste brute de Kyami (`Downloads/roadmap kyami.txt`).
> Piste **art en parallèle** de la roadmap dev (`09_ROADMAP_POST_PREALPHA.md`). Les points de synchro dev sont signalés 🔗.
> Cap : **refonte totale de la DA en pixel art lineless cohérent**, palette stricte, color theory (HUE / ombres) partout.

---

## 🔒 Règle d'or (vaut pour chaque asset)

**Lineless partout. Palette verrouillée. Color theory (HUE, ombres, valeurs) systématique.**
Aucun asset ne part en prod tant qu'il ne respecte pas la charte de **K0**.

---

## 📐 Ordre de priorité

Du plus bloquant (vu à chaque partie) au moins prioritaire (cosmétiques / KS).
Kyami avance **bloc par bloc**, comme le dev : un bloc fini et validé par Lorenzo avant le suivant.

---

### K0 — Fondation DA : la charte visuelle ⭐ *(à faire AVANT tout le reste)*

Le filet de sécurité qui évite de tout refaire 5 fois.
- **Verrouiller la palette** (un PNG de référence + codes couleur).
- Poser les **règles lineless** : épaisseurs, contrastes, gestion des ombres/lumières, HUE shifting.
- **1 perso pilote** : sprite + 1 anim + avatar, en lineless complet → proof of concept validé par Lorenzo **avant** de dérouler sur les 5 classes.

> ❗ Tant que K0 n'est pas validé, rien d'autre ne démarre.

---

### K1 — Cœur visuel jouable *(ce qui se voit à chaque combat)*

- **2 thin tiles** pour la grille des maps combat (à poser en damier).
- **Refonte sprites + animations** des **5 personnages** + leurs familiers existants (lineless).
- **Refonte des avatars** en réutilisant la tête des sprites persos.
- **Polish des tiles** et des **marques au sol**.

🔗 *Dev : aucun blocage, mais c'est ce qui rafraîchit le plus vite le ressenti en jeu.*

---

### K2 — Décors & environnements

- **Map hub** lineless avec décors animés.
- **Map combat 1v1** lineless avec décors animés.
- **Fond d'écran du menu** : logo + décor animé.

🔗 *Maps 2v2 / 3v3 → reportées en **K6** (synchro dev Phase 5).*

---

### K3 — VFX & signatures

- **Polish des icônes de sorts.**
- **VFX des 80 sorts** (remplace le VFX procédural codé actuel — cf chantier Kyami galérait).
- **Signature épique** par personnage (5).

🔗 *Dev : se branche sur les sorts existants, pas de dépendance bloquante.*

---

### K4 — Cosmétiques boutique

- **5 bannières + 1 rare** par personnage.
- **10 bandeaux de clan** (≠ système de ruban ornemental existant, ce sont des cosmétiques).
- **5 émotes + 1 rare** par personnage.
- **1 nouveau skin rare** par personnage.
- **5 nouveaux familiers** pour la boutique.

🔗 *Dev Phase 9 (cosmétiques / monétisation) — peut avancer en avance, Lorenzo branche au fil de l'eau.*

---

### K5 — Nouvelle classe *(à cadrer avec Lorenzo)*

- Sprites, animations, tiles, avatars, marques, VFX, signature de la **6e classe**.

🔗 *Synchro dev Phase 6. Ne démarre que quand le design gameplay de la classe est figé côté Lorenzo.*

---

### K6 — Maps multi

- **Map combat 2v2** lineless décors animés.
- **Map combat 3v3** lineless décors animés.

🔗 *Synchro dev Phase 5 (scènes `41_CombatRanked2v2` / `42_CombatRanked3v3`).*

---

### K7 — Kickstarter *(pas prioritaire)*

- **5 nouveaux skins rares** réservés au Kickstarter.

🔗 *Synchro dev Phase 9 (comm / KS). À garder pour la fin.*

---

## 🗺️ Synchro avec la roadmap dev

| Bloc Kyami | Dépend de / alimente |
|---|---|
| K0 charte | rien — prérequis de tout |
| K1 cœur visuel | indépendant (gain immédiat) |
| K2 décors | indépendant |
| K3 VFX/signatures | sorts existants (80) |
| K4 cosmétiques | Dev Phase 9 |
| K5 6e classe | **Dev Phase 6** (design figé d'abord) |
| K6 maps multi | **Dev Phase 5** (scènes 2v2/3v3) |
| K7 skins KS | **Dev Phase 9** (KS) |

---

## 📌 Notes de méthode

- **Bloc par bloc**, validation Lorenzo entre chaque (même rigueur que le workflow dev).
- Tout asset passe par la **charte K0** avant intégration.
- Les imports combat suivent la calibration connue (PPU 96, pivot custom 0.5/0.1, écrire les `.meta` directement — cf incident skin Ashen).
- Pour un sprite qui rend mal : **re-export propre côté Kyami**, pas de bidouille scale/sortingOrder côté code.
- Décors sur **un PNG par instance** (jamais combiné → casse le tri iso).

---

*Cadrée le 8 juin 2026.*
