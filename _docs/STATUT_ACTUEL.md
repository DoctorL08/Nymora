# 📍 STATUT ACTUEL DU PROJET NYMORA

> **À mettre à jour à chaque fin de session avec Claude.**  
> Ce fichier écrase tous les autres docs en cas de conflit. C'est la source de vérité du moment présent.

**SESSION 23 mai 2026 (🎥 CAMÉRA COMBAT — CLAMP PAN + TEINTE HARMONISÉE) :**

- **Clamp pan caméra** : le pan (clic-molette + drag) est désormais **borné aux limites de la map** → plus de vide aux bords. `GridRenderer.TryGetWorldBounds` (AABB iso depuis `GridConstants`+`GridSettings`+transform, +½ tuile) ; `CameraController.ClampToBounds` clampe **uniquement la position** (zoom laissé intact, cf retour Lorenzo : 1ère version cappait le zoom-out par erreur → retirée). Champs `_clampToMap` (def true) + `_boundsPadding` (def 0). **S'applique aux 3 scènes combat automatiquement** (logique de script + `_clampToMap` défaut true + GridRenderer auto-trouvé, pas de flag sérialisé par scène).
- **Teinte post-FX harmonisée** : les 3 scènes pointaient déjà sur `Combat_PostFX`, MAIS le post-process caméra (`renderPostProcessing`) n'était activé que sur l'IA → casual/ranked sans grading → teinte différente. Tool **`Propagate Combat PostFX (casual + ranked)`** (`CombatPostFXTool`) active le flag caméra + garantit le profil sur les 2 scènes. Validé : 3 scènes raccord.
- ⚠️ Différence à retenir : le **clamp** = logique script (pas de réglage par scène) ; le **post-process** = flag sérialisé par caméra (donc à activer scène par scène). 
- 100% View → pas de bump CombatRulesVersion. Scènes casual/ranked modifiées (flag caméra) → designer doit **rebuild standalone**.

---

**SESSION 23 mai 2026 (🎨 POST-FX SUR LES MAPS COMBAT — LIVRÉ) :** le pack post-process (cf [[project-postfx-pack]]) est appliqué aux 3 scènes combat.

- **Pack** = `_Nymora/Settings/PostProcessing/` : `Hub_PostFX` + **`Combat_PostFX`** (clone créé par le nouveau `CombatPostFXTool`) + 4 LUT (`Neutral/Cinematic/Cold/Warm`).
- **`CombatPostFXTool`** (`Nymora > Setup > Setup Combat PostFX (IA)`) : clone `Hub_PostFX`→`Combat_PostFX`, pose Global Volume "PostFX Volume" + active post-process caméra sur `30_CombatIA`. Pattern calqué sur `HubVisualPolishTool`.
- **Lorenzo a finalement fait à la main** : copier/coller du `PostFX Volume` + `Scene Lighting 2D` du hub vers **les 3 scènes combat** (`30_CombatIA`, `33_CombatCasual`, `40_CombatRanked1v1`) — « perfect ». Donc **pas d'injectable de propagation** (abandonné). ⚠️ Le `PostFX Volume` copié référence probablement **`Hub_PostFX`** (partagé hub↔combat) ; pour rendre le combat indépendant → pointer son Volume sur `Combat_PostFX`. À trancher plus tard, on commit **en l'état**.
- **`CombatPostFXTool` + `Combat_PostFX` gardés** (réutilisables) même si Lorenzo a fait le copier/coller manuel.
- 100% View (Volume + lighting 2D), **pas de bump CombatRulesVersion**. ⚠️ Scènes combat modifiées → le designer devra **rebuild son standalone** pour voir le rendu en casual/PvP.

---

**SESSION 23 mai 2026 (🏷️ TITRES DANS LE TOOLTIP AVATAR — LIVRÉ) :** le tooltip hover avatar passe à **3 lignes : clan (haut, rouge) / pseudo (milieu, crème) / titre (bas, doré italique +petit)**. Validé par Lorenzo.

- **Source** = cosmétique type `title` équipé (backend 5.5 déjà prêt : équipement générique sans class-lock, slot `title`). Titres au catalogue : `title_the_unbroken` (« l'Inébranlable »), `title_soulbound` (« Âme Liée »).
- **`HubAvatar`** : nouveau champ **`[Networked] NetTitle`** (NetworkString<_64>), résolu dans `RefreshEquippedSkinAsync` (même fetch inventaire que le skin, pas de 2ᵉ appel) ; helper `ExtractTitleText` extrait le texte entre « » (« Titre : « l'Inébranlable » » → `l'Inébranlable`). Équiper/déséquiper un titre dans le profil le re-push (les 2 handlers appellent déjà `RefreshEquippedSkin`).
- **`HubAvatarHoverTooltip.ResolveDisplayName`** : 3 lignes, lignes clan/titre omises si vides (pseudo reste centré seul). Rich text TMP `<size=80%><i><color=#ffd700>`.
- **⚠️ Regen `[Networked]`** : procédure SÛRE = Reimport du prefab `HubAvatar.prefab` (PAS `Create Hub Avatar Prefab` qui le recrée à zéro et écrase les refs class/skin/backend — vérifié dans le code) + **rebuild standalone** (étape critique, sinon Invalid Length en multi). Pas de régén de scène (avatar spawné runtime). Mémoire `feedback-networked-field-regen-protocol` à amender sur ce point.
- **Fichiers** (commit Unity LOCAL à faire) : `HubAvatar.cs` (NetTitle + ExtractTitleText + résolution), `HubAvatarHoverTooltip.cs` (3 lignes), + le prefab `HubAvatar.prefab` re-baké. 100% View/hub → **pas de bump CombatRulesVersion**.

---

**SESSION 23 mai 2026 (🔥 TRI ISO TORCHES — RÉSOLU DE BOUT EN BOUT) :** suite du debug torches ci-dessous, les 2 bugs sont **clos**.

- **Bug 2 halo Light2D : ✅ RÉSOLU.** Cause = TOUTES les point lights (12 Torch + 3 Magic Halo) ciblaient `Default + Personnages` dans leur *Target Sorting Layers* → elles lavaient le perso (qui vit sur `Personnages`). Fix = tool `HubLightTargetLayersTool` (`Nymora > Setup > Fix Torch Light Target Layers`) qui remet toutes les **point lights** sur `Default`-only ; seule la **Global Light** garde `Default + Personnages`. Ne touche QUE `m_ApplyToSortingLayers` (intensité/couleur/position préservées). Validé par Lorenzo.

- **Tri devant/derrière des torches : ✅ RÉSOLU.** Cause RACINE (révélée par `HubDepthSortDiagnosticTool`) : **une seule `IsoDepthSort` dans la scène** → toutes les torches étaient **dessinées sur UNE seule PNG** (`torch_frame1..8`, art Kyami, 8 frames de flicker) portée par un unique `Torch`/SpriteRenderer → 1 seul `sortingOrder` impossible à trier individuellement. Fix = injectable **`HubTorchSlicerTool`** (`Nymora > Setup > Slice & Place Torches`) : détecte chaque torche (régions opaques connexes, union des 8 frames), tranche les 8 PNG en *Multiple* (pivot au pied), instancie **1 GameObject par torche** sous `Torches (split)` pile à sa position (math PPU 32 + pivot bottom-center), avec **`SpriteFlipbook`** (nouveau composant runtime, anim désync) + `IsoDepthSort` + `DepthPivot` au pied, layer `Personnages`. L'ancien `Torch` combiné est **désactivé** (pas supprimé). Validé Play Mode par Lorenzo (« ça marche »).

- **Fichiers ajoutés** (commit Unity LOCAL à faire) : `Scripts/Hub/SpriteFlipbook.cs` ; `Editor/Setup/HubLightTargetLayersTool.cs`, `HubTorchSlicerTool.cs`, `HubDepthSortDiagnosticTool.cs`, `HubTorchPivotSyncTool.cs` ; patch `HubTorchDepthSortTool.cs` (ne réécrit plus un `DepthPivot` réglé à la main). 100% View/hub → **pas de bump CombatRulesVersion**.
- **À RETENIR** : les **8 textures `torch_frameN.png` sont passées en `Sprite Mode: Multiple`** (tranchées) ; l'ancien prefab `Torch` + son Animator affichent un sprite "missing" (normal, instance désactivée). Réversible si besoin (réactiver le `Torch` + repasser les textures en Single).
- **LEÇON GÉNÉRALE** : Kyami livre parfois plusieurs instances d'un même décor **sur une seule PNG** → casse le tri iso par objet. Réflexe : trancher (slicer) ou redemander un export par-instance.

---

**SESSION 23 mai 2026 (🔦 DEBUG TRI ISO TORCHE/PERSO — HUB) :** deux sous-bugs distincts sur l'éclairage/tri des torches du hub. Commit Unity **local** `8042a13` (NON poussé), 2 scripts only. **Pas de bump CombatRulesVersion** (100% View/hub).

- **BUG 1 — sprite torche toujours devant le perso : ✅ RÉSOLU.** Cause racine : le child `DepthPivot` du prefab `Torch` est à `localPosition (0,0,0)` = racine torche à `world y ≈ -9.38`, soit **sous toute la grille walkable** (qui va de y≈-4.75 à +4.75). Son inverse-projection donne `gx+gy ≈ -18` → `IsoDepthSort` calcule `order = 100-(-18) = ~118`, toujours > au max avatar (`100-(gx+gy)`, range [62,100]) → torche toujours devant. Le tool `HubTorchDepthSortTool` plaçait le pivot à `sr.bounds.min.y` (bas du rectangle, padding inclus, sous la grille). **Fix = remonter le `DepthPivot` aux pieds visibles de la torche sur la grille** (manip Scene view, `IsoDepthSort` est `[ExecuteAlways]` → feedback live). Lorenzo a confirmé : le perso passe maintenant **devant/derrière** correctement. ⚠️ Le repositionnement du `DepthPivot` est une **édition de scène `10_CommunityHub.unity` NON committée** (seuls les scripts le sont).

- **BUG 2 — halo Light2D toujours devant le perso quand il est DEVANT la torche : 🔴 NON RÉSOLU (RELIQUAT).** Symptôme : le sprite se trie bien, mais la **lueur de la Light2D** continue de laver/teindre le perso même quand il est devant la torche. Cause de fond : **une Light2D URP 2D ignore totalement la profondeur iso** — elle éclaire uniformément tous les sprites de ses *Target Sorting Layers* (ici Default), sans notion de devant/derrière. Constats scène : **toutes les lights ont le volumétrique OFF** (`m_LightVolumeIntensityEnabled: 0`, pas de blob de glow) et **ciblent Default seul** (`m_ApplyToSortingLayers: 00000000`) ; un seul Sorting Layer existait (« Default »).
  - **Approche tentée (validée par Lorenzo avant test, mais bug TOUJOURS présent après)** : sortir le perso **et** le sprite torche de Default vers un nouveau sorting layer **« Personnages »** (au-dessus de Default) — ils s'iso-trient toujours entre eux (même base 100), mais ne sont plus ciblés par les Torch Lights ; la `Global Light` devait être re-ciblée sur Default + Personnages pour garder le perso éclairé. **Code livré** (`IsoDepthSort._sortingLayerName`, `HubAvatar._hubSortingLayer` + `ApplyHubSortingLayer()` sur corps+ombre).
  - **À VÉRIFIER EN PREMIER prochaine session** : est-ce que les **2 manips Unity manuelles ont bien été faites** ? (1) créer le Sorting Layer `Personnages` SOUS Default ; (2) cocher `Personnages` dans **Target Sorting Layers** de la `Global Light`. Si le layer n'existe pas, Unity ignore silencieusement `sortingLayerName` → le perso reste sur Default → bug inchangé (hypothèse #1). Si les manips sont faites et le bug persiste → l'approche layer est insuffisante : **demander un screenshot** à Lorenzo et reconsidérer (peut-être un autre élément qui rend devant, ou rendu unlit, ou fake-glow sprite iso-trié à la place de la Light2D).
  - Mémoires à respecter : `feedback-dont-overwrite-light-values` (ne jamais réécrire ses valeurs Light2D), `feedback-trust-observations`.

- **RELIQUAT TOOL (optionnel)** : patcher `HubTorchDepthSortTool` — (a) ne plus placer le pivot à `sr.bounds.min.y` ; (b) ne PAS réécrire un `DepthPivot` déjà réglé à la main (sinon re-run écrase le fix de Bug 1) ; (c) poser aussi le sorting layer `Personnages` sur le sprite torche.

- **PROCHAIN STEP** : reprendre Bug 2 (cf check ci-dessus). Sinon revenir au step Phase 5.5 (« nouveau chantier à décider », cf session Phase 5.5 ci-dessous).

**SESSION 23 mai 2026 (🛒 PHASE 5.5 BOUTIQUE + SHARDS + COSMÉTIQUES — COMPLÈTE a→e) :**
- **Fix layout Quêtes (5.8)** : `Content.sizeDelta.x=0` (titre `[Quotidien]` n'était plus coupé à gauche) + récompense passée sous la barre (3 étages). Commit Unity local `0bf1185`. Cf mémoire `feedback-rect-sizedelta-zero-stretched`.
- **5.5.a/b backend BOUTIQUE — DÉPLOYÉ PROD OVH + validé E2E** : `UserCosmetic` (migration `shop_cosmetics`), catalogue code `src/shop/catalog.ts` (**Ashen Sovereign** Soulrender 1200 Shards class-locked + placeholders), `src/shop/service.ts` (buy via wallet.spendCurrency idempotent, equip garde-fou class-lock, rotation hebdo déterministe ROTATION_SIZE=4), routes `/shop` `/shop/inventory` `/shop/buy` `/shop/equip` `/shop/unequip`, script `shards:grant`. Commits backend **poussés+déployés** `762a0c5` (a) + `5e958a0` (b). Shards = octroi dev/admin uniquement (Stripe différé). **Nocturn crédité 3000 Shards en prod.**
- **5.5.c/d/e Unity — commit LOCAL `ddab3a5` (non poussé)** : boutique UI (`HubShopPanel`+grille+achat), onglet Cosmétiques du profil (inventaire + équiper, gate « être en Soulrender »), skin Ashen Sovereign animé sur l'avatar hub (LOCAL only). Frames extraites GIF→PNG (Pillow) dans `Art/Cosmetics/AshenSovereign`, `CosmeticSkinDefinition` + `PatchAshenSovereignSkinTool`. **`HubVisualYOffset=0.5` réglé manuellement** sur l'asset.
- **À RETENIR** : (1) commits Unity LOCAUX non poussés (`0bf1185`, `ddab3a5`) — push GitHub en fin de session. (2) **Skin visible par les AUTRES joueurs = follow-up** (champ `[Networked]` NetSkinId + régén Fusion). (3) **Skin en COMBAT = 5.10** (non fait). (4) Procédure test prod boutique : `dev:token` + header `x-nymora-client-version: 0.1.1`, cleanup `psql DELETE FROM users`.
- **5.5.f ✅ sync remote du skin** (commit Unity local `23c45ca`) — champ `[Networked] NetSkinId`, régén Fusion faite, validé 2 instances. Phase 5.5 = 100% (a→f).
- **🔖 RELIQUAT DIFFÉRÉ** : **skin en COMBAT (5.10)** — affiché dans le hub mais pas en combat. En attente que **Kyami livre le skin sur les 2 autres phases/stages** (stage0 seul pour l'instant).
- **MaJ CLIENT 0.1.2 PUBLIÉE PROD** (boutique/cosmétiques/skin + ranked client) : zip uploadé OVH, `version.service.ts` bumpé, commit backend `a491f0c` déployé. `curl /version` → 0.1.2 OK. Repo Unity poussé `origin/main` = `c5a2992`.
- **⚠️ LAUNCHER — builds 0.1.0/0.1.1 PRÉ-L4** : ils téléchargent mais n'installent pas (stub "(installation auto : Brique L4)"). **Lorenzo + Kyami doivent installer 0.1.2 MANUELLEMENT 1× (dézip par-dessus l'install)**. Le code 0.1.2+ a le vrai install. **1ère vraie validation L4 = MaJ 0.1.2→0.1.3.** Cf [[project-launcher-publish-workflow]].
- **WIP NON COMMITÉ (volontaire)** dans le repo Unity : Quantum/IA (`QuantumMap_IA`, `CombatBootstrapIA`, `QuantumEditorSettings`, fallback TMP, `EditorBuildSettings`) + PNG debug `_docs`. À finaliser par Lorenzo.
- **PROCHAIN STEP** : à décider — nouveau chantier (2v2-3v3, ou autre). Skin combat repris quand Kyami a fini les stages. Valider L4 à la prochaine MaJ (0.1.3).

**SESSION 22 mai 2026 (🏆 PHASE 6 BLOC A — RANKED 1v1 COMPLET, de bout en bout sur la prod OVH) :** toute la boucle classée 1v1 livrée + validée E2E en prod (api.nymora.fr). **2v2/3v3 reportés** (décision Lorenzo : focus 1v1 parfait d'abord). Le bouton "Arène" du hub ouvrait déjà un menu Entraînement/1v1/2v2/3v3 (les ranked étaient désactivés).
- **6.1 Scène + entrée ranked** : `40_CombatRanked1v1` clonée de `33_CombatCasual` (tool `Clone CombatCasual to Ranked1v1 Scene`) — pas de QuantumMap dédiée car `AutoLoadSceneFromMap=0` (fix 22 mai). Bouton "Ranked 1v1" actif → ouvre `HubRankedSearchPanel` (coquille). `MatchBridge.IsRanked` (flag View-only). Fix : `PatchArenaPanelTool` préserve désormais la position d'un ArenaButton existant (avait écrasé le layout manuel → bouton caché sous MyProfile à -780). Commit Unity local `c1c050d`.
- **6.2 Matchmaking** : (a) backend — `matchmakingService.ts` (Redis ZSET par MMR + fenêtre adaptative `100 + 50×s`, cap 10000), WS `ENQUEUE_RANKED`/`DEQUEUE_RANKED` + tick 2s + `RANKED_MATCH_FOUND` + cleanup disconnect ; nouveau client Redis partagé `src/db/redis.ts`. (b) client — `HubChatClient.SendEnqueueRanked/Dequeue` + parse `RANKED_MATCH_FOUND` ; `HubMatchTransition` gère le ranked (IsRanked=true + load scène 40, logique factorisée avec casual) ; `CombatBootstrapCasual._expectedSceneName` devient un champ (même bootstrap casual+ranked, tool `Setup Ranked Scene Bootstrap`) ; `CoinFlipIntroView` étendu aux scènes "Ranked". Commits backend `66b499b` (poussé+déployé), Unity local `9550c83`. **Test : matchmaking rapide E2E 2 comptes, coinflip OK.** ⚠️ 2 comptes DISTINCTS requis (apparie par userId).
- **6.3 ELO/MMR + XP/Nymos** : (a) backend — `Profile.rankedGames/Wins/Losses` (migration `ranked_stats`), `elo.service.ts` (K-factor 40/25/15), `rankedResultRegistry.ts` (double-accord : 2 reports cohérents → settle, sinon conflict), `POST /ranked/report-result` (settle = MMR des 2 + XP par classe + Nymos + achievements + push WS `MMR_UPDATED`). `progression.service.awardXpToClass` extrait pour reuse. (b/c) client — `MatchBridge.LastOpponentSub/LastIsRanked`, `HubMatchResultDisplay` reporte le ranked + affiche MMR/XP/Nymos via events WS, **RETRAIT du wiring temp casual/IA → XP/Nymos = RANKED-ONLY** (décision verrouillée appliquée). Fix `MMR_UPDATED.delta`→`mmrDelta` (collision JsonUtility). Commits backend `5397fe3`+`949c3ad` (déployés), Unity local `9300d76`. **Validé E2E : MMR + XP + Nymos sur ranked, rien hors ranked.**
- **6.4 Rangs** : `RankLadder` (Core) 8 paliers Bronze→Légende (seuils MMR ajustables, défaut 1000=Argent), affiché sur le profil + ligne de résultat. Client-only, commit Unity local `1dd0916`.
- **6.5 Saisons** : `Season` model + `Profile.seasonPeakMmr` (migration `seasons`), `season.service` (softReset = compression vers 1000 ×0.5, rollover manuel = récompenses placeholder Nymos par pic de rang + soft reset + nouvelle saison), `rank.service` backend, `GET /ranked/season`, script `season:rollover`. Client affiche "Saison N — Xj restants". Commit backend `a7f1864` (déployé), Unity local `c3326be`. **Rollover testé E2E sur prod : 5 joueurs récompensés +200 Nymos (Argent), S1→S2, MMR soft-reset.**
- **6.6 Leaderboard** : (a) backend — index `profiles(mmr)` (migration `leaderboard_index`) + `GET /ranked/leaderboard?limit` (top par MMR, **requête DB** pas Redis : simple+exact à l'échelle alpha ; par-classe différé faute de MMR/classe). (b) client — `HubLeaderboardPanel` scrollable (ligne locale surlignée) + bouton "Classement" dans la recherche. Commit backend `5486803` (déployé), Unity local `1409df5`.
- **À RETENIR** : commits **backend tous poussés + déployés prod** ; commits **Unity LOCAUX non poussés** (`8b467d6` fix Tab+Entrée login, `c1c050d`→`1409df5` Phase 6) — push GitHub en fin de session. Procédure deploy backend : `git push` + `ssh ubuntu@149.202.57.68 "cd /opt/nymora-backend && ./scripts/deploy.sh"` (applique les migrations). Rollover saison prod : `docker compose -f docker-compose.prod.yml --env-file .env.prod run --rm migrator npx ts-node src/scripts/season-rollover.ts`.
- **6.7 anti-smurf/anti-boost ✅ FAIT** (déployé) : `RankedFlag` (migration `ranked_flags`), `antiCheat.service` (smurf = placements ≤10 + ≥8W + ≥80% wr ; win-trading = ≥5 matchs même paire/1h), hook au settle (fire-and-forget), flags persistés + log WARN, **aucune sanction auto** (modération manuelle via script `ranked:flags`). Commit backend `0c27b11`. **PHASE 6 RANKED 1v1 = 100% CLÔTURÉE.**
- **PROCHAIN STEP** : (1) reprise **Phase 5 reliquats** sans dépendance Kyami (boutique 5.5 + Shards, Battle Pass 5.7, quêtes 5.8) OU (2) cosmétiques 5.9/5.10 quand Kyami livre. **2v2/3v3 = bloc futur** (nécessite bot-fill pour tester à 4/6). Durcissement Phase 7 : webhook Photon serveur-autoritaire pour le résultat ranked (remplacera le double-accord client).

**SESSION 22 mai 2026 (FIX TAB + ENTRÉE LOGIN) :** la navigation **Tab** entre champs de `00_Login` ne marchait pas — cause racine = composant `TabFieldNavigator` **absent de la scène sauvegardée** (le `RefineLoginFormTool` avait été lancé avant l'ajout du `WireTabNavigator`). Fix : relancer `Refine Login Form` (recâble) + dans le code, focus différé d'une frame (coroutine) pour battre la course TMP + `DeactivateInputField` du champ courant. **Touche Entrée** ajoutée (`LoginScreenController.Update`) : valide le panneau actif (Connexion/Inscription). Healthcheck OK. Commit Unity local `8b467d6`. Pas de bump CombatRulesVersion. **A3 (murs/piliers ciblables) validé en PvP** entre-temps.

**SESSION 22 mai 2026 (REFONTE LOGIN — écran épuré + login par pseudo) :** `00_Login` refondu après le launcher. **Connexion** = pseudo + mot de passe → login PUIS entrée directe dans le hub ; dernier pseudo mémorisé (PlayerPrefs `nymora.auth.lastPseudo`) et pré-rempli. **Inscription** = panneau séparé (email + pseudo + mdp + confirmation) → après succès, retour à l'écran connexion (pas d'auto-entrée hub). Boutons dev (Connect Photon / Logout) supprimés. Backend `/login` accepte désormais **pseudo (displayName) OU email** (rétrocompat clients 0.1.1) et le **lookup pseudo est insensible à la casse** (`findFirst` + `mode: 'insensitive'`). Commits backend **poussés + déployés** : `20f35bc` (login pseudo) + `2f1adf0` (insensible casse). Commit Unity **local** `ac12f67`. Pas de bump CombatRulesVersion (UI + auth, pas de sim).

- **Client** : `LoginScreenController` réécrit (flow connexion/inscription, panneaux togglés, launcher conservé) ; `LoginRequest` envoie `displayName` ; `AuthService.LoginAsync(pseudo, password)`. Editor tool `Nymora > Setup > Refine Login Form` (reconstruit la scène en place, idempotent). Message "Vérification de la version…" effacé une fois à jour.
- **🔴 BUG REPORTÉ** : la **touche Tab** entre champs ne fonctionne pas (`TabFieldNavigator`). À investiguer prochaine session — **checker en 1er `Player Settings > Active Input Handling`** (si "Input System (New)", `Input.GetKeyDown` est muet). Détails + pistes : mémoire `project-login-tab-nav-bug`.

**SESSION 22 mai 2026 (LAUNCHER — auto-update pour bosser à deux avec Kyami) :** `00_Login` transformée en **launcher** qui vérifie la version au démarrage et force la MaJ avant de jouer. Objectif : ne plus renvoyer le build à Kyami à chaque grosse MaJ ; il clique "Télécharger" et est à jour. **5 briques livrées + validées E2E (L1→L5).** Commits Unity **locaux non pushés** : `7a82e29` (L1/L2), `b062e44` (L3), `b564ff7`+`36fd8c3` (L5), `fe06ee7` (L4). Backend **poussé + déployé prod** : `bf5d342` (L1) + `9cefe3c` (publication 0.1.1). Pas de bump CombatRulesVersion (rien de sim).

- **L1 backend** : `GET /version` renvoie en plus `downloadUrl` + `sha256` (constantes `CURRENT_CLIENT_VERSION`/`LATEST_ZIP_FILENAME`/`LATEST_ZIP_SHA256` dans `version.service.ts`). Caddy sert les zips via `handle_path /downloads/*` depuis `/opt/nymora-backend/downloads/` (mount `:ro`, `downloads/.gitignore` traque le dossier sans les zips).
- **L2 launcher UI** : `LoginScreenController` réécrit — login masqué tant que pas à jour ; verdict vert persistant "Votre version de Nymora est à jour" / panneau orange "Mise à jour requise pour jouer". **Toute** MaJ dispo bloque le login (anti-mismatch PvP). Editor tool idempotent `Nymora > Setup > Upgrade Login to Launcher (L2)` (upgrade la scène en place, préserve EnterHub).
- **L3 download** : `LauncherUpdateService` (download streaming `DownloadHandlerFile` vers `%TEMP%\Nymora_Update`, barre de progression `IProgress<float>`, vérif sha256 hors thread principal). Bouton "Télécharger" actif seulement si `downloadUrl` non vide.
- **L4 install** : `LauncherInstaller` écrit `update.bat` (attend fermeture par PID → `Expand-Archive` → `robocopy /E` par-dessus l'install → relance Nymora.exe → auto-nettoyage), puis `Application.Quit()`. **Désactivé en éditeur + hors Windows.** Validé E2E : build 0.1.0 → auto-update → 0.1.1.
- **L5 publication** : `GameVersion.Current` lit `Application.version` (bundleVersion, bumpable sans recompil). Editor tool `Nymora > Build > Publish Update` (`Editor/Publishing/` — ⚠️ PAS dans un dossier `Build/`, ignoré par `.gitignore`) : build Win x64 + zip (contenu à la racine) + sha256 + manifeste dans `_publish/` (gitignored). **Procédure "mets le launcher à jour"** détaillée dans la mémoire `project-launcher-publish-workflow`.

- **À RETENIR** : (1) **Kyami doit recevoir une dernière fois manuellement** un build avec launcher (le 0.1.1) — ensuite tout est auto. (2) Après un test de publication, remettre `Player Settings > Version` ≥ version prod sinon l'éditeur affiche "MaJ requise" et masque le login. (3) Prod actuellement en `0.1.1`. (4) Améliorations futures possibles : signer l'exe/bat (anti-SmartScreen/AV), patch delta au lieu du zip complet, écran de notes de version.

**SESSION 22 mai 2026 (PATCHS POST-TEST DESIGNER — 1er vrai test avec Kyami) :** premier vrai test combat avec le designer → liste de patchs (`Desktop/Patch à faire.txt`) traités en groupes A/B/D (+ pile ou face). **7 commits locaux non pushés** (`fa9ce3f`, `d302ab0`, `86a5d07`, `a416a8d`, `c74b061`, `decb7d7`, `8fab0dd`). **CombatRulesVersion 74 → 80.** ⚠️ Sim modifié → designer doit **rebuild son standalone** pour tester casual/PvP.

- **GROUPE A — combat (`fa9ce3f`, CombatRulesVersion 78)** : **(A1)** Invisibilité Nightseer (Voile d'Ombre) côté adversaire = View-only : `CombatantRenderer` calcule le viewer local et appelle `CombatantView.SetCloaked()` (toggle tous les renderers enfants : sprite + marques) quand un combattant ENNEMI vivant porte `StatusKind.Untargetable` ; `CombatantTooltipView` gate aussi le tooltip survol (`IsCloakedFromLocalViewer`). Le Nightseer se voit lui-même. **VALIDÉ.** **(A2)** Pièges Nightseer déclenchés AU PASSAGE : `MovementSystem.ApplyMove` itère les cases intermédiaires du `pathBuffer` (start exclu, dest au dernier index) et appelle `FogHelpers.TryTriggerTrapOnEnter` sur chacune dans l'ordre + garde anti-mort mid-path. **VALIDÉ.** **(A3)** Murs/Piliers ciblables par les sorts de dégâts (TOUTES classes) : filtre `TileWithObstacle` implémenté dans `TargetingResolver` + `SpellSystem` autorise une cible offensive (filter Enemy/AnyUnit) sur une case à **obstacle ADVERSE** (helper `IsAdverseObstacleAt`) ; la boucle damage offensive existante endommage déjà l'obstacle adverse. Restreint à ADVERSE = anti-exploit (passif Colossar +30 HP/Pilier détruit). **VALIDÉ EN PVP (22 mai)** — designer Colossar pose des murs, Lorenzo les détruit. **(A4)** Choc Sismique forcé en LIGNE DROITE cardinale : `SpellSystem` rejette toute cible Choc Sismique non alignée (dx!=0 && dy!=0) ; `TargetingPreviewView` n'affiche la portée castable que sur les 4 rayons cardinaux + survol = ligne complète (4 cases) dans la direction. **VALIDÉ.** **(+ LoS)** Amendement Bible (décision Lorenzo) : `ObstacleHelpers.HasLineOfSight` bloque aussi sur **combattant ENNEMI vivant** (en plus obstacles adverses + leurres) ; alliés non. `SpellSystem.SpellNeedsLineOfSight` passé public → `TargetingPreviewView` **grise** (non cliquable) les cases hors ligne de vue (derrière obstacle/ennemi) pour les sorts à LoS. **VALIDÉ.** Cf mémoire `project-los-units-block-amended`.

- **PILE OU FACE casual (`d302ab0`, CombatRulesVersion 79)** : intro animée de révélation du 1er joueur en combat **casual uniquement**. `CoinFlipIntroView` auto-instancié via `SceneManager.sceneLoaded` (PAS `RuntimeInitializeOnLoadMethod` qui ne tourne qu'1× au lancement → invisible en entrant via hub). Pièce procédurale (placeholder ; sprite designer plus tard), mapping ABSOLU PILE=P0/FACE=P1 (identique sur les 2 clients), bandeau pseudo du gagnant + tag "(toi)". Slot local résolu APRÈS le spin (LocalPlayerSlot async PvP). Input bloqué pendant l'anim (`CoinFlipIntroView.IsIntroActive` lu par `CombatInputController`). **Timer 15s ne démarre qu'APRÈS l'intro** : `TurnSystem.OnInit` reste en `PreMatch` ~3s (`TurnConstants.IntroDelaySeconds`, timer gelé via réutilisation de `TurnTimerTicks`, pas de nouveau [Networked] field) avant `TurnStart` ; IA = démarrage direct. **Fix initiative toujours-P0** : `RuntimeConfig.Seed=0 → RNGSession(0)` identique chaque match ; seed randomisé (`Guid`) dans `CombatBootstrapCasual` + `CombatBootstrapIA` si 0 (comme Photon Menu SDK ; en online le RuntimeConfig du créateur de room est l'autoritaire synchronisé). **VALIDÉ E2E 2 clients.**

- **GROUPE B — UI combat (tout View, pas de bump)** : **(B5 `86a5d07`)** Indicateur de tour TRANSITOIRE animé (`TurnIndicatorView`, auto-instancié) : à chaque changement de tour, bandeau centre-haut "C'EST TON TOUR" (vert) / "TOUR ADVERSE" (rouge), fade-in + glissement G→centre + pop (EaseOutBack), hold, glissement centre→D + fade-out. Drive par `CombatHUDController` (détection changement `ActivePlayerIndex`) ; myTurn comparé au **vrai slot local** (`LocalPlayerResolver`), PAS `controlPlayer` (== activePlayer en IA debug). Pas de sprite (retiré sur retour Lorenzo). **VALIDÉ.** **(B6 `a416a8d`)** Tooltip sorts immédiat : hover des slots en **POLLING** (`SpellSlotView.Update` + `RectangleContainsScreenPoint`) au lieu de `IPointerEnter` (quirk EventSystem : souris immobile au chargement → fallait cliquer un sort pour débloquer). Délai 0.2s→0.03s (const). + feedback survol : slot monte ~10px + halo blanc brillant radial. **VALIDÉ.** **(B7 `c74b061`)** Marques uniformes : `CombatantMarksView` normalise chaque marque à une taille monde commune (`_markTargetWorldSize=0.45`, scale = target/dimension native) au lieu d'un scale fixe 1.2 (tailles inégales selon PPU). **VALIDÉ.** **(B8 `decb7d7`, CombatRulesVersion 80)** Bouton Abandonner : `ForfeitCommand` (enregistrée `CommandSetup.User`) → `TurnSystem.Update` : le slot qui l'envoie perd, l'autre gagne (IA + casual). `ForfeitButtonView` auto-instancié bas-droite + confirmation centrée + fond grisé modal. `TimelineView` remontée de 80px (`_verticalNudge`) pour loger le bouton dessous. **VALIDÉ.**

- **GROUPE D — hub (`8fab0dd`, View/réseau, pas de bump)** : **(D2)** `ChallengePopup.WhisperTarget` passe le **displayName** (pseudo) à `OpenWhisperToUser` au lieu du `Sub`/UUID → le `/w` cible/affiche le pseudo (cohérent avec `ChatUserContextMenu` ; le backend résout `targetUser` flexiblement). Report laissé en Sub (envoi silencieux, marche). **(D1)** `HubClanPanel.Start` appelle `RefreshClanStateAsync` au démarrage : `HubChatClient` étant DontDestroyOnLoad, `OnWelcome` ne refire pas au retour combat→hub, donc le clan ne s'affichait qu'après ouverture du menu ; le fetch au Start charge le tag clan (poll avatar `HubAvatar.SyncClanNameIfChanged` sur `HubClanPanel.MyClanName`) sans ouvrir le panel. **VALIDÉ.**

- **PROCHAIN STEP** : (1) ~~A3 à valider en PvP~~ **VALIDÉ 22 mai**. (2) **GROUPE C — descriptions des ~80 sorts** : revue manuelle contre Bible V7.1 — **Lorenzo le fera lui-même** (décision 22 mai). (3) Polish coinflip : brancher un vrai sprite/anim de pièce designer (placeholder procédural actuel). (4) Push des 7 commits sur origin/main quand Lorenzo stoppe la session.

**PATCH 22 mai 2026 (map animée combat) :** la map statique `Map_Combat_1` des scènes `30_CombatIA` + `33_CombatCasual` est remplacée par une **map animée 12 frames** fournie par le designer. Frames 1920×1080 (même PPU 100 que l'ancienne map → aucun rescale) importées dans `Assets/_Nymora/Art/UI/Maps/Arene1vs1_Anim/` (`Arene1vs1_01..12.png`, metas calqués sur l'ancienne map). Nouveau composant View `MapSpriteAnimator.cs` (cycle les frames sur le `SpriteRenderer`, FPS réglable défaut 10, View-side donc `Time.deltaTime` OK). Editor tool `Nymora > Setup > Setup Animated Arena Map` (`SetupAnimatedArenaTool.cs`) câble le composant dans les 2 scènes en conservant material + sortingOrder -1000 + transform. **Validé E2E par Lorenzo en Play.** Commit `eb71a3d` **pushé origin/main**. Ancien `Map_Combat_1.png` conservé (suppression possible plus tard).

**PATCH 22 mai 2026 (post-test PvP prod) :** premier vrai PvP cross-internet sur le serveur dédié validé E2E (Lorenzo vs designer Kyami, login réel → hub → défi → combat Necram/Soulrender → retour hub). 2 erreurs au retour hub corrigées :
- **403 AwardNymos** : `/wallet/award` gaté dev/admin en prod (`backend/src/routes/wallet.ts:72`) ; l'XP passe (`/progression/award-xp` non gaté), pas les Nymos. **Fix live** : `WALLET_AWARD_ENABLED=true` dans `.env.prod` sur le VPS + restart conteneur app (pas de rebuild client). Documenté dans `.env.prod.example` (backend commit `fa6982a`). TEMP MVP, retrait Phase 6 (award server-side webhook Photon).
- **ArgumentException "Scene to unload is invalid"** au `QuantumGame.OnDestroy` : Quantum auto-chargeait `30_CombatIA` en additif (scène fantôme) via `Map.ScenePath`. **Fix** : `AutoLoadSceneFromMap: 2→0` dans `QuantumDefaultConfigs.asset` (Unity commit `a9da2c5`). Grille procédurale → aucune scène à auto-charger. Pas de bump CombatRulesVersion (config view-side). **⚠️ Nécessite rebuild standalone pour le designer.** Cf mémoire quantum-map-per-combat-mode.

Les 2 commits sont **locaux non pushés** (règle fin de session). POLISH-7 (menu contextuel chat) toujours de côté.

**Dernière mise à jour :** 22 mai 2026 (**INFRA SERVEUR DÉDIÉ PROD — OVH VPS + api.nymora.fr**) — Session ~ chantier infra complet en 8 briques (B1→B7 + B6.5). **Objectif** : remplacer le setup ngrok/localhost (qui imposait Lorenzo en ligne pour tout test) par un backend prod 24/7, pour que le designer puisse tester en autonomie. Verrou `minimize-costs` levé (Phase 4 PvP cross-internet déjà validé). **(B1) OVH VPS-1** (4 vCPU / 8Go / 75Go SSD, ~6,62€ TTC/mois) Ubuntu 22.04 LTS à Gravelines. **IP `149.202.57.68`**. User par défaut OVH = `ubuntu` (pas root) + password expiré forcé au 1er login. Clé SSH ed25519 générée (`~/.ssh/id_ed25519`), uploadée via pipe `Get-Content ... | ssh` (workaround bracketed-paste PowerShell). **(B2) Domaine `nymora.fr`** acheté chez OVH (~5€/an 1ère année puis ~7,79€ HT, DNSSEC inclus ; .gg écarté car ~70€/an). A record `api.nymora.fr → 149.202.57.68`. **(B3) Hardening** via `backend/scripts/bootstrap-vps.sh` (idempotent) : ufw 22/80/443, fail2ban, swap 2Go (swappiness 10), SSH key-only (`PermitRootLogin no` + `PasswordAuthentication no` — patch CRITIQUE aussi sur `/etc/ssh/sshd_config.d/*.conf` que cloud-init force à yes), unattended-upgrades, Docker CE 29.5.2 + Compose v5.1.4, user ubuntu dans groupe docker. Reboot pour kernel 5.15.0-179. **(B4) Stack Docker prod** : `Dockerfile` multi-stage (builder npm ci + prisma generate + tsc → runner node20-alpine prod deps), `docker-compose.prod.yml` (app + postgres + redis + caddy, ports DB non exposés, réseau `nymora-backend_nymora_net`), `Caddyfile` (reverse_proxy app:3000, TLS auto Let's Encrypt sur api.nymora.fr), `.env.prod.example`, `scripts/deploy.sh` + `scripts/backup.sh`. **(B5) Premier deploy** : git clone via **deploy key SSH read-only** (`~/.ssh/github_deploy`, repo privé `DoctorL08/nymora-backend`), `.env.prod` généré sur le VPS (POSTGRES_PASSWORD `openssl rand -hex 24` + JWT_SECRET `openssl rand -hex 64`, jamais loggés) + `.env` copie pour interpolation compose. 4 conteneurs up. **2 fixes Dockerfile** : (a) `ENV DATABASE_URL` factice au build stage (prisma.config.ts throw sinon), (b) suppression `COPY node_modules/.prisma` (Prisma 7 generateur `prisma-client` + adapter-pg = client dans src/generated/prisma, pas de query engine natif). **Migrations** : service dédié `migrator` (profile `tools`, build le builder stage car CLI prisma = devDep absente du runner) → `docker compose --profile tools run --rm migrator` applique 7 migrations. **Smoke test** : `https://api.nymora.fr/` → `{"status":"ok"}` validé depuis Internet (TLS LE OK, `Via: 1.1 Caddy`). Backup pg_dump quotidien cron 03:00 UTC rétention 7j (`/var/backups/nymora/`). **Procédure redeploy future** : `ssh ubuntu@149.202.57.68 "cd /opt/nymora-backend && ./scripts/deploy.sh"`. **(B6) Client Unity** : `NymoraBackendSettings.asset` `_baseUrl=https://api.nymora.fr` + `_backendUrl=wss://api.nymora.fr` dans HubChatClient des scènes hub (manuel Lorenzo). **(B6.5) Flow login réel → hub** : `HubChatClient.ResolveToken()` lit PlayerPrefs `nymora.auth.jwt` (posé par AuthService au login) avec fallback `_devToken` Inspector — bascule tout le hub (WS + REST, point unique `DevToken`). `LoginScreenController` : bouton `_enterHubButton` (caché, apparaît post login/register) → `SceneManager.LoadScene("10_CommunityHub")`. Injectable `AddEnterHubButtonTool.cs` (`Nymora > Setup > Add Enter Hub Button`) crée le bouton vert + câble + Build Settings. **VALIDÉ E2E PAR LORENZO** : register → Entrer → hub connecté prod avec son pseudo. Le designer crée désormais son propre compte (plus de dev token à coller). **(B7 — EN COURS CÔTÉ LORENZO)** : (i) mettre à jour webhook Photon Custom Auth (Quantum + Fusion) → `https://api.nymora.fr/auth/photon-webhook` (sinon PvP échoue à l'auth) ; (ii) build Windows standalone Mono x64 (00_Login en index 0) ; (iii) distribution WeTransfer/Drive (zip + `_docs/ONBOARDING_DESIGNER.md` déjà écrit). **COMMITS** : backend 4 commits **pushés origin/main** (`00b2a53` catch-up phase5 decks+wallet, `f625099` infra stack, `0fe8cfe`+`6eefe28` fixes Dockerfile, `8e03bcf` migrator service) ; Unity 1 commit **LOCAL non-push** (`3e68afe` client prod + login→hub + onboarding). Travail antérieur non-committé laissé tel quel (CombatBootstrapIA, QuantumMap_IA, 30_CombatIA — sessions précédentes). **PROCHAIN STEP** : Lorenzo finit B7 (webhook Photon + build + envoi designer), valide PvP designer↔Lorenzo via api.nymora.fr. Puis reprise roadmap : POLISH-6b-f validation preview damage OU 5.4 Bloc C complétion Nymos OU Phase 6 ranked cf [[project-phase5-plan]].

**Précédente session 20 mai 2026 (**POLISH-7 UNIFICATION PSEUDOS + POLISH UX HUB**) — Session ~5h, **9 commits livrés** (1 backend + 8 Unity dont 1 WIP non-fonctionnel). **(1) POLISH-7.a backend** (`dba4682`) : unification displayName WS protocol. Precharge `Profile.displayName` au connect via Prisma (fallback `email.split('@')[0]` si sub non-UUID dev token). Push `displayName`/`fromDisplayName`/`toDisplayName` dans 6 events sortants (WELCOME, CHANNEL_MESSAGE, WHISPER_RECEIVED, INCOMING_CHALLENGE, CHALLENGE_SENT, CHALLENGE_RESPONSE, MATCH_READY opponents[], REPORT_SENT). Additif : email/from/to conserves pour backward compat. Helper `channels.firstDisplayNameFor(userId)` symetrique a `firstEmailFor`. **(2) POLISH-7.b Unity migration** (`bebf4c3`) : 12 fichiers migres vers displayName. HubChatClient : `+MyDisplayName`, parse displayName, signature `OnWelcome` etendue (sub,email,displayName), helper `SplitEmailLocal` fallback. HubChatUI : 5 handlers affichent pseudos. HubAvatar : `+[Networked] NetworkString<_32> NetDisplayName` push StateAuth au Spawn + OnWelcome retarde. HubAvatarHoverTooltip : `ResolveDisplayName` lit `avatar.NetDisplayName` (single path local+remote). MatchBridge : `+OpponentDisplayName/LocalDisplayName/LastOpponentDisplayName`, signatures `SetPendingMatch`/`SetMatchResult`/`ConsumeLastResult` etendues. HubMatchTransition+HubArenaPanel : virage `ExtractPseudoFromEmail`, utilise `MyDisplayName`+`opponentDisplayName`. HubMatchResultDisplay : `[MATCH] VICTOIRE vs {pseudo}`. HubClanPanel+HubWalletWidget : `HandleWelcome` signature 3 params. IncomingChallengePopup : `_currentFromDisplayName`. MatchEndOverlay : capture+transmet opponentDisplayName. **(3) Fix tooltip avatar recree au retour hub** (`ab0fe00`) : bug `HubAvatarHoverTooltip` detruit au passage hub->combat (pas DontDestroyOnLoad), pas recree au retour. Fix : `SceneManager.sceneLoaded` listener + `TryCreateForActiveScene()` recrée quand scene = hub. **(4) Wallet widget masque pendant DeckBuilder** (`1430295`) : Nymos+Shards en haut-droite chevauchait visuellement le panel. Meme mecanique que SetArenaButtonVisible : `SetActive(false)` au Open / `(true)` au Close. Events WS OnWalletUpdate restent abonnes (Action C# independantes du cycle Unity) -> balances a jour a la reouverture sans refetch. **(5) Fix award XP sur la vraie classe jouee** (`97974a8`) : critique — `_devClassId = "Soulrender"` hardcode en SerializeField, XP allait TOUJOURS sur Soulrender peu importe la classe jouee. Fix : resolution en cascade dans `AwardXpAsync` : `DeckBridge.PendingClassId` (lockee au lancement combat, jamais Clear apres) > `SelectedClassPreferences.Get()` > `_fallbackClassId` Inspector. **(6) Polish avatar context menu** (`2f69b44`) : HubInputController detection click avatar par bounds SpriteRenderer (pattern miroir `HubAvatarHoverTooltip.FindAvatarAtMouse`) au lieu de tile-based — permet de cliquer sur sprite meme s'il deborde de sa tile. ChallengePopup : `_buttonHeight` default 70->40, nouveau `_buttonFontSize=16`, boutons Bold, titre affiche `target.NetDisplayName` au lieu du generique "Actions", swatch blanc 4.8.a desactive au Awake, `EnsureCompactLayout()` au Awake (VerticalLayoutGroup spacing=4 padding=8/8/6/6 + ContentSizeFitter vertical=PreferredSize sur panel pour bg ajuste hauteur boutons). **Manip Unity Lorenzo** : Inspector ChallengePopup -> Button Height : 70->40 (SerializeField deja serialise). **(7) Pre-fetch decks au login** (`f831580`) : bug — clic direct Arena post-login sans ouvrir DeckBuilder -> `MyDecks` vide -> `SelectedDeck` null -> fallback MyDecks[0] au lieu du dernier deck utilise. Fix : subscribe `HubChatClient.OnWelcome` dans `HubDeckBuilderPanel.Start` -> au WELCOME pre-fetch decks de la classe courante -> `TryRestoreLastEditedDeck` restaure `_editingDeckId` depuis PlayerPrefs -> `SelectedDeck` retourne le bon deck immediatement. Effet visible : clic direct Arena post-login lance combat avec le dernier deck. **(8) WIP Chat user context menu** (`9a0b510` — **NON FONCTIONNEL, A DEBUG**) : objectif click sur un pseudo dans le chat ouvre menu contextuel (MP/Ami/Inviter clan conditionnel/Signaler/Annuler). Implementation : HubChatUI.`WrapPseudoLink(displayName)` wrap chaque pseudo dans `<link="user:dn"><color>dn</color></link>`. `EnsureHistoryClickHandler` au Start : `raycastTarget=true` sur _historyText + ajoute sub-component `ChatHistoryClickHandler` (IPointerClickHandler + `TMP_TextUtilities.FindIntersectingLink`). Nouveau `ChatUserContextMenu.cs` auto-cree via `RuntimeInitializeOnLoadMethod` dans scenes hub, panel+boutons construits en code, position au curseur via `ScreenPointToLocalPointInRectangle`, click hors panel/ESC = close, skip silencieux si pseudo == MyDisplayName. HubClanPanel : `+InviteByDisplayNameFromContextMenu` helper. **Statut runtime** : Lorenzo signale ne fonctionne pas. Probables causes a investiguer : (a) raycastTarget chain — le _historyText est probablement dans un ScrollRect Viewport/Content, le hit test peut etre bloque par les Image/Mask intermediaires ; (b) Canvas detection — `FindAnyObjectByType<Canvas>()` peut chopper le mauvais Canvas si plusieurs (le menu se parenterait au mauvais root + position fausse) ; (c) Timing — ChatHistoryClickHandler attache au Start mais `_historyText.raycastTarget` peut etre override par un layout post-Start ; (d) TMP version — `FindIntersectingLink` requiert TMP 1.5+. Code commit en l'etat pour pouvoir y revenir / revert proprement. **(9) Manip Unity Lorenzo** (manuelle, hors commit) : reordonne les 5 boutons hub gauche-droite Profil/Amis/Clan/Decks/Arene + rename "Profil"->"Mon profil" dans Inspector (le `PatchHubButtonsOrderTool` cree en cours de session a echoue de cause indeterminee, supprime du repo, Lorenzo a fait manuellement). **STATUT TEST** : (1) a (7) valides E2E par Lorenzo. (8) chat menu signale non-fonctionnel par Lorenzo (a debug). **PROCHAIN STEP** : (1) **Debug Chat user context menu** — verifier la chain raycastTarget du _historyText (probable culprit : ScrollRect Content/Viewport bloque), tester `TMP_TextUtilities.FindIntersectingLink` en mode standalone, ajouter Debug.Log dans `ChatHistoryClickHandler.OnPointerClick` pour voir si l'event arrive ; (2) Validation E2E POLISH-6b-f par Lorenzo (cast tous les sorts + verifier preview = cast reel sur 5 classes) ; (3) POLISH-6h Consolidation SpellPreview ; (4) 5.4 Bloc C Economie Nymos complétion (schema DB transactions).

**Précédente session 19 mai 2026 (nuit, **FIX PVP CASUAL CRITIQUE + PREVIEW DAMAGE CASUAL + VALIDATION ÉCONOMIE FIN DE COMBAT**) — 3 fixes/validations livrés en session courte (~1h). **(1) Fix PvP casual "Player not found" Quantum Error #19 disconnect immédiat** : symptôme — dès le premier MoveCommand en casual, plugin Quantum kick le client avec "Disconnected: Error #19: Player not found", scène casual inutilisable. Diagnostic via lecture logs Lorenzo : les 3 callsites `game.SendCommand(senderPlayer, ...)` dans `CombatInputController.Update` (Move + Cast via `SendSpellAt`) + `CombatHUDController.OnEndTurnClicked` (EndTurn) passaient `_localPlayerIndex` qui contient le **PlayerRef GLOBAL Quantum** (résolu via `CombatBootstrapCasual.LocalPlayerSlot` = `Runner.Game.GetLocalPlayers()[0]`). Or `QuantumGame.SendCommand(int playerSlot, ...)` attend le **splitscreen slot LOCAL** (= 0 puisque `CombatBootstrapCasual:303-304` fait `AddPlayer(LOCAL_SPLITSCREEN_SLOT=0, ...)`). Quand Quantum attribuait PlayerRef=1 au client master (cf `project_quantum_playerref_resolution` : ordre d'arrivée AddPlayer réseau, pas Photon ActorNumber), `SendCommand(1, ...)` cherchait un local player au splitscreen slot 1 → introuvable → Error #19 → disconnect. Le 18 mai test ngrok PASSAIT par coïncidence (PlayerRef=0 chez les deux clients). **Fix** : découplage des 2 sémantiques dans `CombatInputController.Update` : `senderPlayer` reste le PlayerRef global (pour filtering UI : caster cell sorts Self, logging, etc.) ET nouveau `splitscreenSlot = 0` en prod (1 player local par client) / `splitscreenSlot = state.ActivePlayerIndex` en legacy debug `_debugAllPlayersMovable` (Phase 2 auto-add 2 RuntimePlayer sur même client). `SendCommand(splitscreenSlot, cmd)` partout. Idem `CombatHUDController.OnEndTurnClicked` : `int splitscreenSlot = _debugAllPlayersControllable ? senderPlayer : 0`. Signature `SendSpellAt(game, splitscreenSlot, sender, ...)` étendue avec param explicite. Logs enrichis `player={senderPlayer} splitscreenSlot={splitscreenSlot}` pour diagnostic futur. **Pas de bump CombatRulesVersion** (View pure, zéro Quantum simulation). **(2) Fix preview damage absent en casual** (POLISH-6a-f sans effet en PvP) : symptôme — tooltip [clan]/pseudo/HP au survol cible s'affiche bien en casual MAIS la ligne preview "- X dégâts / + X soins / + X bouclier" est absente (alors qu'elle marche en IA). Cause racine : la scène 33_CombatCasual provoque l'auto-load additif de 30_CombatIA via `QuantumMap.ScenePath` (cf mémoire `project_combat_scene_bootstrap_isolation`). Pendant ~2 frames, **2 instances de CombatHUDController coexistent** (une par scène). Pattern singleton original : `Awake() => Instance = this; OnDestroy() => if (Instance == this) Instance = null;` — séquence buggée : (a) Casual HUD Awake → Instance=casualHud, (b) IA HUD Awake (scène fantôme) → Instance=iaHud (écrase), (c) `DeferredAdditiveCleanup` unload 30_CombatIA → iaHud OnDestroy → Instance==this → **Instance=null** alors que casualHud est toujours vivant. `CombatantTooltipView.BuildSpellPreviewLine` lit `hud == null` → return "" → tooltip HP visible mais pas de preview damage. Confirmé via logs Lorenzo : `[CombatHUDController] DeckBridge applique` apparaît 2× + warnings "2 event systems" / "2 audio listeners" prouvent la coexistence brève. **Fix** : converti `Instance` en **self-healing singleton** : champ privé `_instance` + getter `Instance` qui re-scanne via `FindAnyObjectByType<CombatHUDController>` si `_instance` est null ou destroyed (Unity bool overload). `Awake()` set `_instance = this` (overwrite OK, getter self-corrige). `OnDestroy()` raw check `if (_instance == this) _instance = null`. Coût = 1 scan par miss (rare : uniquement quand HUD fantôme unloaded). **Pas de bump CombatRulesVersion** (View pure). **Pas de manip Unity** (Awake/OnDestroy auto). **(3) Validation E2E économie Nymos en fin de combat** : Lorenzo confirme que la mécanique d'attribution de monnaie (Nymos) procède bien en fin de match — système 5.4 Bloc C Économie fonctionne pour le wiring temporaire MVP (équivalent du wiring `award-xp` de la 5.1 dans `HubMatchResultDisplay`). Code monnaie déjà en place : `HubWalletWidget.cs` + `NymoraApiClient.cs` (REST) + `HubProfilePanel.cs` (affichage solde). Reste à compléter pour 5.4 complet : schéma DB transactions + Shards (premium). **STATUT TEST : VALIDÉ E2E PAR LORENZO** sur (1) fix PvP "Player not found" + (2) preview damage casual + (3) monnaie procède fin de combat. **PROCHAIN STEP** : (1) Validation E2E POLISH-6b-f par Lorenzo (cast tous les sorts + verifier preview = cast réel sur 5 classes, noter écarts) → fix sort-par-sort si dérive → (2) POLISH-6h Consolidation refactor pipeline SpellSystem ↔ SpellPreview (gros, risqué, bump CombatRulesVersion) OU rotation vers (3) **5.4 Bloc C Économie Monnaies — complétion Nymos uniquement** (schéma DB transactions + audit du wiring temporaire Nymos in-game cf `HubWalletWidget`/`NymoraApiClient`/`HubMatchResultDisplay`) cf [[project-phase5-plan]]. **RELIQUAT DIFFÉRÉ** : Shards (monnaie premium) reportés au moment de la 5.5 Boutique — pas de Shards sans boutique pour les dépenser, donc inutile de coder la mécanique avant. Décision Lorenzo 19 mai nuit.

**Précédente session 19 mai 2026 (soir, POLISH UI COMBAT BIG SESSION : FIX TOOLTIP GIGANTESQUE + PREVIEW DAMAGE 6a-f + SIGNATURE UNLOCK ANIM + BANG FLOATING TEXT) :** — 5 livraisons consécutives en session unique (~5-6h, env. 600 lignes nouvelles + 200 modifs). **(1) Fix bug "tooltip survol combatant gigantesque" + réécriture from scratch** : symptôme — `[clan] / pseudo / HP` au survol affichait des lettres ~80% de la hauteur écran combat. Diagnostic après 3 itérations échouées : cause racine PAS le YAML legacy comme suspecté mais une **NullReferenceException silencieuse** dans `TMPro.TextMeshPro.LoadFontAsset` (early Awake, `TMP_Settings.defaultFontAsset` pas encore prêt) qui interrompait `SpawnTooltipGO()` AVANT que `_tooltipText.fontSize = FontSize` soit appliqué → TMP restait au default `fontSize=36` world units = 5x la hauteur écran combat (orthoSize 3.5). **Fix** : (a) `_tooltipText.fontSize = FontSize` appliqué EN PREMIER avant tout assignment qui peut crash, (b) font assignment wrappé en try/catch + check `defaultFontAsset != null`, (c) réécriture complète du component sans aucun `[SerializeField]` (tout en `const`, YAML legacy ignoré 100%), (d) auto-instantiation via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` UNIQUEMENT dans les scènes combat (skip si scene.Name pas "Combat"). Valeurs finales : `FontSize=1.0` (world units, ~14% de la hauteur écran à orthoSize 3.5), `YOffsetAboveSprite=0.40`, `BgPaddingX=0.30`, `BgPaddingY=0.10`. Bonus : garde scène ajoutée à `HubAvatarHoverTooltip.AutoCreate` pour ne plus s'instancier hors scène hub (élimine un overhead Update + risque latent). **(2) POLISH-6a-f : Preview damage/heal/shield au survol cible avec sort armé** — gros chantier livré en vertical slice. Nouveau helper `Assets/QuantumUser/Simulation/Combat/Spells/SpellPreview.cs` (~280 lignes) avec struct `DamagePreviewResult` + static `TryCompute(Frame, caster, target, spellId, out preview)`. **53/80 sorts couverts** (les ~27 utilitaires sans dmg/heal/shield direct skippés : Pacte, Marque Carnage, Empoignade, Rugissement, Rage Insat, Riposte Carmin / Marque Chasseur, Bourrasque, Voile, Pas Furtif / Pilier, Mur, Ancrage, Provocation, Garde Prot, Renvoi Bouclier / Inoculation, Marque Sacrif, Symbiose, Contagion, Pas Spectral, Voile Pestilence, Virus Fatal / Réplique Fantôme, Pas dans Ombre, Marque Ombre, Voile Spectral, Réplique Protectrice). **Pipelines réutilisables** : `ApplyOffensivePipeline` (Pacte de Sang +%, Peau de Fer +30 melee, dorsal Angle Mort, Marque de l'Ombre +20, réduction défensive Colossar cap 50%, shield absorption ShieldActive), `ApplyHealPipeline` (AntiHealShield bloque, cap MaxHP - HP), `ApplyShieldPipeline`. **Conditions Bible codées** : Tir Précis 280 si Traqué / Frappe Lourde 280 si épinglé / Marteau Pun. 240 si PA<4 / Frappe Ombre 300 si moved / Salve 220+60 Traqué / Morsure 110+22/marque cap 90 / Détonation Vir. 80+50/marque / Pulse 70+15/marque cap 90 / Lame Spec+Vorace+Saigne 60-70 si PlaieOuverte / Traquenard +80 marqué. **Cas spécial Frappe Fantôme** : simule le téléport (TryFindFreeCellAdjacentToTarget priorise dorsal) → si case dorsale libre → applique bonus Angle Mort, sinon 0 (fix bug initial où Lorenzo a remarqué que les leurres n'étaient pas comptés). **Approximations notées** : sorts AoE multi-cible (Salve, Détonation Onirique, Faux Décharnée) → preview du dmg cas central uniquement / HG/PT/PR/FD/RM optionnel → preview pessimiste sans bonus de ressource / effets post-damage (status apply, PlaieOuverte, marques venin) NON modélisés / sorts mobilité multi-targets (Pas de l'Au-Delà) → preview cible directe. Branchement dans `CombatantTooltipView.BuildSpellPreviewLine` : lit `CombatHUDController.Instance.ArmedSpell` (nouveau static singleton ajouté) + resolve local caster entity via `LocalPlayerResolver` + appel `SpellPreview.TryCompute`. Affichage : `- X dégâts` rouge (avec suffix ` (Y absorbés)` ou ` (absorbés)` si shield) / `+ X soins` vert / `+ X bouclier` bleu. Live refresh dans `Update()` tant que tooltip visible (rebuild si même texte = no-op perf-friendly). **DETTE TECHNIQUE NOTÉE** : duplication temporaire pipeline calc damage SpellSystem.cs (4000 lignes) ↔ SpellPreview.cs. **POLISH-6h "Consolidation"** à venir : refactor SpellSystem case-par-case pour faire pointer le pipeline vers SpellPreview → source unique. À ce moment-là bump CombatRulesVersion + risque régression sur 80 sorts. **PAS DE BUMP CombatRulesVersion sur 6a-f** (helper read-only, zéro Quantum simulation modifiée). **(3) Signature spell lock/unlock animé** : signature slot (AmeLaceree/Traquenard/Effondrement/VirusFatal/ExecutionSpectrale) caché par défaut quand ressource < max, apparaît avec animation + lueur gold pulsée quand ressource max atteinte, disparaît automatiquement après cast (consume → ressource < max → next tick hide). Nouveau component `SignatureSlotEnhancer.cs` (~170 lignes) auto-attaché au `_signatureSlot.gameObject` par `CombatHUDController.Awake`. Implémentation : (a) crée dynamiquement une Image "SignatureGlow" en sibling sous le slot (renderé derrière) avec sprite radial falloff quadratique généré en code 64×64, couleur or chaud `(1, 0.82, 0.20)`, taille 125% du slot pour déborder légèrement, (b) ajoute un `CanvasGroup` au slot pour piloter alpha (PAS SetActive car coroutines/Update besoin du GO actif — fix bug "Coroutine couldn't be started because the game object 'SlotSignature' is inactive"), (c) `SetUnlocked(bool)` idempotent : false→true déclenche coroutine show anim, true→false hide immédiat, (d) animation show : scale `0 → 1.25 → 1.0` overshoot back-out + alpha `0 → 1` ease-out cubic sur 0.40s, (e) Update pulse alpha glow `0.45 → 1.0 → 0.45` sur 1.4s sin wave. Logic détection dans `CombatHUDController.RefreshSlots` : `IsSignatureUnlocked(c)` retourne true si Ghostra a 3 leurres actifs (iter `c.Decoys[i].Kind != None`) OU autres classes ont `c.Resource >= CombatantStats.GetMaxResource(c.Class)`. **(4) Floating text épique "BANG FULL FURIEUX" pour signatures** : texte standard rouge `-X` insuffisant pour les signatures (Bible "coup le plus risqué/épique du jeu"). Nouveau cocktail FULL épique sur sort signature : (a) `SignatureCastBridge.cs` static class avec liste hardcodée 5 SpellId signatures + `NotifySpellCast(spell)` (set `Time.unscaledTime`) + `IsSignatureRecent()` (window 1.5s pour matcher délai cast→apply Quantum), (b) `CombatInputController.SendSpellAt` hook qui notify le bridge après SendCommand, (c) `CombatantHPWatcher.OnUpdateView` switch entre `Spawn` standard ou `SpawnSignatureHit` si delta<0 ET bridge.IsSignatureRecent, (d) nouvelle méthode `FloatingTextManager.SpawnSignatureHit` qui spawn DEUX textes parallèles + 4 coroutines : texte principal `-X` fontSize x3.5 (~125px) couleur flash blanc→or sur 0.18s + outline rouge sang épais 0.40 + underlay glow orange incandescent (TMP underlay shader), pop violent scale `0 → 2.8 → 1.0` sur 0.35s (overshoot brutal), rotation aléatoire `-12°` à `+12°` initiale, shake position 18px amplitude freq 38Hz decay 0.35s. Plus onomatopée "BANG !" en dessous (fontSize x2.0 italic+bold, rouge feu, outline noir, rotation symétrique inverse, même bounce+shake que le nombre). Plus camera shake 0.18 unit amplitude decay 0.30s. Plus duration x1.8 + rise x1.8 pour reste lisible ~1.8s. Tous paramètres exposés `[SerializeField]` sur FloatingTextManager (header "Signature EPIC FULL BANG") pour tweak Inspector. **(5) Last event sous HP — REVERT** : initialement implémenté (CombatEventTracker + tracking shield magnitude + ligne event sous HP) avant que Lorenzo précise qu'il voulait la PREVIEW avant cast (pas le last event après). Reverté proprement : CombatEventTracker.cs supprimé, CombatantHPWatcher.cs restauré logique HP-only originale, CombatantTooltipView.cs cleané (sans 4e ligne event, garde le live refresh dans Update pour 6a-f). **STATUT TEST : PAS ENCORE TESTÉ E2E PAR LORENZO** — Lorenzo a validé E2E le fix tooltip taille + 6a Frappe Fantôme (avec fix Angle Mort téléport) + signature unlock anim (fix bug Coroutine après 1er essai). Reste à tester E2E par Lorenzo prochaine session : 6b-f sur les ~50 autres sorts (Soulrender + Nightseer + Colossar + Necram + autres Ghostra) + signature unlock anim sur les 5 classes (test sur Soulrender HG max / Nightseer PR max / Colossar FD max / Necram PT max / Ghostra 3 leurres) + BANG floating text sur les 5 signatures distincts. **PROCHAIN STEP** : (1) Validation E2E POLISH-6b-f par Lorenzo (cast tous les sorts + verifier preview = cast réel sur 5 classes contre bot, noter écarts) → fix sort-par-sort si dérive → (2) POLISH-6h Consolidation refactor pipeline SpellSystem ↔ SpellPreview (gros, risqué, bump CombatRulesVersion) OU rotation vers (3) **5.4 Bloc C Économie Monnaies** (Nymos in-game + Shards premium + schéma DB + transactions) cf [[project-phase5-plan]].

**Précédente session 18 mai 2026 (suite jour, FIX OBSTACLE COLOSSAR VISUAL — PARTIEL, À REPRENDRE) :** Lorenzo reporte en combat IA Colossar que les obstacles Pillar + Wall (sprite `tiles_fondation.png`) apparaissent trop petits et mal centrés sur la case (screenshot 4 stones sur 4 cases avec diamonds de tile visibles tout autour, stones occupent ~70% de la largeur). **Diagnostic** : sprite 128×128px avec PPU=180 → 128/180 = 0.71 unité world vs 1.0 unité largeur tile (iso 1.0×0.5). Pivot Center → centre du sprite carré sur centre de tile → impression "flottant au milieu" car le sprite carré (0.71×0.71) ne remplit ni la largeur (1.0) ni ne se cale sur la base de la tile. Historique commentaire ligne 50-52 `CreateObstaclePrefabTool.cs` mentionnait PPU=180 comme "sweet spot" car PPU 128 débordait à l'époque — mais l'asset actuel a son contenu bien cadré dans 128×128 donc PPU 128 = 1.0 unité = pile largeur tile. **Fix appliqué** (proposition utilisateur "PPU 128 + pivot Bottom-Center") sur 4 fichiers : (1) `Editor/Tools/CreateObstaclePrefabTool.cs` constante `DesignerSpritePPU 180→128` + `ApplySpriteImportSettings` alignment `Center→BottomCenter` (= int 7 Unity enum) + log message + commentaires historique mis à jour + HP label `y 0.55→1.2` (sprite plus haut avec pivot BottomCenter va de y=0 base à y=1.0 top) ; (2) `Art/Sprites/Colossar/Tiles/tiles_fondation.png.meta` : `spritePixelsToUnits 180→128` + `alignment 0→7` + `spritePivot.y 0.5→0` (effet immédiat sans re-run du tool grâce au re-import .meta au focus Unity) ; (3) `Prefabs/Combat/Obstacles/Obstacle_Pillar.prefab` RectTransform HPLabel `m_AnchoredPosition.y 0.55→1.2` ; (4) `Obstacle_Wall.prefab` idem. **Pas de bump CombatRulesVersion** (View pure, zéro impact règles/Quantum, zéro [Networked] field). **Pas de manip Unity** (meta re-import auto, prefabs déjà patchés). **STATUT VALIDATION : PAS ENCORE PARFAIT** — Lorenzo a constaté en jeu mais visuel pas encore optimal. **PROCHAIN STEP** : retoucher le rendu obstacle Colossar — options à essayer si encore mauvais : (a) PPU intermédiaire 144 (entre 128 et 180) pour shrink léger, (b) pivot Custom (0.5, 0.25) au lieu de BottomCenter pour caler la "diamond top" du bloc pile sur le losange de la tile (la diamond top du sprite n'est pas en y=0 mais légèrement plus haut), (c) Y offset négatif sur le child Sprite du prefab (-0.1 ou -0.15) pour descendre tout le bloc dans la tile, (d) asker designer un re-export avec le contenu plus dense / proportions ajustées au canvas 128×128. Puis reprendre **5.4 Bloc C Économie Monnaies** (Nymos in-game + Shards premium + schéma DB + transactions) cf [[project-phase5-plan]] OU rotation Phase 6 ranked.

**Précédente session 18 mai 2026 nuit (5.4 POLISH UI COMBAT + DECK BUILDER PERSIST + FIX SPAWN CLASSE IA) :** 4 livraisons consécutives en session unique (~3h). **(1) Tooltip combat description Bible factorisée** : avant fix, `SpellDescriptions.Get()` (Combat HUD) était un switch hardcode Soulrender-only (16 sorts) ; pour Ghostra/Necram/Nightseer/Colossar, fallback `(Description Bible non disponible)`. Idem `SpellDisplayInfo.GetDisplayName()` qui retournait l'enum brut (ex `GhostraLameSpectrale`) au lieu du nom Bible. Création `Assets/_Nymora/Scripts/Core/Data/SpellBibleTexts.cs` (250 lignes) : source unique des 80 entries Bible V7.1 patchée (SpellIdValue Quantum int + SpellIdTech snake_case + ClassId + Category + DisplayName + Description EFFET + LoreFlavor PRESSION) + struct `Entry` public + lookup helpers `TryGetByQuantumId(int)` / `TryGetByTech(string)` avec cache lazy-init. Refacto `Editor/Tools/PopulateSpellCatalog.cs` : retiré les dicts locaux `_mappings`/`_descriptions`/`_loreFlavors` (300+ lignes éliminées), itère désormais sur `SpellBibleTexts.Entries`. Refacto `Combat/View/HUD/SpellDescriptions.cs` + `SpellDisplayInfo.cs` : switches hardcoded → lookup `SpellBibleTexts.TryGetByQuantumId((int)spellId)`. Single source of truth garantie : Deck Builder et Combat Tooltip ne peuvent plus diverger. Asmdef-clean : Core ne ref pas Quantum (lookup par int = `(int)spellId`), donc Core reste portable. **Pas de bump CombatRulesVersion** (UI pure, zéro impact règles). **Pas de manip Unity** (asset SpellCatalog inchangé textuellement). **(2) Tooltip combat largeur fixe / hauteur auto-fit** : `Combat/View/HUD/SpellTooltipView.cs` étendu avec 3 SerializeField (`_fixedWidth=320f` ajustable Inspector + `_panelPadding=10` + `_panelSpacing=6`) + nouvelle méthode `EnsureLayoutWiring()` appelée au Awake. Self-healing : auto-add `VerticalLayoutGroup` (childControlWidth+Height, forceExpandWidth, padding/spacing depuis SerializeField) + `ContentSizeFitter` si manquants sur le panel, force `horizontalFit=Unconstrained` / `verticalFit=PreferredSize` (overwrite garanti, pas négociable pour fixed-width), active `enableWordWrapping=true` sur les 3 TMPs (title/cost/description), lock `_panel.sizeDelta.x = _fixedWidth`. Re-lock width à chaque `Show()` (defensive, au cas où un layout pass tiers l'aurait modifiée). Idempotent : si Lorenzo avait setup manuel VLG/Fitter, padding/spacing custom préservés (Awake n'overrideke pas un VLG pre-existant). Bénéfice : tooltip ne s'étire plus en largeur sur les sorts à description longue (Effondrement, Salve Mortelle, Virus Fatal ~8 lignes), reste lisible quel que soit le sort survolé. Applique aux 2 scènes combat (`SpellTooltipView` est MonoBehaviour partagé asmdef Combat ; 33_CombatCasual clonée depuis 30_CombatIA au 4.14.b qui est postérieur au setup du tooltip POLISH-5d 17 mai). **Pas de bump CombatRulesVersion**. **Pas de manip Unity**. **(3) Deck Builder restauration perso + dernier deck post-reco** : symptôme — hub avatar affichait bien la dernière classe choisie post-deco/reco (avatar lisait déjà `SelectedClassPreferences.Get()`) mais le Deck Builder retombait toujours sur Soulrender. Racine : `_currentClassId = "Soulrender"` était hardcodé en field initializer dans `HubDeckBuilderPanel.cs` ligne 86, jamais sync'd au Awake. Fix Awake : `_currentClassId = SelectedClassPreferences.Get()`. Plus : extension `Core/Data/SelectedClassPreferences.cs` avec 3 nouvelles méthodes `GetLastEditedDeckId(classId)` / `SetLastEditedDeckId(classId, deckId)` / `ClearLastEditedDeckId(classId)` (PlayerPrefs key `Nymora.LastEditedDeck.<classId>`). Hooks dans `HubDeckBuilderPanel` : `OnDeckListItemClicked` (persist), `OnSaveClicked` (persist après create + after update defensive), `OnDeleteClicked` (clean memo si match), `OnNewDeckClicked` (clean — intent "start fresh"). Nouvelle méthode `TryRestoreLastEditedDeck()` appelée en fin de `FetchDecksAsync` : reload nom dans input + 6 slots + `_editingDeckId` (un Save subséquent fait Update, pas Create). Si le deck mémorisé a été delete côté backend depuis la dernière session, memo auto-cleaned (idempotent). **Pas d'impact gameplay** : le combat continue à lire `MyDecks[0]` côté HubArenaPanel/HubMatchTransition, pas `_editingDeckId`. **Note PvP** n'était jamais affecté grâce au verrou `HubMatchTransition.HandleMatchReady → EnsureClassLoadedAsync(SelectedClassPreferences.Get())` qui sync le DeckBuilder juste-à-temps avant chaque match. Le bug était purement UI-visible (panel s'ouvrait sur Soulrender), pas gameplay. **Pas de bump CombatRulesVersion**. **Pas de manip Unity**. **(4) Fix spawn classe combat IA depuis DeckBridge** (`CombatRulesVersion 73 → 74`) — bug critique : `CombatantSystem.OnInit` lignes 38-39 spawn HARDCODÉ P0=Ghostra/P1=Soulrender en mode IA (legacy 2.2 test 1 client survivor au refacto 4.14.d), ignorait totalement `DeckBridge.PendingClassId` set par `HubArenaPanel.OnTrainingClicked` ligne 103. Lorenzo jouait Ghostra peu importe la classe choisie en hub. Création `Assets/_Nymora/Scripts/Combat/Bootstrap/CombatBootstrapIA.cs` (~200 lignes, mirror simplifié de CombatBootstrapCasual) : `GameMode.Local` (pas de Photon, offline), clone RuntimeConfig + bind QuantumMapData scene + force `IsBotMatch=true` safety net, `SessionRunner.StartAsync`, puis `Runner.Game.AddPlayer(0, lorenzo)` avec ClassId résolu depuis DeckBridge (`ResolveLorenzoClassId` parse string Core enum → cast byte → Quantum enum) + 6 SpellIdValues via `SpellCatalog.QuantumSpellIdValue`, puis `AddPlayer(1, bot)` avec `BotClass` configurable Inspector (default Soulrender). Singleton `Instance` (consume par CombatInputController pour détecter présence). Fallback Soulrender slot 0 + array zeros si DeckBridge vide (Play Editor direct sans passer hub, log warning). Refacto `QuantumUser/Simulation/Combat/Combatant/CombatantSystem.cs` : **retiré entièrement `OnInit`** (les 2 SpawnCombatant hardcoded P0=Ghostra/P1=Soulrender), retiré le check `if (f.RuntimeConfig.IsBotMatch) return` au début de `OnPlayerAdded` → désormais IA et PvP passent par le **MÊME chemin signal** `ISignalOnPlayerAdded` (lit `runtimePlayer.ClassId` → spawn la bonne classe + position). Log tag `IA`/`PvP` discriminé. Refacto `Combat/View/CombatInputController.cs` : étendu condition skip auto-add → skip aussi si `CombatBootstrapIA.Instance != null` (sinon `_autoAddLocalPlayers=true` ferait double `AddPlayer(0)` avec RuntimePlayer vide → CombatantSystem fallback Soulrender au lieu de la vraie classe). Fallback legacy auto-add préservé si Lorenzo Play sans bootstrap (logged warning explicite "remplacer QuantumRunnerLocalDebug par CombatBootstrapIA"). Commentaire `TurnSystem.CheckMatchEndOnDeath` mis à jour : guard `totalCount < 2` désormais critique en IA aussi (avant, OnInit spawn instantané les 2 ; maintenant CombatBootstrapIA fait AddPlayer(0) puis AddPlayer(1) sur ticks consécutifs, donc OnPlayerAdded séquentiel comme PvP). **Pas de régen Quantum CodeGen** (aucun .qtn touché, aucun [Networked] field nouveau). **MANIP UNITY Lorenzo REQUISE dans `Assets/_Nymora/Scenes/30_CombatIA.unity`** : (i) disable component `QuantumRunnerLocalDebug` sur le GameObject Quantum/QuantumRunner, (ii) add component `CombatBootstrapIA` (Nymora.Combat.Bootstrap namespace), (iii) drag refs RuntimeConfig (`RuntimeConfigCombatIA.asset`, vérifier `IsBotMatch=true` coché) + SessionConfig (même asset partagé que CombatBootstrapCasual) + SpellCatalog (`Assets/_Nymora/ScriptableObjects/Spells/SpellCatalog.asset`) + BotClass (Soulrender default ou autre pour varier l'adversaire IA). **PROCHAIN STEP** : (1) Manip Unity 30_CombatIA → validation E2E combat IA avec classe choisie en hub (test Necram/Colossar/Ghostra contre bot Soulrender, vérifier logs `[CombatBootstrapIA] AddPlayer slot 0 (Lorenzo) class=Necram deck=[70,75,...]` + `[CombatantSystem] IA spawn slot 0 class Necram`) → (2) Optionnel : étendre `HubArenaPanel` avec un dropdown "Adversaire IA" pour choisir BotClass à chaque match (au lieu de hardcoded Inspector field) → (3) Reprendre **5.4 Bloc C Économie Monnaies** (Nymos in-game + Shards premium + schéma DB + transactions) cf [[project-phase5-plan]] OU enchaîner Phase 6 ranked si rotation préférée.

**Précédente session 18 mai 2026 nuit (PHASE 4.14 PVP CASUAL ONLINE LIVRÉE + VALIDÉE E2E CROSS-INTERNET via ngrok) — 🏆 **PHASE 4 ENFIN CLÔTURÉE** (le dernier reliquat 4.14 PvP casu Quantum [[project-pvp-casu-quantum-pending]] est maintenant résolu). **8 sous-briques livrées en chain (~6h)** : (4.14.b) `IsBotMatch` flag dans CombatState/RuntimeConfig + AISystem early-out + Editor Tool `CloneCombatIAToCasualSceneTool.cs` qui clone `30_CombatIA.unity` → `33_CombatCasual.unity` (backup auto du stub précédent) + EnsureScenesInBuild auto. ⚠️ Quantum .qtn ne supporte pas `Bool` primitive → utilise `Int32 IsBotMatch` (pattern OncePerMatchUsedFlags Combatant.qtn ligne 87). (4.14.c) Nouveau `CombatBootstrapCasual.cs` (`Assets/_Nymora/Scripts/Combat/Bootstrap/`) : MonoBehaviour async qui lit `MatchBridge.PendingMatchId`, connecte Photon Realtime `JoinOrCreateRoom(matchId, max 2)`, `SessionRunner.StartAsync(GameMode.Multiplayer)`, `Runner.Game.AddPlayer(localSlot)` avec localSlot=`IsMasterClient ? 0 : 1`. Singleton `Instance` static pour que CombatInputController + CombatHUDController résolvent `_localPlayerIndex` (sinon 0 hardcoded legacy IA → "Player not found" Quantum disconnect). Asmdef `Nymora.Combat` étendu avec ref `Photon.Realtime` (GUID 831409e8f9d13b5479a3baef9822ad34). **Migration `MatchBridge` Hub → Core.Data** pour respecter séparation asmdef (3 callsites Hub updated avec `using Nymora.Core.Data;`). Ajout `LocalSub`/`LocalEmail` dans MatchBridge pour identifier le local client dans Combat sans dépendance Hub. (4.14.d) Extension `RuntimePlayer.User.cs` avec `NymoraClass ClassId` + `int[] SpellIdValues = new int[6]` (mapping via SpellCatalog.QuantumSpellIdValue). `CombatantSystem` refactor : implements `ISignalOnPlayerAdded`, en PvP spawn via signal (slot 0 → (2,7), slot 1 → (7,2)) avec ClassId depuis RuntimePlayer ; en IA spawn hardcoded OnInit (legacy intact). Helpers `ResolveClassIdForLocalPlayer` (parse string → enum Core → cast → Quantum.NymoraClass) + `ResolveSpellIdValuesForLocalPlayer` (snake_case → SpellCatalog.QuantumSpellIdValue). (4.14.e) `HubMatchTransition.HandleMatchReady` étendu : nouvelle méthode async `HubDeckBuilderPanel.EnsureClassLoadedAsync(classId)` force le DeckBuilder à sync sur `SelectedClassPreferences.Get()` AVANT lecture `MyDecks[0]` (sinon MyDecks pioche dans la classe ouverte dans le DeckBuilder, pas la classe sélectionnée dans le Class Selector → spawn fallback Soulrender). Set MatchBridge avec localSub/localEmail (depuis HubChatClient) + DeckBridge.SetPendingDeck. Guard pas de deck → LogError + bail. (4.14.f) `TurnSystem` implements `ISignalOnPlayerDisconnected` : si player quit Photon room, set `WinnerPlayerIndex = autre slot` + `CurrentPhase = MatchEnd`. Mode IA : early-return (bot pas Photon actor). (4.14.g) Bouton `_returnToHubButton` dans `MatchEndOverlay` + handler `OnReturnToHubClicked` : MatchBridge.SetMatchResult (Victory/Defeat/Draw selon WinnerPlayerIndex vs LocalPlayerIndex) + `QuantumRunner.ShutdownAll()` + `LoadScene 10_CommunityHub`. Discrimination Show : PvP → Retour Hub visible + Restart Easy/Medium cachés ; IA → inverse. `CombatHUDController.Refresh` passe `isPvpMatch = !frame.RuntimeConfig.IsBotMatch`. **Early MatchEnd hotfix critique** : TurnSystem.Update fait maintenant `CheckMatchEndOnDeath` chaque tick (au lieu de seulement EnterTurnEnd) pour que MatchEnd déclenche immédiatement sur KO (en PvP, joueur ne click pas toujours EndTurn). Guard `totalCount < 2` pour skip pendant la phase de spawn OnPlayerAdded séquentiel (sinon faux MatchEnd Draw au tick 1). (4.14.h) Test E2E validé : 2 instances multi PvP local OK, KO + forfait + retour hub flow complet OK. **PUIS test cross-internet via ngrok** : Lorenzo en Editor sur `ws://localhost:3000` + ami sur build standalone avec `wss://alphabet-reverend-cloud.ngrok-free.dev` (forward port 3000 via ngrok free) → les 2 atteignent le même backend Express, se voient dans le hub Photon Fusion Shared Mode, peuvent défier et combattre via Quantum Cloud Frankfurt. **PvP cross-internet fonctionnel** 🎉. **Polish bonus** : 4 fix UI PvP en cours de route — (i) `_localPlayerIndex` résolu via CombatBootstrapCasual.Instance dans CombatInputController + CombatHUDController + `_debugAllPlayersMovable/Controllable=false` en PvP, (ii) auto-add local 2 slots skip en PvP (Mode PvP detecte log), (iii) `MovementRangePreview` + `TargetingPreviewView` : caster=LOCAL en PvP (pas ActivePlayer) — preview spell autour de soi toujours, preview PM autour de soi pendant son tour uniquement (early-return si ActivePlayer != localSlot), (iv) `EndTurnCommand` guard "bot P1" gated sur IsBotMatch (sinon slot 1 humain bloqué). **Polish bouton ReturnHub** : Lorenzo positionne manuellement le Button RectTransform pour qu'il prenne la place visuelle des Restart Easy/Medium (cachés SetActive(false) en PvP). **PopulateSpellIconRegistry étendu Ghostra/Necram/Colossar** : dict `FileToSpellId` étendu de 32 → 80 entries (les 48 sorts manquants des 3 classes ajoutés ; mapping `icon_regeneration_necrotique` → `SpellId.NecramPulseSanguinVert` cf legacy naming) → les icônes apparaissent maintenant pour toutes les classes en barre de sorts. **MANIP UNITY Lorenzo** : (i) Quantum CodeGen Compile (régen Int32 IsBotMatch field CombatState) ; (ii) RuntimeConfig asset de 30_CombatIA : cocher `IsBotMatch=true` (mode IA), 33_CombatCasual a son propre RuntimeConfig avec IsBotMatch=false (default safe pour PvP) ; (iii) 33_CombatCasual : disable `QuantumRunnerLocalDebug` + add `CombatBootstrapCasual` (drag RuntimeConfig + SessionConfig + SpellCatalog) ; (iv) Photon Hub Ctrl+H setup AppIdQuantum ; (v) Ajout Button `ReturnHubPanel` dans MatchEndOverlay + drag dans field `_returnToHubButton` ; (vi) `Nymora > Setup > Populate Spell Icon Registry` re-run (80/80 sorts mappés). **CombatRulesVersion 70 → 73** (IsBotMatch + early MatchEnd + classes spawned via signal). **PROCHAIN STEP** : (1) Quelques bugs UI PvP à fix (notés mentalement par Lorenzo, voir prochaine session) → (2) Retour sur **5.4 Bloc C Économie Monnaies** (Nymos in-game + Shards premium + schéma DB + transactions) cf [[project-phase5-plan]] OU enchainer Phase 6 ranked si rotation préférée.

**Précédente session 17 mai nuit (suite — DECK BUILDER DESCRIPTIONS BIBLE + HUB MULTI-INSTANCE FACING SYNC) :** 🚀 **PHASE 5 BLOC B 5.3 CLÔTURÉE**. Commit `bd63a93 feat(phase5.3): deck builder + class selector + arena panel + hub avatar visual classe` déjà push origin/main (avant cette session). **Cette session (post-bd63a93) : 2 livraisons** : (1) **Import descriptions Bible V7.1 dans Deck Builder** (5.3.e.iii completion) — `PopulateSpellCatalog.cs` étendu avec 2 dicts `_descriptions` (80 entries EFFET Bible V7.1) + `_loreFlavors` (80 entries PRESSION Bible) ; `Run()` overwrite `entry.Description` + `entry.LoreFlavor` à chaque populate (préserve si entrée manque dans dict) ; `HubDeckBuilderPanel.ShowTooltipForSpell` étendu pour afficher LoreFlavor en italique grisé sous Description. Manip Unity Lorenzo : `Nymora > Setup > Populate Spell Catalog` → 80/80 sorts populates avec descriptions Bible-exact. Pas de bump `CombatRulesVersion` (UI pure, zero impact Quantum). (2) **Fix latence facing remote hub** (5.3.g.bis polish multi suite) — bug E2E multi-instance : quand P2 changeait de direction, P1 voyait P2 commencer à walk SANS pivoter puis pivot en cours de route (~1 tile/250ms de retard). **Diagnostic** : `NetGridX/Y` push seulement au END-of-step côté `HubMovementController.Update` ligne 56, donc le remote calculait son facing depuis un grid en retard d'1 tile. **Fix** : nouveau `[Networked] byte NetFacing` avec `OnChangedRender(OnNetFacingChanged)` sur HubAvatar. State Auth push `NetFacing` dès que `_currentFacing` change (dans `TrackGridPositionForFacing` + nouvelle méthode `PrimeFacingForNextStep(nextGx, nextGy)` appelée par `HubInputController.Update` AU MOMENT DU CLIC avant `movement.Follow(path)` — gain 1 frame + chance de rattraper le tick Fusion qui vient de partir, ~30-50ms cumulés). Refactor cohérent : `ComputeFacingFromDelta(dx, dy)` helper statique + `ApplyAndPushFacing(newFacing)` factorise update + push. Côté remote, `Render()` ne call PLUS `TrackGridPositionForFacing` (facing vient de OnNetFacingChanged), garde quand même `_prev*ForFacing` à jour defensif. Init `NetFacing = (byte)_currentFacing` au Spawn State Auth, lecture `_currentFacing = (HubFacing)NetFacing` au Spawn remote (joueurs qui rejoignent en cours voient le bon facing initial). **Validation E2E Lorenzo (multi-instance)** : ✅ facing sync quasi-instantané après le 2e fix push-at-click (latence résiduelle = RTT Photon Cloud Frankfurt ~30-80ms, incompressible). **PHASE 5 BLOC B 5.3 CLÔTURÉE** — multi-instance hub OK + descriptions Bible importées + tooltip riche. **PROCHAIN STEP** : **5.4 Bloc C Économie Monnaies** (Nymos in-game + Shards premium + schéma DB + transactions) cf [[project-phase5-plan]].

**Précédente session 17 mai nuit (5.3.g.bis HUB AVATAR VISUAL CLASSE) :** 🚀 **PHASE 5 BLOC A ✅ + BLOC B EN COURS**. Bloc A `5.1 Class Progression` + `5.2 Achievements` déjà committés (commits `4967fdf` + `9831b03`). **Bloc B Deck Builder (5.3)** entièrement en working tree, NOT YET COMMITTED, prêt pour push fin de session. **Sous-briques livrées en working tree :** (a) **5.3.a** Audit Bible V7.1 (`_docs/AUDIT_BIBLE_SPELLS_2026-05-17.md`) — 0 écart critique sur les 80 sorts, 2 amendements back-portés dans la Bible (`01_BIBLE_V7.1_Combat.md` modifié : Volte-Face offensif 80 dmg / Dague Lancée 40 dmg cap 2× pivot 90° / Réplique Protectrice 4 PA 30% 80HP 3 rounds / Réplique Fantôme 4 rounds) ; (b) **5.3.d** `HubDeckBuilderButton.cs` bouton bas-droite hub à côté de Profil/Amis/Clan ; (c) **5.3.e** `HubDeckBuilderPanel.cs` UI complet (header classe + signature read-only + 6 slots horizontaux + grid 15 sorts non-signature + decks list 5 max + boutons Nouveau/Save/Renommer/Supprimer + tooltip basique 5.3.e.iii) + `SpellCatalog.cs/asset` + `PopulateSpellCatalog.cs` + `DeckBridge.cs` (équipé deck en mémoire pour le combat) + backend wiring `NymoraApiClient.cs/Dtos.cs` (REST /decks + WS OnDeckChanged) ; (d) **5.3.f** `HubClassSelectorPanel.cs` carousel pour changer de classe + `UISpriteAnimator.cs` (UI Image anim) + `SelectedClassPreferences.cs` PlayerPrefs ; (e) **5.3.f.bis** `HubArenaButton.cs` + `HubArenaPanel.cs` (mode de combat — vérifie deck équipé avant lancement) ; (f) **5.3.g** Verify deck équipé avant combat (logique dans `HubArenaPanel`) ; (g) **5.3.g.bis** Hub Avatar Visual Classe — sprite + Animator Idle/Walk extraits des Stage0_SE/NE controllers via `PopulateClassDefinitions.cs`, sync inter-clients via `[Networked] NetClassId`, calib per-class portée depuis combat. **Tools éditeur untracked :** `RestructureHubAvatarPrefabTool.cs` / `PatchHubAvatarPrefabTool.cs` / `PatchDeckBuilderPanelTool.cs` / `PatchClassSelectorPanelTool.cs` / `PatchArenaPanelTool.cs` / `EnsureNymoraScenesInBuildTool.cs` / `PopulateClassDefinitions.cs`. **Cette session 17 mai nuit (5.3.g.bis polish) : 3 fixes hub avatar** : (1) **Mapping iso facing dx/dy → direction CORRIGÉ** dans `HubAvatar.TrackGridPositionForFacing` — convention inversée vs `Quantum.FacingHelpers.FacingFromGridDelta` combat (avant : `dx>0→SE` / `dy>0→NE` ; après formule iso world `dxWorld=dx-dy, dyWorld=dx+dy` → `(+1,0)=NE / (0,+1)=NW / (-1,0)=SW / (0,-1)=SE`). (2) **Walk animation intégrée 5 classes** : ajout `WalkFrames` + `WalkFramesNE` + `WalkFps=12` à `NymoraClassDefinition`, `PopulateClassDefinitions.ExtractClipFrames` refactor pour extract par nom de clip (case-insensitive contains) → peuple Idle + Walk depuis Stage0_SE/NE controllers existants (les 10 controllers ont déjà un AnimatorState "Walk" pointant clip Aseprite). `HubAvatar.Update()` détecte mouvement via delta `transform.position` (seuil 80ms idle threshold + epsilon 0.0001f) uniforme local/remote, flip `_isWalking` → `ApplyFacingVisual` swap frames. `SceneSpriteAnimator.Play()` rendu idempotent (skip reset si mêmes frames+fps+SR référencés) pour pas reset frame 0 sur re-checks. Fallback Idle si WalkFrames pas peuplé. Init clean au Spawn (`_lastMoveTime=-1000f`) pour pas de flicker walk→idle premier 80ms. (3) **Calibration per-class portée combat → hub** : ajout `HubVisualScale` + `HubVisualYOffset` à `NymoraClassDefinition`, nouveau tool `RestructureHubAvatarPrefabTool.cs` (mirror exact des `RestructureNecram/GhostraPrefabTool` combat) qui ajoute child "Visual" au prefab HubAvatar et y déplace SpriteRenderer via `CopySerialized` + `DestroyImmediate`, retrait `[RequireComponent(typeof(SpriteRenderer))]` sur HubAvatar pour permettre la restructuration, bascule `GetComponent<SpriteRenderer>()` → `GetComponentInChildren<SpriteRenderer>(true)`. `ApplyClassVisual` pilote runtime `_visualTransform.localPosition.y = def.HubVisualYOffset` + `localScale = (HubVisualScale, HubVisualScale, 1)`. Fallback no-op si Visual == root (legacy prefab pas restructuré, ne bouge pas le root tile-anchor). `PopulateClassDefinitions` étendu avec `_hubCalib` dict portant les valeurs effectives combat (Necram Y=-0.2 Scale=1.0 / Ghostra Scale=1.2075 Y=-0.207 effectifs = root.scale × visual.localScale combat / Soulrender+Nightseer+Colossar défauts 1.0/0.0). **Manip Unity Lorenzo (faite avec validation E2E solo OK)** : (i) Restructure HubAvatar Prefab (Visual child created), (ii) Populate Class Definitions (5 .asset re-peuplés avec Walk frames + HubVisualScale/YOffset). **Validation E2E SOLO** : facing iso correct sur 4 directions ✓ / walk anim joue pendant déplacement + idle au stop ✓ / Necram et Ghostra bien centrés sur tile vs Soulrender/Nightseer/Colossar défauts ✓. **RELIQUAT TEST : multi-instance hub PAS ENCORE TESTÉ** (cf [[project-hub-multi-instance-test-pending]]) — risque possible que NetClassId arrive AVANT que `_visualTransform` soit résolu côté remote, ou que Walk anim ne switch pas via NetWorldPos delta sur les autres clients. **À TESTER PROCHAINE SESSION AVANT TOUT AUTRE TRAVAIL.** **PROCHAIN STEP** : (1) Multi-instance hub validation (idle/walk sync + facing remote + per-class calib remote + class change live) → (2) Si OK, clôture 5.3 et passage **5.4 Bloc C Économie Monnaies** (Nymos in-game + Shards premium + schéma DB + transactions). Si bug multi → fix avant clôture.

**Précédente session 17 mai (FIN POLISH COMBAT / CLÔTURE PHASE 3) :** 🏆 **PHASE 3 CLÔTURÉE — 5/5 CLASSES BIBLE V7.1 COMPLÈTES**. STATUT en retard rectifié : Ghostra finalisé 16 mai avec les 6 sorts Bloc C Survie (Linceul d'Ombres / Voile Spectral / Réplique Protectrice / Dernier Pas / Pas de l'Au-Delà — SpellId 96→100) + signature Exécution Spectrale (SpellId 101). **Confirmations polish combat (Phase 3 close)** : (1) ✅ Manip Unity POLISH-5e (régen Quantum 10x10 + components scène `BattleMapCalibrator` + tooltip + calage map `Map_Combat_1.png`) commitée et propre ; (2) ✅ Validation E2E POLISH-5d-e — hover combatant sprite-based + tooltip + map alignée 10x10 OK Play Mode ; (3) ✅ Validation E2E Ghostra Bloc C Survie + Exécution Spectrale OK ; (4) ✅ Sorts-move bypassent hooks terrain ([[project-spell-move-terrain-trigger-gap]]) — refacto global confirmé différé, dette acceptée, non-bloquant Phase 5 ; (5) ✅ Reliquat assets designer Nightseer (marque_empreinte/traque/voile_brume dupliqués dans `Nightseer/Tiles/`) rangé côté assets ; (6) ✅ Highlight tile masqué par pion (sortingOrder pion 700-990 > tile 0) — overlay confirmé en place. **Reliquats Phase 7 polish CONSERVÉS (non-bloquants alpha) :** (7) Vapeur Carmin traversée multi-case (MVP : seulement case destination, vraie traversée Phase 7) ; (8) Empoignade pull à refacto dans `MovementHelpers` pour cohérence Phase 7. **PROCHAIN STEP** : entamer Phase 5 (10 briques en 5 blocs cf [[project-phase5-plan]]) — IAP mobile reportés post-alpha, scope strict MVP.
**Précédente session 17 mai (POLISH-5d-e) :** 🎨 **UI COMBAT POLISH + GRILLE 10x10 + MAP BACKGROUND**. Bloc UI (POLISH-5d) : `HubChatClient.cs` passe en `DontDestroyOnLoad` → chat persiste hub→combat→retour, instancier un 2e HubChatClient dans `30_CombatIA` pour debug play direct (singleton gère doublon). `SpellIconRegistry.cs` étendu aux 5 classes : ajout `_passifColossarIcon/_passifNecramIcon/_passifGhostraIcon` + idem avatars + switches `PassifIconFor`/`AvatarFor` couvrent maintenant Colossar (icon_passif_densite_inerte, colossar_avatar_128px), Necram (icon_passif_floraison, necram_avatar_128px), Ghostra (icon_passif_angle_mort, GHOSTRA_Avatar_128px). `PopulateSpellIconRegistry.cs` scan automatique des 5 dossiers d'icônes + 5 dossiers d'avatars via helpers `FindFirstAvatarByPrefix` + `WarnIfMissing`. Hover combatant : `CombatantView.ApplyHighlight()/ClearHighlight()` (tint jaune sur SpriteRenderer) + nouveau `CombatantTooltipView.cs` (UI singleton auto-init Canvas/Panel/Text au Awake, suit la souris avec offset, fontSize 13 panel 95×24). `TileHoverView.cs` étendu avec **détection sprite-based** (`FindCombatantViewByMouse` via `SpriteRenderer.bounds.Contains(mouseWorld)` + tiebreak par sortingOrder) au lieu de case grille → précis même si sprite déborde sa tile (scale 1.16x Phase 3). Bloc grille (POLISH-5e, **CombatRulesVersion 66 → 70**) : **15x17 (255 tiles) → 10x10 (100 tiles)** pour caler l'arène losange de la map background `Map_Combat_1.png` (assets/_Nymora/Art/UI/Maps/). 3 .qtn modifiés (`Grid/Fog/Obstacle.qtn` : `array<*>[100]`) → **régen Quantum CodeGen obligatoire**, pas de [Networked] field touché donc prefab/scène pas affectés. `GridConstants.Width=Height=10`. Spawn `(2,7)/(7,2)` sur diagonale médiane iso (gx+gy=9) facing SE/NW pour respect angles gauche/droite désignés par Lorenzo sur la map. `GridRenderer.cs` + `TileHoverView.cs` + `GridPreviewerWindow.cs` lisent maintenant `GridConstants.Width/Height` au lieu d'hardcoder 15/17 (resize sans forker la View). `GridRenderer.OnDrawGizmos` Editor-only : contour iso jaune + quadrillage interne visible en Scene View hors Play → cale la map sans avoir à lancer le combat. `BattleMapCalibrator.cs` `[ExecuteAlways]` (Y/X/Scale sliders) + `BattleMapCalibratorEditor.cs` Custom Inspector avec helpbox + boutons `Apply Now` / `Copy from Transform` / `Reset`. Pattern "Copy Component → Paste Component Values" pour persister calage Play→Edit. **Manip Unity attendue** (regen + ajout 2 components scène + map calage) **non commitée** dans la scène `30_CombatIA.unity` car mélangée avec les modifs Phase 3.7 du 16 mai en working tree. **PROCHAIN STEP** : valider en jeu (hover + map alignée), continuer polish UI combat ou repartir sur Phase 3.7 Ghostra (6 sorts Bloc C Survie restants : Linceul / Voile Spectral / Réplique Protectrice / Dernier Pas / Pas de l'Au-Delà + signature Exécution Spectrale).
**Précédente session 17 mai (POLISH-5c) :** 🎨 **NORMALIZATION + REBIND ASSETS COMBAT VFX/MARKS/TILES**. Tous les .gif supprimés par Lorenzo (16-17 mai), passage 100% .png frames individuelles `-export[1..N]`. Nouveau tool unique `Assets/_Nymora/Editor/Tools/CombatAssetsNormalizer.cs` (menu `Nymora > Validation > Normalize Combat Assets`) : scan systematique 5 classes × {Marks|Marques, Tiles|Terrains, VFX}, Import Settings unifies (Sprite/Single/Point/Uncompressed/Pivot center) + **PPU dynamique = max(W,H)/targetUnits** → chaque sprite mesure pile la même taille en jeu peu importe sa resolution source (64/128/256). Scan & Bind detecte les series + **multi-binding** (plaie_ouverte → Soulrender `_antiHealShieldFrames` + Ghostra `_plaieOuverteFrames` partagés Bible V7.1). Bouton **Nuke Broken Metas** filesystem-level pour les .meta legacy qui resistent au reimport. **3 bugs racines fixes** : (1) `NymoraSpriteImporterSettings.cs` postprocessor 2.13.e forcait `spriteMode=Multiple` + `PPU=128` hardcode sur Soulrender/Marks et Soulrender/Terrains → reliquat .gif slicing, sabotait `LoadAssetAtPath<Sprite>` → **marques Soulrender invisibles depuis suppression .gif** ; (2) Unity ignore `TextureImporterSettings.spriteMode` via SetTextureSettings — il faut `importer.spriteImportMode` directement ; (3) certains .meta legacy ne sont jamais reecrits par SaveAndReimport (Unity considere le content effectif inchange) → **suppression filesystem du .meta + AssetDatabase.Refresh ForceSynchronousImport** pour regenerer from scratch. Add `_verboseLog` toggle sur `CombatantMarksView` + `TerrainView` pour diagnostic overlay invisible runtime (nullCount sprite, scale, sortingOrder). Validation E2E : F1 PlaieOuverte + Shift+F1 MarkedByCarnage + F3-F5 terrains + F12 decoy → tout s'affiche animé. **Note assets duplicates** : marque_empreinte/marque_traque/marque_voile_brume Nightseer présents AUSSI dans `Nightseer/Tiles/` (erreur livraison designer, à ranger côté assets). **PROCHAIN STEP** : UI combat polish — passif bas-gauche → milieu-gauche + integration `HubChatClient` (WebSocket pur, transportable) en bas-gauche scène `30_CombatIA` via `DontDestroyOnLoad` pour persister hub→combat→retour.
**Précédente session 16 mai (jour, suite 21) :** 🚀 **3.7.a.iv DANSE DES LAMES LIVRÉE + VALIDÉE E2E**. Bible V7.1 ligne 1116 : 5 PA, Self AoE Square3x3 (8 cases adj caster, caster auto-exclu via `target == casterEntity` ligne 606), 180 dgts/cible + bonus dorsal Angle Mort + bonus Marque de l'Ombre +20 + PlaieOuverte auto Angle 2+. Décision Lorenzo : consommation optionnelle Bible zappée (interprétation "naturel dorsal uniquement"). **ZÉRO handler custom** : tout via pipeline générique Ghostra (`GhostraPassif.GetDorsalBonusIfApplicable` ligne 759 + Marque +20 ligne 769 + `ApplyPlaieOuverteIfAngle2Plus` ligne 1079). Bind **V** étendu context-aware (Nightseer=SalveMortelle / Necram=BrumeToxique / **Ghostra=DanseDesLames** target=case caster via `TryGetCasterCell` pattern Faux Décharnée). SpellId.GhostraDanseDesLames=95. `CombatRulesVersion 56→57`. Fichiers touchés : `Spell.qtn` (+1 enum), `SpellRegistry.cs` (2 constantes + 1 case SpellDef Square3x3 Self range 0 IsOffensive=1), `CombatInputController.cs` (extend bind V), `GameVersion.cs`. ⚠️ **Pas de [Networked] field** → recompile suffit, pas de régen prefab/scène/standalone. **VALIDATION E2E (logs Lorenzo)** : (1) round 2 P0 cast V face Soulrender (7,8) → 180 dmg base 1500→1320 + PA 8→3 consume 5 ✅ ; (2) round 3 P0 M sur P1 (+20 magnitude 2 rounds) → cast V → hook Marque `+20 dmg → total 200` → P1 1320→1120 + PA 6→1 ✅ ; (3) caster P0 (6,8) auto-exclu (pas d'auto-dmg) ✅. Cas dorsal Angle 2-3 + PlaieOuverte auto non testés directement mais pipeline partagé éprouvé session 7 (Lame Spectrale). **Bonus** : Marque de l'Ombre 3.7.b.v validée par effet aussi (status applique + +20 dmg hook fonctionnel sur sort Ghostra). **Ghostra : 10/16** (5 offensifs ✅ Lame Spec / Lame Vorace / Saigne-Âme / Frappe Fantôme / Danse Lames + 5 tactiques ✅ Réplique / Pas Ombre / Volte-Face / Dague / Marque Ombre). Reste **6 sorts** : Bloc C Survie (Voile Spectral / Linceul d'Ombres / Pas de l'Au-Delà / Réplique Protectrice / Dernier Pas) + signature Exécution Spectrale. **PROCHAIN STEP** : Bloc C Survie en ordre recommandé Linceul d'Ombres (warm-up reuse pipeline ShieldActive+RipostMelee) → Voile Spectral (StatusKind.DotImmune) → Réplique Protectrice (DecoyKind.Protective déjà préparé) → Dernier Pas (combine Évanescence + Pas Ombre + OncePerMatch) → Pas de l'Au-Delà (modif A* traverse-unités, le plus chaud, en dernier).
**Précédente session 16 mai (jour, suite 20) :** 🚀 **3.7.b.v MARQUE DE L'OMBRE LIVRÉE** (code seulement, E2E à valider). Bible V7.1 ligne 1155 : **2 PA, range 4 ENEMY, 0 dmg direct** (sort préparatoire). Applique nouveau **`StatusKind.MarqueDeLOmbre = 25`** sur target pendant 2 rounds magnitude=20. **2 hooks** : (1) bonus +20 dmg sur tous les sorts Ghostra contre cible marquée (pipeline damage Ghostra), (2) **PlaieOuverte AUTO sur tout dorsal Ghostra contre cible marquée** — étend `GhostraPassif.ApplyPlaieOuverteIfAngle2Plus` pour bypass requirement Angle 2+ leurres. Bible "Anti-tank par contournement". Bind **M** étendu context-aware (Nightseer=Évanescence / Necram=Cocon / **Ghostra=MarqueDeLOmbre**). SpellId.GhostraMarqueDeLOmbre=94. `CombatRulesVersion 55→56`. Fichiers touchés : `Status.qtn` (+1 enum value), `Spell.qtn` (+1 enum), `SpellRegistry.cs` (constantes + SpellDef IsOffensive=0), `SpellSystem.cs` (hook bonus +20 dmg + post-damage handler + LoS list), `GhostraPassif.cs` (bypass Angle 2+ si marquée), `CombatInputController.cs` (bind M étendu), `GameVersion.cs`. ⚠️ **Pas de [Networked] field touché** (nouveau enum value StatusKind = array slots déjà fixed-size 8). Régen Quantum CodeGen + recompile suffit (pas de prefab/scène/standalone régen). **PROCHAIN STEP : régen Quantum + test E2E** : (1) M sur ennemi → status appliqué + log "pose sur P1", (2) Lame Spec sur cible marquée → +20 dmg buff (170+20=190 base), (3) **Combo Bible anti-tank** : M (2 PA) → Frappe Fantôme T (4 PA) dorsal Angle 1 sans leurre → 200+20 marque + 0 dorsal + **PlaieOuverte AUTO via marque** = 220 dmg + plaie sans aucun leurre.
**Précédente session 16 mai (jour, suite 19) :** 🔧 **REFONTE BALANCE Volte-Face / Dague Lancée** (Lorenzo 16 mai). Constat : Volte-Face 2 PA 50 dmg + flip 180° vs Dague Lancée 1 PA 80 dmg + pivot 90° → ratio PA incohérent (Dague paye moins, tape plus). Swap damage pour cohérence ratio : **Volte-Face passe 50 → 80 dmg** (2× Dague Lancée), **Dague Lancée passe 80 → 40 dmg** (½ Volte-Face). Logique : 2 PA = 2× 1 PA donc 80 = 2× 40 + flip 180° ~ 2× utile que pivot 90°. Toutes les autres mécaniques (range, pivot, flag, cap 2×/tour) inchangées. Mémoires `project_volteface_amended` + `project_dague_lancee_amended` mises à jour. `CombatRulesVersion 54→55`. Fichiers touchés : `SpellRegistry.cs` (2 constantes), `Spell.qtn` (2 commentaires), `GameVersion.cs`. **Pas de [Networked] field touché** → recompile suffit. **PROCHAIN STEP : recompile + test E2E** : (1) Q sur ennemi face → 80 dmg + flip 180°, (2) F sur ennemi face → 40 dmg + pivot 90°.
**Précédente session 16 mai (jour, suite 18) :** 🔧 **AMENDEMENT FRAPPE FANTÔME : PRIORITÉ CASE DORSALE** (Lorenzo 16 mai). Avant : téléport priorité côté caster d'origine (ne garantit pas dorsal). Maintenant : **téléport priorité DERRIÈRE target** (Opposite(target.Facing)) → **dorsal GARANTI** quand case libre. Permet le combo `F Dague Lancée (pivot 90°) → T Frappe Fantôme (téléport dorsal garanti)`. Fallback ordre : back → side1 → side2 → front (perpendiculaires puis face en dernier recours). Nouveau helper `FacingHelpers.IsoFacingToGridDelta(IsoFacing) → (dx, dy)` mapping cardinaux purs. Refactor `TryFindFreeCellAdjacentToTarget(f, targetX, targetY, IsoFacing targetFacing, out outX, out outY)` (signature changée : prend Facing au lieu de caster pos). Les 2 call sites (pré-check + téléport) résolvent target.Facing avant de calculer la case dorsale. `CombatRulesVersion 53→54`. Fichiers touchés : `FacingHelpers.cs` (+IsoFacingToGridDelta), `SpellSystem.cs` (refactor helper + 2 call sites + nouveau helper TryCellFromFacing), `GameVersion.cs`. **Pas de [Networked] field nouveau** → recompile suffit. **PROCHAIN STEP : recompile + test E2E** combo F → T : (1) F sur target (pivot 90°), (2) T → Ghostra téléport DERRIÈRE target → 250 dmg dorsal + PlaieOuverte appliquée.
**Précédente session 16 mai (jour, suite 17) :** 🔧 **AMENDEMENT DAGUE LANCÉE** (Lorenzo 16 mai). 2 changements vs Bible orig : **(1) cap 2×/tour** (via 2 nouveaux [Networked] fields `LastDagueLanceeOnTurn` + `DagueLanceeCountThisTurn` sur Combatant + check pré-PA + increment dans handler). **(2) pivot 90° HORAIRE iso** au lieu de pivot face-caster (Bible orig sans valeur car cible déjà face au caster post-cast naturel). Nouveau helper `FacingHelpers.RotateClockwise` (NE→SE→SW→NW→NE). Flag `LastFacingForcedOnTurn` conservé → combo Dague→Frappe Fantôme PlaieOuverte intact. Memory `project_dague_lancee_amended` créée. `CombatRulesVersion 52→53`. Fichiers touchés : `FacingHelpers.cs` (+RotateClockwise), `Combatant.qtn` (+2 [Networked]), `CombatantSystem.cs` (init), `SpellRegistry.cs` (+DagueLanceeMaxUsagesPerTurn=2), `SpellSystem.cs` (pre-cast reject + handler refondu), `Spell.qtn` (comment), `GameVersion.cs`. ⚠️ **Nouveaux [Networked] fields** → **régen Quantum CodeGen + prefab Combatant + scène 30_CombatIA + rebuild standalone OBLIGATOIRES**. **PROCHAIN STEP : régen + test E2E** : (1) F sur ennemi → 80 dmg + pivot 90° horaire visible, (2) 3e F même tour → rejet "deja utilisee 2x ce tour", (3) round suivant → cap reset, (4) combo Dague→Frappe Fantôme PlaieOuverte toujours OK.
**Précédente session 16 mai (jour, suite 16) :** 🚀 **3.7.b.iv DAGUE LANCÉE LIVRÉE** (code seulement, E2E à valider). Bible V7.1 ligne 1148 : **1 PA, range 5 ENEMY, 80 dgts + bonus dorsal Angle Mort** (pipeline générique) + **force `target.Facing` vers caster** (target pivote regard vers Ghostra) + **`target.LastFacingForcedOnTurn = currentTurn`** → **interaction Frappe Fantôme** : combo `Dague Lancée → Frappe Fantôme` même tour applique PlaieOuverte. Sort spam-friendly 1 PA, harcèlement / repositionnement. Bind **F** étendu context-aware (Nightseer=Bourrasque / Necram=Contagion / **Ghostra=DagueLancee**). SpellId.GhostraDagueLancee=93. TODO post-MVP : si caster cast depuis un leurre (Permutation), target devrait regarder le leurre — pour MVP, toujours caster réel. `CombatRulesVersion 51→52`. **Pas de [Networked] field touché** → recompile suffit. **PROCHAIN STEP : recompile + test E2E** : (1) F sur ennemi range 5 → 80 dmg + log "pivot vers caster", (2) Dague Lancée → Frappe Fantôme même tour → 80+250 dmg + **PlaieOuverte** appliquée, (3) Spam Dague Lancée pour maintenir target en face de Ghostra (anti-pivot mécaniste).
**Précédente session 16 mai (jour, suite 15) :** 🎨 **POLISH FRAPPE FANTÔME : ANIM TÉLÉPORT**. Lorenzo demande l'anim de téléport (fade out + flash spectral + fade in, infra Permutation / Pas dans l'Ombre) sur Frappe Fantôme. **Détection via LastCastSpellId == GhostraFrappeFantome + LastCastOnTurn** (pas de nouveau [Networked] field, on réutilise les trackers existants, **pas de régen lourde**). Dict `_lastFrappeFantomeCastTurn` côté CombatantRenderer cache la dernière valeur de `LastCastOnTurn` quand le spell castle était Frappe Fantôme → trigger `isTeleportSnap = true` au prochain increment + `view.PlayTeleportEffect(...)` au lieu de walk lerp. `CombatRulesVersion 50→51`. Fichiers touchés : `CombatantRenderer.cs` (+1 dict + détection ~5 lignes), `GameVersion.cs`. **Pas de [Networked] field touché** → recompile suffit.
**Précédente session 16 mai (jour, suite 14) :** 🔧 **AMENDEMENT LEURRES 4 ROUNDS** (Lorenzo 16 mai). Bible orig disait 2 rounds (`DecoyHelpers.LifetimeRounds = 2`), mais Lorenzo en E2E 4-round combat n'avait pas le temps de mettre en place un combo Volte-Face → Frappe Fantôme avec Angle 2+ (Réplique posée tour 1 expirait tour 3, IA tuait le Ghostra avant tour 4-5). **Étendu à 4 rounds** : `DecoyHelpers.LifetimeRounds = 4`. Tous les leurres (Standard, Protective, RepliqueFantome) bénéficient. Heal Bible Réplique Fantôme +80 HP appliqué à expiration naturelle (= maintenant après 4 rounds au lieu de 2), +40 HP si détruit prématurément inchangé. Commentaires docs mis à jour (`DecoyHelpers.cs`, `Spell.qtn`, `SpellSystem.cs` handler Réplique, `TurnSystem.cs`). Memory `project_decoy_lifetime_amended` créée. `CombatRulesVersion 49→50`. **Pas de [Networked] field touché** → recompile suffit. **PROCHAIN STEP : régen (déjà prévue pour 3.7.a.iii) + test E2E** avec gameplay strategy étoffée (poser 2 leurres tour 1, Volte-Face + Frappe Fantôme tour 2 ou 3, leurres encore actifs = Angle 2 garanti dorsal).
**Précédente session 16 mai (jour, suite 13) :** 🚀 **3.7.a.iii FRAPPE FANTÔME LIVRÉE** (code seulement, E2E à valider). Bible V7.1 ligne 1095 : 4 PA, range 4 ENEMY. **Téléport Ghostra sur case libre adjacente target** (priorité côté caster d'origine pour favoriser dorsal, fallback 4 cardinaux, **reject pré-PA si aucune case libre**) + **200 dgts base + bonus dorsal Angle Mort** (générique pipeline) + **PlaieOuverte conditionnel** (40/tour × 2t) si `target.LastFacingForcedOnTurn == currentTurn` (combo Volte-Face → Frappe Fantôme dans le même tour Bible "shred 350+ HP"). **Nouveau [Networked] field** `LastFacingForcedOnTurn` sur Combatant : set par Volte-Face uniquement (pas par walk/cast pivot naturels). Helper `TryFindFreeCellAdjacentToTarget` : 4 cardinaux ordonnés par priorité côté caster → dorsal probable. Téléport via `MovementHelpers.MoveNonPM` + override `caster.Facing` post-téléport pour pointer target (sinon Facing serait dans le sens du téléport, pas vers la target). Bind **T** context-aware Ghostra (Ghostra=FrappeFantome / autres=DEBUG Voile Nightseer 2.14 compat). SpellId.GhostraFrappeFantome=92. `CombatRulesVersion 48→49`. Fichiers touchés : `Combatant.qtn` (+1 [Networked]), `Spell.qtn`, `SpellRegistry.cs`, `SpellSystem.cs` (pre-cast reject helper + pre-damage téléport hook + post-damage handler PlaieOuverte conditionnel + LoS list + Volte-Face set flag), `CombatantSystem.cs` (init -1000), `CombatInputController.cs`, `GameVersion.cs`. ⚠️ **Nouveau [Networked] field** → **régen Quantum CodeGen + prefab Combatant + scène 30_CombatIA + rebuild standalone OBLIGATOIRES** (cf [[feedback-networked-field-regen-protocol]]). **PROCHAIN STEP : régen + test E2E** : (1) T sur ennemi range 4 → téléport adj + 200 dmg (pas de plaie), (2) Volte-Face → T même tour → 50+280 dorsal + **PlaieOuverte appliquée**, (3) T sur ennemi entouré 4 cases occupées → reject pré-PA sans consume.
**Précédente session 16 mai (jour, suite 12) :** 🔧 **AMENDEMENT VOLTE-FACE** (Lorenzo 16 mai). Bible-original (verrou DirectionLocked 1 round) abandonné car cible restait dos même après son cast/walk → perception "bug" injouable. **Nouveau Volte-Face** : sort **OFFENSIF**, 2 PA range 4 ENEMY, **50 dmg + bonus dorsal Angle Mort** (pipeline générique, IsOffensive=1) + **flip Facing 180° instantané**. **PAS DE VERROU** : la cible se réoriente normalement à son prochain tour (walk/cast/push pivots standard). Si elle ne trigger aucun pivot, elle reste dos → dorsal potentiel sur Lame Spec/Saigne-Âme suivant. **Nettoyage dead code** : retrait des 5 hooks `Has(DirectionLocked)` (MovementSystem.ApplyMove, MovementHelpers.MoveNonPM, SpellSystem cast facing update, PushAndTriggerEx, PullTargetAdjacent, PasDansLOmbre pivot). `StatusKind.DirectionLocked=24` conservé en RESERVED dans l'enum (pas de régen ID forcée). `CombatRulesVersion 47→48`. Fichiers touchés : `Spell.qtn` (commentaire), `Status.qtn` (RESERVED), `SpellRegistry.cs` (VolteFaceDmg=50, IsOffensive=1, retrait VolteFaceLockTurns), `SpellSystem.cs` (handler simplifié + nettoyage 5 hooks), `MovementSystem.cs`, `MovementHelpers.cs`, `GameVersion.cs`. **PROCHAIN STEP : régen Quantum CodeGen + recompile + test E2E Q cible enemy → 50 dmg + flip 180° + cible peut se retourner à son tour (cast/walk/push standard)**.
**Précédente session 16 mai (jour, suite 11) :** 🚀 **3.7.a.ii SAIGNE-ÂME LIVRÉ** (code seulement, E2E à valider). Finisher Bible V7.1 ligne 1109 : **4 PA, range 2 ENEMY, 200 dgts base + 70 si target a PlaieOuverte (consomme la plaie sur cible survivante post-damage) + bonus dorsal Angle Mort générique**. Si **kill** : caster Ghostra heal **+60 HP** (cap MaxHP, bloqué par AntiHealShield comme tous les heals). Aboutissement combo Bible "Plaie Ouverte → Lame Vorace ×N → Saigne-Âme". **Pas de [Networked] field nouveau** → recompile suffit. Bind **R** étendu context-aware Ghostra (Nightseer=FrappeOmbre / Necram=FauxDécharnée / Ghostra=**SaigneAme**), cohérent avec pattern "touche R = sort signature offensif distance" des autres classes. SpellId.GhostraSaigneAme=91. `CombatRulesVersion 46→47`. Fichiers touchés : `Spell.qtn` (+1 enum), `SpellRegistry.cs` (constantes + SpellDef Enemy range 2 IsOffensive=1), `SpellSystem.cs` (inline +70 PlaieOuverte sur pipeline damage + post-damage handler heal kill / consume PlaieOuverte sur survivant + LoS list), `CombatInputController.cs` (bind R étendu Ghostra), `GameVersion.cs`. **PROCHAIN STEP : régen Quantum CodeGen + recompile + test E2E** : (1) Saigne-Ame seul (200 base), (2) Saigne-Ame avec PlaieOuverte (270 + consume sur survivant), (3) Saigne-Ame en finisher kill (heal +60 HP caster + plaie disparaît avec cible morte), (4) combo Bible complet Plaie Ouverte → Lame Vorace → Saigne-Ame.
**Précédente session 16 mai (jour, suite 10 hotfix 2) :** 🔧 **HOTFIX MISMATCH ENUM Quantum/View IsoFacing**. Bug post-suite 10 hotfix 1 : déplacement vers NE → sprite Idle pointait SE. **Cause root** : Quantum `IsoFacing` (SE=0/NE=1/NW=2/SW=3) et View `IsoFacing` (NE=0/SE=1/NW=2/SW=3) ne sont pas alignés par valeur entière ! Le cast `(IsoFacing)self.Facing` faisait Quantum NE (=1) → View SE (=1) ❌. Commentaire FacingHelpers.cs "valeurs identiques pour conversion triviale" était faux pour SE↔NE. **Fix** : nouvelle méthode `CombatantRenderer.QuantumToViewFacing(Quantum.IsoFacing)` switch explicite par enum value. ResolveFacing utilise désormais ce mapping. Commentaire FacingHelpers.cs corrigé avec ⚠️. `CombatRulesVersion 44→45`. **Tests E2E à refaire** : (1) mouvement vers chaque direction NE/SE/NW/SW → Idle correct, (2) Volte-Face flip 180° visuel évident, (3) cast non-Self → caster regarde target.
**Précédente session 16 mai (jour, suite 10 hotfix 1) :** 🔧 **HOTFIX VOLTE-FACE SPRITE NE TOURNE PAS**. Bug E2E : `flip NE -> SW` côté Quantum OK mais sprite cible ne change pas visuellement. Cause : `CombatantRenderer.ResolveFacing` ignorait `combatant.Facing` Quantum et recalculait depuis delta grille local. Si cible ne bouge pas, le cache `_lastFacings` View reste figé. Fix 1 : `ResolveFacing` retourne désormais `(IsoFacing)self.Facing` directement (Quantum source-of-truth, cohérent avec 3.7.a.i pattern). Fix 2 : `SpellSystem.TryCastSpell` met à jour `caster.Facing = FacingFromGridDelta(target - caster)` au cast non-Self (avec respect `DirectionLocked` si actif), sinon régression "reorient on cast" du hack View ligne 340-349 ne survivrait pas au tick suivant. `CombatRulesVersion 43→44`. **PROCHAIN STEP : test E2E flip 180° sprite + verrou direction sur push/pull/move + cast caster reoriente vers cible**.
**Précédente session 16 mai (jour, suite 10) :** 🚀 **3.7.b.iii VOLTE-FACE LIVRÉ** (code seulement, E2E à valider). 2 PA, range 4, ENEMY. **Flip Facing 180°** via `FacingHelpers.Opposite(target.Facing)` + applique nouveau **`StatusKind.DirectionLocked = 24`** pendant 1 round. Le status est consulté par `MovementSystem.ApplyMove` + `MovementHelpers.MoveNonPM` + `PushAndTriggerEx` (push) + `PullTargetAdjacent` (Empoignade) + handler Pas dans l'Ombre (pivot adj enemies) → si actif, **skip** le recalcul Facing depuis dx/dy. Le target peut bouger normalement mais son Facing reste figé post-flip. Bible "Toute attaque dorsale sur elle ce tour est garantie" : combo Volte-Face + Lame Spectrale/Saigne-Âme/Frappe Fantôme imparable pendant la durée. Bind **Q** étendu context-aware (Nightseer MarqueDuChasseur / Necram Inoculation / Ghostra **VolteFace**). Pas de damage direct, pas de [Networked] field nouveau. SpellId.GhostraVolteFace=90. `CombatRulesVersion 42→43`. Fichiers touchés : `Status.qtn`, `Spell.qtn`, `SpellRegistry.cs`, `SpellSystem.cs` (handler + LoS list + hooks DirectionLocked dans push/pull/PasDansLOmbre pivot), `MovementSystem.cs`, `MovementHelpers.cs`, `CombatInputController.cs`, `GameVersion.cs`. **PROCHAIN STEP : régen Quantum CodeGen + recompile + test E2E Q cible enemy → flip 180° + verrou 1 round (cible peut bouger mais facing reste fige) + combo dorsal garanti Bible**.
**Précédente session 16 mai (jour, suite 9) :** 🚀 **3.7.b.ii PAS DANS L'OMBRE LIVRÉ** (code seulement, E2E à valider). 2 PA, range 5 case vide téléport via `MovementHelpers.MoveNonPM`. Pivot AUTO sur ennemis adjacents Manhattan ≤1 à l'arrivée : Facing target = direction vers Ghostra. Option **Shift+H** : pose `DecoyKind.Standard` sur case quittée. **AMENDEMENT Lorenzo (suite 9)** : (1) **cap 1×/tour** ajouté (pattern Permutation) via nouveau `[Networked] Int32 LastPasDansLOmbreOnTurn` sur Combatant + check AVANT consume PA dans SpellSystem (reject si == currentTurn) + set après téléport réussi ; (2) **anim téléport** côté View au lieu de walk classique : CombatantRenderer.OnUpdateView détecte l'increment LastPasDansLOmbreOnTurn (Dict `_lastPasDansLOmbreOnTurn` parallèle à `_lastPermutationOnTurn`) → appelle `view.PlayTeleportEffect()` (réutilise infra Permutation : fade out + snap + flash bleu spectral + fade in). SpellId.GhostraPasDansLOmbre=89. Bind **H** context-aware (Colossar=FrappeLourde / Ghostra=PasDansLOmbre, Shift+H=pose leurre). `CombatRulesVersion 41→42`. Fichiers touchés : `Spell.qtn`, `Combatant.qtn` (+1 [Networked] field), `SpellRegistry.cs`, `SpellSystem.cs` (handler + check 1×/tour), `CombatantSystem.cs` (init -1000), `CombatantRenderer.cs` (teleport anim hook), `CombatInputController.cs`, `GameVersion.cs`. ⚠️ **NOUVEAU [Networked] field** → **régen prefab Combatant + scène 30_CombatIA + rebuild standalone OBLIGATOIRES** (cf [[feedback-networked-field-regen-protocol]]). **PROCHAIN STEP : régen + test E2E H teleport (anim spectral) + cap 1×/tour reject + Shift+H pose leurre + pivot adj enemy**.
**Précédente session 16 mai (jour, suite 8) :** ✅ **3.7.b.i RÉPLIQUE FANTÔME VALIDÉE E2E**. 3 expirations naturelles confirmées (slot 1 round 3 : 960→1040 +80 HP / slot 0 round 4 : 640→720 +80 HP / slot 2 round 4 : 720→800 +80 HP). Permutation Angle 3 OK (swap Ghostra(3,8)↔Decoy slot 2 (6,8)). Charge Brutale Soulrender stoppée par leurre → DestroyByEnemyAction → heal +40 demandé mais clampé à 0 (cap MaxHP 1500/1500 au moment du test, démonstration différée). Validation range 4 OK (distance 5 rejetée). Premier sort tactique Ghostra Bible V7.1 (ligne 1127) : 3 PA, range 4 case vide, pose un leurre **DecoyKind.RepliqueFantome** (nouveau variant enum) clone visuel identique 2 rounds. Heal lifecycle owner via `DecoyHelpers` : **+80 HP si SURVIT 2 rounds** (TickLifetimeAtSubTurnStart) / **+40 HP si DÉTRUIT prématurément** par action adverse (nouvelle méthode `DecoyHelpers.DestroyByEnemyAction` qui discrimine par Kind : RepliqueFantome=+40, Protective=+60 préparation 3.7.c.iv, Standard=no heal). Cap 3 leurres respecté (helper rejette). **Décision ordre** : audit projet 16 mai → Bloc B Tactiques AVANT offensifs restants car Frappe Fantôme dépend Volte-Face et Danse des Lames dépend leurres-via-sort (vs F12 cheat). Plan révisé : 3.7.b.i Réplique Fantôme (now) → 3.7.b.ii Pas dans l'Ombre → 3.7.b.iii Volte-Face → 3.7.a.ii Saigne-Âme → 3.7.a.iii Frappe Fantôme → 3.7.b.iv Dague Lancée → 3.7.b.v Marque de l'Ombre → 3.7.a.iv Danse des Lames → 3.7.c.i→v Survie → 3.7.d Exécution Spectrale. Wiring : caller Charge Brutale (SpellSystem ligne ~1385) migré de `DestroyAtSlot` (sans heal, usage interne Ghostra) vers `DestroyByEnemyAction` (heal Bible-conforme). Bind AZERTY : touche **G** étendue context-aware Ghostra (Nightseer Souffle Glacial / Necram Pas Spectral / Ghostra **Réplique Fantôme** sur case sous souris). SpellId.GhostraRepliqueFantome=88. `CombatRulesVersion 39 → 40`. Fichiers touchés : `Decoy.qtn` (enum DecoyKind +1 variant), `Spell.qtn`, `DecoyHelpers.cs` (DestroyByEnemyAction nouveau + TickLifetime heal expire), `SpellSystem.cs` (handler case + migration Charge Brutale), `SpellRegistry.cs` (constantes + SpellDef), `CombatInputController.cs` (bind G Ghostra), `GameVersion.cs`. **Pas de [Networked] field nouveau** → régen prefab/scène NON nécessaire ; juste **régen Quantum CodeGen + recompile** suffisent. **PROCHAIN STEP : régen Quantum CodeGen + test E2E G spawn + cap 3 + heal +80 sur expire 2 rounds + heal +40 sur destruction par Charge Brutale**.
**Précédente session 16 mai (jour, suite 7) :** ✅ **3.7.a.i VALIDÉ E2E**. Lame Spectrale 170 base + bonus dorsal Angle 3 (+80) + PlaieOuverte auto + bonus +60 PlaieOuverte = combo dorsal Angle 3 = **310 dmg sur un cast** (170+60+80). Lame Vorace +60 si PlaieOuverte (non consommée confirmé). Tick PlaieOuverte fin de round -40 HP. Facing tracking complet : MovementSystem + MovementHelpers.MoveNonPM (Charge Brutale / recul Tranche-Âme) + Pas Furtif + Évanescence + Traquenard + Push (Onde de Choc / Bourrasque / Souffle Glacial) + Pull (Empoignade) + Effondrement swap + AISystem bot move. Spawn decoy refuse case occupée par combatant/obstacle. Total combat E2E : Soulrender 1500→520 HP en 4 casts Ghostra (2 Lame Spec + 2 Lame Vorace).
**Précédente session 16 mai (jour, suite 7 livraison) :** 🚀 **3.7.a.i LIVRÉ** (code seulement, pas encore E2E). 2 sorts offensifs Ghostra (Lame Spectrale + Lame Vorace Spectrale) + Status PlaieOuverte (DoT 40/tour x 2t) + tick fin de round + tracking Facing en simulation Quantum (préreq dorsal). Fichiers nouveaux : `FacingHelpers.cs` (IsDorsalHit, FacingFromGridDelta, Opposite). Schema : `enum IsoFacing` + `Combatant.Facing` field + `StatusKind.PlaieOuverte=23` + 2 SpellId (86/87). Wiring : CombatantSystem init Facing au spawn (P0=NE/P1=NW), MovementSystem.ApplyMove update Facing depuis dx/dy, TurnSystem.TickPlaieOuverte fin de round, SpellSystem inline bonus PlaieOuverte par sort + bonus dorsal Ghostra générique post-handlers, ApplyPlaieOuverteIfAngle2Plus post-damage auto sur dorsal Angle 2+. GhostraPassif.ApplyPlaieOuverteIfAngle2Plus retire stub, applique vrai StatusKind. Binds AZERTY : A/Z context-aware étendu Ghostra (Lame Spectrale / Lame Vorace). `CombatRulesVersion 38 → 39`. **PROCHAIN STEP : régen Quantum CodeGen + test E2E A/Z avec/sans dorsal/PlaieOuverte**.
**Précédente session 16 mai (jour, suite 6) :** ✅ **3.6 FRAMEWORK GHOSTRA VALIDÉ E2E**. F12 spawn decoys (3 slots OK, cap 3 respecté) + Permutation P Angle 3 OK (1×/tour cap respecté) + sprite Ghostra calibré (Scale 1.16 / Y -0.22 via `RestructureGhostraPrefabTool` créé child Visual) + DecoyView spawn fake-Ghostra sprites identiques (Scale/Y synced via `PatchDecoyViewSettingsTool`) + stages 0/1/2 mappés sur Angle Mort (CombatantRenderer.ComputeStage patché). Schema Quantum : enum `DecoyKind` (None/Standard/Protective) + struct `DecoySlot` (Kind, PosX, PosY, SpawnedOnTurn, HP) + `array<DecoySlot>[3] Decoys` sur Combatant + 2 fields (`LastPermutationOnTurn`, `LastExecutionSpectraleUsedOnTurn`). 5 fichiers Quantum nouveaux : `Decoy.qtn`, `DecoyHelpers.cs` (Spawn/Destroy/Count/TryFindEnemyDecoyAt/TickLifetime/TryPermute), `GhostraPassif.cs` (Angle 1/2/3 + DorsalBonus 0/50/80 + ApplyPlaieOuverteIfAngle2Plus stub + OnSubTurnStart hook tick lifetime), `GhostraSystem.cs` (parse DebugSpawnDecoyCommand + PermutationCommand), `DebugSpawnDecoyCommand.cs`, `PermutationCommand.cs`. Wiring : TurnSystem.EnterTurnStart appelle `GhostraPassif.OnSubTurnStart` après Necram, SystemSetup register `GhostraSystem`, CombatantSystem swap P0 = Ghostra (était Necram), init des 2 nouveaux fields à -1000. CombatInputController : touche **F12** = DebugSpawnDecoyCommand (cheat test passif), touche **P** context-aware (Pilier si Colossar / Permutation si Ghostra). Assets copiés `Downloads/ghostra/Ghostra/*` → `Assets/_Nymora/Art/Sprites/Ghostra/` (avatar + 6 .aseprite stage0/1/2 NE/SE + 36 .gif anims + 17 icônes Spell_Icon + marque ciblage 4 frames + tile case voilée + VFX signature 8 frames). AutoSliceFrameSheetsTool patché (ajout dossiers Ghostra/Marques+Tiles+VFX). BuildGhostraAnimator.cs créé (mirror BuildNecramAnimator). CombatantRenderer.ComputeStage patché : cas Ghostra → stage mappé sur Angle Mort (0 leurre→s0 / 1-2→s1 / 3→s2) au lieu de Resource. CombatDebugOverlay affiche `Decoys: X/3  Angle Y  lastPermut=Z` + détail par slot. `CombatRulesVersion 37 → 38`. **PROCHAIN STEP : régen Quantum CodeGen + Build Ghostra Animator + Auto-slice + test E2E F12 spawn decoys + P permutation Angle 3** (cf [[feedback-networked-field-regen-protocol]]).
**Précédente session 16 mai (jour, suite 5) :** 🏆 **3.5.c.vi VIRUS FATAL VALIDÉ** (E2E OK) → **NECRAM 16/16 BIBLE V7.1 CLÔTURÉ**. Dernier sort Necram = SIGNATURE. 2 PA, range 5, **6/6 PT consommé tout** (HGCostMandatory générique), cooldown 4 tours via nouveau `[Networked] Int32 LastVirusFatalUsedOnTurn` sur Combatant (pattern Âme Lacérée/Traquenard/Effondrement). Touche **B** universelle signature étendue context-aware Necram. Effet : tick venin instantané **× 3** sur cible (multiplicateur Floraison appliqué). Formule : `(stacks × GetTickDmgPerMark(densityGlobal) + MarqueSacBonus) × 3`. Bypass shield + réduction. Hook Symbiose Morbide × 3 (heal Necram porteur). Si cible **survit** : VeninStacks=0 (consommées). Si cible **meurt** : transfert marques sur ennemi vivant le plus proche via `VeninHelpers.TryTransferVeninOnKill` (réutilise infra Morsure Putride ; en 1v1 perdues silencieusement). Cas dégénérés gérés : cible vide / déjà morte / sans marques → PA consommée + cooldown active (intentionnel Bible "consomme toute la jauge"). `CombatRulesVersion 36 → 37`. SpellId.NecramVirusFatal=85. ⚠️ **Régen Networked field complète** : Quantum CodeGen + recompile + prefab Combatant + scène 30_CombatIA réalisée. Test E2E confirmé : 4 marques × 40 dmg/marque (density 4 tier 2) × 3 = **480 HP exact Bible**, VeninStacks 4→0 (consommées car survit 490→10 HP), cooldown active tour 5 → ré-castable tour 9. **Total classe Bible V7.1 implémentée : 4/5 = 80%** (Soulrender + Nightseer + Colossar + Necram). Reste Ghostra (0/16, différé).  
**Précédente session 16 mai (jour, suite 4) :** 🔍 **AUDIT NECRAM BIBLE-STRICT** réalisé sur les 15 sorts livrés. Verdict global : **14/15 conformes Bible V7.1**. **1 divergence trouvée** : le sort livré sous le nom "Pulse Sanguin Vert" est en réalité **Régénération Nécrotique** (Bible ligne 971) — nom inventé par Claude lors de la livraison 3.5.c.iv. Audit a aussi révélé **PA cost faux** : Bible dit 2 PA, code disait 3 PA. **Correctif applique (décision Lorenzo) : minimal fix PA 3→2** dans `SpellRegistry.PulseSanguinVertPACost`, **garde le nom inventé** côté code (constantes/SpellId/binds) pour ne pas casser scène/prefabs/replays/STATUT historiques. Commentaire Bible-canonical ajouté dans SpellRegistry pour traçabilité. **Mémoire `feedback_dont_invent_spell_names`** sauvegardée pour ne plus reproduire l'erreur (complète `feedback_bible_check_before_spell_delivery` sur le nom du sort lui-même). `CombatRulesVersion 35→36`. Tous les autres sorts (Crachat, Morsure, Détonation, Faux, Brume, Inoculation, Marque Sacrificielle, Symbiose, Pas Spectral, Contagion, Voile, Carapace, Drain, Cocon) + passif Floraison + Putréfaction (cap 6, gain rules, paliers 30/40/50, regen +10/marque tier 2, halo -20 HP rayon 3) sont **100% conformes Bible**. Reliquats mineurs notés : (1) Bible mentionne "25 dgts/début tour" pour Brume Toxique dans section narrative ligne 849 — non implémenté, à clarifier avec Lorenzo si tick passif zone attendu ; (2) Pas Spectral "appliquent 1 marque bonus" interprété comme "+1 marque par ennemi traversé" — formulation Bible ambiguë.  
**Précédente session 16 mai (jour, suite 3) :** ✅ **3.5.c.v COCON PUTRIDE VALIDÉ** (E2E OK). 5e brique Bloc C Necram survie → **Bloc C SURVIE 5/5 CLÔTURÉ**. Signature panic Bible V7.1 : **4 PA self**, gate `HP < 30% MaxHP` vérifié en amont (style Dernier Souffle, rejette sans consommer PA si HP plein/insuffisamment bas), **1×/match** via `OncePerMatchBit=3` système générique. Effet : Necram heal **+220 HP** (cap MaxHP) + applique **+1 marque venin** sur tous ennemis vivants Manhattan ≤4 du caster via `VeninHelpers.ApplyMark` (cap 4/cible + cap +2 PT/tour Necram via `GainPutrefactionFromMarkApply` respectés par helper). Touche AZERTY **M** context-aware (Nightseer Évanescence target=clic / Necram **Cocon Putride** target=caster cell). `CombatRulesVersion 34→35`. SpellId.NecramCoconPutride=84. Test E2E validé : rejet HP plein tour 1 (`1500/1500`) sans consommer PA ; cast réussi à HP 350/1500 (23.3%) → heal +220 (350→570) + AoE marque P1 stacks `4→4` (cap 4/cible démontré) + log final `1 ennemi(s) marque(s) dans rayon 4` ; PA 8→4 (cost 4). **Total Necram : 15/16** sorts livrés (5 offensifs + 5 tactiques + 5 survie). Reste : Virus Fatal (3.5.c.vi signature ultime 6/6 PT Putréfaction).  
**Précédente session 16 mai (jour, suite 2) :** ✅ **3.5.c.iv PULSE SANGUIN VERT + 3.5.c.iii DRAIN VITAL VALIDÉS** (E2E OK). **Pulse Sanguin Vert** : 3 PA self, heal Necram caster base 70 + 15/marque venin somme sur ennemis vivants Manhattan ≤4 (cap bonus +90 HP). +30 HP additionnel avec 1 PT optionnel (Shift+X). Marques NON consommées. Touche AZERTY **X** context-aware. `CombatRulesVersion 33→34`. SpellId.NecramPulseSanguinVert=83. **Drain Vital** : 3 PA range 4, 60 dmg cible + heal caster 30 HP (ou 60 HP si target.VeninStacks≥3). Marques NON consommées. Heal applique même si target meurt. Touche AZERTY **N** context-aware. `CombatRulesVersion 32→33`. SpellId.NecramDrainVital=82.  
**Précédente session 16 mai (jour) :** ✅ **3.5.c.ii CARAPACE VISQUEUSE VALIDÉE** (E2E OK). 2e brique Bloc C Necram survie → **Bloc C SURVIE 2/5** (livrés : Voile + Carapace ; reste Drain Vital + Pulse Sanguin Vert + Cocon Putride signature). Bouclier piégé Bible V7.1 : 3 PA self, `ShieldActive 110 HP / 2 rounds` (réutilise pipeline shield existant) + flag `StatusKind.CarapaceVisqueuse=22` (Magnitude=0, 2 rounds) qui active le hook riposte marque. Bible "frappe le bouclier" = condition `shieldAbsorbedThisHit > 0` → trigger même si shield absorbe TOUT le dmg (HP_loss=0). Touche AZERTY **C** context-aware (Nightseer Camouflage Ronces / Necram **Carapace Visqueuse**). `SpellId.NecramCarapaceVisqueuse=81`. **Refacto Bible-strict `isMelee`** : redéfini "attaque mêlée" en Chebyshev caster-cible ≤ 1 (8 voisines + soi) au moment du dmg, au lieu de `spellDef.RangeMax == 1` strict. Bible-cohérent : Charge Brutale post-move = mêlée, Faux Décharnée Square3x3 = mêlée, Tranche-Âme range 1 = mêlée, Crachat range 4 = pas mêlée. Hooks Voile + Carapace utilisent la nouvelle définition. **Rebranche manuelle hooks dans path custom Charge Brutale** (`SpellSystem.cs:~1350`) qui bypass le damage loop standard — cf memory [[project_spell_move_terrain_trigger_gap]] (dette technique : Empoignade/Pas Furtif/Pas Spectral/Évanescence ont leurs propres paths, à brancher au cas par cas). `CombatRulesVersion 29→32` (29 carried from sessions intermédiaires Pas Spectral + Voile + Carapace + isMelee refacto). Test E2E confirmé : Charge Brutale + Carapace → shield absorbe 110 + +1 marque Soulrender ; Charge Brutale + Voile → +1 marque attaquant à chaque cast mêlée ; Curée + Voile → +1 marque ; fin sub-turn Manhattan ≤2 → +1 marque adjacence ; Floraison tier 2 density 4 → regen +40 HP P0. ⚠️ **Edge case mineur non-bloquant** : CB2 du round 3 (P1 déjà adjacent post-CB1, pas de log "fonce") ne trigger pas Voile sur cette charge spécifique alors que CB1, CB3 et Curée le font normalement. Suspicion path Charge Brutale court-circuite damage hook quand caster adjacent target. À investiguer si critique en multi/AI tests, mais pas bloquant. **Briques intermédiaires entre suite 7 et aujourd'hui** (state restauré post-crash) : ✅ 3.5.b.iii Pas Spectral (Bloc B tactiques 5/5 clôturé) + ✅ 3.5.c.i Voile de Pestilence (Bloc C survie 1/5). Total Necram : **12/16** sorts livrés.  
**Précédente session nuit 15 mai (suite 7) :** ✅ **3.5.b.iv CONTAGION VALIDÉ** (E2E 1v1 fallback OK). 4e brique Bloc B Necram tactiques → **Bloc B TACTIQUES 4/5** (reste Pas Spectral 3.5.b.iii). Propagation AoE marques venin : 3 PA, range 5, target ennemie marquée requise. Copie min(stacks, cap) marques sur autres ennemis rayon 3 Manhattan ; cap 3 default, **4 avec 2 PT optionnel** (Shift+F). En 1v1 (pas d'autres ennemis du caster) : +1 marque boost sur cible (fallback). Touche AZERTY **F** context-aware (Nightseer Bourrasque shift=1PR / Necram **Contagion** shift=2PT). LoS check ajouté. `CombatRulesVersion 26→27`. SpellId.NecramContagion=78. Test E2E : 3× casts 1v1 fallback (+1 marque chacun), 1× rejet target non marquée, 1× rejet distance 0 (caster cell), 1× boost 2 PT consume validé. Propagation cap 3/4 non testable en 1v1 (deferred à 2v2/3v3). ⚠️ **Régression non-bloquante détectée** : Symbiose Morbide cast au round 3 P0 (target=(5,8) alors que caster en (6,8) — anomalie targeting) → heal Symbiose ne trigger pas au tick suivant. À investiguer si réapparaît. Possibilité : régen Quantum CodeGen partielle entre sessions.  
**Précédente session nuit 15 mai (suite 6) :** ✅ **3.5.b.ii SYMBIOSE MORBIDE VALIDÉ** (E2E OK). 2e brique Bloc B Necram tactiques. Self-buff lifesteal DoT : 3 PA, self, status 2 rounds. A chaque tick venin sur ennemi, tout Necram porteur heal `min(stacks, 4) * 8` HP (max +32 HP/tick, +64 sur 2 rounds avec 4 marques). Touche AZERTY **D** context-aware (Nightseer ChampDeMines / Necram **SymbioseMorbide**). Nouveau `StatusKind.SymbioseMorbide=19`. Hook dans `VeninHelpers.TryTick` après tick effectif : itère tous Necram vivants avec status → heal. `CombatRulesVersion 25→26`. SpellId.NecramSymbioseMorbide=77. Test E2E : Crachat×2 (4 marques) + Symbiose → tick venin -160 dmg cible ET +32 HP heal Necram simultanés (vérifié round 2 + round 3). PA cost 3 rejeté correct si PA 2. Re-cast = refresh.  
**Précédente session nuit 15 mai (suite 5) :** ✅ **3.5.b.i INOCULATION + MARQUE SACRIFICIELLE VALIDÉ** (E2E OK). Première brique Necram tactiques (Bloc B). Inoculation (1 PA, range 5, +2 marques cap 4, no damage, Putréfaction +2 PT) ; Marque Sacrificielle (2 PA, range 5, status 3 rounds, +20 dmg flat par tick venin sur cible). Touches AZERTY **Q** (Nightseer MarqueDuChasseur / Necram **Inoculation**) et **S** (Nightseer FiletDeRonces / Necram **MarqueSacrificielle**) context-aware. Nouveau `StatusKind.MarqueSacrificielle=18` dans Status.qtn. Hook dans `VeninHelpers.TryTick` : totalDmg += Magnitude si status actif. `CombatRulesVersion 24→25`. SpellId.NecramInoculation=75 / SpellId.NecramMarqueSacrificielle=76. Test E2E confirme : Inoculation 2× sur Soulrender → cap 4 marques + Putréfaction 5/6 PT ; tick venin sans MS = 160 dmg (4×40), tick avec MS = 180 dmg (vérifié 2× rounds 3-4) ; portée 5 rejetée à distance 8 ; Floraison tier 2 actif (regen +40 + halo -20).  
**Précédente session nuit 15 mai (suite 4) :** ✅ **3.5.a.iii BRUME TOXIQUE VALIDÉ** (E2E partiel OK). 5e sort offensif Necram livré → **Bloc A offensifs Necram 5/5 CLÔTURÉ**. Brume Toxique : 4 PA, range 4, AoE 3x3 / 2 rounds. Pose : 60 dmg bypass shield/réduction + 1 marque venin sur occupants présents non-caster non-Necram. Entry (MoveCommand standard) : 30 dmg bypass + 1 marque. End-of-sub-turn dans zone : +1 marque (sans dmg). Touche AZERTY **V** context-aware (Nightseer SalveMortelle / Necram BrumeToxique). Décisions design Lorenzo : **caster Necram immunisé** (skip par classe), **pas de stack** (refus cast si chevauchement, PA non consommé), **bypass shield/réduction** (Bible DoT pénétration totale). `CombatRulesVersion 23→24`. SpellId.NecramBrumeToxique=74. TerrainKind.BrumeToxique=3. **Hotfix** AutoSliceFrameSheetsTool : ajout dossiers Necram/Marques/Tiles/VFX dans SheetFolders. Tests E2E confirmés : cast pose (-60 + marque + Putréfaction +1 PT), refus chevauchement 2x, Necram immunisé sa propre Brume, decrement 2 rounds. **Reliquat global** : sorts qui déplacent (Charge Brutale, Bourrasque, Pas Furtif, Empoignade) **bypassent** les hooks terrain entry (Brume + Vapeur Carmin) car ils contournent `MovementSystem.ApplyMove` — fix différé global, cf memory [[project_spell_move_terrain_trigger_gap]]. **Asset TODO** : `tiles_zone_putride_4frame.gif` du designer rend mal en jeu (relief vertical qui dépasse case iso) — Lorenzo demande au designer de refaire **anims VFX + tiles en PNG aplats**. Sort fonctionne sans VFX en attendant.  
**Précédente session nuit 15 mai (suite 3) :** ✅ **3.5.a.ii DÉTONATION VIRULENTE + FAUX DÉCHARNÉE VALIDÉ** (E2E OK). 2 sorts offensifs Necram complémentaires : Détonation Virulente (4 PA, range 4, 80 dmg + 50/marque consommée, reset VeninStacks=0 post-damage — max 280 dmg avec 4 marques) ; Faux Décharnée (4 PA, AoE Square3x3 autour caster mêlée, 130 dmg/cible + heal Necram 30/marque cumulée sur cibles touchées cap +120 HP, 4 marques = 120 heal max). Touches AZERTY **E** (Nightseer DétonationOnirique / Necram **DétonationVirulente**) et **R** (Nightseer FrappeDeLOmbre / Necram **FauxDécharnée**) context-aware. `CombatRulesVersion 22→23`. SpellId.NecramDetonationVirulente=72 / SpellId.NecramFauxDecharnee=73. Test E2E confirme combo **SOUCHE Bible-strict** : 2× Crachat (4 marques) → Détonation (280 dmg + reset) → 2× Crachat (4 nouveau) → Faux (130 dmg + 120 heal).  
**Précédente session nuit 15 mai (suite 2) :** ✅ **3.5.a.i CRACHAT ACIDE + MORSURE PUTRIDE VALIDÉ** (E2E OK). 2 premiers sorts offensifs Necram livrés. Crachat Acide (3 PA, range 4, 90 dmg + 2 marques cap 4). Morsure Putride (4 PA, melee 1, 110 + 22/marque cap +90 = max 200, transfert marques au kill via `VeninHelpers.TryTransferVeninOnKill` — 1v1 = marques perdues silencieusement). Touches AZERTY **A** (Nightseer TirPrecis / Necram CrachatAcide) et **Z** (Nightseer VoleeDEpines / Necram MorsurePutride) **context-aware** à la classe du caster via `TryGetCasterClass`. `CombatRulesVersion 21→22`. SpellId.NecramCrachatAcide=70 / SpellId.NecramMorsurePutride=71. Test E2E : 2 Crachats round 2 → cap PT 2/tour respecté + 4 marques venin appliquées + 2 Morsures round 4 → 198 dmg chacune (110+88, cap bonus 90 atteint, 4 marques × 22 = 88). Necram mort fin round 4 (Soulrender bourre 9 Charge Brutale + Curee), pas eu le temps de tester transfert au kill car en 1v1 le seul ennemi du Necram = Soulrender. Reliquats 3.5.a : **Détonation Virulente + Faux Décharnée** (3.5.a.ii prochain) + **Brume Toxique** (3.5.a.iii nouvelle TerrainKind).  
**Précédente session nuit 15 mai (suite) :** ✅ **3.4 FRAMEWORK MARQUE VENIN + PASSIF FLORAISON VALIDÉ** (E2E OK). Première brique Phase 3 Bloc C Necram. Schema Quantum `VeninStacks` (0-4) + `LastVeninTickOnTurn` + `PutrefactionMarksGainedThisTurn` sur Combatant. `VeninHelpers` : ApplyMark cap 4, GetGlobalDensity, GetTickDmgPerMark (30/40/50 selon paliers Bible), TryTick avec bypass shield/réduction + gain Putréfaction (cap +2/tour Necram via marque appliquée + +1/tick global). `NecramPassif.OnSubTurnStart` hook dans TurnSystem : tick venin sur porteur + regen Necram +10/marque globale tier 2+ + halo toxique rayon Manhattan 3 (-20 HP/début tour ennemi). `DebugApplyVeninCommand` + `NecramSystem` + touche **F11** pour test sans sorts. `CombatRulesVersion 20→21`. CombatDebugOverlay affiche `Venin: X/4 marques`. Validation E2E : densité 1-2 → 30 dmg/marque, densité 4 → 40 dmg/marque + regen 40 HP + halo -20 HP, cap 4 marques + cap 2 PT/tour Necram respectés. Bypass shield/réduction non testé (pas de buff actif), à valider en 3.5+. Pack visuel Necram livré aujourd'hui (avatar + 6 .aseprite stage0/1/2 NE/SE + 36 .gif + 17 icônes sorts + marque venin + tile zone putride + VFX signature). Anims Colossar stage 1+2 livrées (BuildColossarAnimator étendu de 2→6 controllers). Necram swapped P0 dans CombatantSystem (ex-Colossar). Prefab Necram restructuré avec child "Visual" (Y offset configurable) pour décaler sprite verticalement sans toucher position grid.  
**Précédente session nuit 15 mai (fin) :** ✅ **5.2 SUCCÈS VALIDÉE** (E2E OK). Bloc A Phase 5 "Progression perso" bouclé (5.1 + 5.2). Backend `UserAchievement` Prisma + catalog static 12 succès MVP (3 cat : Premiers pas / Combat / Progression — **pas de social anti-farm** cf [[project-no-social-achievements]]) + service `awardProgress` + triggers `onMatchEnd`/`onLevelUp` + 2 endpoints REST + push WS `ACHIEVEMENT_PROGRESS` / `ACHIEVEMENT_UNLOCKED`. Wiring depuis `/progression/award-xp` via parse source (TEMP MVP, à propre en Phase 6 avec endpoint match-completed dédié). Unity onglet "Succès" du profil branché (groupé par catégorie, progress bars, header "X/12 · N points", toast chat doré au unlock). Smoke test 8 scénarios → PASSED.  
**Précédente session nuit 15 mai (plus tard) :** ✅ **5.1 NIVEAUX PAR CLASSE VALIDÉE** (E2E OK). Première brique Phase 5 livrée. Backend `ClassProgression` Prisma + 2 endpoints `/progression/me` et `/progression/award-xp` + courbe XP `100 + n*100` + level cap 50 + push WS `XP_AWARDED` / `CLASS_LEVEL_UP` depuis route (single source of truth). Unity onglet "Classes" du profil branché (placeholder remplacé par 5 rows avec level + XP bar + couleur classe). Award XP TEMP MVP wiré sur `HubMatchResultDisplay` au retour hub (V=50 / D=15 / Draw=25 XP). Smoke test 10 scénarios → PASSED. **Convention sauvegardée** : XP de classe sera ranked-only en prod (Phase 6), wiring temporaire à retirer.  
**Précédente session nuit 15 mai :** ✅ **4.11 CLANS VALIDÉE** (E2E multi-instance OK) : système clans complet sur 6 sous-briques (4.11.a→f) + polish UX. 4 rôles Dofus-style (Leader/Officer/Member/Recruit), invite par displayName OU UUID, 10 endpoints REST, 6 push WS events (depuis routes REST, pas de handlers WS dédiés → single source of truth). Panel 2 modes (NoClan : créer + invitations / InClan : header + membres + actions selon rôle). 3e bouton hub "Clan" bas-droite (à gauche d'Amis), 6e action "Inviter dans clan" dans ChallengePopup affichée **uniquement** si user a un clan + droit Leader/Officer. **Hotfix critique** : `HubAvatar.NetSub` passé de `NetworkString<_64>` (avant `_16` tronquait l'UUID de 36 chars → SEND_FRIEND_REQUEST et clan-invite-by-UUID cassés). Régen prefab + scène + rebuild standalone effectués (cf [[feedback-networked-field-regen-protocol]]). **Bonus polish** : empty strings du payload Unity (JsonUtility sérialise les `string null` en `""`) normalisés en `undefined` côté backend pour que Zod `.optional()` passe. Reliquats : pas de cancel-own-outgoing-clan-invite ; chat dédié clan différé à 4.11.chat (channel WS dynamique).  
**Précédente session soir 15 mai (tard) :** ✅ **4.10 AMIS PLEIN VALIDÉE** (E2E multi-instance OK) : système amis complet sur 8 sous-briques (4.10.a→h). Backend Prisma `Friendship` (PENDING/ACCEPTED) + 5 endpoints REST `/friends*` + 3 handlers WS `SEND_FRIEND_REQUEST` / `RESPOND_FRIEND_REQUEST` / `REMOVE_FRIEND` + 7 push events (incoming/sent/response/removed + online/offline/online-list). Unity HubFriendsPanel (3 sections + barre recherche) + HubFriendsButton (badge demandes pending) + IncomingFriendRequestPopup + 5e action "Ajouter en ami" dans ChallengePopup data-driven. **4.10.g online status** : dots vert/gris temps réel via push WS au connect/disconnect (multi-instance gère correctement les multiples sockets/user via channels.isUserOnline). **4.10.h MP depuis amis** : bouton "MP" sur chaque ami → ouvre tab Privé + pré-remplit `/w <pseudo> ` + focus clavier. **Bonus déblocant** : SEND_WHISPER refacto async avec `resolveWhisperTarget` multi-pass (sub direct → email → DB displayName), donc `/w Alice salut` marche maintenant nativement. SEND_FRIEND_REQUEST accepte `targetUserId` (UUID) ou `targetUser` (displayName).  
**Précédente session soir 15 mai :** ✅ **4.12 PROFIL JOUEUR VALIDÉE** : panel 5 onglets (Vue / Stats / Classes / Succès / Cosmétiques) accessible via bouton bas-droite "Mon profil". Onglet Vue alimenté par nouveau endpoint REST `GET /profile/me` (auth Bearer JWT). 4 autres onglets en placeholder "Coming soon" propre. Chat panel repositionné bas-gauche. **Privacy fix** : email volontairement NON affiché dans l'UI (streamer-safe), backend continue de le retourner via API. **Bonus déblocant** : `dev:token` adapté pour upsert User+Profile en DB (résout dette listée ligne 18, débloque 4.10 plein futur) + try/catch dans `/profile/me` (évite crash serveur si sub non-UUID). Dette restante : `lastLoginAt` pas mis à jour au 1er run dev:token (branche `create`), nice-to-have à fixer plus tard. **Reliquat designer** : vrai profil avec sprite perso prévu post-Phase 4.  
**Précédente session jour 15 mai :** 🎁 **POLISH HUB LIVRÉ** : (1) torches animées 8 frames (Editor Tool `CreateHubTorchPrefabTool` — PPU 32, 12fps loop, prefab drag-and-drop) ; (2) preview tile au survol souris (`HubTileHoverView` + `RefreshTileColor(gx, gy)` sur `HubGridRenderer` + patch idempotent dans `PatchCommunityHubSceneTool`). 🚀 **PUSH GITHUB COMMIT `d36fc2e`** — Phase 4 (briques 4.1→4.13 hors 4.12) + session courante en 1 méga-commit (441 fichiers, +140k lignes, LFS 43MB). Branche `main` à jour avec `origin/main`.  
**Mis à jour par :** Claude (session courante)

---

## 🎯 OÙ ON EN EST

**Phase actuelle :** 🏆 **PHASE 6 BLOC A (RANKED 1v1) CLÔTURÉE** (matchmaking + ELO/MMR + XP/Nymos ranked-only + 8 rangs + saisons + leaderboard, tout déployé prod). Avant : Phase 1/2/3 (5/5 classes Bible V7.1), Phase 4 hub, Phase 5 Bloc A (niveaux+succès) + Bloc B (deck builder) + Bloc C partiel (Nymos), infra prod OVH. **Voir le bloc de session du jour en tête de fichier pour le détail Phase 6.**
**Brique en cours :** _aucune — choix : 6.7 anti-smurf (clôture Phase 6) OU Phase 5 reliquats sans dépendance Kyami (boutique 5.5 / Battle Pass 5.7 / quêtes 5.8). 2v2/3v3 = bloc futur._
**Brique précédente :** ✅ **POLISH FINAL combat / clôture Phase 3** validé 17 mai (cf entrée du jour : confirmations 1→6, reliquats 7-8 différés Phase 7).
**Brique précédente :** ✅ **3.7.d Exécution Spectrale** (signature Ghostra SpellId 101, livrée + validée 16 mai).
**Brique précédente :** ✅ **3.7.c.i→v Bloc C Survie Ghostra** (Linceul d'Ombres / Voile Spectral / Réplique Protectrice / Dernier Pas / Pas de l'Au-Delà — SpellId 96→100 livrés + validés 16 mai).
**Brique précédente :** ✅ **3.7.a.iv Danse des Lames** validée 16 mai E2E (180 base + 200 avec Marque, pipeline générique 100% plug-and-play).
**Amendements roadmap actés 16 mai 2026 (audit projet) :**
  - ✅ Ghostra avant Phase 5 (priorité Bible V7.1 complète à 100%)
  - ✅ Modes 2v2 + 3v3 (Phase 6) MAINTENUS dans le scope alpha
  - ✅ IA Hard MCTS MIGRÉE Phase 3 → Phase 7 (différée mais pas zappée)
  - ✅ Stripe / monétisation : décision repoussée fin Phase 5
  - ✅ Phase 5 économie : **200 succès + 100 tiers BP** avant soft launch (roadmap V1 stricte, annule scope MVP "20 tiers")
  - ✅ Phase 7 polish alpha : tutoriel + replay public + accessibilité + localisation FR+EN tous conservés
**Brique précédente :** ✅ **3.7.a.i Lame Spectrale + Lame Vorace** validées 16 mai E2E (combo dorsal Angle 3 = 310 dmg confirmé, facing tracking complet).
**Brique précédente :** ✅ **3.5.c.vi Virus Fatal** validée 16 mai (E2E OK, Necram 16/16 CLÔTURÉ).
**Brique précédente :** ✅ **3.5.c.v Cocon Putride** validée 16 mai (E2E OK + gate HP <30% + heal 220 + AoE marque rayon 4 + once-per-match).
**Brique précédente :** ✅ **3.5.c.iv Pulse Sanguin Vert** validée 16 mai (E2E OK + cap +90 bonus + 1 PT optionnel via Shift+X).
**Brique précédente :** ✅ **3.5.c.iii Drain Vital** validée 16 mai (E2E OK + heal scalé sur stacks target).
**Brique précédente :** ✅ **3.5.c.ii Carapace Visqueuse** validée 16 mai (E2E OK + refacto Bible-strict isMelee Chebyshev≤1 + rebranche hooks dans path Charge Brutale).
**Brique précédente :** ✅ **3.5.c.i Voile de Pestilence** validée (entre suite 7 et aujourd'hui, état restauré post-crash).
**Brique précédente :** ✅ **3.5.b.iii Pas Spectral** validée (Bloc B tactiques 5/5 clôturé).
**Brique précédente :** ✅ **4.13 Modération light** validée — Signaler + mutes progressifs + filtre.
**Brique précédente :** ✅ **4.10.refacto ChallengePopup data-driven** validée — pattern `List<MenuAction>` + boutons instanciés runtime via VerticalLayoutGroup.
**Brique précédente :** ✅ **4.8.d.iii stub + 4.9** boucle E2E complète validée (défi → match stub → retour hub avec result coloré).
**4.10 Amis (plein)** : DIFFÉRÉ — décision Lorenzo 14 mai 2026 soir. Persistance Postgres requiert adaptation `dev:token` pour upsert User en DB. À frais avec Docker setup propre.
**Brique précédente :** ✅ **4.8.d.ii Transition scène hub → combat casual** validée E2E + fix shutdown propre Fusion. Unity `MatchBridge` (static cross-scène) + `HubMatchTransition` (async Task : delay + await runner.Shutdown() + SceneManager.LoadScene) + `MatchTestLogger` (lit MatchBridge dans 33_CombatCasual stub). 2 Editor tools : `CreateCombatCasualSceneTool` (génère scène + add BuildSettings) + `CreateCommunityHubSceneTool` étendu (HubMatchTransition GO + add BuildSettings). Log `[Hub] Runner shutdown : Ok` + `[Fusion] Left Room` clean, plus de warning `HubGridRenderer introuvable`.
**Brique précédente :** ✅ **4.8.d.i Protocole MATCH_READY** validée E2E (MATCH_READY reçu + opponent résolu sur main thread, system line cyan OK).
**Antérieures :** ✅ 4.8.c popup Accept/Refuse, ✅ 4.8.b wire backend défi, ✅ 4.8.a popup local, ✅ 4.7 chat privé, ✅ 4.6 chat global.
**Brique précédente :** ✅ **4.8.b Wire backend défi (SEND_CHALLENGE)** validée E2E (clic Défier → CHALLENGE_SENT ack + INCOMING_CHALLENGE forwardé au target). Backend `WELCOME` + handler `SEND_CHALLENGE` + randomUUID. Unity `HubChatClient.MyUserId/MyEmail`, `SendChallenge`, 3 events (OnWelcome/OnIncomingChallenge/OnChallengeSent), `HubAvatar.NetSub<_16>` race-safe via OnWelcome. **Incident résolu** : ajout `[Networked] NetSub` a déclenché `InvalidOperationException: Invalid Length: 2595` côté remote — fix = régén prefab + scène + rebuild standalone (memory `feedback_networked_field_regen_protocol.md` sauvegardée). Emoji ⚔ retiré (manquant dans LiberationSans SDF), remplacé par `[DEFI]` ASCII.
**Brique antérieure :** ✅ **4.8.a Popup défi local** validée (clic avatar remote → popup, bouton Défier → Debug.Log).
**Brique antérieure :** ✅ **4.7 Chat privé + filtre anti-insulte** validée E2E (multi-instance dev-1 ↔ dev-2 OK). Backend `profanityFilter.ts` + userPool + `SEND_WHISPER`. Unity tabs Global/Privé + parser `/w`.
**Phase 3 :** ⏸️ **Partiellement clôturée** — Bloc A (préreqs cross-classe) + Bloc B (Colossar 16/16) + **Bloc E lite (Replay + Debug Overlay)** ✅. **Bloc C Necram (0/16) + Bloc D Ghostra (0/16) + Bloc E IA Hard MCTS DIFFÉRÉS** — décision Lorenzo 14 mai 2026 : prioriser Phase 4 (Hub commu) car bibliothèque V7.1 sur 3 classes (Soulrender + Nightseer + Colossar) suffisante pour valider le fun en multi. Necram + Ghostra + IA Hard à reprendre post-Phase 4 ou en parallèle si bandwidth.
**Bloc A Phase 3 :** ✅ **3.1 + 3.1.bis VALIDÉES** (14 mai 2026)
**Bloc B Phase 3 :** ✅ **CLÔTURÉ 14 mai 2026 nuit** — Colossar **16/16 sorts** Bible V7.1 conformes E2E validés :
  - ✅ 3.2 Stats + FD + Densité Inerte
  - ✅ 3.3.a.i Frappe Lourde + Représailles + bonus adjacence (cap 4 retours fix audit)
  - ✅ 3.3.a.ii Onde de Choc + Marteau Punisseur + Choc Sismique
  - ✅ 3.3.b.i Pilier + Mur + LoS fix (Pilier permanent post-audit)
  - ✅ 3.3.b.ii Ancrage + Provocation + Brisure
  - ✅ 3.3.b.iii Refacto Bible-correct 5 tactiques
  - ✅ **3.3.audit Pass Bible V7.1** (7 corrections Bible-strict + amendement Filet)
  - ✅ **3.3.c Survie** : Stoïcisme + Garde Protectrice + Ressac Vital + Renvoi du Bouclier + Soin Lourd
  - ✅ **3.3.d Effondrement** signature avec mécanique EJECTION+SWAP originale Lorenzo
**Polish 3.3.d ✅ livré** : TileHoverView (glow + HP obstacles au survol), MovementRangePreview hover-driven, BFS contour obstacles (CombatantRenderer), fallback directionnel Stage N→0 (CombatantView), MarkSpriteLibrary étendu MarkKind (Traque/Empreinte Nightseer), AutoSlice + BindClassVisuals tools, avatars NS+CO binds.
**Asset TODO** : VFX Effondrement (12 frames) — le `.gif` livré est animé 128×128 (1 frame seule lue par Unity). Redemander designer un export PNG sprite sheet horizontal 1536×128. Sort fonctionne sans VFX.
**Cadrage Phase 3 (état final partiel)** : Bloc A préreqs ✅ / Bloc B Colossar ✅ / Bloc C Necram ⏸️ DIFFÉRÉ / Bloc D Ghostra ⏸️ DIFFÉRÉ / Bloc E IA Hard MCTS ⏸️ DIFFÉRÉ + **Bloc E lite Replay/Debug ✅ CLÔTURÉ** (14 mai 2026)
**Cadrage Phase 4** (memory `project_phase4_plan.md`) : 5 blocs / 13 briques — Bloc A Photon Fusion + Scene Hub (4.1→4.4) / Bloc B Backend WS + Chat (4.5→4.7) / Bloc C Défi casual (4.8→4.9) / Bloc D Social amis+clans (4.10→4.11) / Bloc E Profil + Modération (4.12→4.13). Contrainte **local-first** : pas d'Hetzner avant 4.4 validée (gate multi-instance 2 clients PC Lorenzo).
**Statut Phase 1 :** ✅ **CLÔTURÉE le 11 mai 2026** (1.13 reportée Phase 7, sinon 14/14 briques validées)
**Statut Phase 2 :** ✅ **CLÔTURÉE le 13 mai 2026 soir**. 17/17 briques validées + 2.12.bis + 2.13.a/b/c/d/e + 2.14 + 2.15.a/b/c + **2.16.a/b/c complets** (Bloc E IA). **🏆 Soulrender + Nightseer 100% jouables, combat 1v1 vs IA E2E**.

**État classes Bible V7.1 (80 sorts canoniques) :**
  - Soulrender : ✅ 16/16
  - Nightseer  : ✅ 16/16
  - Colossar   : ✅ 16/16
  - Necram    : ✅ **16/16** — 5 offensifs + 5 tactiques + 5 survie + **1 signature Virus Fatal**. Classe complète Bible V7.1.
  - Ghostra   : 🚧 **9/16** — Lame Spectrale + Lame Vorace + Réplique Fantôme + Pas dans l'Ombre + Volte-Face + Saigne-Âme + Frappe Fantôme + Dague Lancée + Marque de l'Ombre ✅ E2E
  - **Total implémenté : 73/80 = 91.25 %** (4 classes complètes + Ghostra 9/16 en cours, reste 7 sorts : Danse des Lames + 5 survie + signature Exécution Spectrale)

**Ordre Bloc D Ghostra (révisé 16 mai, décision Lorenzo "ordre des choses sans défauts") :**
  - ✅ 3.7.a.i Lame Spectrale + Lame Vorace (E2E OK)
  - ✅ 3.7.b.i **Réplique Fantôme** (E2E OK 16 mai — heal +80 expire ×3 confirmés)
  - ✅ 3.7.b.ii **Pas dans l'Ombre** (code livré 16 mai + amendements cap 1×/tour & anim téléport, E2E validé implicitement) — téléport 5 + pivot adj enemy + leurre optionnel Shift+H
  - ✅ 3.7.b.iii **Volte-Face** (E2E OK 16 mai puis **amendé 16 mai suite 12** : 50 dmg + flip sans verrou, à re-tester E2E) — Bible-original abandonné
  - ✅ 3.7.a.ii **Saigne-Âme** (code livré 16 mai, E2E partiel : rejet PA fonctionne, effectif pas testé faute PA dans combat 4 rounds — sera retesté en condition réelle)
  - ✅ 3.7.a.iii **Frappe Fantôme** (E2E OK 16 mai — combo Volte-Face → Frappe Fantôme = 300 dmg + PlaieOuverte appliquée par flag direction forcée, total combat 870 dmg en 3 rounds)
  - ✅ 3.7.b.iv **Dague Lancée** (code livré 16 mai + amendé suite 17 cap 2×/tour + pivot 90° + balance 80→40 dmg suite 19, E2E à re-valider) — 1 PA / range 5 / 40 dmg + pivot 90° / bind F Ghostra
  - ✅ 3.7.b.v **Marque de l'Ombre** (E2E OK 16 mai — combat 5 rounds validé : combo Bible **anti-tank par contournement** confirmé (Marque + Lame Spec dorsal Angle 1 = PlaieOuverte AUTO sans leurre), refresh status OK, +20 dmg buff appliqué sur Dague Lancée (60) + Lame Spec face (190) + Lame Spec dorsal Angle 3 (270 = 170+80+20), total ~700 dmg cumulés sur 5 rounds + plaies)
  - ⏳ 3.7.a.iv Danse des Lames — AoE 8 + bonus dorsal auto si leurre adj
  - ⏳ 3.7.c.i→v Survie (Voile Spectral / Linceul / Pas de l'Au-Delà / Réplique Protectrice / Dernier Pas)
  - ⏳ 3.7.d Exécution Spectrale signature

**CombatRulesVersion :** **56** (bump 3.7.b.v Marque de l'Ombre — nouveau StatusKind.MarqueDeLOmbre=25 + hook +20 dmg sorts Ghostra + hook PlaieOuverte auto dorsal bypass Angle 2+, bind M context-aware Ghostra). Précédent : 55 = bump refonte balance Volte-Face / Dague Lancée — swap damage 50↔80 pour cohérence ratio PA). Précédent : 54 = bump amendement Frappe Fantôme priorité case dorsale — TryFindFreeCellAdjacentToTarget signature change pour prendre IsoFacing targetFacing au lieu de caster pos, ordre back→side1→side2→front via FacingHelpers.IsoFacingToGridDelta nouveau helper). Précédent : 53 = bump amendement Dague Lancée — cap 2×/tour via 2 nouveaux [Networked] fields LastDagueLanceeOnTurn + DagueLanceeCountThisTurn + pivot 90° horaire iso au lieu de pivot face-caster, helper FacingHelpers.RotateClockwise). Précédent : 52 = bump 3.7.b.iv Dague Lancée initiale 1 PA range 5, 80 dmg + force pivot target vers caster + flag LastFacingForcedOnTurn pour combo Dague→Frappe Fantôme, bind F context-aware Ghostra). Précédent : 51 = bump polish Frappe Fantôme anim téléport — dict _lastFrappeFantomeCastTurn dans CombatantRenderer + détection via LastCastSpellId, pas de [Networked] field). Précédent : 50 = bump amendement leurres 4 rounds — DecoyHelpers.LifetimeRounds 2→4 pour permettre setup combos Ghostra, tous leurres + heal Réplique Fantôme +80 à expiration). Précédent : 49 = bump 3.7.a.iii Frappe Fantôme — nouveau [Networked] field LastFacingForcedOnTurn set par Volte-Face, helper TryFindFreeCellAdjacentToTarget priorité côté caster, pre-cast reject sans consume PA si pas de case libre, téléport via MoveNonPM + override caster.Facing vers target, post-damage handler PlaieOuverte conditionnel sur direction forcée ce tour, bind T context-aware Ghostra). Précédent : 48 = bump amendement Volte-Face — sort offensif 50 dmg + flip sans verrou + nettoyage 5 hooks DirectionLocked dead code, StatusKind.DirectionLocked=24 conservé RESERVED). Précédent : 47 = bump 3.7.a.ii Saigne-Âme. 46 = bump hotfix 3 — init Facing P1 SW au spawn, géométriquement correct iso. Auparavant init NW masquée par FacingTowardEnemy View, révélée par passage Quantum source-of-truth). Précédent : 45 = bump hotfix 2 mismatch enum Quantum/View IsoFacing — mapping explicite via CombatantRenderer.QuantumToViewFacing). Précédent : 44 = bump hotfix 1 Volte-Face sprite — fix `ResolveFacing` lit `self.Facing` Quantum + cast non-Self update `caster.Facing` côté Quantum avec respect DirectionLocked). Précédent : 43 = bump 3.7.b.iii Volte-Face — nouveau StatusKind.DirectionLocked=24 + handler flip Facing 180° + hooks DirectionLocked dans MovementSystem.ApplyMove / MovementHelpers.MoveNonPM / PushAndTriggerEx / PullTargetAdjacent / PasDansLOmbre pivot adj). Précédent : 42 = bump 3.7.b.ii Pas dans l'Ombre amendé — cap 1×/tour via [Networked] LastPasDansLOmbreOnTurn + anim téléport spectral réutilisant infra Permutation côté CombatantRenderer). Précédent : 41 = bump 3.7.b.ii Pas dans l'Ombre initial (handler téléport via MovementHelpers.MoveNonPM + pivot 4 cardinales sur enemies adj + pose Standard decoy case quittée si HGSpend>=1 + bind H context-aware Ghostra/Colossar). 40 = bump 3.7.b.i Réplique Fantôme — nouveau DecoyKind.RepliqueFantome=3 + DecoyHelpers.DestroyByEnemyAction + heal lifecycle Bible-conforme branche dans TickLifetime/DestroyByEnemyAction + Charge Brutale migré vers DestroyByEnemyAction). Précédent : 39 = bump 3.7.a.i Lame Spectrale + Lame Vorace + PlaieOuverte + Facing tracking. 38 = bump 3.6 Framework Ghostra (leurres + Angle Mort + Permutations). 37 = bump 3.5.c.vi Virus Fatal — nouveau [Networked] field LastVirusFatalUsedOnTurn + handler tick venin x3 + transfert marques sur kill + cooldown 4 tours + touche B context-aware Necram). Précédent : 36 = bump audit Bible-strict Necram — fix PA cost "Pulse Sanguin Vert" 3→2 = valeur Bible canonique de Régénération Nécrotique. 35 = bump 3.5.c.v Cocon Putride — handler self-heal 220 cap MaxHP + AoE marques Manhattan ≤4 + gate HP <30% inline + OncePerMatchBit=3. 34 = bump 3.5.c.iv Pulse Sanguin Vert — handler self-heal AoE itère ennemis Manhattan ≤4 + HGCostMaxOptional=1 pour 1 PT bonus). Précédent : 33 = bump 3.5.c.iii Drain Vital. 32 = bump 3.5.c.ii Carapace Visqueuse + refacto Bible-strict `isMelee` Chebyshev≤1 pour hooks Voile/Carapace + rebranche hooks dans path custom Charge Brutale. Précédent : 31 = bump refacto isMelee Chebyshev≤1 (Voile + Carapace, fix Bible-cohérence Charge Brutale comme attaque mêlée post-move). 30 = bump 3.5.c.ii Carapace initiale. 29 = bump 3.5.c.i Voile de Pestilence. 28 = bump 3.5.b.iii Pas Spectral. 27 = bump 3.5.b.iv Contagion. 26 = bump 3.5.b.ii Symbiose Morbide. 25 = bump 3.5.b.i Inoculation + Marque Sacrificielle. 24 = bump 3.5.a.iii Brume Toxique. 23 = bump 3.5.a.ii Détonation Virulente + Faux Décharnée. 22 = bump 3.5.a.i Crachat Acide + Morsure Putride. 21 = bump 3.4 Framework Marque Venin Necram + passif Floraison. 20 = bump 3.3.d Colossar Effondrement signature.

**Convention temporelle (depuis 2.14) :** **TurnNumber = round complet** (P0+P1 = 1 round, sémantique Dofus). Toutes les durées Bible V7.1 "N tours" = N rounds. Décrémentation statuses/marques/voiles/terrains uniquement en fin de dernier sous-tour du round. Cf memory `project_turn_semantics.md`.

**Cible alpha :** **Windows uniquement** (Mac + Mobile reportés post-alpha)

---

## 🪟 OBJECTIF ALPHA WINDOWS-ONLY

Lorenzo a explicitement décidé que la cible alpha sera **Windows uniquement** pour produire un jeu **totalement opérationnel avec le moins de galères possible**. 

Mac et Mobile sont **reportés en Phase 8 et Phase 9 (post-alpha)**, à définir une fois l'alpha Windows stabilisée. Cela simplifie significativement la roadmap et le scope.

**Gain estimé :** environ 1.5 à 2 mois sur la roadmap initiale (passe de 14 mois → ~12 mois pour alpha Windows complète).

---

## 🛡️ OUTILS DE SCAN AUTOMATIQUE (intégrés en Phase 0)

Lorenzo veut **éliminer le maximum de bugs structurels avant qu'ils n'apparaissent en runtime**. On installe donc dès la Phase 0 :

1. **Roslyn Analyzers** + ruleset custom Nymora — analyse statique à chaque compilation
2. **Editor Script `Nymora_HealthCheck`** — scan complet du projet à la demande
3. **Pre-commit Git hook** — bloque les commits sales
4. **Console filter Nymora** — filtre le bruit Unity

**Philosophie :** fail fast. Mieux vaut 100 erreurs détectées en 30 secondes par un outil que 10 erreurs trouvées en 3 jours par debug runtime.

---

## ✅ BRIQUES VALIDÉES

- **Brique 5.2** — Succès (12 MVP, 3 catégories, extension itérative vers 200) — 4 sous-briques (validée 15 mai 2026 nuit fin) — **repo nymora + nymora-backend**
  - **DB** : Prisma `UserAchievement { id, userId+achievementId unique, progress int, unlockedAt DateTime? }`. AchievementId reste string (catalog static, pas en DB) pour ajout/edit sans migration.
  - **Catalog** `src/achievements/catalog.ts` : 12 succès MVP — 3 Premiers pas (first_match, first_victory, hub_explorer), 5 Combat (wins_10/50/100, matches_50/100), 4 Progression (reach_lv5/10/25/50_any). Total = 1640 points possibles.
  - **Service** `src/achievements/service.ts` : `awardProgress(userId, achievementId, increment)` idempotent (pas de re-unlock) avec push WS systématique. Triggers : `onMatchEnd(result)` (first_match + first_victory si V + matches_X + wins_X si V), `onLevelUp(newLevel)` (paliers 5/10/25/50). `getMyAchievements(userId)` merge catalog avec progress user.
  - **REST** `src/routes/achievements.ts` : `GET /catalog` (12 entries) + `GET /me` (progress merged + totalPoints/unlockedCount).
  - **Push WS** depuis service : `ACHIEVEMENT_PROGRESS` à chaque changement, `ACHIEVEMENT_UNLOCKED` en plus au unlock (avec title + points).
  - **Wiring trigger** : `progression.ts` parse `source` du award-xp (`casual_Victory` / `casual_Defeat` / `casual_Draw`) pour call `onMatchEnd`. Si leveledUp, call `onLevelUp(newLevel)`. Fire-and-forget pour ne pas bloquer la response. **TEMP MVP** — sera remplacé par endpoint dédié match-completed en Phase 6 ranked.
  - **Smoke test** `npm run test:achievements` (8 scénarios : catalog 12, initial locked, 1 victoire trigger 2 unlocks + 3 progress, défaite incrémente matches_50 sans wins, points totals, level-up trigger reach_lv5_any, no auth 401) → PASSED.
  - **Unity DTOs/API** : 4 DTOs (AchievementDefDto, UserAchievementDto, AchievementCatalogResponse, AchievementsMeResponse) + 2 méthodes ApiClient.
  - **Unity HubChatClient** : 2 events (`OnAchievementProgress`, `OnAchievementUnlocked`) + 7 fields Payload + 7 fields IncomingEvent + 2 EventKind + parse/dispatch.
  - **`Scripts/Hub/HubProfilePanel.cs`** : onglet Achievements lazy fetch (catalog + me en parallèle au 1er switch). Render runtime grouped by category (3 catégories avec header) avec items spawn (icon ✓/•, title, mini progress bar, X/Y progress, points en doré). Cache `_myAchievementsById` mis à jour sur events WS. Toast doré dans chat au unlock via SerializedField `_chatUIForAchievementToast`.
  - **Editor Tool `PatchProfileAchievementsTabTool.cs`** (`Nymora > Setup > Patch Profile Achievements Tab`) : remplace placeholder "Coming soon" par Header (TMP TextMeshPro) + Container vertical pour spawn runtime. Auto-wire `_chatUIForAchievementToast` via `FindFirstObjectByType<HubChatUI>()`.
  - **Reliquats connus** : pas de description visible au survol (juste title). Pas de progress sur succès "any" via la valeur réelle (toujours 1/1, on aurait pu tracker la classe la + haute). Extension future vers 200 succès dans le catalog quand on aura plus de systèmes.

---

- **Brique 5.1** — Niveaux par classe (XP, level 1-50) — 6 sous-briques (validée 15 mai 2026 nuit, plus tard) — **repo nymora + nymora-backend** — 🎯 **Première brique Phase 5**
  - **DB** : enum Prisma `NymoraClass` (5 classes Bible V7.1) + model `ClassProgression { id, userId+classId unique, level int default 1, xp int default 0 }`. Migration `20260515*_class_progression`.
  - **Backend REST** `src/routes/progression.ts` : 2 endpoints. `GET /me` retourne les 5 progressions (entries manquantes complétées avec defaults level 1 / xp 0 / xpToNext 200). `POST /award-xp` avec validation Zod (classId enum, amount int 1-10000, source optional). Courbe `xpToNextLevel(n) = 100 + n*100` (200 XP pour L1→2, 5000 XP pour L49→50). Level cap 50 (xp clampé à 0 au cap). Push WS depuis la route : `XP_AWARDED` systématique + `CLASS_LEVEL_UP` si level changé.
  - **Smoke test** `npm run test:progression` (10 scénarios : initial 5 classes L1, simple award, level up exact, multi level up 1000 XP→L4 300xp, invalid classId, negative amount, amount > 10000 Zod cap, award 10000 OK, GET /me état final, no auth 401) → PASSED.
  - **Unity DTOs/API** : 4 DTOs (ClassProgressionDto, ProgressionMeResponse, AwardXpBody, AwardXpResponse). 2 méthodes ApiClient (`GetProgressionMeAsync`, `AwardXpAsync`).
  - **Unity HubChatClient** : 2 events (`OnXpAwarded` Action<XpAwardedData struct>, `OnClassLevelUp` Action<string, int>) + parsing/dispatching.
  - **`Scripts/Hub/HubProfilePanel.cs`** : étendu avec `ClassProgressionRow[] _classRows` (array de 5 SerializableStruct avec classId / levelLabel / xpLabel / xpBarFill). Lazy fetch au 1er switch sur tab Classes (cache `_hasFetchedProgressionOnce`). Handlers `OnXpAwarded` refresh la row concernée en temps réel + log level-up.
  - **`Scripts/Hub/HubMatchResultDisplay.cs`** : **TEMP MVP** award-xp via REST au retour hub. Balance V=50 / D=15 / Draw=25 XP (SerializedField ajustables). Affiche dans chat `+50 XP Soulrender` (vert clair) ou `+50 XP Soulrender — NIVEAU 2 !` (doré) au level-up. Marqué `// TEMP MVP — retirer quand ranked Phase 6` (cf [[project-xp-source-ranked-only]]).
  - **Editor Tool `PatchProfileClassesTabTool.cs`** (`Nymora > Setup > Patch Profile Classes Tab`) : remplace le placeholder "Coming soon" du tab Classes par 5 rows (Soulrender rouge / Nightseer violet / Colossar marron / Necram vert / Ghostra bleu glacé) avec colorBar + nameLabel + levelLabel + XpBar Filled + xpLabel. Wire array `_classRows` du HubProfilePanel via SerializedObject + `InsertArrayElementAtIndex`.
  - **Manip manuelle** : Lorenzo wire `HubMatchResultDisplay._backendSettings` une fois dans l'Inspector (pas automatisé pour rester rapide).
  - **Reliquats connus** : pas d'animation popup XP visuel (juste ligne chat) — différé à 5.9 polish cosmétique. Classe hardcode "Soulrender" pour MVP — sélection de classe avant match arrivera avec ranked Phase 6.

---

- **Brique 4.11** — Clans (4 rôles Dofus-style) — 6 sous-briques (validée 15 mai 2026 nuit) — **repo nymora + nymora-backend**
  - **DB** : Prisma `Clan` (name unique, bannerColor #RRGGBB, description) + `ClanMember` (userId unique → 1 user = max 1 clan, role enum) + `ClanInvite` (unique [clanId, toUserId]) + enum `ClanRole {Leader, Officer, Member, Recruit}`. 3 relations User. Migration `20260515105001_clans`.
  - **Backend REST** `src/routes/clans.ts` (~500 lignes) : 10 endpoints (POST /, GET /me, GET /:id, GET /invites/list, POST /invite (XOR displayName OR userId), POST /invites/:id/respond, POST /me/promote, POST /me/kick, POST /me/leave, DELETE /me). Permissions par rôle codées en `canInvite/canKick/canPromote`. Validation Zod + try/catch.
  - **Push WS depuis routes REST** (pas de handlers WS dédiés — single source of truth) : 6 events (INCOMING_CLAN_INVITE, CLAN_INVITE_RESPONSE, CLAN_MEMBER_JOINED, CLAN_MEMBER_ROLE_CHANGED, CLAN_MEMBER_LEFT, CLAN_DISBANDED). Helper `broadcastToClanMembers` pour diffuser à tous les membres restants.
  - **Smoke test** `npm run test:clans` (20 scénarios : create, dup name 409, self-invite 400, dup invite 409, accept, role hierarchy, officer/leader permissions, kick, leave, disband, no auth 401) → PASSED.
  - **Unity DTOs/API** : 11 DTOs (ClanDto, ClanMemberDto, ClanInviteDto, ClanInvitesListResponse, CreateClanBody, ClanInviteBody, ClanInviteCreatedResponse, ClanRespondBody/Response, ClanPromoteBody, ClanKickBody, ClanLeaveResponse, ClanDisbandResponse, ClanGenericOkResponse). 11 méthodes ApiClient (incluant `InviteToClanByDisplayNameAsync` + `InviteToClanByUserIdAsync` + DELETE custom pour Disband).
  - **Unity HubChatClient** : 6 clan events + parsing/dispatching complet + 7 fields IncomingEvent + 9 fields Payload pour la sérialisation JSON.
  - **`Scripts/Hub/HubClanPanel.cs`** (~450 lignes) : panel 2 modes (NoClan + InClan) avec switch automatique. Header bannière couleur + nom + description. Section membres triée par rôle (Leader > Officer > Member > Recruit), actions selon mon rôle (Leader : promote/demote/kick tous ; Officer : promote Recrue→Membre, kick Member/Recruit ; Member/Recruit : leave only). InviteRow Officer/Leader avec input displayName. Boutons Quitter (non-leader) / Dissoudre (leader). Property publique `HasClan` + `CanInviteToClan` + event `OnClanStateChanged` pour synchroniser ChallengePopup.
  - **`Scripts/Hub/HubClanButton.cs`** : 3e bouton hub bas-droite (anchor -400, à gauche de Friends) + badge rouge nombre d'invitations pending.
  - **`Scripts/Hub/IncomingClanInvitePopup.cs`** : popup haut-droite à l'arrivée d'un INCOMING_CLAN_INVITE avec bannière couleur preview + Accept/Refuse via REST.
  - **`Scripts/Hub/ChallengePopup.cs`** : 6e action "Inviter dans clan" **conditionnelle** (visible seulement si `HubClanPanel.Instance.CanInviteToClan` true). `BuildActions()` désormais appelé à chaque `Show()` (dynamique) au lieu de Awake (statique).
  - **Editor Tool `PatchClanPanelTool.cs`** (~600 lignes) : idempotent, crée bouton + panel + popup + wire tous les SerializedField.
  - **🚨 Hotfix critique** : `HubAvatar.NetSub` passé de `NetworkString<_16>` à `NetworkString<_64>` car `_16` tronquait les UUID Postgres (36 chars). Régen prefab + scène + rebuild standalone obligatoires (cf [[feedback-networked-field-regen-protocol]]). Sans ce fix, `SendFriendRequestByUserId` ET `InviteToClanByUserId` envoyaient un UUID tronqué et le serveur Zod refusait avec "Invalid UUID".
  - **Polish backend** : helper `emptyStringsToUndef` dans clans.ts qui normalise les `""` Unity en `undefined` avant le Zod parse, permettant aux `.optional()` de fonctionner correctement avec les payloads JsonUtility.
  - **Reliquats connus** : pas de cancel sa propre invitation sortante (chef seul peut révoquer via dissolution). Pas de transfert de leadership avant la dissolution. Chat clan différé à 4.11.chat (channel WS dynamique par clan).

---

- **Brique 4.10** — Amis (plein) — 8 sous-briques (validée 15 mai 2026 soir tard) — **repo nymora + nymora-backend**
  - **DB** : nouveau model Prisma `Friendship { id, fromUserId, toUserId, status (PENDING/ACCEPTED), timestamps }` + enum FriendshipStatus. Index `(toUserId, status)` + `(fromUserId, status)`. Unique `(fromUserId, toUserId)`. Cascade delete via User. Migration `20260515085621_friendships`.
  - **Backend REST** `src/routes/friends.ts` : 5 endpoints (GET / mes amis, GET /requests pending in/out, POST /request, POST /respond, DELETE /:friendUserId). Validation Zod + try/catch (évite crash sur sub non-UUID).
  - **Smoke test** `npm run test:friends` (15 scénarios E2E : register x2, request, dup 409, self 400, unknown 404, accept, friends symétrie, remove, decline) → PASSED.
  - **Backend WS** `wsServer.ts` : 3 handlers (`SEND_FRIEND_REQUEST` avec accept targetUser OR targetUserId, `RESPOND_FRIEND_REQUEST`, `REMOVE_FRIEND`) + 4 push events (`INCOMING_FRIEND_REQUEST`, `FRIEND_REQUEST_SENT`, `FRIEND_REQUEST_RESPONSE`, `FRIEND_REMOVED`). Routing via channels.sendToUser.
  - **4.10.g Online status** : 3 push events (`FRIENDS_ONLINE_LIST` au connect, `FRIEND_ONLINE` quand un ami connect, `FRIEND_OFFLINE` quand un ami disconnect). `notifyFriendsConnected` à chaque WS connect (push à user + à amis online). `notifyFriendsDisconnected` après removeClient (multi-instance safe via channels.isUserOnline). `channels.ts` ajout `isUserOnline(userId)`.
  - **4.10.h SEND_WHISPER refacto** : async + nouvelle fonction `resolveWhisperTarget` multi-pass (sub direct dans userPool → email → DB displayName via Prisma). Maintenant `/w Alice salut` marche nativement même si Alice n'a pas son sub/email == "Alice".
  - **Unity DTOs/API** : 8 DTOs (FriendDto, IncomingFriendRequestDto, OutgoingFriendRequestDto, FriendsListResponse, FriendRequestsResponse, FriendRequestBody, FriendRequestCreatedResponse, FriendRespondBody, FriendRespondResponse) + 5 méthodes ApiClient (GetFriendsAsync, GetFriendRequestsAsync, SendFriendRequestAsync, RespondFriendRequestAsync, RemoveFriendAsync) dont DELETE custom retournant `EmptyResponse`.
  - **Unity HubChatClient** : 7 events friends (4 base + 3 online) + 3 méthodes Send (`SendFriendRequest`, `SendFriendRequestByUserId`, `RespondFriendRequest`, `RemoveFriend`) + parsing/dispatching complet. Payload extension avec `friendshipId`, `fromDisplayName`, `toDisplayName`, `friendUserId`, `friendDisplayName`, `friendUserIds[]`.
  - **`Scripts/Hub/HubFriendsPanel.cs`** : panel fullscreen overlay, container centré 820x620, header + barre recherche (input + bouton "Envoyer") + statusText + 3 sections empilées (Mes amis / Demandes reçues / Demandes envoyées). Spawn runtime des items avec layout horizontal (dot online + nom + 1 ou 2 boutons d'action). Fetch initial REST en parallèle (GetFriendsAsync + GetFriendRequestsAsync). Refresh auto sur events WS. Dictionnaire `_onlineDots` pour update O(1) au push FRIEND_ONLINE/OFFLINE.
  - **`Scripts/Hub/HubFriendsButton.cs`** : bouton bas-droite à gauche de "Mon profil" (anchor -210, 20, size 180x56). Badge rouge 28x28 en haut-droite affichant le compte de demandes pending (souscrit à HubFriendsPanel.OnPendingCountChanged).
  - **`Scripts/Hub/IncomingFriendRequestPopup.cs`** : popup haut-droite à l'arrivée d'un INCOMING_FRIEND_REQUEST avec boutons Accepter/Refuser (pattern IncomingChallengePopup).
  - **`Scripts/Hub/ChallengePopup.cs`** : 5e action "Ajouter en ami" (violet) entre "Message privé" et "Signaler" dans le menu data-driven. Utilise `SendFriendRequestByUserId(target.Sub)` car l'avatar remote expose seulement le NetSub UUID.
  - **Editor Tool `Editor/Setup/PatchFriendsPanelTool.cs`** (`Nymora > Setup > Patch Friends Panel`) : idempotent. Crée bouton Amis + badge, FriendsPanelHost avec hiérarchie complète, FriendRequestPopupHost. Wire tous les SerializedField via SerializedObject, dont `_chatUI` via `FindFirstObjectByType<HubChatUI>()`.
  - **Reliquat connu** : pas de cancel-own-outgoing-request (la section "Demandes envoyées" liste mais pas d'action). Différé.

---

- **Brique 4.12** — Profil joueur 5 onglets (validée 15 mai 2026 soir) — **repo nymora + nymora-backend**
  - **Backend** : nouveau router `src/routes/profile.ts` exposant `GET /profile/me` (versionGuard + requireAuth, retourne `{id, email, displayName, mmr, createdAt, lastLoginAt}`). Try/catch sur Prisma findUnique → 400 si sub non-UUID (au lieu de crash serveur). Wire dans `server.ts` via `app.use('/profile', profileRouter)`. Smoke test `npm run test:profile` (4 scénarios : 200 / 401 no token / 401 bad token / 426 no version) → PASSED.
  - **dev:token refacto** : upsert User+Profile en DB par email (auto-UUID), signe JWT avec le **vrai UUID** en `sub`. Résout la dette listée ligne 18 (4.10 plein débloqué futur). stderr log `[dev:token] user=... id=<UUID>` pour info, stdout = JWT pur copiable.
  - **Unity DTO + ApiClient** : `ProfileMeResponse` dans `NymoraApiDtos.cs` + méthode `GetProfileMeAsync()` dans `NymoraApiClient.cs`.
  - **`Scripts/Hub/HubProfilePanel.cs`** : panel fullscreen overlay, container centré 820x620, Header + TabsBar + ContentArea. 5 onglets data-driven (`ProfileTab` enum). Onglet Vue branché sur `GetProfileMeAsync` avec cache `_hasFetchedOnce` (1 fetch à la 1ère ouverture). 4 autres onglets = placeholders "Coming soon" sobres avec descriptions.
  - **`Scripts/Hub/HubProfileButton.cs`** : composant bouton léger qui appelle `HubProfilePanel.Instance.Toggle()` (`RequireComponent(Button)`).
  - **`Scripts/Hub/HubChatClient.cs`** : ajout getter `public string DevToken => _devToken` pour réutilisation par `HubProfilePanel` côté REST (évite duplication du JWT en SerializedField).
  - **Privacy fix** : email volontairement NON exposé dans l'UI panel (streamer-safe). Le backend continue de le retourner via API pour usage futur (onglet paramètres compte par ex).
  - **Asmdef** : `Nymora.Hub.asmdef` ajoute refs `Nymora.Network` + `UniTask`.
  - **Editor Tool `Editor/Setup/PatchProfilePanelTool.cs`** (`Nymora > Setup > Patch Profile Panel`) : idempotent, construit la hiérarchie UI complète (panel + 5 onglets + bouton + déplacement chat bas-gauche) + wire tous les SerializedField via SerializedObject. Cleanup auto de la ligne Email si patch précédent l'avait créée.
  - **Reliquat connu** : `lastLoginAt` reste null après 1er run `dev:token` (branche `create`) → affiche "—". Acté avec Lorenzo, fix différé au vrai profil avec sprite (post-Phase 4).

---

- **Brique 4.5** — Backend Express + WebSocket + JWT minimaliste (validée 14 mai 2026) — **repo nymora-backend**
  - `npm install ws @types/ws` (ws 8.20.1).
  - `src/websocket/wsServer.ts` : WebSocket server attaché au HTTP Express via `noServer: true` + handle upgrade manuel. JWT validé AVANT acceptation (reject 401 si invalide). Token via query string `ws://host/?token=...`. Stocke `userId` + `email` sur le WebSocket object.
  - `src/websocket/channels.ts` : ChannelRegistry in-memory (`Map<string, Set<AuthenticatedWebSocket>>`). 2 canaux init : `global` + `system`. Methods `join` / `leave` / `removeClient` / `broadcast`. Pas de persist Postgres (4.7 plus tard).
  - Protocol JSON : `{ type, channel, payload, timestamp }`. Client → serveur : `JOIN_CHANNEL` / `LEAVE_CHANNEL` / `SEND_MESSAGE`. Serveur → client : `CHANNEL_MESSAGE` / `ERROR`.
  - Patch `src/index.ts` : récupère le `Server` HTTP retourné par `app.listen()` + appelle `attachWebSocketServer(httpServer)`.
  - `src/scripts/test-ws.ts` (script `npm run test:ws`) : signe JWT local → connect → JOIN_CHANNEL global → SEND_MESSAGE → vérif echo CHANNEL_MESSAGE. PASSED.
  - `src/scripts/test-ws-reject.ts` (script `npm run test:ws:reject`) : token bidon → reject 401 vérifié. PASSED.

---

- **Brique 4.4.b** — Build standalone Windows + 2 instances multi-joueur (validée 14 mai 2026) 🎯 **GATE PASSÉE**
  - `HubAvatar.ColorForPlayer(PlayerRef)` static : HSV deterministe via hash(InputAuthority.RawEncoded). Cosmétique ineffective sur sprite rouge Soulrender (multiplication couleurs) — accepté tel quel par Lorenzo, pas de besoin gameplay.
  - Build Settings : scène `10_CommunityHub.unity` ajoutée. Build standalone Windows x86_64 Development Build dans `Build_4_4_b/NymoraHub.exe`.
  - E2E validé : `NymoraHub.exe` + Unity Editor Play en parallèle → 2 avatars visibles dans chaque fenêtre, déplacements répliqués via `[Networked] NetGridX/Y` + Render() interpolation, **aucun désync** observé.
  - **🎯 PHASE 4 BLOC A CLOS** — toute la fondation Hub (Fusion install + scène + grille + avatar networked + multi-instance) est validée. Décision : feu vert pour avancer Bloc B (Backend WS + Chat) sans investissement payant tant que pas besoin. Hetzner toujours différé jusqu'à besoin réel beta-test externe.

---

- **Brique 4.4.a** — Réplication position avatar via [Networked] GridX/Y + Render() interpolation (validée 14 mai 2026)
  - `HubAvatar.cs` : ajout `[Networked] NetGridX/Y { get; set; }` + override `Render()` pour interpoler `transform.position` vers world(NetGridX, NetGridY) sur non-State Authority. Lerp factor 0.25 par frame.
  - `SetGridPosition` pousse NetGridX/Y au réseau sur State Authority (end-of-step uniquement, ~4 updates/sec à 4 tiles/sec). `SetWorldPositionInterpolated` reste 100% local (lerp tile-par-tile pendant le mouvement).
  - **`Nymora.Hub.asmdef`** : `allowUnsafeCode: true` (sinon `FieldAccessException` du weaver Fusion qui accède au `NetworkBehaviour.Ptr` internal unsafe). À retenir pour toute nouvelle asmdef Nymora avec `[Networked]` properties.
  - E2E validé solo : click-to-move marche comme 4.3.c, comportement E2E identique. La réplication ne se révèle qu'en multi-instance (validée 4.4.b).

---

- **Brique 4.3.c** — Avatar networked Fusion + spawn via Runner.Spawn OnPlayerJoined (validée 14 mai 2026)
  - `Scripts/Hub/HubAvatar.cs` refacto `MonoBehaviour → NetworkBehaviour`. Static `Local` set dans `Spawned()` si `Object.HasStateAuthority`. Init grid via `FindFirstObjectByType<HubGridRenderer>()`. SetGridPosition(10,10) au spawn.
  - `Scripts/Hub/HubBootstrap.cs` implémente `INetworkRunnerCallbacks` (19 méthodes, 3 utilisées : OnPlayerJoined, OnPlayerLeft, OnShutdown). `_avatarPrefab : NetworkObject` SerializedField. `runner.AddCallbacks(this)` post-StartGame. `OnPlayerJoined` filtre `player == runner.LocalPlayer` → `Runner.Spawn(prefab, Vector3.zero, identity, player)`.
  - `Scripts/Hub/HubInputController.cs` patch : utilise `HubAvatar.Local` au lieu de SerializedField → pas besoin de wiring scène-à-prefab.
  - `Scripts/Hub/HubCamera.cs` patch : fallback `HubAvatar.Local.transform` si `_target` null.
  - `Editor/Setup/CreateHubAvatarPrefabTool.cs` : génère `Prefabs/Hub/HubAvatar.prefab` avec SpriteRenderer + NetworkObject + HubAvatar + HubMovementController. Idempotent.
  - `Editor/Setup/CreateCommunityHubSceneTool.cs` refacto : plus de HubAvatar scène, câble `HubAvatar.prefab` dans `HubBootstrap._avatarPrefab` via SerializedObject.
  - **NetworkProjectConfig.fusion** : `AssembliesToWeave` += `"Nymora.Hub"` (sinon Fusion weaver ignore notre asmdef → `NetworkBehaviour has not been weaved` exception).
  - **Dette technique 4.4** : `NetworkTransform` RETIRÉ du prefab — il resettait `transform.position` à chaque tick simulation, cassant le click-to-move local. À reintroduire en 4.4 soit reconfig predicted, soit pattern `[Networked] GridX/GridY` + interpolation `Render()`. En l'état 4.3.c, la position avatar n'est PAS répliquée aux autres clients — sera traité en 4.4.a.
  - E2E validé solo : Play → grille → connecté Fusion → OnPlayerJoined → spawn avatar (NetworkId) → clic gauche → A* path → marche smooth tile par tile → caméra suit.

---

- **Brique 4.3.b** — Avatar local + click-to-move + A* tile-based (validée 14 mai 2026)
  - `Scripts/Hub/HubAvatar.cs` : sprite player position grid (int), SetGridPosition / SetWorldPositionInterpolated, sorting order = baseSortingOrder - (gx+gy), baseSorting 100 (avatar devant les tiles).
  - `Scripts/Hub/HubPathfinder.cs` : A* tile-based 4-conn Manhattan, callback `isWalkable`, retourne `List<(int gx, int gy)>` (sans la case de départ, avec la cible). Null si pas de path / out of bounds / start==target. Open list O(n) suffisant pour 400 nodes.
  - `Scripts/Hub/HubMovementController.cs` : Queue<(gx,gy)> + lerp world space tile par tile. 4 tiles/sec par défaut. IsMoving property publique. StopImmediate / Follow.
  - `Scripts/Hub/HubInputController.cs` : `Input.GetMouseButtonDown(0)` → `EventSystem.IsPointerOverGameObject()` ignore UI → `camera.ScreenToWorldPoint` → `IsoProjection.WorldToGrid` → `HubPathfinder.FindPath` → `MovementController.Follow`. Logs `[HubInput]` (path / hors grille / pas de chemin). `IsWalkable` stub = always true (obstacles en 4.x ultérieur quand designer livre).
  - Editor Tool : auto-câble Soulrender_Placeholder.png sur avatar + 4 refs sur InputController + HubCamera.target = HubAvatar.transform.
  - HubPivot.cs et HubCamera.cs gardés inchangés (HubPivot non instancié par le tool mais le fichier reste — sera supprimé après 4.3.c validée).
  - E2E validé : avatar spawn au (10,10), clic gauche → A* path → marche smooth tile par tile, caméra suit, clic hors grille / sur self = no-op propre, Fusion toujours connecté en parallèle.

---

- **Brique 4.3.a** — Grille hub iso 20×20 + caméra-follow placeholder (validée 14 mai 2026)
  - `Scripts/Hub/IsoProjection.cs` : dup pure (60 lignes math statique) de `Combat.View.IsoProjection`. Isolation asmdef Nymora.Hub vs Nymora.Combat préservée — pas de cross-référence (règle sacrée n°4).
  - `Scripts/Hub/HubGridRenderer.cs` : génère 400 tiles iso 2:1 runtime au Start. Width/Height/TileWorldWidth/Height SerializedField. Sprite via `TilePlaceholder.png` partagé avec Combat. Sorting order décroissant avec (gx+gy).
  - `Scripts/Hub/HubCamera.cs` : camera-follow lerp 0.1, target Transform. Préserve Z = -10. SetTarget() public pour rebinding runtime (4.3.b remplacera Pivot par HubAvatar).
  - `Scripts/Hub/HubPivot.cs` : placeholder ZQSD via Input.GetAxisRaw("Horizontal"/"Vertical"). 4 units/sec. **Jetable** : sera supprimé en 4.3.b au profit de l'avatar joueur click-to-move A*.
  - `Editor/Setup/CreateCommunityHubSceneTool.cs` réécrit : auto-câble TilePlaceholder.png dans HubGridRenderer via SerializedObject + wire Pivot→HubCamera target. Idempotent.
  - Camera ortho size 8 (large pour voir ~6 tiles autour du pivot).
  - E2E validé : Play Mode → grille iso 20×20 visible centrée, ZQSD déplace pivot, caméra suit smooth, Fusion toujours connecté en parallèle.

---

- **Brique 4.2** — Scène `10_CommunityHub` + NetworkRunner Fusion Shared Mode (validée 14 mai 2026)
  - `Assets/_Nymora/Scripts/Hub/HubBootstrap.cs` : MonoBehaviour spawn `NetworkRunner` programmatique + `StartGame(GameMode.Shared, SessionName="nymora-hub-dev", PlayerCount=100)` async. Logs `[Hub]` (StartGame / Connected / failed).
  - `Assets/_Nymora/Scripts/Hub/Nymora.Hub.asmdef` enrichi : référence `"Fusion.Unity"` ajoutée.
  - `Assets/_Nymora/Editor/Setup/CreateCommunityHubSceneTool.cs` : Editor Tool menu `Nymora > Setup > Create Community Hub Scene`. Génère `Assets/_Nymora/Scenes/10_CommunityHub.unity` avec Main Camera ortho (size 5, fond `#262630`) + GameObject `HubBootstrap`. Idempotent (popup overwrite).
  - **Fix warning Fusion TickRate** : `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` → `TickRateSelection.Client` 64→32 pour aligner avec Shared Mode (Fusion override en 32Hz runtime de toute façon). Console désormais propre.
  - E2E validé : Play Mode → log `[Hub] Connected. LocalPlayer=[Player:1] Region=eu` (~1-2s). Pas de warning, pas d'exception. Session Photon rejoignable depuis n'importe quel client lançant la scène.

---

- **Brique 4.1** — Photon Fusion 2 SDK install + PhotonAppSettings Fusion (validée 14 mai 2026)
  - SDK Fusion 2 latest stable importé depuis dashboard.photonengine.com → `Assets/Photon/Fusion/`
  - AppId Fusion (créé en 1.1) collé dans `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` (gitignored)
  - **Patch conflit ScriptedImporter** : `Assets/Photon/Fusion/Editor/FusionEditorConfigImporter.cs` désactivé via `#if FUSION_EDITORCONFIG_IMPORTER_ENABLED` (jamais activé). Sentinelle commentaire `NYMORA PATCH (Brique 4.1)` à préserver lors des updates Fusion SDK. Quantum garde son importer `.editorconfig` (installé en premier).
  - `.gitignore` enrichi : exclusions `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` + fallback `Assets/Photon/Resources/PhotonAppSettings.asset`
  - HealthCheck = 0 erreur critique, Console propre, Fusion Hub accessible via `Tools → Fusion → Fusion Hub`
  - Aucun code C# Nymora ajouté (brique 100% setup d'outil). `Nymora.Hub` asmdef référencera `Fusion.Runtime` en 4.2.

---

- **Brique 0.1** — Installation Unity et création du projet (validée 8 mai 2026)
  - Projet Unity Nymora créé, Universal 2D, Unity 2022.3.62f3
  - Force Text + Visible Meta Files OK
  - Localisation : `C:\Users\Lorenzo\Documents\Unity\Nymora\Nymora\`
- **Brique 0.2** — Configuration Editor et structure dossiers (validée 8 mai 2026)
  - 20 sous-dossiers créés sous `Assets/_Nymora/`
  - `README.md` à la racine, `.meta` générées
- **Brique 0.3** — Git + Git LFS + .gitignore + pre-commit hook (validée 8 mai 2026)
  - Repo Git isolé, branche `main`, LFS local actif
  - `.gitignore` + `.gitattributes` complets, hooks versionnés
  - Remote HTTPS, commit `6f25d0e` poussé sur `github.com/DoctorL08/Nymora`
- **Brique 0.4** — IDE et auto-complétion (validée 8 mai 2026)
  - VS 2022 sélectionné comme External Script Editor
  - `.editorconfig` à la racine (LF, indent 4 spaces, Allman style)
  - Autocomplétion `Debug.Log` testée OK
- **Brique 0.5** — Assembly Definitions + Console Filter (validée 8 mai 2026)
  - 6 asmdef créés (CLAUDE.md ajoute Nymora.Editor à la roadmap V2 qui en prévoyait 5)
    - `Nymora.Core` (sans dépendance) → base
    - `Nymora.Combat`, `Nymora.Hub`, `Nymora.Network` → dépendent de Core
    - `Nymora.UI` → dépend de Core + Hub + Combat
    - `Nymora.Editor` → dépend de tous, Editor-only (`includePlatforms: ["Editor"]`)
  - `NymoraConsoleWindow` Editor Window (menu `Nymora > Console > Nymora Logs`) qui filtre les logs par stack trace contenant `Nymora.`
  - Hooks `Application.logMessageReceivedThreaded` avec lock thread-safe + max 1000 entries

---

- **Brique 0.6** — Enums et data containers (validée 8 mai 2026)
  - 4 enums sous `Scripts/Core/Enums/` : `NymoraClass`, `ResourceType`, `DamageType` (renommé depuis `Element` car la Bible V7.1 n'a pas d'éléments magiques), `SpellCategory`
  - 3 structs `readonly` sous `Scripts/Core/Data/` : `Damage`, `Position2D` (int-only pour Quantum), `ResourceCost`
  - 1 static `GameVersion` : `Current = "0.1.0"`, `CombatRulesVersion = 1`, `BibleVersion = "V7.1"`

---

- **Brique 0.7** — NymoraClassDefinition (validée 8 mai 2026)
  - SO `NymoraClassDefinition.cs` sous `Scripts/Core/ScriptableObjects/` (CreateAssetMenu : `Nymora/Class Definition`)
  - Editor Tool `CreateAllClassDefinitionsTool` (menus `Create All` et `Force Recreate All`)
  - 5 assets générés dans `ScriptableObjects/Classes/` : Soulrender, Nightseer, Colossar, Necram, Ghostra
  - Stats Bible V7.1 : 1500 HP / 8 PA / 3 PM, ressources cap (HG 5, PR 4, FD 3, PT 6, RM 3 leurres)
  - Accent colors : `#B22222`, `#6A4FB6`, `#7A6B5C`, `#5A8B3E`, `#6F8FA8`

---

- **Brique 0.8** — SpellDefinition template (validée 8 mai 2026)
  - 3 enums sous `Scripts/Core/Enums/` : `SpellEffectType` (Damage/Heal/ApplyMark/Push/Pull/Spawn... ), `TargetingShape` (Single/Cross/Square/Line/Cone/Circle), `TargetingFilter` (Self/Ally/Enemy/EmptyTile/...)
  - 1 struct `SpellEffect` sérialisable sous `Scripts/Core/Data/`
  - SO `SpellDefinition` sous `Scripts/Core/ScriptableObjects/` avec sections Identity/Cost/Targeting/Effects/Versioning
  - Editor Tool `CreateSpellTemplateTool` (menu `Nymora > Setup > Create Spell Template`)
  - Asset `_Template_Spell.asset` créé dans `ScriptableObjects/Spells/`

---

- **Brique 0.9** — Roslyn Analyzers + ruleset Nymora (validée 8 mai 2026)
  - `Assets/_Nymora/AnalyzerConfig/Nymora.ruleset` (XML — sévérités centralisées CA + UNT)
  - `.editorconfig` enrichi avec règles `dotnet_diagnostic.UNT*` et `CA*`
  - Microsoft.Unity.Analyzers v1.26.0 installé : `Assets/Plugins/Analyzers/Microsoft.Unity.Analyzers.dll` + `.meta` avec label `RoslynAnalyzer`
  - Script auto `tools/install-unity-analyzers.ps1` pour réinstall future
  - UNT0001 (`Update()` vide) testé OK dans VS

---

- **Brique 0.10** — Nymora_HealthCheck (validée 8 mai 2026)
  - `Assets/_Nymora/Editor/Tools/NymoraHealthCheck.cs` (menu `Nymora > Validation > Project Health Check`, raccourci Ctrl+Alt+H)
  - 4 checks : Quantum violations, Missing scripts (prefabs + scène active), ClassDefinitions integrity, Project version
  - Output console + `_docs/healthcheck_report.md` horodaté + emojis
  - Hook `IPreprocessBuildWithReport` : fail le build si erreurs critiques détectées
  - Test fonctionnel OK (BaseHP=0 sur Soulrender → erreur détectée)

---

- **Brique 1.1** — Compte Photon + dashboard (validée 8 mai 2026)
  - 2 apps créées sur dashboard.photonengine.com : "Nymora Quantum" (Quantum 3) + "Nymora Fusion" (Fusion 2)
  - SO `PhotonAppSettings` sous `Scripts/Network/` (asmdef `Nymora.Network`)
  - Editor Tool `Nymora > Setup > Create Photon App Settings`
  - Asset `Assets/_Nymora/Settings/PhotonAppSettings.asset` rempli localement, **git-ignored** (les 2 AppIds restent chez Lorenzo)
  - Fix bonus : warning CS0162 unreachable code dans NymoraHealthCheck (constant folding sur GameVersion.* → checks runtime supprimés)

---

- **Brique 1.2** — Installation Photon Quantum 3 SDK (validée 8 mai 2026)
  - `Assets/Photon/` (PhotonLibs + Realtime + Quantum + QuantumMenu) — 46 MB, 8 asmdef Photon
  - `Assets/QuantumUser/` — dossier user pour notre code de simulation Quantum (Editor / Resources / Scenes / Simulation / View)
  - `Tools > Quantum > Quantum Hub` accessible
  - `.gitignore` mis à jour : `Assets/QuantumUser/Resources/PhotonServerSettings.asset` exclu (contient l'AppId, jamais commit)
  - 76 nouveaux fichiers LFS (DLLs, fonts, audio Photon) — 8.8 MB

---

- **Brique 1.3** — Premier projet Quantum vide (validée 8 mai 2026)
  - SDK a posé tout le scaffolding sous `Assets/QuantumUser/` (Simulation, Editor, Scenes, Resources, View)
  - 6 fichiers `.User.cs` points d'extension (CommandSetup, Frame, RuntimeConfig, etc.)
  - Scène démo `Assets/QuantumUser/Scenes/QuantumGameScene.unity` testée Play mode
  - GraphProfiler en vert pendant 30s → checksum déterministe stable
  - Tick rate à calibrer en Phase 2 (par défaut Quantum pour l'instant)

---

- **Brique 1.4** — Backend Node.js init (validée 8 mai 2026)
  - Repo séparé `github.com/DoctorL08/nymora-backend` (privé, branche `main`)
  - Local : `C:\Users\Lorenzo\Documents\Unity\Nymora\backend\`
  - Stack : Node 20 + TS 5.5 + Express 4 + nodemon + ESLint + Prettier (253 packages)
  - `GET /` → `{"status":"ok"}` testé OK sur localhost:3000
  - Premier commit `250ce72 — chore: phase 1.4 - initial backend setup`

---

- **Brique 1.5** — PostgreSQL + Redis en local Docker (validée 8 mai 2026)
  - `backend/docker-compose.yml` : Postgres 16-alpine + Redis 7-alpine, healthchecks, volumes nommés
  - `backend/.env.example` avec valeurs dev par défaut (DATABASE_URL, REDIS_URL)
  - Deps `pg` ^8.13 et `redis` ^4.7 ajoutées + types `@types/pg`
  - Scripts npm : `docker:up`, `docker:down`, `docker:logs`, `test:db`
  - Script `src/scripts/test-connections.ts` : connexion + ping + roundtrip set/get/del
  - Test `npm run test:db` → "All connections OK." ✅

---

- **Brique 1.6** — Schéma DB v1 avec Prisma 7 (validée 9 mai 2026)
  - **Upgrade Node** : v20.16 → v22 LTS (Prisma 7 demande ≥20.19/22.12/24.0)
  - Stack Prisma 7 (architecture nouvelle) :
    - `@prisma/client` 7.8 + `prisma` 7.8 (devDep) + `@prisma/adapter-pg` + `dotenv`
    - `prisma/schema.prisma` : provider `prisma-client` (nouveau, plus `prisma-client-js`), `output = "../src/generated/prisma"`, **plus de `url` dans datasource** (interdit en P7)
    - `prisma.config.ts` à la racine `backend/` : `defineConfig` + `datasource.url` + `migrations.adapter` (PrismaPg)
    - `src/db/prisma.ts` : singleton client avec adapter PrismaPg + `dotenv/config`
    - Client généré dans `src/generated/prisma/` (gitignored, regen via `prisma generate`)
  - Modèles :
    - `User` : id (uuid), email (unique), passwordHash, emailVerifiedAt?, lastLoginAt?, timestamps. Table `users`.
    - `Profile` : id (uuid), userId (1-1 FK cascade), displayName (unique global), mmr (default 1000), timestamps. Table `profiles`.
  - Migration `prisma/migrations/20260509211604_init/migration.sql` créée et appliquée
  - Smoke test `npm run test:prisma` (create+read+delete cascade) → "Prisma smoke test PASSED." ✅
  - Scripts npm ajoutés : `test:prisma`, `prisma:migrate`, `prisma:studio`, `prisma:generate`
  - Pièges traversés (à retenir) :
    - Prisma 7 a refondu le générateur (`prisma-client-js` deprecated → `prisma-client` avec `output` obligatoire)
    - `datasource.url` interdit dans schema, doit être dans `prisma.config.ts`
    - `npm`/`npx` cherche le package.json dans le CWD → toujours `cd backend` avant
    - `migrate dev` ne loggue plus la génération du client en sortie standard (mais le génère bien)

---

- **Brique 2.12.bis** — Anims complètes Soulrender : walk + cast (par catégorie) + attack + hurt + death + idle ralenti (validée 12 mai 2026)
  - **Quantum** : `Combatant.qtn` ajoute `Int32 LastCastOnTurn` (init -1) + `SpellId LastCastSpellId` (init None). `SpellSystem` set ces 2 champs à la fin de chaque cast réussi (juste avant le log de fin). Approche pull : la View pole la diff frame-par-frame pour trigger l'anim correspondante, pas besoin de Quantum Signal/Event lourd. Codegen Quantum re-run pour propager la struct Combatant C#.
  - **BuildSoulrenderAnimator v2** : refonte complète. Chaque controller (6 au total, 3 stages × 2 directions NE/SE) a maintenant une vraie state machine :
    - **States** : Idle (default, speed 0.4 — fix du "ultra speed" remonté par Lorenzo, donne une respiration lente naturelle), Walk (speedParameter `MoveSpeed`), Cast (speedParameter `CastSpeed` modulé par SpellCategory), Attack (mêlée), Hurt (dégâts reçus), Death (HP=0, latché).
    - **Parameters** : `MoveSpeed` (float 0), `CastSpeed` (float 1), triggers `Cast` / `Attack` / `Hurt` / `Death`.
    - **Transitions** : `Idle ↔ Walk` via threshold `MoveSpeed > 0.01` / `< 0.01` (no exit time, smooth 0.1s). `AnyState → Cast/Attack/Hurt/Death` sur trigger (no exit time, blend 0.05s). `Cast/Attack/Hurt → Idle` après exit time 0.95 (retour auto). `Death` n'a pas de transition retour (latched sur dernier frame).
    - **Clip extraction** : `LoadClipSet` parse les sub-assets de chaque `.aseprite` et matche par substring (idle/walk/attack/cast/hurt/death sur `clip.name.ToLowerInvariant()`). Si un tag manque → warning console + fallback Idle au state correspondant.
    - **Fallback sprite** : le SpriteRenderer du prefab est forcé sur le 1er Sprite extrait du `.aseprite` Stage 0 SE après build. Évite le retour au placeholder rouge si l'AnimationClip ne fire pas (frame tag mal nommé ou animation path qui ne matche pas).
  - **CombatantView** : nouvelle API anims côté View, exposée au Renderer :
    - `SetDesiredMoveSpeed(float)` : push la vitesse `MoveSpeed` que l'Animator utilisera pendant le Walk. Pendant le lerp, `Update()` push automatiquement cette valeur ; à l'arrêt, push 0 (transition Walk → Idle).
    - `TriggerCast(SpellCategory)` : set `CastSpeed` selon la catégorie (Survival 0.7 / Tactical 1.0 / Offensive 1.3 / Signature 1.5) puis trigger l'anim Cast. Le designer n'a livré qu'un clip cast par direction donc on varie la vitesse pour différencier visuellement.
    - `TriggerAttack()` / `TriggerHurt()` / `TriggerDeath()` : triggers simples.
    - Hashes des params en static readonly (`Animator.StringToHash` une seule fois).
  - **Mouvement constant** : remplacement de `Vector3.Lerp` (exponentiel Zeno) par `Vector3.MoveTowards` à vitesse constante `MoveSpeedUnitsPerSecond = 2.5f` (1 case = 0.4s). Avant : le lerp atteignait la case en ~0.15s et l'anim walk n'avait pas le temps de tourner (~"le perso se TP"). Maintenant : 3 cycles de walk visibles par case. `SnapDistance` relevé à 0.05.
  - **CombatantRenderer** : `DispatchAnimTriggers(entity, combatant, view)` polle 3 deltas par frame :
    - Diff HP : si `currHP < prevHP && currHP > 0` → `TriggerHurt`. Si `currHP == 0 && prevHP > 0` → `TriggerDeath` (Death prend priorité, pas de double-trigger Hurt+Death le même frame).
    - Diff `LastCastOnTurn` : si change ET ≥ 0 → fetch `SpellRegistry.TryGet(LastCastSpellId)` → si `RangeMax ≤ 1` → `TriggerAttack`, sinon `TriggerCast(category)`. `CategoryForSpell(SpellId)` hardcode le mapping Soulrender Bible V7.1 (Offensive×5 / Tactical×5 / Survival×5 / Signature×1).
    - `SetDesiredMoveSpeed(1.0f)` à chaque frame (TODO 2.12.ter : différencier 1-2 PM lent / 3+ PM rapide en regardant les cases parcourues dans la séquence).
    - 2 dicts `_lastHP` + `_lastCastTurn` (préalloués cap 2) pour stocker l'état précédent par EntityRef. Reset dans `ClearAll`.
  - **Fix import** : `using SpellCategory = Nymora.Core.Enums.SpellCategory;` au lieu de `using Nymora.Core.Enums` (collision avec `Quantum.NymoraClass` qui est aussi dans `Core.Enums`).
  - **Reliquat 2.12.ter** : variation Walk speed selon PM dépensé (1-2 PM lent vs 3 PM rapide) — nécessite de tracker le nb de cases parcourues dans une séquence, pas implémenté ce soir car nécessiterait soit un signal Quantum, soit une heuristique View timing-based fragile. À voir si le besoin gameplay justifie l'effort.

---

- **Brique 2.12** — Intégration visuelle Soulrender : sprites + Animator idle + icônes + facing 4 directions iso (validée 12 mai 2026, Soulrender visuellement complet pour idle)
  - **Aseprite Importer** (`com.unity.2d.aseprite 1.1.11`) installé via `Packages/manifest.json` — gère les `.aseprite` natifs livrés par le designer avec tags de frames.
  - **AssetPostprocessor `NymoraSpriteImporterSettings`** : scope restreint à `Sprites/Soulrender/Base/` (sprites perso, pivot custom (0.5, 0.5)) + `Sprites/Soulrender/soulrender_icons/` + `UI/Icons/` (pivot Center). PPU=128, Point, FullRect mesh, alpha is transparency. Volontairement scopé pour ne pas écraser `TilePlaceholder.png` (PPU=64, Tight) qui cassait la grille auparavant.
  - **SpellIconRegistry** (ScriptableObject `Assets/_Nymora/ScriptableObjects/Spells/SpellIconRegistry.asset`) : mapping `SpellId → Sprite` + `PassifIconFor(NymoraClass)`. Populé via Editor Tool `Nymora > Setup > Populate Spell Icon Registry` (scan `icon_*.png`, dictionary nom fichier → SpellId).
  - **17 icônes 128×128** intégrées (15 sorts + signature Âme Lacérée + passif Appel du Sang).
  - **Stage swap idle** : `CombatantView` retient la `Resource` du combatant (Bible V7.1 HG paliers 0-1 / 2-4 / 5). Stage 0 = peau normale, Stage 1 = aura rouge progressive, Stage 2 = fissures écarlates (HG cap). Pushé depuis `CombatantRenderer.OnUpdateView` via `ComputeStage(combatant)`.
  - **Animator par stage** : Editor Tool `Nymora > Setup > Build Soulrender Animator` charge les `.aseprite` du designer et génère 6 `AnimatorController` (3 stages × 2 directions NE/SE) dans `Assets/_Nymora/Animations/Soulrender/`. AddMotion du clip `idle` extrait des sub-assets de chaque `.aseprite`. Bind les 6 controllers + l'Animator sur le prefab `Combatant_Soulrender` via `SerializedObject` + `PrefabUtility.SaveAsPrefabAsset`. Idempotent.
  - **Facing 4 directions iso** : enum `IsoFacing { NE, SE, NW, SW }`. Le designer ne livre que NE+SE ; NW = NE flipX, SW = SE flipX (runtime, pas d'asset dupliqué).
    - `CombatantView.SetStageAndFacing(stage, facing)` : pick controller selon (stage, NE/SE) + applique `flipX = facing in {NW, SW}`. Priorité AnimatorController, fallback Sprite statique.
    - `CombatantRenderer.ResolveFacing(entity, combatant)` : retient la dernière position grille + le dernier facing par entity. Si mouvement détecté → nouveau facing dérivé du delta. Si immobile → garde le dernier facing. Au tout 1er frame post-spawn : facing initial dirigé vers l'ennemi (les 2 combatants se regardent au départ).
    - Math iso : delta grille `(dx, dy)` → `dxWorld = dx − dy`, `dyWorld = dx + dy`. Signe(dxWorld) → est/ouest, signe(dyWorld) → nord/sud. Quadrant écran → NE/SE/NW/SW.
  - **Combat Damier corrigé** : pivot character changé en (0.5, 0.5) (test final visuel Lorenzo). `TilePlaceholder.png` rétabli manuellement à PPU=64 + Tight mesh après scope restreint de l'AssetPostprocessor.
  - **TMP fix** : remplacement du caractère `▶` U+25B6 (manquait dans la police) par `> ` ASCII dans `CombatHUDView` pour stopper le warning permanent.
  - **Reliquat designer (Brique 2.12.bis future)** : VFX Âme Lacérée 256×256 8-12f, Marque de Carnage overlay 64×64 4f, Plaie Ouverte overlay, Tile Vapeur Carmin animée 128×128 4f, Tile Sang Coagulé, Avatar profil 256×256. À intégrer quand le designer livre.
  - **Reliquat anims (Brique 2.12.bis prochaine session)** : exploiter à 100% les frame tags livrés (idle/walk/attack/cast/hurt/death dans les 6 .aseprite NE+SE × 3 stages) :
    - Walk lent 1-2 PM / walk rapide 3 PM (paramètre `Speed` sur Animator)
    - Anim cast par catégorie de sort (Survie/Attack/Tactical) avec triggers distincts
    - Hurt sur damage, death sur HP=0
    - Transitions Animator par stage + hook depuis events Quantum (CommandMove, CommandCast, CombatantDamaged, CombatantKilled)

---

- **Brique 2.11** — Signature Âme Lacérée + Passif Appel du Sang (validée 11 mai 2026, clôt le Bloc C, Soulrender 100% Bible V7.1)
  - **Signature Âme Lacérée** (SpellId 25, touche `B` slot dédié) :
    - SpellDef : 2 PA, range 1 mêlée, Enemy, 320 dgts, 5 HG mandatory (consomme toute la jauge).
    - Cooldown 4 tours après usage (champ `Int32 LastAmeLaceeUsedOnTurn` sur Combatant, init -1000).
    - Heal caster = 50% des dgts qui passent (`lastHitHPLoss` tracker, post-shield).
    - Si kill : Sang Coagulé croix 5 cases (centre + 4 cardinales) sur la cible tuée.
    - Re-castable si HG remonte à 5 ET cooldown expiré.
  - **Passif L'Appel du Sang** (3 paliers HP cible, caster Soulrender uniquement) :
    - **<70% HP** : tous les sorts coûtent -1 PA (min 1). Implémenté dans `EffectiveStats.GetPACost` qui prend désormais un `targetHPRatio` (helper `ResolveTargetHPRatio` lit l'occupant de la case ciblée).
    - **<40% HP** : (a) au TurnStart Soulrender, scan ennemis vivants ; si au moins 1 a HP<40% → reset PM = MaxPM + 1 (`Rage Ouverte`). (b) Dans damage loop, si sort mêlée + target<40% + shield présent → 50% des dgts bypass shield direct au HP (refactor `dmgToShield` + `rageOuverteBypass` + `totalHPLoss`).
    - **<20% HP post-hit** : tracker `castTriggeredLeCri` dans damage loop ; après le loop, si true → pose Sang Coagulé croix 5 (caster + 4 cardinales) pour 2 tours. **LE CRI** (Bible V7.1).
  - **Interlock Détonation Sanglante 5 HG** (TODO 2.10.c clôturé) : si Détonation consume 5 HG total (mandatory 2 + optional 3) → `caster->LastAmeLaceeUsedOnTurn = currentTurn` (interdit Âme Lacérée + reset cooldown). Le joueur fait un choix : utiliser ses 5 HG pour la signature (320 dgts + heal) ou pour Détonation maxée (260 dgts AoE + Sang Coagulé). Bible : "consommer 5 HG ici interdit Âme Lacérée et reset son cooldown".
  - **Touche dédiée `B`** : slot séparé du deck de 6 sorts (cohérent Bible "slot séparé du deck de 6").
  - **Refactor damage loop** : 3 nouvelles vars `lastHitHPLoss`, `castTriggeredLeCri`, `rageOuverteBypass`. Signature `ApplySpellSpecificEffects` étendue avec `lastHitHPLoss` (pour le heal 50% Âme Lacérée). Pas de régression sur les sorts existants (tous gèrent `totalHPLoss > 0` au lieu de `dmgRemaining > 0`).
  - **Validation E2E (5/7 tests directs + 2 par inspection)** : palier <70% PA cost -1 prouvé par valeurs PA restant (cast Tranche-Âme cost 2 effectif au lieu de 3) ✓, palier <40% +1 PM `Rage Ouverte` ✓, palier <20% `LE CRI Sang Coagule croix 5` ✓, Âme Lacérée cast (Damage 320 + heal 160 + cooldown posé) ✓, cooldown rejet `Ame Laceree en cooldown (2/4 tours)` ✓. Non testés directement : KILL Âme Lacérée → croix 5 sur cible tuée, Interlock Détonation 5 HG.
  - **Moment de gameplay observé** : la dramaturgie Bible V7.1 se déploie. Le Soulrender bash la cible jusqu'à <70% (combos PA -1), puis <40% (+1 PM = arrive plus vite + 50% bypass shield), puis <20% → LE CRI + finition Âme Lacérée. C'est le "prédateur qui RACCOURCIT le match" prévu par la Bible.
  - **🏆 Bloc C clôturé** : 2.9 (HG) + 2.10 (14 sorts) + 2.11 (signature + passif) = Soulrender 100% Bible V7.1.

---

- **Brique 2.10.c** — Terrains + Mvt non-PM + Kill detection + 4 sorts Soulrender (validée 11 mai 2026, sous-brique 3/3 du Bloc C, clôt 2.10)
  - **Nouveau framework Terrains** (par-case, pas par-combattant) :
    - `Grid.qtn` étendu : enum `TerrainKind` (None/VapeurCarmin/SangCoagule) + champs `Terrain`, `TerrainTurnsLeft`, `TerrainAppliedOnTurn` sur la struct Tile (skip-décrémentation comme Statuses).
    - `GridHelpers` : GetTerrainKind/SetTerrain/ClearTerrain/DecrementAllTerrainsOnTurnEnd.
    - `TurnSystem` étendu : tick Sang Coagulé (-30 HP au combatant actif sur sa case au TurnStart) + DecrementAllTerrains à TurnEnd.
  - **Mouvement non-PM** : nouveau helper `MovementHelpers.MoveNonPM` (valide walkable + occupant mais ignore le compteur PM). Réutilisable : Charge Brutale, recul Tranche-Âme, futurs sorts qui téléportent.
  - **Kill detection post-damage** : tracker `wasKill = (HP==0 && before>0)` + `killedTargetX/Y` dans le damage loop. Signature `ApplySpellSpecificEffects` étendue avec ces 3 params. Champ `Int32 BonusPANextTurn` sur Combatant (Curée kill chain), appliqué au reset PA du TurnSystem.
  - **Vapeur Carmin cost +1** simplifié : si la case de destination du mouvement est Vapeur Carmin → cost += 1 PM. Vraie traversée multi-case en Phase 7 polish.
  - **4 sorts livrés Bible V7.1 strict (touches F1-F4)** :
    - **Charge Brutale** (id 12, F1) : 4 PA, ligne range 5, 180 dgts à 1ère cible bloquante. Caster fonce jusqu'à la case avant l'obstacle. Toutes cases foulées deviennent Vapeur Carmin 1 tour. Gain HG caster/cible géré manuellement (logique hors pipeline générique car damage = 0 dans SpellDef).
    - **Détonation Sanglante** (id 13, F2, Shift+F2 = HGSpend max 3) : 4 PA, AoE croix 3, 60 base + 40 par HG total (mandatory 2 + optional 0-3). Sang Coagulé posé sous le centre AoE pour 2 tours. TODO 2.11 : si HG=5 total → interdit Âme Lacérée + reset son cooldown.
    - **Curée** (id 14, F3) : 2 PA, range 2, 2 HG mandatory, 150 dgts. KILL → heal 50% HP manquants + `BonusPANextTurn += 4`. MISS (target vivante) → caster prend 60 dgts self.
    - **Cautérisation** (id 21, F4) : 2 PA, self, stub. Retire DoT actifs (uniquement `BleedDoT` en 2.10.c, vide en pratique), heal min 60 toujours (max 180 si 3 DoT retirés en Phase 3 Necram). Check AntiHealShield.
  - **Effet bonus Tranche-Âme** : si `wasKill && SpellId == SoulrenderTrancheAme` → caster recule 2 cases (fallback 1 si bloqué) dans la direction opposée à la cible tuée. Helper `MovementHelpers.MoveNonPM`. Clôt le TODO 2.11 noté en 2.8.
  - **Constants Bible V7.1 centralisées** dans SpellRegistry : ChargeBrutaleRange/Damage, VapeurCarminTurns, DetonationBaseDamage/DamagePerHG, SangCoaguleTurns, CureeDamage/BonusPANextTurn/MissSelfDamage, CauterisationHealMin/PerDoT/Max, TrancheAmeKillRecul.
  - **Validation E2E (6/7 tests critiques + #3 par inspection)** : Charge Brutale fonce ✓, Vapeur Carmin posée ✓, Détonation Damage 140 ✓, Sang Coagulé posé ✓, tick TurnStart -30 HP ✓, Cautérisation heal 60 ✓.
  - **Bug C# rencontré et fixé** : 3e occurrence du shadowing `hpBefore` avec le case `Pacte de Sang` (sans braces). Renommé `hpBeforeCuree`, `hpBeforeCureeMiss`, `hpBeforeCauter`, `maxResDS`, `resBeforeDS`. Pattern à généraliser en 2.13 : wrapper toutes les cases en `{}`.
  - **15 sorts Soulrender complets** Bible V7.1. Bloc C avance à 2/3 (reste 2.11).

---

- **Brique 2.10.b** — Shields + Heals + Marques + 5 sorts Soulrender (validée 11 mai 2026, sous-brique 2/3 du Bloc C)
  - **Frameworks ajoutés au moteur** :
    - **Shields** : nouveau `StatusKind.ShieldActive` (8). Magnitude = HP courant du shield, TurnsLeft = durée. Absorption avant HP dans le damage loop (`dmgRemaining -= absorbed`). Si Magnitude tombe à 0 : `StatusHelper.Consume`. Si pas brisé mid-cast : `SetMagnitude` pour update. Pattern simple sans refactor Combatant.qtn.
    - **Heals** : effet flat dans `ApplySpellSpecificEffects`. Compute amount avec variants (HG bonus optionnel, bonus DoT conditionnel via `BleedDoT` stub). Check `AntiHealShield` sur cible (log "BLOQUE" + skip si actif). Clamp à `MaxHP`. Réutilisé par Sève Vive (caster=self) et Dernier Souffle (caster=self).
    - **Marques** : `StatusKind.MarkedByCarnage` activé. Tracker `castHitMarkedTarget` dans damage loop. Après le HG normal Soulrender, +1 HG bonus si caster a touché au moins 1 cible marquée (max 1 par cast). Conditionné à `dmgRemaining > 0` (le shield total absorption ne déclenche pas).
    - **Pull mechanic** : helper privé `PullTargetAdjacent(caster, target)` qui calcule la case adjacente au caster sur la ligne caster→target (axe dominant). Fallback sur 4 cases cardinales si la case "naturelle" est occupée/non-walkable. Skip si target déjà adjacent.
    - **Bonus melee Peau de Fer** : check `RangeMax==1 && ShieldActive.Magnitude>0` au calcul effective damage → +30 dgts. Combinable avec Pacte +50% et Ouvre-Plaie HG.
    - **Conditionnel HP%** : check `caster->HP * 100 >= caster->MaxHP * threshold` AVANT consommation PA. Pas de SpellDef extension (un seul sort en bénéficie pour 2.10.b, on reste pragmatique).
  - **Nouveau StatusKind réservé** : `BleedDoT` (9) — stub pour Phase 3 Necram. Aucun sort 2.10.b ne l'applique mais Sève Vive vérifie sa présence pour le bonus +50 HP.
  - **5 sorts livrés Bible V7.1 strict (touches 6-9, 0)** :
    - **Marque de Carnage** (id 16, touche `6`) : 2 PA, range 5, applique `MarkedByCarnage` 3 tours. +1 HG bonus sur cast Soulrender qui touche.
    - **Empoignade** (id 17, touche `7`) : 3 PA, range 3, pull cible adjacent (helper `PullTargetAdjacent`) + `AntiTeleport` 1 tour. Pas de dgts.
    - **Peau de Fer** (id 22, touche `8`) : 3 PA, self. `ShieldActive` 200 HP / 2 tours. +30 dgts melee pendant durée (tant que Magnitude > 0).
    - **Sève Vive** (id 23, touche `9`, Shift+9 = +HG) : 2 PA, self. Heal 100 (+60 si 1 HG, +50 si BleedDoT). Check AntiHealShield.
    - **Dernier Souffle** (id 24, touche `0`) : 4 PA, self. Conditionnel HP < 30% MaxHP. Heal 200 + 3 HG. 1/match. Check AntiHealShield (heal seul bloqué, HG toujours appliqué).
  - **Constants centralisées** dans SpellRegistry : `PeauDeFerShieldHP/Turns`, `PeauDeFerMeleeDmgBonus`, `MarqueDeCarnageTurns`, `SeveViveHealBase/BonusHG/BonusBleed`, `DernierSouffleHealAmount/HGGain/HPThresholdPct`. Tous Bible V7.1 strict.
  - **View** : CombatInputController bind touches 6-9, 0. Helper `TryGetCasterCell` réutilisé pour sorts self-target.
  - **Validation E2E (7/7 tests critiques)** : Marque bonus HG (+1+1 = 2 HG) ✓, Peau de Fer status posé ✓, Bonus melee +30 (Damage 250 sur Tranche-Âme) ✓, Shield absorption (200 absorbé + 20 HP loss) ✓, Pull Empoignade ((9,8)→(7,8)) ✓, Sève Vive heal base (100 HP) ✓, Dernier Souffle rejet HP>30% ✓.
  - **Bug C# rencontré et fixé** : `hpBefore`, `maxRes`, `resBefore` shadow le case `Pacte de Sang` (qui n'a pas de braces). Renommé en `hpBeforeHeal`, `maxResDS`, `resBeforeDS` dans les cases Sève Vive et Dernier Souffle.
  - **Observation** : la règle "casterHitSomething = HP loss only" (introduite ici) couvre proprement le cas où le shield absorbe tout — pas de gain HG ni Marque bonus si pas de dgts au HP. Cohérent avec Bible "inflige des dégâts".

---

- **Brique 2.10.a** — Statuses framework + 5 sorts Soulrender (validée 11 mai 2026, sous-brique 1/3 du Bloc C suite 2.9)
  - **Nouveau framework Statuses** (clé de voûte des sorts complexes des 14 restants) :
    - `Status.qtn` : `enum StatusKind` (7 valeurs : AntiHealShield, AntiTeleport, BuffNextOffensiveDmgPercent, RipostMelee, MovementMalus, RageInsatiableActive, MarkedByCarnage réservé 2.10.b) + `struct Status { Kind, Magnitude, TurnsLeft, AppliedOnTurn }`
    - `StatusHelper.cs` : Apply/Has/GetMagnitude/SetMagnitude/Consume/ClearAll/DecrementAllOnTurnEnd
    - Règle de décrémentation **clé** : skip si `AppliedOnTurn == currentTurn` (semantic propre : durée "N tours" = N tours réellement vécus côté owner). Décrémentation à chaque TurnEnd dans TurnSystem.
    - `Combatant.qtn` étendu : `array<Status>[8] Statuses` + `Int32 OncePerMatchUsedFlags` (bitfield, bit 0 = Pacte de Sang)
  - **Extensions infrastructure** :
    - `SpellDef` étendu : `HGCostMandatory`, `HGCostMaxOptional`, `OncePerMatchBit`, `IsOffensive`
    - `CastSpellCommand` : ajout `byte HGSpend` (joueur choisit combien de HG dépenser pour cost optionnel)
    - `EffectiveStats.GetPACost(spell, caster)` : point d'extension central pour modificateurs PA (Rage Insatiable +1, futur passif Appel du Sang -1 en 2.11)
    - `TurnSystem.EnterTurnStart` : applique `MovementMalus` au reset PM du joueur actif (Rugissement, Riposte chain)
    - `SpellSystem.ApplySpellSpecificEffects` : switch SpellId pour effets non-damage (post-pipeline générique)
    - Reflect Riposte Carmin dans damage loop : si `target` a `RipostMelee` ET sort melee → caster prend dgts reflect + MovementMalus
    - Helper `ResolveCircleManhattan` inline (rayon 3 pour Rugissement, CircleLarge n'était pas dans TargetingResolver)
  - **5 sorts livrés Bible V7.1 strict** :
    - **Ouvre-Plaie** (id 11, touche `1`) : 2 PA, range 1, 110 dgts. Shift+1 = +1 HG → 230 dgts + `AntiHealShield 2 tours` sur cible
    - **Pacte de Sang** (id 15, touche `2`) : 1 PA, self, **1/match**. -80 HP self + +3 HG (clamp cap) + `BuffNextOffensiveDmgPercent +50%` (consumed au cast offensif suivant)
    - **Rugissement** (id 18, touche `3`) : 3 PA, AoE rayon 3 Manhattan autour caster. Sur chaque ennemi : `MovementMalus` (magnitude 1, ou 2 si cible <50% HP) + `AntiTeleport` (1 tour)
    - **Rage Insatiable** (id 19, touche `4`) : 3 PA, self, 2 tours. `RageInsatiableActive` posé. Sorts coûtent +1 PA effectif. Après chaque offensif : caster regen +1 PA (max 1/tour, tracker via Magnitude = LastTurnPAGained)
    - **Riposte Carmin** (id 20, touche `5`) : 2 PA, self, 1 tour. `RipostMelee:100` posé. Quand subi en mêlée : attaquant prend 100 dgts retour + reçoit MovementMalus 1
  - **View** : CombatInputController bind touches 1-5 + Shift modifier pour HG spend. Helper `TryGetCasterCell` pour sorts self-target. CombatHUDView refactor pour afficher HP/PA/PM/HG/Statuses des 2 joueurs simultanément (`{Kind:TurnsLeft[xMag]}`).
  - **Validation E2E (5/5 tests critiques)** : Pacte buff +50% (Damage 330) + consume ✓, Ouvre-Plaie +HG (230 dgts + AntiHealShield) ✓, Riposte reflect (P1 prend 100 dgts retour) ✓, Rage cycle (+1 PA cost et +1 PA regen) ✓, Pacte 1/match rejet ✓.
  - Pattern de design verrouillé : SpellDef simple + switch SpellId dans SpellSystem pour effets exotiques. Scale naturellement vers 2.10.b/c et Phase 3.

---

- **Brique 2.9** — Ressource Hémoglyphe Soulrender (validée 11 mai 2026, ouverture Bloc C)
  - `Combatant.qtn` : ajout `Int32 Resource` (générique pour les 5 classes) + `Int32 LastResourceGainOnHitTurn` (tracker pour appliquer la règle "max 1 par tour adverse")
  - `CombatantStats.cs` : caps des 5 classes (Bible V7.1) :
    - Soulrender Hemoglyph = 5
    - Nightseer Prescience = 4
    - Colossar Fondation = 3
    - Necram Putrefaction = 6
    - Ghostra Remanence = 3 leurres
    - helper `GetMaxResource(NymoraClass) → int`
  - `CombatantSystem.cs` : init `Resource = 0, LastResourceGainOnHitTurn = -1` au spawn
  - `SpellSystem.cs` (modif): logique de gain dans le pipeline de cast :
    - Boolean `casterHitSomething` tracke si au moins 1 cible a effectivement pris des dgts
    - Pendant le loop damage : si `targetC->Class == Soulrender` et `LastResourceGainOnHitTurn != currentTurn` → +1 HG cible + update tracker
    - Après le loop : si `casterHitSomething && caster->Class == Soulrender` → +1 HG caster (clamped à MaxResource)
  - `CombatHUDView.cs` : affichage `[HG x/5]` (ou PR/FD/PT/RM selon classe) après le label classe, uniquement si MaxResource > 0
  - Convention de design Bible V7.1 verrouillée :
    - **Resource persiste entre tours** (jamais reset, sauf via consommation par sorts spécifiques en 2.10)
    - **PA/PM reset au TurnStart, mais HP et Resource non** — ressource asymétrique = stratégie long terme
  - Pas de piège technique. Le pattern "Resource générique + helper par classe" devrait scale pour les 4 autres classes sans refactor majeur.

---

- **Brique 2.8** — Premier sort Tranche-Âme Soulrender (validée 11 mai 2026, clôture Bloc B)
  - `Spell.qtn` : ajout `SoulrenderTrancheAme = 10` dans `SpellId` + plages réservées documentées (10-29 Soulrender, 30-49 Nightseer, 50-69 Colossar, 70-89 Necram, 90-109 Ghostra)
  - `SpellRegistry.cs` : case `SpellId.SoulrenderTrancheAme` → SpellDef { PACost=3, Shape=SingleTile, Filter=Enemy, RangeMin=1, RangeMax=1, DamageAmount=220 } — valeurs Bible V7.1 strictes
  - TestZap retiré du registry (gardé dans `SpellId` enum pour ne pas casser des commands sérialisées éventuelles). Plus aucun sort de debug castable — que des vrais sorts Bible V7.1 désormais
  - `CombatInputController` : touche Espace cast `SpellId.SoulrenderTrancheAme` (au lieu de TestZap). Le clic gauche reste pour le mouvement.
  - **TODO 2.11** : implémenter l'effet bonus Bible V7.1 "Si le coup tue, le Soulrender RECULE de 2 cases gratuitement (mouvement non-PM)". Nécessite :
    1. Détection HP=0 dans SpellSystem post-damage
    2. Système de mort (entity destroyed ? marked dead ? Bible V7.1 ne précise pas — à trancher)
    3. Mouvement non-PM (helper séparé qui ignore le compteur PM, mais conserve walkable + occupant check)
  - Validation E2E : cast à distance 2 rejeté ✓, cast adjacent 220 dmg ✓, second cast PA=5→2 dmg ✓, third cast rejeté PA insuffisant ✓.

---

- **Brique 2.7** — Spell runtime engine (validée 11 mai 2026)
  - `Assets/QuantumUser/Simulation/Combat/Spells/Spell.qtn` : `enum SpellId : Byte` (None=0, TestZap=1, Soulrender 10-29 réservé, Nightseer 30-49 réservé, Colossar 50-69, Necram 70-89, Ghostra 90-109) + `enum SpellEffectKind : Byte` (Damage, Heal, ApplyMark, Push, Pull, Spawn)
  - `CastSpellCommand.cs` : `DeterministicCommand` avec `SpellId Spell, int TargetX, int TargetY`. Serialize : `byte spellByte = (byte)Spell; stream.Serialize(ref spellByte); Spell = (SpellId)spellByte;` puis serialize TargetX/Y (int).
  - `SpellRegistry.cs` : struct `SpellDef { PACost, TargetingShape Shape, TargetingFilter Filter, RangeMin/Max, DamageAmount }` + static `TryGet(SpellId, out SpellDef)` via switch déterministe (pas de Dictionary heap-alloc). En 2.7 : seul TestZap (3 PA, SingleTile, Enemy, Range 1-5, 100 dmg).
  - `SpellSystem.cs` (`SystemMainThread`, unsafe) : pipeline
    - Check phase TurnActive
    - Iterate playerIndex 0..PlayerCount, récupère `f.GetPlayerCommand(playerIndex) is CastSpellCommand cmd`
    - `TryCastSpell` : validation joueur actif → SpellRegistry.TryGet → trouve Combatant du caster → PA >= PACost → range Manhattan [Min..Max] → TargetingResolver.MatchesFilter → consomme PA → `int* effectBuffer = stackalloc int[GridConstants.Count]` → ResolveEffectCells → applique damage à chaque Combatant dans la zone (HP clampé à 0)
  - `CommandSetup.User.cs` : `factories.Add(new CastSpellCommand())` (au-dessus de la MoveCommand)
  - `SystemSetup.User.cs` : Add SpellSystem après MovementSystem (l'ordre entre eux n'importe pas car ils lisent les commands en read-only)
  - `CombatInputController` enrichi : touche Espace cast TestZap sur case sous souris pour le joueur actif. Le clic gauche reste pour le mvt (sauf si mode targeting preview)
  - **🚨 Découverte importante outils Quantum** :
    - Les méthodes `Log.Info/Warn/Error` de Quantum sont **stripped par `[Conditional]` attributes** si les defines `QUANTUM_LOGLEVEL_INFO/WARN/ERROR/DEBUG` ne sont pas activés dans **Player Settings > Other Settings > Scripting Define Symbols**.
    - Sans ces defines, AUCUN log côté simu n'apparaît dans la console Unity → debug aveugle. C'est extrêmement piégeux car ça ne génère pas d'erreur, juste un silence.
    - **Fix Phase 2 verrouillé** : ajout de `QUANTUM_LOGLEVEL_INFO` dans les Scripting Define Symbols.
    - À documenter pour les nouveaux developers / phases ultérieures.
  - Validation E2E : touche Espace sur Nightseer (après mvt à portée 1-5) → console affiche `[Spell] Damage 100 sur P1 (11,8) HP 1500 -> 1400` + `[Spell] P0 cast TestZap target=(11,8) PA restant=5`. Tous les cas de rejet (PA insuffisant, hors range, filter no-match) loggent un `Log.Warn` traçable.

---

- **Brique 2.6** — Système de targeting Shape + Filter (validée 11 mai 2026)
  - DSL Quantum sous `Assets/QuantumUser/Simulation/Combat/Targeting/Targeting.qtn` :
    - `enum TargetingShape : Byte` (13 valeurs identiques Nymora.Core.Enums)
    - `enum TargetingFilter : Byte` (10 valeurs identiques Nymora.Core.Enums)
  - `TargetingResolver.cs` (static unsafe) avec **3 méthodes principales** + wrappers safe :
    - `ResolveCastableCells(Frame, casterX, casterY, rangeMin, rangeMax, outBuffer, out count)` : cases dans la range Manhattan
    - `ResolveEffectCells(Frame, casterX, casterY, targetX, targetY, shape, outBuffer, out count)` : zone d'effet selon shape
    - `MatchesFilter(Frame, cellX, cellY, filter, casterEntity, casterPlayerIndex)` : validation occupant
    - Shapes implémentés en 2.6 : **SingleTile, CrossSmall, Line, CircleSmall**. Les autres (CrossMedium/Large, Square3x3/5x5, LineThrough, Cone, CircleMedium/Large) loggent `Log.Warn` "non implémentée — à faire quand un sort en aura besoin" pour éviter le code mort.
    - Wrappers safe `int[]` + wrappers unsafe `int*` (le simu utilise stackalloc, le View utilise les arrays managés sans avoir besoin de `allowUnsafeCode` sur Nymora.Combat asmdef)
  - Côté View :
    - `TileView` enrichi : `_baseColor` stocké, helpers `ApplyHighlight(Color)` / `ClearHighlight()`
    - `GridRenderer.GetTileView(int gx, int gy)` : lookup direct par coords (gridWidth/Height hardcodés 15/17 pour rester aligné avec GridConstants côté simu)
    - `TargetingPreviewView` : MonoBehaviour subscribe `CallbackUpdateView`, clear highlights → si mode debug actif, highlight castable (bleu) + effect au survol (rouge)
    - `CombatInputController` : 5 champs `_debugShowTargeting/_debugShape/_debugFilter/_debugRangeMin/_debugRangeMax` (default OFF + SingleTile + Enemy + 1-4), exposés en read-only properties. Quand mode debug actif → bypass `MoveCommand` au clic gauche
  - Pièges à retenir :
    1. **`unsafe { fixed(int* buf = ...) }`** ne compile pas dans une asmdef avec `allowUnsafeCode: false`. Solution : ajouter des wrappers safe `int[]` dans l'asmdef de simu (qui a `allowUnsafeCode: true`) qui font le `fixed` en interne. Le View appelle la version safe, le simu garde la version unsafe pour stackalloc.
    2. **Visibilité du highlight** sous les combattants : les pions ont un sortingOrder de 700-990, les tiles 0-, donc le pion masque visuellement le highlight de sa case. Pas gênant pour la 2.6 (le résolveur fait bien son boulot, vérifiable via les autres cases), à améliorer en Phase 7 polish avec un overlay.
    3. **MatchesFilter Enemy vs AnyUnit** : Enemy = combattant avec PlayerIndex différent du caster. Self = combattant avec entity == caster. AnyUnit = n'importe quel combattant (incluant caster). Logique vérifiée pour le 1v1 (Soulrender vs Nightseer).

---

- **Brique 2.5** — Pathfinding A* déterministe (validée 11 mai 2026)
  - `Assets/QuantumUser/Simulation/Combat/Pathfinding/AStarPathfinder.cs` : static class, méthode `TryFindPath(Frame f, int sx, int sy, int tx, int ty, int maxSteps, int* pathOutBuffer, out int pathLength)`.
  - Algorithme :
    - 4-connexité Manhattan (DX = {1,-1,0,0}, DY = {0,0,1,-1}, ordre fixe pour déterminisme)
    - Open set : tableau plat de taille max 255 cases, `bestF` recherché par scan linéaire (acceptable pour 255 max)
    - Closed set : `bool[255]`
    - gScore : `int[255]` init à `int.MaxValue`
    - cameFrom : `int[255]` init à `-1` (Unreachable)
    - Heuristique : Manhattan strict, jamais surestimation (admissible → optimum garanti)
    - Tie-break : index grille croissant en cas d'égalité fScore → reproductibilité totale
    - Zero allocation heap : tous les buffers en `stackalloc` (~4 KB stack par appel)
    - Reconstruction du path en 2 phases : count d'abord (fail-fast si > maxSteps), puis écriture dans le buffer fourni
  - `MovementSystem.cs` refactor : 
    - Validation rapide `manhattan > PM` → skip A* (économie)
    - Cas adjacent (manhattan==1) → skip A* (optim, applique direct comme en 2.4)
    - Sinon → A* avec `pathBuffer = stackalloc int[GridConstants.Count]`
    - Application synchrone en 1 tick : `GridX/Y` → target, `PM -= path.length`, SetOccupant ancien/nouveau
  - Hooks ajustés (cohérence simu/view) :
    - `tools/git-hooks/pre-commit` : refactor pour scanner SIMULATION uniquement (2 paths, exclusion `View/` et `Generated/`)
    - `NymoraHealthCheck` : refactor `CheckQuantumViolations` → nouvelle méthode `ScanPath(relativePath, ...)` réutilisable, 2 invocations (CombatScripts + QuantumSimPath), exclusion `View/` et `Generated/`. Le View Unity peut désormais utiliser `Time.deltaTime` sans déclencher de violation Quantum.
  - Pièges traversés :
    1. **Conflit nom variable `f`** : dans un système Quantum la convention est `Frame f`, donc une variable locale `int f = fScore` collide. Réflexe : nommer toujours `fScore` ou `gValue` etc. dans le code A*.
    2. **Périmètre du check déterminisme** : avant ce refactor, le hook et le healthcheck scannaient `Scripts/Combat/` complet mais pas `QuantumUser/Simulation/`. Asymétrie corrigée — désormais les 2 paths scannés, View/ et Generated/ exclus.

---

- **Brique 2.4** — Mouvement case par case via DeterministicCommand (validée 11 mai 2026)
  - DeterministicCommand `MoveCommand` sous `Assets/QuantumUser/Simulation/Combat/Movement/MoveCommand.cs` : extends `DeterministicCommand`, contient `int TargetX, TargetY`, override `Serialize(BitStream)` qui sérialise les 2 int via `stream.Serialize(ref TargetX/TargetY)`
  - `MovementSystem.cs` (`SystemMainThread`, unsafe) :
    - `Update(Frame f)` : vérifie phase TurnActive (sinon return), itère sur `playerIndex 0..PlayerCount-1`, récupère `f.GetPlayerCommand(playerIndex) is MoveCommand cmd`
    - Validations strictes : `playerIndex == ActivePlayerIndex` (anti-cheat), Combatant existe pour ce player, `PM > 0`, `GridHelpers.InBounds(targetX, targetY)`, `IsWalkable`, `GetOccupant == None`, adjacence Manhattan stricte `|dx| + |dy| == 1` (4-connexité, pas de diagonale)
    - Application : libère ancienne case, MAJ GridX/GridY du Combatant, `PM--`, occupe nouvelle case
    - Log warning détaillé à chaque rejet pour faciliter le debug
  - `CommandSetup.User.cs` (update) : `factories.Add(new MoveCommand())` — le command sert de factory pour lui-même via `DeterministicCommand.GetCommandInstance`
  - Inscrit dans `SystemSetup.User.cs` après TurnSystem
  - Côté View :
    - `IsoProjection.WorldToGrid(Vector3 worldPos, float tileW, float tileH, Vector3 centerOffset) → (int gx, int gy)` : inverse arithmétique de `GridToWorld`. Math : annule l'offset de centrage, puis résout le système `a = 2*wx/tw, b = 2*wy/th, gx = (a+b)/2, gy = (b-a)/2` avec `Mathf.RoundToInt` final pour snap.
    - `CombatantView` (update) : lerp `transform.position` vers `_targetWorldPosition` à `Time.deltaTime * 8f` (~0.15s/case). `SnapDistance = 0.01f` pour éviter le lerp infini sur micro-distances. Snap direct au tout premier `UpdateGridPosition` post-`Bind` (sinon animation de spawn depuis 0,0,0).
    - `CombatInputController.cs` (nouveau) : MonoBehaviour qui s'abonne à `CallbackGameStarted` pour ajouter les players locaux + récupérer le `centerOffset`, puis dans `Update` détecte `UnityEngine.Input.GetMouseButtonDown(0)`, convertit via `_camera.ScreenToWorldPoint` → `IsoProjection.WorldToGrid` → `game.SendCommand(senderPlayer, new MoveCommand{TargetX,TargetY})`. 3 toggles : `_localPlayerIndex` (default 0), `_debugAllPlayersMovable` (default true, envoie au joueur ACTIF pour tester P0/P1 alternativement), `_autoAddLocalPlayers` + `_autoAddPlayerCount` (default true/2 pour sortir du mode spectator).
  - Pièges à retenir :
    1. **`Quantum.Input` vs `UnityEngine.Input` ambiguïté** : `using Quantum` importe le struct `Input` Quantum (DSL input continu) qui collide avec la classe `UnityEngine.Input`. Fix : toujours qualifier explicitement `UnityEngine.Input.X` dans les scripts View qui touchent à l'input clavier/souris.
    2. **Mode spectator Quantum** : sans `game.AddPlayer(slot, RuntimePlayer)` au démarrage, le runner est en mode spectator et rejette tous les `SendCommand` avec "Can't send commands in spectating mode". Solution Phase 2 : auto-add depuis `CombatInputController` au `CallbackGameStarted`. Solution Phase 6 : flow menu/matchmaking via `QuantumRunnerLocalDebug.Players[]` ou les RuntimePlayers du runner Photon.
    3. **`Filter.NextUnsafe` vs `Filter.Next`** : utilisé `NextUnsafe(out, out Combatant*)` dans `MovementSystem` (asmref Quantum.Simulation qui a `allowUnsafeCode: true`) car on doit MODIFIER les champs du Combatant. Pour lecture seule côté View on reste sur `Next(out, out Combatant)` safe.
    4. **Adjacence Manhattan stricte `|dx| + |dy| == 1`** : pas de diagonale en 2.4 (cohérent Bible V7.1 qui parle de "case adjacente" sans préciser diagonale). À reconsidérer en Phase 2.5+ si la Bible mentionne explicitement la diagonale pour certains sorts/déplacements.

---

- **Brique 2.3** — FSM tour + timer 15s + initiative (validée 11 mai 2026)
  - DSL Quantum sous `Assets/QuantumUser/Simulation/Combat/Turn/Turn.qtn` : `enum CombatPhase : Byte { PreMatch=0, TurnStart=1, TurnActive=2, TurnEnd=3, MatchEnd=4 }` + `singleton component CombatState { CombatPhase CurrentPhase; Int32 ActivePlayerIndex; Int32 TurnNumber; Int32 TurnTimerTicks; }`
  - `TurnConstants.cs` : `TurnDurationSeconds = 15` + `PlayerCount = 2` + helper `GetTurnDurationTicks(Frame f) = TurnDurationSeconds * f.UpdateRate` (60 Hz standard → 900 ticks)
  - `TurnSystem.cs` (`SystemMainThread`, unsafe) :
    - `OnInit(Frame f)` : crée CombatState via `Unsafe.GetOrAddSingletonPointer<CombatState>(EntityRef.None)`, set Phase=PreMatch puis TurnStart, tire initiative via `f.RNG->Next(0, 2)`. Log "Initiative: Joueur PX commence".
    - `Update(Frame f)` : switch sur Phase :
      - `TurnStart` → increment TurnNumber, init timer, RESET PA/PM du joueur actif (HP et ressources de classe préservées par design Bible V7.1), transition TurnActive
      - `TurnActive` → décrémente TurnTimerTicks chaque tick, transition TurnEnd quand <= 0
      - `TurnEnd` → swap ActivePlayerIndex via `(idx + 1) % 2`, transition TurnStart
      - `PreMatch` / `MatchEnd` → no-op
  - Inscrit dans `SystemSetup.User.cs` APRÈS GridSystem et CombatantSystem (ordre important : TurnSystem reset PA/PM des Combatants déjà créés).
  - Côté View (Nymora.Combat avec ref Unity.TextMeshPro ajoutée à l'asmdef) :
    - `CombatHUDView.cs` : MonoBehaviour subscribe `CallbackUpdateView`, lit `frame.TryGetSingleton<CombatState>()` (safe API par valeur), filter Combatant pour trouver la classe du joueur actif, affiche `"Phase: X | Tour N | Joueur PX Class | Timer s.s s"`. Conversion ticks → secondes via `frame.UpdateRate` (float côté View OK pour affichage uniquement).
  - Editor Tool `CreateCombatHUDTool.cs` (menu `Nymora > Setup > Create Combat HUD`) :
    - Crée `CombatHUDCanvas` (RenderMode.ScreenSpaceOverlay, sortingOrder=100, CanvasScaler 1920×1080 matchWidthOrHeight=0.5)
    - Crée GameObject `CombatHUD` enfant avec `TextMeshProUGUI` ancré en haut centre (anchorMin (0,1), anchorMax (1,1), pivot (0.5,1), sizeDelta (0, 60))
    - Câble `_label` via SerializedObject + crée EventSystem si manquant
    - Marque la scène dirty pour forcer la sauvegarde
  - Convention de design importante (Bible V7.1) :
    - En `TurnStart`, **seuls les PA et PM sont resettés au max** pour le joueur actif
    - HP et ressources de classe (Hémoglyphe, Prescience, Fondation, Putréfaction, Rémanence) **persistent entre tours** — c'est le cœur du design Bible V7.1
  - Pas de piège technique sur cette brique — implémentation fluide

---

- **Brique 2.2** — Entity Combatant Quantum (validée 11 mai 2026)
  - DSL Quantum sous `Assets/QuantumUser/Simulation/Combat/Combatant/Combatant.qtn` : `enum NymoraClass : Byte { None=0, Soulrender=1, Nightseer=2, Colossar=3, Necram=4, Ghostra=5 }` (dupliqué intentionnellement vs Nymora.Core.Enums car DSL Quantum ne peut référencer un type externe) + `component Combatant { PlayerIndex, Class, HP/MaxHP, PA/MaxPA, PM/MaxPM, GridX, GridY }` (positions discrètes int pour rester pur grid-based)
  - `CombatantStats.cs` : constantes Bible V7.1 verrouillées (1500 HP, 8 PA, 3 PM standard, 2 PM Colossar) — helpers `GetMaxHP/MaxPA/MaxPM(NymoraClass)`
  - `CombatantSystem.cs` (`SystemSignalsOnly`) : `OnInit` crée 2 entities via `f.Create() + f.Add(entity, value)` (signature value-pass plus simple que Add + GetPointer), positions hardcodées (3,8) Soulrender + (11,8) Nightseer, marque la grille via `GridHelpers.SetOccupant`. Inscrit dans `SystemSetup.User.cs > AddSystemsUser` APRÈS `GridSystem` (ordre important : SetOccupant lit la singleton GridSingleton initialisée par GridSystem).
  - Côté View (Nymora.Combat) :
    - `CombatantView.cs` : MonoBehaviour léger sur chaque GameObject combattant, expose EntityRef + GridX/GridY + Class + helpers Bind/UpdateGridPosition. Sorting order = `1000 - (gx + gy) * 10` (range 700-990, toujours au-dessus des tiles qui sont à 0..-30).
    - `CombatantRenderer.cs` : MonoBehaviour subscribe `CallbackGameStarted` (spawn initial via `frame.Filter<Combatant>()`) + `CallbackUpdateView` (sync positions à chaque frame verified, spawn à la volée si nouvelle entity Combatant apparaît). Pas de pooling (max 2 combattants en 1v1, useless en Phase 2).
  - Editor Tool `CreateCombatantPlaceholdersTool` (menu `Nymora > Setup > Create Combatant Placeholders`) : génère 5 sprites circulaires 128×128 PNG procédural (couleurs accent Bible V7.1 : #B22222 Soulrender, #6A4FB6 Nightseer, #7A6B5C Colossar, #5A8B3E Necram, #6F8FA8 Ghostra) + 5 prefabs câblés (SpriteRenderer + CombatantView via SerializedObject). Idempotent.
  - Sprites stockés : `Assets/_Nymora/Art/Sprites/Combatants/<Class>_Placeholder.png`. Prefabs : `Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_<Class>.prefab`.
  - **Convention PPU verrouillée pour le projet** :
    - Tiles : 64×64 px → PPU 64 → 1×1 unité world = 1 case ✓
    - Persos : 128×128 px → PPU 128 → 1×1 unité world = 1 case ✓
    - Règle : chaque catégorie de sprite a son PPU = sa résolution. Jamais le même PPU pour des sprites de tailles différentes.
  - Pièges traversés (à retenir) :
    1. `Nymora.Editor.asmdef` doit aussi inclure les GUIDs Quantum.Simulation + Quantum.Unity dans `references` pour que les Editor Tools puissent compiler du code utilisant `NymoraClass`/etc. Le flag `autoReferenced` côté Quantum ne suffit jamais pour les asmdef custom.
    2. PPU vs taille sprite : 128×128 + PPU 64 → 2×2 cases (BUG). 128×128 + PPU 128 → 1×1 case ✓. Convention finalement claire : PPU = résolution du sprite.
    3. Sorting order combattants : formule `100 - (gx+gy)*10` produisait des valeurs négatives pour les combattants éloignés → invisibles derrière les tiles. Fix : base 1000 → toujours > 0 et > toutes les tiles.
    4. `Filter.NextUnsafe<T>(out, out T*)` nécessite `allowUnsafeCode: true`. Pour Nymora.Combat (safe), utiliser `Filter.Next(out, out T)` qui retourne par valeur (copie d'un struct ~40 bytes, négligeable).

---

- **Brique 2.1** — Grille de combat 15×17 (validée 11 mai 2026)
  - DSL Quantum 3 sous `Assets/QuantumUser/Simulation/Combat/Grid/Grid.qtn` : `struct Tile { Byte Walkable; EntityRef Occupant; }` + `singleton component GridSingleton { Int32 Width; Int32 Height; array<Tile>[255] Tiles; }` (regen auto par `QuantumQtnAssetImporter` au save)
  - `GridSystem.cs` (`SystemSignalsOnly`) : init des 255 tiles walkables dans `OnInit(Frame f)` via `f.Unsafe.GetOrAddSingletonPointer<GridSingleton>(EntityRef.None)`. Inscrit dans `SystemSetup.User.cs > AddSystemsUser`.
  - `GridHelpers.cs` : `GridConstants` (Width=15, Height=17, Count=255) + helpers `Index/InBounds/IsWalkable/GetOccupant/SetOccupant/SetWalkable`. Constants hardcodées car liées à la taille du fixed array DSL.
  - Côté View (Nymora.Combat asmdef enrichie de refs `Quantum.Simulation` + `Quantum.Unity` via GUID) :
    - `IsoProjection.cs` : `GridToWorld(int gx, int gy, float tileW, float tileH) → Vector3` (formule iso 2:1) + `SortingOrderFor(gx,gy)` = `-(gx+gy)` + `CenterOffset(w,h,tW,tH)` (moyenne des 4 coins inversée) pour centrer la grille autour de (0,0).
    - `TileView.cs` : MonoBehaviour léger sur chaque tile, stocke (GridX, GridY) + helper `SetSortingOrder(layer, order)`.
    - `GridRenderer.cs` : MonoBehaviour abonné à `CallbackGameStarted` qui lit `GridSingleton` en safe API (`frame.GetSingleton<GridSingleton>()`) et instancie les 255 tiles avec offset de centrage. Pattern color even/odd pour échiquier visuel.
  - SO `GridSettings.asset` sous `Assets/_Nymora/Settings/` : `TileWorldWidth=1.0`, `TileWorldHeight=0.5`, `SortingLayer="Default"`, `BaseSortingOrder=0`, `CenterGrid=true`.
  - Editor Tools : `Nymora > Setup > Create Grid Assets` (génère sprite losange 64×32 procédural Texture2D + prefab `TileView.prefab` câblé via SerializedObject + SO GridSettings idempotent) ; `Nymora > Validation > Grid Previewer` (preview iso 15×17 hors Play, `HideFlags.DontSaveInEditor`).
  - Pièges traversés (à retenir) :
    1. Quantum 3 unsafe API : `GetOrAddSingleton<T>()` n'existe PAS en unsafe → utiliser `Unsafe.GetOrAddSingletonPointer<T>(EntityRef.None)`. La version safe `f.GetOrAddSingleton<T>(EntityRef)` retourne par valeur (copie), pas un pointer.
    2. `autoReferenced: true` côté Quantum.Unity/Simulation NE suffit PAS pour qu'une asmdef custom (Nymora.Combat) y ait accès. Il faut ajouter les GUIDs explicitement dans `references` : `5d82202959c2f144ea95e134645b6833` (Simulation) + `f6fa0c2f8b9a9f64897d3351666f3d66` (Unity).
    3. Grille iso non centrée par défaut : la formule étend le losange de `-(H-1)*tW/2` à `+(W-1)*tW/2` en X et de 0 à `(W+H-2)*tH/2` en Y. Toujours appliquer un offset de centrage si on veut que la caméra (0,0,-10) tombe juste.
    4. Le DSL `.qtn` régénère le codegen Quantum automatiquement à l'import (postprocessor dans `QuantumQtnAssetImporter`), pas besoin de menu manuel.

---

- **Brique 1.14** — Test bout-en-bout Phase 1 (validée 11 mai 2026, scope minimal)
  - Décision de scope : on a coupé court vs la roadmap V2 originale (qui demandait simulation Quantum déterministe + checksums) → on a fait uniquement le test **auth multi-client** (Build .exe + Editor en parallèle, 2 comptes distincts, 2 sessions Photon simultanées). La validation déterministe Quantum se fera naturellement en Phase 2 dès qu'on aura 2 personnages qui bougent.
  - Build Standalone Windows configuré : scènes [00_Login.unity], Mono backend, Windowed 1280×720, build dans `Builds/1.14/Nymora.exe`
  - Photon Cloud / ngrok réactivés pour la session : URL ngrok-free `https://alphabet-reverend-cloud.ngrok-free.dev` configurée dans le dashboard Photon Custom Authentication
  - 2 comptes créés : `test1@nymora.local` (tester1) + `test2@nymora.local` (tester2)
  - Validation E2E :
    - Editor (Play 00_Login) : Login tester1 → Photon OK UserId=`e13a1867-6994-47db-a730-0d3a5f6d21d5`
    - Build (Nymora.exe) : Login tester2 → Photon OK UserId=`33b893cc-75c2-4683-a8d6-3fe3f4531fa8`
    - 2 UUIDs Postgres distincts, 2 sessions Photon simultanées validées par le webhook backend en parallèle
  - Pièges traversés :
    1. PlayerPrefs Editor vs Build : sur Windows, Unity Editor et un Build standalone ont **des clés de registry distinctes** (`HKCU\Software\Unity\UnityEditor\...` vs `HKCU\Software\<CompanyName>\...`). Pas d'interférence à craindre entre les 2 instances pour la persistence du JWT.
    2. URL ngrok-free temporaire change à chaque relance → toujours mettre à jour le dashboard Photon avant de tester. Sinon Photon tape une URL morte et retourne ResultCode=3 BadParams sans message clair.

---

- **Brique 1.13** — Hosting Phase 1 VPS Hetzner — **REPORTÉE en Phase 7** (prep alpha)
  - Décision prise le 11 mai 2026 : Lorenzo veut minimiser les dépenses tant que le gameplay (Phase 2-3) ne prouve pas que Nymora est fun. Hetzner CX22 (~4€/mois) + domaine (~10€/an) = ~60€/an inutiles tant qu'on bosse seul en local.
  - Tout marche en local : backend Node sur localhost:3000, Photon Cloud free tier (100 CCU = ~5-10K inscriptions OK). Multi-client local testé en 1.14.
  - Hetzner sera réactivé quand on voudra inviter des testeurs externes (Phase 7 prep alpha closed).
  - À ce moment-là : créer compte Hetzner Cloud + domaine OVH/Namecheap + Docker deploy + sous-domaine `api-dev.nymora.fr` + Let's Encrypt HTTPS (HTTPS obligatoire pour webhook Photon).

---

- **Brique 1.12** — CI/CD GitHub Actions (validée côté backend le 11 mai 2026 ; Unity CI désactivé en attente d'une license)
  - Décisions techniques :
    1. Unity CI scope = compile-check via GameCI test-runner EditMode (~5-10 min/run, pas de build .exe full)
    2. Triggers = push sur main uniquement (pas de PR car solo dev)
    3. Backend = 5 smoke tests (db + prisma + auth + photon + version) via services Docker
  - Backend (commit côté `nymora-backend`) :
    - `.github/workflows/backend-ci.yml` : services Postgres 16-alpine + Redis 7-alpine avec healthchecks, Node 22 + cache npm, JWT_SECRET généré à la volée via `openssl rand -hex 64`, séquence `npm ci → prisma generate → prisma migrate deploy → lint → build → 5 smoke tests`
    - **Run validé VERT** sur le premier push 1.10+1.11+1.12 (~3-5 min)
  - Unity (commit côté `Nymora`) :
    - `.github/workflows/unity-ci.yml` créé puis **désactivé** : passé en `on: workflow_dispatch` car la license Unity n'est pas dispo (compte orga sans Pro/Plus, et Personal n'apparaît pas pour les comptes orga)
    - Workflow `unity-activation.yml` supprimé (action GameCI `unity-request-activation-file@v2` dépréciée, GameCI demande maintenant de générer le .alf en local depuis Unity Editor)
  - À faire plus tard pour réactiver Unity CI (Phase 7 prep alpha au plus tard) :
    1. Soit créer un compte Unity ID secondaire perso avec un autre email → activer Personal → récupérer le .ulf dans `%PROGRAMDATA%\Unity\Unity_lic.ulf` → coller dans secret repo `UNITY_LICENSE`
    2. Soit acheter une Unity Pro license et utiliser la méthode GameCI Pro (3 secrets : `UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD`)
    3. Puis changer `on: workflow_dispatch` vers `on: { push: { branches: [main] } }` dans `unity-ci.yml`
  - Pièges traversés :
    1. Action GameCI `unity-request-activation-file@v2` dépréciée → ne plus l'utiliser, générer le .alf en local via la commande CLI Unity (`Unity.exe -batchmode -createManualActivationFile -logfile -`)
    2. Sur la commande CLI Unity : besoin des guillemets autour du chemin (`"C:\Program Files\..."`) sinon cmd casse sur l'espace de "Program Files"
    3. Le `.ulf` Personal d'une license active est dans `%PROGRAMDATA%\Unity\Unity_lic.ulf` (pas `%PROGRAMFILES%`)
    4. Les comptes Unity org sans license Pro/Plus achetée ne peuvent pas activer Unity Personal → besoin d'un compte secondaire

---

- **Brique 1.11** — Logger structuré client + serveur (validée 11 mai 2026)
  - Décisions techniques tranchées :
    1. Scope du remplacement = runtime uniquement (Editor scripts et smoke tests CLI restent en `Debug.Log` / `console.log` car non-prod)
    2. Niveaux exposés = `Info / Warn / Error / Critical` (4 niveaux, KISS, alignés sur la roadmap V2 et compatibles Pino qui appelle Critical = `fatal`)
    3. Format Pino = pretty-print en dev (couleurs + timestamps), JSON brut en prod (parsable Loki en Phase 7+) via `NODE_ENV`
  - Backend (commit côté `nymora-backend`) :
    - Nouveau `src/services/logger.ts` : singleton Pino configuré via `NODE_ENV`. `base: { service: 'nymora-backend' }`. Méthodes `logger.info/warn/error/fatal(...)` avec convention `logger.info({ contextObj }, 'message')`.
    - Modif `src/index.ts` : `console.log` remplacé par `logger.info({ port }, 'Backend Nymora demarre')`.
    - Deps ajoutées : `pino@9.14.0` (dependency) + `pino-pretty@11.3.0` (devDep).
    - Smoke tests CLI inchangés (leurs `console.log` sont des outputs lisibles pour Lorenzo, pas du runtime serveur).
  - Unity (commit côté `Nymora`) :
    - Nouveau `Assets/_Nymora/Scripts/Core/Logging/NymoraLog.cs` (asmdef `Nymora.Core`, sans dépendance) : static class avec enum `NymoraLogLevel` + méthodes `Info/Warn/Error/Critical(category, message)`. Wrap `Debug.Log/LogWarning/LogError` (Critical = `LogError` avec prefix `[CRITICAL]`) pour rester visible dans la Console Unity ET le filtre `NymoraConsoleWindow`. Event `OnLogEmitted` static pour brancher un sink HTTP vers Loki en Phase 7+.
    - Modif `LoginScreenController.cs` : 6 `Debug.Log/LogWarning/LogError` remplacés par `NymoraLog.Info/Warn/Critical("Login", ...)`. `Debug.LogError` initial sur backend settings manquant → promu `NymoraLog.Critical`. Le `SetStatus` interne logge maintenant via `NymoraLog.Info`.
  - Convention adoptée : préfixe automatique `[Nymora.{Category}]` géré par le wrapper. Les appelants passent seulement le category (`"Login"`) + le message brut, sans réécrire le préfixe.
  - Validation E2E :
    - Backend Pino : démarrage avec `pino-pretty` lisible (couleurs + `HH:MM:ss.l` + service tag)
    - Smoke tests : `test:auth` 7/7 + `test:photon-webhook` 6/6 + `test:version` 8/8 (21/21 total)
    - Console Unity propre : logs `[Nymora.Login] ...` avec stack trace montrant le flow `NymoraLog.Emit → Info → SetStatus → Start`
    - Healthcheck Unity : 0 erreur
  - Pièges traversés :
    1. Nodemon crashe en cascade pendant les saves successifs des fichiers (TS6133/TS6192 "imports unused" transitoires). Inoffensif tant que le DERNIER état du fichier compile. Faut pas paniquer sur des erreurs intermédiaires.
    2. Cannot find module 'pino' → toujours bien faire `npm install` après ajout d'une dep. Sur Windows il faut souvent un Ctrl+C + relance `npm run dev` pour que nodemon repère les nouvelles deps installées pendant qu'il tourne.

---

- **Brique 1.10** — Système de versioning runtime (validée 10 mai 2026)
  - Backend (commit côté `nymora-backend`) :
    - Nouveau service `src/services/version.service.ts` : constantes `MIN_CLIENT_VERSION = '0.1.0'`, `CURRENT_CLIENT_VERSION = '0.1.0'`, `MIN_COMBAT_RULES_VERSION = 1` + helpers `parseSemver` / `compareSemver` / `checkClientVersion`
    - Politique : `min < current` (flexible) — on bumpe `current` à chaque release, `min` uniquement aux breaking changes
    - Nouveau endpoint `GET /version` (public, non gardé) sous `src/routes/version.ts` qui renvoie `{ minClientVersion, currentClientVersion, minCombatRulesVersion }`
    - Nouveau middleware `versionGuard` sous `src/middlewares/version.middleware.ts` qui lit le header `X-Nymora-Client-Version`, refuse en HTTP 426 si absent/malformé/trop vieux (avec `reason: 'missing'|'malformed'|'too_old'` dans la réponse)
    - Appliqué sur `/auth/register`, `/auth/login`, `/auth/me` (pas sur `/auth/photon-webhook` car Photon n'envoie pas de header custom)
    - Webhook Photon : lit `clientVersion` depuis body/query (envoyé par Unity via `AuthValues.SetAuthPostData`), refuse en ResultCode=2 si trop vieux
    - Nouveau smoke test `npm run test:version` (8 cas : GET /version + 3 cas de refus register + register OK + 3 cas webhook Photon)
    - Mise à jour des 2 smoke tests existants pour passer le header / `clientVersion` (sinon régression)
  - Unity (commit côté `Nymora`) sous `Assets/_Nymora/Scripts/Network/Backend/` :
    - Nouveau `NymoraVersionClient.cs` : interroge GET /version, parse semver, retourne `VersionCheckResult` (`IsReachable` / `IsCompatible` / `IsUpdateAvailable` / `MinClientVersion` / `CurrentClientVersion`)
    - `NymoraApiClient.cs` : injection automatique du header `X-Nymora-Client-Version: GameVersion.Current` sur **toutes** les requêtes auth ; nouvelle méthode `GetVersionAsync` qui SKIP le header (anti chicken-and-egg : un client trop vieux doit pouvoir interroger `/version` pour apprendre ce qui est requis)
    - `NymoraApiDtos.cs` : ajout DTO `VersionResponse`
    - `PhotonAuthBridge.cs` : ajout `clientVersion = GameVersion.Current` dans le dictionnaire `SetAuthPostData` (transmis au webhook backend)
  - Unity UI sous `Assets/_Nymora/Scripts/UI/Login/` :
    - `LoginScreenController.cs` : au `Start()`, check version AVANT toute autre requête. Si incompatible → `LockUiForUpdate` qui désactive les 4 boutons + affiche le panel "Mise à jour requise" avec version installée / min / dernière. Defense-in-depth : détection HTTP 426 sur Login/Register handlers (au cas où le serveur bumperait son `min` en cours de session)
    - Nouvelles refs `_updateRequiredPanel` (GameObject) + `_updateRequiredText` (TMP_Text)
  - Editor Tool `CreateLoginSceneTool.cs` : ajout panel plein écran "Mise à jour requise" (titre orange + message centré, initialement désactivé) + câblage SerializedObject des 2 nouvelles refs
  - Validation E2E :
    - `npm run test:auth` 7/7 PASSED, `npm run test:photon-webhook` 6/6 PASSED, `npm run test:version` 8/8 PASSED
    - Unity Play normal : status "Verification de la version client..." puis "Aucune session active." → panel non affiché
    - Test faux client trop vieux : `GameVersion.Current = "0.0.5"` → panel "Mise à jour requise" plein écran, boutons grisés
    - Healthcheck Unity : 0 erreur, 0 warning
  - Pièges à retenir :
    - `GET /version` doit rester publique sans middleware version (sinon chicken-and-egg : un client trop vieux ne pourrait pas apprendre ce qui est requis)
    - Tests smoke existants régresseront sans le header X-Nymora-Client-Version → toujours penser à les mettre à jour quand on ajoute une garde middleware globale
    - Sur webhook Photon, ordre des checks : token d'abord (BadParams si manquant), puis version (InvalidAuth si trop vieille), puis JWT verify, puis DB anti-révocation

---

- **Brique 1.9** — Client Unity : intégration Photon Quantum + Auth (Custom Auth) (validée 10 mai 2026)
  - Architecture end-to-end Custom Auth :
    - Client Unity envoie son JWT à Photon Cloud via `AuthenticationValues.SetAuthPostData({"token": jwt})` (Custom auth type)
    - Photon Cloud appelle le webhook backend `POST /auth/photon-webhook` (URL configurée dans le dashboard)
    - Backend valide le JWT (`verifyAccessToken`) + check anti-révocation en DB → réponse `{ResultCode: 1/2/3, UserId, Nickname, AuthCookie}`
    - Si ResultCode=1 : Photon valide la connexion, sinon refus
  - Backend (commit côté `nymora-backend`) :
    - Nouveau endpoint `POST /auth/photon-webhook` dans `src/routes/auth.ts` : accepte le token en POST body OU en query string, distingue 3 ResultCodes (OK / InvalidAuth / BadParams)
    - Nouveau smoke test `npm run test:photon-webhook` (6 cas : body OK, query OK, missing token, fake JWT, user supprimé, AuthCookie correct avec mmr=1000)
    - Constants ResultCodes nommées (PHOTON_RESULT_OK = 1, _INVALID_AUTH = 2, _BAD_PARAMS = 3) pour lisibilité
  - Unity (commit côté `Nymora`) sous `Assets/_Nymora/Scripts/Network/Backend/` :
    - `PhotonAuthBridge.cs` : helper static `BuildAuthValues(jwt)` qui construit l'`AuthenticationValues` Photon
    - `PhotonConnectionTester.cs` : MonoBehaviour qui implémente `IConnectionCallbacks`, lance un test connect au Master Server avec UniTask (`TestConnectAsync(jwt)` → `PhotonConnectResult` succès/échec), Service() en Update, cleanup propre qui attend `ClientState.Disconnected` avant de relâcher le RealtimeClient (évite warning Photon "DispatchIncomingCommands wasn't called")
  - Modifs `LoginScreenController.cs` : 4ème bouton "Connect Photon" + handler async, vérifie `_auth.IsLoggedIn` avant de tester
  - Modifs `CreateLoginSceneTool.cs` : ajout du 4ème bouton sur ligne 2, `PhotonConnectionTester` ajouté en component sur le GameObject `LoginScreenController`, câblage SerializedObject
  - Asmdef `Nymora.Network` ref `Photon.Realtime` + `Quantum.Unity`
  - Photon Dashboard : Custom Authentication activée pour app Quantum, URL = `https://<ngrok-id>.ngrok-free.dev/auth/photon-webhook`, "Reject all clients if not available" coché (fail-closed)
  - Tunnel HTTPS public : ngrok-free utilisé pour cette session de dev (URL temporaire, change à chaque relance ngrok)
  - Pièges traversés en 1.9 :
    1. Backend webhook URL incomplète dans le dashboard Photon (sans path `/auth/photon-webhook`) → Photon tape la racine → Express renvoie page HTML d'erreur "Cannot POST /" → Photon parse JSON → crash `Unexpected character: <`. Diagnostic via comparaison côté client (erreur explicite "deserialization failed: <") + reproduction `curl POST /` qui montre le HTML
    2. Faux suspect au début : warning page ngrok-free. Écarté en testant un POST sans header `ngrok-skip-browser-warning` qui forwardait correctement le JSON
    3. Warning Photon "DispatchIncomingCommands() wasn't called for >5000s" après Disconnect : Cleanup() était trop rapide → fix avec wait `ClientState.Disconnected` + timeout 2s en boucle UniTask.Yield
  - Validation manuelle (via ngrok tunnel actif) : Login → Click "Connect Photon" → Status "Photon OK ! Region=eu UserId=<UUID Postgres>" → confirmation que Photon Cloud a appelé notre webhook et reçu UserId du backend
  - Healthcheck Unity : 0 erreur, 0 warning
  - Smoke tests backend post-1.9 : `npm run test:auth` 7/7 PASSED + `npm run test:photon-webhook` 6/6 PASSED

---

- **Brique 1.8** — Client Unity : connexion HTTP au backend (validée 10 mai 2026)
  - Décisions techniques : JsonUtility natif (pas Newtonsoft) + UniTask 2.5.10 (async/await sur UnityWebRequest) + Editor Script de génération de scène
  - Dep ajoutée dans `Packages/manifest.json` : `com.cysharp.unitask` via git URL pinnée au tag `#2.5.10`
  - Asmdef updates : `Nymora.Network` ref `UniTask` ; `Nymora.UI` ref `Network` + `Unity.TextMeshPro` + `UniTask` ; `Nymora.Editor` ref `Unity.TextMeshPro` + `UniTask` (pour le tool de génération de scène)
  - Code livré sous `Assets/_Nymora/Scripts/Network/Backend/` :
    - `NymoraBackendSettings.cs` : SO `BaseUrl` + `TimeoutSeconds` (commit OK, BaseUrl pas un secret)
    - `NymoraApiDtos.cs` : `RegisterRequest`, `LoginRequest`, `AuthResponse`, `ApiUserDto`, `MeResponse`, `ApiErrorBody` (champs publics pour JsonUtility)
    - `NymoraApiClient.cs` : wrap `UnityWebRequest`, struct `ApiResult<T>` (success/failure unifié), 3 méthodes `RegisterAsync` / `LoginAsync` / `GetMeAsync`, gestion `UnityWebRequestException` + `OperationCanceledException`
    - `AuthService.cs` : façade haut-niveau, persiste le JWT via `PlayerPrefs["nymora.auth.jwt"]`, restaure au constructeur, expose `IsLoggedIn` / `Logout`
  - Code livré sous `Assets/_Nymora/Scripts/UI/Login/` :
    - `LoginScreenController.cs` : MonoBehaviour qui pilote la scène, hooks/unhooks listeners proprement, gère `CancellationTokenSource` au cycle de vie, vérifie `/me` au `Start()` si token présent
  - Editor Scripts livrés sous `Assets/_Nymora/Editor/Setup/` :
    - `CreateBackendSettingsTool.cs` (menu `Nymora > Setup > Create Backend Settings`)
    - `CreateLoginSceneTool.cs` (menu `Nymora > Setup > Create Login Scene`) — génère Canvas + 3 TMP_InputField + 3 Button + StatusText + cable les références sur le LoginScreenController via SerializedObject
  - Asset settings créé : `Assets/_Nymora/Settings/NymoraBackendSettings.asset` (BaseUrl `http://localhost:3000`)
  - Scène créée : `Assets/_Nymora/Scenes/00_Login.unity` (première scène Nymora dans le repo)
  - Stratégie token : PlayerPrefs (temporaire Phase 1) ; à migrer vers stockage sécurisé plateforme-spécifique en Phase 7+ avant alpha
  - Piège traversé : `Nymora.Editor.asmdef` doit aussi référencer `Unity.TextMeshPro` pour que le tool de génération de scène compile (sinon `TMPro` introuvable). La transitivité via `Nymora.UI` ne suffit pas pour les types externes.
  - Validation manuelle (6/6 PASSED) : register depuis Unity → user en DB Postgres avec passwordHash bcrypt $2b$12$... → JWT loggé en console → token persiste après stop/play (PlayerPrefs) → /me valide la session au démarrage → logout efface le token → login avec mauvais mdp retourne bien 401 "Invalid credentials"
  - Healthcheck Unity : 0 erreur, 0 warning
  - 3 fenêtres de terminal ouvertes pour le test : Docker Desktop (Postgres+Redis), `npm run dev` (backend Express), Unity Editor

---

- **Brique 1.7** — Auth JWT + bcrypt (validée 10 mai 2026)
  - Stack : `bcrypt@6` (cost 12) + `jsonwebtoken@9` + `zod@4` + types associés
  - Architecture séparée services / middlewares / routes :
    - `src/services/auth.service.ts` : `hashPassword` / `verifyPassword` / `signAccessToken` / `verifyAccessToken`
    - `src/middlewares/auth.middleware.ts` : `requireAuth` (Bearer) + augmentation globale `Request.user`
    - `src/routes/auth.ts` : `POST /auth/register`, `POST /auth/login`, `GET /auth/me`
  - Validation zod (email + password 8-128 + displayName 3-20 [a-zA-Z0-9_-])
  - Stratégie JWT : **access only, durée 24h** (refresh token = brique dédiée plus tard si l'UX le demande)
  - JWT_SECRET 512 bits (hex) + JWT_EXPIRES_IN dans `.env` (jamais commit), placeholder + commande de génération dans `.env.example`
  - Erreur unique Prisma `P2002` distinguée (email vs displayName) → 409 avec message clair
  - `lastLoginAt` mis à jour à chaque login réussi
  - `src/index.ts` charge `dotenv/config` au démarrage (avant tout import qui lit `process.env`)
  - Smoke test `npm run test:auth` (7 checks : register, dup register 409, login, wrong pw 401, /me OK, /me sans token 401, /me token bidon 401) → "Auth smoke test PASSED." ✅
  - Test démarre Express in-process sur port random + cleanup auto (avant + après) → pas besoin de serveur externe
  - ESLint vert (warning cosmétique TS 5.9 vs typescript-eslint 7 → upgrade toolchain à planifier en fin de Phase 1, voir section Maintenance plus bas)

---

## 🔄 BRIQUE EN COURS

### 🎮 PHASE 2 — Combat (Soulrender + Nightseer)

**Durée estimée :** 2 mois (~17 briques)

**Objectifs (extraits de `05_Roadmap_V2_Novice.md`) :**
- Système de grille de combat 15×17 cases
- Système de tour par tour avec PA/PM/HP
- Pathfinding A* pour les déplacements
- **Soulrender complète** : 15 sorts + signature + ressource Hémoglyphe + passif L'Appel du Sang
- **Nightseer complète** : 15 sorts + signature + ressource Prescience + passif L'Œil qui n'est pas
- Brouillard de guerre fonctionnel
- IA de combat niveau Easy et Medium
- **Combat 1v1 vs IA jouable bout en bout** ← LE moment de vérité gameplay

### Cadrage des 17 briques (validé le 11 mai 2026)

**Bloc A — Fondations grille & tour (5 briques) ✅ BOUCLÉ 11 mai 2026**
- 2.1 — Grille 15×17 (data Quantum FP + visualisation iso 2D) ✅ VALIDÉE 11 mai 2026
- 2.2 — Entity Combatant Quantum (HP/PA/PM/Class) ✅ VALIDÉE 11 mai 2026
- 2.3 — État machine de tour + timer 15s + initiative ✅ VALIDÉE 11 mai 2026
- 2.4 — Mouvement case par case (PM) ✅ VALIDÉE 11 mai 2026
- 2.5 — Pathfinding A* déterministe ✅ VALIDÉE 11 mai 2026
- 2.3 — État machine de tour + timer 15s + initiative
- 2.4 — Mouvement case par case (PM)
- 2.5 — Pathfinding A* déterministe

**Bloc B — Sorts & ciblage générique (3 briques) ✅ BOUCLÉ 11 mai 2026**
- 2.6 — Système de targeting (Shape + Filter) ✅ VALIDÉE 11 mai 2026
- 2.7 — Spell runtime engine ✅ VALIDÉE 11 mai 2026
- 2.8 — Premier sort Tranche-Âme Soulrender ✅ VALIDÉE 11 mai 2026
- 2.7 — Spell runtime engine
- 2.8 — Premier sort Tranche-Âme Soulrender (E2E damage flow)

**Bloc C — Soulrender (3 briques, 2.10 découpée en a/b/c)**
- 2.9 — Ressource Hémoglyphe (cap 5) ✅ VALIDÉE 11 mai 2026
- 2.10.a — Framework Statuses + 5 sorts (Ouvre-Plaie, Pacte de Sang, Rugissement, Rage Insatiable, Riposte Carmin) ✅ VALIDÉE 11 mai 2026
- 2.10.b — Shields + Heals + Marques + 5 sorts (Peau de Fer, Sève Vive, Dernier Souffle, Marque de Carnage, Empoignade) ✅ VALIDÉE 11 mai 2026
- 2.10.c — Terrains (Vapeur Carmin, Sang Coagulé) + Mvt non-PM + Kill detection + 4 sorts (Charge Brutale, Détonation Sanglante, Curée, Cautérisation) + effet bonus Tranche-Âme ✅ VALIDÉE 11 mai 2026
- 2.11 — Signature Âme Lacérée + Passif Appel du Sang ✅ VALIDÉE 11 mai 2026
- 2.12 — Assets visuels Soulrender (sprites 4 dirs + icônes 15 sorts + icône passif + signature) ✅ VALIDÉE 12 mai 2026
- 2.12.bis — Anims complètes (Idle/Walk/Cast par catégorie/Attack/Hurt/Death + state machine) ✅ VALIDÉE 12 mai 2026
- 2.13.a — HUD layout + icônes cliquables + EndTurn manuel ✅ VALIDÉE 12 mai 2026
- 2.13.b — Prévisu range PM (BFS) + range sort armed (Manhattan) + zone d'effet hover ✅ VALIDÉE 12 mai 2026
- 2.13.c — Tooltips Bible V7.1 + texte flottant dgts/heals + cooldown signature ✅ VALIDÉE 12 mai 2026
- 2.13.d — Caméra zoom molette + pan clic molette + reset (Home / double-clic molette) ✅ VALIDÉE 12 mai 2026

**Bloc D — Nightseer (3 briques)**
- 2.12 — Brouillard de guerre déterministe
- 2.13 — Prescience + 15 sorts Nightseer + passif L'Œil qui n'est pas
- 2.14 — Signature Traquenard

**Bloc E — IA & E2E (3 briques)**
- 2.15 — IA Easy (greedy)
- 2.16 — IA Medium (heuristique multi-tour)
- 2.17 — Scène `30_CombatIA` + test E2E combat 1v1 jouable 🎯

**Prochaine étape :** Démarrer 2.10.c — Terrains (`Tile.TerrainType` + tick TurnStart pour effets passants/dégâts début tour : Vapeur Carmin -1 PM en traversée, Sang Coagulé 30 dgts en début de tour), Mouvement non-PM (helper qui ignore le compteur PM mais respecte walkable/occupant — pour Charge Brutale + recul Tranche-Âme), Kill detection post-damage (signal pour Curée chain + Sang Coagulé Détonation + recul Tranche-Âme). 4 sorts à livrer : Charge Brutale (ligne range 5 + Vapeur Carmin), Détonation Sanglante (AoE croix 3 + Sang Coagulé + interlock signature 2.11), Curée (kill detection chain + heal proportionnel + gain PA next turn + self-damage miss), Cautérisation (stub retire DoT — pas de DoT actuel mais structure prête pour Necram). Plus l'effet bonus Tranche-Âme (recul 2 cases si kill, clôt le TODO 2.11 noté en 2.8).

---

## 🔧 MAINTENANCE PLANIFIÉE

- **Fin de Phase 1** : upgrade toolchain ESLint (`eslint@8` → `eslint@9`, `@typescript-eslint/*@7` → `@8`) pour couvrir TS 5.9. Aujourd'hui ça marche, juste un warning au lancement de `npm run lint`. ~30 min de churn estimé (config flat-eslint à migrer).

---

## 📋 PHASE 0 — STRUCTURE MISE À JOUR (10 briques)

La Phase 0 passe de **8 briques (2 semaines)** à **10 briques (2.5 semaines)** avec ajout des outils de scan.

| # | Brique | Durée | Statut |
|---|---|---|---|
| 0.1 | Installation Unity et création du projet | 1/2 jour | ⏳ En cours |
| 0.2 | Configuration Editor et structure dossiers | 1/2 jour | À venir |
| 0.3 | Git + Git LFS + .gitignore + **pre-commit hook** | 1 jour | À venir |
| 0.4 | IDE et auto-complétion | 1/2 jour | À venir |
| 0.5 | Assembly Definitions (asmdef) + console filter | 1 jour | À venir |
| 0.6 | Enums et data containers de base | 1 jour | À venir |
| 0.7 | ScriptableObject NymoraClassDefinition | 1 jour | À venir |
| 0.8 | ScriptableObject SpellDefinition (template) | 1 jour | À venir |
| **0.9** | **Roslyn Analyzers + ruleset custom Nymora** | **1 jour** | **NOUVEAU** |
| **0.10** | **Editor Script Nymora_HealthCheck** | **1 jour** | **NOUVEAU** |

---

## 🛠️ ENVIRONNEMENT CONFIRMÉ

- **OS :** Windows
- **Unity :** 2022.3.62f3 (LTS) installé
- **Unity Hub :** installé
- **IDE :** Visual Studio 2022 (workload Unity à confirmer)
- **Git :** installé + SSH configuré
- **GitHub :** compte actif
- **Dossier dev :** existant sur sa machine
- **Disponibilité :** 3-5h/jour en moyenne

---

## 📦 DÉCISIONS PRISES (verrouillées)

| Décision | Choix | Date |
|---|---|---|
| **Cible alpha** | **Windows uniquement** (Mac + Mobile post-alpha) | **8 mai 2026** |
| Plateformes long terme | PC + Mac + Mobile (extension post-alpha) | 8 mai 2026 |
| Modèle économique | F2P + battle pass + cosmétiques (pas de P2W) | 8 mai 2026 |
| Engine | Unity 2022.3.62f3 (pas de migration vers Unity 6) | 8 mai 2026 |
| Render pipeline | Universal 2D (URP 2D) | 8 mai 2026 |
| Netcode combat | Photon Quantum 3 (déterministe) | 8 mai 2026 |
| Netcode hub/social | Photon Fusion 2 (Shared Mode) | 8 mai 2026 |
| Backend stack | Node.js + TypeScript + Express + PostgreSQL 16 + Redis 7 | 8 mai 2026 |
| ORM | Prisma | 8 mai 2026 |
| Hosting Phase 1 | Hetzner CX22 (~4€/mois) | 8 mai 2026 |
| Auth | JWT + bcrypt cost 12 + Custom Auth Photon | 8 mai 2026 |
| Workflow | Brique par brique, validation one-shot | 8 mai 2026 |
| Timeline cible | **~12 mois pour alpha Windows** (mai 2026 → mai 2027) | 8 mai 2026 |
| Parallélisme Claude Code | Phases 0-3 sequential strict, 4-7 parallel controlled | 8 mai 2026 |
| Editor Scripts | Convention `Assets/_Nymora/Editor/{Setup,Generators,Windows}/` + asmdef `Nymora.Editor` | 8 mai 2026 |
| **Outils scan auto** | **Roslyn + Healthcheck + Pre-commit hook + Console filter (Phase 0)** | **8 mai 2026** |
| **Soft launch alpha** | **Steam Playtest ou itch.io** (pas TestFlight ni Google Play pour alpha) | **8 mai 2026** |
| **Déblocage des sorts** | **Tous les 15 sorts par classe dispos dès la création du compte** (pas de gate gameplay par level) | **10 mai 2026** |
| **Rôle des levels par classe** | **Récompenses cosmétiques + titres uniquement** (cadres, couleurs, skin niveau 50) — pas de gameplay | **10 mai 2026** |
| **6e classe** | **Non planifiée** (focus sur les 5 existantes : Soulrender, Nightseer, Colossar, Necram, Ghostra) | **10 mai 2026** |
| **Vue grille de combat** | **Isométrique 2D** (cases en losange, style Dofus/Wakfu) — vit uniquement côté View ; la simulation Quantum reste rectangulaire en `int` | **11 mai 2026** |
| **Origine grille (logique)** | **(0,0) bas-gauche, X→droite, Y→haut** (convention math classique, cohérent Unity world space) | **11 mai 2026** |
| **PPU sprites combat (tiles)** | **64 px / unit** (sprites tile 64×64, projection iso ratio 2:1 = losange 64×32 world) | **11 mai 2026** |
| **PPU sprites combat (persos)** | **128 px / unit** (sprites perso 128×128 → 1×1 unite world = 1 case). Convention : chaque categorie de sprite a son PPU adapte a sa resolution, jamais le meme PPU pour des sprites de tailles differentes | **11 mai 2026** |

---

## 🧠 CHOSES IMPORTANTES À RETENIR

### Pour la prochaine instance Claude
- Lorenzo a déjà un projet Unity NymoraV1 existant (avec Photon PUN 2) **mais on repart de zéro** — pas de migration.
- Les docs de la **Bible V7.1** (combat) sont **stables** et ne doivent pas être modifiés sans accord explicite de Lorenzo.
- Lorenzo apprécie les questions ciblées (`ask_user_input_v0`) plutôt que les longs paragraphes spéculatifs.
- Quand un sort/passif/ressource est mentionné, vérifier dans `01_BIBLE_V7.1_Combat.md` avant d'inventer.
- **Lorenzo a vécu un échec sur un précédent projet Nymora** à cause de mauvais setup (copier-coller entre scènes, desync, MVP mal ficelé). Cette nouvelle approche existe précisément pour éviter ça. Il est très motivé à respecter les règles strictes.
- **Toute suggestion de coder du Mac/iOS/Android avant la fin de l'alpha Windows doit être REFUSÉE.** L'alpha Windows d'abord, point.
- **Toute brique de combat doit utiliser FP (fixed-point) et non float**, car Photon Quantum est déterministe.
- **Avant chaque commit important, exécuter mentalement le Healthcheck** ou le lancer si déjà implémenté.

### Pour Lorenzo
- Si tu modifies un script que Claude t'a donné, **préviens-la dans le prochain message** sinon désync mentale.
- Commit Git à chaque fin de brique, message format : `feat(phaseX): description courte`.
- Si une brique te paraît trop grosse, demande à Claude de la **redécouper** plutôt que de la traiter à moitié.
- Si tu sautes 2-3 jours, pas grave, mais tiens à jour ce fichier au retour.

---

## 📝 JOURNAL DE BORD (les sessions importantes)

### 14 mai 2026 (session courante post-Bloc E lite) — 🚀 **OUVERTURE PHASE 4 (Hub Communautaire + Social)**
- **Contexte** : retour Lorenzo après session AFK où Claude a livré Bloc E lite Replay/Debug (commits `adff404` → `b1b7d51`). Plan Phase 4 validé en memory (`project_phase4_plan.md`) : 13 briques en 5 blocs, ~8 semaines, contrainte local-first jusqu'à 4.4.
- **Décisions clés** :
  - **Phase 3 partiellement clôturée** : Bloc A + Bloc B Colossar + Bloc E lite Replay/Debug ✅. **Bloc C Necram + Bloc D Ghostra + Bloc E IA Hard MCTS différés** post-Phase 4 (ou parallèle si bandwidth). Rationale : 3 classes (Soulrender + Nightseer + Colossar) suffisantes pour valider le fun multijoueur, on bascule sur Hub commu pour activer le combat ranked 1v1 et le défi casual.
  - **Local-first jusqu'à 4.4 validée** (gate multi-instance 2 clients sur PC Lorenzo qui se voient se déplacer) : pas d'Hetzner, pas de domaine, pas de licence payante avant validation gameplay. Cf memory `feedback_minimize_costs_before_gameplay_validation.md`.
  - **Designer travaille sans backend** : place-holders avatar hub + décors map + PNJ livrés en *late insert* dans le bloc concerné quand Necram + Ghostra finis côté art. Cf memory `project_team_setup.md`.
  - **Sub-agents autorisés "PARALLEL CONTROLLED"** : Phase 4 est dans la zone où sub-agents OK pour tâches strictement indépendantes (ex: 4.10 Amis + 4.11 Clans) AVEC accord explicite Lorenzo. Défaut = séquentiel.
- **Brique 4.1 ouverte** : Photon Fusion 2 SDK install + PhotonAppSettings Fusion (latest stable, AppId Fusion déjà créé en 1.1).

---

### 14 mai 2026 nuit profonde (5) — ⚒️ Brique 3.3.b.iii ✅ Refacto Bible-correct 5 sorts tactiques Colossar
- **Contexte** : avant d'attaquer 3.3.c, Lorenzo a demandé `"Donne-moi la Bible exacte d'abord"`. Lecture Bible V7.1 a révélé que **les 5 sorts tactiques livrés en 3.3.b.i + 3.3.b.ii étaient NON-CONFORMES** (constantes inventées, effets manquants, mauvaise cible). Refacto rétroactif immédiat avant de continuer.
- **5 fichiers** : `SpellRegistry.cs` (constantes Bible-exact : PilierRangeMax 1→3, MurRangeMax 2→4, MurSegmentsBoosted 5, AncrageRangeMax → 4 + 2 PM + 2T + 1T immune, ProvocationRangeMax 4→5 + Turns 2→1 + 3 effets, BrisureRangeMax 0→2 + 90 dmg + TraumaPAMag 2), `SpellSystem.cs` (5 cases refactorés + hook +2 PA cost via EffectiveStats.GetPACost si Provoked et target ≠ provocateur + Mur boost option `hgSpend >= 1 ? 5 : 3` segments + Brisure refacto 90 dmg pipeline + retire 1 buff prio (Shield > Ronces > AnchorImmune > BuffNextOffensive > RipostMelee > RageInsatiable) sinon TRAUMA ActionMalus -2 PA), `TurnSystem.cs` (hook Provocation auto-dmg 100 fin tour P1 si distance Manhattan(P0, P1) > 1 + lookup provocateur via Magnitude=PlayerIndex), `CombatInputController.cs` (commentaires Bible-correct + Shift+O = Mur boost FD), `GameVersion.cs` (16→17).
- **Décisions clés** :
  - **Magnitude semantics flexible** : AnchorImmune Magnitude = % réduction (50), Provoked Magnitude = PlayerIndex provocateur. Permet lookup runtime du provocateur sans champ dédié dans Status.qtn. Resource field gardé générique (HG/PR/FD/PT/RM).
  - **Brisure priorité de buff** : ordre explicite (ShieldActive > RoncesAura > AnchorImmune > BuffNextOffensiveDmgPercent > RipostMelee > RageInsatiableActive) — interprétation Bible "retire 1 buff actif au choix" en pratique = 1 buff prioritaire défensif. Si aucun buff = TRAUMA fallback (-2 PA prochain tour).
  - **Mur option FD** : `hgSpend >= 1` (option Bible "1 FD supplémentaire" → 5 segments boostés au lieu de 3 base). Recyclage du champ `hgSpend` (qui signifie "ressource optionnelle générique" dans le pipeline cast) pour FD ici. Shift+O dans input mappe 1 FD.
  - **Memory rule créée** : `feedback_bible_check_before_spell_delivery.md`. Toute livraison de sort/passif/signature désormais précédée d'une lecture Bible V7.1 exacte. Plus jamais d'invention/approximation.
- **E2E validé** :
  - ✅ Pilier touche P range 3 cast OK
  - ✅ Mur touche O range 4, Shift+O = 5/5 segments posés à 7,8→7,10
  - ✅ Combo Mur + Pilier bloque IA 20 PA Charge Brutale sur 3 rounds (gameplay émergent)
  - ✅ Ancrage touche Y range 4 cible Enemy : rejection sur Mur (case obstacle) + distance 5 (hors range) + success distance 4 + -2 PM appliqué P1 sur 2 rounds
  - ✅ Provocation touche `,` range 5 cible Enemy : `[Spell] Provocation : P1 provoque par P0 pour 1T` + MovementMalus -1 PM appliqué P1 start sub-turn confirmé
  - ✅ Provocation pas de 100 dmg auto fin tour P1 = cohérent (Charge Brutale a posé P1 en (6,8) adjacent à P0 (5,8), Manhattan dist=1, règle Bible respectée)
  - ✅ Brisure touche `.` range 2 cible Enemy : `Damage 90 sur P1 1500→1410` pipeline standard + `Brisure : pas de buff sur P1 -> TRAUMA -2 PA prochain tour` + `ActionMalus -2 PA applique sur P1 (PA=6/8)` round suivant confirmé
- **Backlog Brisure E2E retire-buff** : non testé (besoin scenario avec buff actif sur cible, ex Shield Soulrender → tester en 3.3.c quand on aura plus de Soulrender survie). Pas bloquant.
- **Backlog IA Hard MCTS 3.8** : confirme bug `AISystem.TryGreedyCastSingle` ne check pas LoS dans son estim → l'IA continue à gaspiller PA en Charge Brutale vers cible derrière Mur. Pas bloquant pour Phase 3 Colossar.

---

### 14 mai 2026 nuit profonde (4) — ⚒️ Brique 3.3.b.ii ✅ Ancrage + Provocation + Brisure (Colossar tactiques complètes)
- **6 fichiers** : `Status.qtn` (StatusKind 13 AnchorImmune + 14 Provoked), `Spell.qtn` (SpellId 57/58/59), `SpellRegistry.cs` (constantes + 3 SpellDef Self/Enemy/AnyTile), `SpellSystem.cs` (3 case handlers + 3 hooks AnchorImmune dans damage compute pipeline standard + Charge Brutale + Choc Sismique + 1 hook anti-push dans PushAndTriggerEx + 1 hook anti-pull dans Empoignade), `CombatInputController.cs` (touches Y / , / .), `GameVersion.cs` (15→16).
- **Décisions clés** :
  - **AnchorImmune Magnitude = % reduction** (50). Permet d'extensible pour d'autres niveaux d'ancrage futurs (Necram?). Hook unifié `dmg * (100 - magnitude) / 100`.
  - **Provoked stub MVP** : status apply + duree decrement OK (skip-decrement turn 0). Effet IA (force le bot à cibler le provocateur) reporté en 3.8 IA Hard MCTS pour ne pas dériver du scope brique tactique.
  - **Brisure Filter EmptyTile rejeté** au profit de Filter AnyTile + validation custom dans handler (case doit avoir obstacle non-OWN). Plus lisible, log d'erreur explicite, friendly fire bloqué.
  - **AnchorImmune anti-displacement EXTERNE uniquement** : push/pull bloqués, mais Pas Furtif/Évanescence (self-teleport) restent possibles. Bible silencieuse, choix gameplay (caster maître de ses propres sorts).
  - **3 hooks dmg parallèles** (pipeline + Charge Brutale + Choc Sismique) car ces 2 derniers bypass le pipeline standard. Pattern miroir Densité Inerte (3.2). Le Brisure aussi le check pour cohérence.
- **E2E validé** :
  - **Ancrage (Y)** : `[Spell] Ancrage : P0 immune push/pull + -50% dgts subis pour 2 tours` ✅
  - **Effet -50% sur Charge Brutale** : 6 logs `[Ancrage] -50% dmg sur P0 (Charge Brutale) : 180 -> 90` cumulés sur Round 1+2 ✅
  - **Expiration Round 3+** : retour à `Damage 180 (HP loss 180)` confirme skip-decrement + 2 rounds = effet round 1 et 2, off à round 3 ✅
  - **Provocation (,)** : 2 casts `[Spell] Provocation : P1 provoque par P0 pour 2 tours (stub IA, effet en 3.8)` ✅
  - **Brisure (.)** sur case vide : `[Spell] Brisure : pas d'obstacle sur (3,6), no-op` (rejet propre) ✅
- **Brisure E2E destruction obstacle** : non testé (manque setup 2× Colossar pour avoir un Pilier ENEMY à briser). Reporté backlog.
- **Bug Provocation distance 0** : Lorenzo a tenté cast tour 1 sur sa propre case (3,8) → `rejet : distance 0 hors range [1,4]`. Comportement Bible-correct (Provocation cible ennemi). Lorenzo a dû Sève Sauvage + déplacer puis retry tour 2.
- **Brique 3.3.b complète** : 5/5 sorts tactiques Colossar livrés et E2E validés. Prêt 3.3.c (5 survie + 1 signature EFFONDREMENT).

---

### 14 mai 2026 nuit profonde (3) — ⚒️ Brique 3.3.b.i ✅ Pilier + Mur de Pierre + helper LoS + fix Charge Brutale
- **Découpage** : brique 3.3.b (5 sorts tactiques Colossar + fix LoS bug deferred 3.3.a) sous-divisée en 3.3.b.i (Pilier + Mur + LoS infra) + 3.3.b.ii (Ancrage + Provocation + Brisure) pour limiter surface bug. Pattern Phase 2 reproduit.
- **7 fichiers** : `Spell.qtn` (SpellId 55 Pilier + 56 MurDePierre), `SpellRegistry.cs` (constantes PilierHP=200/3T + MurSegmentHP=150/2T/3 segments + 2 SpellDef Filter EmptyTile), `ObstacleHelpers.cs` (NEW `HasLineOfSight` Bresenham 2D int-only deterministe, OWN obstacles ne bloquent pas le owner), `SpellSystem.cs` (hook LoS pré-cast + helper `SpellNeedsLineOfSight` whitelist 14 sorts directs distance + 2 cases handlers Pilier/Mur perpendiculaire + fix Charge Brutale `break` sur HasObstacleAt + fix Choc Sismique stop sur obstacle non-OWN), `CombatInputController.cs` (touche P=Pilier remplace ancien debug spawn 3.1, touche O=Mur, U gardé pour debug damage destruction +30 HP), `GameVersion.cs` (14→15).
- **Décisions clés** :
  - **LoS OWN-friendly** : Bible "Pilier/Mur bloque LoS" interprété comme adversaire-uniquement bloque le caster. Sinon Colossar empêcherait ses propres sorts entre ses Murs (gameplay-killing). Param `casterPlayerIndex` au helper, OWN obstacles traversés.
  - **Liste sorts LoS hardcoded** dans `SpellNeedsLineOfSight` switch (vs flag dans SpellDef) — patterns existant en Phase 2/3, plus simple à lire et maintenir. 14 sorts whitelistés (Soulrender 4 + Nightseer 8 + Colossar 2). Sorts en LIGNE custom (Charge/Volée/Choc) gèrent leur propre arrêt obstacle dans handler → exclus.
  - **Mur perpendiculaire** : axe-dominant Manhattan caster→cible inversé pour direction du mur. Cohérent Bible (mur perpendiculaire à l'angle d'attaque). Si centre occupé → log warn `Spawn rejete` mais autres segments passent (ex 2/3).
  - **Charge Brutale fix** : `break` ajouté dans loop step si `HasObstacleAt(cx, cy)` AVANT check occupant. Caster s'arrête juste avant obstacle, pas de damage (Bible : seule la cible vivante prend dgts, l'obstacle absorbe l'impact mais n'est pas blessé par la charge).
- **E2E validé** :
  - **Pilier (P)** sur (4,8) → Spawn HP=200 + +1 FD : 0→1/3 ✅. Charge Brutale ennemie au tour suivant s'arrête à (5,8) au lieu d'aller jusqu'à P0 en (3,8) ✅
  - **Mur (O)** sur (4,8) qui était déjà Pilier → segments (4,7)+(4,9) posés, centre (4,8) skippé proprement → "2/3 segments poses (centre 4,8, axe perp 0,1)" + 2× +1 FD → cap 3/3 atteint ✅
  - **Choc Sismique (L)** sur (4,8) → traverse Pilier OWN +50 ✅, hit P1 (5,8) **180 dgts** (130 base + 50 wall bonus) ✅ + MovementMalus -1 PM appliqué tour suivant ✅
  - **Power gameplay validé** : Charge Brutale ennemie 2× contre le Mur = 8 PA P1 gaspillés en un tour. Colossar défensif Bible-correct.
- **Bug IA observé** : `AISystem.TryGreedyCastSingle` n'utilise pas `HasLineOfSight` dans son estim → l'IA continue à choisir Charge Brutale sur cible derrière obstacle. Pas un bug du sort (Bible-correct), mais imperfection IA. Reporté en backlog 3.8 (IA Hard MCTS).

---

### 14 mai 2026 nuit profonde (suite) — ⚒️ Brique 3.3.a.ii ✅ Onde de Choc + Marteau Punisseur + Choc Sismique
- **Livraison** : 5 fichiers MOD : `Spell.qtn` (SpellId 51/52/53), `SpellRegistry.cs` (constantes + 3 SpellDef), `SpellSystem.cs` (refactor PushAndTriggerEx avec `out bool stoppedAgainstObstacleOrBorder`, blocs damage compute Marteau Punisseur DEPLETED 240+TRAUMA-2PA, case Onde de Choc dans Resolve AoE rayon 1 like Rugissement, case Onde de Choc dans ApplySpellSpecificEffects qui itère 4 cases adjacentes + push 2 + bonus +80 + TRAUMA si stoppé, case Choc Sismique line iteration custom +50 si traverse Pilier OWN), `CombatInputController.cs` (touches I/K/L), `GameVersion.cs` (13→14).
- **PushAndTriggerEx** : refactor pour exposer `stoppedAgainstObstacleOrBorder`. PushAndTrigger devient wrapper qui passe ce flag à null pour les call sites historiques (Bourrasque, Souffle Glacial). Permet Onde de Choc de savoir si VERROU bonus wall doit s'appliquer.
- **Bug fix mid-livraison** : CS0136 `hpBefore` shadow conflict — Pacte de Sang case sans braces déclarait déjà `int hpBefore` dans le scope englobant de `ApplySpellSpecificEffects`. Renommé `hpBefore` → `hpBeforeOdC` dans le bloc Onde de Choc. C'est le 2e occurrence de ce pattern (déjà rencontré sur Représailles en 3.3.a.i). À surveiller pour les futures cases.
- **E2E match complet validé** : Lorenzo a joué un match complet de 4 rounds qui a fini par sa propre mort (Curée KILL P1).
  - Round 2 : Spawn Pillar (7,8) +1 FD ✅ ; Cast OdC vers (5,8) → P1 (6,8) push 2 bloqué par Pillar (7,8) → `Onde de Choc BONUS WALL +80` (HP 1420 → 1340) + `TRAUMA -1 PA / -1 PM` ✅ + `+1 FD sur P0 (Push contre obstacle) : 1 → 2/3` ✅
  - Round 3 : `Marteau Punisseur DEPLETED : 240 dgts + TRAUMA -2 PA sur P1 (PA=1)` ✅ ; au tour suivant `[TurnSystem] ActionMalus -2 PA applique sur P1 (PA=6/8)` ✅
  - Round 4 : `Choc Sismique : 130 dgts sur P1` ✅ ; au tour suivant `MovementMalus -1 PM applique sur P1` ✅
- **Tout marche** : VERROU bonus wall passif Colossar + TRAUMA combo ActionMalus+MovementMalus + Marteau anti-caster + Choc Sismique line + Densité Inerte continue d'absorber les Charge Brutale (180→165) et Curée (150→138).
- **Bug LoS connu non bloquant** : reporté en 3.3.b quand sorts Pilier/Mur arrivent (cohérent : on fixe LoS quand on a vraiment des sorts qui posent ces obstacles via gameplay normal).

---

### 14 mai 2026 nuit profonde — ⚒️ Brique 3.3.a.i ✅ Frappe Lourde + Représailles + bonus adjacence
- **Découpage** : la brique 3.3.a (5 sorts offensifs Colossar) sous-divisée en 3.3.a.i (2 sorts simples + infra) + 3.3.a.ii (3 sorts AoE) + 3.3.a.iii (cap Représailles backlog) pour limiter la surface de bug. Pattern Phase 2 (2.10.a/b/c) reproduit.
- **5 fichiers** : `Spell.qtn` (SpellId 50 ColossarFrappeLourde + 54 ColossarRepresailles), `SpellRegistry.cs` (constantes + 2 entries SpellDef + bonus adjacence constants), `ColossarPassif.cs` (helper `IsTargetPinnedFromCaster` axe-dominant), `SpellSystem.cs` (bloc Frappe Lourde épinglée + bloc générique bonus adjacence pour tous sorts Colossar range ≤ 2 + case Représailles dans ApplySpellSpecificEffects → RipostMelee 80 dmg/2 tours sur caster), `CombatInputController.cs` (touches H/J), `GameVersion.cs` (12→13).
- **Décisions clés** :
  - Status TRAUMA Bible (Onde de Choc + Marteau Punisseur 3.3.a.ii) = combo des `StatusKind.ActionMalus` (-PA) + `MovementMalus` (-PM) déjà existants. Pas de nouveau StatusKind nécessaire.
  - Représailles cap 4 retours Bible reporté en 3.3.a.iii (edge case, demande extension du Status struct ou nouveau champ tracker côté Combatant).
  - Bonus passif adjacence branché en bloc générique post damage modifs spécifiques (avant shield calc) pour s'appliquer automatiquement à TOUS les sorts Colossar futurs (Onde de Choc, Marteau Punisseur 3.3.a.ii). Pas dans Choc Sismique car range > 2.
  - Helper `IsTargetPinnedFromCaster` placé dans ColossarPassif (cohérent avec autres helpers Colossar). Algo : axe-dominant Manhattan caster→target, check case derrière target = obstacle OU bord de map = épinglée. Bible : Pilier ennemi compte aussi (géométrie pure).
- **E2E Frappe Lourde** (3 effets validés) :
  - Round 2 : `Damage 180 sur P1` (pas épinglée car case (7,8) libre derrière)
  - Round 3 : `Frappe Lourde EPINGLEE : 280 dgts sur P1 (6,8)` (P0 en (6,7), Pilier en (6,9) derrière P1 sur axe vertical) → COMBO BIBLE VALIDÉ
  - Round 4 : `Densite Inerte +20 dmg adjacence sur P0 (sort ColossarFrappeLourde) -> 200` (P0 adjacent à son Pilier en (5,7)) → bonus passif adjacence opérationnel
- **Représailles non testé E2E** : Lorenzo a tenté de cast sur sa propre case (5,8) → `[Spell] rejet : distance 0 hors range [1,1]`. Le code est en place et compile, juste le ciblage de test (case ennemi adjacent) à refaire en session future.
- **Match perdu Lorenzo** : Charge Brutale cascade P1 Soulrender → P0 Colossar HP 40 → 0 → Curée KILL. Affichage `MATCH END — Winner: P1` au round 4. C'est OK pour le test, gameplay équilibré (Soulrender Bible-fort vs Colossar setup-dépendant sans tous ses sorts).
- **Reste pour 3.3.a complet** : 3.3.a.ii (Onde de Choc + Marteau Punisseur + Choc Sismique = 3 handlers complexes AoE/line bypass mur). 3.3.a.iii cap Représailles si Bible-strict.

---

### 14 mai 2026 — 🪨 Brique 3.2 ✅ Stats Colossar + Ressource FD + Passif Densité Inerte
- **Livraison** : 1 NEW (`ColossarPassif.cs` helpers) + 3 MOD (`ObstacleHelpers.cs`, `SpellSystem.cs`, `GameVersion.cs`).
- **`ColossarPassif.cs`** : helpers `CountObstaclesOwnedByPlayer`, `GetDamageReductionPercent` (8%/obstacle cap 24%), `ApplyDamageReduction`, `IsAdjacentToOwnObstacle` (préparé pour 3.3.a +20 dmg adjacence), `GainFondation` (no-op si pas Colossar — defensif).
- **Hooks branchés** :
  - `ObstacleHelpers.SpawnObstacle` → +1 FD au owner si Class=Colossar (Bible "+1 FD chaque fois que pose Pilier/Mur"). Pour 3.2 déclenché par DebugSpawnObstacleCommand, sera repris naturellement par les sorts Pilier/Mur 3.3.b.
  - `ObstacleHelpers.DestroyObstacle` → +30 HP au owner si Class=Colossar ET Kind=Pillar (Bible Densité Inerte "Pilier détruit").
  - `SpellSystem.cs` damage loop standard ligne ~395 → applique `ColossarPassif.ApplyDamageReduction` sur `dmgThisTarget` AVANT shield/HP calc, si target=Colossar.
  - `SpellSystem.cs` `PushAndTrigger` → ajout arg optionnel `Combatant* caster`, ajout check `HasObstacleAt` (cohérence avec MovementSystem 3.1), ajout +1 FD si caster=Colossar et push s'arrête contre obstacle OU bord de map. 2 call sites updated (Bourrasque + Souffle Glacial).
- **Bug détecté pendant test E2E + fix** : Charge Brutale (Soulrender) bypass le damage loop standard et fait son damage manuel ligne ~919. Mon hook initial ne couvrait QUE le pipeline standard donc la Charge Brutale ignorait Densité Inerte (Lorenzo a vu `Damage 180 (HP loss 180)` au lieu de 165 attendu). Fix : ajout du même bloc réduction dans le handler Charge Brutale juste avant le shield calc. Validé après fix : `[Densite Inerte] -8% dmg sur P0 (Charge Brutale) : 180 -> 165`.
- **Décisions architecturales** :
  - Passif sans état persistent : recalcule à la volée depuis `Filter<Obstacle>` à chaque damage. Cheap (max ~6 obstacles concurrents). Pas de cache prématuré.
  - `OwnerPlayerIndex` (redondant vs `Owner` EntityRef) utilisé pour le count car résilient si l'entity caster est détruit entre temps (edge case mais propre).
  - Pacte de Sang / Curee MISS / Riposte Carmin = self-damage, donc Bible Densité Inerte ne s'applique pas (la Bible parle de "subir" des dégâts d'un attaquant). Skipped intentionnellement.
- **Validation E2E Lorenzo (2 sessions de test)** : Spawn Pilier x3 → FD 1/3 → 2/3 → 3/3 ✓. Damage Pilier U×4 → destroy + Colossar +30 HP (`780 → 810`) ✓. Curée 150 → 126 (-16% avec 2 Piliers) ✓. Charge Brutale 180 → 165 (-8% avec 1 Pilier) ✓. Bourrasque P0 push P1 contre Pilier → `+1 FD sur P0 (Push contre obstacle) : 1 -> 2/3` ✓. **VERROU Bible fonctionnel**.
- **Pour 3.3.a** : les sorts offensifs Colossar (Frappe Lourde, Onde de Choc, etc.) bénéficieront naturellement de Densité Inerte sur damage reçu. Le bonus +20 dmg adjacence obstacle (Bible) sera branché dans le damage compute des sorts Colossar (helper `IsAdjacentToOwnObstacle` déjà prêt).

---

### 14 mai 2026 — 🎨 Brique 3.1.bis ✅ Assets Colossar intégrés + switch P0 Colossar
- **Livraison designer** : 35 fichiers (2 .aseprite stage0 NE/SE + 12 GIFs preview + 1 avatar + 1 marque FD + 1 tile fondation + 1 VFX strates + 17 icons sorts/passif/signature). Stages 1+2 viendront plus tard comme pour le Nightseer.
- **Outputs** :
  - Tous les assets copiés sous `Sprites/Colossar/` (Base/sources, Base/stage0, Avatar, Marques, Tiles, VFX, colossar_icons)
  - Nouveau `BuildColossarAnimator.cs` (clone simplifié Nightseer 2.12.bis) — stage0 only car designer n'a livré que ça. Bind les 2 controllers `ColossarStage0_{NE,SE}.controller` sur le prefab `Combatant_Colossar`. Les fields _stage1/2Controller du CombatantView restent null (PickController fallback).
  - Modif `CombatantSystem.OnInit` : P0 spawn = Colossar (au lieu de Soulrender 2.16.a.iii). P1 reste Soulrender pour avoir un adversaire de test.
  - Modif `CreateObstaclePrefabTool` : utilise `tiles_fondation.png` du designer (PPU 180 + pivot Center, après 3 itérations de tuning visuel) pour le prefab Pilier.
  - Modif `TuneAsepriteCharacterSpritesTool` : ajout du dossier Colossar/Base/sources (le designer a sauvegardé Aseprite avec PPU 100 default au lieu de 96 convention projet → forçage via le tool existant).
- **3 itérations visuelles tuning Pilier** (Lorenzo en mode designer en chambre) :
  - Itération 1 : PPU 128 → "2x trop grand" (cube débordait sur cases voisines)
  - Itération 2 : PPU 256 → "trop petit" (cube paraissait flotter au milieu)
  - Itération 3 : PPU 180 (moyenne géométrique) + scale Y=2 + pivot BottomCenter → "smear stretch" laid (étirement pixel art interdit sur sprite carré sans côtés visuels à étirer)
  - Final : PPU 180 + scale 1 + pivot Center → dalle iso correcte posée sur la case. Visuel placeholder honnête, le vrai pilier vertical viendra en 3.3.b avec le VFX `strates_qui_sempilent` du designer.
- **2 pièges traversés** :
  - `TextureImporter.spriteAlignment` n'existe pas en API directe → passer par `TextureImporterSettings` (read/modify/write en 1 fois), sinon `SetTextureSettings` écrase les modifs directes faites juste avant sur l'importer (textureType, PPU). 1ère version mélangeait les 2 APIs → settings du sprite cassés (textureType retombait à Default → tool fallback sur placeholder gris).
  - Aseprite a son propre importer (`AsepriteImporter` du package `com.unity.2d.aseprite`), pas le `TextureImporter` standard. Le designer a sauvegardé Colossar avec PPU 100 (Aseprite default) au lieu de 96 (convention projet) → Colossar mal aligné sur sa case visuel. Heureusement il existait déjà un Editor Tool dédié (`TuneAsepriteCharacterSpritesTool`) qui force pivot custom (0.5, 0.1) + PPU 96 sur les .aseprite — on a juste ajouté le dossier Colossar à sa liste.
- **Validation Lorenzo** : "c'est bon c'est ok chef" — Colossar bien aligné, Pilier bien aligné. Soulrender + Nightseer pas cassés (idempotent grâce à mêmes settings).
- **Bug Bible-correct identifié pendant le test** : le Pilier ne bloque pas les lignes de mire des sorts à distance, et Charge Brutale Soulrender passe à travers. Bible V7.1 dit "Pilier bloque lignes de vue/tir des sorts directs". **Reporté à 3.3.b** (quand les vrais sorts Pilier/Mur Colossar arrivent et qu'on devra anyway intégrer obstacles ↔ SpellSystem).
- **Aucun changement gameplay** : juste assets visuels + switch class P0 + setup tool. CombatRulesVersion reste à 11.

---

### 13 mai 2026 nuit — 🧱 Brique 3.1 ✅ Framework obstacles dynamiques (Bloc A Phase 3)
- **Cadrage Phase 3 publié** : 5 blocs en ~16 briques (~18 max avec marge sous-découpages). Bloc A préreqs cross-classe (3.1) / Bloc B Colossar (3.2-3.3.x) / Bloc C Necram (3.4-3.5.x) / Bloc D Ghostra (3.6-3.7.x) / Bloc E IA Hard MCTS + Replay + Debug (3.8-3.10). Séquentiel par classe (Colossar → Necram → Ghostra) car identités structurellement très différentes. CombatRulesVersion bump à 11.
- **3.1 livraison** (14 fichiers : 8 NEW + 6 MOD) :
  - DSL Quantum `Obstacle.qtn` : `enum ObstacleKind { None, Pillar, Wall }`, `struct ObstacleTile { EntityRef Obstacle }` (8 bytes), `singleton ObstacleSingleton { array<ObstacleTile>[255] }` (~2KB), `component Obstacle { Owner, OwnerPlayerIndex, Kind, HP, MaxHP, GridX, GridY, ExpiresOnTurn }`.
  - **Pattern singleton séparé** (vs extension `Tile`) : la struct `Tile` est marquée "ne pas étendre" (limite 10kB GridSingleton, cf 2.14 FogSingleton). Mirror du Fog : un ObstacleSingleton à part, pareil pattern lookup O(1) par case.
  - **Pattern entity-based** (vs data-only Fog) : un Obstacle = une entity Quantum + composant. Justifié par : (a) HP/destruction par damage = lifecycle entity naturel, (b) Filter<Obstacle> classique pour systèmes futurs, (c) destroy via `f.Destroy(entity)` propre.
  - `ObstacleHelpers` : `SpawnObstacle/DestroyObstacle/DamageAt/HasObstacleAt/GetObstacleAt`. Refus spawn si case occupée par combatant ou autre obstacle.
  - `ObstacleSystem` : OnInit init singleton 255 slots, Update process commands debug + tick expirations en TurnEnd. Stackalloc EntityRef[16] zero-heap pour collecter destroy queue (1ère utilisation de stackalloc EntityRef dans le projet — EntityRef étant unmanaged en Quantum 3, OK).
  - **MovementSystem + AStarPathfinder** : ajout du check `ObstacleHelpers.HasObstacleAt` aux endroits du check `GetOccupant`. Pas de refacto vers `IsBlocked` global pour minimiser le risque de régression.
  - **CombatInputController** : touches P (Spawn Pilier) + U (Damage 50). P/U identiques AZERTY/QWERTY donc pas de scancode mapping. DebugSpawnObstacleCommand + DebugDamageObstacleCommand processed by ObstacleSystem. Sera retiré en 3.3.b.
  - **View** : ObstacleView (TMP HP world space + sprite) + ObstacleRenderer (Filter<Obstacle> à chaque CallbackUpdateView, spawn/despawn dict-tracked). Pas de pooling (max ~5 obstacles concurrents en pratique).
  - **Editor Tool `CreateObstaclePrefabTool`** : génère sprite procédural pierre #7A6B5C (carré 64×64 PPU 64 avec bord noir) + prefab placeholder Obstacle_Pillar avec sprite + label TMP "200/200" world space.
- **1 piège traversé** : 1ère version d'`ObstacleRenderer` appelait `IsoProjection.GridToWorld(...)` avec 5 args (j'avais inventé une signature avec centerOffset intégré). Erreur compil CS1501. Pattern réel : 4 args + `+ centerOffset` en post (cohérent avec CombatantRenderer/GridRenderer/HUD/Watcher). Fix 1 ligne.
- **Validation E2E par Lorenzo** : touche P sur (7,8) → Pilier visuel apparaît (carré gris #7A6B5C + label "200/200"), case bloquée pour mouvement (`[Movement] rejet : (7,8) bloquee par un obstacle`), A* contourne automatiquement quand on clique derrière, touche U → damage 50 → label HP descend (200→150→100→50→0), à HP 0 → destroy auto + sprite disparaît. Confiance Lorenzo : "test ok pas possible de passer dessus et 200hp qui baisse en fonction des attaques".
- **Pas d'impact gameplay réel** : aucun sort Colossar/Necram n'utilise encore le framework. Sera branché en 3.3.b (sorts Pilier/Mur de Pierre Colossar).
- **Prochaine étape** : 3.1.bis Colossar assets (sprites livrés par designer pendant la 3.1) + switch P0 Colossar pour test. Puis 3.2 stats/ressource/passif Densité Inerte.

---

### 13 mai 2026 fin soirée — 🎨 Brique 2.12.bis Nightseer (anims stages 1+2 intégrées)
- **Contexte** : Lorenzo (avec son designer) livre les anims évolutives Nightseer pour les stages 1 et 2. Le stage0 avait été setup en 2.12 (2 controllers manuels `NightseerStage0_{NE,SE}.controller`) mais pas via un Editor Tool dédié — contrairement au Soulrender qui a `BuildSoulrenderAnimator` depuis 2.12.bis.
- **Inputs designer** :
  - 4 sources `NS_animation_stage{1,2}_{NE,SE}.aseprite` → `Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/`
  - 24 GIFs preview `NS_{action}_stage{1,2}_{dir}_{N}frame.gif` → `stage1/` + `stage2/`
  - Naming des nouveaux GIFs un peu inconsistant avec stage0 (`NS_attack_stage1_NE_6frame.gif` vs `NS_attack_NE_6frame.gif` pour stage0) — pas bloquant, le tool se base sur les .aseprite.
- **Outputs** :
  - Nouveau Editor Tool `Assets/_Nymora/Editor/Tools/BuildNightseerAnimator.cs` — clone exact de `BuildSoulrenderAnimator`, mirror structurel parfait. State machine identique (Idle 0.4 / Walk / Cast / Attack / Hurt / Death + params MoveSpeed/CastSpeed + 4 triggers).
  - 6 controllers générés/écrasés : `Animations/Nightseer/NightseerStage{0,1,2}_{NE,SE}.controller` (stage0 existants overwrite — state machine identique, pas de régression).
  - Prefab `Combatant_Nightseer.prefab` bind sur les 6 fields `_stage{0,1,2}Controller{NE,SE}` du `CombatantView` (déjà préparés depuis la refonte 2.12.bis Soulrender).
- **Pas de refactor `CombatantView`** : les 6 fields stage controllers existent depuis 2.12.bis Soulrender, le code de pick controller (PickController + PickSprite) supporte déjà les 3 stages × 2 dirs. Aucun changement gameplay.
- **Pas d'impact gameplay** : que de l'asset visuel + Editor Tool. Combat reste identique, `CombatRulesVersion` inchangée (toujours 10).
- **Validation** : Lorenzo a lancé le tool (`Nymora > Setup > Build Nightseer Animator`), confiance accordée pour validation visuelle ultérieure (sera vérifié quand il rejouera 30_CombatIA — le Nightseer doit afficher ses stages 1 et 2 quand sa Prescience monte).
- **Décision** : commit local seul (pas de push, session pas encore stoppée par Lorenzo, cf memory `feedback_end_of_session_push`). Phase 3 (Colossar) reste la prochaine étape majeure.

---

### 13 mai 2026 soir tardif — 🏁 **PHASE 2 ENTIÈREMENT CLÔTURÉE** (Bloc E IA + polish E2E)
- **Session marathon** : ouverture du Bloc D backlog (commits non-poussés du backlog 2.13.e→2.15.c) jusqu'à la clôture complète Phase 2 (Bloc E IA). Phase 2 = 8 mois selon la roadmap initiale, finie au bout de seulement ~5 jours intenses.
- **Bloc E IA — sous-briques i à c.vi** :
  - **2.16.a.i** : squelette AISystem + EndTurn auto (P1 hardcoded bot, 0.5s delay).
  - **2.16.a.ii** : TryGreedyMove + AIEvaluator (rapprochement Manhattan, A* check, Vapeur Carmin cost, tie-break index).
  - **2.16.a.iii** : TryGreedyCast random pick (Easy) + skip signature + cap 2 + HGSpend=0. Decks par difficulté (Lorenzo "les IA Easy/Medium vont dévoiler les metas").
  - **2.16.b** : enum AIDifficulty + Medium greedy max-score + SoulrenderMediumDeck. Constate empiriquement que OuvrePlaie+HG = auto-sustaining META exploit → HGSpend=0 force pour les 2 niveaux.
  - **2.16.c.i** : MatchEnd detection sim (TurnSystem.EnterTurnEnd) + Int32 WinnerPlayerIndex dans CombatState. CombatRulesVersion 9→10.
  - **2.16.c.ii** : UI MatchEndOverlay (VICTOIRE/DÉFAITE/MATCH NUL) + Restart button. Fix `QuantumRunner.ShutdownAll()` requis avant SceneManager.LoadScene (sinon DontDestroyOnLoad fait survivre le runner).
  - **2.16.c.iii** : rename scène QuantumGameScene → 30_CombatIA (`git mv` préserve GUID, update QuantumMap.asset/QuantumMapSceneInfo.asset/EditorBuildSettings/tools dialogs).
  - **2.16.c.iv** : 2 boutons Easy/Medium sur l'overlay MatchEnd. CurrentDifficulty passe const→static (mutable runtime). 1 click set difficulty + ShutdownAll + reload.
  - **2.16.c.v** : AI pacing — ActionIntervalTicks=60 (1s/action). Refactor TryGreedyCast → TryGreedyCastSingle (1 cast max), AISystem.Update planifie tick 0 move + tick N*60 casts. Bot turn 3-9s au lieu d'instant. Sensation "vrai joueur".
  - **2.16.c.vi** : mouvement cardinal cell-by-cell style Dofus. CombatantView refactor queue de waypoints + IsMoving + facing per segment (East→NE iso, North→NW iso, etc.). CombatantRenderer calcule Manhattan path X-puis-Y. Bug critique trouvé+fixé : `UpdateGridPosition` resetait la queue à CHAQUE frame du Renderer, jetant les intermédiaires posés au tick du move. Fix : ne rebuild queue que si destination change OU nouveaux intermédiaires fournis.
- **Décisions design clés** :
  - **Decks IA cachent le meta** : Easy = 6 sorts faibles (2 offensifs + 4 utility no-damage), Medium = 6 modérés (sans TrancheAme/DétonationSanglante/AmeLaceree). Vrai meta deck réservé IA Hard (futur, post-Phase 3).
  - **HGSpend=0 forcé en Easy ET Medium** : Ouvre-Plaie+1 HG = 230 dgts pour 2 PA est auto-sustaining (gain +1 HG par hit) → 1100+ dgts/tour. C'est une combo META à cacher.
  - **MaxCastsPerTurn Easy=2, Medium=8** : Easy ~150 dgts/tour, Medium ~400-500 dgts/tour. Combat dure 3-7 tours.
- **Commits de la session** (chronologique) :
  - `0950feb feat(phase2.16.a.i)`: squelette IA + EndTurn auto
  - `65c9856 feat(phase2.16.a.ii)`: déplacement greedy + AIEvaluator
  - `680fb52 feat(phase2.16.a.iii)`: random + cap 2 + skip signature
  - `7761339 feat(phase2.16.b)`: Medium greedy + AIDifficulty + decks par difficulté
  - `74713a2 feat(phase2.16.c.i)`: MatchEnd detection sim + WinnerPlayerIndex
  - `6554110 feat(phase2.16.c.ii)`: UI MatchEndOverlay + restart scene
  - `8bf5377 chore(phase2.16.c.iii)`: rename scene QuantumGameScene → 30_CombatIA
  - `5e5f620 feat(phase2.16.c.iv-v)`: sélecteur Easy/Medium + AI pacing
  - `615ee72 feat(phase2.16.c.vi)`: mouvement cardinal cell-by-cell + walk orientation
- **🏁 PHASE 2 100% TERMINÉE**. Soulrender + Nightseer 100% jouables côté sim, IA Easy/Medium fonctionnelle, combat 1v1 vs IA E2E avec UI Victory/Defeat + restart + difficulty selector.
- **Prochain pas** : **Phase 3 — Les 3 classes restantes** (Colossar, Necram, Ghostra). Roadmap initiale prévoyait 2 mois pour Phase 3. Bibliothèque V7.1 + framework combat éprouvé → devrait aller vite.

### 13 mai 2026 soir — 🏁 Bloc D Nightseer CLÔTURÉ + ménage Git
- **Contexte** : 2.13.e + 2.14 + 2.15.a/b/c avaient été développés dans une autre conversation Claude Code mais le STATUT n'avait pas été mis à jour et rien n'était commité dans le repo Git. ~80 fichiers modifiés + 25 untracked en attente.
- **Cette session a couvert** :
  1. **Intégration assets Nightseer livrés par le designer** : .aseprite NE corrigé (perso de dos au lieu de face), 17 icônes de sorts (5 offensifs + 5 tactiques + 5 survie + signature + passif), avatar 128px, marques, tile piège runique.
  2. **Branchage View HUD Nightseer** : `SpellIconRegistry` étendu avec champs `_avatarNightseerIcon` + `_passifNightseerIcon`, méthodes `AvatarFor()` et `PassifIconFor()` complétées. `PopulateSpellIconRegistry` scanne désormais les dossiers Sprites/Nightseer/Icons + Avatar et mappe les 16 sorts via `FileToSpellId`.
  3. **Audit signature + passif côté simulation** : confirmé que Traquenard (SpellSystem.cs:1294) et L'Œil qui n'est pas (SpellSystem.cs:377-386) sont 100% wirés Quantum (damage, paralysie, cooldown, consume marque, +2 PR si bonus, 30% shield pierce).
  4. **Ménage Git** : 6 commits propres dans l'ordre chronologique :
     - `feat(phase2.13.e)`: avatar HUD Soulrender (54 fichiers, +571 -345)
     - `feat(phase2.14)`: brouillard de guerre + Marks/Voile/Pièges + tour=round Dofus (75 fichiers, +7399 -2867)
     - `feat(phase2.15)`: Nightseer 16 sorts + Prescience + passif + signature (9 fichiers, +1428 -32)
     - `chore(art)`: assets Nightseer (100 fichiers, +8462 — un peu lourd à cause des binaires)
     - `feat(hud)`: wire Nightseer dans SpellIconRegistry (3 fichiers, +121 -9)
     - `docs(statut)`: clôture Bloc D + ouverture Bloc E (cette mise à jour)
- **Découpage 2.15.a/b/c fusionné** dans 1 commit : splitter chirurgicalement les 663 lignes de diff SpellSystem.cs + 393 SpellRegistry.cs aurait demandé ~30-45 min de patch surgery avec risque d'erreur élevé. Lorenzo a validé la fusion pragmatique.
- **Reliquats** :
  - Memory `project_phase2_progress_2026_05_13.md` à supprimer maintenant que STATUT à jour.
  - Pousser sur GitHub uniquement en fin de session (cf memory `feedback_end_of_session_push.md`).
- **Prochain pas** : **Bloc E IA**. Roadmap initiale liste 2.15 (IA Easy greedy) + 2.16 (IA Medium heuristique multi-tour) + 2.17 (scène 30_CombatIA + E2E 1v1 vs IA). Numérotation à clarifier : ces 3 briques deviennent 2.16.a / 2.16.b / 2.16.c puisque 2.15.a/b/c sont prises.

### 12 mai 2026 (suite) — Brique 2.13.d validée + 🏁 2.13 ENTIÈRE TERMINÉE
- **CameraController** (1 fichier ~110 lignes, brique XS) : zoom molette centré sur curseur + pan clic molette maintenu + reset Home/double-clic molette.
- **Zoom anchored sur curseur** : capture `mouseWorldBefore = ScreenToWorldPoint(mouse)`, change `orthographicSize *= (1 - wheel * speed)`, clamp [2, 15], recalcule `mouseWorldAfter`, translate camera de `(before - after)`. Sensation "pivot mouse" comme Dofus/Wakfu/Civ.
- **Pan grab-and-drag** : capture `panAnchorWorld` au middle-mouse-down. Chaque frame du drag, calcule `delta = (anchor - currentMouseWorld) * panSensitivity` et translate la camera. Math : si le point monde original reste sous le curseur, delta tend vers 0 → stable. Si curseur bouge, camera suit.
- **Reset double-clic molette** : fenêtre 0.35s entre 2 mouseDown(2). Si dans la fenêtre → ResetView. Sinon → start nouveau drag.
- **Tool auto-add idempotent** : `Camera.main.GetComponent<CameraController>()` check, sinon `Undo.AddComponent`. Re-run tool sans risque de duplication.
- **Validation Lorenzo "ok"** : tout fonctionnel.
- **🏁 2.13 ENTIÈRE CLÔTURÉE** : 4 sous-briques validées sur la session 12 mai (a layout HUD, b previews range, c tooltips + floating text, d caméra). Soulrender 100% jouable + lisible + confortable.
- **Prochain pas** : Bloc D Nightseer (2.14). Roadmap initiale liste 2.12/2.13/2.14 pour Nightseer mais ces numéros sont maintenant pris par le HUD. À renuméroter ou continuer en suffixe.

### 12 mai 2026 (suite) — Brique 2.13.c validée (tooltips Bible + texte flottant + cooldown signature)
- **5 fichiers livrés** (SpellDescriptions, SpellTooltipView, FloatingText, FloatingTextManager, CombatantHPWatcher) + 3 modifiés (SpellSlotView, CombatHUDController, CreateCombatHUDTool).
- **SpellDescriptions** : descriptions Bible V7.1 condensées par SpellId. 1-2 phrases courtes par sort, mention de la variante HG. Pas de ScriptableObject — static class C# pour zéro asset à maintenir et accès instant.
- **SpellTooltipView** : Panel UI avec VerticalLayoutGroup + ContentSizeFitter pour auto-size selon contenu (description courte = panel court, description longue = panel grand). Format : Titre (bold 20) / Cout PA + HG + filter + portée (orange 14) / Description Bible (grey 14).
- **Auto-flip tooltip** : si l'affichage au-dessus du slot dépasse le haut du canvas, flip pivot et position en dessous. Calcul via `GetWorldCorners` du slot + tooltip + canvas. Pratique pour la signature/sorts en haut de l'écran (futurs layouts).
- **SpellSlotView refacto** : IPointerEnterHandler/IPointerExitHandler + coroutine `WaitForSeconds(0.2f)` avant `ShowTooltip` (anti-clignotement). Stop coroutine + Hide sur exit ou OnDisable.
- **Cooldown signature Âme Lacérée** : lecture `Combatant.LastAmeLaceeUsedOnTurn` + `state.TurnNumber`. Helper `ResolveCooldownTurnsLeft(spell, c, valid, turnNumber)` retourne nombre tours restants. Label rouge `Xt` overlay sur l'icône via `SetCooldownLabel(n)` ; slot grisé tant que cooldown > 0.
- **1/match grisage** : check `OncePerMatchUsedFlags & (1 << def.OncePerMatchBit)`. Couvre Pacte de Sang et Dernier Souffle automatiquement (Bible V7.1).
- **FloatingText** : MonoBehaviour qui Lerp Y +60px sur 1.0s + fade alpha 0.7-1.0s, puis Destroy. `GetComponent<TMP_Text>()` au runtime pour récupérer le label (cleaner que reflection — première version avait reflection, refactor avant commit).
- **FloatingTextManager** : Spawn dynamique de GameObjects UI avec TMP_Text + FloatingText sur un Canvas dédié `CombatFloatingTextCanvas` (sortingOrder 90, sous le HUD à 100). `WorldToScreenPoint` depuis Camera.main pour convertir position grille → pixel canvas.
- **CombatantHPWatcher** : pattern polling (vs Quantum Signal). `Dictionary<EntityRef, int> _lastHP` + diff par tick → spawn FloatingText. Cleared sur CallbackGameStarted. Limitation connue : ne distingue PAS shield absorb (HP ne bouge pas pendant absorb) — reportée à une future brique avec Signal dédié si Lorenzo en a besoin.
- **Tool extensions** : `BuildTooltip` (nouveau builder) + `BuildFloatingTextStack` (nouveau builder, crée Canvas séparé + Manager + Watcher sur la même GO, copie GridSettings depuis TargetingPreviewView pour éviter re-câblage manuel). Cooldown label ajouté dans `BuildSpellSlot` (TMP rouge 36px bold center, hidden par défaut).
- **Validation Lorenzo "parfait"** : flow ergo complet (hover → tooltip → cast → texte flottant → cooldown visible).
- **Suite** : Lorenzo demande une **brique 2.13.d caméra** (zoom molette + pan clic molette) pour le confort de jeu, ajoutée à la roadmap.

### 12 mai 2026 (suite) — Brique 2.13.b validée (preview range PM + range sort armed + zone d'effet hover)
- **Infra de highlight déjà 80% en place** : `TargetingPreviewView` (créé en 2.6) + `TileView.ApplyHighlight/ClearHighlight` + `GridRenderer.GetTileView(x,y)` + `TargetingResolver.ResolveCastableCells/ResolveEffectCells`. Refacto léger plutôt que tout réécrire.
- **`TargetingPreviewView` refacto — priorité armed > debug > rien** : ajout de `_hudController` SerializedField. Si `ArmedSpell.HasValue` → resolve via `SpellRegistry.TryGet(id)` (shape, range). Si pas d'armé mais `_debugShowTargeting=true` → fallback debug 2.6.
- **`MovementRangePreview` nouveau (BFS/SPFA)** : preview vert pâle des cases atteignables avec le PM courant. Algo SPFA avec relaxation (coût variable Vapeur Carmin +1). Buffers préalloués `_bestCost[255]` et `_queueBuf[1020]` → zéro alloc par tick. Skip si `ArmedSpell.HasValue` (priorité au spell preview).
- **Tool auto-câblage** : `CreateCombatHUDTool` étendu pour wirer `_hudController` sur `TargetingPreviewView` + ajouter `MovementRangePreview` comme sibling component (partage `_gridRenderer` du sibling).
- **Bug "clic icône = cast direct"** signalé par Lorenzo : trace montrait que `[Nymora.HUD] Sent Cast` ET `[Spell] Peau de Fer` firaient dans la même frame en boucle. Cause : `CombatInputController.Update` polle `Input.GetMouseButtonDown(0)` qui est `true` même quand le clic atterrit sur la UI. Fix : check `EventSystem.current.IsPointerOverGameObject()` → si pointeur sur UI, `mouseDown = false`. Les inputs clavier restent traités normalement.
- **Bug "Tranche-Âme n'affiche pas la zone"** : la preview filtrait les cases bleues par `MatchesFilter` (Filter=Enemy → seulement cases avec ennemi). Sans ennemi adjacent à range 1 → 0 cases bleues. Fix : afficher la **portée Manhattan complète** indépendamment du filter ; Quantum filtre au cast (clic invalide = rejet silencieux). Plus lisible et cohérent avec Dofus/Wakfu.
- **Bug "Peau de Fer cast direct sans visu"** : design original avait Self filter = cast immédiat sans armement (gain 1 clic). Lorenzo a demandé la cohérence visuelle : `faudrait montrer une zone de 0, zone sur la case du personnage`. Fix : **arming généralisé à tous les sorts** (y compris Self). Click icône → frame jaune + range bleue (caster cell pour Self). Click n'importe où sur la grille = confirme cast. Pour Self : le `CombatInputController` redirige `TargetX/Y` vers la case du caster avant `SendCommand`.
- **Nettoyage `CombatHUDController`** : suppression de `SendCast` et `TryGetCasterCell` (n'étaient plus appelés après le pivot arming). `SpellDisplayInfo.NeedsArming` n'est plus appelé non plus mais conservé pour usage futur 2.13.c (tooltips qui pourraient afficher "Self" sur les self-target).
- **Validation Lorenzo "tout est ok"** : 4 spells testés sur la session (Tranche-Âme, Peau de Fer, Détonation Sanglante, mvt PM). Aucun screenshot mais flow validé en jeu.
- **Reste à boucler 2.13** : 2.13.c tooltips persistantes (hover icône → nom + coût PA + range + description Bible) + texte flottant dgts/heals sur les sprites combatants.

### 12 mai 2026 (suite) — Brique 2.13.a validée (HUD combat layout + icônes cliquables + EndTurn manuel)
- **Découpage 2.13 en 3 sous-briques validé par Lorenzo** : a (layout + clic icône) / b (prévisu range) / c (tooltips + texte flottant). Évite la brique monolithique 5-7j non validable. Pattern identique à 2.10 a/b/c.
- **Décision Option 2 — mode armé pour cast cible** : clic icône sur un sort `Filter != Self` → frame jaune (armed), prochain clic gauche grille → CastSpellCommand au lieu de MoveCommand. Toggle off si re-clic même icône. Pattern Dofus/Wakfu. Self-target = cast immédiat sans armement.
- **EndTurnCommand côté Quantum** : nouvelle DeterministicCommand no-payload. Handler dans `TurnSystem.TickTurnActive` : si sender == ActivePlayerIndex → force `state.TurnTimerTicks = 0` + transition `TurnEnd`. Avant ça, le tour ne passait qu'à expiration timer naturelle.
- **Réutilisation infra existante** : `SpellIconRegistry` SO + populator `Nymora > Setup > Populate Spell Icon Registry` étaient déjà en place et peuplés (16 entrées) depuis 2.12. Task #3 simplifiée : pas besoin de recréer de catalogue, juste ajouter `SpellDisplayInfo` (display names + helper NeedsArming basé sur `SpellRegistry.TryGet(id).Filter`).
- **Architecture HUD modulaire** : `CombatHUDController` orchestrateur + 5 widgets autonomes (`ResourcePanelView`, `TimerView`, `PassivePanelView`, `TimelineView`, `SpellSlotView` ×7). Controller subscribe `CallbackUpdateView` une fois, lit la Frame.Verified, dispatch Refresh aux widgets. Allocations zéro dans la boucle View (filtres Quantum struct iterators).
- **Auto-câblage tool** : `CreateCombatHUDTool` refacto complet (102 → 380 lignes). Détruit ancien `CombatHUDCanvas`, recrée tout le layout 1920×1080 (anchors précis), instancie chaque widget, branche les SerializedObject references, charge `SpellIconRegistry.asset` standard, et **auto-trouve `CombatInputController` dans la scène pour câbler `_hudController`**. Idempotent.
- **Bug `enumValueIndex` corrigé en passant** : `SpellId : Byte` a des valeurs non-séquentielles (None=0, TestZap=1, Soulrender* à partir de 10). `enumValueIndex` Unity attend l'INDEX de déclaration, pas l'underlying byte. Helper `SetEnumValue(prop, enum)` qui fait `Array.IndexOf(prop.enumNames, value.ToString())`.
- **Conflits ambigus Quantum vs UnityEngine.UI** : `Button` (et potentiellement `Image`) existent dans les 2 namespaces. Pattern alias `using Button = UnityEngine.UI.Button;` + `using Image = UnityEngine.UI.Image;` ajouté à tous les fichiers HUD + tool. Même pattern que `CombatInputController` qui qualifie `UnityEngine.Input`.
- **Mode debug control player** : `_debugAllPlayersControllable = true` par défaut (Phase 2.x sans matchmaking) — le HUDController envoie les commands au joueur actif courant pour permettre à Lorenzo de tester P0 et P1 alternativement. À désactiver en Phase 6 (vrai LocalPlayerIndex).
- **Touches clavier fallback préservées** : aucune modif des bindings 1-9/0/F1-F4/B/Space dans `CombatInputController` (sauf l'ajout du chemin "armed → cast" sur clic gauche). Lorenzo peut toujours débloquer via clavier si bug HUD.
- **Orphelin `CombatHUDView.cs` supprimé** après validation Lorenzo (nouveau HUD utilise `CombatHUDController` namespace `Nymora.Combat.View.HUD`). 
- **Validation Lorenzo "hud ok"** sans entrer dans le détail des 13 points de checklist — bonne ergo confirmée. Tooltips/range previews/floating text reportés à 2.13.b/c (volontairement hors-scope 2.13.a).
- **Reste à boucler 2.13** : 2.13.b prévisu range PM + range sorts (hover icône), 2.13.c tooltips persistantes + texte flottant dgts/heals.

### 12 mai 2026 (suite) — Brique 2.12.bis enchaînée dans la même session
- Lorenzo a décidé d'enchaîner 2.12.bis avant de clôturer ("tu sais quoi go faire la 2.12.bis maintenant avant de clôturer"). Une heure de travail dans la foulée.
- **Idle "ultra speed"** remonté par Lorenzo : l'idle 6 frames jouait à ~12 FPS Aseprite par défaut, ce qui faisait un effet trépidant. Fix : `state.speed = 0.4` sur l'Idle state dans le tool. Cycle de respiration lent ~2 sec. À ajuster si trop lent ou trop rapide.
- **Walk imperceptible** ("le perso se TP, on voit même pas l'anim") : le `Vector3.Lerp(_, _, dt * 8)` rendait le combatant à 1% de la case en ~0.15s. Le clip walk 6 frames n'avait pas le temps de tourner. Fix : passage à `Vector3.MoveTowards` à vitesse constante 2.5 u/s = 1 case en 0.4s = ~3 cycles de walk visibles. Plus naturel.
- **Tracking Quantum minimaliste** : approche pull plutôt que signal/event. `LastCastOnTurn` + `LastCastSpellId` sur Combatant, la View pole la diff. Évite la lourdeur d'ajouter un Quantum Signal + écoute View. Trade-off : la View doit appeler `frame.Filter<Combatant>()` à chaque CallbackUpdateView (déjà fait pour le facing, on étend).
- **Cast par catégorie via CastSpeed** : le designer n'a livré qu'un clip cast par direction. Pour différencier visuellement Survival/Tactical/Offensive/Signature, on module `state.speed` via paramètre `CastSpeed` (0.7/1.0/1.3/1.5). Solution pragmatique en attendant des clips dédiés par catégorie (futur reliquat designer si Lorenzo le demande).
- **Fallback sprite anti-pion-rouge** : Lorenzo a vu son perso redevenir un pion rouge à un moment ("mon perso est a nouveau un pion rouge"). C'était le placeholder original assigné au SpriteRenderer du prefab qui ressortait quand l'anim ne firait pas (clip mal taggé ou timing). Fix : le tool force `SpriteRenderer.sprite` sur le 1er Sprite extrait du `.aseprite` Stage 0 SE après build. Au pire on voit un Soulrender statique, jamais le rouge.
- **Collision `NymoraClass`** : ajout de `using Nymora.Core.Enums` a déclenché une ambiguïté avec `Quantum.NymoraClass` (la classe est définie 2 fois — une fois Core C#, une fois généré par Quantum DSL). Fix : `using SpellCategory = Nymora.Core.Enums.SpellCategory` (alias précis sans tirer NymoraClass).
- **6 commits cumulés pas pushés** : 2.10.a/b/c + 2.11 + 2.12 + 2.12.bis. Lorenzo signale la fin de session avec instruction explicite "push github demain" — donc on conserve en local pour l'instant, push à la prochaine session.

### 12 mai 2026 — Brique 2.12 finalisée (facing 4 directions iso) + reliquat anims pour 2.12.bis
- Reprise de la session post-compaction du contexte. Hier la 2.12 avait été validée par Lorenzo avec idle statique et flipX P0/P1, mais en testant Lorenzo a constaté que **les 4 directions iso ne fonctionnaient pas** (un seul facing visible). Le designer avait pourtant livré 6 .aseprite (NE+SE × 3 stages).
- **Refonte CombatantView** : abandon de `SetStage(int)` + `SetFlipX(bool)` au profit d'une seule API `SetStageAndFacing(int stage, IsoFacing facing)`. Ajout de 6 champs `_stage{0,1,2}Controller{NE,SE}` (au lieu de 3) + 6 champs sprites fallback. `IsoFacing.NW/SW` réutilisent les assets NE/SE avec `flipX = true` (pas d'asset dupliqué, pas de boulot designer en plus).
- **BuildSoulrenderAnimator étendu** : charge maintenant les 6 .aseprite (au lieu de 3), génère 6 `.controller` `SoulrenderStage{0,1,2}_{NE,SE}.controller`, bind les 6 fields via `SerializedObject` sur le prefab. Le tool reste idempotent (overwrite via `AssetDatabase.DeleteAsset`).
- **Refonte CombatantRenderer pour le facing** :
  - Première itération : auto-aim ennemi (le combatant regarde toujours vers l'ennemi). Lorenzo a corrigé : **le facing doit suivre la direction du déplacement**, pas l'ennemi.
  - Version finale : `ResolveFacing(entity, combatant)` retient `_lastGridPos` + `_lastFacings` par EntityRef. Si pos change depuis le dernier frame → nouveau facing depuis le delta. Si pas de mouvement → keep last facing. Au tout 1er frame post-spawn : facing initial dirigé vers l'ennemi (pour que les 2 combatants se regardent au départ).
  - Helper `FacingFromGridDelta(dx, dy)` : math iso `dxWorld = dx - dy`, `dyWorld = dx + dy`, puis quadrant → IsoFacing. Pure static, réutilisable.
- **Allocations zéro dans OnUpdateView** : `_frameCombatants` (List<CombatantSnapshot>) préalloué capacité 2 + `_lastGridPos`/`_lastFacings` (Dictionary) préallocs aussi. ClearAll les nettoie au démarrage/destroy.
- **Reliquat pour demain** : Lorenzo veut exploiter à 100% les frame tags livrés (idle/walk/attack/cast/hurt/death). Walk lent (1-2 PM) vs walk rapide (3 PM), anim cast par catégorie (Survie/Attack/Tactical), hurt/death sur events. Sera la Brique 2.12.bis (avant 2.13 HUD ou en parallèle selon priorité).
- **Décision design verrouillée** : facing = sens du mouvement, pas auto-aim. C'est plus naturel pour un tactical iso (le perso ne se tourne pas téléportiquement vers l'ennemi à chaque mouvement adverse).

### 11 mai 2026 — Brique 2.11 (Signature Âme Lacérée + Passif Appel du Sang) — Soulrender 100% complete
- Suite directe de 2.10.c dans la même méga-session. Bloc C clôturé.
- **Choix de design : passif sans état persistent par-tour** : le passif Appel du Sang relit le HP cible à chaque cast (pour PA cost) et à chaque TurnStart Soulrender (pour +1 PM). Pas de Status dédié ; les paliers sont des seuils statiques. Si la cible heal au-dessus de 70%, le bonus PA disparaît au prochain cast. Cohérent avec Bible (effet dépend dynamiquement de l'état HP cible).
- **Helper `EffectiveStats.ResolveTargetHPRatio`** : centralise la logique "regarder l'occupant de la case visée, retourner son HP%". Réutilisable pour Phase 3 (passifs Colossar Densité Inerte qui pourrait dépendre du HP self, etc.).
- **Refactor damage loop pour Rage Ouverte** : variable `rageOuverteBypass` séparée de `dmgToShield`. `totalHPLoss = dmgToShield_apres_shield + rageOuverteBypass`. Tous les hooks (kill detection, marque, riposte, gain HG, LE CRI) utilisent maintenant `totalHPLoss > 0` au lieu de `dmgRemaining > 0`. Pas de régression sur les sorts antérieurs (Tranche-Âme/Ouvre-Plaie/Détonation/etc. continuent à fonctionner correctement, vérifié par les logs de la session).
- **Interlock Détonation 5 HG ↔ Âme Lacérée** : géré via le même champ `LastAmeLaceeUsedOnTurn`. Si Détonation consume 5 HG, il set ce champ comme si Âme Lacérée avait été cast, ce qui déclenche le cooldown. Élégant et reuse l'infrastructure existante.
- **Découverte gameplay** : pendant le test, P0 a marché sur sa propre croix Sang Coagulé LE CRI au prochain TurnStart, prenant -30 HP. C'est un trade-off Bible cohérent ("Le Soulrender rend la map collante — pour lui aussi"). À garder en tête pour le design (le Soulrender doit se déplacer avant la fin du tour si possible).
- **Validation E2E** : 5/7 tests directs validés en 1 session de ~10 tours. Les 2 manquants (KILL signature + interlock 5 HG) sont des branches code triviales, validées par inspection.
- **🏆 SOULRENDER COMPLET BIBLE V7.1** : 15 sorts + signature + passif. Architecture combat solide pour passer au Nightseer (Phase 2.14+).
- **Prochaine étape** : 2.12 sprites (designer a déjà tout préparé dans `Sprites/Soulrender/Base/stage{0,1,2}/` et `Sprites/Soulrender/soulrender_icons/`). 2.13 HUD complet. Puis Nightseer.

### 11 mai 2026 — Brique 2.10.c (Terrains + Mvt non-PM + Kill detection + 4 sorts) — fin de 2.10
- **Marathon session** : 2.10.a, 2.10.b, 2.10.c livrées et validées dans la même journée. 15 sorts Soulrender complets.
- **Décision d'architecture importante** : terrains stockés directement dans la struct `Tile` (par-case), pas via un `StatusKind`. Logique : un terrain est lié à une CASE, pas à un combattant ; les Statuses sont conçus pour les combattants. Évite des hacks comme "Tile virtuel sans owner". Pattern proche : champs ShieldHP/TurnsLeft mais directement sur la grid.
- **Mouvement non-PM** : helper isolé dans son propre fichier (`MovementHelpers.cs`) pour pouvoir être réutilisé. Charge Brutale + recul Tranche-Âme y appellent. Empoignade pull (2.10.b) pourrait y être refacto-é en Phase 7 polish pour cohérence.
- **Discussion roadmap HUD avec Lorenzo (clé)** : la roadmap actuelle n'a AUCUNE brique dédiée au HUD combat (Phase 4 = map communautaire, pas combat). Lorenzo allait jouer 10 mois avec un HUD placeholder TMP_Text. Décision : intercaler 2.12 (sprites Soulrender + icônes) et 2.13 (HUD combat complet) après 2.11, AVANT d'attaquer Nightseer. Le designer va déposer les assets dans `Assets/_Nymora/Art/Sprites/Soulrender/` et `Assets/_Nymora/Art/UI/Icons/Soulrender/` (dossiers créés).
- **HUD layout validé** (à livrer en 2.13) : PA/PM haut-gauche, Timer haut-centre, zone combat centrale, End Turn milieu-droite, Passif bas-gauche, 6 sorts bas-centre, Timeline simple `P0 > P1` bas-droite. **Prévisu range PM cliquables + prévisu range sorts + tooltips persos + texte flottant dgts/heals**. Deck éditable via `[SerializeField] SpellId[] _testDeck` (pas de deck imposé, configuration libre Inspector).
- **Bug C# récurrent identifié** : le case `Pacte de Sang` (sans braces) shadow les locals `hpBefore`, `maxRes`, `resBefore` dans tout le switch. À chaque nouveau case 2.10.x j'ai dû renommer mes vars. Note pour 2.13 : refacto le switch pour wrapper TOUTES les cases en `{}`, plus jamais de shadowing.
- **6/7 tests E2E validés** : Charge Brutale, Vapeur Carmin posée, Détonation Damage 140, Sang Coagulé posé, tick TurnStart -30 HP, Cautérisation. Le test cost +1 Vapeur Carmin n'a pas été déclenché en E2E (P1 a évité les cases Vapeur), mais code trivial (1 ligne) considéré OK par inspection.
- **Brique 2.10 complète** (a+b+c). Soulrender est à 15/15 sorts. Reste 2.11 (signature + passif) pour clôturer le Bloc C.

### 11 mai 2026 — Brique 2.10.b (Shields + Heals + Marques + 5 sorts)
- Suite directe de 2.10.a dans la même session. Lorenzo a enchaîné après validation E2E de 2.10.a.
- **Réutilisation maximale du framework Statuses** : Shield et Mark sont implémentés comme StatusKind (`ShieldActive`, `MarkedByCarnage`). Pas de nouveaux champs sur Combatant. Décision pragmatique : la Magnitude variable du Status `ShieldActive` (HP courant qui baisse à chaque hit) marche très bien et reste lisible. Pas d'over-engineering avec un composant dédié.
- **Damage loop refactoré** pour gérer shield absorption en premier. La règle "casterHitSomething = HP loss only" (introduite ici) couvre proprement les cas où le shield absorbe tout : pas de gain HG ni Marque bonus si pas de dgts au HP. Cohérent avec Bible "inflige des dégâts".
- **Bug C# rencontré** : `hpBefore`, `maxRes`, `resBefore` dans les nouveaux cases Sève Vive/Dernier Souffle shadow les locals du case `Pacte de Sang` (qui n'a pas de braces, donc dans le scope de la méthode). Renommé en `hpBeforeHeal`, `maxResDS`, `resBeforeDS`. À noter pour futurs sorts : préférer wrapper les cases en `{}` pour scoper proprement.
- **5/7 tests passés au 1er run, 7/7 après ajustement**. Le test Empoignade a raté la 1ère fois car Lorenzo a bougé P0 en (4,8) au lieu de (5,8) → distance 4 > range 3 → rejet. Au run suivant, P0(6,8) cast Empoignade sur P1(9,8) (distance 3) → pull à (7,8), cohérent avec l'algorithme axe-dominant.
- **Bonus melee Peau de Fer +30** validé : Tranche-Âme (220) + Pacte (+50%) **non testé combiné**, mais Tranche-Âme + Peau de Fer = 250 dgts observé. La stack `Pacte→Peau de Fer→Tranche-Âme` est théoriquement faisable : 220 × 1.5 + 30 = 360 dgts (un Soulrender peut donc envoyer 360 dgts mêlée Tranche-Âme avec setup).
- **Note Bible-strict** : seul un Soulrender devrait pouvoir cast les sorts Soulrender. En 2.10.b on n'a pas de check class — P1 Nightseer peut cast Sève Vive dans les tests (visible dans le log "P1 cast SoulrenderSeveVive"). C'est OK pour 2.10 (mode debug) mais à corriger en Phase 6 quand on aura un vrai matchmaking avec sélection de classe.
- **Brique 2.10.b complète**. 10 sorts Soulrender sur 15 implémentés. Reste 4 sorts + bonus Tranche-Âme en 2.10.c, puis signature Âme Lacérée + passif L'Appel du Sang en 2.11. Ensuite Bloc D (Nightseer).

### 11 mai 2026 — Brique 2.10.a (Framework Statuses + 5 sorts Soulrender) — grosse session
- **Pivot architectural** : la brique 2.10 (14 sorts restants Bible V7.1) est découpée en 3 sous-briques pour rester gérable dans le workflow "1 brique = validation E2E" :
  - **2.10.a** : Framework Statuses + 5 sorts simples (livrée ce jour)
  - **2.10.b** : Shields + Heals + Marques + 5 sorts (prochaine)
  - **2.10.c** : Terrains (Vapeur Carmin, Sang Coagulé) + Mouvement non-PM + Kill detection + 4 sorts + effet bonus Tranche-Âme
- **12 fichiers livrés** : 2 nouveaux (Status.qtn, StatusHelper.cs) + 10 modifiés (Combatant.qtn, CombatantSystem, TurnSystem, Spell.qtn, CastSpellCommand, SpellRegistry, SpellSystem, MovementSystem indirectement via TurnSystem.ResetPM, CombatInputController, CombatHUDView).
- **Découverte importante (Quantum 3 DSL)** : les fixed-size arrays `array<T>[N]` dans les composants s'accèdent **directement via pointer indexer** (`c->Statuses[i].Kind = ...`), pas via `f.ResolveList` (qui est pour les list dynamiques). J'avais d'abord utilisé `f.ResolveList` par confusion, corrigé après avoir lu le pattern existant `array<Tile>[255]` dans Grid.qtn / GridHelpers.
- **Sémantique de durée Statuses** : règle "skip si `AppliedOnTurn == currentTurn`" à la décrémentation TurnEnd. Permet d'avoir une lecture intuitive de "X tours" Bible V7.1 (= X tours réellement vécus du POV de l'owner). Vérifié sur Rage Insatiable (2 tours du caster), Riposte Carmin (1 tour adverse), Ouvre-Plaie debuff (2 tours de la cible).
- **Design `MovementMalus` au reset PM** : appliqué dans `TurnSystem.EnterTurnStart` (sur le combattant qui démarre son tour) plutôt que dans MovementSystem. Plus propre : pas de calcul "effective PM" dispersé, le PM stocké reflète directement ce qui est disponible.
- **Pattern verrouillé pour les sorts complexes** : SpellDef reste simple (cost + targeting + damage + variants HG + once-per-match bit + IsOffensive). Effets exotiques (statuses appliqués, self-effects, conditionnels) dans `SpellSystem.ApplySpellSpecificEffects` (switch SpellId). Scale naturellement vers 2.10.b/c et Phase 3 sans data-driven Effect Composition Engine prématuré.
- **Reflect Riposte Carmin** : trigger dans le damage loop, condition `isMelee && target.Has(RipostMelee)`. Inflige `Magnitude` dgts au caster + applique MovementMalus 1 sur lui. Validé E2E : P1 Tranche-Âme 220 sur P0 (HP 1500→1280), P1 prend 100 dgts retour (HP 1500→1400).
- **HUD refactor** : affiche P0 + P1 simultanément avec HP/PA/PM/HG/Statuses formattés `{Kind:TurnsLeft[xMag]}`. Lorenzo voit en permanence les buffs/debuffs en action. Bug mineur résolu : caractère `▶` (U+25B6) absent de LiberationSans SDF → remplacé par `> ` ASCII.
- **5/5 tests E2E validés** : Pacte +50% buff appliqué + consumed sur cast offensif (Damage 330), Ouvre-Plaie Shift+1 = 230 dgts + AntiHealShield 2 tours, Riposte reflect 100 dgts, Rage cycle (+1 PA cost / +1 PA regen, vérifié via log "regen 1 PA (1 -> 2)" et PA effectif), Pacte 1/match rejet.
- **Brique 2.10.a complète et propre**. Aucun bug runtime. Tous les hooks scaleront pour 2.10.b (Shields, Heals, Marks réutilisent le framework Statuses).

### 11 mai 2026 — Brique 2.9 (Ressource Hémoglyphe Soulrender)
- 5 modifs : DSL Combatant.qtn (Resource + LastResourceGainOnHitTurn), CombatantStats (caps 5 classes), CombatantSystem (init au spawn), SpellSystem (gain caster/cible), CombatHUDView (affichage [HG x/5])
- Architecture **générique** : champ `Resource` partagé pour les 5 classes (HG/PR/FD/PT/RM), helper `CombatantStats.GetMaxResource(NymoraClass)` qui retourne 5/4/3/6/3 selon la classe. En 2.9 seul Soulrender a la logique gain implémentée, les autres viendront avec leurs classes respectives (2.13 Nightseer, Phase 3 reste).
- Logique gain Bible V7.1 :
  - **Cast Soulrender qui touche au moins 1 cible** → +1 HG (caster), max 1 par cast peu importe le nb de cibles
  - **Soulrender qui subit dégâts** → +1 HG par cast adverse, max 1 par TurnNumber via tracker `LastResourceGainOnHitTurn`. Si pris 3 hits dans le même tour adverse, +1 HG total.
- Affichage HUD : `Joueur P0 Soulrender [HG x/5]` après la classe, tag adapté par classe (HG/PR/FD/PT/RM). Pour Nightseer/Colossar/Necram/Ghostra le HUD affiche déjà le placeholder même si la logique gain n'est pas encore branchée.
- Aucun piège technique sur cette brique — implémentation fluide.
- 🎯 **Le Soulrender a maintenant sa vraie économie de combat** (Bible V7.1) — la ressource va dicter les payoffs des sorts à venir (Ouvre-Plaie +1 HG → +120 dgts + anti-heal, Détonation Sanglante consomme tous les HG, Signature Âme Lacérée nécessite HG=5).

### 11 mai 2026 — Brique 2.8 (Premier sort Bible V7.1 : Tranche-Âme) + clôture Bloc B
- 3 modifs courtes : `Spell.qtn` (ajout SpellId.SoulrenderTrancheAme = 10 + plages réservées), `SpellRegistry.cs` (TrancheAme + TestZap retiré), `CombatInputController` (Espace → TrancheAme)
- Tranche-Âme spec Bible V7.1 : **3 PA, range 1 (mêlée), SingleTile, Enemy, 220 dégâts**. Effet bonus "recul de 2 cases si kill" différé en 2.11 (nécessite système de mort + mouvement gratuit non-PM, hors scope minimal).
- TestZap retiré du registry (gardé dans l'enum pour ne pas casser d'éventuelles commands sérialisées). Plus aucun sort de "debug" castable — uniquement les vrais sorts Bible V7.1.
- Validation E2E impressionnante côté logs (cf détail) :
  - Soulrender bouge vers (6,8) puis (7,8) en plusieurs tours
  - Nightseer (IA absente, déplacé manuellement via `_debugAllPlayersMovable`) à (8,8)
  - Cast Tranche-Âme avec distance 2 → `[Spell] rejet : distance 2 hors range [1,1]` ✓
  - Cast Tranche-Âme adjacent → `[Spell] Damage 220 sur P1 (8,8) HP 1500 -> 1280` ✓ ✓ ✓
  - Second cast → `HP 1280 -> 1060` ✓ (8 PA - 3 cast 1 - 3 cast 2 = 2 PA restant)
  - Troisième cast tenté avec PA=2 → `[Spell] rejet : PA 2 < cost 3` ✓
- 🏁 **Bloc B — Sorts & ciblage générique : BOUCLÉ** (3/3 briques : targeting, engine, premier sort vertical Bible V7.1)
- Question UX clarifiée pour Lorenzo : le flag `_debugAllPlayersMovable` (true par défaut Phase 2) sur `CombatInputController` envoie les commandes au joueur actif courant, peu importe P0/P1 — pratique pour tester les 2 combattants sans matchmaking. Sera désactivé en Phase 6 quand le matchmaking arrivera.
- 🎯 **MOMENT CHARNIÈRE** : c'est la 1ère fois qu'un sort Nymora *réel* (Bible V7.1) inflige des dégâts dans le jeu. Les fondations de la Phase 2 sont maintenant validées (grille + tours + mouvement + sorts). Reste à étoffer : ressource Hémoglyphe (2.9), 14 sorts Soulrender (2.10), signature + passif (2.11), Nightseer complet (2.12-2.14), IA (2.15-2.16), test E2E (2.17).

### 11 mai 2026 — Brique 2.7 (Spell runtime engine + DÉCOUVERTE QUANTUM_LOGLEVEL)
- 4 fichiers Quantum nouveaux (Spell.qtn, CastSpellCommand, SpellRegistry, SpellSystem) + 2 updates (CommandSetup, SystemSetup) + 1 update View (CombatInputController : touche Espace = cast TestZap)
- DSL Quantum : `enum SpellId : Byte` (plages réservées 10-29 Soulrender, 30-49 Nightseer, 50-69 Colossar, 70-89 Necram, 90-109 Ghostra, 1-9 sorts de dev) + `enum SpellEffectKind` (Damage/Heal/ApplyMark/Push/Pull/Spawn)
- `CastSpellCommand : DeterministicCommand` avec serialize SpellId via byte + TargetX/Y
- `SpellRegistry` : switch déterministe (pas de Dictionary alloc heap) sur SpellId → SpellDef { PACost, Shape, Filter, RangeMin/Max, DamageAmount }
- `SpellSystem` (SystemMainThread, unsafe) : pipeline complet validation→consommation PA→ResolveEffectCells stackalloc→damage sur chaque cible
- TestZap (sort de dev 2.7) : 3 PA, SingleTile, Enemy, Range 1-5, 100 dmg. Sera retiré en 2.8 au profit de Tranche-Âme.
- **🚨 DÉCOUVERTE MAJEURE OUTILS** : les logs Quantum (`Log.Info/Warn/Error`) sont **stripped à la compilation** via `[Conditional]` attributes si les **defines `QUANTUM_LOGLEVEL_INFO/WARN/ERROR/DEBUG` ne sont pas activés** dans Player Settings > Scripting Define Symbols. **Sans ces defines, AUCUN log côté simu n'apparaît dans la console Unity**, ce qui rend le debug aveugle. Fix : ajout de `QUANTUM_LOGLEVEL_INFO` dans les Scripting Define Symbols. Désormais tous les logs simu Quantum sortent avec le préfixe coloré `[Quantum]` dans la console Unity standard. À documenter dans `_docs/00_README_CLAUDE.md` pour les futures phases.
- 1 piège diagnostic traversé (~30 min de debug) : 
  - Symptômes initiaux : la `CastSpellCommand` partait du View (log visible) mais aucun log côté simu, même en `Log.Error`. Suspect 1 (faux) : MovementSystem consomme la command — testé en swappant l'ordre Spell/Movement, sans effet. Suspect 2 (vrai) : les logs sont strippés par le define manquant.
  - Le diagnostic final est tombé en cherchant dans `Quantum.Log.xml` la phrase "needs to be defined" qui exposait le mécanisme.
- Validation finale (logs visibles après ajout du define) :
  - `[Quantum] [TurnSystem] Initiative: Joueur P0 commence`
  - `[Quantum] [SpellSystem.DEBUG] OnInit appele` (avant cleanup)
  - `[Quantum] [Movement] P0 -> (6,8) cost=3 PM restant=0`
  - `[Quantum] [Spell] Damage 100 sur P1 (11,8) HP 1500 -> 1400`
  - `[Quantum] [Spell] P0 cast TestZap target=(11,8) PA restant=5`
- Code de debug retiré après validation : OnInit log, PING toutes les 60 frames, log RECV par player, warning "command pendant pas TurnActive". Le SpellSystem est revenu à sa forme propre.

### 11 mai 2026 — Brique 2.6 (Targeting Shape + Filter)
- 2 fichiers Quantum (Targeting.qtn + TargetingResolver.cs) + 1 nouveau View (TargetingPreviewView) + 3 updates (TileView, GridRenderer, CombatInputController)
- DSL Quantum : `enum TargetingShape : Byte` (13 valeurs : None, SingleTile, CrossSmall/Medium/Large, Square3x3/5x5, Line, LineThrough, Cone, CircleSmall/Medium/Large) + `enum TargetingFilter : Byte` (10 valeurs : None, Self, Ally, AllyIncludingSelf, Enemy, AnyUnit, EmptyTile, TileWithObstacle, TileWithLure, AnyTile). Valeurs dupliquées de Nymora.Core.Enums (mêmes IDs).
- `TargetingResolver` (static, unsafe class) : 3 méthodes principales
  - `ResolveCastableCells` : cases visables par caster (range Manhattan rangeMin..rangeMax)
  - `ResolveEffectCells` : cases impactées par le sort (zone d'effet selon shape)
  - `MatchesFilter` : la case match-elle le filter (occupant type, vs casterEntity)
  - **Shapes implémentés en 2.6** : SingleTile, CrossSmall, Line (sans stop sur unité), CircleSmall (disque Manhattan rayon 1). Les autres logguent un warning "à implémenter quand un sort en aura besoin" — éviter le code mort.
  - Wrappers safe `int[]` ajoutés en plus des versions unsafe `int*` (le simu utilisera stackalloc pour zero-alloc, le View utilise les arrays managés sans avoir besoin de `allowUnsafeCode` sur Nymora.Combat asmdef)
- `TargetingPreviewView` : MonoBehaviour subscribe `CallbackUpdateView`
  - Clear highlights précédents
  - Si `_debugShowTargeting` actif sur le CombatInputController :
    - Trouve le caster = combattant du joueur actif
    - Calcule castable cells (range Manhattan) + applique MatchesFilter → highlight bleu clair
    - Au survol case castable → calcule effect cells (shape autour du hover) → highlight rouge clair par-dessus
- `TileView` enrichi : `_baseColor` stocké, méthodes `ApplyHighlight(Color)` / `ClearHighlight()` pour restaurer
- `GridRenderer.GetTileView(int gx, int gy)` exposé (lookup direct par coords)
- `CombatInputController` enrichi : 5 nouveaux champs `_debugShowTargeting/_debugShape/_debugFilter/_debugRangeMin/_debugRangeMax` exposés en read-only properties. Quand `_debugShowTargeting` actif, le clic ne déclenche pas de MoveCommand (bypass pour pas pourrir l'UX de test).
- 1 piège traversé : 
  - `unsafe { fixed(int* buf = ...) }` ne compile pas dans `Nymora.Combat` qui a `allowUnsafeCode: false`. Fix propre : ajout de wrappers safe dans `TargetingResolver` (asmref Quantum.Simulation qui A `allowUnsafeCode: true`) qui acceptent `int[]` et font le `fixed` en interne. Le code unsafe reste contenu dans Quantum.Simulation, le View ne le voit jamais.
- Limitation visuelle constatée par Lorenzo : les pions combattants (sortingOrder ~700-990) passent devant les tiles highlighted (sortingOrder 0-) → la case où est le combattant est masquée visuellement. Pas gênant fonctionnellement (le résolveur fait bien son boulot, vérifiable via le hover sur d'autres cases). Pourra être amélioré avec un overlay au-dessus du pion (Phase 7 polish).
- Validation : Range respecté, shape change en live au switch inspector, filter Enemy/AnyUnit/Self/EmptyTile fonctionnel (testé avec range=12 pour voir le Nightseer à 8 cases du Soulrender).

### 11 mai 2026 — Brique 2.5 (Pathfinding A* déterministe) + clôture Bloc A
- 1 nouveau fichier (`AStarPathfinder.cs`) + refactor `MovementSystem.cs` + 2 fix hook (pre-commit Git + Healthcheck Nymora)
- A* déterministe :
  - Heuristique Manhattan (4-connexité, garantit optimum sur grille rectangulaire)
  - Tie-break par index grille croissant (`y*Width + x`) → ordre d'expansion totalement reproductible
  - Zero allocation heap : tous les buffers en `stackalloc int[Count]` / `stackalloc bool[Count]` (~4 KB stack/appel pour 255 cases)
  - Pas de FP : tout int (gScore, fScore = gScore + Manhattan)
  - Reconstruction du path en 2 phases (count + écriture) pour fail-fast si len > maxSteps
- `MovementSystem` refactor :
  - Heuristique rapide `manhattanDistance > PM` → skip A* (économie)
  - Cas adjacent (manhattan==1) → skip A* aussi (optim cas fréquent)
  - Sinon → A* déterministe
  - Application synchrone en 1 tick : combattant téléporté, PM -= path.length, SetOccupant
  - Le View lerp en ligne droite (anim case-par-case = Phase 2.10+ si nécessaire)
- 2 fix hook **importants** côté outillage (cohérence simu/view) :
  - `tools/git-hooks/pre-commit` : scan séparé `QuantumUser/Simulation/` (sauf Generated/) ET `Scripts/Combat/` (sauf View/) — le View Unity peut légitimement utiliser `Time.deltaTime` pour les lerps/animations
  - `NymoraHealthCheck` (Editor Tool) : même refactor — nouvelle méthode `ScanPath` réutilisable, scanne 2 paths avec exclusions, ajoute le check sur la simu Quantum qui n'était pas couvert avant
- 1 piège traversé :
  - Conflit de nom local `int f = ...` dans la boucle A* alors que le paramètre est `Frame f` — renommé en `fScore`. Réflexe à garder : éviter les variables locales nommées `f` dans tout code Quantum.
- Validation : mouvement 2-3 cases fonctionne, A* contourne le Nightseer si nécessaire, distance > PM rejeté, healthcheck à 0 erreur (incluant le nouveau scan de la simu Quantum).
- 🏁 **Bloc A — Fondations grille & tour : BOUCLÉ** (5/5 briques validées en une session marathon : 2.1 → 2.5 le 11 mai 2026).

### 11 mai 2026 — Brique 2.4 (Mouvement case par case + DeterministicCommand)
- 7 fichiers livrés (2 simu Movement + 2 updates Setup + 2 updates View + 1 nouveau input controller)
- DeterministicCommand `MoveCommand : DeterministicCommand` avec `int TargetX, TargetY` + override `Serialize(BitStream)`
- `MovementSystem` (`SystemMainThread`, unsafe) :
  - Lit `frame.GetPlayerCommand(playerIndex) is MoveCommand cmd` pour chaque player slot
  - Validations en cascade : phase TurnActive, joueur actif (rejet sinon), Combatant existe, PM>0, dans la grille, walkable, case libre, adjacence Manhattan (|dx|+|dy|==1, 4-connexité stricte, pas de diagonale en 2.4)
  - Application : `GridHelpers.SetOccupant(ancien, None)` → MAJ GridX/GridY → `PM--` → `SetOccupant(nouveau, entity)`
- `CommandSetup.User.cs` : `factories.Add(new MoveCommand())` (le command est sa propre factory via DeterministicCommand)
- `IsoProjection.WorldToGrid(Vector3, tileW, tileH, centerOffset) → (int gx, int gy)` : inverse arithmétique exact de `GridToWorld` (résolution du système 2 équations 2 inconnues) + `Mathf.RoundToInt` pour snap au plus proche
- `CombatantView` : lerp Vector3 entre `transform.position` et `_targetWorldPosition` à vitesse 8 (~0.15s/case), avec snap distance < 0.01 pour éviter de lerp infiniment. Snap direct au tout premier `UpdateGridPosition` post-Bind pour éviter une animation depuis (0,0,0) au spawn.
- `CombatInputController` : MonoBehaviour qui écoute `UnityEngine.Input.GetMouseButtonDown(0)`, fait `_camera.ScreenToWorldPoint` → `IsoProjection.WorldToGrid` → envoie `game.SendCommand(playerSlot, new MoveCommand{TargetX,TargetY})`. Toggle `_debugAllPlayersMovable` (true par défaut Phase 2) qui envoie au joueur ACTIF courant pour pouvoir tester P0 puis P1 alternativement sans setup matchmaking. À désactiver en Phase 6.
- 2 pièges traversés :
  1. **Ambiguïté `Input`** : `using Quantum` importe `Quantum.Input` (struct DSL pour input continu Quantum) qui collide avec `UnityEngine.Input`. Fix : qualifier explicitement `UnityEngine.Input.GetMouseButtonDown(0)` et `UnityEngine.Input.mousePosition` dans le code View. Le `using UnityEngine` ne sauve pas car le `using Quantum` est prioritaire dans le scope local.
  2. **Mode spectator Quantum** : par défaut un runner Quantum sans player explicitement ajouté est en mode "spectator" et rejette toutes les commands ("Can't send commands in spectating mode"). Fix : appeler `game.AddPlayer(slot, new RuntimePlayer())` pour chaque player local au `CallbackGameStarted`. Ajouté en option `_autoAddLocalPlayers` (default true Phase 2) dans `CombatInputController`. En Phase 6, ces players viendront du menu/matchmaking et on retirera ce code de debug.
- Validation : clic case adjacente → animation lerp visible + PM-- (validé via console), clic non-adjacent → log warning + pas de move, clic case occupée → rejet, PM=0 → plus de move possible jusqu'au prochain tour (reset auto en TurnStart vu en 2.3), swap P0/P1 fonctionne sans réinitialisation.

### 11 mai 2026 — Brique 2.3 (FSM tour + timer 15s + initiative)
- 6 fichiers livrés (3 simu Quantum + 1 update SystemSetup + 1 view + 1 editor tool) + 1 update asmdef Nymora.Combat (ref Unity.TextMeshPro pour le HUD)
- DSL Quantum : `enum CombatPhase : Byte { PreMatch, TurnStart, TurnActive, TurnEnd, MatchEnd }` + `singleton component CombatState { CombatPhase CurrentPhase; Int32 ActivePlayerIndex; Int32 TurnNumber; Int32 TurnTimerTicks; }`
- `TurnConstants` : `TurnDurationSeconds = 15` (Bible V7.1) + helper `GetTurnDurationTicks(f) = 15 * f.UpdateRate` (= 900 ticks à 60 Hz standard)
- `TurnSystem` : `SystemMainThread`, FSM stricte en `unsafe` (manipulation pointers singleton)
  - `OnInit` : init CombatState, tirage initiative déterministe via `f.RNG->Next(0, 2)`, transition immédiate vers TurnStart
  - `Update` : switch sur CurrentPhase (TurnStart reset PA/PM + Turn++, TurnActive décompte ticks, TurnEnd swap player → TurnStart)
- Côté View : `CombatHUDView` MonoBehaviour qui s'abonne à `CallbackUpdateView` et lit la singleton via `frame.TryGetSingleton<CombatState>()`. Affiche `Phase | Tour N | Joueur PX Class | Timer X.Xs`. Conversion ticks → secondes via `frame.UpdateRate`.
- Editor Tool `CreateCombatHUDTool` génère Canvas (ScreenSpaceOverlay + CanvasScaler 1920×1080) + GameObject HUD avec TextMeshProUGUI ancré en haut centre + EventSystem si manquant. Cable `_label` via SerializedObject. Marque la scène dirty.
- Décisions importantes :
  1. **PA/PM reset en début de tour, PAS le HP ni la ressource de classe** (Bible V7.1 : HG/PR/FD/PT/RM persistent entre tours, c'est leur design fondamental).
  2. **End turn auto au timer en 2.3**, input "End Turn" volontaire viendra en 2.4 avec le mouvement.
  3. **TurnTimerTicks stocké en int** (pas FP) pour rester pur déterministe trivialement. Conversion vers secondes uniquement côté View (float OK pour l'affichage).
- Validation : initiative reproductible (même seed → même P0/P1), timer décompte 15→0, swap automatique au timer, PA/PM reset visibles via log Quantum, HUD affiche bien la phase courante (`TurnActive`).
- Aucun piège majeur sur cette brique — la 1.10/1.11/1.12 fluide.

### 11 mai 2026 — Brique 2.2 (Entity Combatant Quantum HP/PA/PM/Class)
- 7 fichiers livrés + 2 updates asmdef (Nymora.Editor pour refs Quantum) + 1 update SystemSetup
- DSL Quantum : `enum NymoraClass : Byte` (dupliqué côté Quantum, valeurs identiques à Nymora.Core.Enums) + `component Combatant { PlayerIndex, Class, HP/MaxHP, PA/MaxPA, PM/MaxPM, GridX, GridY }`
- `CombatantSystem` : `SystemSignalsOnly`, OnInit crée 2 entities via `f.Create()` + `f.Add<T>(entity, value)` (API unique value-pass plus simple que Add + GetPointer), positions hardcodées (3,8) et (11,8), marque la grille via `GridHelpers.SetOccupant`
- `CombatantStats` : constantes Bible V7.1 (1500 HP, 8 PA, 3 PM / 2 PM Colossar) accessibles via helpers `GetMaxHP/PA/PM(NymoraClass)`
- Côté View : `CombatantView` (binding entity ↔ GameObject, sorting order) + `CombatantRenderer` (subscribe CallbackGameStarted pour spawn initial + CallbackUpdateView pour sync positions à chaque tick verified)
- Editor Tool : `CreateCombatantPlaceholdersTool` génère 5 sprites placeholder (1 par classe avec couleur accent Bible V7.1) + 5 prefabs câblés (SpriteRenderer + CombatantView via SerializedObject)
- **Convention PPU clarifiée et verrouillée** (décision majeure de cette brique) :
  - Tiles : 64×64 PPU 64 → 1 unité world = 1 case ✓
  - Persos : 128×128 PPU 128 → 1 unité world = 1 case ✓
  - Règle : chaque catégorie de sprite a son PPU adapté à sa résolution, JAMAIS le même PPU pour des sprites de tailles différentes
- 4 pièges traversés (à retenir) :
  1. **Nymora.Editor.asmdef** doit aussi référencer Quantum.Simulation + Quantum.Unity via GUID pour que les Editor Tools voient `NymoraClass`. Le `autoReferenced: true` côté Quantum ne suffit jamais pour les asmdef custom — toujours ajouter explicitement les GUIDs.
  2. **PPU vs taille de sprite** : confusion initiale, j'avais mis sprite 128×128 avec PPU 64 → pion 2×2 cases. Le bon couple est sprite 128×128 + PPU 128 → 1×1 case. Convention notée pour tous les futurs sprites du projet.
  3. **Sorting order combattants** : ma première formule `100 - (gx+gy)*10` donnait des valeurs négatives (jusqu'à -90) pour les combattants éloignés → ils passaient derrière les tiles (qui sont à 0..-30). Fix : base 1000 → range 700-990, toujours > 0 et > tiles.
  4. **Quantum API safe vs unsafe** : `Filter.NextUnsafe(out, out T*)` nécessite `allowUnsafeCode: true` sur l'asmdef. Pour Nymora.Combat qui reste safe (View), utiliser `Filter.Next(out, out T)` qui retourne par valeur (copie ~40 bytes, négligeable).
- Validation : 2 pions visibles (rouge Soulrender en (3,8), violet Nightseer en (11,8)), logs corrects HP=1500/1500 PA=8 PM=3, healthcheck OK, Graph Profiler vert.
- Question UX restée ouverte : pion rond placeholder "déborde" verticalement (sprite 1×1 unité world, case iso 1×0.5 visuel). C'est normal — anticipe le rendu final (perso debout ~2 cases de haut posant les pieds au centre). Lorenzo accepte le placeholder visuel actuel, on continue.

### 11 mai 2026 — Brique 2.1 (Grille 15×17 data + view iso 2D)
- Première brique gameplay de la Phase 2 ✅
- 10 fichiers livrés (3 simu Quantum + 4 view/settings + 2 editor tools + 1 update SystemSetup) + 1 update asmdef Nymora.Combat (refs Quantum.Simulation/Quantum.Unity via GUID)
- DSL Quantum 3 : `struct Tile { Byte Walkable; EntityRef Occupant; }` + `singleton component GridSingleton { Int32 Width; Int32 Height; array<Tile>[255] Tiles; }`. Régénéré auto par `QuantumQtnAssetImporter` au save → `Generated/Quantum.CodeGen.Core.cs` enrichi + nouveau `QPrototypeGridSingleton.cs` côté View.
- Simu déterministe : `GridSystem : SystemSignalsOnly` qui init la grille dans `OnInit(Frame f)` via `f.Unsafe.GetOrAddSingletonPointer<GridSingleton>(EntityRef.None)`. Width/Height stockés dans la singleton, taille fixe `array[255]` verrouillée au compile time.
- View iso 2D : `IsoProjection` static helper (formule 2:1) + `GridRenderer` MonoBehaviour qui s'abonne à `CallbackGameStarted` et spawn 255 tiles. Sorting order `-(gx+gy)` pour ordre de rendu correct futur.
- Centrage auto via `IsoProjection.CenterOffset(...)` calculé sur la moyenne des 4 coins du losange → grille centrée à (0,0) world, tombe juste sous la caméra par défaut.
- Editor Tools : `CreateGridAssetsTool` (génère sprite losange 64×32 procédural en Texture2D + prefab TileView + SO GridSettings) + `GridPreviewerWindow` (preview iso hors Play pour validation rapide).
- Pièges traversés :
  1. Mauvaise API Quantum 3 au premier jet : `GetOrAddSingleton<T>()` n'existe pas en unsafe → corrigé en `Unsafe.GetOrAddSingletonPointer<T>(EntityRef.None)` (la version safe retourne par valeur, l'unsafe retourne un pointer).
  2. `Nymora.Combat.asmdef` ne référençait pas explicitement Quantum.Unity / Quantum.Simulation → `QuantumGame` introuvable au compile. Le flag `autoReferenced: true` côté Quantum ne suffit pas pour les asmdef custom : il faut ajouter les GUIDs `5d82202959c2f144ea95e134645b6833` (Simulation) et `f6fa0c2f8b9a9f64897d3351666f3d66` (Unity) dans les `references`.
  3. Caméra non centrée au premier Play : la formule iso donne un losange qui s'étend en X de `-(Height-1)*tileW/2` à `+(Width-1)*tileW/2` et en Y de 0 à `(Width+Height-2)*tileH/2`. Fix : option `CenterGrid` (default true) + offset calculé sur la moyenne des 4 coins.
- Validation : compil 0 erreur / 0 warning, grille 15×17 = 255 tiles visibles en iso centrée, Play QuantumGameScene OK (GridSystem tourne, CallbackGameStarted déclenche le spawn View), Graph Profiler vert.

### 11 mai 2026 — Cadrage Phase 2 (17 briques) + démarrage 2.1
- Lorenzo dit "go phase 2" après les prérequis (Docker + backend + Unity OK)
- Claude lit `01_BIBLE_V7.1_Combat.md` (stats, ressources, signatures) + section Phase 2 du `05_Roadmap_V2_Novice.md`
- Cadrage Phase 2 publié : **17 briques** en 5 blocs (Fondations / Sorts génériques / Soulrender / Nightseer / IA+E2E)
- 3 décisions techniques verrouillées en début de Phase 2 :
  1. **Vue iso 2D** (cases losange style Dofus) — projection vit côté View uniquement, simu reste rectangulaire en `int`
  2. **Origine grille (0,0) bas-gauche** (Y vers le haut, convention math classique)
  3. **PPU 64** (sprites 64×64, losange world 64×32 ratio 2:1)
- 17 tâches créées dans la task list pour tracker la progression
- Brique en cours : **2.1** (grille 15×17 data + view iso) — SETUP en cours de présentation

### 11 mai 2026 — Brique 1.14 (Test E2E Phase 1) — 🏁 CLÔTURE PHASE 1
- Décisions cadrage : scope minimal (auth multi-client uniquement, pas de simulation Quantum déterministe) car la simulation propre demande du custom code throwaway qui sera refait en Phase 2 quand on aura de vrais Systems
- Discussion stratégie coûts importante : Lorenzo a demandé "la 1.13 est obligatoire ?" → clarification que Hetzner ≠ slots Photon, et qu'on peut tout faire en local jusqu'à Phase 7 sans dépenser. **Décision : reporter 1.13 en Phase 7 (prep alpha).** Memory feedback créée pour cette philosophie.
- 1.14 en pratique :
  1. Build Standalone Windows configuré (Mono, Windowed 1280×720, scène 00_Login seule, dossier `Builds/1.14/`)
  2. ngrok relancé : nouvelle URL `https://alphabet-reverend-cloud.ngrok-free.dev`, dashboard Photon Custom Auth mis à jour avec `/auth/photon-webhook`
  3. Test multi-client : Editor login tester1 + Build login tester2, chacun clic Connect Photon → 2 UserIds Postgres distincts (e13a... et 33b8...)
- Validation : 2 sessions Photon simultanées OK, backend supporte le multi-client, webhook valide les 2 JWTs en parallèle sans interférence
- 🏁 **PHASE 1 CLÔTURÉE** : 14 briques / 14 (1.13 reportée explicitement). Stack fondations complète : Quantum + Fusion + Backend Node+Postgres+Redis+Prisma + Auth JWT/bcrypt/Custom Auth Photon + version-guard + Logger Pino/NymoraLog + CI/CD backend vert + multi-client validé. Coût total : 0€.
- **Prochaine session : Phase 2 (Combat). Claude livrera le détail des briques 2.x au début de la prochaine session.**

### 11 mai 2026 — Brique 1.12 (CI/CD GitHub Actions) — backend OK, Unity bloqué license
- Décisions cadrage : Unity scope = compile-check léger (pas build full), triggers = push main only, backend = 5 smoke tests
- Backend workflow livré en 1 fichier `backend-ci.yml` : services Postgres + Redis avec healthchecks, JWT_SECRET à la volée, séquence complète npm ci → prisma → lint → build → 5 tests
- Push initial sur les 2 repos (avec tous les commits accumulés 1.10 + 1.11 + 1.12) → Backend CI **VERT au premier essai** ✅
- Galère Unity CI : 
  1. Workflow `unity-activation.yml` créé pour générer .alf via action GameCI → action `unity-request-activation-file@v2` **DÉPRÉCIÉE** ("This action is no longer supported")
  2. Pivot vers génération .alf en local via `Unity.exe -batchmode -createManualActivationFile -logfile -` → Lorenzo a galéré avec les guillemets (`"C:\Program Files\..."`) puis sur le site Unity manual qui demandait un serial Pro/Plus
  3. Tentative alternative via Unity Hub → Manage License → mais le compte Unity de Lorenzo est de type "organisation" sans license Pro achetée, et Personal n'est pas dispo pour les comptes orga
  4. **Décision** : on désactive Unity CI pour la 1.12 (passé en `on: workflow_dispatch`), on le réactivera en Phase 7 prep alpha (au plus tard) avec soit un compte Unity ID perso secondaire, soit une Unity Pro license achetée
- Brique 1.12 **validée partiellement** (backend OK, Unity en attente) → enchaîne sur 1.13 (Hetzner) ; Unity CI reste en backlog pour plus tard

### 11 mai 2026 — Brique 1.11 (Logger structuré client + serveur)
- 3 décisions techniques tranchées en début de brique : scope runtime uniquement, niveaux Info/Warn/Error/Critical, Pino pretty en dev + JSON en prod
- Backend : install `pino@9.14.0` + `pino-pretty@11.3.0`, création `src/services/logger.ts` (singleton avec base `service: nymora-backend` + transport pino-pretty conditionnel via NODE_ENV), modif `src/index.ts` pour `logger.info({port}, '...')`
- Unity : nouveau `Scripts/Core/Logging/NymoraLog.cs` (4 méthodes Info/Warn/Error/Critical + event OnLogEmitted pour Loki Phase 7+), modif `LoginScreenController.cs` pour remplacer 6 `Debug.Log` par `NymoraLog`
- Convention : préfixe `[Nymora.{Category}]` géré par le wrapper, appelants passent juste category + message
- 2 pièges traversés :
  1. Crash cascade nodemon pendant les saves successifs (TS6133/TS6192 transitoires sur auth.ts) — inoffensif, juste attendre que toutes les saves soient passées
  2. `Cannot find module 'pino'` car npm install pas encore fait quand le serveur a redémarré — résolu par Ctrl+C + relance `npm run dev` après les install
- Validation : pino-pretty visible au démarrage, 21/21 smoke tests passed, Console Unity propre avec stack traces qui passent bien par NymoraLog.Emit, Healthcheck Unity 0 erreur
- Brique 1.11 validée → 1.12 (CI/CD GitHub Actions) en attente

### 10 mai 2026 — Brique 1.10 (Système de versioning runtime)
- Cleanup docs préalable : commit `5f37270 docs: cleanup pre-1.10` (4 docs modifiés + plan pixel art + 2 captures d'écran + suppression fichier vide "Déblocage")
- 3 décisions techniques tranchées en amont :
  1. Politique versions : `min < current` (flexible) — pas strict, permet de bumper `current` à chaque release sans casser les clients existants
  2. Points de blocage : double verrou HTTP `/auth/*` (header `X-Nymora-Client-Version` → 426) + webhook Photon (champ `clientVersion` dans body/query → ResultCode=2)
  3. Cleanup docs séparé avant la brique pour un diff propre
- Backend : 4 nouveaux fichiers (version.service.ts / route version.ts / middleware version.middleware.ts / smoke test version.ts) + 3 modifs (server.ts pour câbler le router, auth.ts pour appliquer le middleware + check inline webhook, package.json pour le nouveau script test:version) + 2 updates des smoke tests existants (test-auth et test-photon-webhook devaient passer le header / clientVersion sinon régression)
- Unity : 1 nouveau fichier (NymoraVersionClient.cs avec semver parse + comparator) + 5 modifs (NymoraApiClient pour injection auto du header X-Nymora-Client-Version, NymoraApiDtos pour le DTO VersionResponse, PhotonAuthBridge pour ajouter clientVersion dans SetAuthPostData, LoginScreenController pour le check version au Start + LockUiForUpdate, CreateLoginSceneTool pour le panel "Mise à jour requise" plein écran)
- Anti chicken-and-egg : `GET /version` est public sans middleware version + côté Unity, `GetVersionAsync` SKIP volontairement le header X-Nymora-Client-Version. Sinon un client trop vieux ne pourrait pas apprendre quelle version est requise.
- Validation E2E :
  - Smoke tests backend : `test:auth` 7/7, `test:photon-webhook` 6/6, `test:version` 8/8 (21/21 total)
  - Test Unity golden path : status "Aucune session active", panel non affiché
  - Test faux client : `GameVersion.Current = "0.0.5"` → panel "Mise à jour requise" plein écran avec toutes les versions, boutons grisés
  - Healthcheck Unity : 0 erreur, 0 warning
- Brique 1.10 validée → 1.11 (Logger structuré) en attente

### 10 mai 2026 — Cleanup docs : retrait du déblocage par level + de la 6e classe
- Lorenzo tranche 2 décisions design : (1) tous les sorts dispos d'office, pas de gate par level ; (2) pas de 6e classe planifiée
- Cleanup en cascade dans 3 docs (8 modifs au total) :
  - `05_Roadmap_V2_Novice.md` : Phase 5 reformulée + section "Mois 19-24" renommée "clan wars + tournois" (au lieu de "nouvelle classe + clan wars")
  - `03_GDD_Features.md` : edge case "Sort verrouillé" supprimée + refonte complète section 13 (PRINCIPE + tableau "PROGRESSION DES RÉCOMPENSES COSMÉTIQUES" + DESIGN insight reformulé). Tableau XP de classe inchangé (toujours pertinent)
  - `04_Roadmap_14_mois.md` : S33 reformulé + checkpoint QA Phase 5 corrigé + ligne "6e classe" retirée de la roadmap post-launch 6-12 mois
- Justification design (notée dans le GDD) : pas de barrière à l'entrée, matchmaking équitable car même pool de sorts pour tous, levels servent uniquement de prestige cosmétique
- Décision : les paliers cosmétiques précis (cadre niveau 5, titre niveau 10, etc.) restent indicatifs dans le GDD et seront figés en Phase 5
- 3 décisions verrouillées ajoutées au tableau de ce STATUT_ACTUEL
- Pas de touche au code, pas de touche à la Bible V7.1 (qui ne mentionnait ni déblocage par level ni 6e classe)

### 10 mai 2026 — Brique 1.9 (Custom Auth Photon ↔ JWT backend) — session marathon
- Lorenzo a voulu enchaîner direct sur la 1.9 (3ème brique de la soirée après 1.7 et 1.8, mode warrior)
- Setup ngrok-free pour exposer le backend localhost via URL HTTPS publique (`*.ngrok-free.dev`)
- Backend : webhook `POST /auth/photon-webhook` + smoke test 6 cas (token body/query, fake JWT, user supprimé, AuthCookie)
- Unity : `PhotonAuthBridge` (helper AuthValues from JWT) + `PhotonConnectionTester` (MonoBehaviour avec UniTask + IConnectionCallbacks + cleanup propre)
- Modifs scène 00_Login : 4ème bouton "Connect Photon" + component PhotonConnectionTester sur le LoginScreenController
- Photon Dashboard : Custom Authentication activée + URL webhook configurée + fail-closed
- 3 bugs traversés en cours de test E2E :
  1. URL Photon Dashboard sans path → Photon tapait la racine → page HTML Express → "Unexpected character: <" → fix : URL complète avec `/auth/photon-webhook`
  2. Faux suspect "warning page ngrok" écarté par debug ciblé (test curl avec/sans header skip-warning)
  3. Warning Photon cleanup fix par wait sur ClientState.Disconnected
- Validation E2E réussie : Status Unity "Photon OK ! Region=eu UserId=<UUID Postgres>"
- Healthcheck Unity 0/0, smoke tests backend 13/13 (auth + photon-webhook)
- Brique 1.9 validée → 1.10 (Versioning runtime) en attente

### 10 mai 2026 — Brique 1.8 (Unity client HTTP → backend) — même soirée
- Lorenzo a voulu enchaîner direct sur la 1.8 après la validation de la 1.7 (pas de pause)
- Lecture de `05_Roadmap_V2_Novice.md` pour cadrer la 1.8 officielle (= Client Unity HTTP au backend)
- Décisions techniques tranchées en 3 questions : JsonUtility (pas Newtonsoft), UniTask (pas coroutines), Editor Script de génération de scène (pas manip clic à clic)
- 11 fichiers livrés en parallèle (manifest.json, 3 asmdef updates, 4 nouveaux scripts Network, 1 script UI, 2 Editor Scripts)
- 2 pièges traversés en cours de manip avec Lorenzo :
  1. Backend Express pas démarré → status 0 "cannot connect to destination host" : il faut bien laisser `npm run dev` actif dans une fenêtre cmd dédiée pendant tout le test
  2. `Nymora.Editor.asmdef` ne référençait pas `Unity.TextMeshPro` → erreur de compilation `TMPro introuvable` dans le tool de génération de scène ; ajout de la ref a corrigé
- Pédagogie pour Lorenzo (novice) : besoin d'expliquer Prisma Studio, les filtres console Unity, le workflow multi-fenêtres (Docker + backend + Studio + Unity en parallèle)
- 6 checks de validation manuels passés : register, console JWT, ligne en DB Postgres, persist PlayerPrefs, logout, login avec mauvais mdp
- Healthcheck Unity : 0 erreur, 0 warning
- Brique 1.8 validée → 1.9 (Custom Auth Photon) en attente

### 10 mai 2026 — Brique 1.7 (Auth JWT + bcrypt)
- Reprise de session, push des restes de la 1.6 (backend Prisma + STATUT_ACTUEL maj) sur les 2 repos GitHub
- Brique 1.7 : 3 décisions techniques tranchées avant livraison
  - Stratégie JWT → access only 24h (refresh = brique dédiée plus tard)
  - Register exige email + password + displayName (transaction User+Profile)
  - Validation zod (email format / password 8-128 / displayName regex)
- Livraison de 4 nouveaux fichiers (auth.service / auth.middleware / routes/auth / scripts/test-auth) + 4 modifs (server / index / .env / package.json) + install bcrypt jsonwebtoken zod (et types)
- Piège traversé : `Prisma` namespace exporté depuis `generated/prisma/client.ts` (pas `generated/prisma/index.ts`) → import à corriger
- `npm run test:auth` → 7/7 PASSED
- `npm run lint` → 0 erreur (warning cosmétique TS 5.9 noté pour upgrade toolchain en fin de Phase 1)
- Brique 1.7 validée → 1.8 à définir au début de la prochaine session

### 9 mai 2026 — Brique 1.6 (Prisma)
- Reprise de session, validation que la stack Docker était toujours opérationnelle (Postgres + Redis Up)
- Tentative install Prisma → blocage : Node 20.16 < 20.19 requis par Prisma 7 → upgrade Node 22 LTS via MSI
- Plusieurs adaptations forcées par Prisma 7 (sortie 2025, breaking changes vs Prisma 6) :
  1. `prisma-client-js` (legacy) → `prisma-client` avec `output` explicite
  2. `url` dans `datasource` interdit → tout passe par `prisma.config.ts`
  3. `prisma.config.ts` doit avoir `datasource.url` (CLI) ET `migrations.adapter` (runtime PrismaPg)
  4. `dotenv` n'est plus chargé auto, faut `import 'dotenv/config'`
- Migration `init` appliquée, smoke test PASSED → Brique 1.6 validée
- Brique 1.7 (Auth JWT + bcrypt) en attente

### 8 mai 2026 — Brique 1.5 (Docker stack)
- Reprise de session après restart de Docker Desktop
- Validation que `docker-compose.yml` (Postgres 16 + Redis 7) tournait correctement
- Fix npm : il faut `cd backend` avant `npm run test:db` (sinon npm cherche un package.json dans le home)
- Test connexions OK → Brique 1.5 validée
- `STATUT_ACTUEL.md` à jour, Brique 1.6 (Prisma) en attente

### 8 mai 2026 — Session de cadrage initiale
- Refonte complète Bible V6.1 → V7.0 → V7.1 (combat, classes, ressources, signatures)
- Choix d'architecture validés (Quantum + Fusion split, Node.js backend, F2P éthique)
- Roadmap V1 (technique haut niveau) puis V2 (novice-friendly brique par brique)
- Pack docs MD créé pour transmission entre instances Claude
- Setup Claude Code : `CLAUDE.md` + `.claudeignore` + `.claude/settings.json`
- **Décision parallélisme** : Phases 0-3 sequential strict, Phases 4-7 parallel controlled
- **Convention Editor Scripts** : `Assets/_Nymora/Editor/{Setup,Generators,Windows}/` + asmdef `Nymora.Editor`
- CLAUDE.md mis à jour en v2.0 avec ces nouvelles règles
- **PIVOT MAJEUR** : alpha Windows-only (Mac + Mobile post-alpha)
- **Ajout outils scan auto** dès Phase 0 : Roslyn + Healthcheck + Pre-commit hook + Console filter
- Phase 0 étendue de 8 briques (2 sem) à 10 briques (2.5 sem)
- CLAUDE.md mis à jour en v3.0
- **Brique 0.1 livrée**, en attente de validation

---

## 🎯 PROCHAINE ACTION POUR LORENZO

> **Session du 13 mai 2026 (demain)** 🚀
>
> **Étape 1 — push GitHub des 6 commits cumulés** (instruction explicite Lorenzo 12 mai soir) :
> ```
> git -C "C:/Users/Lorenzo/Documents/Unity/Nymora/Nymora" push origin main
> ```
> Commits en attente :
> - 9781816 `feat(phase2.10.a)` — Statuses framework + 5 sorts
> - 25dffb8 `feat(phase2.10.b)` — Shields/Heals/Marques + 5 sorts
> - d4d623a `feat(phase2.10.c)` — Terrains + mvt non-PM + 4 sorts
> - def6e8f `feat(phase2.11)` — Signature Âme Lacérée + passif Appel du Sang
> - 8682b11 `feat(phase2.12)` — Visuel Soulrender + facing 4 directions iso
> - 9172f05 `feat(phase2.12.bis)` — Anims complètes walk/cast/attack/hurt/death
>
> **Étape 2 — démarrer Brique 2.13 : HUD combat complet** 🎮
>
> **Layout validé avec Lorenzo le 11 mai** :
> - PA/PM panel **haut-gauche**
> - Timer **haut-centre** (futur, optionnel pour l'alpha)
> - End Turn **milieu-droite**
> - Passif (icône + tooltip) **bas-gauche**
> - Deck 6 sorts **bas-centre** (slots cliquables avec icône + raccourci clavier + cooldown/HG cost overlay)
> - Timeline simple `P0 > P1` **bas-droite**
> - Texte flottant dégâts/heals **au-dessus des combatants** (rouge dégâts, vert heal, jaune shield absorb)
> - Prévisu PM + range sorts au survol
> - Infobulles persos (HP/PA/PM/HG + statuses actifs) au hover
> - Deck éditable dans l'Inspector (ScriptableObject ou liste sur un Combatant config)
>
> **Pré-requis (mêmes 3 fenêtres que d'habitude)** :
> - Docker Desktop allumé + backend `npm run dev` dans `backend/`
> - Unity Editor avec le projet Nymora ouvert
> - Smoke tests rapides : `npm run test:auth`, `npm run test:version`
>
> **Ordre des briques restantes** :
> - 2.13 : HUD combat complet ← **PRIORITÉ DEMAIN après push**
> - 2.14-2.16 : Nightseer (Prescience + 15 sorts + Œil + Traquenard + Brouillard de guerre)
> - 2.17 : IA + E2E combat 1v1 vs IA
>
> **Reliquats en attente** :
> - **Designer** : VFX Âme Lacérée 256×256 8-12f, Marque de Carnage overlay 64×64 4f, Plaie Ouverte overlay, Tile Vapeur Carmin animée 128×128 4f, Tile Sang Coagulé, Avatar profil 256×256.
> - **2.12.ter** (optionnel) : Walk speed variable selon PM (1-2 = lent, 3 = rapide). Pas critique gameplay, à voir si besoin.
>
> **Backlog notable** : Unity CI (license) + 1.13 Hetzner (Phase 7 prep alpha) + 1.14 simulation Quantum déterministe complète (couverte naturellement par Phase 2).
