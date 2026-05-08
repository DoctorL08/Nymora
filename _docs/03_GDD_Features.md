# Nymora — GDD & Features

> Source : `Nymora_DOC2_GDD_Features.docx`

---

# **NYMORA**

## **GDD — FEATURES**

*Map commu · Profil · Deck builder · Boutique · Chat · Ranked · Clans*

*Document 2/3 — V7.1*

# **SOMMAIRE DES FEATURES**

*Ce document détaille chaque feature mentionnée dans la vision : objectif, écran, flux UX, données backend, edge cases. Chaque section peut être lue indépendamment et servir de spec pour l'implémentation.*

| # | FEATURE | DESCRIPTION COURTE |
|---|---|---|
| 1 | Compte & Authentification | Création, login, persistance multi-device |
| 2 | Map Communautaire | Hub social, défis casual, événements |
| 3 | Profil Joueur | Stats, MMR, succès, niveaux par classe |
| 4 | Deck Builder | Sélection 6/15 sorts, save/edit/delete |
| 5 | Menu Arène | IA (3 niveaux) / Ranked 1v1/2v2/3v3 |
| 6 | Système de Classement | MMR, ladder, saisons |
| 7 | Chat Multi-Canal | Global, clan, privé, combat, système |
| 8 | Boutique | Nymos (in-game) + Premium currency |
| 9 | Battle Pass | Saison, tiers, récompenses |
| 10 | Système de Clans | Création, gestion, level, XP |
| 11 | Système de Succès | Achievements, points, récompenses |
| 12 | Cosmétiques | Skins, bannières, titres, effets |
| 13 | Niveaux par Classe | Leveling 1-50 par classe / compte |
| 14 | Paramètres | Audio, vidéo, contrôles, accessibilité |

# **1. COMPTE & AUTHENTIFICATION**

## **OBJECTIF**

Permettre à un joueur de créer un compte, se connecter depuis n'importe quelle plateforme (PC/Mac/Mobile) et retrouver tous ses progrès. C'est le SOCLE de tout le système — sans compte solide, pas de live service.

## **ÉCRANS**

- WelcomeScreen — logo, "Se connecter" / "Créer un compte"

- RegisterScreen — email, username (unique), password, confirm, captcha, T&C checkbox

- LoginScreen — email/username + password, "Mot de passe oublié", "Connexion automatique"

- ForgotPasswordScreen — saisie email, lien envoyé, screen "Vérifie ta boîte mail"

- Account2FAScreen (optionnel mais recommandé) — setup TOTP via app authenticator

## **FLUX TECHNIQUE**

| [Client] POST /api/auth/register { email, username, password (plain over HTTPS), captcha } ↓ [Backend] - Vérifie unicité email + username - bcrypt(password, cost=12) - INSERT accounts - INSERT account_progress (level=1, xp=0) - INSERT class_levels × 5 (toutes classes au niveau 1) - INSERT decks (1 deck starter par classe) - INSERT wallet (nymos=200 starter) - Envoie email de vérification ↓ [Client] redirect → MainMenu (compte créé mais email à vérifier)  [Client] POST /api/auth/login { identifier (email or username), password } ↓ [Backend] - Récupère account - bcrypt.compare() - Génère JWT (access 1h) + refresh token (30d) - INSERT login_history ↓ [Client] stocke tokens dans secure storage (Keychain iOS / Keystore Android / DPAPI Windows) |
|---|

## **DONNÉES STOCKÉES**

| CRITIQUE Aucun mot de passe n'est jamais transmis ni stocké en clair. Le client envoie le password sur HTTPS, le backend le hash immédiatement. Les tokens JWT sont stockés dans le storage SÉCURISÉ de la plateforme (Keychain/Keystore/DPAPI), pas en PlayerPrefs. |
|---|

## **EDGE CASES**

- Compte créé mais email jamais vérifié : peut jouer mais pas accès à boutique premium pendant 30 jours, sinon compte supprimé

- Login depuis 3+ devices simultanés : autorisé en cross-platform (PC + mobile en parallèle), mais 2 PC simultanés = ancienne session déconnectée

- Refresh token expiré : redirige vers login screen avec message "Session expirée, reconnecte-toi"

- Compte banni : login retourne 403 + message contenant raison + durée + email de support

- Compte supprimé (RGPD) : email/username libérés après 90 jours pour réutilisation

# **2. MAP COMMUNAUTAIRE**

## **OBJECTIF**

Hub social où les joueurs se baladent en isométrique 2.5D, chattent, lancent des défis casual entre eux, voient les annonces, accèdent aux PNJ marchands. C'est le CŒUR DU SENTIMENT MMO — sans cette map, le jeu est juste un menu + combats.

## **DESCRIPTION VISUELLE**

- Map fixe d'environ 60x40 cases (8 écrans), thème dark fantasy : ruines de cathédrale, brumes, torches, statues brisées

- Sprites des joueurs en 128x128 (cohérence avec les classes du combat)

- Skin du joueur visible — montre les cosmétiques équipés

- PNJ stationnaires : Marchand de cosmétiques, Maître de Clan, Guide ranked, Tableau d'événements

