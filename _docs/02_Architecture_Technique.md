# Nymora — Architecture Technique

> Source : `Nymora_DOC1_Architecture_Technique.docx`

---

# **NYMORA**

## **ARCHITECTURE TECHNIQUE**

*Stack · Netcode · Backend · Sécurité · Anti-desync*

*Document 1/3 — V7.1*

Solo dev · Unity · **Alpha Windows-only** (Mac + Mobile post-alpha) · F2P

# **EXECUTIVE SUMMARY**

*Ce document définit l'architecture technique complète de Nymora : stack technologique, séparation des modes (map communautaire / IA / ranked), netcode anti-desync, backend live service, sécurité et persistance compte. C'est le DOCUMENT DE RÉFÉRENCE qui doit être consulté à chaque décision technique majeure.*

| PRINCIPE FONDATEUR Nymora est un jeu live service multiplateforme où chaque match ranked enregistre du MMR sur un compte persistant. Toute desync ou exploit côté client peut potentiellement corrompre le ladder. L'architecture entière est conçue autour de ce principe : LE SERVEUR EST LA SEULE SOURCE DE VÉRITÉ. |
|---|

## **🪟 STRATÉGIE PLATEFORME : ALPHA WINDOWS-ONLY**

**Décision majeure (8 mai 2026) :** la phase alpha cible **Windows uniquement**. Mac et Mobile sont reportés en Phases 8 et 9 post-alpha.

### Justification

- **Réduction du scope** : 1 plateforme au lieu de 4 → ~1.5 à 2 mois économisés sur la roadmap initiale (12 mois au lieu de 14 pour alpha complète)
- **Réduction des cas particuliers** : pas de touch input, pas de IL2CPP, pas de gestion énergie/thermal mobile, pas de variabilité d'écrans
- **Distribution simplifiée** : Steam Playtest ou itch.io au lieu de TestFlight + Google Play Internal
- **Paiements simplifiés** : Stripe uniquement (Apple/Google IAP en post-alpha)
- **Tests simplifiés** : un seul environnement à valider en CI/CD

### Conséquences techniques

| Élément | Choix alpha | Post-alpha |
|---|---|---|
| Build Target | Windows Standalone (Mono x64) | + Mac + iOS + Android |
| Scripting Backend | Mono (rapide à itérer) | + IL2CPP pour mobile/Mac |
| Input | Clavier + souris | + Touch + gestures |
| UI | Anchors classiques | + Safe areas mobile + scaling |
| Audio | Pas de gestion focus/background | + Audio interruption iOS, audio focus Android |
| Stockage | PlayerPrefs + fichiers locaux | + iCloud sync, Google Play Saves |
| Paiements | Stripe (web checkout via Steam) | + StoreKit (iOS), BillingClient (Android) |
| Distribution | Steam Playtest, itch.io | + App Store, Google Play |
| CI/CD | Build Windows uniquement | + Builds Mac/Android/iOS auto |

### Briques anticipées en post-alpha (Phase 8 + Phase 9)

**Phase 8 — Extension Mac (~1 mois) :**
- Migration vers IL2CPP, build Mac, signing Apple Developer, distribution DMG, test sur Apple Silicon + Intel.

**Phase 9 — Extension Mobile (~2 mois) :**
- Refonte UI mobile (touch + safe areas), IAP Apple/Google, tests appareils Android variés, optimisation perf mobile, soumission stores.

Cette stratégie permet à Lorenzo de **livrer un jeu Windows alpha totalement fonctionnel et stable** avant d'élargir, plutôt que de coder 4 plateformes à moitié.

## **🛡️ OUTILS DE SCAN AUTOMATIQUE (intégrés en Phase 0)**

Pour minimiser les bugs structurels et les régressions, le projet intègre dès la Phase 0 plusieurs **garde-fous techniques** qui scannent le code en continu.

### 1. Roslyn Analyzers + ruleset custom Nymora

**Quoi :** analyse statique du code C# à chaque compilation Visual Studio.

**Détecte :**
- Memory leaks potentiels, code unreachable
- Allocations dans `Update()`, GC pressure
- Anti-patterns Unity (`GameObject.Find` dans Update, `transform.position` répétés, etc.)
- Variables qui devraient être `readonly` ou `static`
- Méthodes deprecated
- Règles custom Nymora : interdiction de `Random.Range`, `Time.time`, `float` dans `Assets/_Nymora/Scripts/Combat/Simulation/`

