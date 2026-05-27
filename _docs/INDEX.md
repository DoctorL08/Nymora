# 📚 Pack documentation Nymora — INDEX

## 🚀 À uploader dans chaque nouvelle conversation Claude

Ces 7 fichiers contiennent **tout le contexte du projet**. Upload-les dans cet ordre dans la nouvelle conv pour que Claude reprenne là où on en est.

| # | Fichier | Rôle | Taille |
|---|---|---|---|
| 0 | **00_README_CLAUDE.md** | 🎯 Briefing complet pour Claude — **À LIRE EN PREMIER** | 12 KB |
| 1 | **STATUT_ACTUEL.md** | 📍 État actif du projet (vivant, à update à chaque session) | 5 KB |
| 2 | **01_BIBLE_V7.1_Combat.md** | ⚔️ Combat, classes, sorts, ressources, signatures | 109 KB |
| 3 | **02_Architecture_Technique.md** | 🏗️ Stack, netcode, backend, sécurité, hosting | 27 KB |
| 4 | **03_GDD_Features.md** | 🎮 14 features (auth, profil, deck builder, chat, shop, BP) | 37 KB |
| 5 | **04_Roadmap_14_mois.md** | 🗺️ Roadmap V1 (vue technique haut niveau, 7 phases) | 30 KB |
| 6 | **05_Roadmap_V2_Novice.md** | 🧱 Roadmap V2 — **WORKFLOW ACTIF** brique par brique | 35 KB |
| 7 | **07_PLAN_COMMUNICATION.md** | 🐯 La Doc du Seigneur de la Seigneurie — comm, réseaux, Kickstarter, plan hebdo | — |
| 8 | **08_KICKSTARTER_A_Z.md** | 🏰 Le Grimoire du Kickstarter — compte, objectif, page, trailer, paliers, légal, jour J, fulfillment | — |

**Total : ~255 KB** — léger, pas de risque de saturer le contexte.

---

## 🔄 Workflow de transmission entre sessions

### Avant de fermer une session Claude
1. Demande à Claude : **"Update le STATUT_ACTUEL.md avec ce qu'on a fait aujourd'hui"**
2. Récupère le fichier mis à jour
3. Remplace l'ancien `STATUT_ACTUEL.md` par le nouveau

### Au début de la session suivante
1. Ouvre une nouvelle conversation Claude
2. Upload les 7 fichiers du pack (drag & drop ou bouton attach)
3. Premier message type :
   ```
   Salut chef. J'ouvre une nouvelle session sur Nymora.
   Lis le 00_README_CLAUDE.md et le STATUT_ACTUEL.md en priorité.
   Une fois fait, dis-moi où on en est et ce qu'on attaque.
   ```

---

## 📌 Hiérarchie des sources de vérité

En cas de conflit entre les docs (ça arrivera après quelques mois) :

1. **STATUT_ACTUEL.md** — écrase tout, c'est le présent
2. **05_Roadmap_V2_Novice.md** — le workflow officiel
3. **01_BIBLE_V7.1_Combat.md** — le gameplay
4. **02_Architecture_Technique.md** + **03_GDD_Features.md** — les specs technique/features
5. **04_Roadmap_14_mois.md** — vue d'ensemble (plus ancien, peut être obsolète)
6. **00_README_CLAUDE.md** — le briefing (rarement obsolète mais à mettre à jour si la stack change)

---

## 🛠️ Maintenance du pack

À mettre à jour :

- **STATUT_ACTUEL.md** → à chaque session
- **00_README_CLAUDE.md** → quand une décision majeure change (ex : changement de stack)
- **05_Roadmap_V2_Novice.md** → quand tu finis une phase (ajout du détail brique de la phase suivante)
- **01 / 02 / 03 / 04** → rarement, uniquement si une refonte design/architecture

---

## 💡 Astuce : versionning des docs

Crée un dossier dans ton projet : `D:\Dev\Nymora\_docs\`

Mets-y tous ces fichiers MD. Commit-les dans le repo Git du projet (pas séparé). Comme ça :
- Historique versionné
- Tu peux faire un diff pour voir ce qui a changé entre 2 sessions
- Tu peux revenir à une version antérieure si tu te plantes
- Les futures Claude peuvent voir l'évolution du projet

```bash
cd D:\Dev\Nymora
mkdir _docs
# Coller les 7 fichiers .md dedans
git add _docs/
git commit -m "docs: initial documentation pack"
git push
```

---

C'est ton package complet chef. Bon jeu. 💪
