# 📍 STATUT ACTUEL — NYMORA

> Source de vérité du présent. À garder **léger** (l'historique d'avant le 8 juin 2026 est dans `STATUT_ARCHIVE_jusqua_8juin2026.md`).
> Workflow actif : **`09_ROADMAP_POST_PREALPHA.md`**.

---

## ⚡ Attaques accélérées 1,5× (15 juin) — pur View, pas de bump CombatRulesVersion

Toutes les anims d'**attaque** passées à **1,5×** (plus nerveuses). Levier = vitesse de l'état `Attack` de l'Animator (pas le fps du clip : keyframes à temps absolus). Tool **`Nymora > Combat > Set Attack Speed`** (`Editor/Tools/SetAttackSpeed.cs`, const `AttackSpeed`) : fixe (valeur absolue → ré-exécutable sans cumul) la vitesse `Attack` sur **tous** les AnimatorControllers de combat (5 classes + 5 skins cosmétiques, 66 controllers). Les 3 Build *_Refonte bakent aussi `AttackSpeedMultiplier=1.5` (rebuild self-consistent). ⚠️ Après un rebuild d'un animator NON-refonte (Colossar/Necram/cosmétique), re-lancer Set Attack Speed. Régler la nervosité = changer la const (+ garder les 3 `AttackSpeedMultiplier` alignés).

---

## 🎨 Refonte graphique NIGHTSEER (15 juin) — livrée (code), **à valider en jeu**, pur View, pas de bump CombatRulesVersion

3e refonte graphique, **calque EXACT du modèle Soulrender** (lui-même calque Ghostra). **1 skin de base unique** (idle/walk/attack/cast/hurt/death × SE/NE, miroir NW/SW) joué à TOUS les stages + **2 couches d'aura « sandwich »** (back/front) allumées aux stages 1 et 2 (stage 2 remplace stage 1). `idle` ping-pong, auras via `AuraLoopPlayer` (sans Animator imbriqué).

**Comme Soulrender** : stage piloté par la **Resource (Prescience, cap 5)** via `ComputeStage` générique (< 2 → s0, 2-4 → s1, 5 → s2) — **rien à toucher côté sim**, et **pas de leurres/DecoyView**.

**Fait :**
- Editor tool **`Nymora > Setup > Build Nightseer Refonte`** (`Editor/Tools/BuildNightseerRefonte.cs`) : importe/slice (PPU 96, pivot 0.5/0.1) les 20 PNG de `Desktop/Nymora_Graph/Nightseer/PNG` (sous-dossiers `stage0/1/2`), génère clips + 2 controllers base `NightseerBase_SE/NE` (reconstruits **en place**, guid préservé) + bind `Combatant_Nightseer` + retarget hub (`Nightseer.asset`). Idempotent.
- ⚠️ Nommage PNG livré : base en **MAJUSCULE** `Nightseer_base_…` (≠ Soulrender minuscule), marche = `walking`, auras `Nightseer_stage{n}_aura_{back,front}_{SE,NE}`.
- **À valider en jeu** : lancer le tool → combat IA Nightseer → F10 stages 0/1/2 SE/NE+miroir + avatar hub.

---

## 🎨 Refonte graphique SOULRENDER (14 juin) — livrée + **validée en jeu**, pur View, pas de bump CombatRulesVersion

2e refonte graphique (calque exact du modèle Ghostra). **1 skin de base unique** (idle/walk/attack/cast/hurt/death × SE/NE, miroir NW/SW) joué à TOUS les stages + **2 couches d'aura « sandwich »** (back/front) allumées aux stages 1 et 2 (stage 2 remplace stage 1).

**Différence vs Ghostra** : le stage du Soulrender est piloté par la **Resource (Carnage)** via `ComputeStage` générique (< 40% → s0, < max → s1, == max → s2) — **rien à toucher côté sim**, et **pas de leurres/DecoyView** à gérer.