- Zones spéciales : Arène d'entraînement (visuel), Salon des Champions (top ladder), Forum des Clans

- Capacité : 50 joueurs par instance. Au-delà, instance suivante créée automatiquement (instance 1, 2, 3...)

## **INTERACTIONS POSSIBLES**

| ACTION | RÉSULTAT |
|---|---|
| Cliquer sur sol | Déplacement vers la case (pathfinding) — cosmétique, pas de PA/PM |
| Cliquer sur joueur | Ouvre menu radial : Profil, Défi, Whisper, Inviter clan, Inviter ami, Bloquer, Signaler |
| Cliquer sur PNJ Marchand | Ouvre la boutique |
| Cliquer sur PNJ Ranked | Ouvre le menu Arène (1v1/2v2/3v3) |
| Cliquer sur PNJ Clan | Ouvre la gestion du clan ou la création |
| Cliquer sur Tableau | Affiche les événements en cours et à venir |
| Touche E (PC) / bouton (mobile) | Emote — anime le sprite (saluer, danser, applaudir, etc.) |

## **DÉFI CASUAL**

Le défi entre joueurs sur la map commu permet de jouer des matchs SANS impact sur le MMR mais avec gain d'XP réduit (50% de l'XP ranked).

| [Joueur A clic-droit sur Joueur B → "Défier"] ↓ [Sélection du mode : 1v1 (par défaut)] ↓ [Joueur B reçoit popup : "Joueur A te défie en 1v1 — Accepter / Refuser"] ↓ (B accepte) [Sélection du deck (rapide UI)] ↓ [Backend crée match casual room Quantum] ↓ [Transition vers scène CombatRanked1v1 (instance casual)] ↓ [Combat se joue — UI affiche "MATCH AMICAL — Pas de MMR"] ↓ [Fin → retour automatique sur la map commu, à la même position] |
|---|

## **NETCODE — IMPORTANT**

| INFO La map commu utilise PHOTON FUSION (Shared Mode), PAS Quantum. Les positions sont en floats classiques, l'interpolation est suffisante. Aucun calcul critique. Si un joueur lag, on extrapole sa position pendant 2 secondes max puis on freeze son sprite. Pas de game-impact en cas de desync. |
|---|

## **ÉVÉNEMENTS DYNAMIQUES (POST-LAUNCH)**

- Boss communautaire (rare, 1x/semaine) : tous les joueurs d'une instance combattent un boss IA, récompenses partagées

- Tournoi casual (sponsorisé par devs) : annonce sur le tableau, inscription en cliquant

- Saison Halloween/Noël : la map change visuellement (citrouilles, neige), nouveaux cosmétiques événementiels

- Mini-jeux : course de vitesse via parkour sur la map, puzzle sortant à chercher pour gagner Nymos

# **3. PROFIL JOUEUR**

## **OBJECTIF**

Centraliser tout ce qui définit le joueur : identité, progression, stats, histoire. C'est l'écran qu'un joueur fier ou ambitieux montre à ses potes ou affiche sur Twitch.

## **STRUCTURE DU PROFIL**

Le profil est divisé en 5 onglets navigables :

| ONGLET | CONTENU |
|---|---|
| VUE D'ENSEMBLE | Card avec avatar, nom, niveau de compte, MMR principal, clan, dernière activité |
| STATS | Détails : K/D, winrate, durée moyenne match, sorts les plus utilisés, classe préférée |
| CLASSES | Niveau (1-50) de chaque classe, XP, déblocages, builds favorisé |
| SUCCÈS | Liste des achievements débloqués/à débloquer, progression, points totaux |
| COSMÉTIQUES | Inventaire des skins, bannières, titres, effets — équipement actuel |

## **VUE D'ENSEMBLE — DÉTAIL**

| ╔══════════════════════════════════════════╗ ║  [AVATAR 256x256]  Lorenzo                ║ ║                    Niv 23 · Bannière équipée   ║ ║                    Clan : LES CARMINS [LCRM]   ║ ║                                                  ║ ║  TITRE ÉQUIPÉ : "Le Saigneur"                   ║ ║                                                  ║ ║  ────────────────────────────────────────────   ║ ║                                                  ║ ║  MMR 1v1     : 1847 (Diamant III)               ║ ║  MMR 2v2     : 1620 (Platine I)                 ║ ║  MMR 3v3     : 1455 (Or II)                     ║ ║                                                  ║ ║  Matchs joués : 342  ·  W/L : 198/144 (58%)     ║ ║  Classe préférée : Soulrender (47% des matchs)  ║ ║                                                  ║ ║  Dernière activité : il y a 2h                  ║ ║  Inscrit depuis : 4 mois                        ║ ╚══════════════════════════════════════════╝ |
|---|

## **STATS — TRACKING DÉTAILLÉ**

