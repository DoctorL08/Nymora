# 📍 STATUT ACTUEL — NYMORA

> Source de vérité du présent. À garder **léger** (l'historique d'avant le 8 juin 2026 est dans `STATUT_ARCHIVE_jusqua_8juin2026.md`).
> Workflow actif : **`09_ROADMAP_POST_PREALPHA.md`**.

---

## Où on en est (8 juin 2026)

- **Pré-alpha fermée terminée et réussie** : 60+ joueurs enregistrés, retours positifs.
- Version client **0.1.19** · **CombatRulesVersion 154** · Bible V7.1.
- 5 classes complètes (80 sorts), hub/social, ranked 1v1, spectateur live, replay, méta-progression (deck builder, 100 succès, BP 100 tiers, shop) : **livrés et en prod**.
- Backend prod OVH `api.nymora.fr` opérationnel.
- Healthcheck : 0 erreur / 0 warning.

## Nouvelle roadmap cadrée (cf `09_ROADMAP_POST_PREALPHA.md`)

Phase 0 housekeeping → **Phase 1 patchs perso (équilibre parfait)** → Phase 2 mort subite → Phase 3 patchs mineur UI → Phase 4 migration hub WS (libère CCU) → Phase 5 2v2/3v3 → Phase 6 6e classe → Phase 7 Mac → Phase 8 tuto + analytics → Phase 9 comm/KS/cosmétiques. Backlog : sonorisation (Lorenzo décide).

## Travaux récents (8 juin) — grosse session, v140 → v154, tout commité