**Fait :**
- Editor tool **`Nymora > Setup > Build Soulrender Refonte`** (`Editor/Tools/BuildSoulrenderRefonte.cs`) : importe/slice (PPU 96, pivot 0.5/0.1) les 20 PNG de `Desktop/Nymora_Graph/Soulrender/PNG`, génère clips + 2 controllers base `SoulrenderBase_SE/NE` (reconstruits **en place**, guid préservé) + bind `Combatant_Soulrender` + retarget hub (`Soulrender.asset`). Idempotent.
- ⚠️ Nommage PNG livré différent : base minuscule `soulrender_base_…`, marche = `walking`, auras `Soulrender_stage{n}_aura_{back,front}_{SE,NE}`. Frames : 6 par anim, **walking = 8**.
- `idle` ping-pong, auras via `AuraLoopPlayer` (sans Animator imbriqué) — mêmes décisions techniques que la Ghostra.
- **Tous les tests OK en jeu (14 juin)** : idle/walk/attack/cast/hurt/death SE/NE+miroir, F10 stages 0/1/2, hub.
- Note : changer le **nb de frames** d'une anim impose de relancer le tool (re-slice + clips) ; un simple touch-up pixel ne nécessite qu'un reimport.

---

## 🎨 Refonte graphique GHOSTRA (13 juin) — livrée, **pur View, pas de bump CombatRulesVersion**

1re refonte graphique (Ghostra uniquement). Nouveau modèle visuel : **1 skin de base unique** (idle/walk/attack/cast/hurt/death × SE/NE, miroir NW/SW) joué à TOUS les stages + **2 couches d'aura « sandwich »** (back derrière / front devant) allumées aux stages 1 et 2 (1 aura par stage, stage 2 remplace stage 1). Le stage reste piloté par les leurres actifs (0→s0, 1-2→s1, 3→s2).

**Fait :**
- `CombatantView` : couches d'aura via **`AuraLoopPlayer`** (boucle de `Sprite[]` SANS Animator — un Animator imbriqué sous l'Animator de base cassait l'idle). Garde anti-reset du controller de base. `idle` en **ping-pong 12 frames** (aller-retour), vitesse state **1.0** (= cadence pleine 8 fps comme le hub).
- **Leurres** (`DecoyView`) : reprennent base + sandwich d'aura **lus sur la `CombatantView` de la vraie Ghostra** (via `CombatantRenderer.GetCombatantView`) → indiscernables, zéro câblage de scène.
- **Hub** : avatar Ghostra repointé sur le skin de base refonte + **idle ping-pong** aussi (`Ghostra.asset` IdleFrames/WalkFrames).
- Editor tool **`Nymora > Setup > Build Ghostra Refonte`** : importe/slice (PPU 96, pivot 0.5/0.1) les 20 PNG de `Desktop/Nymora_Graph/Ghostra/Ghostra_refonte/PNG`, génère clips + 2 controllers base (reconstruits **en place**, guid préservé) + bind prefab + retarget hub. Idempotent.

**⏳ RELIQUAT (next session) :**
- 🐞 **Leurres reprennent PARFOIS l'ancien skin Ghostra** : le fallback sérialisé de `DecoyView` (`_decoyIdleController` etc.) pointe encore les vieux `GhostraStage0/1/2_*` ; utilisé quand la `CombatantView` parente n'est pas encore résolue (timing spawn) → flash d'ancien skin. Fix : repointer le fallback sur `GhostraBase_*` (via les 4 scènes ou la lecture-vue rendue obligatoire), OU virer le fallback.
- 🐞 **Prefab Ghostra : 6 slots Stage Controller affichés « Missing »** dans l'inspecteur alors que `GhostraBase_SE/NE` sont **valides sur disque** (guid OK, clips OK, pas de collision). Base de données Unity restée stale après le churn delete/recreate des 1ers runs. Workaround : Reimport du dossier `Animations/Ghostra/Refonte` + drag manuel des 2 controllers dans les 6 slots, OU régénérer. Le tool reconstruit désormais en place (ne devrait plus arriver).
- 🧹 Cleanup possible : anciens `GhostraAuraStage*_*.controller` + `.anim` d'aura (Refonte/) orphelins. ⚠️ NE PAS supprimer `GhostraStage0/1/2_*` (fallback DecoyView des scènes).

