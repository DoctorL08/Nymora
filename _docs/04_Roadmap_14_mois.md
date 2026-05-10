# Nymora — Roadmap 14 Mois (V1)

> Source : `Nymora_DOC3_Roadmap_14_mois.docx`

---

# **NYMORA**

## **ROADMAP 14 MOIS**

*Solo dev · 7 phases · Anti-bug · Anti-desync*

*Document 3/3 — V7.1*

Mai 2026 → Juillet 2027 (Soft Launch)

# **VUE D'ENSEMBLE**

| PRINCIPE Cette roadmap est conçue pour UN développeur SEUL. Les durées intègrent une marge réaliste (le doublement classique solo dev). Chaque phase a des CHECKPOINTS QA stricts pour empêcher les bugs de s'accumuler. Aucune phase n'est skippable — chacune valide les fondations de la suivante. |
|---|

## **LES 7 PHASES**

| # | PHASE | DURÉE | OBJECTIF |
|---|---|---|---|
| 1 | FONDATIONS TECHNIQUES | 2 mois | Refonte netcode (Quantum + Fusion), backend MVP, auth, SpellResource, PassiveBehaviour |
| 2 | COMBAT 1v1 — 2 CLASSES | 2 mois | Soulrender + Nightseer complets (15+1 sorts chacun). Mode IA basique. Tests internes. |
| 3 | COMBAT 1v1 — 3 AUTRES CLASSES | 2 mois | Colossar + Necram + Ghostra. Tests internes complets 1v1. IA Difficile. |
| 4 | MAP COMMU + SOCIAL | 2 mois | Map commu Photon Fusion, chat multi-canal, défis casual, clans, friends, profil. |
| 5 | PROGRESSION & ÉCONOMIE | 2 mois | Niveaux par classe, succès, deck builder complet, boutique, battle pass, Nymos. |
| 6 | RANKED + 2v2/3v3 | 2 mois | Ladder, saisons, leaderboards, modes 2v2 et 3v3 (scènes séparées), matchmaking. |
| 7 | POLISH + SOFT LAUNCH | 2 mois | Tutoriel, accessibilité, optimisation mobile, marketing, soft launch limité. |

## **TIMELINE VISUELLE**