**Installation :** package NuGet `Microsoft.CodeAnalysis.NetAnalyzers` + ruleset XML dans `Assets/_Nymora/AnalyzerConfig/Nymora.ruleset`.

### 2. Editor Script `Nymora_HealthCheck`

**Quoi :** outil custom Unity accessible via `Nymora > Validation > Project Health Check`.

**Scan complet du projet en 30 secondes :**
- ScriptableObjects orphelins (référencés nulle part)
- Valeurs gameplay hardcodées dans les scripts (regex sur HP, dégâts, coûts)
- Scripts qui violent les règles d'asmdef
- Références cassées dans les scènes (missing scripts, missing prefabs)
- Sprites/audios non utilisés
- Tags ou Layers orphelins
- Violations Quantum (Random.Range, Time.time, float dans la simulation)

**Output :** rapport en console Unity + génération d'un fichier `_docs/healthcheck_report.md` avec timestamp.

**Quand l'utiliser :**
- Avant chaque commit important
- En fin de chaque brique
- Avant chaque tag de version

### 3. Pre-commit Git hook

**Quoi :** script bash exécuté automatiquement avant chaque `git commit`.

**Bloque le commit si :**
- Console Unity a des erreurs (lecture du fichier `Logs/Editor.log`)
- `Random.Range`, `Time.time`, `DateTime.Now` détectés dans le combat
- Nouvelles valeurs hardcodées dans le combat
- Message de commit non-conventionnel (pas de `feat(...)`, `fix(...)`, `chore(...)`, etc.)

**Installation :** fichier `.git/hooks/pre-commit` créé en Brique 0.3.

### 4. Console Filter Nymora

**Quoi :** custom filter Unity qui n'affiche en console que les logs **du code Nymora**, pas des packages tiers (Photon, URP, etc.).

**Activation :** un toggle dans la barre de la Console Unity (`Nymora Logs Only`).

### Philosophie : FAIL FAST

> *Mieux vaut 100 erreurs détectées en 30 secondes par un outil que 10 erreurs trouvées en 3 jours par debug runtime.*

Cette philosophie guide chaque décision d'architecture du projet. Tout investissement en garde-fou technique en amont est rentabilisé par les heures de debug évitées en aval.

## **LES 5 DÉCISIONS ARCHITECTURALES MAJEURES**

| DÉCISION | JUSTIFICATION |
|---|---|
| Split netcode | Photon Quantum (deterministic) pour le combat ranked + Photon Fusion (state sync) pour la map communautaire. Deux netcodes différents pour deux problèmes différents. |
| Backend custom | Serveur Node.js + PostgreSQL + Redis pour l'authentification, MMR, économie, inventaire. PlayFab/Unity Gaming Services évalués mais coût et lock-in trop élevés à long terme. |
| Authoritative server pour le combat | Aucun calcul de dégâts/PA/PM/passifs côté client. Le client est un AFFICHAGE + INPUTS uniquement. Anti-cheat de fait. |
| Maps physiquement séparées par mode | 3 scènes Unity totalement isolées : MapCommu (Mirror over Photon), MapIA (offline pur), MapRanked (Quantum). Aucun code partagé sauf data assets. |
| Versioning stricte des règles de combat | Toute modification de sort/passif/règle = bump de version (CombatRules v1.4.2). Les replays et MMR sont liés à une version. Évite les corruptions de ladder lors de patches. |

# **STACK TECHNIQUE**

## **LE PROJET ACTUEL — INVENTAIRE**

Le projet Unity 2022.3.62f3 existant reste la base. Les briques suivantes sont conservées et étendues :

- Unity 2022.3 LTS (URP) — gardée jusqu'à mi-projet, migration LTS 6 envisagée pour mobile (voir Phase 4 de la roadmap)

- Photon PUN 2 — REMPLACÉ par Photon Quantum + Fusion (voir section Netcode)

- TextMesh Pro — gardé

- DOTween — gardé pour les animations UI

- ParrelSync — gardé pour tests local 2-instances

- Système combat existant (CombatInitializer, OracleCombatNetBridge) — REFONTE complète sur Quantum

## **STACK CIBLE COMPLÈTE**

