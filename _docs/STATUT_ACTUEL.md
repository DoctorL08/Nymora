# 📍 STATUT ACTUEL DU PROJET NYMORA

> **À mettre à jour à chaque fin de session avec Claude.**  
> Ce fichier écrase tous les autres docs en cas de conflit. C'est la source de vérité du moment présent.

**Dernière mise à jour :** 11 mai 2026  
**Mis à jour par :** Claude (session courante)

---

## 🎯 OÙ ON EN EST

**Phase actuelle :** **Phase 2 — Combat (Soulrender + Nightseer)** 🎮  
**Brique en cours :** **2.1 — À définir au début de la Phase 2**  
**Statut Phase 1 :** ✅ **CLÔTURÉE le 11 mai 2026** (1.13 reportée Phase 7, sinon 14/14 briques validées)

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

**Durée estimée :** 2 mois (~16 briques)

**Objectifs (extraits de `05_Roadmap_V2_Novice.md`) :**
- Système de grille de combat 15×17 cases
- Système de tour par tour avec PA/PM/HP
- Pathfinding A* pour les déplacements
- **Soulrender complète** : 15 sorts + signature + ressource Hémoglyphe + passif L'Appel du Sang
- **Nightseer complète** : 15 sorts + signature + ressource Prescience + passif L'Œil qui n'est pas
- Brouillard de guerre fonctionnel
- IA de combat niveau Easy et Medium
- **Combat 1v1 vs IA jouable bout en bout** ← LE moment de vérité gameplay

**Le détail brique par brique de la Phase 2 sera livré au début de la prochaine session.**

**Prochaine étape :** Demander à Claude de cadrer la Phase 2 et lister les briques 2.1, 2.2, ...

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

> 1. À la prochaine session, dire : **"On démarre la Phase 2 chef"** 🎮
> 2. **Pré-requis Phase 2** (mêmes 3 fenêtres que d'habitude, pas de nouveau setup) :
>    - Docker Desktop allumé (`docker compose ps` depuis `backend/` montre Postgres + Redis Up)
>    - Backend Express : `cd backend && npm run dev` dans une fenêtre cmd (laisser tourner)
>    - Unity Editor avec le projet Nymora ouvert
> 3. Pas besoin de ngrok pour la Phase 2 (le combat est local jusqu'à la fin Phase 3)
> 4. Smoke tests rapides (sanity check) au démarrage :
>    - `cd backend && npm run test:auth` → "Auth smoke test PASSED."
>    - `cd backend && npm run test:version` → "Version smoke test PASSED."
> 5. Claude lira `01_BIBLE_V7.1_Combat.md` (stats, sorts, classes) + `05_Roadmap_V2_Novice.md` pour cadrer la Phase 2 et te livrer la liste des briques 2.x. Probablement on commencera par : système de grille 15×17 + Position2D + Tile data + visualisation grille.
> 6. **Backlog notable** à reprendre plus tard : Unity CI (license) + 1.13 Hetzner (Phase 7 prep alpha) + 1.14 simulation Quantum déterministe complète (sera couverte naturellement par Phase 2 quand on aura du combat à tester).
