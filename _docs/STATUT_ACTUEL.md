# 📍 STATUT ACTUEL DU PROJET NYMORA

> **À mettre à jour à chaque fin de session avec Claude.**  
> Ce fichier écrase tous les autres docs en cas de conflit. C'est la source de vérité du moment présent.

**Dernière mise à jour :** 8 mai 2026  
**Mis à jour par :** Claude (session courante)

---

## 🎯 OÙ ON EN EST

**Phase actuelle :** Phase 0 — Fondations projet (étendue avec scan auto)  
**Brique en cours :** **0.8 — ScriptableObject SpellDefinition (template)**  
**Statut brique :** À démarrer (S — 1 jour)

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

## 🔄 BRIQUE EN COURS

### Brique 0.8 — ScriptableObject SpellDefinition (template vide)

**Objectifs :**
1. Créer la structure complète de `SpellDefinition` (identity, cost, targeting, effects, versioning)
2. Enums supports : `SpellEffectType`, `TargetingShape`, `TargetingFilter`
3. Struct `SpellEffect` (sérialisable, list dans SpellDefinition)
4. Editor Tool pour générer un `_Template_Spell.asset` vide
5. **PAS** de remplissage des 75 sorts à ce stade — juste la structure

**Prochaine étape après validation :** Brique 0.9 — Roslyn Analyzers + ruleset custom

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

> 1. Suivre la **Brique 0.1** (instructions détaillées dans le dernier message Claude ou dans `05_Roadmap_V2_Novice.md`)
> 2. Cocher la checklist validation ci-dessus au fur et à mesure
> 3. Au début de la prochaine session Claude, dire : **"Brique 0.1 validée chef"** + montrer une capture du projet Unity ouvert
> 4. Si bug : coller le texte de l'erreur console