---

## 🔜 NEXT — Phase 4 : migration hub Fusion → WebSocket (plan validé 11 juin)

**Choisi pendant que le 2v2 réseau attend sa session de validation à 4 testeurs.** Plan approuvé sur le principe (session stoppée avant de lancer H1 → **reprendre par H1**).

**But** : sortir mouvement + présence du hub de Photon Fusion → relayer sur le backend WS OVH (qui porte déjà chat/matchmaking/challenges/spectate/ONLINE_COUNT). **Libère l'offre Fusion Free 100 CCU « one-app » → réaffectée à Quantum (combat 20 → 100 CCU).** Le hub n'étant pas déterministe, c'est le même modèle Shared Mode (chaque client broadcaste sa position, les autres interpolent) avec le transport WS au lieu de Fusion — l'interp remote est déjà maison (`HubAvatar.Render`).

**Scope Fusion = 2 gros fichiers** : `HubAvatar.cs` (NetworkBehaviour + ~9 `[Networked]` + RpcPlayEmote) et `HubBootstrap.cs` (NetworkRunner Shared). 9 autres fichiers = touches légères. Pur réseau hub → **AUCUN bump CombatRulesVersion**.

**Découpage (brique par brique) :**
- **H1 backend** : registre présence in-memory + relais position. Messages `HUB_ENTER`/`HUB_MOVE`(throttlé)/`HUB_EMOTE`/`HUB_STATE`/`HUB_LEAVE` → `HUB_ROSTER` + `HUB_PEER_JOINED/MOVE/EMOTE/STATE/LEFT`. Additif sur `nymora-backend/src/websocket/wsServer.ts`. Reco : in-memory (1 node), pas Redis.
- **H2 client** : `HubPresenceClient` (émet/parse).
- **H3 client** : `HubAvatar` → MonoBehaviour pur (retire Fusion), distants pilotés par peer events.
- **H4 client** : `HubBootstrap` sans NetworkRunner ; nettoie les 9 fichiers ; retire Fusion de l'asmdef `Nymora.Hub` ; régén prefab avatar.
- **H5 backend+client** : filet matchmaking (gate « max matchs concurrents » + file) — *à confirmer : Lorenzo voulait peut-être le sortir du lot et faire H1→H4+H6 d'abord.*
- **H6 config** : retirer Fusion du hub / réaffecter l'app Photon → **vérifier 100 CCU sur Quantum** + cleanup.

Cf mémoire `project_phase4_hub_ws_migration`.

> ⏳ **Reliquat de tests à valider (rappel)** : spectateur item B (PA/PM/abandon/prévisu/pièges masqués) + deck ranked item I (admin « decks les + joués » se remplit). Et la **grosse session 2v2 à 4 testeurs** (briques C/D/E réseau) reste en attente.

---

## ✅ Phase 2 — MORT SUBITE livrée + VALIDÉE 1v1 (11 juin) — CombatRulesVersion **162**

Codée pendant que le 2v2 attend sa session de validation (indépendante du réseau, se valide en 1v1). Dérivée de `TurnNumber` (aucun champ `[Networked]` → pas de régén). **Valeurs verrouillées Lorenzo** : avertissement rounds **23-24** → mort subite **round 25**. À l'entrée (round 25) : **purge tout le terrain** (obstacles + brume/pièges/voiles + terrains, garde les positions) + ressources maxxées. Round ≥25 : **poison d'arène +100/round** (100, 200, 300… vrais dégâts) + chaque joueur boosté **12 PA / 4 PM + ressources max**. View : filtre rougeâtre + bandeau (`SuddenDeathView`). Fichiers : `SuddenDeath.cs` (sim), `TurnSystem.cs` (3 injections `EnterTurnStart`), `GameVersion.cs` (162), `SuddenDeathView.cs`.

