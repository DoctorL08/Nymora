# 🏰 LE GRIMOIRE DU KICKSTARTER — NYMORA DE A À Z

> **Manuel complet pour construire, lancer et conclure le Kickstarter de Nymora.**
> Compagnon technique de `07_PLAN_COMMUNICATION.md` (qui gère l'audience). Cette doc-ci gère **la campagne elle-même**.
> Document vivant — coche les cases au fur et à mesure.
>
> **Version :** 1.0 — créée le 28 mai 2026
> **Règle d'or :** on ne clique "Lancer" QUE quand on a ~500 engagés (cf. doc comm). Le reste de ce grimoire se prépare *pendant* qu'on bâtit cette audience.

---

## ⚠️ AVERTISSEMENTS PRÉALABLES (à lire avant tout)

1. **Je ne suis pas avocat ni comptable.** Les sections légal/fiscal (§9) sont des repères pour poser les bonnes questions — **à valider avec un comptable français** avant de lancer. Une campagne KS qui rapporte de l'argent = revenu imposable. Ne pas improviser.
2. **Kickstarter = tout-ou-rien.** Si on n'atteint pas l'objectif à la fin, on touche **0 €** et personne n'est débité. → On fixe un objectif **bas et certain d'être atteint**, et on fait grimper avec les paliers étendus (stretch goals). C'est LA décision stratégique du KS.
3. **Le trailer est l'asset n°1.** 90% des gens décident dans les 30 premières secondes de la vidéo. On y met le paquet.

---

## SOMMAIRE

1. [Prérequis & ouverture du compte](#1-prérequis--ouverture-du-compte)
2. [Fixer l'objectif de financement (le calcul)](#2-fixer-lobjectif-de-financement-le-calcul)
3. [Les frais (ce que KS et le paiement prélèvent)](#3-les-frais)
4. [La structure de la page (section par section)](#4-la-structure-de-la-page-campagne)
5. [Le trailer / la vidéo](#5-le-trailer--la-vidéo)
6. [Les paliers de récompense (tiers)](#6-les-paliers-de-récompense-reward-tiers)
7. [Les paliers étendus (stretch goals)](#7-les-paliers-étendus-stretch-goals)
8. [Le calendrier de campagne](#8-le-calendrier-de-campagne)
9. [Légal & fiscal (France)](#9-légal--fiscal-france)
10. [Le playbook du jour J](#10-le-playbook-du-jour-j)
11. [Pendant la campagne (30 jours)](#11-pendant-la-campagne)
12. [Après : livraison des récompenses](#12-après-la-campagne--fulfillment)
13. [Checklist finale pré-lancement](#13-checklist-finale-pré-lancement)
14. [Les pièges qui tuent une campagne](#14-les-pièges-qui-tuent-une-campagne)

---

## 1. PRÉREQUIS & OUVERTURE DU COMPTE

Kickstarter est **disponible pour les créateurs français**. Pour pouvoir recevoir l'argent, il faut tout vérifier AVANT le lancement (la vérification peut prendre quelques jours — ne pas s'y prendre la veille).

**Ce qu'il faut réunir :**
- [ ] Un **compte Kickstarter** (le créer avec l'adresse pro/projet, pas perso si possible).
- [ ] **Vérification d'identité** : pièce d'identité du porteur de projet (la personne qui reçoit les fonds — décidez qui c'est entre vous deux, ça a des implications fiscales § 9).
- [ ] **Un compte bancaire** (français) pour le versement. KS verse via virement après la fin de campagne réussie.
- [ ] **Une carte bancaire valide** (KS la demande pour la vérification).
- [ ] **Adresse de résidence** correspondant au pays du projet (France).

**Décision à prendre en amont (importante) :** **qui porte juridiquement le projet ?** Une seule personne reçoit les fonds sur KS. Les deux options :
- **(a)** L'un de vous porte le projet en **micro-entreprise** (le plus simple/rapide pour démarrer), reverse une part à l'autre via facturation/contrat.
- **(b)** Vous créez une **société commune** (SAS/SASU…) — plus lourd, mais plus propre si l'ambition est sérieuse et durable.
→ **À trancher avec un comptable** (§9). Ne pas lancer le KS sans avoir réglé ça.

---

## 2. FIXER L'OBJECTIF DE FINANCEMENT (LE CALCUL)

**Principe :** l'objectif affiché doit être le **minimum vital pour tenir une promesse honnête**, pas le rêve. On le veut **atteignable à coup sûr** (idéalement couvert à 30-40% le jour J par les Fondateurs chauds). Le rêve, c'est les **stretch goals** (§7).

### Méthode : on additionne les vrais coûts à financer

| Poste | Estimation à remplir | Notes |
|---|---|---|
| **Serveurs / infra (12 mois)** | ___ € | Vous êtes déjà sur OVH pas cher → poste faible. Anticiper la montée en charge alpha ouverte. |
| **Art / animations (designer renforcé)** | ___ € | Le gros poste : financer du temps designer dédié (skins combat, VFX, key arts, classes). |
| **Audio / musique / SFX** | ___ € | Compositeur freelance pour OST + pack SFX combat (les SoundId orphelins attendent ça). |
| **Localisation (FR→EN au minimum)** | ___ € | Traduction pro de l'UI + textes pour l'alpha ouverte internationale. |
| **Outils / licences** | ___ € | Steam (100 $), assets, plugins, polices, etc. |
| **Fulfillment récompenses** | ___ € | **Quasi nul** : nos récompenses sont **numériques** (titres/skins/familiers/accès — systèmes déjà en place). Énorme avantage. |
| **Frais KS + paiement (~8-10%)** | ___ € | Voir §3. À provisionner SUR l'objectif. |
| **Provision impôts/taxes (~20-30%)** | ___ € | Voir §9. À provisionner, sinon mauvaise surprise. |
| **Marge de sécurité (+15%)** | ___ € | Toujours prévoir l'imprévu. |
| **= OBJECTIF AFFICHÉ** | **___ €** | |

### Garde-fou
> **Conseil fort :** vise un objectif que tu es **sûr** de couvrir. Pour un premier KS de petit studio FR sans gros historique, un objectif **modeste (ex. 8 000–15 000 €)** atteint et dépassé envoie un signal de succès bien plus puissant qu'un objectif ambitieux raté. **Un KS financé à 250% fait la une ; un KS financé à 90% rapporte 0 €.** On garde l'ambition pour les stretch goals.

---

## 3. LES FRAIS

À provisionner dès le calcul de l'objectif. Ordres de grandeur (à reconfirmer sur le barème KS en vigueur) :
- **Commission Kickstarter : ~5%** du montant collecté (si financé).
- **Traitement des paiements : ~3-5%** (+ petite part fixe par contribution).
- **Total à prévoir : ~8-10%** prélevés sur le montant brut.

→ Donc si on a besoin de **10 000 € nets**, il faut viser un objectif **brut d'environ 11 000–11 500 €** rien que pour les frais (avant impôts). Ne jamais l'oublier dans le §2.

---

## 4. LA STRUCTURE DE LA PAGE CAMPAGNE

L'ordre compte : on accroche, on prouve, on rassure, on appelle à l'action. Structure éprouvée, à remplir bloc par bloc :

### Bloc 1 — En-tête
- [ ] **Titre du projet :** clair + accrocheur. Ex. *« Nymora — Le PvP tactique dark fantasy où chaque classe est un jeu différent »*.
- [ ] **Sous-titre / blurb (135 caractères) :** le pitch d'une phrase (cf. doc comm §1).
- [ ] **Vidéo (le trailer)** en haut — c'est le 1er truc vu (§5).
- [ ] **Visuel de couverture** (key art) propre, lisible en miniature.

### Bloc 2 — Le hook (les 3 premières lignes)
- [ ] Une accroche forte qui résume pourquoi Nymora est différent. *« 5 classes. 5 jeux radicalement différents. Tu ne perds pas en points de vie — tu perds en temps, en information, en territoire, en identité. »*
- [ ] **Immédiatement : un GIF de gameplay** qui claque (le juice, le SMASH).

### Bloc 3 — C'est quoi Nymora ?
- [ ] Le pitch de 30s en texte + GIFs.
- [ ] Les comparables (Dofus 1.29 / Slay the Spire / Brawlhalla) pour situer vite.
- [ ] **Insister : LE JEU EST DÉJÀ JOUABLE.** Montrer le ranked, le hub, la méta. C'est ce qui nous distingue de 90% des KS.

### Bloc 4 — Les 5 classes (notre signature)
- [ ] Une section par classe avec son visuel + son hook + 1 GIF de sa mécanique unique :
  - **Soulrender** — l'horloge biologique (perdre en temps)
  - **Nightseer** — l'information asymétrique (map cachée)
  - **Colossar** — la géométrie qui se ferme (arène qui rétrécit)
  - **Necram** — la mort programmée (condamnations à retardement)
  - **Ghostra** — l'identité brisée (qui frappe ?)

### Bloc 5 — Pourquoi un Kickstarter ? (transparence)
- [ ] *« On a construit Nymora à deux, en silence. Il tourne déjà. Ce Kickstarter sert à l'amener plus loin, plus vite, et avec vous. »*
- [ ] **Le camembert d'usage des fonds** (issu du §2) : serveurs / art / audio / localisation / frais. Visuel clair.
- [ ] **Ce que ça N'EST PAS :** *« Pas notre loyer. Pas un pari sur un concept. Un accélérateur sur un jeu qui existe. »* (ton anti-réclameur de la doc comm).

### Bloc 6 — L'équipe
- [ ] Photo/avatar + rôle de chacun. L'histoire des **2 personnes** (l'underdog vend). Parcours, pourquoi ce jeu.

### Bloc 7 — Les paliers (affichés à droite, mais expliqués ici) → §6

### Bloc 8 — Les paliers étendus (stretch goals) → §7

### Bloc 9 — Calendrier / roadmap
- [ ] Une frise simple : pré-alpha fermée (en cours) → alpha ouverte → contenu financé par le KS. **Honnête, pas de dates intenables.**

### Bloc 10 — Risques & Défis (SECTION OBLIGATOIRE Kickstarter)
- [ ] KS impose cette section. **Ne pas la bâcler — la traiter sérieusement rassure.**
- [ ] Exemples honnêtes : dépendance à un petit studio, calendrier qui peut glisser, équilibrage live-service, montée en charge serveur. Pour chaque risque → comment on le gère. **Notre meilleur argument : le jeu marche déjà, donc le risque "vaporware" est faible.**

### Bloc 11 — FAQ
- [ ] Quand sort le jeu ? / Sur quelle plateforme ? (Windows d'abord) / C'est pay-to-win ? (**NON, jamais** — cosmétique only) / Comment je reçois mes récompenses ? / Et si l'objectif n'est pas atteint ? / Mac/Mobile ? (post-alpha)

---

## 5. LE TRAILER / LA VIDÉO

**L'asset le plus important de toute la campagne.** 60-90 secondes max. Structure qui convertit :

| Temps | Contenu | But |
|---|---|---|
| 0-5 s | **Le hook le plus violent** : un combo de juice combat (SMASH, sang, signature) | Stopper le scroll. Pas d'intro lente, pas de logo 10s. |
| 5-25 s | **Le concept** : "5 classes, 5 jeux" — montage rapide des 5 mécaniques uniques | Faire comprendre l'unicité. |
| 25-50 s | **La preuve** : ranked, hub, méta — "et tout ça est déjà jouable" | Dé-risquer. |
| 50-70 s | **L'équipe + la vision** : 2 personnes, l'ambition, voix-off sincère | Créer le lien humain. |
| 70-90 s | **L'appel** : "Rejoignez les Fondateurs de Nymora" + logo + date KS | Call to action clair. |

- [ ] Tourné en **capture propre** (résolution native, 60 fps si possible).
- [ ] **Sous-titré** (beaucoup regardent sans son) — et ça sert la version EN.
- [ ] Musique : idéalement un extrait de l'OST si le compositeur peut produire un bout tôt ; sinon musique libre de droits adaptée dark fantasy.
- [ ] Version courte 15-30s verticale dérivée → réseaux (cf. doc comm).

---

## 6. LES PALIERS DE RÉCOMPENSE (REWARD TIERS)

**Notre super-pouvoir :** toutes nos récompenses sont **numériques et déjà productibles** (titres, bannières, skins, familiers, Battle Pass, accès, rôles Discord). Fulfillment quasi gratuit, livraison fiable. La plupart des KS galèrent là-dessus ; nous non.

**Règles de conception des paliers :**
- 5-7 paliers, pas plus (trop de choix = paralysie).
- Un **palier "sweet spot"** mis en avant (souvent le 2e ou 3e, ~25-35 €) où on concentre la meilleure valeur perçue.
- Des **paliers limités** (quantité plafonnée) pour créer la rareté et l'urgence.
- **Chaque palier inclut tout le précédent.**
- **Zéro pay-to-win** (cosmétique/accès only) — c'est dans nos règles sacrées ET un argument marketing.

| Palier | Prix indicatif | Limite | Contenu (cumulatif) |
|---|---|---|---|
| 🐾 **Curieux** | ~5 € | — | Nom dans les crédits "Fondateurs" + rôle Discord exclusif + accès aux updates |
| ⚔️ **Initié** | ~15 € | — | + Accès alpha garanti + **titre Fondateur** in-game + bannière exclusive Fondateur |
| 🔥 **Combattant** ⭐ | ~30 € | — | + **1 skin Fondateur exclusif** (classe au choix) + 1 familier exclusif + pack de Nymos *(palier mis en avant)* |
| 🛡️ **Vétéran** | ~60 € | — | + Les **5 skins Fondateur** + Battle Pass Saison 1 offert + emote exclusive |
| 👑 **Seigneur** 🐯 | ~120 € | ~50 | + Ton pseudo sur un **PNJ/leurre** du hub + accès au salon Discord "Conseil" (sondages de design) |
| 🌑 **Légende** | ~300 € | ~10 | + Session de jeu avec les devs + **un sort ou un leurre co-nommé** avec toi (sous validation lore) |

> **Add-ons (options) :** KS permet d'ajouter des extras à un palier (ex. "+10 € : pack de bannières", "+15 € : skin supplémentaire"). À préparer pour augmenter le panier moyen. **Aucun add-on pay-to-win.**

> ⚠️ **Chaque récompense doit être tenable.** Ne promettez QUE ce que vous savez livrer (et vous savez livrer du cosmétique → restez-y). Le pseudo-sur-PNJ et le sort-co-nommé sont géniaux car peu coûteux et très désirables.

---

## 7. LES PALIERS ÉTENDUS (STRETCH GOALS)

Annoncés au-dessus de l'objectif. Ils **relancent la dynamique** quand la campagne ralentit en milieu de course (le "ventre mou" classique). Ils transforment l'ambition en bonus collectif sans risquer l'objectif de base.

Exemples (à caler sur les vrais coûts) :
- [ ] **+X € → OST complète** par un compositeur (et tout le monde la reçoit).
- [ ] **+X € → Localisation supplémentaire** (allemand / espagnol).
- [ ] **+X € → Une nouvelle classe** post-alpha (la 6e) — gros aimant.
- [ ] **+X € → Mode 2v2** ramené dans la roadmap (actuellement post-alpha).
- [ ] **+X € → Skins communautaires** votés par les Fondateurs.

> Garder 1-2 stretch goals **secrets** à révéler en cours de campagne = bon levier de relance.

---

## 8. LE CALENDRIER DE CAMPAGNE

- **Durée :** **30 jours** (sweet spot prouvé). 60 jours max sur KS mais les longues campagnes s'essoufflent — **ne pas dépasser 30-35 jours**.
- **Forme de la courbe :** un KS fait l'essentiel de ses fonds au **début (48 premières heures)** et à la **fin (72 dernières heures)**. Le milieu est mou → c'est là qu'on sort les updates et stretch goals.
- **Jour de lancement :** privilégier un **mardi ou mercredi**, en début d'après-midi (heure FR) pour toucher Europe + début de journée US.
- **Pré-lancement :** la page "Notify me" tourne **dès maintenant** (cf. doc comm §4). On ne lance la vraie campagne qu'au seuil des ~500 engagés.

---

## 9. LÉGAL & FISCAL (FRANCE)

> 🚨 **À valider impérativement avec un comptable avant de lancer.** Ce qui suit sert à préparer le rendez-vous, pas à s'en passer.

- **L'argent d'un KS réussi est un revenu imposable.** Il faut une structure pour le recevoir proprement (micro-entreprise au minimum, ou société). On ne reçoit pas 10 000 € sur un compte perso sans déclaration.
- **TVA :** selon le statut et les seuils, la TVA peut s'appliquer. À vérifier (les récompenses = contreparties, pas des dons).
- **Qui porte le projet ?** (cf. §1) Une seule personne reçoit sur KS → définir le partage entre vous deux par écrit (contrat/facturation) pour éviter les litiges et clarifier le fisc.
- **CGU / mentions légales :** prévoir une politique claire (remboursements, données perso/RGPD pour les emails collectés, propriété intellectuelle).
- **Provision fiscale :** mettre de côté **~20-30%** des fonds pour l'impôt/cotisations dès réception. Ne pas tout dépenser.

**Action :** prendre RDV comptable **avant** la semaine de lancement. C'est un coût (quelques centaines d'€) qui évite des milliers d'€ de problèmes.

---

## 10. LE PLAYBOOK DU JOUR J

Les **48 premières heures décident de tout** (l'algo KS pousse les projets qui démarrent fort, la presse aussi).

**La veille :**
- [ ] Page 100% relue, testée sur mobile (la majorité consulte au tel).
- [ ] Message au Discord : *« Demain [heure]. Les premières heures sont décisives. Soyez là. »*
- [ ] Préparer les posts de lancement (tous canaux) en brouillon, prêts à publier.
- [ ] Confirmer que les "Notify me" vont bien recevoir l'email auto de KS.

**Jour J (ordre des opérations) :**
1. [ ] Lancer la campagne à l'heure prévue.
2. [ ] Poster **partout en même temps** (X, TikTok, Reels, Shorts, Discord @everyone, Reddit aux endroits autorisés, IG, Bluesky).
3. [ ] Email/DM aux Fondateurs pré-engagés et aux micro-influenceurs contactés.
4. [ ] **Répondre à TOUT en temps réel** les 2 premiers jours (commentaires KS, DM, Discord).
5. [ ] Première **update KS** dans les 24h : "On est financé à X% — MERCI" (même partiel, ça crée l'élan).

**Objectif des 48h :** atteindre **30-40% de l'objectif**. Si on a bien fait le pré-lancement (§ doc comm), c'est jouable.

---

## 11. PENDANT LA CAMPAGNE

- **Rythme d'updates :** une tous les **2-3 jours**. Toujours de la valeur : nouveau clip, palier étendu débloqué, coulisse, remerciement, sondage Fondateurs.
- **Le contenu organique continue** en parallèle (TikTok/X) et ramène du trafic frais → KS.
- **Milieu de campagne (ventre mou) :** dégainer un stretch goal secret, un nouveau gros clip, ou une interview/feature presse.
- **Derniers 72h :** campagne de "dernière chance" — relancer le Discord, les réseaux, les indécis. Beaucoup de gens backent à la fin.
- **Transparence constante :** chaque communication respecte le ton anti-réclameur (jamais "donnez", toujours "voilà ce qu'on construit ensemble").

---

## 12. APRÈS LA CAMPAGNE — FULFILLMENT

**Notre plus gros avantage : la livraison est numérique et déjà outillée.**

- [ ] **Collecte des infos backers :** KS fournit un **sondage post-campagne** (BackerKit ou le sondage natif KS) pour récupérer le pseudo in-game de chaque backer + le choix de classe pour les skins, etc.
- [ ] **Octroi des récompenses :** via les systèmes existants — `cosmetic:grant` / titres / bannières / familiers / accès alpha / rôles Discord. **Prévoir un script d'octroi en masse "Fondateurs"** (calqué sur `cosmetic:grant-all`).
- [ ] **Cosmétiques exclusifs Fondateur :** les marquer comme **non rachetables** (jamais remis en boutique → la promesse d'exclusivité est tenue). On a déjà le pattern (titres/bannières hors listing boutique).
- [ ] **Tenir le calendrier annoncé** ou communiquer tout retard **en avance et honnêtement** (les backers pardonnent un retard annoncé, jamais un silence).
- [ ] **Garder les Fondateurs proches :** salon Discord dédié, accès prioritaire à l'alpha. Ce sont nos premiers ambassadeurs et nos meilleurs testeurs.

---

## 13. CHECKLIST FINALE PRÉ-LANCEMENT

À 100% cochée avant de cliquer "Lancer" :
- [ ] ~500 engagés atteints (page KS + Discord) — **le gate non-négociable**
- [ ] Compte KS vérifié (identité + banque) ✅
- [ ] Statut juridique + RDV comptable réglés (§9)
- [ ] Objectif de financement calculé et **bas/certain** (§2)
- [ ] Trailer final monté, sous-titré, intégré (§5)
- [ ] Key art + visuels des 5 classes prêts
- [ ] Page rédigée bloc par bloc (§4), relue à 2, testée sur mobile
- [ ] Section "Risques & Défis" sérieusement traitée
- [ ] Paliers + add-ons configurés (§6), zéro pay-to-win
- [ ] Stretch goals préparés (dont 1-2 secrets) (§7)
- [ ] FAQ remplie
- [ ] Posts de lancement rédigés d'avance pour tous les canaux
- [ ] Discord mobilisé, date communiquée
- [ ] Micro-influenceurs/curateurs contactés
- [ ] Script d'octroi récompenses Fondateurs préparé (anticipe le fulfillment)

---

## 14. LES PIÈGES QUI TUENT UNE CAMPAGNE

- ❌ **Lancer froid** (sans les ~500 engagés) → mort lente, invisible.
- ❌ **Objectif trop ambitieux** → tout-ou-rien = 0 € si on rate. Vise bas, dépasse.
- ❌ **Trailer mou / lent** → on perd 90% des gens dans les 10 premières secondes.
- ❌ **Promettre ce qu'on ne sait pas livrer** → reste sur le numérique cosmétique, notre zone sûre.
- ❌ **Oublier les frais et impôts dans l'objectif** → tu finances et tu finis dans le rouge.
- ❌ **Négliger "Risques & Défis"** → les backers exigeants le lisent, ça rassure ou ça refroidit.
- ❌ **Silence après financement** → la confiance se perd dans le fulfillment, pas dans la campagne.
- ❌ **Ton "réclameur"** → on vend l'appartenance aux Fondateurs, jamais la pitié.
- ❌ **Pay-to-win dans les paliers** → trahit la promesse du jeu ET la communauté.

---

*« Un seigneur ne quémande pas son fief. Il invite ses vassaux à bâtir le royaume. » 🐯*