| STAT | DESCRIPTION |
|---|---|
| Matchs joués | Total / par classe / par mode |
| Winrate global | % victoires sur tous matchs ranked |
| Winrate par classe | Important pour identifier ses forces |
| K/D ratio | Kills/morts par match — moins critique en 1v1 (toujours 1/1) mais essentiel en 2v2/3v3 |
| Damage moyen / match | Dégâts infligés par match |
| Damage subi moyen | Fragilité du joueur |
| Durée moyenne match | Reflète style de jeu (rush vs sustain) |
| Sort le plus utilisé | Top 3 sorts globaux + par classe |
| Streak actuel | Win streak / loss streak en cours |
| Peak MMR (par mode) | Record personnel |
| Classement saison actuelle | Position globale + par classe |

## **PROFIL PUBLIC vs PRIVÉ**

Par défaut le profil est PUBLIC (visible par tous via la map commu). Le joueur peut paramétrer dans Settings > Privacy :

- Profil public complet (tout visible)

- Profil semi-public (stats cachées, juste niveau + clan visibles)

- Profil privé (seul le clan + amis voient les stats)

# **4. DECK BUILDER**

## **OBJECTIF**

Permettre au joueur de construire ses 6 sorts parmi les 15 disponibles par classe, sauvegarder plusieurs decks par classe, les renommer, les modifier, les supprimer.

## **RÈGLES SYSTÈME**

- MAX 5 DECKS sauvegardés par classe (donc max 25 decks au total pour les 5 classes)

- Le sort SIGNATURE n'est pas dans le builder — il est automatiquement attaché à la ressource max de la classe

- Aucune restriction interne (pas de "max 2 sorts offensifs") — composition totalement libre

- Chaque deck a un nom (3-20 caractères, filtre anti-insulte) et une icône (5 icônes au choix)

- Un deck peut être marqué comme "FAVORI" — il sera pré-sélectionné lors d'un défi rapide

- Au niveau 1 d'une classe : seulement 5 sorts débloqués (1 offensif, 1 tactique, 3 survie pour démarrer en sécurité). Le reste se débloque par leveling (niveau 5, 10, 15... 50)

## **INTERFACE — MAQUETTE**

| ╔══════════════════════════════════════════════════════╗ ║ DECK BUILDER                          [✕ Fermer]      ║ ║                                                        ║ ║ Classe : [SOULRENDER ▼]   Deck : [Mes Decks ▼] [+ Nouveau] ║ ║                                                        ║ ║ ┌────────── DECK ACTUEL : "Build Agro" ──────────┐    ║ ║ │ [SLOT 1] [SLOT 2] [SLOT 3] [SLOT 4] [SLOT 5] [SLOT 6] ║ ║ │ Tranche  Ouvre   Charge  Empoign  Pacte   Curée   │  ║ ║ │ -Âme     -Plaie  Brutale (3 PA)  Sang   (2 PA)   │  ║ ║ │ (3 PA)   (2 PA)  (4 PA)          (1 PA)         │  ║ ║ │                                                  │  ║ ║ │ + SIGNATURE : Âme Lacérée (auto, 5 HG cap)      │  ║ ║ └────────────────────────────────────────────────┘    ║ ║                                                        ║ ║ ─── SORTS DISPONIBLES (15 sorts, filtre catégorie) ── ║ ║ [TOUS] [OFFENSIFS] [TACTIQUES] [SURVIE]                ║ ║                                                        ║ ║  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐         ║ ║  │ Sort │ │ Sort │ │ Sort │ │ Sort │ │ Sort │         ║ ║  │ icon │ │ icon │ │ icon │ │ icon │ │ icon │         ║ ║  │ +info│ │ +info│ │ +info│ │ +info│ │ +info│         ║ ║  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘         ║ ║  (... etc, 15 sorts en tout)                          ║ ║                                                        ║ ║ [💾 Sauvegarder]  [✏️ Renommer]  [🗑️ Supprimer]    ║ ╚══════════════════════════════════════════════════════╝ |
|---|

## **INTERACTIONS**

| ACTION | RÉSULTAT |
|---|---|
| Drag & drop sort | Place ou échange dans un slot du deck |
| Hover sur sort | Tooltip détaillé (effet complet, dégâts, lore court) |
| Clic sur sort équipé | Affiche stats détaillées + bouton "Remplacer" |
| Bouton Nouveau deck | Vide les 6 slots, demande un nom |
| Bouton Sauvegarder | POST /api/deck/save → backend persiste |
| Bouton Renommer | Inline edit du nom (avec validation filtre) |
| Bouton Supprimer | Confirmation modale → DELETE /api/deck/{id} |
| Bouton Marquer favori | Étoile dorée sur le deck |

## **FLUX TECHNIQUE**

| [Client] GET /api/deck/list?class=soulrender → 200 { decks: [{ id, name, slots, isFavorite, lastUsed }] }  [Client] POST /api/deck/save { id?, classId, name, slots: [spell_id × 6], isFavorite } → backend valide : - Tous les sorts existent et appartiennent à la classe - Tous les sorts sont DÉBLOQUÉS pour ce joueur (level check) - Le joueur n'a pas plus de 5 decks sur cette classe - Pas de doublon dans les slots → 200 { id, name, slots, ... } ou 400 { error }  [Client] DELETE /api/deck/{id} → 204 (no content) → si c'était le deck favori, le plus récent devient favori auto |
|---|