| CATÉGORIE | TECHNO | POURQUOI |
|---|---|---|
| Moteur | Unity 2022.3 LTS → Unity 6 LTS (Q3 2026) | URP, isométrique, sprites 128x128 |
| Netcode combat | Photon Quantum 3 | Déterministe, rollback, replay-friendly, ANTI-DESYNC by design |
| Netcode map commu | Photon Fusion 2 (Shared Mode) | State sync léger, pas besoin de déterminisme pour du chat/déplacement libre |
| Voice chat | Aucun (pas de voice) — chat texte uniquement | Économie de ressources serveur, simplicité de modération |
| UI | Unity UI Toolkit (UI Builder) | Scaling responsive PC/mobile, performances meilleures que UGUI |
| Backend API | Node.js + Express + TypeScript | Maintenable solo, énorme écosystème |
| Base de données | PostgreSQL 16 | ACID, requêtes complexes pour ranked/stats, mature |
| Cache & sessions | Redis 7 | MMR temps réel, matchmaking queues, leaderboard, sessions |
| Auth | JWT + refresh tokens | Standard, multiplateforme, pas de session serveur |
| Stockage assets | CDN (Cloudflare R2 ou AWS S3) | Cosmétiques, hotfix data, replays |
| Logs & monitoring | Grafana + Loki + Prometheus | Auto-hébergeable, gratuit, dashboards essentiels |
| CI/CD | GitHub Actions | Build auto Windows pour alpha (Mac/Android/iOS post-alpha) |
| Crash reporting | Sentry | Free tier suffisant en early access |
| Paiements | Stripe (PC) + Apple IAP + Google Play Billing | 3 SDK distincts, abstraction côté backend |
| Anti-cheat | Côté serveur uniquement (Quantum est déterministe + authoritative) | Pas d'EAC/BattlEye à payer en early |

| ÉVALUATION DES BAAS PourQuoi pas PlayFab / Unity Gaming Services ? Coût qui scale très vite avec la base joueurs (paiement par MAU + par opération), vendor lock-in fort, customisation limitée pour des features comme le système de clan ou le replay. À 1000 joueurs actifs/jour, un serveur custom Node.js sur un VPS à 20€/mois suffit largement. |
|---|

# **NETCODE — LE CŒUR DE L'ANTI-DESYNC**

| RECOMMANDATION CRITIQUE Tu as posé la question "quel netcode". Voici ma reco DÉTAILLÉE avec justification technique. Cette section est la plus critique du document — un mauvais choix de netcode = un jeu unjouable en ranked. |
|---|

## **LE PROBLÈME**

Nymora a 3 contextes réseau RADICALEMENT différents :

- MAP COMMUNAUTAIRE : 20-50 joueurs qui se baladent, chattent, défi casual. Pas de précision requise. État partagé léger (positions, messages chat).

- MODE IA (3 difficultés) : OFFLINE pur. Aucun réseau. Calculs locaux uniquement. La sauvegarde de stats remonte au backend en fin de match via API REST.

- RANKED 1v1 / 2v2 / 3v3 : 2 à 6 joueurs en duel intense. Calcul de dégâts précis, ressources, marques, leurres, passifs. AUCUNE desync tolérée — un joueur qui voit 3 leurres alors que l'autre en voit 2 = match corrompu.

| ATTENTION Un netcode mal calibré pour le contexte = jeu cassé. Photon PUN 2 (ce que tu as actuellement) est OK pour la map commu mais NE GÉRE PAS l'anti-desync sur les marques persistantes, les leurres Ghostra ou le timing des passifs Soulrender. Il faut une stack double. |
|---|

## **DÉCISION : SPLIT NETCODE**

Deux netcodes coexistent dans le client. Chaque mode initialise UNIQUEMENT le netcode dont il a besoin.

|  PHOTON QUANTUM (combat ranked) ▸ Déterministe par design — même inputs = même résultats sur tous les clients ▸ Rollback netcode (retour en arrière + rejeu) pour masquer la latence ▸ Anti-cheat naturel : le serveur valide chaque frame ▸ Replays gratuits — il suffit de stocker les inputs ▸ Excellent pour les jeux où chaque chiffre compte (Nymora typique) ▸ Coût : licence à partir de 500€ one-time pour 100 CCU |  PHOTON FUSION (map commu) ▸ State sync classique (interpolation/extrapolation) ▸ Bien plus léger à intégrer pour des scènes hub ▸ Shared Mode = pas besoin de serveur dédié, un client est host ▸ Suffit largement pour 50 joueurs qui marchent et chattent ▸ Inclus dans le plan Photon standard (pas de coût additionnel) ▸ Permet le défi casual (instancie un combat Quantum entre 2 joueurs) |
|---|---|

