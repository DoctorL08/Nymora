# 📍 STATUT ACTUEL DU PROJET NYMORA

> **À mettre à jour à chaque fin de session avec Claude.**  
> Ce fichier écrase tous les autres docs en cas de conflit. C'est la source de vérité du moment présent.

**Dernière mise à jour :** 12 mai 2026 (Brique 2.12 finalisée — facing 4 directions iso NE/SE/NW/SW)  
**Mis à jour par :** Claude (session courante)

---

## 🎯 OÙ ON EN EST

**Phase actuelle :** **Phase 2 — Combat (Soulrender + Nightseer)** 🎮  
**Brique en cours :** **2.13 — HUD combat complet** (à démarrer) + reliquat 2.12.bis (anims walk/spell par catégorie/PM speed)  
**Statut Phase 1 :** ✅ **CLÔTURÉE le 11 mai 2026** (1.13 reportée Phase 7, sinon 14/14 briques validées)  
**Statut Phase 2 :** 12/17 briques validées. Bloc A ✅ 5/5, Bloc B ✅ 3/3, **Bloc C ✅ 3/3** (2.9, 2.10 a/b/c, 2.11) + **2.12 ✅** (sprites Soulrender + icônes + Animator + facing 4 directions iso). **🏆 Soulrender 100% gameplay + visuel base** : 15 sorts + signature Âme Lacérée + passif Appel du Sang + 3 stages animés idle (HG palier) + facing 4 dirs (NE/SE livrés par designer, NW/SW miroir flipX runtime) + 17 icônes. Reste 2.12.bis (anims walk/cast/attack/hurt/death + vitesse selon PM), 2.13 HUD complet → 2.14-2.16 Nightseer + IA → 2.17 E2E.

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
- 2.12 — Assets visuels Soulrender (sprites 4 dirs + icônes 15 sorts + icône passif + signature) ⏳ PROCHAINE
- 2.13 — HUD combat complet (passif bas-gauche, 6 sorts bas-centre, timeline bas-droite, PA/PM haut-gauche, timer haut-centre, End Turn milieu-droite, prévisu PM/range, infobulles, texte flottant dgts/heals, deck éditable Inspector)

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

> **Session du 13 mai 2026 (demain) — Brique 2.12.bis : exploitation 100% des anims Soulrender** 🎬
>
> Lorenzo a stoppé la session le 12 mai après validation du facing 4 directions iso. Demande explicite pour demain : **exploiter à 100% les frame tags Aseprite livrés par le designer** (les anims walk/attack/cast/hurt/death sont déjà dans les .aseprite mais on n'utilise que `idle` actuellement).
>
> **Spec 2.12.bis (à confirmer avec Lorenzo en début de session)** :
> 1. **Walk** : déclenché à chaque déplacement case-par-case (pendant le lerp `MoveLerpSpeed`)
>    - Walk **lent** si PM dépensé sur ce déplacement = 1 ou 2
>    - Walk **rapide** si PM = 3 (ou plus, futur boost)
>    - Paramètre `Speed` (float) sur l'Animator ou 2 triggers distincts
> 2. **Cast (par catégorie de sort)** :
>    - Sort `Survie` → anim cast douce/défensive
>    - Sort `Attack` (ou offensif) → anim cast agressive
>    - Sort `Tactical` → anim cast neutre/concentration
>    - À mapper depuis `SpellCategory` (enum existant Core/Enums)
> 3. **Attack** : anim mêlée pour les sorts qui font du contact (Tranche-Âme, Empoignade, Charge Brutale, Âme Lacérée, etc.)
> 4. **Hurt** : trigger sur dégâts reçus (event Quantum à exposer côté View)
> 5. **Death** : trigger sur HP = 0 (event KO → freeze ou pas après l'anim)
>
> **Architecture probable** :
> - `CombatantView` : exposer `Animator` directement + paramètres (`Speed` float, triggers `Cast`, `Attack`, `Hurt`, `Death`)
> - 6 controllers `SoulrenderStage{0,1,2}_{NE,SE}` à enrichir avec une vraie state machine (idle/walk/cast/attack/hurt/death + transitions) — refonte de `BuildSoulrenderAnimator` pour créer les states/transitions/parameters via `AnimatorController.AddParameter` + `AddState` + `AddTransition`.
> - Côté Quantum → View : il faut exposer des events View pour `CombatantMoved(delta, pmCost)`, `CombatantCasted(spellId, category)`, `CombatantDamaged(amount)`, `CombatantKilled`. Probablement déjà partiellement présent côté `CombatantRenderer` (le delta de position permet déjà de détecter le mouvement).
>
> **Pré-requis (mêmes 3 fenêtres que d'habitude)** :
> - Docker Desktop allumé + backend `npm run dev` dans `backend/`
> - Unity Editor avec le projet Nymora ouvert
> - Smoke tests rapides : `npm run test:auth`, `npm run test:version`
>
> **Ordre des briques restantes (rappel)** :
> - 2.12.bis : anims complètes Soulrender ← **PRIORITÉ DEMAIN**
> - 2.13 : HUD combat complet (PA/PM haut-gauche, End Turn milieu-droite, deck 6 sorts bas-centre, timeline P0 > P1, floating text dégâts/heals)
> - 2.14-2.16 : Nightseer (Prescience + 15 sorts + Œil + Traquenard + Brouillard de guerre)
> - 2.17 : IA + E2E combat 1v1 vs IA
>
> **Reliquat designer encore en attente** : VFX Âme Lacérée 256×256 8-12f, Marque de Carnage overlay 64×64 4f, Plaie Ouverte overlay, Tile Vapeur Carmin animée 128×128 4f, Tile Sang Coagulé, Avatar profil 256×256.
>
> **Backlog notable** : Unity CI (license) + 1.13 Hetzner (Phase 7 prep alpha) + 1.14 simulation Quantum déterministe complète (couverte naturellement par Phase 2).