| MAI 2026 ─┐ ├─[ PHASE 1 — Fondations ]─────────┐ JUIN 2026 ─┤                                    │ ├─────────────────────── (2 mois) ──┘ JUL 2026 ──┤ ├─[ PHASE 2 — Combat 1v1 (SR+NS) ]─┐ AOÛ 2026 ──┤                                    │ ├─────────────────────── (2 mois) ──┘ SEP 2026 ──┤ ├─[ PHASE 3 — Combat 1v1 (CO+NE+GH)─┐ OCT 2026 ──┤                                    │ ├─────────────────────── (2 mois) ──┘ NOV 2026 ──┤ ├─[ PHASE 4 — Map Commu + Social ]──┐ DÉC 2026 ──┤                                    │ ├─────────────────────── (2 mois) ──┘ JAN 2027 ──┤ ├─[ PHASE 5 — Progression + Eco ]──┐ FÉV 2027 ──┤                                    │ ├─────────────────────── (2 mois) ──┘ MAR 2027 ──┤ ├─[ PHASE 6 — Ranked + 2v2/3v3 ]───┐ AVR 2027 ──┤                                    │ ├─────────────────────── (2 mois) ──┘ MAI 2027 ──┤ ├─[ PHASE 7 — Polish + Soft Launch ]┐ JUN 2027 ──┤                                    │ ├─────────────────────── (2 mois) ──┘ JUL 2027 ──● SOFT LAUNCH (1000 joueurs invités) |
|---|

| PHASE 1 FONDATIONS TECHNIQUES 2 mois — Mai à Juin 2026 |
|---|

## **OBJECTIF**

Refonder l'architecture technique sans toucher au gameplay. À la fin de cette phase, le projet est PRÊT pour recevoir les classes — netcode propre, backend opérationnel, systèmes de combat modulaires en place.

## **LIVRABLES**

- Migration de Photon PUN 2 vers Photon Quantum 3 + Fusion 2 (ancien code combat archivé, nouveau squelette créé)

- Backend Node.js + PostgreSQL + Redis opérationnel sur VPS staging (Hetzner)

- API REST : auth/login/register/refresh + endpoints stub pour profil, deck, mmr

- Authentification JWT fonctionnelle, stockage tokens sécurisé multi-plateforme

- Système SpellResource modulaire (5 implémentations : HG, PR, FD, PT, RM)

- Système PassiveBehaviour modulaire avec interface unifiée

- Système MarkSystem (Traqué, Voilé, Empreinté, Venin, Plaie Ouverte)

- Slot Signature côté UI (composant qui s'illumine à ressource max, gère cooldown)

- Pipeline CI/CD : GitHub Actions + Unity Cloud Build pour Win/Mac/Android/iOS

## **DÉCOUPAGE PAR SEMAINE**

| SEM. | TÂCHE | DELIVERABLE |
|---|---|---|
| S1 | Setup projet, audit code existant, archivage de l'ancien combat | Backup git complet, branches arch-refactor créées |
| S2 | Backend Node.js + Postgres : auth basique, schéma BDD initial | Inscription/login fonctionnels en local |
| S3 | Migration Photon PUN 2 → Quantum + Fusion (squelette) | Premier match Quantum entre 2 instances locales OK |
| S4 | JWT côté Unity : storage sécurisé Keychain/Keystore/DPAPI | Login persistant cross-platform validé |
| S5 | SpellResource : interface + 5 implémentations stub | Tests unitaires sur chaque resource passent |
| S6 | PassiveBehaviour : architecture événementielle | 1 passif factice (placeholder Soulrender) déclenche correctement |
| S7 | MarkSystem : registre central, lifecycle, sérialisation Quantum | Marques persistent à travers ticks Quantum sans desync |
| S8 | Slot Signature UI + cooldown logic, polish phase, QA gate 1 | GO/NO-GO pour Phase 2 |

## **CHECKPOINT QA — FIN DE PHASE 1**

| GATE OBLIGATOIRE Avant de passer à la Phase 2, ces 8 critères doivent être VALIDÉS. Si un seul échoue, on prolonge la phase de 2-4 semaines. Pas de bypass. |
|---|

- ✓ Match Quantum entre 2 clients locaux : 100% des frames synchrones (state hash identique)

- ✓ Login fonctionnel sur Win + Mac + Android + iOS

- ✓ Token JWT correctement persisté entre sessions sur les 4 plateformes

- ✓ Passif factice se déclenche au bon moment côté serveur ET côté client (pas de race condition)

- ✓ MarkSystem : 100 marques posées/retirées en cycle de 30s sans memory leak (Profiler Unity)

- ✓ Build Cloud Build OK pour Win + Mac + Android + iOS (≤ 10 min par plateforme)

- ✓ Backend stable 24h sans crash sous charge fictive de 50 sessions simultanées

- ✓ Documentation à jour : README backend, README Unity, schéma BDD

## **RISQUES PHASE 1**

| RISQUE | PROBABILITÉ | MITIGATION |
|---|---|---|
| Migration Quantum complexe | Élevé | Suivre tutoriels officiels Photon, demander aide Discord Photon, prévoir +2 semaines de buffer |
| Token storage différent par OS | Moyen | Utiliser plugin maintenu (ex: Best HTTP/2) au lieu de coder à la main |
| Backend crash répétés | Faible | PM2 + systemd auto-restart, monitoring Sentry |

| PHASE 2 COMBAT 1v1 — SOULRENDER + NIGHTSEER 2 mois — Juillet à Août 2026 |
|---|

## **OBJECTIF**

Implémenter complètement les deux premières classes : 15+1 sorts chacune, passifs non-linéaires, ressources, signature avec cooldown. Tester en duel local et en réseau Quantum. À la fin, les 2 classes sont JOUABLES en 1v1 avec une IA basique.

## **POURQUOI COMMENCER PAR SR + NS ?**

- Soulrender = la classe la plus simple mécaniquement (pas de leurres, pas de fog of war, pas de marques venin)

- Nightseer = teste le système de marques + le fog of war (le plus complexe info-warfare)

- Ces 2 classes valident 80% de l'architecture combat, le reste sera plus rapide

- Tester un matchup SR vs NS révèle les bugs de pression/setup avant les classes plus complexes

## **LIVRABLES**

- Soulrender complet : ressource Hémoglyphe, passif Appel du Sang, 15 sorts + Âme Lacérée signature

- Nightseer complet : ressource Prescience (invisible côté adversaire !), passif Œil qui n'est pas, 15 sorts + Traquenard signature

- Système de Vapeur Carmin + Sang Coagulé (cases modifiées Soulrender)

- Système de Voilé/Empreinté (marques Nightseer)

- Système de fog of war : cases Voilé invisibles côté ENNEMI mais visibles côté Nightseer (rendu conditionnel)

- Mode IA Facile + Moyen pour ces 2 classes (l'IA Difficile arrive en Phase 3)

- Scène CombatRanked1v1 fonctionnelle en réseau Quantum

## **DÉCOUPAGE PAR SEMAINE**

| SEM. | TÂCHE | DELIVERABLE |
|---|---|---|
| S9 | Soulrender — Ressource HG + Passif L'Appel du Sang | HG génère/consume correctement, passif déclenché aux 4 seuils HP |
| S10 | Soulrender — 5 sorts offensifs | Tranche-Âme, Ouvre-Plaie, Charge Brutale, Détonation Sanglante, Curée |
| S11 | Soulrender — 5 sorts tactiques + 5 survie | Empoignade, Pacte de Sang, Marque Carnage, Rugissement, Rage. + Riposte, Cautérisation, Peau de Fer, Sève Vive, Dernier Souffle |
| S12 | Soulrender — Signature Âme Lacérée + cooldown UI | Cycle complet : montée HG → signature → cooldown → re-montée OK |
| S13 | Nightseer — Ressource PR (invisible côté adversaire) + Passif | PR cachée côté adverse, marques Traqué/Voilé/Empreinté implémentées |
| S14 | Nightseer — 5 offensifs + 5 tactiques | Tir Précis, Volée Épines, Détonation Onirique, Frappe Ombre, Salve Mortelle. + Marque Chasseur, Filet Ronces, Champ Mines, Bourrasque, Souffle Glacial |
| S15 | Nightseer — 5 survie + Signature Traquenard + FOG OF WAR | Voile Ombre, Pas Furtif, Camouflage, Sève Sauvage, Évanescence. Fog of war client-side conditionnel testé OK |
| S16 | IA Facile + Moyen pour SR et NS, tests matchup, QA gate 2 | 20 matchs SR vs NS sans crash, sans desync, MMR mock fonctionnel |

## **CHECKPOINT QA — FIN DE PHASE 2**

- ✓ 30 matchs SR vs NS joués entre 2 clients réels (pas en local) sans desync

- ✓ Replays sauvegardés et rejoués : résultat identique au match original

- ✓ Aucune fuite mémoire après 50 matchs consécutifs (Profiler validation)

- ✓ Mode IA Moyen ne triche pas (validé en testant le code IA en boîte noire)

- ✓ Toutes les marques (Voilé, Traqué, Empreinté, Plaie Ouverte) se comportent correctement

- ✓ Le signature respecte son cooldown 4 tours sans bug de timing

- ✓ Tooltips de tous les sorts complets et exacts (auto-générés depuis ScriptableObjects)

| PHASE 3 COMBAT 1v1 — COLOSSAR + NECRAM + GHOSTRA 2 mois — Septembre à Octobre 2026 |
|---|

## **OBJECTIF**

Compléter les 3 classes restantes. La GHOSTRA est la plus complexe (système de leurres + permutations) — elle prend toute la dernière partie de la phase. À la fin, le combat 1v1 est COMPLET avec toutes les classes.

## **LIVRABLES**

- Colossar complet : Fondation, Densité Inerte, 15 sorts, Effondrement signature avec annonce 1 tour à l'avance

- Système d'OBSTACLES DYNAMIQUES (Piliers + Murs) intégré au pathfinding

- Necram complet : Putréfaction, Floraison non-linéaire, marques venin, Virus Fatal signature

- Système de DENSITÉ TOXIQUE qui scale avec le nombre de marques actives sur la map

- Ghostra complète : Rémanence, Angle Mort, 3 LEURRES INDISCERNABLES, permutations 0 PA, Exécution Spectrale

- Mode IA DIFFICILE pour les 5 classes (le plus dur)

- Tous les matchups 1v1 testés (10 matchups au total) — équilibrage initial à l'œil

## **DÉCOUPAGE PAR SEMAINE**

| SEM. | TÂCHE | DELIVERABLE |
|---|---|---|
| S17 | Colossar — Ressource FD + Passif Densité Inerte + obstacles dynamiques | Piliers/Murs ajoutés à la grille A* en temps réel, FD générée par construction |
| S18 | Colossar — 15 sorts + Signature Effondrement (annonce 1 tour avant) | Effondrement annoncé visuellement à l'adversaire, dégâts au tour suivant |
| S19 | Necram — Ressource PT + Passif Floraison + densité toxique | DoT venin ignore les boucliers, scale avec densité, 3 paliers actifs |
| S20 | Necram — 15 sorts + Signature Virus Fatal | Virus Fatal X3 ticks fonctionnel, anti-Colossar validé |
| S21 | Ghostra — Système de LEURRES (le plus complexe) | 3 leurres maximum, indiscernables côté adversaire, autorité serveur |
| S22 | Ghostra — Permutations 0 PA + Angle Mort | Permutation invisible côté adversaire, bonus dorsal scale avec leurres |
| S23 | Ghostra — 15 sorts + Signature Exécution Spectrale | Exécution rate si non-dorsal, leurres consommés quand même |
| S24 | IA Difficile pour les 5 classes, tests 10 matchups, QA gate 3 | 100 matchs joués (10 matchups × 10 matchs), aucun bug critique |

## **CHECKPOINT QA — FIN DE PHASE 3**

| ATTENTION C'est le moment de vérité du gameplay. Si l'équilibrage est catastrophique, il faut prolonger la phase. Mais on ne touche PAS aux numbers tant que les bugs ne sont pas corrigés. |
|---|

- ✓ Les 5 classes jouables en mode IA et entre 2 clients réels

- ✓ Tous les matchups 1v1 jouables (aucun crash sur un matchup spécifique)

- ✓ Système de leurres : aucun client ne peut différencier la vraie Ghostra des leurres avant interaction

- ✓ Effondrement Colossar : annonce et dégâts synchrones sur les 2 clients

- ✓ Densité toxique Necram : scale correctement avec nombre de marques (testé jusqu'à 7+)

- ✓ Replays validés sur les 5 classes (chaque classe au moins 1 replay rejoué identique)

- ✓ Performance : 60 fps mobile (Pixel 6 / iPhone 12) en match standard, 30 fps minimum

| PHASE 4 MAP COMMU + SOCIAL 2 mois — Novembre à Décembre 2026 |
|---|

## **OBJECTIF**

Construire le hub social et toutes les fonctionnalités sociales. C'est l'équivalent du "Hub de Frigost" Dofus en plus moderne. À la fin, le jeu N'EST PLUS QU'UN COMBAT — c'est une expérience sociale.

## **LIVRABLES**

- Scène 02_HubCommu.unity avec Photon Fusion (Shared Mode)

- Map de 60x40 cases dark fantasy : décors, PNJ, zones spéciales

- Système de pathfinding casual (clic-déplacement, cosmétique)

- Sprite custom avec cosmétiques visibles

- Système de défi casual (Joueur A → Joueur B → match Quantum sans MMR)

- Chat multi-canal (Global, Clan, Privé, Combat, Système) avec WebSocket backend

- Système d'amis : invite, accept, status online, whisper

- Système de clans : création, rejoindre, kick, level XP, banner

- Profil joueur complet (5 onglets : Vue, Stats, Classes, Succès, Cosmétiques)

- Système de signalement et modération basique

## **DÉCOUPAGE PAR SEMAINE**

| SEM. | TÂCHE | DELIVERABLE |
|---|---|---|
| S25 | Scène HubCommu + Photon Fusion intégration | 50 joueurs sur même instance, déplacement libre fluide |
| S26 | Décors map + PNJ + interactions de base | PNJ Marchand, Ranked, Clan, Tableau ÉvénEments cliquables |
| S27 | Chat multi-canal + WebSocket backend | 5 canaux fonctionnels, filtre anti-insulte serveur-side |
| S28 | Système de défi casual + transition Fusion → Quantum | Joueur A défie B, match créé, après match retour à la map intacte |
| S29 | Système d'amis : invite, accept, status, whisper | Liste d'amis persistée backend, notifications online/offline |
| S30 | Système de clans : création, gestion, rôles, XP, banner | Créer un clan, inviter, kick, promote — tout fonctionnel |
| S31 | Profil joueur 5 onglets, vue publique vs privée | Profil affiché en cliquant sur un autre joueur |
| S32 | Modération + signalement + QA gate 4 | Workflow signalement → mute auto temporaire si pattern détecté |

## **CHECKPOINT QA — FIN DE PHASE 4**

- ✓ 50 joueurs simulés (bots) tournent en même temps sur la map sans lag

- ✓ Transition Fusion → Quantum (défi casual) zéro perte de données

- ✓ Chat : 1000 messages/min sur le canal global sans crash backend

- ✓ Filtre anti-insulte test : 50 messages tagués manuellement, 95% détection

- ✓ Création clan + 20 invites + 20 acceptations sans bug

- ✓ Profil affiché correctement (5 onglets) pour 10 profils différents

- ✓ Système d'amis : invite cross-platform (PC ami iOS) fonctionne

| PHASE 5 PROGRESSION & ÉCONOMIE 2 mois — Janvier à Février 2027 |
|---|

## **OBJECTIF**

Implémenter tout ce qui retient le joueur sur la durée : leveling par classe, succès, deck builder complet, boutique, battle pass. À la fin, le joueur a des objectifs clairs à long terme.

## **LIVRABLES**

- Système de niveaux par classe (1-50, XP, déblocages progressifs des sorts)

- Système de succès : 200 succès au total avec progression et points

- Deck builder UI complète : sélection 6/15 sorts, save/edit/delete, max 5 decks/classe

- Boutique avec 2 monnaies (Nymos + Shards), rotation hebdomadaire

- Système de battle pass 100 tiers, voie gratuite + premium

- Système de quêtes (quotidiennes, hebdomadaires, saisonnières)

- Système de cosmétiques : skins, bannières, titres, effets, emotes, stickers

- Inventaire joueur complet avec équipement

- Intégration IAP : Stripe (PC), Apple IAP (iOS), Google Play Billing (Android)

## **DÉCOUPAGE PAR SEMAINE**

| SEM. | TÂCHE | DELIVERABLE |
|---|---|---|
| S33 | Niveaux par classe (XP curve, paliers cosmétiques, UI tracking) | Match donne XP, level-up débloque récompenses cosmétiques + titres (pas de sorts), UI d'XP visible |
| S34 | Système de succès (200 succès design + tracking) | Tous les succès trackés en temps réel, points calculés |
| S35 | Deck builder UI complète + save/edit/delete API | Créer 5 decks, en supprimer un, en modifier — tout persiste |
| S36 | Boutique UI + catalogue initial + rotation | 20 items en boutique, achat avec Nymos OK |
| S37 | Premium currency (Shards) + IAP Stripe (PC) | Achat Shards via Stripe en sandbox OK |
| S38 | IAP Apple + Google avec validation server-side | Achat sandbox validé sur les 2 stores |
| S39 | Battle Pass 100 tiers + quêtes quotidiennes/hebdomadaires | Cycle complet : quête → XP BP → tier débloqué → récompense |
| S40 | Cosmétiques : équipement, skins de classe, polish UI, QA gate 5 | Skins changent visuellement le sprite en combat |

## **CHECKPOINT QA — FIN DE PHASE 5**

- ✓ Cycle complet : nouveau compte → 5 matchs → niveau 2 → débloque récompense cosmétique (cadre/titre) → save deck (avec n'importe lesquels des 15 sorts) → utilise en match

- ✓ Achat Nymos → boutique → équipement skin → visible en combat

- ✓ Achat Shards via Stripe (PC) + Apple TestFlight + Google Play Internal Testing : 3 plateformes OK

- ✓ Battle Pass : 10 tiers franchis dans la session de test → toutes récompenses correctes

- ✓ Aucune fraude possible : tentative de spoof IAP côté client = rejet serveur

- ✓ Inventaire persiste cross-platform (achat PC visible sur mobile)

| PHASE 6 RANKED + 2v2/3v3 2 mois — Mars à Avril 2027 |
|---|

## **OBJECTIF**

Activer le mode ranked compétitif avec saisons et leaderboards, et ajouter les modes 2v2 et 3v3 (qui sont moins prioritaires que le 1v1 mais nécessaires pour la rétention long terme et les clans).

## **LIVRABLES**

- Matchmaking 1v1 par MMR avec fenêtre adaptative

- Système ELO modifié (K-factor variable, modificateurs Nymora)

- 8 ranks (Bronze → Légende) avec déblocage visuel à chaque rank

- Saisons de 90 jours avec soft reset MMR + récompenses fin de saison

- Leaderboards multiples : global, par classe, par mode, par pays, par clan

- Scènes 2v2 et 3v3 distinctes (CombatRanked2v2, CombatRanked3v3) avec grilles plus grandes

- Matchmaking équipe (pré-formée OU solo queue avec teammates aléatoires)

- Système de communication d'équipe (pings, ordres rapides) en 2v2/3v3

- MMR séparé par mode (un joueur Diamant 1v1 peut être Or 3v3)

## **DÉCOUPAGE PAR SEMAINE**

| SEM. | TÂCHE | DELIVERABLE |
|---|---|---|
| S41 | Matchmaking queue 1v1 + ELO modifié + Redis sorted sets | Queue fonctionnelle, MMR calculé après chaque match |
| S42 | 8 ranks visuels + saisons + soft reset script | UI de rank visible sur profil, récompenses fin de saison automatisées |
| S43 | Leaderboards globaux + par classe + par mode | Top 100 global affiché avec photos, mise à jour en temps réel |
| S44 | Scène CombatRanked2v2 + grille 14x12 | Match 2v2 jouable entre 4 clients réels |
| S45 | Scène CombatRanked3v3 + grille 16x14 | Match 3v3 jouable entre 6 clients réels |
| S46 | Matchmaking équipe pré-formée + solo queue | Inviter ami → queue 2v2 ensemble + queue solo où on est avec random |
| S47 | Système de pings/ordres équipe en team modes | Pings sur la grille visibles par alliés uniquement |
| S48 | Anti-smurf + anti-boost detection + QA gate 6 | Détection auto + flag pour modération manuelle |

## **CHECKPOINT QA — FIN DE PHASE 6**

- ✓ 50 matchs ranked 1v1 joués entre testeurs : MMR évolue cohéremment, pas d'exploit

- ✓ 20 matchs 2v2 et 20 matchs 3v3 sans crash, sans desync

- ✓ Leaderboards : top 100 visible en < 1 seconde (Redis OK)

- ✓ Saison test (compressée à 7 jours) : reset effectué correctement, récompenses distribuées

- ✓ Anti-smurf : compte fictif win streak 20-0 → boost MMR forcé +200 appliqué

- ✓ Aucun joueur ne peut influencer le matchmaking via des tricks (ex: cancel queue puis re-queue spam)

| PHASE 7 POLISH + SOFT LAUNCH 2 mois — Mai à Juin 2027 |
|---|

## **OBJECTIF**

Préparer le jeu pour de vrais joueurs externes. Tutoriel, accessibilité, optimisation mobile, monitoring, marketing minimum, soft launch limité (1000 invitations Discord/réseaux). À la fin, le jeu est PUBLIQUEMENT JOUABLE en early access.

## **LIVRABLES**

- Tutoriel interactif (premier match contre IA scriptée qui apprend les bases)

- Accessibilité complète : daltonisme (3 modes), reduce motion, font scaling, high contrast

- Localisation : FR + EN au minimum (autres langues post-launch)

- Optimisation mobile : 30 fps min sur Pixel 5 / iPhone 11 (devices minimum supportés)

- Monitoring production : Grafana dashboards, alertes Discord pour incidents critiques

- Système de feedback in-game (bouton "Signaler un bug" avec capture screenshot auto)

- Page Steam (page-coming-soon) + page web simple

- Discord communautaire avec bots de modération

- Soft launch : 1000 invitations Discord, monitoring intensif des 30 premiers jours

## **DÉCOUPAGE PAR SEMAINE**

| SEM. | TÂCHE | DELIVERABLE |
|---|---|---|
| S49 | Tutoriel interactif + premier match scripté | Nouveau joueur arrive → tutoriel 5 min → 1er match IA réussi |
| S50 | Accessibilité (daltonisme, motion, scaling, contrast) | Settings testés sur 5 utilisateurs avec besoins différents |
| S51 | Localisation FR + EN (Lokalise ou tableurs) | Toutes les UI strings traduites, switcher de langue OK |
| S52 | Optimisation mobile (Profiler, GPU debugger) | Pixel 5 = 30 fps stable, iPhone 11 = 30 fps stable |
| S53 | Monitoring : Grafana, alertes Discord, dashboards | Crash → alerte Discord en < 1 minute |
| S54 | Page Steam + page web + Discord setup | Communauté Discord prête, modérateurs bénévoles recrutés |
| S55 | Beta interne (50 testeurs Discord), bugfix sprint | Bugs P0 et P1 fixés, P2 en backlog |
| S56 | Soft launch — 1000 invitations + monitoring 7 jours | Métriques OK : crash rate < 0.5%, retention J7 > 30% |

## **CRITÈRES SOFT LAUNCH**

| INFO Le soft launch n'est PAS un release public. C'est une beta limitée. Il sert à vérifier que le jeu tient la charge réelle (vs simulée) et à corriger les bugs invisibles en QA interne. |
|---|

- 1000 invitations distribuées via Discord + Twitter + Reddit r/IndieGames

- Monitoring 24/7 pendant les 7 premiers jours

- Hotfix sous 24h pour tout bug P0

- Patchnote quotidien sur Discord les premiers jours

- Métrique go/no-go pour passer à l'open beta : 60% des joueurs ont joué 5+ matchs (engagement) ET crash rate < 1%

# **MÉTHODOLOGIE ANTI-BUG & ANTI-DESYNC**

*Solo dev sur 14 mois sans accumuler de bugs critiques = challenge énorme. Cette section détaille les pratiques OBLIGATOIRES pour rester clean.*

## **LES 7 RÈGLES SACRÉES**

- 1. CHAQUE FONCTIONNALITÉ a des tests unitaires. Pas de code merge sans tests qui passent.

- 2. CHAQUE PHASE a un QA Gate. Si gate non validé, on PROLONGE la phase. Pas de bypass.

- 3. CHAQUE WEEKEND : pause de 2 jours OBLIGATOIRE. Solo dev qui burn = projet abandonné.

- 4. CHAQUE LUNDI : 1h de revue du code de la semaine d'avant (relecture froide).

- 5. AVANT TOUT MERGE en main : checklist de 10 points (voir ci-dessous).

- 6. UN BUG EN PROD = 1 test unitaire ajouté. JAMAIS de fix sans test de non-régression.

- 7. CRASH UTILISATEUR = log Sentry + investigation sous 48h. Pas de bug ignoré.

## **CHECKLIST PRE-MERGE**

Avant tout merge en branche main :

- ☐ Tests unitaires verts (CI passe)

- ☐ Build Cloud Build OK pour Win, Mac, Android, iOS

- ☐ Pas de warning compilateur ajouté

- ☐ Le code ne casse aucune feature existante (smoke test manuel 5 min)

- ☐ Si combat affecté : 1 match Quantum entre 2 clients locaux test post-merge

- ☐ Si UI : test sur résolution 1080p + 4K + mobile portrait

- ☐ Pas de TODO ou FIXME laissés en plan

- ☐ Documentation à jour (au moins commentaires sur fonctions publiques)

- ☐ ScriptableObjects modifiés ? Bump CombatRulesVersion si oui

- ☐ Migration BDD ? Script de migration Prisma écrit ET TESTÉ sur copie de DB

## **ANTI-DESYNC — DÉTECTION ET RÉACTION**

| INFO Quantum a un mécanisme de detection de desync via state hash. Si les 2 clients ont des hash différents à un tick T, Quantum déclenche un VerifiedFrame qui force un re-sync. Si la divergence persiste : déconnexion automatique. |
|---|

- TICK 1 OK : state hash A == B → on continue

- TICK 2 OK : state hash A == B

- TICK 3 PROBLÈME : state hash A != B

- → Quantum demande VerifiedFrame du serveur

- → Si serveur dit "A est correct" : B rollback son state, replay les inputs

- → Si re-sync impossible (divergence trop grande) : DÉCONNEXION + match annulé + log Sentry

- → Le développeur reçoit l'alerte, peut analyser le log et identifier la cause (souvent : code non-déterministe ajouté par erreur)

## **LOGGING STRATÉGIQUE**

Trois niveaux de logs en production :

| NIVEAU | QUOI | RÉTENTION |
|---|---|---|
| INFO | Connexions, matchs lancés, achats | Conservé 30 jours |
| WARNING | Validations échouées, rate limits hit, retries | Conservé 90 jours |
| ERROR | Crashes, desync, exceptions | Conservé 1 an, alerté Discord en temps réel |

## **BACKUP STRATEGY**

- Database : backup quotidien automatique via pg_dump → S3 Glacier (rétention 30 jours)

- Database : backup hebdomadaire complet → S3 Glacier (rétention 1 an)

- Code : Git remote sur GitHub + miroir GitLab (redondance)

- Replays Quantum : stockés sur Cloudflare R2, immuables

- Disaster recovery test : 1x par trimestre, restauration complète à partir des backups

# **POST-LAUNCH — ROADMAP CONTINUE**

## **APRÈS LE SOFT LAUNCH**

Une fois en early access, le développement passe en mode LIVE SERVICE. Cycles de release réguliers, écoute communauté, équilibrage continu.

## **ROADMAP 3 MOIS POST-LAUNCH**

| PÉRIODE | FOCUS | ACTIONS |
|---|---|---|
| MOIS 1 | Stabilisation | Hotfix prioritaire, monitoring intense, équilibrage initial sur données réelles |
| MOIS 2 | Première saison ranked | Lancement officiel saison 1, premier battle pass premium |
| MOIS 3 | Open beta publique | Steam Early Access ouverture, marketing intensif, 10k joueurs cible |

## **ROADMAP 6-12 MOIS POST-LAUNCH**

- Mode TOURNOI (formats best-of-5, brackets, prizes)

- Mode RANKED ÉQUIPES (clans vs clans, format compétitif)

- Replays publics partageables sur le web (URL → lecteur web)

- Système de spectateurs (observer mode pour stream/esport)

- Application companion (mobile-only) : voir profil, chat, mais pas jouer

- API publique pour les data nerds (stats anonymisées)

## **PRINCIPES LIVE SERVICE**

- 1 patch contenu majeur tous les 3 mois (synchronisé avec saison ranked)

- Hotfix sous 24h pour P0

- Patchnote détaillé à chaque release, communauté informée à l'avance des changements d'équilibrage

- Discord = canal principal de communication communauté

- Ne PAS céder aux demandes hâtives d'équilibrage : attendre 2-3 semaines de données avant de toucher aux numbers

- Toute decision majeure (refonte d'une classe, changement majeur économie) = devblog public expliquant le pourquoi

# **MOT DE LA FIN**

*"Solo dev pendant 14 mois sur un live service multi-plateforme, c'est une marche. Pas un sprint. Pas une course. Une marche, lente, méthodique, où chaque pas compte plus que la vitesse."*

Cette roadmap n'est PAS une promesse de dates. C'est un cadre. Si une phase prend 3 mois au lieu de 2, c'est OK. Si une feature s'avère trop complexe, on coupe son scope pour rester en phase. La règle d'or : NE JAMAIS sacrifier la qualité pour respecter le calendrier.

| VRAI INDICATEUR DE SUCCÈS Les 14 mois sont une CIBLE. Le vrai indicateur de succès n'est pas la date, c'est le STATE du jeu au soft launch : stable, jouable, fun, sans bugs critiques. Mieux vaut un soft launch en mois 16 qu'un crash répété en mois 14. |
|---|

## **CHECKLIST DE LA SEMAINE 1**

Pour démarrer concrètement lundi prochain :

- ☐ Backup git complet du projet existant

- ☐ Création d'une branche "v7.1-arch-refactor"

- ☐ Installation Node.js + PostgreSQL + Redis en local

- ☐ Compte Photon Cloud créé (plan Indie ou trial)

- ☐ VPS Hetzner CX22 commandé (5€/mois)

- ☐ Domain acheté (nymora.io ou similaire)

- ☐ Compte Sentry créé (free tier)

- ☐ Compte Discord serveur de test créé

- ☐ Lecture complète des 3 docs (Architecture + Features + Roadmap) en imprimé pour annoter

- ☐ Première session de 4h : audit du code existant + plan de migration détaillé

## **OUTILS RECOMMANDÉS**

- Notion ou Obsidian — wiki personnel pour notes design

- Linear ou GitHub Projects — tracking des tâches par phase

- Figma — maquettes UI

- TablePlus — client SQL pour PostgreSQL

- Postman ou Bruno — test API

- Grafana Cloud (free tier) — monitoring early stage

- Discord Webhook — alertes incidents

- Mob Programming avec ChatGPT/Claude — pas pour coder pour toi, mais pour rubber-duck les designs

*Bonne route, chef. Le jeu est ambitieux mais cohérent. Tu as la bible combat (V7.1), l'architecture, les features, la roadmap. Le reste, c'est du code et de la persévérance.*