## **FLUX DE TRANSITION ENTRE NETCODES**

Quand un joueur sur la map commu défie un autre joueur :

| [Joueur A clique "défier" sur Joueur B] ↓ [Photon Fusion envoie ChallengeRequest à B via RPC] ↓ [Joueur B accepte → ChallengeAccepted RPC] ↓ [Backend reçoit "casual_match_request" avec userId A et B] ↓ [Backend crée une room Quantum dédiée + token de session] ↓ [Les deux clients quittent Fusion (map commu)] ↓ [Les deux clients chargent la scène CombatRanked + connectent à Quantum room] ↓ [Combat se déroule en deterministic-rollback] ↓ [Fin de match → résultats POSTés au backend → MMR/stats updated] ↓ [Les deux clients retournent sur Fusion (map commu)] |
|---|

## **ANTI-DESYNC — RÈGLES STRICTES**

| RÈGLES NON-NÉGOCIABLES Ces 7 règles doivent être respectées sans exception. Toute violation introduit potentiellement de la desync. |
|---|

- Aucun appel à Random.Range() ou Time.time dans le code de combat. Utiliser FrameSession.Random et FrameSession.Tick fournis par Quantum.

- Aucun float non-déterministe dans la logique de combat. Quantum impose des FP (fixed-point) pour les calculs critiques. Les dégâts, HP, distances doivent être en FP.

- Aucune référence à des composants Unity (Transform, Rigidbody) dans la simulation Quantum. Le rendu est SÉPARÉ de la simulation.

- Aucun input client traité directement. Tous les inputs passent par Quantum.Input et sont validés/rejetés par la simulation.

- Versioning strict : chaque release a une CombatRulesVersion (ex: 1.4.2). Les rooms Quantum vérifient que les deux clients ont la même version, sinon refus de connexion.

- Replays = inputs + version + seed. Pas de snapshot d'état. Si on rejoue avec la même version + inputs + seed, on obtient exactement le même match.

- Si un client détecte une desync (state hash différent du serveur), il déconnecte et le match est annulé côté MMR. Pas de "on essaye de réconcilier".

# **SÉPARATION DES MODES — ANTI-BUG STRUCTUREL**

*Tu as explicitement demandé que les maps communautaire / arène IA / ranked soient "complètement différentes pour éviter les desync et les bugs". Voici l'architecture qui garantit ça.*

## **PRINCIPE D'ISOLATION TOTALE**

| RÈGLE D'OR Chaque mode est dans une scène Unity TOTALEMENT séparée, avec son propre netcode initialisé, ses propres assets, ses propres règles de combat. Aucun script de gameplay n'est partagé entre modes — uniquement les ScriptableObjects de data (sorts, stats, classes). |
|---|

## **ARBORESCENCE DES SCÈNES**

| Assets/_Game/Scenes/ ├── 00_Boot.unity              ← Init Photon, auth, version check ├── 01_MainMenu.unity          ← Login, create account, settings ├── 02_HubCommu.unity          ← Map communautaire (Photon Fusion) │   └── Mode : MMO-lite / 50 joueurs max / chat │ ├── 10_DeckBuilder.unity       ← Sélection deck (offline) ├── 11_Profile.unity           ← Profil joueur (offline + API calls) ├── 12_Shop.unity              ← Boutique (offline + API calls) ├── 13_Settings.unity          ← Paramètres (offline) │ ├── 20_ArenaMenu.unity         ← Choix mode (IA / Ranked) │ ├── 30_CombatIA.unity          ← Combat solo vs IA (OFFLINE) │   └── Aucun netcode, calcul local, save async │ ├── 40_CombatRanked1v1.unity   ← Combat ranked 1v1 (Quantum) ├── 41_CombatRanked2v2.unity   ← Combat ranked 2v2 (Quantum) ├── 42_CombatRanked3v3.unity   ← Combat ranked 3v3 (Quantum) │   └── Trois scènes distinctes pour optimiser le placement et l'UI │ └── 99_Loading.unity           ← Transition (loading screen) |
|---|

## **MATRICE DES MODES**

