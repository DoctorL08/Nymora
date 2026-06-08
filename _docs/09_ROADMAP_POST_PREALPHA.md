# 🗺️ NYMORA — ROADMAP POST-PRÉ-ALPHA

> **Cadrée le 8 juin 2026** après la pré-alpha fermée réussie (60+ joueurs, retours positifs).
> Remplace `05_Roadmap_V2_Novice.md` comme **workflow actif**. Source de cadrage : `Desktop/cadrage claude.txt`.
> Workflow **brique par brique** conservé et renforcé : SETUP → LIVRAISON → MANIP UNITY → VALIDATION. Rien n'est validé sans le retour de Lorenzo en Play Mode.

---

## 🎯 Cap de cette roadmap

Transformer un combat déjà jouable et apprécié en un jeu **équilibré, propre et élargi** (2v2/3v3, Mac, 6e classe), puis lancer la **comm + Kickstarter**. Cible plateforme : **Windows + Mac**. **Mobile banni** (le jeu ne s'y prête pas).

## 🔒 Décisions verrouillées

- **Quantum reste le moteur combat** (déterminisme déjà écrit, idéal tour-par-tour).
- **Le hub quitte Photon Fusion** → backend WebSocket OVH maison, pour **réaffecter l'offre Free 100 CCU sur Quantum** (combat 20 → 100 CCU).
- **2000 HP** par classe, **sans toucher aux dégâts** (donne de la marge pour temporiser).
- **Refontes de classes (29 mai) = déjà faites.** On finit les **patchs perso** pour « l'équilibre parfait ».
- **Descriptions de sorts minimalistes** = priorité.
- **Localisation EN** non prioritaire (marché FR d'abord).
- **Sonorisation** = backlog, déclenchée par Lorenzo uniquement (musiciens).

---

## 📋 PHASES

### Phase 0 — Housekeeping *(rapide)*
- Commit + push de la **v140** en attente (nerf buffs Soulrender : Peau de Fer 30→10 / Sang Bouillant 30→15 / Frénésie 10%→5%).
- **Archiver `STATUT_ACTUEL.md`** (544 KB de journal) → `STATUT_ARCHIVE_jusqua_8juin2026.md` ; repartir sur un statut léger.
- Roadmap V2 (ce doc) posée comme workflow actif.

### Phase 1 — Patchs perso / « équilibre parfait » ⭐ *(on démarre ici)*
Objectif : appliquer la **patch list MAJEUR** (`Desktop/Patch à faire.txt`) qui doit stabiliser l'équilibrage.
- **2000 HP** toutes classes (sans toucher aux dégâts).
- Spells de survie « -30% HP » → **-50% HP + 2 PA** (uniformiser).
- **Nightseer** : signature + poussée directionnelle (2e clic) · pièges 6 tours (compteur casteur) · pas furtif = pas de bonus portée + 3PA + relance 1t · filet de ronces 1×/tour.
- **Ghostra** : case leurres inciblable · fix TP Voile Spectrale (corner) · leurres subissent poison · fix priorité dorsale éveil spectral · pas dans l'ombre = cible dos · lame spectrale 130 + retourne cible dos.
- **Colossar** : Représaille → survie -50% (⚠️ demander à Lorenzo les valeurs) · renvoi bouclier 2PA + relance 1t + cap 2 · limite 6 piliers/murs (7e détruit le + ancien, n° casteur, murs sans durée) · provocation sans -1PM.
- **Necram** : brumes superposables sans cumul · **audit complet bugs miroir Necram vs Necram** (dégâts plafonnés, ticks fin de tour) **+ vérif préventive bugs miroir TOUTES classes** · brume toxique kick 1PM + 2PA · échange spectral → survie · inoculation 30 · crachat acide 110 · morsure putride 150.
- **Soulrender** : sang bouillant prend dégâts poison sans gagner HG · sang coagulé ne perd plus les PM en passant dessus.
- **Descriptions de sorts → minimalistes** (chantier prioritaire transversal).

### Phase 2 — Mort subite / anti-antijeu
Mécanique pour empêcher les parties qui s'éternisent. Hook : `TurnSystem.EnterTurnStart`.
- **Tour 25**, en **2 paliers** : avertissement, puis mort subite.
- **Poison d'arène** : attrition croissante chaque tour (pas de dégâts ×2).
- **Purge tout le terrain** (piliers/murs/fondations/leurres/pièges/brume) ; **garde les positions** des joueurs.
- À l'entrée : **12 PA / 4 PM** + **ressources de classe maxxées** pour les deux.
- Visuel : **filtre rougeâtre** (pas trop sombre), codé **sans Kyami**.

### Phase 3 — Patchs MINEUR (UI/UX/outils)
- Combat : panneaux haut-G/haut-D → **icônes de malus/bonus** au-dessus du tooltip ; **timeline agrandie avec HP** ; bouton fin de tour plus petit ; chat indique pourquoi un cast est impossible.
- **Spectateur** : ne jamais voir les pièges Nightseer ; cacher PA/PM/abandon/prévisu PM.
- **Replay rewind** + compat replays cross-version.
- Social : amis « toujours déconnecté » ; retour hub à la position d'avant (pas respawn centre).
- SVG propre du redimensionnement chat ; supprimer succès « explorateur ».

### Phase 4 — Migration hub → WebSocket (libérer les CCU)
- Sortir le mouvement/présence du hub de **Photon Fusion** vers le **backend WS OVH** (mutualise l'infra chat).
- **Réaffecter l'offre Free 100 CCU one-app sur Quantum** (combat 20 → 100 CCU).
- Filet matchmaking : gate « max matchs concurrents » + file d'attente (prérequis alpha ouverte).

### Phase 5 — 2v2 / 3v3
- Scènes Quantum séparées (`41_CombatRanked2v2`, `42_CombatRanked3v3`).
- Rotation d'initiative multi-joueurs, matchmaking par équipe, adaptation des modes.

### Phase 6 — 6e classe
- Conception d'une **nouveauté gameplay** équilibrée vis-à-vis des 5 existantes (ressource + passif + 16 sorts + signature).

### Phase 7 — Extension Mac
- Build target macOS, input, packaging, tests.

### Phase 8 — Onboarding + Data
- **Tutoriel** plus complet et meilleur (jamais réellement fini).
- **Admin analytics data-driven** : winrate par classe vs classe, spells/decks les + joués, **résumé auto le dimanche soir** (équilibrage piloté par la data, pas le feeling) ; whitelist par clic ; édition lien Discord/site en jeu.

### Phase 9 — Comm / Kickstarter / Cosmétiques
- **Comm à fond avec Kyami** (objectif 0→500 engagés avant KS).
- **Cosmétiques** garnis par Kyami (lui laisser le temps).
- Lancement KS conditionnel au seuil d'engagement.

### Backlog (hors phase, déclenché par Lorenzo)
- **Refonte sonorisation** (musique + SFX) — quand les musiciens sont prêts. *Ne jamais le proposer ; Lorenzo décide.*
- Sortie de Quantum lui-même (gated sur coût réel, lointain).
- Reliquats art Kyami (normals propres persos/skins/familiers).
- Refacto sorts-move ↔ hooks terrain.

---

## 📌 Notes de méthode

- **Brique par brique** strict. Une brique = une feature, livrée et validée avant la suivante.
- Sur les grosses briques (2v2/3v3, 6e classe, migration hub), je propose un **plan validé AVANT de coder**.
- **Revue de code** (`/code-review`) sur le diff avant chaque commit important, en plus du test Play Mode.
- `CombatRulesVersion` incrémentée à chaque modif combat ; rebuild standalone avant ranked.
- Healthcheck avant commit important.