**✅ Correctif Ghostra (v162, validé en jeu 11 juin)** : la purge effaçait les leurres Ghostra ET forçait `Resource=max` → désync (compteur 3 sans leurres réels) → l'ulti **Exécution Spectrale** (exige 3 leurres ACTIFS) restait rejetée. Fix : les leurres ne sont plus purgés mais **téléportés dans un coin** (`RelocateGhostraDecoysToCorner`, balayage déterministe depuis (0,0), cases libres ; garde Kind/HP/durée) → Ghostra garde le compte pour l'ulti mais perd l'emprise plateau comme les autres classes perdent leur terrain ; Ghostra **exclue du « max ressource »** (`OnActivate` + `ApplyBoost`) car sa ressource = nombre de leurres (auto-sync). **Décision : on ne redonne PAS 3 leurres d'office** (juste préserver l'existant). **Mort subite 1v1 = VALIDÉE.**

---

## ✅ SESSION 10 juin — 1er TEST 2v2 RÉSEAU 4 CLIENTS validé (à 4 vrais testeurs)

**Le match 2v2 réseau a tourné bout-en-bout** (matchmaking → combat 4 joueurs → tours alternés équipes). 2 bugs trouvés en jeu, **les 2 corrigés et validés** (commit local `3284d99`) :

- ✅ **CORRIGÉ + VALIDÉ — barre de sorts** : tous les clients voyaient le deck du joueur ACTIF (joueur 0) au lieu du leur. Cause = `CombatHUDController.OnGameStartedResolveLocalSlot` testait le gate `isPvp`/IA AVANT le réseau 2v2 → `_debugAllPlayersControllable` restait à son défaut `true`. Fix : modes équipe testés avant le gate + barre-suit-l-actif gatée sur le marqueur hot-seat. **(commit local `3284d99`, pur View.)**
- ✅ **CORRIGÉ (à valider) — pseudo « Bot »** : le tooltip combat lisait `PlayerProfileBridge` (binaire 1v1) non rempli en 2v2. Fix : le bootstrap réseau pose le vrai displayName dans `RuntimePlayer.PlayerNickname` (sync Quantum), le tooltip le lit en fallback. **(même commit `3284d99`.)**
- ✅ **RÉSOLU — gemmes PA/PM** : après rebuild unique avec `3284d99`, les badges rubis PA/PM s'affichent pour tout le monde (l'asymétrie venait d'un build pas à jour côté équipe affectée, pas d'un bug de résolution — le log Kyami montrait déjà une résolution parfaite).

**Statut : 2v2 RÉSEAU A+B VALIDÉ bout-en-bout** (matchmaking → combat 4 joueurs → tours alternés → barre/pseudo/gemmes corrects → victoire/défaite par équipe). Reste **C + D** pour compléter le ranked 2v2.