## **EDGE CASES**

- Joueur qui possédait un deck avec sort X, mais X est nerfé/refondu : le deck est gardé, mais lors du prochain match, alerte "Ce deck contient des sorts modifiés — vérifie-le"

- Joueur qui supprime sa dernière deck d'une classe : un deck "starter" est recréé automatiquement

- Joueur qui essaye d'utiliser un deck en match alors qu'il a un sort verrouillé (level non atteint) : popup "Sort verrouillé, équipe-en un autre" avant le match

- Synchronisation cross-device : un deck créé sur mobile est instantanément dispo sur PC après login

# **5. MENU ARÈNE — IA & RANKED**

## **OBJECTIF**

Point d'entrée vers les modes de jeu compétitifs et solo. C'est l'écran le PLUS UTILISÉ du jeu — il doit être ultra-clair et rapide d'accès.

## **STRUCTURE — 2 GRANDES CATÉGORIES**

|  MODE IA (offline, prioritaire) ▸ 3 niveaux de difficulté : Facile, Moyen, Difficile ▸ Récompense XP réduite (30% de l'XP ranked) ▸ Pas de gain Nymos (sauf missions quotidiennes) ▸ Aucun impact MMR — entraînement pur ▸ Idéal pour tester un nouveau deck, apprendre une classe, débloquer succès solo ▸ Disponible offline (pas besoin de réseau pour démarrer) |  MODE RANKED (online) ▸ 1v1 : mode principal — MMR, ladder, saison ▸ 2v2 : équipe pré-formée OU matchmaking solo ▸ 3v3 : équipe pré-formée OU matchmaking solo ▸ MMR séparé par mode (un joueur peut être Diamant 1v1 et Or 3v3) ▸ Récompenses Nymos + XP + battle pass progress ▸ Saison de 3 mois, reset MMR partiel + récompenses fin de saison |
|---|---|

## **MODE IA — DÉTAIL DES 3 DIFFICULTÉS**

| DIFFICULTÉ | XP/MATCH | COMPORTEMENT | POUR QUI |
|---|---|---|---|
| FACILE | 60 | Réagit lentement (1 sort/tour max). Ne joue jamais son signature. | Découverte d'une classe, premier match |
| MOYEN | 120 | Joue 2-3 sorts/tour. Utilise le signature à ressource max. Pas de mindgames. | Tester un nouveau deck |
| DIFFICILE | 200 | Joue optimal. Anticipe ("si je pose X, le joueur fera Y"). Utilise mindgames de base. | S'entraîner à un matchup |

| OFFLINE-FIRST Le mode IA est OFFLINE. Quand le joueur lance un match IA, le client charge la scène CombatIA, exécute les calculs localement (déterministes pour debug), et POST le résultat au backend EN ASYNC. Si offline, les XP/Nymos sont mis en cache et synchronisés au prochain login. |
|---|

## **MODE RANKED — MATCHMAKING**

Le matchmaking pair les joueurs par MMR avec une fenêtre qui s'élargit avec le temps :

| TEMPS EN QUEUE | FENÊTRE MMR | QUALITÉ |
|---|---|---|
| 0-30 sec | ±50 MMR | Match parfait MMR-wise |
| 30-60 sec | ±100 MMR | Match acceptable |
| 60-120 sec | ±200 MMR | Match large mais valide |
| 120+ sec | ±400 MMR | Match désespéré (ladder vide) |

## **INTERFACE — MAQUETTE ARÈNE**

| ╔════════════════════════════════════════════════╗ ║  ARÈNE                            [Map commu]   ║ ║                                                  ║ ║  ┌──────────────────────────────────────────┐  ║ ║  │ ⚔️  MODE IA                              │  ║ ║  │ ──────────────────────────────────────   │  ║ ║  │ [FACILE]  [MOYEN]  [DIFFICILE]           │  ║ ║  │                                           │  ║ ║  │ Classe : [SOULRENDER ▼]                  │  ║ ║  │ Deck   : [Build Agro ▼]                  │  ║ ║  │                                           │  ║ ║  │ [▶ JOUER]                                │  ║ ║  └──────────────────────────────────────────┘  ║ ║                                                  ║ ║  ┌──────────────────────────────────────────┐  ║ ║  │ 🏆 RANKED                                │  ║ ║  │ ──────────────────────────────────────   │  ║ ║  │ [1v1]  [2v2]  [3v3]                      │  ║ ║  │                                           │  ║ ║  │ Ton MMR 1v1 : 1847 (Diamant III)         │  ║ ║  │ Saison se termine dans : 47 jours        │  ║ ║  │                                           │  ║ ║  │ Classe : [SOULRENDER ▼]                  │  ║ ║  │ Deck   : [Build Agro ▼]                  │  ║ ║  │                                           │  ║ ║  │ [▶ CHERCHER UN MATCH]                    │  ║ ║  └──────────────────────────────────────────┘  ║ ║                                                  ║ ║  Cooldown défaite : 30 sec après une défaite    ║ ╚════════════════════════════════════════════════╝ |
|---|

# **6. SYSTÈME DE CLASSEMENT (LADDER)**

## **STRUCTURE DU MMR**

| RANG | MMR | PROFIL JOUEUR |
|---|---|---|
| BRONZE III → I | 0 - 999 | Découverte, fluctuation forte |
| ARGENT III → I | 1000 - 1399 | Joueurs réguliers |
| OR III → I | 1400 - 1699 | Joueurs solides, 30% des actifs |
| PLATINE III → I | 1700 - 1999 | Top 15%, maîtrise des classes |
| DIAMANT III → I | 2000 - 2399 | Top 5%, lecture avancée |
| MAÎTRE | 2400 - 2799 | Top 1%, joueurs compétitifs |
| GRAND MAÎTRE | 2800 - 3199 | Top 100 par mode/classe |
| LÉGENDE | 3200+ | Top 10 mondial uniquement |

## **CALCUL DU MMR — ELO MODIFIÉ**

| Formule de base (ELO classique) : expected_winner = 1 / (1 + 10^((mmr_loser - mmr_winner) / 400)) delta = K * (1 - expected_winner)  K-factor : - K=40 si <30 matchs joués (placement) - K=24 entre 30-200 matchs - K=16 après 200 matchs (stabilisé)  Modifications Nymora : - Bonus +5 MMR si le match a duré moins de 5 tours (récompense la pression) - Pénalité -3 MMR si le match a duré plus de 12 tours (jeu mou) - Bonus performance +10 MMR si le perdant a fait plus de dégâts mais a perdu (encouragement) - Cap : ±50 MMR par match (anti-tilting et anti-smurf) |
|---|

## **LEADERBOARDS**

- Top 100 GLOBAL (par mode 1v1/2v2/3v3) — affichage public

- Top 100 PAR CLASSE (combinaison classe + mode = 5 × 3 = 15 leaderboards) — pour les mains spécialistes

- Top 100 PAR PAYS (basé sur l'IP géolocalisée au moment de l'inscription)

- Top 100 PAR CLAN — leaderboard de clans (somme des MMR des 10 meilleurs membres)

- Mise à jour quasi temps-réel via Redis sorted sets (lecture < 1ms)

## **SAISONS**

Une saison dure 3 mois (90 jours). À la fin :

- Reset MMR partiel : new_mmr = 1000 + (current_mmr - 1000) × 0.7 — soft reset, le talent reste lisible

- Récompenses fin de saison selon rang atteint (cosmétiques saisonnier exclusifs, Nymos, titres)

- Top 100 de chaque mode = badge permanent visible sur le profil

- Reset des leaderboards à 0

- Annonce in-game 7 jours avant la fin

## **ANTI-SMURF & ANTI-BOOST**

| ATTENTION Détection automatique de smurfing : un compte neuf qui win-streak avec un winrate >80% sur 20 matchs reçoit un MMR boost forcé (+200 instantané) pour le placer à son vrai niveau. Détection de boosting (joueur Diamant qui aide un Bronze) via patterns IP + comportement — flag pour modération manuelle. |
|---|

# **7. CHAT MULTI-CANAL**

## **CANAUX**

| CANAL | PARTICIPANTS | VISIBILITÉ |
|---|---|---|
| GLOBAL | Tous les joueurs connectés | Visible sur la map commu uniquement |
| CLAN | Membres du clan | Toujours visible (overlay UI) |
| PRIVÉ | 1-1 entre amis | Toujours visible, pop sur réception |
| COMBAT | Joueurs du match en cours | Visible uniquement pendant un match |
| SYSTÈME | Notifications du jeu | Lecture seule (lvl up, succès, MMR change) |

## **INTERFACE**

| ╔══════════════════════════════════════════╗ ║ [Global] [Clan] [Privé] [Combat] [Sys]   ║ ║                                            ║ ║ [Global][14:32] Mortis_X : gg wp          ║ ║ [Clan ][14:33] Lorenzo  : qui pour 3v3 ?  ║ ║ [Sys  ][14:33] Lorenzo a gagné +28 MMR    ║ ║ [Clan ][14:34] Aria_     : moi !          ║ ║ [Privé][14:35] Bestfriend: bouge je arrive║ ║                                            ║ ║ ┌───────────────────────────────┐ [📤]   ║ ║ │ Tape ton message...            │        ║ ║ └───────────────────────────────┘        ║ ║                                            ║ ║ [⚙ Filtres] [🚫 Liste bloqués]            ║ ╚══════════════════════════════════════════╝ |
|---|

## **FILTRES & MODÉRATION**

Le filtre est appliqué côté SERVEUR avant broadcast. Aucun message brut n'arrive sur les clients d'autres joueurs.

- Filtre anti-insulte : liste de mots interdits (FR + EN), niveau adjustable par joueur (off/light/strict)

- Filtre anti-spam : max 5 messages / 10 sec / joueur, sinon mute auto 60 sec

- Filtre anti-flood : message identique 3x = warn, 5x = mute 5 min

- Anti-doxing : filtre regex sur emails, numéros de téléphone, IP

- Mute-list : un joueur peut mute jusqu'à 200 autres joueurs (sa liste personnelle)

- Signalement : bouton "signaler" sur chaque message → log + queue de modération

## **STOCKAGE & RGPD**

| INFO Tous les messages sont stockés en PostgreSQL pendant 30 jours (modération). Après 30 jours : purge automatique sauf si signalés. Les messages signalés sont conservés 1 an pour traçabilité. Le joueur peut demander la suppression de tous ses messages via /api/auth/delete-my-chat. |
|---|

## **EMOTES & STICKERS**

Le chat supporte des emotes spécifiques au jeu (ex: :soulrender_rage:, :ghostra_voile:, :colossar_slam:). Certaines emotes sont des cosmétiques achetables ou débloquables via succès.

# **8. BOUTIQUE**

## **DEUX MONNAIES**

|  NYMOS (in-game) ▸ Gagnés en jouant : matchs ranked, succès, missions quotidiennes ▸ Cap journalier : 1000 Nymos / jour pour éviter le grind toxique ▸ Achetables avec premium currency (1000 Nymos = 1€ équivalent) ▸ Servent à : cosmétiques basiques, expansions deck (5e deck slot bonus), reroll missions ▸ Ne servent JAMAIS à acheter du power |  PREMIUM CURRENCY (Shards) ▸ Achetables uniquement avec argent réel ▸ Packs : 500 / 1200 / 3000 / 8000 Shards (bundles avec bonus) ▸ Servent à : battle pass premium, cosmétiques exclusifs, bundles événementiels ▸ Convertibles en Nymos (1 Shard = 5 Nymos), pas l'inverse ▸ Toujours offerts avec les achats de battle pass premium |
|---|---|

| PRINCIPE F2P F2P éthique : aucun item de la boutique ne donne d'avantage compétitif. Les sorts, classes, deck slots de base sont gratuits. Seuls les COSMÉTIQUES sont monétisés. Le battle pass premium offre uniquement des cosmétiques + boost XP non-pay-to-win. |
|---|

## **CATÉGORIES**

| CATÉGORIE | DESCRIPTION | PRIX TYPIQUE |
|---|---|---|
| Skins de classe | Apparence du sprite en combat ET sur la map commu | 150-800 Shards |
| Bannières de profil | Image affichée derrière l'avatar | 500-200 Nymos |
| Titres | Texte affiché sous le pseudo (ex: "Le Saigneur") | 1000-3000 Nymos |
| Effets de spell | Particules custom sur les sorts (ne change pas le gameplay) | 200-600 Shards |
| Emotes | Animations sur la map commu | 100-400 Shards |
| Stickers chat | Images dans le chat | 50-200 Nymos |
| Bundles événementiels | Pack thématique (Halloween, Noël) | 1000-2500 Shards |
| Battle Pass Premium | Tier premium pour 90 jours de saison | 1000 Shards (≈ 10€) |

## **ROTATION & FOMO ÉTHIQUE**

- Rotation hebdomadaire : 6-8 items mis en avant, refresh chaque lundi

- Boutique permanente : tous les cosmétiques de base toujours disponibles

- Items événementiels : exclusifs pendant 4 semaines, puis retournent dans le pool 6 mois plus tard

- Pas de "limited forever" — tous les items reviennent éventuellement (anti-FOMO toxique)

- Pas de loot box — tous les achats sont directs et transparents

## **FLUX D'ACHAT**

| [Client] User clique "Acheter" sur un skin ↓ [Client] Affiche modale de confirmation "Skin Soulrender Carmin - 600 Shards. Confirmer ?" ↓ [Client] POST /api/shop/purchase { itemId, currency: "shards", quantity: 1 } ↓ [Backend] - Vérifie balance Shards >= prix - Vérifie item disponible (pas expiré, pas déjà possédé) - Transaction ACID : UPDATE wallet SET premium_balance -= price INSERT inventory (account_id, item_id, source='shop') INSERT transactions ↓ [Backend] 200 { newBalance, newInventory } ↓ [Client] Animation "Skin débloqué !" + propose équipement immédiat |
|---|

# **9. BATTLE PASS**

## **STRUCTURE**

- Saison de 90 jours (synchronisée avec saison ranked)

- 100 tiers à débloquer via XP de battle pass (différent de l'XP de classe)

- Voie GRATUITE : récompenses à chaque tier (Nymos, stickers, 1-2 skins basiques en fin de pass)

- Voie PREMIUM : achat 1000 Shards (≈ 10€) → débloque toutes les récompenses premium des 100 tiers

- Voie ÉLITE : achat 2500 Shards (≈ 25€) → premium + skip 25 tiers d'office + 1 skin exclusif élite

- XP gagnée : 100 XP par match ranked, 50 XP par match casual, 30 XP par match IA, bonus quêtes quotidiennes/hebdomadaires

## **DURÉE D'OBTENTION**

Calibrage : un joueur qui joue 1h/jour finira 80 tiers en 90 jours (sans achat de tier). Le battle pass est PRÉVU pour ne pas être 100% fini par tout le monde — c'est OK. Les joueurs hardcore feront 100 tiers + Prestige.

## **PRESTIGE (POST-100)**

Au-delà du tier 100, chaque tier supplémentaire débloque 100 Nymos. Pas de limite. Récompense les hardcore sans pousser à acheter du gain de temps.

## **QUÊTES**

| TYPE | FRÉQUENCE | RÉCOMPENSE XP | RESET |
|---|---|---|---|
| Quotidiennes | 3 quêtes / jour | 100-300 XP BP chacune | Reset 04:00 UTC |
| Hebdomadaires | 5 quêtes / semaine | 500-1000 XP BP | Reset lundi 04:00 UTC |
| Saisonnières | 20 quêtes longue durée | 2000-5000 XP BP | Reset fin de saison |

Exemples de quêtes : "Gagne 5 matchs ranked avec n'importe quelle classe", "Inflige 10000 dégâts cumulés", "Joue 3 classes différentes en ranked", "Termine un match en moins de 5 tours".

# **10. SYSTÈME DE CLANS**

## **CRÉATION & GESTION**

- Coût création : 5000 Nymos (anti-spam de clans morts)

- Tag de clan : 3-4 caractères majuscules, unique global

- Nom : 3-20 caractères, filtre anti-insulte, unique

- Capacité initiale : 20 membres → augmentée par level (max 50)

- Description (motto) : 200 caractères max, modifiable par leader/officier

- Bannière : pool de 20 bannières à débloquer

## **RÔLES**

| RÔLE | QUOTA | POUVOIRS |
|---|---|---|
| LEADER | 1 par clan | Tous les pouvoirs : invite, kick, promote, dissoudre |
| OFFICIER | Max 5 | Invite, kick membres normaux, modifie motto |
| VÉTÉRAN | Illimité | Cosmétique, badge spécial sur chat clan, propose événements |
| MEMBRE | Illimité | Accès chat clan, contribue XP clan, voit roster |

## **XP CLAN & LEVELING**

L'XP de clan se gagne collectivement :

- +10 XP clan par match ranked gagné par un membre

- +5 XP clan par match casual gagné

- +50 XP clan par succès de clan complété (objectifs collectifs)

- +200 XP par participation à un clan war (post-launch feature)

| NIVEAU CLAN | XP REQUIS | DÉBLOCAGES |
|---|---|---|
| 1 | 0 XP | 20 membres max, banner basique |
| 5 | 5000 XP | 25 membres, 1 banner premium débloquée |
| 10 | 20000 XP | 30 membres, chat clan custom emotes |
| 20 | 100000 XP | 40 membres, salon clan privé sur la map commu |
| 30 | 300000 XP | 50 membres (cap), banner légendaire |
| 50 | 1000000 XP | Statut prestige, accès clan war priority queue |

## **CLAN WARS (POST-LAUNCH, PHASE 6)**

Affrontements 5v5 entre clans, organisés en matchmaking. Calendrier : weekend uniquement. Récompenses : XP clan x3, badge saisonnier pour les top clans.

# **11. SYSTÈME DE SUCCÈS**

## **STRUCTURE**

3 catégories de succès, chacune avec progression et points :

| CATÉGORIE | QUANTITÉ | EXEMPLE |
|---|---|---|
| GÉNÉRAUX | 60 succès | Jouer 100 matchs, atteindre niveau 50, etc. |
| PAR CLASSE | 20 succès × 5 classes = 100 | Gagner 50 matchs en Soulrender, etc. |
| DÉFIS | 40 succès difficiles | Win streak de 10, gagner sans heal, etc. |

## **RÉCOMPENSES**

- Chaque succès = points (5 / 10 / 20 / 50 selon difficulté)

- Total points = score de complétion affiché sur le profil

- Tous les 100 points : récompense Nymos

- Tous les 500 points : titre exclusif

- Tous les 1000 points : skin/effet cosmétique unique

- À 100% complétion : badge "Maître de Nymora" + skin légendaire exclusif

## **EXEMPLES DE SUCCÈS**

| CAT. | NOM | DESCRIPTION | PTS |
|---|---|---|---|
| GÉNÉRAL | Premier sang | Gagner ton premier match | 5 |
| GÉNÉRAL | Stratège | Atteindre 1000 MMR en 1v1 | 10 |
| GÉNÉRAL | Légende vivante | Atteindre 3000 MMR en 1v1 | 50 |
| CLASSE | Boucher | Gagner 50 matchs en Soulrender | 10 |
| CLASSE | Maître de la rage | Lancer Âme Lacérée 100 fois | 20 |
| DÉFI | Sans bavures | Gagner un match sans utiliser un seul sort de survie | 20 |
| DÉFI | Express | Gagner un match en moins de 4 tours | 20 |
| DÉFI | Le Saigneur | Gagner 10 matchs d'affilée en Soulrender | 50 |

# **12. COSMÉTIQUES**

## **CATÉGORIES**

- SKINS DE CLASSE — change le sprite du personnage en combat (5 par classe = 25 base + saisonniers)

- BANNIÈRES PROFIL — image derrière l'avatar (50 base + thématiques)

- TITRES — texte sous le pseudo, débloqués via succès, achats, événements

- EFFETS DE SORT — particules custom sur les sorts (pas d'avantage gameplay)

- EMOTES MAP COMMU — animations spectaculaires pour le sprite (saluer, danser, taunt)

- STICKERS CHAT — images animées dans les channels chat

- BANNIÈRES DE COMBAT — affichées avant et après match

- AVATARS DE PROFIL — portraits 256x256 (pas le skin de combat)

## **OBTENTION**

| SOURCE | MÉCANIQUE | % DU CATALOGUE |
|---|---|---|
| Boutique | Achat direct (Nymos ou Shards) | 70% du catalogue |
| Battle Pass | Récompense de tier | 15% (exclusifs saisonniers) |
| Succès | Débloqué via achievements | 10% |
| Événements | Tournoi, quête saisonnière | 5% |

# **13. NIVEAUX PAR CLASSE**

## **PRINCIPE**

Chaque classe a son propre niveau de 1 à 50, indépendant des autres classes. Un compte peut donc avoir Soulrender 50 et Necram 12. Le leveling DÉBLOQUE des sorts dans le deck builder.

## **PROGRESSION DES SORTS**

| NIVEAU | DÉBLOCAGE | IMPLICATION GAMEPLAY |
|---|---|---|
| 1 | 5 sorts (1 offensif basique + 1 tactique + 3 survie) | Match safe, découverte |
| 3 | +2 offensifs | Premier vrai outil DPS |
| 5 | +1 tactique avancé | Premier outil de setup réel |
| 10 | +1 offensif majeur | Pic de pression accessible |
| 15 | +1 tactique de contrôle | Mindgames possibles |
| 20 | +1 sort signature en combat (ex: Charge Brutale) | Build agressif viable |
| 25 | +1 sort de survie avancé | Sustain build viable |
| 30 | +1 burst final (ex: Détonation Sanglante) | Build burst viable |
| 35 | +1 panic button (ex: Dernier Souffle) | Tous outils défensifs |
| 40 | +1 sort signature défensif | Polyvalence totale |
| 50 | Tous les 15 sorts débloqués + skin exclusif niveau 50 | Maîtrise complète |

| DESIGN L'IDÉE : un nouveau joueur n'est PAS submergé par 15 sorts dès le début. Il en a 5 et apprend la classe. À mesure qu'il level, il découvre des outils plus avancés. Il peut quand même affronter des joueurs niveau 50 — son MMR le placera contre des joueurs de son niveau. |
|---|

## **XP DE CLASSE**

| ACTION | XP CLASSE |
|---|---|
| Match ranked gagné (cette classe) | 200 XP |
| Match ranked perdu (cette classe) | 80 XP |
| Match casual / défi | 100 XP gagné / 40 XP perdu |
| Match IA Difficile | 60 XP gagné |
| Match IA Moyen | 40 XP gagné |
| Match IA Facile | 20 XP gagné |
| Quêtes quotidiennes (variable) | 50-200 XP |

XP nécessaire par niveau : courbe exponentielle douce. Niveau 1→2 = 200 XP. Niveau 49→50 = 8000 XP. Total pour passer du niveau 1 au niveau 50 : environ 150 000 XP, soit ~750 matchs ranked gagnés (par classe).

# **14. PARAMÈTRES**

## **ONGLETS**

| ONGLET | OPTIONS |
|---|---|
| GAMEPLAY | Vitesse animations, auto-confirm sorts, hover info, raccourcis clavier |
| VIDÉO | Résolution, plein écran, qualité (Low/Med/High/Ultra), FPS cap, V-Sync |
| AUDIO | Volume master, musique, SFX, voice (chat futur), volume autre joueurs |
| CONTRÔLES | Rebind clavier (PC), tactile (mobile), gamepad (futur) |
| CHAT | Filtre niveau, mute global, bloqués |
| PRIVACY | Profil public/privé, statut online visible, demandes d'amis ouvertes |
| COMPTE | Email, password, 2FA, langue, suppression de compte |
| NOTIFICATIONS | Quels événements génèrent une notif (defy, ami online, clan, succès) |
| ACCESSIBILITÉ | Daltonisme (3 modes), reduce motion, font scaling, high contrast |

## **ACCESSIBILITÉ — DÉTAIL**

- MODES DALTONISME : Protanopie, Deutéranopie, Tritanopie. Modifie les couleurs des effets de sort, marques, jauges de ressource pour qu'elles restent distinguables.

- REDUCE MOTION : désactive les animations non-essentielles (caméra qui shake, particules excessives) pour les joueurs sensibles aux mouvements.

- FONT SCALING : 75% / 100% / 125% / 150%. Le UI scale en conséquence.

- HIGH CONTRAST : augmente les contrastes pour malvoyants. Les sorts ont des contours blancs visibles.

- AUDIO DESCRIPTIONS : annonces vocales optionnelles pour les événements clés du combat (sort dorsal détecté, ressource max atteinte, etc).