**Phase 1 « patchs perso » TERMINÉE** (toute la patch list `Desktop/Patch à faire.txt` hors items MINEUR) :
- **v141-143 Necram** : fix miroir (densité venin par-Necram), dégâts directs (Crachat 100 / Morsure 120+10 / Inoculation 30), Brume Toxique miroir complète (2PA/2t, superposable owner-mask, kick PM owner-based), Échange Spectral → Survie.
- **v144** : toutes classes **1500 → 2000 HP** (dégâts inchangés).
- **v145 Soulrender** : Sang Bouillant déclenche sur poison + Vapeur Carmin owner-immune (sim + prévisu).
- **v146-147 Ghostra** : Pas dans l'Ombre dos, Lame Spectrale 130 + retourne dos, Éveil priorise leurre dorsal, Voile anti-corner. **+ View** : anim d'attaque + facing parfait des leurres (NE controllers à assigner dans les scènes combat).
- **v148-150 Nightseer** : Filet 1×/tour, Pas Furtif 3PA+relance, pièges 6 tours + compteur casteur, signature Traquenard refonte (poussée 2 cases 2-clics + NS prend la case d'origine).
- **v151-153 Colossar** : Provocation sans -1PM, Renvoi Bouclier 2PA/relance/cap2, Représailles → survie (heal 200 + riposte, <50%PV, 1×/match), Piliers/Murs **cap 6 cases** (n° d'ordre casteur, mur persistant).
- **v154 ligne 7** : les 5 sorts panic low-HP (Dernier Souffle/Évanescence/Dernier Pas/Cocon Putride/Représailles) uniformisés à **<50% PV + 2 PA**.
- **Descriptions minimalistes** : les **80 sorts** réécrits (1 effet/ligne, `\n`, sans PA/portée, limites en dernière ligne). Source unique `SpellBibleTexts` → deck builder + tooltip combat.

## Travaux 9 juin — Phase 3 « patchs MINEUR » TERMINÉE

Toute la liste MINEUR (`Desktop/Patch à faire.txt`) traitée. **Pur View** (aucun bump CombatRulesVersion) sauf backend.

**Combat (client, validé) :** panneaux haut G/D → HP sous portraits timeline + chips de statuts (tous états, venins inclus) + tooltip méga-complet ; bouton Fin de tour milieu-droit ; chat indique pourquoi un sort n'est pas castable ; handle de resize chat en SVG.
**Backend (déployé prod + validé) :** succès Explorateur retiré (100→99) ; admin = matrice winrate matchups + résumé hebdo auto (dimanche soir) + sorts/decks les plus joués (deck enregistré par match) ; lien Discord éditable depuis l'admin → **source unique site nymora.fr (live) + jeu**.
**Validés en jeu :** amis online (fix HubChatClient), retour hub à la position d'avant combat, replay rewind (scrubber), Discord en jeu.
**Décision :** replay cross-version = **on garde le verrou CombatRulesVersion** (pas de replay « état », gros chantier non justifié).

### ⏳ Reliquat à TESTER (cf mémoire `project_tests_reliquat`) — me rappeler chaque session
- **B Spectateur** : ne voit plus PA/PM, prévisu PM, bouton Abandonner, pièges Nightseer.
- **I deck ranked** : après un match ranked, l'admin « sorts/decks les plus joués » se remplit.
- ~~**F overlay signature**~~ ✅ testé et fonctionnel (9 juin).

> Note workflow : Lorenzo connaît les rebuild standalone — **ne plus les lui demander** sauf si tu veux qu'il teste un truc précis sur le moment.

## ⚠️ En attente / à faire

- ✅ Populate Spell Catalog **fait** + controllers NE des leurres **assignés** (9 juin).
- Reliquat mineur : exemption Vapeur Carmin owner-immune aussi côté IA (`AISystem`) ; IA traite encore Représailles comme une attaque (à réajuster).
- **Patch list MINEUR : TERMINÉE** (cf section 9 juin) ; reste juste 3 tests reliquat ci-dessus.

## Phase 5 — 2v2 / 3v3 (en cours, démarrée 9 juin)

Décision : **2v2/3v3 d'abord, mort subite (Phase 2) APRÈS** (intégration différente en multi). Design verrouillé cf `09_ROADMAP_POST_PREALPHA` + mémoire `project_2v2_3v3_design_decisions` (alternance stricte équipes + ordre voté par capitaine, pas de tir allié, soutien self-only, cadavre-obstacle, classes uniques/équipe, maps irrégulières bord-only, files solo/premade fusionnables, ELO perso + MMR moyen).

Découpage : **5.1 fondations équipe** → 5.2 rotation N-joueurs + ordre voté → 5.3 cadavre-obstacle + déco/joueur → 5.4 grille agrandie + maps irrégulières + éditeur → 5.5 scène 41_CombatRanked2v2 + spawn + HUD → 5.6 pré-combat vote capitaine → 5.7 matchmaking 2v2 (backend) → 5.8 polish. Puis 3v3 (réutilise 5.1→5.3).

- ✅ **5.1 livrée + commit `9eb8b9c`** (v155) : champ `Combatant.TeamId` + `RuntimePlayer.TeamId` + helper central `TeamHelper` ; victoire = « dernière équipe debout » ; **tir allié OFF** (les ~80 call-sites ennemi/allié de la sim convertis ; brume Toxique re-clée sur TeamId). INVARIANT 1v1 : team == slot == PlayerIndex → **validé identique en IA**. Le tir allié ne s'observera qu'en 2v2 (brique 5.5).
  - À confirmer au playtest 2v2 : murs d'un Colossar allié ne bloquent pas ta LoS ; allié dans une AoE = immunisé total.
- ✅ **5.2 livrée + commit** (v156) : `CombatState.PlayerCount` dynamique + `TurnOrder[6]` + `StartingTeam` + `TurnOrderBuilt` ; `RuntimeConfig.PlayerCount` (bootstrap) ; `RuntimePlayer.TeamOrder` + `Combatant.TeamOrder` (rang voté, défaut PlayerIndex). Rotation = alternance stricte entre équipes via `TurnOrder` ; la FSM attend que tous les combattants soient spawnés avant de juger forfait/MatchEnd. `TurnConstants.MaxPlayers=6` borne les scans de commandes. INVARIANT 1v1 : même draw RNG → **validé identique en IA**. Ordre voté testable réellement en 2v2 (5.5/5.6).

- ✅ **5.3 livrée + commit** (v157) : cadavre-obstacle (mort jamais détruite, garde sa case → bloque mouvement/pathfinding déjà ; LoS bloque maintenant aussi sur cadavre, neutre) ; forfait/déco = **KO du joueur** (pas l'équipe) → `EvaluateTeamMatchEnd` décide ; `EnterTurnStart` **saute le sous-tour d'un KO**. **+ fix intro 5.2** : `ActivePlayerIndex` provisoire = `StartingTeam` à l'OnInit (l'intro « pile ou face » lisait un placeholder 0 → annonçait le mauvais démarreur). Validé 1v1 IA + intro correcte.

- ✅ **5.4a livrée + commit** (v158) : grille MAX **15×15 (225)** (stride/array), dims logiques par mode dans `GridSingleton` (1v1 10 / 2v2 12 / 3v3 15) via `RuntimeConfig.PlayerCount` ; `Walkable` actif seulement dans la zone logique ; `TargetingResolver` + View (`GridRenderer`) sur dims logiques. INVARIANT 1v1 : zone 10×10 mêmes coords → **identique**. Reste 5.4b (MapAsset Quantum : masque irrégulier + spawns) → 5.4c (éditeur) → 5.4d (View forme irrégulière).

- ✅ **5.4b livrée + commit** (v159) : `AssetObject NymoraCombatMap` (dims + masque Walkable irrégulier + spawns par équipe/rang) référencé par `RuntimeConfig.CombatMap` (AssetRef = GUID synchronisé → déterministe). `GridSystem` applique la forme si map présente (sinon rectangle), `CombatantSystem` spawn aux points (Team,Rank). Dormant en 1v1 (fallback) → identique. Pas de `.qtn` (C# pur). Compile OK + 1v1 IA OK.

- ✅ **5.4c livrée + commit** : éditeur `Nymora > Combat > Map Editor` (peindre la forme Walkable + placer les spawns par équipe/rang → asset `NymoraCombatMap`). **Map `Assets/CombatMap_2v2.asset` dessinée par Lorenzo** (12×12). Outil éditeur pur (pas de runtime/version).

- ✅ **5.4d livrée + commit** : `GridRenderer` n'instancie que les cases walkable (`GridHelpers.IsWalkable`) → rend la forme irrégulière. View-only, 1v1 identique (rectangle plein). **Brique 5.4 COMPLÈTE** (a grille / b MapAsset / c éditeur / d rendu).

## Phase 5 brique 5.5 (scène 2v2 hot-seat) — ✅ COMPLÈTE & VALIDÉE (commits 77c27f3 / 8faf6d2 / bda59ff)

**Sous-découpage 5.5 :** 5.5a bootstrap ✅ → 5.5b input hot-seat ✅ → 5.5c scène ✅ → 5.5d HUD N portraits + ordre de jeu ✅ → 5.5e barre par-classe + previews suivant l'actif ✅. **CombatRulesVersion = 160** (seul bump = fix spawn cap ; tout le reste = pur View / read-only).

**Bilan 5.5 (jouable en hot-seat local, scène 41) :** 4 combattants spawnent par (Team,Rank) ; input pilote le joueur actif ; ordre A0/B0/A1/B1 (alternance stricte, timeline ordonnée par TurnOrder) ; HUD N portraits teintés par équipe ; chaque joueur caste SON deck (cache par joueur) + déplacement/zone de sort suivent l'actif.

**Prochaine brique : 5.6 — pré-combat vote capitaine** (l'ordre intra-équipe `TeamOrder` est déjà câblé bout-en-bout, défaut = PlayerIndex ; reste l'UI de vote). Puis 5.7 matchmaking 2v2 (backend) → 5.8 polish.
⚠️ Reliquat connu hot-seat (non bloquant) : visibilité brouillard / pièges Nightseer utilise encore « slot 0 = moi » (`LocalPlayerResolver.LocalOwns`) → à adapter à l'actif si gênant en playtest.

**Reprise 9 juin (après /clear) — diagnostic + fixes :**
- ⚠️ **La scène 41 avait le MAUVAIS bootstrap** : `CombatBootstrapIA` (hérité du clone de 30_CombatIA), PAS `CombatBootstrap2v2`. Le swap n'avait jamais été appliqué → en Play, le garde `ExpectedSceneName=30_CombatIA` faisait tout skip (0 spawn). **Corrigé** : composant remplacé par `CombatBootstrap2v2` (refs TeamQuantumMap=QuantumMap_2v2, CombatMap=CombatMap_2v2, SpellCatalog, SessionConfig, classes Soulrender/Nightseer/Colossar/Necram).
- ✅ **Spawn fix (v160)** : `CombatantSystem.OnPlayerAdded` levait un cap 1v1 résiduel (`slot>1` ignoré) → seuls 2 combattants sur 4 spawnaient. Cap relevé à `TurnConstants.MaxPlayers` (6). INVARIANT 1v1 inchangé. **Validé : 4/4 spawnent** aux points (Team,Rank) de la map.
- ✅ **5.5b input hot-seat (pur View)** : `CombatInputController` + `CombatHUDController` reconnaissent `CombatBootstrap2v2.Instance` → `_debugAllPlayersMovable/Controllable=true` → input, gate « mon tour », bouton Fin de tour et barre de sorts suivent `state.ActivePlayerIndex`. **Validé : on déplace les 4 combattants chacun à son tour, alternance OK.** (Caméra combat = manuelle zoom/pan, pas de follow → rien à changer.)
- ✅ **Non-problème tranché** : le RuntimeConfig est sérialisé INLINE par scène (pas un asset partagé) + les bootstraps clonent avant mutation → aucun risque de contamination 1v1. L'inquiétude « RuntimeConfig dédié » du checkpoint est rayée.
- 🔧 **Housekeeping** : `QuantumMap.asset` (map partagée Casual) avait été corrompue par le baking Quantum (ScenePath → scène backup 2v2) → **re-pointée vers 33_CombatCasual** dans le commit.

**5.5d — HUD 4 portraits (NEXT) :** timeline 4 combattants (au lieu de 2 P0/P1) + HP + couleurs allié/ennemi + barre de sorts par-classe selon le joueur actif (decks par joueur à câbler côté View).

**🧹 À nettoyer (junk non commité, sans urgence) :**
- `Assets/QuantumUser/Resources/NymoraCombatMap.asset` (+meta) : asset NymoraCombatMap **vide** (Width/Height 0) créé par erreur, non référencé → supprimable.
- Scènes backup : `33_CombatCasual_BACKUP_20260517_*`, `41_CombatRanked2v2_BACKUP_20260609_*` ×2 → supprimables.
- 2 assets TMP SDF (Anton / LiberationSans fallback) modifiés = bruit de fonts, laissés non commités.
- Fichier parasite `StackOverflowException` racine repo (jamais commité).
- Gizmo grille GridRenderer dessine 15×15 (max) en édition (cosmétique).

---

*Dernière mise à jour : 9 juin 2026 (brique 5.5 2v2 hot-seat COMPLÈTE a→e, next 5.6).*