| MODE | NETCODE | JOUEURS | MULTI | BACKEND | FONCTION |
|---|---|---|---|---|---|
| Map Commu | Photon Fusion (Shared) | 50 joueurs | OUI (chat, déplacement, défis) | OUI (Fusion + REST API) | Hub social |
| Combat IA | Aucun (offline) | 1 joueur | NON (calcul local) | OUI (REST async fin de match) | Training, fun, missions |
| Ranked 1v1 | Photon Quantum | 2 joueurs | OUI (Quantum tick = 30Hz) | OUI (REST début + fin) | MMR principal |
| Ranked 2v2 | Photon Quantum | 4 joueurs | OUI (Quantum tick = 30Hz) | OUI (REST début + fin) | MMR teams 2v2 |
| Ranked 3v3 | Photon Quantum | 6 joueurs | OUI (Quantum tick = 30Hz) | OUI (REST début + fin) | MMR teams 3v3 |

## **CODE PARTAGÉ vs CODE ISOLÉ**

|  PARTAGÉ ENTRE TOUS LES MODES ▸ ScriptableObjects de sorts (data uniquement) ▸ Definitions de classes (data) ▸ Assets sprites/animations ▸ UI commune (chat, menu pause) ▸ Système de saving local ▸ Authentification & profil |  ISOLÉ PAR MODE ▸ Scripts de gameplay (logique combat, IA, etc) ▸ Composants réseau (Quantum vs Fusion) ▸ Scripts de placement / spawn ▸ Logique de grille (différent par taille) ▸ Logique de fin de match (REST endpoint différent) ▸ Effets visuels lourds (optimisations par mode) |
|---|---|

## **POURQUOI 3 SCÈNES DE COMBAT DISTINCTES (1v1, 2v2, 3v3) ?**

- TAILLE DE GRILLE différente : 12x10 en 1v1, 14x12 en 2v2, 16x14 en 3v3. Hardcodée par scène pour zéro confusion.

- PLACEMENT DES UNITÉS différent : 1 spawn par côté en 1v1, 2 en 2v2, 3 en 3v3. Logique de spawn isolée.

- UI HUD différente : barre de team, ordres alliés visibles, ping system en team modes.

- PERFORMANCES : 1v1 peut tourner à 60 fps mobile, 3v3 vise 30 fps mobile (plus d'unités, plus d'effets). Optimisations spécifiques par scène.

- TESTING : un bug en 3v3 ne casse pas le 1v1. Régressions isolées.

# **BACKEND — LIVE SERVICE**

## **ARCHITECTURE GLOBALE**

| ┌──────────────────────────────────────────────────────┐ │                   CLIENT UNITY                        │ │  (PC / Mac / Mobile)                                  │ └──────────┬───────────────────────────┬───────────────┘ │                           │ │  REST API (HTTPS)         │  WebSocket │  (auth, profile, MMR,     │  (chat global, │   shop, deck, stats)      │   notifications) │                           │ ▼                           ▼ ┌──────────────────────────────────────────────────────┐ │              API GATEWAY (Node.js)                    │ │  - JWT validation                                     │ │  - Rate limiting (anti-spam)                          │ │  - Routing vers services                              │ └────┬───────────┬───────────┬───────────┬─────────────┘ │           │           │           │ ▼           ▼           ▼           ▼ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ │  AUTH   │ │ PROFILE │ │MATCHMAK.│ │ECONOMY  │ │ Service │ │ Service │ │ Service │ │ Service │ └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘ │           │           │           │ └─────┬─────┴─────┬─────┴─────┬─────┘ ▼           ▼           ▼ ┌─────────┐ ┌──────────┐ │POSTGRES │ │  REDIS   │ │  (data) │ │  (cache) │ └─────────┘ └──────────┘ ┌─────────────────┐ │  PHOTON CLOUD   │ │  (Quantum +     │ │   Fusion rooms) │ └─────────────────┘ |
|---|

## **SERVICES BACKEND**