**Prochaines briques (2v2 réseau) :**
- ✅ **D1 — settle ELO/MMR 2v2 BACKEND : DÉPLOYÉ + VALIDÉ PROD** (commits backend `d9342ea`/`a0a5536`). **Ladder 2v2 SÉPARÉ du 1v1** : champs `Profile.mmr2v2/ranked2v2Games/Wins/Losses/seasonPeak2v2Mmr` (migration `20260610000000` appliquée prod). Matchmaking 2v2 lit `mmr2v2`. `POST /ranked2v2/report-result` (consensus cross-équipe `ranked2v2ResultRegistry`) + `settleRanked2v2Match` (ELO perso vs moyenne adverse `compute2v2MmrChanges`, pur/testé 13/13). **Récompenses BONUS** : win 250 XP / 120 Nymos, loss 100 / 60 + BP/quêtes/succès. Push WS `MMR2V2_UPDATED`. `GET /ranked2v2/leaderboard`. Pas de nul. Healthcheck prod OK, 0 erreur.
- ✅ **D2 — client LIVRÉ + VALIDÉ EN JEU + COMMITÉ** (commit jeu `6a38049`). 2 correctifs post-test validés : (#2) Kyami ne voyait pas sa ligne MMR → le settle poussait les 4 en boucle séquentielle `await` (un échec sautait les suivants) → `Promise.allSettled` + push W/L best-effort (backend `6435164`, déployé) ; (#1) la carte Ranked 2v2 affichait le MMR 1v1 → `/profile/me` expose `mmr2v2` (backend déployé) + le badge 2v2 lit `mmr2v2` (client). **Brique D 100% terminée.**
  - Détail D2 : (D2a) report au retour hub — `Match2v2ResultBridge` (combat→hub) rempli par `MatchEndOverlay.OnReturnToHubClicked` (chemin 2v2 si `Match2v2Bridge.HasPendingMatch`, verdict team-aware) → `HubMatchResultDisplay` POST `/ranked2v2/report-result` (`ReportRanked2v2ResultAsync`) + ligne `[CLASSÉ 2v2] VICTOIRE/DÉFAITE` ; event WS `MMR2V2_UPDATED` (`HubChatClient` enum/parse/dispatch + `OnMmr2v2Updated`) → system line rang 2v2. (D2b) onglet **2v2** du leaderboard du menu (`HubMenuShell.SelectLbTab` idx 1 → `LoadLeaderboardMode(true)` → `Get2v2LeaderboardAsync`) ; les onglets 1v1/2v2/3v3 existaient déjà (2v2 était « bientôt dispo »). Pas de stats de combat dans le report 2v2 v1 (défaut sûr). **À builder + valider en jeu** (un match 2v2 → ligne [CLASSÉ 2v2] + MMR 2v2 qui bouge + onglet leaderboard 2v2 peuplé). Fichiers : `Match2v2ResultBridge.cs` (nouveau), `MatchEndOverlay.cs`, `NymoraApiClient.cs`, `NymoraApiDtos.cs`, `HubChatClient.cs`, `HubMatchResultDisplay.cs`, `HubLeaderboardPanel.cs`, `HubMenuShell.cs`.
- ⏳ **C — vote capitaine réseau (EN COURS)** : design verrouillé (capitaine = chef de groupe, désigné backend ; transport = Photon room/player properties ; anti-hang → ordre défaut).
  - ✅ **C backend** : `RANKED_MATCH_FOUND` 2v2 marque `captain` sur le 1er joueur de chaque équipe (`teamA[0]/teamB[0]`, commit backend `f685e43`, **déployé + validé prod**).
  - ✅ **C plumbing client** (commit jeu `75dcb69`) : `captain` → `RankedTeamPlayer` → `Match2v2Bridge.Player.IsCaptain` (porté jusqu'au bootstrap).
  - ✅ **C cœur CODE-COMPLET (à valider à 4 clients, commit jeu `53cb206`)** : `NetworkTeamOrderLobby` (View, calqué sur `PreCombatLobbyController`) — le capitaine ordonne son équipe (panneau CombatUiKit ▲/▼ + « Valider »), publie l'ordre (subs) dans une **player custom property Photon `"to"`** ; chaque client lit la propriété contenant SON sub → en déduit son rang (`TeamOrder`) AVANT `AddPlayer`. Pompe `Client.Service()` à chaque frame (avant `StartAsync`). Non-capitaine = écran d'attente. Anti-hang (timeout 30s) → rang par défaut (ordre roster). `CombatBootstrapRanked2v2.RunCaptainOrderLobbyAsync` await entre `ConnectToRoom` et `AddPlayer` ; lève le voile (`SignalReady`). Pur View/réseau, pas de bump CombatRulesVersion. ⚠️ Le `.meta` du nouveau script sera généré par Unity à l'import.
  - ✅ **VALIDÉ EN JEU À 4 CLIENTS (10 juin)** : log `Ordre capitaine résolu : rang local = 1` → `AddPlayer rank=1` → `TurnOrder 1er = P2`. Lorenzo : « le choix de joueur via chef d'équipe c'est ok ». **Brique C TERMINÉE.**
  - 🔧 **Fix associé (commit jeu `5f84de7`)** : la ligne chat MMR 2v2 était perdue quand le settle (consensus cross-équipe) tombait PENDANT le combat (les autres joueurs reportent avant que toi tu rentres au hub → l'event `MMR2V2_UPDATED` arrive sans `HubMatchResultDisplay` abonné). Fix : `HubChatClient` cache le dernier `MMR2V2_UPDATED` (`TryTakePendingMmr2v2`), affiché au retour hub. 1v1 non concerné (double-accord). **À re-valider : chaque joueur (même le dernier à rentrer) voit sa ligne `[CLASSÉ 2v2] MMR …`.**

**🎯 2v2 RÉSEAU COMPLET** : matchmaking + combat 4 joueurs + vote capitaine (C) + settle ELO/MMR/leaderboard 2v2 (D). Reste 5.8 polish (optionnel) puis **3v3** (réutilise 5.1→5.3 + 5.6 + C + D).

## Phase 5 brique E — PRÉ-COMBAT 2v2 SÉQUENCÉ (NEXT, demandé 10 juin)

Demande Lorenzo (testera « après »). Design verrouillé (cf mémoire `project_precombat_2v2_flow`). Séquence voulue avant le combat 2v2 : **(1) choix de deck par les 4 joueurs** (complet, comme 1v1 ; l'écran révèle les **4 classes**, alliés + ennemis) → **(2) vote capitaine** enrichi avec les classes (= brique C `NetworkTeamOrderLobby` + libellés de classe) → **(3) pile ou face animé « Équipe A/B commence »** (team-aware) → **(4) combat**.

Découpage : E1 lobby deck 4 joueurs → E2 classes dans le panneau capitaine → E3 pile ou face team-aware.

- ✅ **E1 — lobby de deck 4 joueurs CODE-COMPLET** (commit jeu `d69f4a0`) : `NetworkDeckLobby` (View) — chaque joueur voit les 4 joueurs groupés par équipe (TON ÉQUIPE / ADVERSAIRES) + leur **classe** (révélée via player property Photon `ec`) et choisit son deck (`<`/`>` + Prêt) ; résout quand tous prêts ou timeout. `HubMatchTransition` remplit `PreCombatBridge` (decks de la classe locale) pour le 2v2. `CombatBootstrapRanked2v2.RunDeckLobbyAsync` lance le lobby AVANT le vote capitaine, met à jour `DeckBridge` avec le deck choisi avant `AddPlayer`.
- ✅ **E2 — classes dans le panneau capitaine CODE-COMPLET** (commit jeu `1fd8cdd`) : `NetworkTeamOrderLobby` affiche la classe de chaque membre (lue depuis la prop `ec` publiée en E1), rafraîchie chaque frame.
- ✅ **E3 — pile ou face team-aware CODE-COMPLET** (commit jeu `1fd8cdd`) : `CoinFlipIntroView` ne jouait QU'EN Casual (gate `CombatBootstrapCasual.Instance`) → aucune anim en 2v2 ; désormais joue aussi en équipe et annonce l'**ÉQUIPE** qui commence (A=pile/équipe 0, B=face/équipe 1, + « (ton équipe) » si `StartingTeam==Match2v2Bridge.LocalTeam`). 1v1 inchangé.

**Séquence pré-combat 2v2 complète (code)** : deck (E1) → ordre capitaine + classes (C/E2) → pile ou face « Équipe A/B » (E3) → combat. **À valider à 4 clients.**

- 🔧 **Fix E1 (11 juin, commit `5760845`) — testé à 4 testeurs → bug → corrigé** : le deck choisi dans le lobby 2v2 ne s'appliquait pas à la barre de sorts (le HUD applique le deck du HUB en `Awake`, AVANT le lobby) → ajout de `CombatHUDController.ReapplyDeckFromBridge()` après le choix (comme le 1v1 Casual). **À RE-TESTER** : le deck choisi au lobby 2v2 = celui en barre au combat.

> **Fin de session 11 juin** : journée énorme — 2v2 réseau complet (D ELO/MMR backend déployé prod + C vote capitaine + E pré-combat séquencé), **Phase 2 Mort subite (v161→v162, VALIDÉE 1v1 + fix Ghostra leurres-en-coin)**, fix deck E1. ClientErrorReporter = déjà existant/câblé (doublon annulé). **1 session de validation en attente : 2v2 à 4 testeurs** (mort subite 1v1 = validée). Tout poussé sur GitHub.
- Puis **5.8 polish**, puis **3v3** (réutilise 5.1→5.3 + 5.6 générique).
- **Push GitHub uniquement quand Lorenzo le dit** (côté jeu ; le backend est déjà poussé/déployé par Claude).

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

> Reliquat vision pièges équipe = **RÉSOLU le 10 juin** (cf section dédiée plus bas).

## Phase 5 brique 5.6 (vote d'ordre pré-combat) — ✅ COMPLÈTE & VALIDÉE (10 juin)

Panneau pré-combat hot-seat **panneau unique** (pas de notion de capitaine en hot-seat ; le vrai vote par-capitaine réseau = 5.7). Pur View, **pas de bump CombatRulesVersion**.
- `TeamOrderVote` (bridge statique Bootstrap⇄View, façon `DeckBridge`) : le bootstrap publie le roster + await ; le panneau Submit l'ordre (TeamOrder par PlayerSlot).
- `CombatBootstrap2v2` : le vote précède les `AddPlayer` (TeamOrder fige TurnOrder) ; **fallback ordre par défaut** anti-hang (~1,5 s sans panneau).
- `PreCombatOrderPanel` (auto-instancié scènes 2v2/3v3, procédural) : 2 colonnes d'équipes, réordon ▲/▼ par membre, « Lancer le combat ». **DA hub** (`CombatUiKit` monochrome + coins arrondis + police Ari runtime + layout groups, façon `MatchEndOverlay`) ; flèches = sprite `direction_arrow` pivoté (pas de glyphe que la police Ari ne porte pas) ; lève le voile de chargement (`SignalReady`) car le démarrage est retardé derrière le vote. Générique N-joueurs → prêt pour 3v3.

## Phase 5 brique 5.7 (matchmaking 2v2 réseau) — EN COURS (10 juin)

Décision Lorenzo : **solo + duo premade fusionnables**, objectif **2v2 réseau end-to-end** (livré par paliers qui compilent/se valident, pas en un bloc).

- ✅ **5.7 backend — DÉPLOYÉ PROD & VALIDÉ** (repo `nymora-backend`, commit `cb96fa8`, push + `deploy.sh` OK, `{"status":"ok"}` + `[Matchmaking2v2] Tick started`, 0 erreur).
  - `matchmaking2v2Service.ts` : file Redis par **groupe** (solo/duo), MMR moyen, fenêtre adaptative ; `planMatch()` PUR cherche 4 joueurs contigus en MMR puis 2 équipes équilibrées en préservant les duos (`[duo,duo]`/`[duo,solo,solo]`/`[solo×4 → extrêmes vs milieu]`).
  - `wsServer.ts` : `ENQUEUE_RANKED_2V2`/`DEQUEUE_RANKED_2V2` + `dispatchRanked2v2Match` (`RANKED_MATCH_FOUND` mode `'2v2'`, `myTeam` + `teams`) + tick 2s + dequeue au disconnect. **Pas de migration Prisma** (Redis only) → additif, zéro impact 1v1/hub.
  - `npm run test:matchmaking2v2` : test PUR sans Redis, **12/12 vert**.

- ⏳ **5.7 client (reste)** — sous-briques, chacune testée à 2-4 clients :
  - ✅ **A — code-complet** (commits `d35ab0a`/`fd73492`/`8b0e2bd`) : `Match2v2Bridge` + `HubChatClient` (parse 2v2 aplati + events + `SendEnqueueRanked2v2`/`Dequeue`) + `HubMatchTransition` (remplit le bridge → scène 41) + **file 2v2 dans le menu moderne** (`HubMenuShell` : carte « Ranked 2v2 » activée, écran matchmaking mode `_mmMode`). Backend payload aplati redéployé. ⚠️ Pas encore testé en Play. ⚠️ Charger la scène 41 lance ENCORE le **hot-seat** (bootstrap réseau = B) → A valide le round-trip matchmaking, pas le combat réseau.
  - ✅ **B — code-complet** (commits `a1d5d1c`/`990e873`) : `CombatBootstrapRanked2v2` (room Photon MaxPlayers=4, AddPlayer local avec TeamId/TeamOrder + deck, poll PlayerRef ; calqué Casual ; s'active si `Match2v2Bridge` rempli). Hot-seat `CombatBootstrap2v2` skip si match appairé. `LocalPlayerResolver`/`CombatInputController`/`CombatHUDController` reconnaissent le réseau 2v2 (1 combattant local/client). **Écran fin de match team-aware** (VICTOIRE/DÉFAITE par équipe via `TeamHelper`). Editor tool `Nymora > Setup > Patch Ranked 2v2 Bootstrap`. ⚠️ Pas encore testé.
    - ✅ **PRÉREQUIS TEST FAIT** : `Nymora > Setup > Patch Ranked 2v2 Bootstrap` déjà appliqué sur la scène 41 (composant `CombatBootstrapRanked2v2` présent, refs clonées, commit `590a909`).
    - Limitations attendues : **pas de vote capitaine** (ordre par défaut = rang roster) ni **d'ELO** (MMR inchangé) ; **classes-uniques non imposées** (les 4 testeurs coordonnent : 1 classe différente chacun).
  - ⏳ **C** (DIFFÉRÉ) : lobby pré-combat réseau 4 joueurs + vote capitaine (le panneau 5.6 → réseau via player properties). Non bloquant (ordre par défaut OK).
  - ⏳ **D** (DIFFÉRÉ) : settle ELO perso + MMR moyen 2v2 (backend 4-way + report). Non bloquant pour jouer.

**Statut « 2v2 jouable » : A+B livrés → un vrai match 2v2 réseau appairé est jouable de bout en bout (matchmaking → combat 4 joueurs → victoire/défaite par équipe).** Reste C (ordre voté) + D (ELO) en polish méta.

> ⚠️ Repo backend : **clone canonique = `C:\Users\Lorenzo\Documents\nymora-backend`** (à jour). Le `Unity\Nymora\backend` est un **clone PÉRIMÉ** à ignorer (supprimable).

Puis 5.8 polish. Puis 3v3 (réutilise 5.1→5.3 + 5.6 générique).

### ✅ RÉSOLU (10 juin) — Vision des pièges Nightseer en équipe (2v2/3v3)
**Vraie cause trouvée :** ce n'était PAS la logique `show`, mais le **garde de spawn basé sur les coins**. `TrapView/TerrainView/FogOfWarView.TrySpawnOverlays` faisaient `if (GetTileView(0,0) == null || GetTileView(dernier) == null) return;`. En map 2v2 **irrégulière (bord-only)** les coins sont **carvés** → `GetTileView` = null → le spawn avortait à chaque frame → **aucun overlay créé → personne ne voyait aucun piège** (ni terrain, ni brouillard) en 2v2, quelle que soit la phase.

**Fix (pur View, pas de bump CombatRulesVersion) :**
- `GridRenderer.TilesSpawned` (nouvelle prop, signal de dispo **irrégulier-safe**) ; les 3 vues gatent dessus au lieu des coins (cases carvées sautées case par case, déjà géré).
- `TrapView` : visibilité dernière phase passée de « soi seul » (`owner == viewer`) à « **même camp** » (`!TeamHelper.AreEnemiesByPlayerIndex`) → l'allié garde la vue. POV hot-seat = **équipe active** (`ResolveControllable(ActivePlayerIndex)`). NON-RÉGRESSIF en 1v1 (TeamId==PlayerIndex).
- `TerrainView` + `FogOfWarView` : même garde de spawn corrigé (Sang Coagulé/Vapeur Carmin/Voile étaient invisibles pareil). FogOfWarView : **spawn seulement**, perspective slot 0 NON touchée.

**Validé en jeu (10 juin) :** par défaut tous voient ; en phase 3 NS+allié voient, ennemis non ; 1v1 IA inchangé.

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

*Dernière mise à jour : 9 juin 2026 (5.5 complète ; reliquat vision pièges équipe à reprendre ; session stoppée).*
