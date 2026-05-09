# 📍 STATUT ACTUEL DU PROJET NYMORA

> **À mettre à jour à chaque fin de session avec Claude.**  
> Ce fichier écrase tous les autres docs en cas de conflit. C'est la source de vérité du moment présent.

**Dernière mise à jour :** 10 mai 2026  
**Mis à jour par :** Claude (session courante)

---

## 🎯 OÙ ON EN EST

**Phase actuelle :** Phase 1 — Netcode + Backend  
**Brique en cours :** **1.9 — Client Unity : intégration Photon Quantum + Auth (Custom Auth)**  
**Statut brique :** À démarrer

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

### Brique 1.9 — Client Unity : intégration Photon Quantum + Auth (Custom Auth)

**Objectifs (extraits de `05_Roadmap_V2_Novice.md`) :**
1. Lier l'authentification backend avec la connexion Photon (Custom Auth)
2. Le client envoie le JWT à Photon, Photon valide via webhook backend
3. Si JWT invalide, connexion Photon refusée

**Validation attendue :** connexion Photon Quantum uniquement avec un JWT valide ; le backend logge la validation à chaque connexion.

**Prochaine étape après validation :** Brique 1.10 — Système de versioning runtime (GameVersion + CombatRulesVersion + endpoint /version)

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

> 1. À la prochaine session, dire : **"On démarre la Brique 1.9 chef"** (Custom Auth Photon)
> 2. **Avoir 3 fenêtres prêtes** :
>    - Docker Desktop allumé (`docker compose ps` depuis `backend/` doit montrer Postgres + Redis Up)
>    - Backend Express : `cd backend && npm run dev` dans une fenêtre cmd (laisser ouverte)
>    - Unity Editor avec le projet Nymora ouvert
> 3. Smoke test rapide :
>    - `cd backend && npm run test:auth` doit afficher "Auth smoke test PASSED."
>    - Lancer Unity sur la scène 00_Login → Press Play → si déjà connecté, status "Connecte : DoctorL08"
> 4. Claude relira `05_Roadmap_V2_Novice.md` pour cadrer la 1.9 (intégration JWT ↔ Photon Custom Auth via webhook backend)