| SERVICE | ENDPOINT | RÔLE |
|---|---|---|
| AUTH | /api/auth/* | Login, register, refresh token, password reset, account deletion (RGPD) |
| PROFILE | /api/profile/* | Stats joueur, niveaux par classe, succès, cosmétiques équipés, clan |
| DECK | /api/deck/* | CRUD des decks de 6 sorts par joueur (max 5 decks/classe) |
| MATCHMAKING | /api/match/* | Queue 1v1/2v2/3v3, MMR-based pairing, validation post-match |
| ECONOMY | /api/shop/*, /api/wallet/* | Nymos (currency), achats cosmétiques, battle pass |
| SOCIAL | /api/clan/*, /api/friends/* | Clans (création, invite, kick, level), liste d'amis |
| LEADERBOARD | /api/leaderboard/* | Top 100 global, par classe, par mode (Redis sorted sets) |
| CHAT | WebSocket /chat | Chat global, clan, privé, modération, filtres |
| TELEMETRY | /api/telemetry/* | Stats anonymes pour balancing (pickrate, winrate, durée) |
| IAP | /api/iap/verify | Validation des achats Apple/Google/Stripe, anti-fraude |

## **SCHÉMA DE BASE DE DONNÉES — VUE D'ENSEMBLE**

Tables principales PostgreSQL (15 tables au total) :

| TABLE | COLONNES PRINCIPALES |
|---|---|
| accounts | id, email, username, password_hash, created_at, last_login, banned, country |
| account_progress | account_id, total_xp, account_level, achievements_count, created_at |
| class_levels | account_id, class_id (1-5), xp, level (1-50), spells_unlocked |
| decks | id, account_id, class_id, name, slot_1..6 (spell_ids), created_at, last_used |
| mmr_records | account_id, mode (1v1/2v2/3v3), mmr, peak_mmr, season_id, games_played |
| match_history | id, mode, started_at, ended_at, winner_account_id, version, replay_url |
| match_participants | match_id, account_id, class_id, deck_used, mmr_before, mmr_after, kills, deaths |
| wallet | account_id, nymos_balance, premium_balance, last_updated |
| transactions | id, account_id, type (purchase/reward/refund), amount, source, item_id, ts |
| inventory | account_id, item_id (skin/banner/title/etc), acquired_at, source |
| clans | id, name, tag, level, xp, leader_id, created_at, member_count, motto |
| clan_members | clan_id, account_id, role (leader/officer/member), joined_at, contribution |
| friends | account_id, friend_id, status (pending/accepted), created_at |
| chat_logs | id, channel (global/clan/private/system), sender_id, recipient_id, message, ts, deleted |
| bans_reports | id, reporter_id, reported_id, reason, status, ts, action_taken |

| INFO Toutes les tables ont des index sur account_id et timestamp. Les MMR_records utilisent Redis comme cache (lecture < 1ms) avec sync vers PostgreSQL toutes les 30s. Les matchs ranked écrivent en PostgreSQL en transaction ACID pour garantir cohérence MMR/stats/wallet. |
|---|

# **SÉCURITÉ & ANTI-CHEAT**

## **PRINCIPES**

| STRATÉGIE Le seul anti-cheat solide en F2P solo dev est l'AUTHORITATIVE SERVER. Pas EAC (cher, complexe), pas BattlEye. Le serveur valide tout. Le client est aveugle aux dégâts qu'il ne devrait pas voir. |
|---|

## **LES 8 VECTEURS D'ATTAQUE & LEURS PARADES**

| VECTEUR | PARADE |
|---|---|
| Modification de mémoire (CheatEngine) | Le client n'a JAMAIS la valeur HP réelle. Il a juste un display. Quantum simulation côté serveur tient l'état. |
| Speedhack / TimeScale | Quantum tick à 30Hz fixe. Si le client envoie trop d'inputs en un tick, ils sont dropped. La simulation continue à son rythme. |
| Replay attack (replay des packets) | Chaque input est signé avec un nonce + timestamp. Replays détectés = match annulé. |
| Ghost client / Bot | Captcha à la création de compte. Détection de patterns (clicks parfaits, timing impossible). Shadow ban si détecté. |
| MMR manipulation (smurfing extrême) | Décalage MMR limité par classe (max +200/-200 par match). Détection de win streak vs accounts neufs. |
| IAP spoofing (fausse validation) | Toute IAP est validée côté serveur via API officielle Apple/Google. Pas de validation client-side. |
| Account sharing / piratage | 2FA optionnel mais recommandé. Detection IP+device. Email de notification login. |
| Exploits client | Sentry pour détecter les crashes anormaux. Logs serveur pour patterns suspicieux (1000 sorts/min, etc). |

## **DONNÉES SENSIBLES & RGPD**

- Passwords stockés en bcrypt (cost 12) — JAMAIS en clair, jamais en MD5/SHA1

- Emails chiffrés au repos (AES-256) dans PostgreSQL via pgcrypto

- Logs chat conservés 30 jours max (modération) puis purgés automatiquement

- Endpoint /api/auth/delete-account qui purge TOUTES les données (RGPD obligatoire)

- Endpoint /api/auth/export-my-data qui retourne un JSON de toutes les données du compte (RGPD)

- Pas de tracking analytics tiers (Google Analytics, Facebook Pixel) — privacy-friendly

## **RATE LIMITING**

Tous les endpoints API ont du rate limiting via Redis :

| ENDPOINT | LIMITE | RAISON |
|---|---|---|
| Login attempts | 5 / IP / 15min | Anti-bruteforce |
| Account creation | 1 / IP / 1h | Anti-bot massif |
| Chat messages | 5 / sec / account | Anti-spam |
| Friend requests | 20 / day / account | Anti-spam social |
| Match queue | 1 / 5 sec / account | Anti-flood matchmaking |
| Shop purchase | 10 / hour / account | Anti-fraude IAP |

# **VERSIONING & DÉPLOIEMENT**

## **PRINCIPE — VERSIONS MULTIPLES SIMULTANÉES**

| CRITIQUE Un live service ne peut pas casser les replays anciens ni les MMR records. Chaque match a une CombatRulesVersion qui ne CHANGE JAMAIS rétroactivement. Les replays de la v1.0 doivent être lisibles en v1.5. |
|---|

## **STRATÉGIE DE VERSIONING**

| TYPE | FORMAT | ROLE |
|---|---|---|
| Game version | 1.4.2 (semver) | Affichée au joueur. Bump à chaque release client. |
| CombatRulesVersion | 1.4.2 (synchro game version) | Numéros de stats, sorts, passifs. Locked dans une room Quantum. |
| Protocol version | 12 (entier) | Format des messages réseau. Bump quand on change un schema. |
| Database schema version | 27 (entier) | Migrations PostgreSQL via Knex/Prisma. |
| API version | v1 (path /api/v1/*) | URL versionnée pour breaking changes futurs. |
| Asset bundle version | 1.4.2-mobile (+ hash) | CDN par plateforme, content delivery. |

## **CYCLE DE RELEASE**

| SEMAINE 1-2 : DEV - Features développées sur branche feature/* - Tests unitaires verts - Code review (par toi-même : checklist + 1 jour de pause)  SEMAINE 3 : QA INTERNE - Build interne sur staging server - Tests manuels : tous les modes, tous les edge cases - Vérifier le LOG des matchs Quantum (state hash check)  SEMAINE 4 : SOFT RELEASE (5% des joueurs) - Feature flag activé pour 5% random - Monitoring crash rate, desync rate, retention - Si crash > 0.5% ou desync > 0.1% : ROLLBACK  SEMAINE 5 : ROLLOUT 100% - Si métriques OK : 25% J+1, 50% J+2, 100% J+3 - Patchnote publié sur Discord + in-game - Saison ranked redémarre si patch majeur (.X.0) |
|---|

## **HOTFIX PROCEDURE**

Pour les bugs critiques détectés en production :

- P0 (game-breaking, exploit MMR, crash systématique) : hotfix sous 24h via patch côté serveur si possible (data-driven), sinon rebuild client

- P1 (annoying mais contournable) : fix dans le prochain release programmé

- P2 (cosmetic, edge case rare) : backlog, fix au prochain sprint

- Si hotfix change CombatRulesVersion : tous les matchs en cours deviennent invalides — annulés sans perte MMR

# **HOSTING & COÛTS PRÉVISIONNELS**

## **INFRA RECOMMANDÉE — 3 PHASES**

### **Phase 1 — Soft launch (0-1000 joueurs/jour)**

| RESSOURCE | FOURNISSEUR | COÛT |
|---|---|---|
| VPS Backend | Hetzner CX22 (2 vCPU, 4GB RAM) | 5€/mois |
| VPS Database | Hetzner CX22 (PostgreSQL + Redis colocated) | 5€/mois |
| Photon Cloud | Plan Indie (100 CCU) | 95€/mois |
| CDN | Cloudflare R2 (free tier) | 0€ |
| Domain + SSL | Namecheap + Let's Encrypt | 10€/an |
| Monitoring | Self-hosted Grafana sur même VPS | 0€ |
| TOTAL | — | ≈ 105€/mois |

### **Phase 2 — Growth (1k-10k joueurs/jour)**

| RESSOURCE | FOURNISSEUR | COÛT |
|---|---|---|
| Backend | Hetzner CCX23 (8 vCPU, 32GB) + load balancer | 60€/mois |
| Database | Hetzner CCX23 dédié + replica | 60€/mois |
| Photon Cloud | Plan Pro (500 CCU) | 475€/mois |
| CDN | Cloudflare R2 (paid tier) | 20€/mois |
| Backups | S3 Glacier ou similaire | 10€/mois |
| Monitoring | Grafana Cloud free tier (limité) | 0€ |
| TOTAL | — | ≈ 625€/mois |

### **Phase 3 — Scale (10k+ joueurs/jour)**

À ce stade, le projet génère assez de revenus pour justifier une migration cloud (AWS/GCP) avec scaling automatique. Coût ≈ 2k-5k€/mois mais avec revenus supérieurs (un F2P sain à 10k DAU génère 5-15k€/mois en battle pass + cosmétiques).

| ASTUCE Solo dev en early : ne pas surinvestir en infra. Hetzner + Photon Cloud Indie suffit pour soft launch. Le coût total à phase 1 (105€/mois) est largement absorbable, même sans revenus encore. |
|---|

# **ARCHITECTURE DU CLIENT UNITY**

## **ORGANISATION DES SCRIPTS**

| Assets/_Game/Scripts/ ├── Core/                       ← Singletons, services │   ├── GameManager.cs          ← Orchestrateur global │   ├── SceneLoader.cs          ← Transitions entre scènes │   ├── BackendService.cs       ← API REST wrapper │   └── EventBus.cs             ← Communication inter-systèmes │ ├── Auth/                       ← Login, account ├── Profile/                    ← Profil, stats, succès ├── Deck/                       ← Deck builder + ScriptableObjects ├── Shop/                       ← Boutique ├── Settings/                   ← Paramètres ├── Social/                     ← Clan, amis, chat │ ├── Combat/                     ← LOGIQUE DE COMBAT (data + rules) │   ├── Data/                   ← ScriptableObjects sorts/classes/passifs │   │   ├── SpellData.cs │   │   ├── ClassData.cs │   │   ├── PassiveData.cs │   │   └── ResourceData.cs │   ├── Rules/                  ← Règles déterministes │   │   ├── DamageCalculator.cs │   │   ├── ResourceTracker.cs │   │   └── PassiveResolver.cs │   └── Simulation/             ← QUANTUM SIMULATION │       ├── QSpellSystem.cs │       ├── QPassiveSystem.cs │       └── QGridSystem.cs │ ├── Combat.IA/                  ← Mode IA (offline) │   ├── AIBrain.cs │   ├── AIDifficultyEasy.cs │   ├── AIDifficultyMedium.cs │   └── AIDifficultyHard.cs │ ├── Combat.Network/             ← Couche réseau Quantum │   ├── QuantumRunner.cs │   ├── QuantumInputProvider.cs │   └── QuantumViewSync.cs │ ├── Hub/                        ← Map communautaire │   ├── HubFusionRunner.cs │   ├── HubPlayerController.cs │   └── ChallengeSystem.cs │ └── UI/                         ← Tous les écrans UI ├── HUD/ ├── Menus/ └── Common/ |
|---|

## **RÈGLES STRICTES D'ARCHITECTURE**

- Aucun MonoBehaviour dans Combat/Rules/* — uniquement des classes pures (testables sans Unity).

- Aucun appel API dans les Update() — toutes les requêtes backend passent par BackendService et sont async.

- Aucun scene direct load — toujours via SceneLoader.LoadScene() qui gère la cleanup et la cinétique de transition.

- Tous les ScriptableObjects de combat sont LOCKÉS par version. Toute modif doit incrémenter CombatRulesVersion.

- Le rendu des unités est SÉPARÉ de leur état logique (pattern View/Model). Quantum gère le state, Unity affiche.

- EventBus pour la communication inter-systèmes — éviter les références hard-coded (ex: HUD ne référence pas directement Combat).

## **PATTERN VIEW/MODEL POUR LE COMBAT**

| // MODEL (Quantum simulation - DETERMINISTIC) struct UnitState { FP HP;              // Fixed-point, déterministe FP MaxHP; int CurrentResource; EntityRef Position; BitArray ActiveMarks; }  // VIEW (Unity rendering - NON-DETERMINISTIC ok) class UnitView : MonoBehaviour { void OnUpdated(UnitState state) { healthBar.SetValue(state.HP.AsFloat); sprite.SetMarks(state.ActiveMarks); // Animations, particles, sound — tout ici } }  // SYNC : Quantum events → Unity views QuantumGame.AddCallback<UnitDamaged>(evt => { var view = ViewRegistry.Get(evt.Unit); view.PlayDamageAnimation(evt.Damage); }); |
|---|

