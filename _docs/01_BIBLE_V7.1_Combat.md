# Nymora — Bible V7.1 Refonte Combat

> Source : `Nymora_V7_1_Refonte_Combat.docx`

---

# **NYMORA**

## **REFONTE COMBAT — V7.1**

*Bible de design pour PvP 1v1 compétitif*

Tactical · Asymétrique · Esportable · Dark Fantasy

1500 HP · 8 PA · 3 PM · 6 sorts par deck · 15 sorts par classe

*+ 1 Sort Signature débloqué à ressource max (cooldown 4 tours)*

*Soulrender · Nightseer · Colossar · Necram · Ghostra*

# **VISION DIRECTOR'S CUT — V7.1**

*Cette V7.1 corrige le tir de la V7.0 : on garde la profondeur narrative, les passifs non-linéaires et les ressources uniques, MAIS on revient à 15 sorts par classe (5 Offensif / 5 Tactique / 5 Survie) pour permettre plusieurs builds par classe. Le SORT SIGNATURE (anciennement "sort Enragé" V6.1) est conservé sous forme de sort débloqué à ressource max, avec cooldown 4 tours après usage — réutilisable plusieurs fois en match si la ressource remonte.*

## **CHANGEMENTS V7.0 → V7.1**

| CHANGEMENT | EXPLICATION |
|---|---|
| 8 sorts/classe → 15 sorts/classe | Le joueur a plus de matière pour bâtir plusieurs builds dans une même classe (agressif/contrôle/sustain/burst) sans changer d'identité. Deck de 6 sur 15 = forte deckbuild. |
| Sort signature "intégré au kit" → Sort signature SÉPARÉ | Le sort signature n'est PAS dans les 6 sorts équipés. C'est un slot à part, dispo automatiquement quand la ressource atteint le cap. Cooldown 4 tours après usage. |
| Nouvelle structure 5/5/5 | 5 sorts Offensifs (dégâts) / 5 sorts Tactiques (setup/contrôle) / 5 sorts Survie (heal/bouclier). Lisible, structuré comme V6.1, mais avec la nouvelle philosophie. |
| Conservation totale des passifs V7.0 | Hémoglyphe, Prescience, Fondation, Putréfaction, Rémanence inchangés. |
| Conservation totale des ressources V7.0 | Inchangées. Toujours 5 systèmes asymétriques. |

## **PRINCIPE FONDATEUR**

Chaque classe doit changer non pas le SCORE du combat, mais la NATURE du combat.

Un duel Soulrender vs Nightseer ne se joue pas sur la même grille mentale qu'un duel Colossar vs Ghostra. C'est l'asymétrie absolue qui fait le skill ceiling : pour gagner avec et contre chaque classe, il faut apprendre 5 jeux.

## **LES 5 JEUX**

| CLASSE | C'EST QUOI | CE QUE L'ADVERSAIRE RESSENT |
|---|---|---|
| SOULRENDER | Le jeu de l'horloge biologique | L'adversaire perd le match en TEMPS, pas en HP. |
| NIGHTSEER | Le jeu de l'information asymétrique | Les deux joueurs ne voient pas la même map. |
| COLOSSAR | Le jeu de la géométrie qui se ferme | L'arène se rétrécit physiquement, tour après tour. |
| NECRAM | Le jeu de la mort qui se programme | Chaque sort est une condamnation à retardement. |
| GHOSTRA | Le jeu de l'identité brisée | L'adversaire ne sait jamais qui frappe. |

## **PILIERS DE DESIGN V7.1**

| PILIER | PRINCIPE |
|---|---|
| Asymétrie radicale | Aucune classe ne partage de mécanique avec une autre. |
| Passifs non-linéaires | Aucun palier rigide. Chaque passif scale différemment selon le contexte. |
| Ressources uniques | 5 systèmes différents (HG, PR, FD, PT, RM). |
| 15 sorts/classe en 3 axes | 5 Offensifs, 5 Tactiques, 5 Survie. Force le joueur à choisir 6 et à faire des trade-offs. |
| Sort signature à ressource max | Le payoff de la ressource — réutilisable plusieurs fois si la jauge remonte. Pas de "1x/match" frustrant. |
| Match court (15-20 min) | 6-8 tours moyens. |
| Lisibilité absolue en stream | Chaque effet identifiable en 1 seconde. |

# **SYSTÈME DE COMBAT**

## **STATS DE BASE**

| STAT | VALEUR | POURQUOI |
|---|---|---|
| HP | 1500 | 6-8 tours de jeu, hits forts (200+) sans one-shot |
| PA / tour | 8 | Force des choix : 2 sorts à 4 PA, 3 sorts à 2-3 PA, ou 1 burst à 5+ |
| PM / tour | 3 (2 pour Colossar) | Mobilité limitée force le positionnement précoce |
| Sorts équipés | 6 sur 15 | Choix de pré-match : forte deckbuild |
| Sort signature | Slot séparé | Débloqué automatiquement à ressource max. Cooldown 4 tours après usage. |
| Tour timer | 15s | Tension constante. Force l'instinct. |
| Initiative | Tirage tour 1, alternance ensuite | Premier joueur avantagé en pression — second en information |

## **STRUCTURE DU DECK**

Avant le match, le joueur sélectionne 6 sorts parmi les 15 disponibles de sa classe. AUCUNE restriction interne (pas de "max 2 offensifs obligatoires") — composition totalement libre. Le sort signature N'EST PAS dans ce choix : il s'ajoute automatiquement comme 7e slot, dispo dès que la ressource est au cap.

**Exemples de builds Soulrender**

- BUILD AGRO : Tranche-Âme · Ouvre-Plaie · Charge Brutale · Empoignade · Pacte de Sang · Curée. Plan : initier au tour 1, kill avant tour 5, recycler Âme Lacérée.

- BUILD CONTRÔLE : Tranche-Âme · Marque de Carnage · Riposte Carmin · Rugissement · Détonation Sanglante · Sève Vive. Plan : zoner, punir l'engage, payoff zone.

- BUILD SUSTAIN : Tranche-Âme · Ouvre-Plaie · Cautérisation · Peau de Fer · Sève Vive · Rage Insatiable. Plan : tenir le long terme, multiples Âme Lacérée.

## **LE SORT SIGNATURE — RÈGLES SYSTÈME**

- Chaque classe a UN sort signature unique. Il n'est pas dans les 15 sorts équipables — c'est un slot à part.

- Il devient ACCESSIBLE quand la ressource de la classe atteint son cap (5 HG, 4 PR, 3 FD, 6 PT, 3 leurres).

- Tant que la ressource est au cap, le sort peut être lancé une fois. Le coût en PA est généralement bas (2-4 PA).

- Après usage, il rentre en COOLDOWN 4 TOURS. Pendant ce cooldown, même si la ressource est au cap, il est indisponible.

- Une fois le cooldown terminé, il redevient lançable — à condition que la ressource soit (ou redevenue) au cap.

- Implication : un Soulrender qui charge ses HG vite peut lancer Âme Lacérée 2-3 fois en match. Un Necram qui plante des marques peut Virus Fatal 2 fois. Réutilisable, mais pas spammable.

## **LA RESSOURCE & LE SIGNATURE — 5 SYSTÈMES**

| CLASSE | RESSOURCE | CAP | SORT SIGNATURE |
|---|---|---|---|
| SOULRENDER | HÉMOGLYPHE | Cap : 5 | ÂME LACÉRÉE |
| NIGHTSEER | PRESCIENCE | Cap : 4 | TRAQUENARD |
| COLOSSAR | FONDATION | Cap : 3 | EFFONDREMENT |
| NECRAM | PUTRÉFACTION | Cap : 6 | VIRUS FATAL |
| GHOSTRA | RÉMANENCE | 3 LEURRES MAXIMUM | EXÉCUTION SPECTRALE |

## **DÉROULÉ D'UN TOUR**

1. Début de tour : effets passifs, ticks DoT, début de zone, vérification cooldown signature.

2. Phase active (15s) : le joueur dépense ses PA et PM. Ordre libre.

3. Si ressource au cap ET cooldown signature OK : le slot signature s'illumine — utilisable ce tour.

4. Fin de tour : application des effets de fin (marques posées, leurres décrémentés, etc.)

## **MATRICE DE MATCHUPS — INTENTION DE DESIGN**

*La V7.1 vise tous les matchups dans la fourchette 35/65 — 65/35. Aucun match joué d'avance, mais des AVANTAGES STRUCTURELS donnent du sens au draft.*

|   | vs SR | vs NS | vs CO | vs NE | vs GH |
|---|---|---|---|---|---|
| SOULRENDER | — | 55/45 | 60/40 | 45/55 | 65/35 |
| NIGHTSEER | 45/55 | — | 60/40 | 55/45 | 50/50 |
| COLOSSAR | 40/60 | 40/60 | — | 55/45 | 55/45 |
| NECRAM | 55/45 | 45/55 | 45/55 | — | 60/40 |
| GHOSTRA | 35/65 | 50/50 | 45/55 | 40/60 | — |

*Lecture : la pression Soulrender domine le Colossar lent (60/40). Le DoT Necram passe à travers la réduction Colossar (55/45). La Ghostra fragile craque devant le bleed Soulrender (35/65). Ces écarts sont VOLONTAIRES.*

# **SOULRENDER**

*LE BERSERKER SANGUINAIRE*

### **FANTASY DE GAMEPLAY**

*Un prédateur en hémorragie permanente qui RACCOURCIT le match. Le Soulrender ne survit pas à un long combat, il refuse qu'il en existe un. Chaque tour qui passe, il pousse son adversaire un peu plus près d'un précipice. Il transforme l'arène en boucherie — pas en duel.*

**ÉMOTION RESSENTIE PAR L'ADVERSAIRE**

*Panique. L'adversaire ne joue pas SON tempo, il joue contre une horloge biologique qui tambourine.*

### **RESSOURCE — HÉMOGLYPHE**

**Cap : 5**

- +1 HG quand le Soulrender INFLIGE des dégâts (max 1 par sort, donc max 1 par usage de sort).

- +1 HG quand le Soulrender SUBIT des dégâts (max 1 fois par tour adverse, peu importe le nombre de coups).

- Les HG ne se perdent jamais entre les tours — uniquement consommés par les Glyphes de Sang.

- Hémoglyphe est lisible : aura rouge progressive, puis fissures écarlates dans la peau du sprite à 5/5.

*POURQUOI : Cette ressource bilatérale (subir = générer) résout le problème classique du berserker : il n'a plus besoin de "tanker pour rien". Quand il prend des dégâts, il s'arme. L'adversaire est forcé à un dilemme constant : "Si je le frappe, il monte sa rampe. Si je ne le frappe pas, il me la met dessus."*

### **PASSIF — L'APPEL DU SANG**

*Pas de paliers fixes. Montée NON-LINÉAIRE indexée sur les seuils HP de la cible.*

- Cible >70% HP : aucun bonus. Le Soulrender chasse en silence.

- Cible <70% HP : MARQUAGE. La silhouette ennemie est cernée d'un halo rouge. Tous les sorts du Soulrender coûtent -1 PA (min 1).

- Cible <40% HP : RAGE OUVERTE. +1 PM permanent jusqu'à fin du tour suivant la kill, et tous les sorts à portée 1 ignorent 50% des boucliers.

- Cible <20% HP : LE CRI. La case du Soulrender et toutes les cases adjacentes deviennent du Sang Coagulé pendant 2 tours (30 dégâts par début de tour à tout ennemi présent).

*POURQUOI : Le passif punit le stalling. Plus l'adversaire ralentit, plus il s'enfonce. Plus il ouvre ses defenses, plus il s'expose. Le Soulrender devient le scenario du film d'horreur : on n'arrête pas la créature, on retarde l'inévitable.*

### **COMMENT LA CLASSE TRANSFORME LE COMBAT**

Le Soulrender ne joue pas la map, il l'INFECTE. Toutes ses cases foulées au cours d'un tour où il a infligé des dégâts deviennent imprégnées de Vapeur Carmin pendant 1 tour : un ennemi qui les traverse perd 1 PM. Plus le match avance, plus la grille est barrée. À 5/5 HG, le Soulrender devient injoignable car la zone autour de lui est un tapis de cases ralentissantes. Il n'enferme pas comme le Colossar — il pourrit l'espace.

### **FORCES & FAIBLESSES ASSUMÉES**

**FORCES**

- Pression brutale dès le tour 1 (pas de setup, pas de phase d'attente)

- Snowball monstrueux contre tout adversaire qui sustain

- Incassable en mid-fight : plus on lui tape dessus, plus il devient dangereux

- Sort signature réutilisable plusieurs fois en match si HG remonte — finisher répété possible

**FAIBLESSES**

- Aucun outil de range au-delà de 4 cases — kite-able si l'adversaire a la map

- Aucun heal réel sans HG — chaque erreur défensive coûte un tempo entier

- Vulnérable aux invuls, téléportations longues, et control PM

- Fragile contre les classes qui dictent la distance (Nightseer, Ghostra qui kite)

### **STYLE DE PRESSION**

LINÉAIRE et FRONTALE. Le Soulrender ne fait pas semblant. À chaque tour, il pose la même question : "Tu fais quoi ?". Sa pression vient de la prévisibilité de sa direction (il avance, il frappe) couplée à l'imprévisibilité de quel sort va sortir. C'est une pression de versus fighting : tu sais qu'il va attaquer, tu ne sais pas COMMENT.

### **STYLE DE COMBO**

Le combo Soulrender s'appelle un GLYPHE DE SANG. Il consomme des HG pour transformer un sort offensif en version augmentée. Chaque Glyphe est un PAYOFF — il déstructure le tour adverse plutôt que d'ajouter des dégâts plats. Les Glyphes ne sont pas des combos de spells enchaînés mécaniquement — ce sont des décisions de timing. Quand activer ? Quand sacrifier des HG pour un sort à 200 dégâts ou attendre les 5 HG pour Âme Lacérée ?

### **GESTION DU TERRAIN**

Vapeur Carmin (-1 PM aux ennemis qui traversent, 1 tour) + Sang Coagulé (zones générées par le passif sous 20% HP qui infligent 30 dégâts par début de tour à tout ennemi dessus). Le Soulrender ne pose pas de murs — il rend la map collante.

### **GESTION DU TEMPO**

AGRESSIF MAIS PATIENT. Tour 1-2 : positionnement et premier coup. Tour 3-4 : la cible passe sous 70%, le Soulrender entre en chasse. Tour 5+ : il termine. Aucun match Soulrender ne devrait dépasser 7 tours. S'il en arrive là, il a perdu — sauf si Âme Lacérée a remonté 2 fois.

## **SORT SIGNATURE — ÂME LACÉRÉE**

*Sort débloqué automatiquement quand la ressource atteint son cap. Cooldown 4 tours après usage. Slot séparé du deck de 6.*

| ÂME LACÉRÉE | ÂME LACÉRÉE | ÂME LACÉRÉE |
|---|---|---|
| PA | 2 | PORTÉE  1 (mêlée) |
| TYPE | Sort signature | COÛT RESS.  Coûte 5/5 HG (consomme toute la jauge) |
| EFFET | Inflige 320 dégâts. Le Soulrender se soigne de 50% des dégâts qui ont passé (après bouclier). Si la cible meurt sur ce sort : le combat est marqué d'une explosion de sang qui crée du Sang Coagulé en croix 5 cases. | Inflige 320 dégâts. Le Soulrender se soigne de 50% des dégâts qui ont passé (après bouclier). Si la cible meurt sur ce sort : le combat est marqué d'une explosion de sang qui crée du Sang Coagulé en croix 5 cases. |
| PRESSION | L'exécution rituelle. Âme Lacérée n'est pas un simple finisher — c'est l'aboutissement d'un cycle. Le Soulrender a saigné, fait saigner, accumulé. Maintenant il récolte. La présence du sort à 5 HG dicte tout le mid-game adverse : esquiver = subir le bleed, rester = subir la lacération. | L'exécution rituelle. Âme Lacérée n'est pas un simple finisher — c'est l'aboutissement d'un cycle. Le Soulrender a saigné, fait saigner, accumulé. Maintenant il récolte. La présence du sort à 5 HG dicte tout le mid-game adverse : esquiver = subir le bleed, rester = subir la lacération. |

**COOLDOWN : Cooldown 4 tours après usage. Réutilisable si HG remonte à 5.**

## **SORTS OFFENSIFS — 5 sorts**

*Sorts dont la fonction primaire est d'infliger des dégâts. Le cœur du DPS. Choisissez 1 à 4 dans votre deck selon votre style.*

| TRANCHE-ÂME | TRANCHE-ÂME | TRANCHE-ÂME |
|---|---|---|
| PA | 3 | PORTÉE  1 (mêlée) |
| TYPE | Frappe de base | COÛT RESS.  — |
| EFFET | Inflige 220 dégâts. Si le coup tue, le Soulrender RECULE de 2 cases gratuitement (mouvement non-PM). Effet purement de mise en scène, mais bloque les contre-attaques zone post-kill. | Inflige 220 dégâts. Si le coup tue, le Soulrender RECULE de 2 cases gratuitement (mouvement non-PM). Effet purement de mise en scène, mais bloque les contre-attaques zone post-kill. |
| PRESSION | Le sort signature de base. Lent (3 PA), prévisible — et c'est ce qui le rend terrifiant. L'adversaire SAIT qu'il arrive. Il ne peut pas l'arrêter. | Le sort signature de base. Lent (3 PA), prévisible — et c'est ce qui le rend terrifiant. L'adversaire SAIT qu'il arrive. Il ne peut pas l'arrêter. |

| OUVRE-PLAIE | OUVRE-PLAIE | OUVRE-PLAIE |
|---|---|---|
| PA | 2 | PORTÉE  1 |
| TYPE | Frappe combo / Glyphe | COÛT RESS.  Optionnel : 1 HG → +120 dégâts et anti-heal |
| EFFET | Inflige 110 dégâts. SI 1 HG dépensé : 230 dégâts ET la cible ne peut pas se soigner ni recevoir de bouclier pendant 2 tours. | Inflige 110 dégâts. SI 1 HG dépensé : 230 dégâts ET la cible ne peut pas se soigner ni recevoir de bouclier pendant 2 tours. |
| PRESSION | L'anti-sustain. La simple existence de ce sort dans le deck Soulrender suffit à interdire à l'adversaire de poser un Carapace ou Soin Lourd sans préparation. C'est une menace persistante. | L'anti-sustain. La simple existence de ce sort dans le deck Soulrender suffit à interdire à l'adversaire de poser un Carapace ou Soin Lourd sans préparation. C'est une menace persistante. |

| CHARGE BRUTALE | CHARGE BRUTALE | CHARGE BRUTALE |
|---|---|---|
| PA | 4 | PORTÉE  5 ligne |
| TYPE | Initiation | COÛT RESS.  — |
| EFFET | Le Soulrender fonce en ligne droite jusqu'à la première unité ou case bloquante. Inflige 180 dégâts à la cible touchée. Toute case foulée pendant la charge devient Vapeur Carmin pendant 1 tour. | Le Soulrender fonce en ligne droite jusqu'à la première unité ou case bloquante. Inflige 180 dégâts à la cible touchée. Toute case foulée pendant la charge devient Vapeur Carmin pendant 1 tour. |
| PRESSION | Le bélier. Charge Brutale ne fait pas seulement entrer le Soulrender — elle CRÉE un couloir de pression qui restera après son passage. L'adversaire qui veut fuir devra repasser par la zone empoisonnée. | Le bélier. Charge Brutale ne fait pas seulement entrer le Soulrender — elle CRÉE un couloir de pression qui restera après son passage. L'adversaire qui veut fuir devra repasser par la zone empoisonnée. |

| DÉTONATION SANGLANTE | DÉTONATION SANGLANTE | DÉTONATION SANGLANTE |
|---|---|---|
| PA | 4 | PORTÉE  4 (AoE croix 3) |
| TYPE | Glyphe explosif | COÛT RESS.  Coût : tous les HG actuels (min 2) |
| EFFET | Centre AoE croix 3. Inflige 60 dégâts de base à toutes les cibles dans la zone, +40 par HG consommé. Avec 5 HG : 260 dégâts. Sang Coagulé créé sous le centre pendant 2 tours. ATTENTION : consommer 5 HG ici interdit Âme Lacérée et reset son cooldown. | Centre AoE croix 3. Inflige 60 dégâts de base à toutes les cibles dans la zone, +40 par HG consommé. Avec 5 HG : 260 dégâts. Sang Coagulé créé sous le centre pendant 2 tours. ATTENTION : consommer 5 HG ici interdit Âme Lacérée et reset son cooldown. |
| PRESSION | Le payoff total. Détoner 5 HG est un acte de FOI — le Soulrender renonce à son finisher pour un coup massif. Si la cible esquive, le match est perdu. Si elle ne peut pas, le match est gagné. | Le payoff total. Détoner 5 HG est un acte de FOI — le Soulrender renonce à son finisher pour un coup massif. Si la cible esquive, le match est perdu. Si elle ne peut pas, le match est gagné. |

| CURÉE | CURÉE | CURÉE |
|---|---|---|
| PA | 2 | PORTÉE  2 |
| TYPE | Finisher conditionnel | COÛT RESS.  Coût : 2 HG |
| EFFET | Inflige 150 dégâts. SI la cible meurt sur ce sort : le Soulrender heal 50% de ses HP max manquants ET récupère 4 PA pour le tour suivant. Si la cible NE MEURT PAS : le Soulrender perd 60 HP. | Inflige 150 dégâts. SI la cible meurt sur ce sort : le Soulrender heal 50% de ses HP max manquants ET récupère 4 PA pour le tour suivant. Si la cible NE MEURT PAS : le Soulrender perd 60 HP. |
| PRESSION | Le tout ou rien. Curée est une lecture pure : si tu calcules juste, le match s'enchaîne. Si tu calcules mal, tu donnes un tempo entier à l'adversaire. Aucun autre sort du jeu n'a une variance émotionnelle aussi haute. | Le tout ou rien. Curée est une lecture pure : si tu calcules juste, le match s'enchaîne. Si tu calcules mal, tu donnes un tempo entier à l'adversaire. Aucun autre sort du jeu n'a une variance émotionnelle aussi haute. |

## **SORTS TACTIQUES — 5 sorts**

*Sorts de setup, de contrôle, de manipulation. Pas ou peu de dégâts directs, mais ils dictent la grille et les décisions adverses. Choisissez 1 à 4 dans votre deck.*

| EMPOIGNADE | EMPOIGNADE | EMPOIGNADE |
|---|---|---|
| PA | 3 | PORTÉE  3 |
| TYPE | Engage / Mindgame | COÛT RESS.  — |
| EFFET | Tire la cible jusqu'à 1 case du Soulrender. La cible ne peut pas être téléportée par un de ses propres sorts au tour suivant. Pas de dégâts. | Tire la cible jusqu'à 1 case du Soulrender. La cible ne peut pas être téléportée par un de ses propres sorts au tour suivant. Pas de dégâts. |
| PRESSION | L'arrachement. Empoignade défait la map des classes-kite. Une Nightseer qui pensait son setup safe se retrouve au corps à corps, son Évanescence verrouillée. | L'arrachement. Empoignade défait la map des classes-kite. Une Nightseer qui pensait son setup safe se retrouve au corps à corps, son Évanescence verrouillée. |

| PACTE DE SANG | PACTE DE SANG | PACTE DE SANG |
|---|---|---|
| PA | 1 | PORTÉE  0 (self) |
| TYPE | Engagement / Risque | COÛT RESS.  — |
| EFFET | Le Soulrender s'inflige 80 dégâts à lui-même et gagne +3 HG immédiatement. Son prochain sort offensif ce tour gagne +50% de dégâts. UTILISABLE 1 FOIS PAR MATCH. | Le Soulrender s'inflige 80 dégâts à lui-même et gagne +3 HG immédiatement. Son prochain sort offensif ce tour gagne +50% de dégâts. UTILISABLE 1 FOIS PAR MATCH. |
| PRESSION | Le bouton clutch. Quand l'adversaire pense être safe, le Soulrender saigne lui-même pour ouvrir une fenêtre de burst. Décision à très haut risque. | Le bouton clutch. Quand l'adversaire pense être safe, le Soulrender saigne lui-même pour ouvrir une fenêtre de burst. Décision à très haut risque. |

| MARQUE DE CARNAGE | MARQUE DE CARNAGE | MARQUE DE CARNAGE |
|---|---|---|
| PA | 2 | PORTÉE  5 |
| TYPE | Setup / Pression | COÛT RESS.  — |
| EFFET | Marque la cible 3 tours. Pendant ce temps, tous les sorts du Soulrender sur cette cible génèrent +1 HG bonus. La marque est visible sur le sprite ennemi (croix de sang). | Marque la cible 3 tours. Pendant ce temps, tous les sorts du Soulrender sur cette cible génèrent +1 HG bonus. La marque est visible sur le sprite ennemi (croix de sang). |
| PRESSION | Le sceau. Marque de Carnage transforme une cible en machine à fabriquer de la ressource. Plus l'adversaire reçoit de coups, plus le Soulrender accélère. | Le sceau. Marque de Carnage transforme une cible en machine à fabriquer de la ressource. Plus l'adversaire reçoit de coups, plus le Soulrender accélère. |

| RUGISSEMENT | RUGISSEMENT | RUGISSEMENT |
|---|---|---|
| PA | 3 | PORTÉE  0 (AoE 3 autour) |
| TYPE | Pression psychologique | COÛT RESS.  — |
| EFFET | AoE rayon 3 autour du Soulrender. Toutes les cibles ennemies subissent -1 PM ET ne peuvent pas téléporter au tour suivant. Si une cible est sous 50% HP : -2 PM au lieu de -1. Pas de dégâts. | AoE rayon 3 autour du Soulrender. Toutes les cibles ennemies subissent -1 PM ET ne peuvent pas téléporter au tour suivant. Si une cible est sous 50% HP : -2 PM au lieu de -1. Pas de dégâts. |
| PRESSION | Le cri primal. Rugissement ne tue pas — il fige. Combiné à Charge Brutale derrière, c'est un piège géométrique : l'adversaire ne peut plus s'enfuir. Anti-Ghostra par excellence. | Le cri primal. Rugissement ne tue pas — il fige. Combiné à Charge Brutale derrière, c'est un piège géométrique : l'adversaire ne peut plus s'enfuir. Anti-Ghostra par excellence. |

| RAGE INSATIABLE | RAGE INSATIABLE | RAGE INSATIABLE |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Buff de tempo | COÛT RESS.  — |
| EFFET | Pendant 2 tours, chaque sort offensif lancé par le Soulrender regenère 1 PA (max 1 par tour). Les sorts coûtent 1 PA de plus pendant ces 2 tours. | Pendant 2 tours, chaque sort offensif lancé par le Soulrender regenère 1 PA (max 1 par tour). Les sorts coûtent 1 PA de plus pendant ces 2 tours. |
| PRESSION | Le cycle ouvert. Rage Insatiable est un investissement : on accepte de payer plus cher chaque sort, en échange d'un tempo qui ne s'arrête jamais. Une fois lancée, le Soulrender devient une machine. | Le cycle ouvert. Rage Insatiable est un investissement : on accepte de payer plus cher chaque sort, en échange d'un tempo qui ne s'arrête jamais. Une fois lancée, le Soulrender devient une machine. |

## **SORTS DE SURVIE — 5 sorts**

*Sorts de heal, bouclier, protection, panic-button. Choisissez 1 à 3 dans votre deck — trop d'outils défensifs et vous perdez en pression.*

| RIPOSTE CARMIN | RIPOSTE CARMIN | RIPOSTE CARMIN |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Bait défensif | COÛT RESS.  — |
| EFFET | Pendant 1 tour, toute attaque MÊLÉE subie par le Soulrender renvoie 100 dégâts à l'attaquant ET lui coûte 1 PM additionnel pour son prochain mouvement. Le Soulrender prend les dégâts normalement. | Pendant 1 tour, toute attaque MÊLÉE subie par le Soulrender renvoie 100 dégâts à l'attaquant ET lui coûte 1 PM additionnel pour son prochain mouvement. Le Soulrender prend les dégâts normalement. |
| PRESSION | Le piège du chasseur. Riposte Carmin n'est pas une défense — c'est une invitation. Elle dit à l'adversaire : 'Viens me frapper.' | Le piège du chasseur. Riposte Carmin n'est pas une défense — c'est une invitation. Elle dit à l'adversaire : 'Viens me frapper.' |

| CAUTÉRISATION | CAUTÉRISATION | CAUTÉRISATION |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Reset DoT | COÛT RESS.  — |
| EFFET | Retire instantanément tous les DoT actifs sur le Soulrender (poison, plaie ouverte, autres saignements ennemis). Le Soulrender se soigne de 60 HP par DoT retiré (min 60, max 180). | Retire instantanément tous les DoT actifs sur le Soulrender (poison, plaie ouverte, autres saignements ennemis). Le Soulrender se soigne de 60 HP par DoT retiré (min 60, max 180). |
| PRESSION | L'auto-cautérisation. Anti-Necram et anti-Ghostra. Quand le bleed devient trop dense, le Soulrender brûle ses propres plaies pour repartir. Décision de timing : retirer trop tôt = gâcher le heal. | L'auto-cautérisation. Anti-Necram et anti-Ghostra. Quand le bleed devient trop dense, le Soulrender brûle ses propres plaies pour repartir. Décision de timing : retirer trop tôt = gâcher le heal. |

| PEAU DE FER | PEAU DE FER | PEAU DE FER |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Bouclier | COÛT RESS.  — |
| EFFET | Le Soulrender gagne un BOUCLIER de 200 HP pendant 2 tours. Pendant la durée du bouclier, ses sorts à portée 1 (mêlée) gagnent +30 dégâts. Le bouclier se vide normalement aux dégâts subis. | Le Soulrender gagne un BOUCLIER de 200 HP pendant 2 tours. Pendant la durée du bouclier, ses sorts à portée 1 (mêlée) gagnent +30 dégâts. Le bouclier se vide normalement aux dégâts subis. |
| PRESSION | Le mur viandard. Peau de Fer ne fait pas que protéger — elle ENCOURAGE l'engagement. Le Soulrender plante les pieds et frappe plus fort. Anti-Colossar/Nightseer qui zone à distance. | Le mur viandard. Peau de Fer ne fait pas que protéger — elle ENCOURAGE l'engagement. Le Soulrender plante les pieds et frappe plus fort. Anti-Colossar/Nightseer qui zone à distance. |

| SÈVE VIVE | SÈVE VIVE | SÈVE VIVE |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Heal mineur | COÛT RESS.  Optionnel : 1 HG → +60 HP additionnels |
| EFFET | Le Soulrender se soigne de 100 HP. Avec 1 HG : 160 HP. Si le Soulrender saigne actuellement (DoT actif sur lui) : +50 HP additionnels. | Le Soulrender se soigne de 100 HP. Avec 1 HG : 160 HP. Si le Soulrender saigne actuellement (DoT actif sur lui) : +50 HP additionnels. |
| PRESSION | Le rapide. Sève Vive est le micro-heal qui maintient le Soulrender en vie sans qu'il quitte le combat. À 2 PA, il peut soigner ET frapper le même tour. | Le rapide. Sève Vive est le micro-heal qui maintient le Soulrender en vie sans qu'il quitte le combat. À 2 PA, il peut soigner ET frapper le même tour. |

| DERNIER SOUFFLE | DERNIER SOUFFLE | DERNIER SOUFFLE |
|---|---|---|
| PA | 4 | PORTÉE  0 (self) |
| TYPE | Panic button conditionnel | COÛT RESS.  — |
| EFFET | Utilisable uniquement à <30% HP. Le Soulrender se soigne de 200 HP ET gagne 3 HG. UTILISABLE 1 FOIS PAR MATCH. | Utilisable uniquement à <30% HP. Le Soulrender se soigne de 200 HP ET gagne 3 HG. UTILISABLE 1 FOIS PAR MATCH. |
| PRESSION | L'ultime. Dernier Souffle n'est pas un heal — c'est une renaissance. Le Soulrender qui aurait dû mourir au tour 5 revient à 50% HP avec 3 HG en main, prêt pour un Âme Lacérée. Game-changer absolu. | L'ultime. Dernier Souffle n'est pas un heal — c'est une renaissance. Le Soulrender qui aurait dû mourir au tour 5 revient à 50% HP avec 3 HG en main, prêt pour un Âme Lacérée. Game-changer absolu. |

# **NIGHTSEER**

*LE PRÉDATEUR MANIPULATEUR*

### **FANTASY DE GAMEPLAY**

*Le Nightseer ne tue pas — il CONDUIT. Il guide l'adversaire vers une mort que l'adversaire choisit lui-même, en lui donnant une fausse impression de contrôle. C'est un piégeur, mais surtout un manipulateur d'INFORMATION. Le tableau de jeu n'est pas le même pour les deux joueurs.*

**ÉMOTION RESSENTIE PAR L'ADVERSAIRE**

*Paranoïa. L'adversaire commence à douter de tout : ses propres déplacements, son propre deck, ses propres yeux.*

### **RESSOURCE — PRESCIENCE**

**Cap : 4**

- +1 PR à chaque tour où le Nightseer N'A PAS pris de dégâts directs.

- +1 PR à chaque déclenchement de Marque (Traqué, Voilé, Empreinté).

- Si le Nightseer prend des dégâts directs un tour, il perd 1 PR (min 0).

- PR est INVISIBLE pour l'adversaire. Il n'apparaît pas en HUD ennemi.

*POURQUOI : Récompense la lecture et l'évasion. La PR matérialise la pensée du Nightseer : "j'ai tout vu venir, j'ai pris zéro dégât, je sais ce que tu vas faire". L'invisibilité côté adversaire crée un fog mental — l'ennemi ne sait jamais quel sort va frapper.*

### **PASSIF — L'ŒIL QUI N'EST PAS**

*Au lieu d'un palier de jauges, le Nightseer pose des MARQUES sur la grille et sur l'adversaire. Sa puissance dépend du nombre de marques actives.*

- MARQUE TRAQUÉ (sur unité) : la cible apparaît visuellement plus grande sur l'écran du Nightseer (lecture facilitée). Tous les sorts du Nightseer sur cette cible ignorent 30% des boucliers.

- MARQUE VOILÉ (sur case) : la case devient invisible pour l'adversaire (il voit du brouillard) jusqu'à ce qu'une unité y entre. Lisible pour le Nightseer.

- MARQUE EMPREINTÉ (sur unité) : à chaque déplacement, la cible laisse un sillage visible 1 tour pour le Nightseer uniquement.

- AUCUNE marque ne s'empile. Une cible ne peut avoir qu'1 marque active. Mais les marques ENTRE ELLES interagissent via les sorts.

*POURQUOI : Le Nightseer n'a pas de "stage 3 enragé". Il a un état d'INFORMATION. Plus il dépose de marques, plus il prend l'avantage cognitif. Le combat est asymétrique au sens littéral : les deux joueurs ne voient pas la même map.*

### **COMMENT LA CLASSE TRANSFORME LE COMBAT**

Le Nightseer instaure un FOG OF WAR mobile. Avec Marque Voilé + Pas Furtif, certaines zones de la grille deviennent illisibles pour l'adversaire. Le Nightseer transforme un tactique-info-complète en un tactique-info-partielle. À 4/4 PR avec 2 cases voilées et 1 cible empreintée, l'adversaire ne voit plus 20% de la grille et ne sait plus où le Nightseer va frapper.

### **FORCES & FAIBLESSES ASSUMÉES**

**FORCES**

- Information warfare absolue — l'adversaire est aveugle dans certaines zones

- Setup de range énorme (5-7 cases sur certains sorts)

- Dictée du tempo : décide quand engager

- Mindgames brutaux — le simple fait de jouer un sort change la lecture du tour suivant

**FAIBLESSES**

- Mêlée = mort. À 1 case, le Nightseer perd presque tous ses outils

- Setup-dépendant : un Nightseer qui se prend un Empoignade tour 1 commence à -2

- Pas de heal sustain — vulnérable aux DoT (Soulrender bleed, Necram poison)

- Skill ceiling très haut : un Nightseer mal joué donne juste de la PR à l'adversaire

### **STYLE DE PRESSION**

INDIRECTE et DIFFÉRÉE. Le Nightseer ne menace pas un coup, il menace une carte mentale. Il pose une Marque Voilé et l'adversaire passe le reste du match à se demander ce qu'il y a dedans. Sa pression est psychologique.

### **STYLE DE COMBO**

LE COMBO NIGHTSEER S'APPELLE UN PROTOCOLE. Chaque protocole exploite une combinaison de marques. Empreinté + Tir Précis = on voit où la cible va et on tire en avance. Voilé + Détonation Onirique = explosion sur une case que l'adversaire ne voit pas. Traqué + Salve Mortelle = exécution propre. Le Nightseer ne fait pas de "rotation" — il fait de l'INTERCEPTION.

### **GESTION DU TERRAIN**

Cases Voilées (invisibles côté adversaire). Cases Bourrasque (les sorts du Nightseer peuvent recréer des courants d'air qui repoussent les unités). Pas de murs. Pas de blocage dur. Le Nightseer manipule l'INFO, pas la géométrie.

### **GESTION DU TEMPO**

LENT, MÉTHODIQUE, EXPLOSIF. Tour 1-3 : pose de marques, jauge PR. Tour 4-6 : bursts précis sur cibles vulnérables. Tour 7+ : si la partie traîne, le Nightseer a déjà 4 PR et la map en feu — il finit. La force du Nightseer est dans le CONTROL DU TEMPS.

## **SORT SIGNATURE — TRAQUENARD**

*Sort débloqué automatiquement quand la ressource atteint son cap. Cooldown 4 tours après usage. Slot séparé du deck de 6.*

| TRAQUENARD | TRAQUENARD | TRAQUENARD |
|---|---|---|
| PA | 2 | PORTÉE  5 |
| TYPE | Sort signature | COÛT RESS.  Coûte 4/4 PR (consomme toute la jauge) |
| EFFET | Le Nightseer se téléporte à 1 case de la cible (côté libre, choix du joueur). Inflige 280 dégâts. Applique PARALYSIE (-3 PM, -2 PA) au prochain tour de la cible. Si la cible était Traqué/Voilé/Empreinté avant le sort : +80 dégâts ET la marque est consommée pour générer 2 PR au Nightseer après le coup. | Le Nightseer se téléporte à 1 case de la cible (côté libre, choix du joueur). Inflige 280 dégâts. Applique PARALYSIE (-3 PM, -2 PA) au prochain tour de la cible. Si la cible était Traqué/Voilé/Empreinté avant le sort : +80 dégâts ET la marque est consommée pour générer 2 PR au Nightseer après le coup. |
| PRESSION | L'embuscade pure. Traquenard n'est pas un finisher de DPS — c'est l'aboutissement d'un piège mental. Le Nightseer a passé 3 tours à poser des marques. Maintenant l'adversaire les paie. La paralysie verrouille le tour adverse, le Nightseer peut décrocher ou enchaîner. | L'embuscade pure. Traquenard n'est pas un finisher de DPS — c'est l'aboutissement d'un piège mental. Le Nightseer a passé 3 tours à poser des marques. Maintenant l'adversaire les paie. La paralysie verrouille le tour adverse, le Nightseer peut décrocher ou enchaîner. |

**COOLDOWN : Cooldown 4 tours après usage. Réutilisable si PR remonte à 4.**

## **SORTS OFFENSIFS — 5 sorts**

*Sorts dont la fonction primaire est d'infliger des dégâts. Le cœur du DPS. Choisissez 1 à 4 dans votre deck selon votre style.*

| TIR PRÉCIS | TIR PRÉCIS | TIR PRÉCIS |
|---|---|---|
| PA | 3 | PORTÉE  6 |
| TYPE | Frappe de précision | COÛT RESS.  — |
| EFFET | Inflige 200 dégâts. Si la cible est Traqué : 280 dégâts ET le Nightseer regagne 1 PR. | Inflige 200 dégâts. Si la cible est Traqué : 280 dégâts ET le Nightseer regagne 1 PR. |
| PRESSION | Le sniper. Tir Précis n'a pas besoin de surprendre — sa simple existence à 6 cases force l'adversaire à toujours regarder en l'air. Sort qui occupe de l'espace mental. | Le sniper. Tir Précis n'a pas besoin de surprendre — sa simple existence à 6 cases force l'adversaire à toujours regarder en l'air. Sort qui occupe de l'espace mental. |

| VOLÉE D'ÉPINES | VOLÉE D'ÉPINES | VOLÉE D'ÉPINES |
|---|---|---|
| PA | 4 | PORTÉE  5 ligne |
| TYPE | Frappe ligne / Setup | COÛT RESS.  — |
| EFFET | Tir en ligne droite. Inflige 130 dégâts à toutes les cibles touchées. Pose un Filet de Ronces (50 dégâts, -1 PM) sur la dernière case touchée. | Tir en ligne droite. Inflige 130 dégâts à toutes les cibles touchées. Pose un Filet de Ronces (50 dégâts, -1 PM) sur la dernière case touchée. |
| PRESSION | Le double effet. Volée d'Épines fait des dégâts ET pose un piège. L'adversaire qui survit doit décider : foncer dans le filet ou contourner et perdre du tempo. | Le double effet. Volée d'Épines fait des dégâts ET pose un piège. L'adversaire qui survit doit décider : foncer dans le filet ou contourner et perdre du tempo. |

| DÉTONATION ONIRIQUE | DÉTONATION ONIRIQUE | DÉTONATION ONIRIQUE |
|---|---|---|
| PA | 4 | PORTÉE  5 (AoE 2x2) |
| TYPE | Burst zone | COÛT RESS.  Optionnel : 2 PR → portée x2 ET déchire les cases Voilées |
| EFFET | AoE 2x2 cases. 170 dégâts dans la zone. Si une case Voilé existe dans la zone, elle se déchire et inflige 80 dégâts supplémentaires. Avec 2 PR : portée passe à 10, peut frapper depuis l'autre côté de la map. | AoE 2x2 cases. 170 dégâts dans la zone. Si une case Voilé existe dans la zone, elle se déchire et inflige 80 dégâts supplémentaires. Avec 2 PR : portée passe à 10, peut frapper depuis l'autre côté de la map. |
| PRESSION | L'œil qui frappe à travers le brouillard. Détonation Onirique punit la lecture. Si l'adversaire pensait être hors de portée, il ne l'était pas — le Nightseer voyait à travers. | L'œil qui frappe à travers le brouillard. Détonation Onirique punit la lecture. Si l'adversaire pensait être hors de portée, il ne l'était pas — le Nightseer voyait à travers. |

| FRAPPE DE L'OMBRE | FRAPPE DE L'OMBRE | FRAPPE DE L'OMBRE |
|---|---|---|
| PA | 4 | PORTÉE  3 |
| TYPE | Finisher conditionnel | COÛT RESS.  — |
| EFFET | Inflige 200 dégâts. Si la cible a moins de 50% de ses PM max actuels (donc s'est déjà déplacée) : +100 dégâts ET applique EMPREINTÉ pour 2 tours. | Inflige 200 dégâts. Si la cible a moins de 50% de ses PM max actuels (donc s'est déjà déplacée) : +100 dégâts ET applique EMPREINTÉ pour 2 tours. |
| PRESSION | L'archer immobile. Frappe de l'Ombre punit le mouvement. Les classes qui sprintent (Ghostra, Soulrender qui charge) se font shred. Force l'adversaire à choisir entre fuir et exister. | L'archer immobile. Frappe de l'Ombre punit le mouvement. Les classes qui sprintent (Ghostra, Soulrender qui charge) se font shred. Force l'adversaire à choisir entre fuir et exister. |

| SALVE MORTELLE | SALVE MORTELLE | SALVE MORTELLE |
|---|---|---|
| PA | 5 | PORTÉE  6 (AoE croix 5) |
| TYPE | Burst final | COÛT RESS.  Coût : 3 PR |
| EFFET | Cible une case. Centre + 4 cases adjacentes en croix : 220 dégâts au centre, 130 sur les côtés. Toute cible Traqué dans la zone : +60 dégâts. Toute case Voilé dans la zone : déchirée, dévoilée, +50 dégâts à qui s'y trouvait. ATTENTION : consommer 3 PR ici diffère le cooldown du Traquenard. | Cible une case. Centre + 4 cases adjacentes en croix : 220 dégâts au centre, 130 sur les côtés. Toute cible Traqué dans la zone : +60 dégâts. Toute case Voilé dans la zone : déchirée, dévoilée, +50 dégâts à qui s'y trouvait. ATTENTION : consommer 3 PR ici diffère le cooldown du Traquenard. |
| PRESSION | Le moment où la map révèle sa vérité. Salve Mortelle déchire toutes les illusions du Nightseer en même temps. Quand elle sort, soit le match finit, soit le Nightseer est ouvert pour le tour suivant. | Le moment où la map révèle sa vérité. Salve Mortelle déchire toutes les illusions du Nightseer en même temps. Quand elle sort, soit le match finit, soit le Nightseer est ouvert pour le tour suivant. |

## **SORTS TACTIQUES — 5 sorts**

*Sorts de setup, de contrôle, de manipulation. Pas ou peu de dégâts directs, mais ils dictent la grille et les décisions adverses. Choisissez 1 à 4 dans votre deck.*

| MARQUE DU CHASSEUR | MARQUE DU CHASSEUR | MARQUE DU CHASSEUR |
|---|---|---|
| PA | 1 | PORTÉE  5 |
| TYPE | Setup | COÛT RESS.  — |
| EFFET | Applique TRAQUÉ à la cible pendant 3 tours. Sort très peu cher car le payoff est dans les autres sorts. Coexiste avec Voilé/Empreinté sur d'autres cibles, pas sur la même. | Applique TRAQUÉ à la cible pendant 3 tours. Sort très peu cher car le payoff est dans les autres sorts. Coexiste avec Voilé/Empreinté sur d'autres cibles, pas sur la même. |
| PRESSION | L'oeil. Quand l'adversaire prend une Marque du Chasseur, il sait que les 3 prochains tours vont être violents. | L'oeil. Quand l'adversaire prend une Marque du Chasseur, il sait que les 3 prochains tours vont être violents. |

| FILET DE RONCES | FILET DE RONCES | FILET DE RONCES |
|---|---|---|
| PA | 2 | PORTÉE  4 |
| TYPE | Contrôle de zone | COÛT RESS.  — |
| EFFET | Pose une embûche invisible sur une case (Voilé pour l'adversaire). Toute unité ennemie qui entre : 100 dégâts, -2 PM, et applique EMPREINTÉ pour 2 tours. | Pose une embûche invisible sur une case (Voilé pour l'adversaire). Toute unité ennemie qui entre : 100 dégâts, -2 PM, et applique EMPREINTÉ pour 2 tours. |
| PRESSION | Le piège classique, mais ré-ingéniéré. Le Filet est lisible pour le Nightseer — il SAIT où il l'a posé. Il l'utilise pour pousser l'adversaire ailleurs, pas pour l'y faire marcher. | Le piège classique, mais ré-ingéniéré. Le Filet est lisible pour le Nightseer — il SAIT où il l'a posé. Il l'utilise pour pousser l'adversaire ailleurs, pas pour l'y faire marcher. |

| CHAMP DE MINES | CHAMP DE MINES | CHAMP DE MINES |
|---|---|---|
| PA | 4 | PORTÉE  3 (AoE 3x3) |
| TYPE | Zonage massif | COÛT RESS.  — |
| EFFET | Pose 3 embûches Voilées dans une zone 3x3 (placement aléatoire pour l'adversaire, choisi par le Nightseer). Chaque embûche : 70 dégâts + applique EMPREINTÉ. | Pose 3 embûches Voilées dans une zone 3x3 (placement aléatoire pour l'adversaire, choisi par le Nightseer). Chaque embûche : 70 dégâts + applique EMPREINTÉ. |
| PRESSION | Le terrain miné. Champ de Mines transforme une zone en no-go. L'adversaire doit faire un détour OU absorber 3 mines pour passer. Le Nightseer dicte la géométrie. | Le terrain miné. Champ de Mines transforme une zone en no-go. L'adversaire doit faire un détour OU absorber 3 mines pour passer. Le Nightseer dicte la géométrie. |

| BOURRASQUE | BOURRASQUE | BOURRASQUE |
|---|---|---|
| PA | 3 | PORTÉE  5 |
| TYPE | Push / Manipulation | COÛT RESS.  Optionnel : 1 PR → push 5 cases au lieu de 3 |
| EFFET | Pousse la cible 3 cases dans la direction choisie. Avec 1 PR : 5 cases. Si la cible finit sa course sur un Filet de Ronces, une mine, ou une case Sang Coagulé : effets déclenchés à pleine puissance. | Pousse la cible 3 cases dans la direction choisie. Avec 1 PR : 5 cases. Si la cible finit sa course sur un Filet de Ronces, une mine, ou une case Sang Coagulé : effets déclenchés à pleine puissance. |
| PRESSION | L'arme du conducteur. Bourrasque n'est pas une frappe — c'est un volant. Le Nightseer décide où l'adversaire VA, pas où il EST. | L'arme du conducteur. Bourrasque n'est pas une frappe — c'est un volant. Le Nightseer décide où l'adversaire VA, pas où il EST. |

| SOUFFLE GLACIAL | SOUFFLE GLACIAL | SOUFFLE GLACIAL |
|---|---|---|
| PA | 3 | PORTÉE  0 (AoE croix 3) |
| TYPE | Push défensif / AoE | COÛT RESS.  — |
| EFFET | AoE croix 3 cases autour du Nightseer. Inflige 70 dégâts + push 1 case + applique -1 PM aux cibles. Si une cible est poussée sur un Filet/mine : déclenchement. | AoE croix 3 cases autour du Nightseer. Inflige 70 dégâts + push 1 case + applique -1 PM aux cibles. Si une cible est poussée sur un Filet/mine : déclenchement. |
| PRESSION | Le décrochage. Souffle Glacial est l'outil anti-mêlée. Quand un Soulrender colle au Nightseer, ce sort le repousse ET le pousse potentiellement dans une mine voilée. Combo géométrique défensif. | Le décrochage. Souffle Glacial est l'outil anti-mêlée. Quand un Soulrender colle au Nightseer, ce sort le repousse ET le pousse potentiellement dans une mine voilée. Combo géométrique défensif. |

## **SORTS DE SURVIE — 5 sorts**

*Sorts de heal, bouclier, protection, panic-button. Choisissez 1 à 3 dans votre deck — trop d'outils défensifs et vous perdez en pression.*

| VOILE D'OMBRE | VOILE D'OMBRE | VOILE D'OMBRE |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Survie / Reset | COÛT RESS.  — |
| EFFET | Le Nightseer disparaît visuellement de l'écran adversaire pendant 1 tour entier. Il ne peut pas être ciblé directement (les AoE non-ciblées passent toujours). Si on devine sa case par AoE : effet normal. | Le Nightseer disparaît visuellement de l'écran adversaire pendant 1 tour entier. Il ne peut pas être ciblé directement (les AoE non-ciblées passent toujours). Si on devine sa case par AoE : effet normal. |
| PRESSION | Le grand silence. Voile d'Ombre est l'arme du décrochage. Quand le Soulrender le pourchasse, le Nightseer disparaît juste avant le finisher. | Le grand silence. Voile d'Ombre est l'arme du décrochage. Quand le Soulrender le pourchasse, le Nightseer disparaît juste avant le finisher. |

| PAS FURTIF | PAS FURTIF | PAS FURTIF |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Mobilité / Information | COÛT RESS.  Optionnel : 1 PR → pose Voilé sur la case quittée |
| EFFET | Téléporte le Nightseer jusqu'à 4 cases. Si 1 PR consommée, la case d'arrivée devient VOILÉE pour l'adversaire pendant 2 tours. | Téléporte le Nightseer jusqu'à 4 cases. Si 1 PR consommée, la case d'arrivée devient VOILÉE pour l'adversaire pendant 2 tours. |
| PRESSION | Le coup le plus frustrant pour l'adversaire. Le Nightseer disparaît littéralement. L'adversaire doit deviner où il est. | Le coup le plus frustrant pour l'adversaire. Le Nightseer disparaît littéralement. L'adversaire doit deviner où il est. |

| CAMOUFLAGE RONCES | CAMOUFLAGE RONCES | CAMOUFLAGE RONCES |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Bouclier piégé | COÛT RESS.  — |
| EFFET | Le Nightseer gagne un BOUCLIER de 130 HP pendant 2 tours. Pendant la durée, sa case est entourée d'un Filet de Ronces invisible : tout ennemi adjacent fin de tour subit 70 dégâts + EMPREINTÉ. | Le Nightseer gagne un BOUCLIER de 130 HP pendant 2 tours. Pendant la durée, sa case est entourée d'un Filet de Ronces invisible : tout ennemi adjacent fin de tour subit 70 dégâts + EMPREINTÉ. |
| PRESSION | L'épine défensive. Camouflage Ronces dit à l'adversaire : 'Approche-toi, vois ce qui se passe.' Anti-engage parfait contre Soulrender et Ghostra mêlée. | L'épine défensive. Camouflage Ronces dit à l'adversaire : 'Approche-toi, vois ce qui se passe.' Anti-engage parfait contre Soulrender et Ghostra mêlée. |

| SÈVE SAUVAGE | SÈVE SAUVAGE | SÈVE SAUVAGE |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Heal conditionnel | COÛT RESS.  — |
| EFFET | Le Nightseer se soigne de 130 HP. Si une de ses embûches a été déclenchée ce tour ou le tour précédent : +60 HP additionnels. Si une case Voilée existe actuellement sur la map : +30 HP. | Le Nightseer se soigne de 130 HP. Si une de ses embûches a été déclenchée ce tour ou le tour précédent : +60 HP additionnels. Si une case Voilée existe actuellement sur la map : +30 HP. |
| PRESSION | Le heal de récolte. Sève Sauvage récompense le Nightseer qui a déjà fait son setup. Plus la map est piégée, plus il survit. | Le heal de récolte. Sève Sauvage récompense le Nightseer qui a déjà fait son setup. Plus la map est piégée, plus il survit. |

| ÉVANESCENCE | ÉVANESCENCE | ÉVANESCENCE |
|---|---|---|
| PA | 4 | PORTÉE  0 (self) |
| TYPE | Panic button mobile | COÛT RESS.  — |
| EFFET | Utilisable uniquement à <30% HP. Le Nightseer se téléporte jusqu'à 7 cases ET se soigne de 150 HP. La case quittée devient Voilée pendant 2 tours. UTILISABLE 1 FOIS PAR MATCH. | Utilisable uniquement à <30% HP. Le Nightseer se téléporte jusqu'à 7 cases ET se soigne de 150 HP. La case quittée devient Voilée pendant 2 tours. UTILISABLE 1 FOIS PAR MATCH. |
| PRESSION | L'évasion totale. Évanescence permet au Nightseer de quitter complètement le combat le temps d'un tour. Mauvais pour le Soulrender qui le pourchasse — l'adversaire perd potentiellement 2-3 tours à chercher. | L'évasion totale. Évanescence permet au Nightseer de quitter complètement le combat le temps d'un tour. Mauvais pour le Soulrender qui le pourchasse — l'adversaire perd potentiellement 2-3 tours à chercher. |

# **COLOSSAR**

*LE COLOSSE OPPRESSANT*

### **FANTASY DE GAMEPLAY**

*Le Colossar ne veut pas tuer rapidement — il veut SUFFOQUER. Il prend la grille à deux mains et la rétrécit, tour après tour, jusqu'à ce que l'adversaire n'ait plus assez d'espace pour exister. Quand il frappe, c'est qu'il n'y avait littéralement plus de place pour fuir.*

**ÉMOTION RESSENTIE PAR L'ADVERSAIRE**

*Oppression. Enfermement. Le sentiment d'avoir une montagne qui marche dans ta direction.*

### **RESSOURCE — FONDATION**

**Cap : 3**

- +1 FD chaque fois que le Colossar pose un Pilier ou un Mur (sort qui crée un obstacle physique sur la grille).

- +1 FD chaque fois qu'un ennemi est PUSH/PULL contre un mur, un pilier, ou un bord de map.

- FD ne se gagne PAS en subissant des dégâts (volontairement asymétrique au passif Soulrender).

- À 3/3 FD, le Colossar peut DÉCLENCHER l'EFFONDREMENT (sort signature, voir ci-dessous).

*POURQUOI : La FD récompense l'INGÉNIERIE de la map. Le Colossar ne fait pas de dégâts pour générer sa ressource — il construit. Cette asymétrie volontaire force le Colossar à jouer un autre jeu : celui du sculpteur du champ de bataille.*

### **PASSIF — DENSITÉ INERTE**

*État continu lié au nombre d'obstacles posés. Pas de palier de jauge — un système de bénéfice progressif.*

- Pour chaque Pilier OU segment de Mur actuellement actif sur la map appartenant au Colossar : -8% dégâts subis (cap -24%).

- Pour chaque Pilier détruit pendant le match : le Colossar regagne instantanément 30 HP.

- Quand le Colossar est adjacent à un de ses Piliers/Murs : ses sorts à portée 1-2 gagnent +20 dégâts.

- L'EFFONDREMENT (signature) est le moment de bascule — voir ci-dessous.

*POURQUOI : Le passif récompense la PERSISTANCE des obstacles. Le Colossar ne gagne pas en frappant — il gagne en CONSTRUISANT et en RESTANT près de ce qu'il a construit. Posture immobile, presence fortifiée.*

### **COMMENT LA CLASSE TRANSFORME LE COMBAT**

Le Colossar est la SEULE classe qui modifie physiquement la grille de manière permanente. Ses Piliers (cubes de pierre infranchissables, 200 HP, peuvent être détruits) restent toute la partie sauf si abattus. Ses Murs créent des couloirs. Une partie Colossar de 6 tours laisse une arène différente : couloirs étroits, lignes de vue cassées, points d'engagement forcés.

### **FORCES & FAIBLESSES ASSUMÉES**

**FORCES**

- Map control absolu — il décide où le combat a lieu

- Tankisé naturel via les Piliers (réduction continue jusqu'à -24%)

- Sa position est presque jamais exploitable car il s'enferme avec ses propres murs

- Effondrement = un game-changer fondamental, contre lequel beaucoup de kits n'ont pas de réponse

**FAIBLESSES**

- Lent. Le Colossar a 2 PM de base (-1 vs autres classes)

- Pas de finisher rapide — un Colossar contre une cible >50% HP doit construire son chemin

- Vulnérable aux classes qui ignorent les obstacles (Ghostra téléport, Nightseer Voile)

- Vulnérable au DoT venin du Necram (ignore la réduction de dégâts)

### **STYLE DE PRESSION**

STATIQUE et CIRCULAIRE. Le Colossar ne court pas après l'adversaire — il l'EMMURE. Sa pression vient du fait que chaque tour, l'adversaire a moins d'espace. Au tour 5, il n'y a peut-être plus que 8 cases libres sur 60. C'est une pression cartographique.

### **STYLE DE COMBO**

Le combo Colossar s'appelle un VERROU. Il combine un déplacement forcé (push/pull) avec un Pilier ou un mur fraîchement posé. Onde de Choc push 2 cases + Pilier posé pile derrière la cible = la cible est ÉPINGLÉE. Tous les sorts du Colossar deviennent du burst sur cible immobile. Le Verrou n'est pas un combo de sorts — c'est un combo de GÉOMÉTRIE.

### **GESTION DU TERRAIN**

Piliers (200 HP, infranchissables, 1 case), Murs (segment 3 cases, 2 tours, infranchissables), Failles (cases qui apparaissent après l'Effondrement, 100 dégâts à qui marche dessus pendant 2 tours).

### **GESTION DU TEMPO**

LENT mais INEVITABLE. Tour 1-2 : positionnement, premier Pilier. Tour 3-4 : verrou installé, premier vrai dégât. Tour 5 : Effondrement annoncé. Tour 6 : Effondrement. Tour 7 : finition.

## **SORT SIGNATURE — EFFONDREMENT**

*Sort débloqué automatiquement quand la ressource atteint son cap. Cooldown 4 tours après usage. Slot séparé du deck de 6.*

| EFFONDREMENT | EFFONDREMENT | EFFONDREMENT |
|---|---|---|
| PA | 4 | PORTÉE  0 (AoE 2 autour) |
| TYPE | Sort signature | COÛT RESS.  Coûte 3/3 FD (consomme toute la jauge) |
| EFFET | ANNONCE 1 TOUR À L'AVANCE (le sol craque sous le Colossar à la fin du tour de cast — l'ennemi le voit). Au tour suivant : toutes les cases adjacentes au Colossar (rayon 2) deviennent IMPRATICABLES pendant 2 tours. Les ennemis dessus prennent 200 dégâts immédiats et sont éjectés vers la case libre la plus proche. Pendant les 2 tours d'Effondrement, le Colossar gagne +1 PM, ses sorts coûtent -1 PA, et toute attaque qu'il subit est réduite de 30%. À la fin de l'Effondrement, FD revient à 0. | ANNONCE 1 TOUR À L'AVANCE (le sol craque sous le Colossar à la fin du tour de cast — l'ennemi le voit). Au tour suivant : toutes les cases adjacentes au Colossar (rayon 2) deviennent IMPRATICABLES pendant 2 tours. Les ennemis dessus prennent 200 dégâts immédiats et sont éjectés vers la case libre la plus proche. Pendant les 2 tours d'Effondrement, le Colossar gagne +1 PM, ses sorts coûtent -1 PA, et toute attaque qu'il subit est réduite de 30%. À la fin de l'Effondrement, FD revient à 0. |
| PRESSION | L'arme tellurique. L'annonce 1 tour à l'avance crée un mindgame brutal : l'adversaire DOIT se repositionner ou prendre 200 dégâts. Pas de troisième option. Le Colossar dicte la prochaine demi-minute. La capacité à le réutiliser plusieurs fois en match (si FD remonte) en fait un game-changer cyclique. | L'arme tellurique. L'annonce 1 tour à l'avance crée un mindgame brutal : l'adversaire DOIT se repositionner ou prendre 200 dégâts. Pas de troisième option. Le Colossar dicte la prochaine demi-minute. La capacité à le réutiliser plusieurs fois en match (si FD remonte) en fait un game-changer cyclique. |

**COOLDOWN : Cooldown 4 tours après usage. Réutilisable si FD remonte à 3.**

## **SORTS OFFENSIFS — 5 sorts**

*Sorts dont la fonction primaire est d'infliger des dégâts. Le cœur du DPS. Choisissez 1 à 4 dans votre deck selon votre style.*

| FRAPPE LOURDE | FRAPPE LOURDE | FRAPPE LOURDE |
|---|---|---|
| PA | 3 | PORTÉE  1 (mêlée) |
| TYPE | Frappe de base | COÛT RESS.  — |
| EFFET | Inflige 180 dégâts. Si la cible est ÉPINGLÉE (adjacente à un Pilier, mur, ou bord de map du côté opposé au Colossar) : 280 dégâts. | Inflige 180 dégâts. Si la cible est ÉPINGLÉE (adjacente à un Pilier, mur, ou bord de map du côté opposé au Colossar) : 280 dégâts. |
| PRESSION | Le coup signature. La cible doit littéralement éviter d'avoir un mur derrière elle pour exister. Le Colossar transforme les bords de map en pièges. | Le coup signature. La cible doit littéralement éviter d'avoir un mur derrière elle pour exister. Le Colossar transforme les bords de map en pièges. |

| ONDE DE CHOC | ONDE DE CHOC | ONDE DE CHOC |
|---|---|---|
| PA | 3 | PORTÉE  2 (AoE 1 autour) |
| TYPE | Push / Verrou | COÛT RESS.  — |
| EFFET | AoE autour du Colossar. Inflige 80 dégâts à toutes les unités adjacentes ET les pousse de 2 cases. Si une unité est poussée contre un mur, Pilier, ou bord de map : 80 dégâts supplémentaires + APPLIQUE TRAUMA (-1 PM, -1 PA pendant 1 tour). | AoE autour du Colossar. Inflige 80 dégâts à toutes les unités adjacentes ET les pousse de 2 cases. Si une unité est poussée contre un mur, Pilier, ou bord de map : 80 dégâts supplémentaires + APPLIQUE TRAUMA (-1 PM, -1 PA pendant 1 tour). |
| PRESSION | Le sort qui transforme un Pilier en arme. Sans Onde, un Pilier est juste décoratif. Avec, c'est un mur sur lequel l'adversaire va s'écraser. | Le sort qui transforme un Pilier en arme. Sans Onde, un Pilier est juste décoratif. Avec, c'est un mur sur lequel l'adversaire va s'écraser. |

| MARTEAU PUNISSEUR | MARTEAU PUNISSEUR | MARTEAU PUNISSEUR |
|---|---|---|
| PA | 4 | PORTÉE  2 |
| TYPE | Anti-caster | COÛT RESS.  — |
| EFFET | Inflige 160 dégâts. Si la cible a moins de 4 PA actuels (donc a déjà cast ce tour) : 240 dégâts ET applique TRAUMA (-2 PA prochain tour). | Inflige 160 dégâts. Si la cible a moins de 4 PA actuels (donc a déjà cast ce tour) : 240 dégâts ET applique TRAUMA (-2 PA prochain tour). |
| PRESSION | L'anti-tempo. Marteau Punisseur punit les classes qui spam — Soulrender, Necram. Le Colossar dit : 'Tu as fini ton tour ? Tant mieux. Maintenant tu prends.' | L'anti-tempo. Marteau Punisseur punit les classes qui spam — Soulrender, Necram. Le Colossar dit : 'Tu as fini ton tour ? Tant mieux. Maintenant tu prends.' |

| CHOC SISMIQUE | CHOC SISMIQUE | CHOC SISMIQUE |
|---|---|---|
| PA | 4 | PORTÉE  4 ligne |
| TYPE | Frappe ligne | COÛT RESS.  — |
| EFFET | Frappe en ligne droite 4 cases. Inflige 130 dégâts à toutes les cibles touchées. Toutes les cibles touchées : -1 PM au prochain tour. Si une case Pilier ou Mur du Colossar se trouve sur la trajectoire : la frappe traverse, +50 dégâts à la cible suivante. | Frappe en ligne droite 4 cases. Inflige 130 dégâts à toutes les cibles touchées. Toutes les cibles touchées : -1 PM au prochain tour. Si une case Pilier ou Mur du Colossar se trouve sur la trajectoire : la frappe traverse, +50 dégâts à la cible suivante. |
| PRESSION | L'onde tellurique. Choc Sismique passe à travers ses propres murs comme un piston. Le Colossar tire à travers ses fortifications — il est le seul. | L'onde tellurique. Choc Sismique passe à travers ses propres murs comme un piston. Le Colossar tire à travers ses fortifications — il est le seul. |

| REPRÉSAILLES | REPRÉSAILLES | REPRÉSAILLES |
|---|---|---|
| PA | 3 | PORTÉE  1 (mêlée) |
| TYPE | Bait défensif | COÛT RESS.  — |
| EFFET | Inflige 100 dégâts immédiatement. Pendant 2 tours après le cast, chaque attaque mêlée subie par le Colossar renvoie 80 dégâts à l'attaquant. Cap à 4 retours. | Inflige 100 dégâts immédiatement. Pendant 2 tours après le cast, chaque attaque mêlée subie par le Colossar renvoie 80 dégâts à l'attaquant. Cap à 4 retours. |
| PRESSION | Le contre-engage. Représailles est posé AVANT le combat rapproché — c'est un engagement délibéré du Colossar pour dire à un Soulrender ou une Ghostra : 'Vas-y, viens.' | Le contre-engage. Représailles est posé AVANT le combat rapproché — c'est un engagement délibéré du Colossar pour dire à un Soulrender ou une Ghostra : 'Vas-y, viens.' |

## **SORTS TACTIQUES — 5 sorts**

*Sorts de setup, de contrôle, de manipulation. Pas ou peu de dégâts directs, mais ils dictent la grille et les décisions adverses. Choisissez 1 à 4 dans votre deck.*

| PILIER | PILIER | PILIER |
|---|---|---|
| PA | 3 | PORTÉE  3 (case vide) |
| TYPE | Construction | COÛT RESS.  — |
| EFFET | Pose un Pilier (200 HP, infranchissable, occupe 1 case) sur une case vide. Reste jusqu'à destruction. Le Colossar gagne +1 FD à la pose. Le Pilier bloque les lignes de vue et de tir des sorts directs. | Pose un Pilier (200 HP, infranchissable, occupe 1 case) sur une case vide. Reste jusqu'à destruction. Le Colossar gagne +1 FD à la pose. Le Pilier bloque les lignes de vue et de tir des sorts directs. |
| PRESSION | L'outil. À lui seul, Pilier ne menace personne. En combinaison avec push/pull, il devient un instrument de meurtre. | L'outil. À lui seul, Pilier ne menace personne. En combinaison avec push/pull, il devient un instrument de meurtre. |

| MUR DE PIERRE | MUR DE PIERRE | MUR DE PIERRE |
|---|---|---|
| PA | 4 | PORTÉE  4 |
| TYPE | Construction lourde | COÛT RESS.  Optionnel : 1 FD → mur de 5 cases au lieu de 3 |
| EFFET | Crée un mur infranchissable de 3 cases (en ligne) pendant 2 tours. Avec 1 FD : 5 cases. Le Mur bloque tout : déplacements, ciblages directs, lignes de tir. | Crée un mur infranchissable de 3 cases (en ligne) pendant 2 tours. Avec 1 FD : 5 cases. Le Mur bloque tout : déplacements, ciblages directs, lignes de tir. |
| PRESSION | Le grand séparateur. Un Mur bien posé peut couper la map en deux et forcer l'adversaire à choisir : il fait demi-tour ou il détruit le mur. | Le grand séparateur. Un Mur bien posé peut couper la map en deux et forcer l'adversaire à choisir : il fait demi-tour ou il détruit le mur. |

| ANCRAGE | ANCRAGE | ANCRAGE |
|---|---|---|
| PA | 2 | PORTÉE  4 |
| TYPE | Anti-mobilité | COÛT RESS.  — |
| EFFET | La cible perd 2 PM pendant 2 tours ET ne peut pas être déplacée par effets externes (push/pull/téléport) au prochain tour. Pas de dégâts. | La cible perd 2 PM pendant 2 tours ET ne peut pas être déplacée par effets externes (push/pull/téléport) au prochain tour. Pas de dégâts. |
| PRESSION | Le gel. Ancrage est l'anti-mobilité ultime. Une Ghostra ancrée ne peut plus se téléporter. C'est un sort qui DÉSACTIVE des kits entiers. | Le gel. Ancrage est l'anti-mobilité ultime. Une Ghostra ancrée ne peut plus se téléporter. C'est un sort qui DÉSACTIVE des kits entiers. |

| PROVOCATION | PROVOCATION | PROVOCATION |
|---|---|---|
| PA | 2 | PORTÉE  5 |
| TYPE | Contrôle mental | COÛT RESS.  — |
| EFFET | Force la cible à tenter d'attaquer le Colossar pendant 1 tour (ses sorts non-ciblant le Colossar coûtent +2 PA). La cible perd aussi 1 PM. Si la cible n'est pas adjacente au Colossar à la fin de son tour : 100 dégâts auto. | Force la cible à tenter d'attaquer le Colossar pendant 1 tour (ses sorts non-ciblant le Colossar coûtent +2 PA). La cible perd aussi 1 PM. Si la cible n'est pas adjacente au Colossar à la fin de son tour : 100 dégâts auto. |
| PRESSION | L'humiliation. Provocation force l'adversaire à venir au Colossar — qui l'attend avec Représailles posé. Le Colossar dicte les engagements directement par sort. | L'humiliation. Provocation force l'adversaire à venir au Colossar — qui l'attend avec Représailles posé. Le Colossar dicte les engagements directement par sort. |

| BRISURE | BRISURE | BRISURE |
|---|---|---|
| PA | 3 | PORTÉE  2 |
| TYPE | Anti-buff | COÛT RESS.  — |
| EFFET | Inflige 90 dégâts. Retire un buff/bouclier de la cible (au choix du joueur). Si la cible n'a pas de buff/bouclier : applique TRAUMA (-2 PA prochain tour). Si la cible avait Camouflage Ronces, Linceul d'Ombres, Carapace Visqueuse, Stoïcisme, Peau de Fer : le bouclier est entièrement retiré. | Inflige 90 dégâts. Retire un buff/bouclier de la cible (au choix du joueur). Si la cible n'a pas de buff/bouclier : applique TRAUMA (-2 PA prochain tour). Si la cible avait Camouflage Ronces, Linceul d'Ombres, Carapace Visqueuse, Stoïcisme, Peau de Fer : le bouclier est entièrement retiré. |
| PRESSION | Le briseur de mur. Brisure est l'anti-tank, l'anti-tortue. Aucune classe ne peut se reposer derrière un bouclier face au Colossar — il les casse explicitement. | Le briseur de mur. Brisure est l'anti-tank, l'anti-tortue. Aucune classe ne peut se reposer derrière un bouclier face au Colossar — il les casse explicitement. |

## **SORTS DE SURVIE — 5 sorts**

*Sorts de heal, bouclier, protection, panic-button. Choisissez 1 à 3 dans votre deck — trop d'outils défensifs et vous perdez en pression.*

| STOÏCISME | STOÏCISME | STOÏCISME |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Bouclier | COÛT RESS.  — |
| EFFET | Le Colossar gagne un BOUCLIER de 200 HP pour 2 tours. Pendant ces 2 tours, il ne peut PAS être déplacé (push/pull/téléport ennemi sans effet). Si le bouclier survit aux 2 tours sans être brisé, le Colossar récupère 80 HP. | Le Colossar gagne un BOUCLIER de 200 HP pour 2 tours. Pendant ces 2 tours, il ne peut PAS être déplacé (push/pull/téléport ennemi sans effet). Si le bouclier survit aux 2 tours sans être brisé, le Colossar récupère 80 HP. |
| PRESSION | Le rocher. Stoïcisme est le contraire d'un panic-button — c'est une déclaration. Le Colossar plante les pieds. | Le rocher. Stoïcisme est le contraire d'un panic-button — c'est une déclaration. Le Colossar plante les pieds. |

| GARDE PROTECTRICE | GARDE PROTECTRICE | GARDE PROTECTRICE |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Réduction continue | COÛT RESS.  — |
| EFFET | Pendant 2 tours, le Colossar subit -30% de dégâts de TOUTES les sources (sauf DoT venin Necram qui ignore les réductions). Ne se cumule pas avec le passif Densité Inerte au-delà du cap -50% total. | Pendant 2 tours, le Colossar subit -30% de dégâts de TOUTES les sources (sauf DoT venin Necram qui ignore les réductions). Ne se cumule pas avec le passif Densité Inerte au-delà du cap -50% total. |
| PRESSION | L'armure mobile. Garde Protectrice est le bouclier qui ne casse pas. Il n'a pas de HP — il a un timer. Le Colossar peut traverser une zone hostile sans se faire démolir. | L'armure mobile. Garde Protectrice est le bouclier qui ne casse pas. Il n'a pas de HP — il a un timer. Le Colossar peut traverser une zone hostile sans se faire démolir. |

| RESSAC VITAL | RESSAC VITAL | RESSAC VITAL |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Heal réactionnel | COÛT RESS.  — |
| EFFET | Le Colossar se soigne de 80 HP + 30 HP par attaque qu'il a subie au tour précédent (max +120 HP, donc cap 200 HP). | Le Colossar se soigne de 80 HP + 30 HP par attaque qu'il a subie au tour précédent (max +120 HP, donc cap 200 HP). |
| PRESSION | Le contre-tank. Ressac Vital récompense le Colossar qui s'est fait taper. Plus l'adversaire l'agresse, plus il se soigne. Anti-burst implacable. | Le contre-tank. Ressac Vital récompense le Colossar qui s'est fait taper. Plus l'adversaire l'agresse, plus il se soigne. Anti-burst implacable. |

| RENVOI DU BOUCLIER | RENVOI DU BOUCLIER | RENVOI DU BOUCLIER |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Bait à distance | COÛT RESS.  — |
| EFFET | Pendant 1 tour, toute attaque (mêlée OU à distance) subie par le Colossar renvoie 60 dégâts à l'attaquant. Cap à 4 retours. | Pendant 1 tour, toute attaque (mêlée OU à distance) subie par le Colossar renvoie 60 dégâts à l'attaquant. Cap à 4 retours. |
| PRESSION | Le miroir. Renvoi du Bouclier est l'anti-Nightseer — un sort à distance qui frappe le Colossar lui revient direct. Sort à utiliser quand on sent un setup adverse arriver. | Le miroir. Renvoi du Bouclier est l'anti-Nightseer — un sort à distance qui frappe le Colossar lui revient direct. Sort à utiliser quand on sent un setup adverse arriver. |

| SOIN LOURD | SOIN LOURD | SOIN LOURD |
|---|---|---|
| PA | 3 | PORTÉE  3 |
| TYPE | Heal cross-classe | COÛT RESS.  — |
| EFFET | Soigne 150 HP sur soi OU sur un allié à 3 cases. UNIQUE HEAL CROSS-CLASSE DU JEU. En 1v1 : self-only, agit comme un heal lourd à 3 PA. | Soigne 150 HP sur soi OU sur un allié à 3 cases. UNIQUE HEAL CROSS-CLASSE DU JEU. En 1v1 : self-only, agit comme un heal lourd à 3 PA. |
| PRESSION | Le seul vrai support du jeu. Inutile en 1v1 sauf comme heal classique, mais son existence définit le rôle du Colossar en team. En 2v2/3v3 c'est un game-changer. | Le seul vrai support du jeu. Inutile en 1v1 sauf comme heal classique, mais son existence définit le rôle du Colossar en team. En 2v2/3v3 c'est un game-changer. |

# **NECRAM**

*LE MAGE INFECTIEUX*

### **FANTASY DE GAMEPLAY**

*Le Necram ne combat pas — il INOCULE. Chaque sort qu'il lance est une condamnation à retardement. À mesure que le match avance, la map devient toxique, l'adversaire devient infesté, et la simple existence dans l'arène devient mortelle. Le Necram joue contre une horloge qu'il a installée lui-même.*

**ÉMOTION RESSENTIE PAR L'ADVERSAIRE**

*Urgence. Fatalité. Le sentiment d'être un cadavre qui marche encore.*

### **RESSOURCE — PUTRÉFACTION**

**Cap : 6**

- +1 PT à chaque tour où une unité ennemie subit du DoT venin (au tick).

- +1 PT chaque fois que le Necram applique une nouvelle marque (max 2 PT par tour Necram).

- Les PT ne se perdent jamais — elles s'accumulent jusqu'au cap.

- À 6/6 PT, le Necram peut déclencher VIRUS FATAL (sort signature, voir ci-dessous).

*POURQUOI : La PT punit le simple FAIT QUE LE TEMPS PASSE. Tant que le Necram a posé une marque, le tic du tour suivant lui rapporte. L'adversaire ne peut pas attendre — chaque tour qu'il prend pour respirer renforce le Necram.*

### **PASSIF — LA FLORAISON**

*Système de DENSITÉ TOXIQUE. Le passif suit le nombre de marques actives dans le match (toutes cibles confondues).*

- Densité 1-3 marques actives : ticks venin = 30 dégâts/tour. Le DoT venin IGNORE les boucliers (passe à travers).

- Densité 4-6 marques actives : ticks venin = 40 dégâts/tour. Le Necram regen 10 HP par marque active à chaque début de tour. Cases adjacentes au Necram (rayon 3) deviennent toxiques : 20 dégâts par début de tour aux ennemis.

- Densité 7+ marques actives : ticks venin = 50 dégâts/tour. Le Necram peut déclencher VIRUS FATAL (signature).

- IMPORTANT : le DoT venin ignore aussi la réduction de dégâts du passif Colossar — identité Necram vs tanks.

*POURQUOI : Le Necram ne joue pas un palier individuel — il joue une SCIENCE. Plus la map est infestée, plus le passif scale. Le système est NON-LINÉAIRE par essence.*

### **COMMENT LA CLASSE TRANSFORME LE COMBAT**

Le Necram transforme la map en ZONE DE MORT. Les marques au sol (Brume Toxique) restent visibles 2 tours. Au cap de densité, les cases autour du Necram dégénèrent. Une partie Necram de 6 tours laisse une grille où 30-40% des cases sont dangereuses à traverser.

### **FORCES & FAIBLESSES ASSUMÉES**

**FORCES**

- Pénétration totale — le DoT ignore boucliers, réductions, sustains

- Snowball massif tour 4+ avec densité élevée

- Domine les classes tank (Colossar) qui dépendent de la réduction

- Identité unique : le seul vrai DoT-mage compétitif du jeu

**FAIBLESSES**

- Tour 1-2 catastrophiques — le setup prend du temps, et un burst direct le tue

- Vulnérable aux classes qui retirent les marques (Soulrender Cautérisation, Ghostra Voile Spectral)

- Le pic d'efficacité se situe en mid-game — un match qui finit tour 3 est un match perdu

- Pas de mobilité réelle — il doit tenir sa position pour optimiser sa zone toxique

### **STYLE DE PRESSION**

DIFFUSE et OMNIPRÉSENTE. La pression Necram n'est pas une menace de coup — c'est un climat. À chaque seconde, l'adversaire perd des HP qu'il n'aurait jamais perdu autrement. Il ne peut pas heal au-dessus du tick.

### **STYLE DE COMBO**

Le combo Necram s'appelle une SOUCHE. Une Souche est une combinaison de marques + détonation qui transforme un setup lent en burst massif. 4 marques sur 1 cible + Détonation Virulente = 280 dégâts d'un coup. Inoculation + Contagion + Détonation = burst AoE qui clean tous les ennemis. Une Souche n'est pas un combo "rapide" — c'est un PIÈGE qui se referme après 3 tours de setup.

### **GESTION DU TERRAIN**

Brume Toxique (cases empoisonnées 2 tours, 25 dégâts par début de tour à qui s'y trouve), Spores (cases qui appliquent une marque à qui les traverse), Champ Putride (à densité 4+, halo de 3 cases autour du Necram).

### **GESTION DU TEMPO**

LENT-EXPLOSIF. Tour 1 : Inoculation, setup minimal. Tour 2-3 : application massive de marques, jauge PT. Tour 4 : Détonation ou Virus Fatal. Tour 5+ : finition. Si le Necram passe le tour 4 sans avoir pu déposer 4 marques, il a perdu.

## **SORT SIGNATURE — VIRUS FATAL**

*Sort débloqué automatiquement quand la ressource atteint son cap. Cooldown 4 tours après usage. Slot séparé du deck de 6.*

| VIRUS FATAL | VIRUS FATAL | VIRUS FATAL |
|---|---|---|
| PA | 2 | PORTÉE  5 |
| TYPE | Sort signature | COÛT RESS.  Coûte 6/6 PT (consomme toute la jauge) |
| EFFET | Cible une unité ennemie. TOUTES les marques sur la cible déclenchent leur tick instantanément X3 (multiplicateur Floraison appliqué). Une cible avec 4 marques de venin (50 dmg/tick × 4 × 3) prend 600 dégâts d'un coup. Les marques sont consommées. Si la cible meurt sur ce sort : les marques ne sont PAS consommées et restent disponibles pour Contagion ou Détonation Virulente sur d'autres cibles. | Cible une unité ennemie. TOUTES les marques sur la cible déclenchent leur tick instantanément X3 (multiplicateur Floraison appliqué). Une cible avec 4 marques de venin (50 dmg/tick × 4 × 3) prend 600 dégâts d'un coup. Les marques sont consommées. Si la cible meurt sur ce sort : les marques ne sont PAS consommées et restent disponibles pour Contagion ou Détonation Virulente sur d'autres cibles. |
| PRESSION | L'apoptose. Virus Fatal est l'aboutissement absolu de la stratégie Necram : 4-5 tours de setup transformés en 1 tour de mort lente accélérée. La cible voit son DoT s'effondrer sur elle. Le timing est tout : trop tôt = peu de marques, peu d'effet ; trop tard = la cible se soigne et le Necram a lent la jauge pour rien. | L'apoptose. Virus Fatal est l'aboutissement absolu de la stratégie Necram : 4-5 tours de setup transformés en 1 tour de mort lente accélérée. La cible voit son DoT s'effondrer sur elle. Le timing est tout : trop tôt = peu de marques, peu d'effet ; trop tard = la cible se soigne et le Necram a lent la jauge pour rien. |

**COOLDOWN : Cooldown 4 tours après usage. Réutilisable si PT remonte à 6.**

## **SORTS OFFENSIFS — 5 sorts**

*Sorts dont la fonction primaire est d'infliger des dégâts. Le cœur du DPS. Choisissez 1 à 4 dans votre deck selon votre style.*

| CRACHAT ACIDE | CRACHAT ACIDE | CRACHAT ACIDE |
|---|---|---|
| PA | 3 | PORTÉE  4 |
| TYPE | Frappe / Setup | COÛT RESS.  — |
| EFFET | Inflige 90 dégâts ET applique 2 marques de venin (au lieu de 1). Cap à 4 marques par cible. | Inflige 90 dégâts ET applique 2 marques de venin (au lieu de 1). Cap à 4 marques par cible. |
| PRESSION | Le sort de base, mais redoutable. Crachat Acide combine dégâts directs et setup en 1 PA-efficace. C'est l'arme à 80% de l'utilisation Necram en early. | Le sort de base, mais redoutable. Crachat Acide combine dégâts directs et setup en 1 PA-efficace. C'est l'arme à 80% de l'utilisation Necram en early. |

| MORSURE PUTRIDE | MORSURE PUTRIDE | MORSURE PUTRIDE |
|---|---|---|
| PA | 4 | PORTÉE  1 (mêlée) |
| TYPE | Finisher mêlée | COÛT RESS.  — |
| EFFET | Inflige 110 dégâts + 22 par marque sur la cible (max +90, donc 200 dégâts max). Si la cible meurt : toutes ses marques sont transférées sur l'unité ennemie la plus proche. | Inflige 110 dégâts + 22 par marque sur la cible (max +90, donc 200 dégâts max). Si la cible meurt : toutes ses marques sont transférées sur l'unité ennemie la plus proche. |
| PRESSION | L'embrasement. Morsure Putride est le finisher qui propage. Tuer une cible avec elle ne stoppe pas le DoT — elle migre. Anti-team, mais aussi outil pour cycler en 1v1. | L'embrasement. Morsure Putride est le finisher qui propage. Tuer une cible avec elle ne stoppe pas le DoT — elle migre. Anti-team, mais aussi outil pour cycler en 1v1. |

| BRUME TOXIQUE | BRUME TOXIQUE | BRUME TOXIQUE |
|---|---|---|
| PA | 4 | PORTÉE  4 (AoE 3x3) |
| TYPE | Zone DoT | COÛT RESS.  — |
| EFFET | Pose une zone toxique 3x3 pendant 2 tours. Toute unité dans la zone : 60 dégâts immédiats + 1 marque. Toute unité qui ENTRE : 30 dégâts + 1 marque. Toute unité qui FINIT son tour dans la zone : 1 marque additionnelle. | Pose une zone toxique 3x3 pendant 2 tours. Toute unité dans la zone : 60 dégâts immédiats + 1 marque. Toute unité qui ENTRE : 30 dégâts + 1 marque. Toute unité qui FINIT son tour dans la zone : 1 marque additionnelle. |
| PRESSION | L'air vicié. Brume Toxique ne tue pas — elle CONDAMNE. Les ennemis voient une zone et savent qu'ils ne peuvent pas y mettre les pieds. | L'air vicié. Brume Toxique ne tue pas — elle CONDAMNE. Les ennemis voient une zone et savent qu'ils ne peuvent pas y mettre les pieds. |

| DÉTONATION VIRULENTE | DÉTONATION VIRULENTE | DÉTONATION VIRULENTE |
|---|---|---|
| PA | 4 | PORTÉE  4 |
| TYPE | Burst conditionnel | COÛT RESS.  — |
| EFFET | Inflige 80 dégâts immédiats. Consomme TOUTES les marques sur la cible : chaque marque consommée inflige 50 dégâts. Avec 4 marques : 280 dégâts totaux. Les marques disparaissent. | Inflige 80 dégâts immédiats. Consomme TOUTES les marques sur la cible : chaque marque consommée inflige 50 dégâts. Avec 4 marques : 280 dégâts totaux. Les marques disparaissent. |
| PRESSION | Le détonateur. Détonation est le moment où le Necram récolte. Décision : 'Maintenant ou plus tard ?' Plus tard = plus de marques = plus de dégâts. Mais si la cible heal, payoff diminué. | Le détonateur. Détonation est le moment où le Necram récolte. Décision : 'Maintenant ou plus tard ?' Plus tard = plus de marques = plus de dégâts. Mais si la cible heal, payoff diminué. |

| FAUX DÉCHARNÉE | FAUX DÉCHARNÉE | FAUX DÉCHARNÉE |
|---|---|---|
| PA | 4 | PORTÉE  1 (mêlée AoE 1) |
| TYPE | AoE sustain | COÛT RESS.  — |
| EFFET | AoE 1 case (le Necram et ses 8 voisines). Inflige 130 dégâts. Le Necram se SOIGNE de 30 HP par marque active sur toutes les cibles touchées (cap +120 HP). | AoE 1 case (le Necram et ses 8 voisines). Inflige 130 dégâts. Le Necram se SOIGNE de 30 HP par marque active sur toutes les cibles touchées (cap +120 HP). |
| PRESSION | Le moment où le mage devient bête. La Faux est anti-Soulrender, anti-Ghostra : si tu te rapproches du Necram, il en profite pour se soigner sur ton dos. | Le moment où le mage devient bête. La Faux est anti-Soulrender, anti-Ghostra : si tu te rapproches du Necram, il en profite pour se soigner sur ton dos. |

## **SORTS TACTIQUES — 5 sorts**

*Sorts de setup, de contrôle, de manipulation. Pas ou peu de dégâts directs, mais ils dictent la grille et les décisions adverses. Choisissez 1 à 4 dans votre deck.*

| INOCULATION | INOCULATION | INOCULATION |
|---|---|---|
| PA | 1 | PORTÉE  5 |
| TYPE | Setup pur | COÛT RESS.  — |
| EFFET | Applique 2 marques de venin sur la cible (sans dégâts directs). Cap à 4 marques par cible. | Applique 2 marques de venin sur la cible (sans dégâts directs). Cap à 4 marques par cible. |
| PRESSION | Le baiser de la mort. Inoculation ne fait rien d'immédiat. L'adversaire qui prend 2 marques sait que les 3 prochains tours vont être un compte à rebours. La pression vient du SILENCE. | Le baiser de la mort. Inoculation ne fait rien d'immédiat. L'adversaire qui prend 2 marques sait que les 3 prochains tours vont être un compte à rebours. La pression vient du SILENCE. |

| CONTAGION | CONTAGION | CONTAGION |
|---|---|---|
| PA | 3 | PORTÉE  5 |
| TYPE | Propagation | COÛT RESS.  Optionnel : 2 PT → cap copié passe à 4 |
| EFFET | Cible une unité ENNEMIE marquée. Toutes ses marques (cap 3, ou 4 avec PT) sont COPIÉES sur les autres unités ennemies dans un rayon de 3 cases. En 1v1 : la cible reçoit un boost de tick (+1 marque dupliquée sur elle-même). | Cible une unité ENNEMIE marquée. Toutes ses marques (cap 3, ou 4 avec PT) sont COPIÉES sur les autres unités ennemies dans un rayon de 3 cases. En 1v1 : la cible reçoit un boost de tick (+1 marque dupliquée sur elle-même). |
| PRESSION | L'épidémie. En 2v2/3v3, Contagion est dévastateur. En 1v1, elle reste utilisable comme un boost de DoT sur la cible. | L'épidémie. En 2v2/3v3, Contagion est dévastateur. En 1v1, elle reste utilisable comme un boost de DoT sur la cible. |

| MARQUE SACRIFICIELLE | MARQUE SACRIFICIELLE | MARQUE SACRIFICIELLE |
|---|---|---|
| PA | 2 | PORTÉE  5 |
| TYPE | Buff DoT | COÛT RESS.  — |
| EFFET | Pendant 3 tours, les marques sur la cible infligent +20 dégâts par tick. La cible peut recevoir Marque Sacrificielle même si elle n'a pas encore de marques (mais sans marques actives, l'effet est neutre). | Pendant 3 tours, les marques sur la cible infligent +20 dégâts par tick. La cible peut recevoir Marque Sacrificielle même si elle n'a pas encore de marques (mais sans marques actives, l'effet est neutre). |
| PRESSION | L'engrais. Marque Sacrificielle force l'adversaire à se soigner CONSTAMMENT. Un tick à 70 dégâts ne pardonne aucun délai. | L'engrais. Marque Sacrificielle force l'adversaire à se soigner CONSTAMMENT. Un tick à 70 dégâts ne pardonne aucun délai. |

| PAS SPECTRAL | PAS SPECTRAL | PAS SPECTRAL |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Mobilité | COÛT RESS.  — |
| EFFET | Le Necram gagne +2 PM ce tour ET peut traverser les unités ennemies au prochain déplacement. Ses marques posées par traversée appliquent 1 marque bonus. | Le Necram gagne +2 PM ce tour ET peut traverser les unités ennemies au prochain déplacement. Ses marques posées par traversée appliquent 1 marque bonus. |
| PRESSION | Le passage du fantôme. Pas Spectral est l'unique vrai outil de mobilité du Necram. Il l'utilise pour se positionner dans la Brume ou s'extraire d'une mêlée. | Le passage du fantôme. Pas Spectral est l'unique vrai outil de mobilité du Necram. Il l'utilise pour se positionner dans la Brume ou s'extraire d'une mêlée. |

| SYMBIOSE MORBIDE | SYMBIOSE MORBIDE | SYMBIOSE MORBIDE |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Lifesteal DoT | COÛT RESS.  — |
| EFFET | Pendant 2 tours, chaque tick de venin sur les ennemis soigne le Necram de 8 HP. Cap à 4 marques actives qui comptent pour le heal (donc max +32 HP par tour, +64 sur 2 tours). | Pendant 2 tours, chaque tick de venin sur les ennemis soigne le Necram de 8 HP. Cap à 4 marques actives qui comptent pour le heal (donc max +32 HP par tour, +64 sur 2 tours). |
| PRESSION | Le parasite. Symbiose transforme le Necram en machine à régen. Plus il a de marques sur la map, plus il devient incassable. Anti-attrition par excellence. | Le parasite. Symbiose transforme le Necram en machine à régen. Plus il a de marques sur la map, plus il devient incassable. Anti-attrition par excellence. |

## **SORTS DE SURVIE — 5 sorts**

*Sorts de heal, bouclier, protection, panic-button. Choisissez 1 à 3 dans votre deck — trop d'outils défensifs et vous perdez en pression.*

| VOILE DE PESTILENCE | VOILE DE PESTILENCE | VOILE DE PESTILENCE |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Aura défensive | COÛT RESS.  — |
| EFFET | Pendant 2 tours, toute unité ennemie qui finit son tour à 2 cases ou moins du Necram reçoit 1 marque automatiquement. Pendant ces 2 tours, toute attaque mêlée subie par le Necram applique 1 marque à l'attaquant. | Pendant 2 tours, toute unité ennemie qui finit son tour à 2 cases ou moins du Necram reçoit 1 marque automatiquement. Pendant ces 2 tours, toute attaque mêlée subie par le Necram applique 1 marque à l'attaquant. |
| PRESSION | Le linceul. Voile de Pestilence punit l'adjacence. Une Ghostra qui se téléporte derrière le Necram pour un dorsal se retrouve marquée. | Le linceul. Voile de Pestilence punit l'adjacence. Une Ghostra qui se téléporte derrière le Necram pour un dorsal se retrouve marquée. |

| CARAPACE VISQUEUSE | CARAPACE VISQUEUSE | CARAPACE VISQUEUSE |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Bouclier piégé | COÛT RESS.  — |
| EFFET | Le Necram gagne un BOUCLIER de 110 HP pour 2 tours. Tout attaquant mêlée qui frappe le bouclier reçoit 1 marque automatiquement. | Le Necram gagne un BOUCLIER de 110 HP pour 2 tours. Tout attaquant mêlée qui frappe le bouclier reçoit 1 marque automatiquement. |
| PRESSION | L'épine pourrie. Carapace Visqueuse n'est pas un mur — c'est un piège défensif. Frapper le Necram en mêlée = signer son arrêt de mort. | L'épine pourrie. Carapace Visqueuse n'est pas un mur — c'est un piège défensif. Frapper le Necram en mêlée = signer son arrêt de mort. |

| DRAIN VITAL | DRAIN VITAL | DRAIN VITAL |
|---|---|---|
| PA | 3 | PORTÉE  4 |
| TYPE | Heal offensif | COÛT RESS.  — |
| EFFET | Inflige 60 dégâts à la cible. Le Necram se soigne de 30 HP, ou 60 HP si la cible a 3+ marques actives. | Inflige 60 dégâts à la cible. Le Necram se soigne de 30 HP, ou 60 HP si la cible a 3+ marques actives. |
| PRESSION | Le siphon. Drain Vital est le heal qui FAIT mal. Sustain économique anti-Soulrender qui presse trop. | Le siphon. Drain Vital est le heal qui FAIT mal. Sustain économique anti-Soulrender qui presse trop. |

| RÉGÉNÉRATION NÉCROTIQUE | RÉGÉNÉRATION NÉCROTIQUE | RÉGÉNÉRATION NÉCROTIQUE |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Heal mineur AoE | COÛT RESS.  Optionnel : 1 PT → +30 HP additionnels |
| EFFET | Le Necram se soigne de 70 HP + 15 HP par marque ennemis dans rayon 4 (max +90 HP). Avec 1 PT : +30 HP additionnels. | Le Necram se soigne de 70 HP + 15 HP par marque ennemis dans rayon 4 (max +90 HP). Avec 1 PT : +30 HP additionnels. |
| PRESSION | La récolte. Régénération Nécrotique scale avec le travail accompli. Plus de marques = plus de heal. C'est le heal qui dit : 'J'ai bien semé.' | La récolte. Régénération Nécrotique scale avec le travail accompli. Plus de marques = plus de heal. C'est le heal qui dit : 'J'ai bien semé.' |

| COCON PUTRIDE | COCON PUTRIDE | COCON PUTRIDE |
|---|---|---|
| PA | 4 | PORTÉE  0 (self) |
| TYPE | Panic button | COÛT RESS.  — |
| EFFET | Utilisable uniquement à <30% HP. Le Necram se soigne de 220 HP ET applique 1 marque à toutes les unités ennemies dans rayon 4. UTILISABLE 1 FOIS PAR MATCH. | Utilisable uniquement à <30% HP. Le Necram se soigne de 220 HP ET applique 1 marque à toutes les unités ennemies dans rayon 4. UTILISABLE 1 FOIS PAR MATCH. |
| PRESSION | L'explosion fongique. Cocon Putride n'est pas qu'un panic-heal — c'est une aspersion. Le Necram à l'agonie devient soudain le Necram avec 6+ marques sur la map. Game-changer total. | L'explosion fongique. Cocon Putride n'est pas qu'un panic-heal — c'est une aspersion. Le Necram à l'agonie devient soudain le Necram avec 6+ marques sur la map. Game-changer total. |

# **GHOSTRA**

*L'ASSASSIN SPECTRAL*

### **FANTASY DE GAMEPLAY**

*La Ghostra ne combat pas dans le présent. Elle combat dans le passé et le futur, simultanément. Elle laisse derrière elle des images d'elle-même qui frappent, et frappe à des endroits où elle n'est pas encore. Le combat n'est plus une lecture de positions — c'est un puzzle temporel.*

**ÉMOTION RESSENTIE PAR L'ADVERSAIRE**

*Peur mentale. L'incapacité de lire ce qui est réel.*

### **RESSOURCE — RÉMANENCE**

**3 LEURRES MAXIMUM**

- La Ghostra n'a pas une jauge — elle a des LEURRES sur la grille.

- Chaque sort spécifique pose un Leurre (clone visuel) sur une case.

- Maximum 3 Leurres simultanés sur la grille.

- Chaque Leurre dure 2 tours ou jusqu'à destruction par interaction.

- Les Leurres apparaissent IDENTIQUES à la vraie Ghostra côté adversaire. La Ghostra elle-même apparaît comme un Leurre.

- Les sorts ciblés sur un Leurre passent à travers (le Leurre disparaît). Les AoE qui touchent un Leurre le détruisent.

*POURQUOI : La RM matérialise l'identité de la Ghostra : l'IMPOSSIBLE LECTURE. L'adversaire ne joue plus contre 1 personnage, il joue contre 4 silhouettes dont 3 sont fausses.*

### **PASSIF — L'ANGLE MORT**

*Système de DÉSYNCHRONISATION qui modifie les sorts dorsaux selon la densité de leurres.*

- ANGLE 1 (0 leurre actif) : la Ghostra applique ses sorts au moment normal. Aucun bonus.

- ANGLE 2 (1-2 leurres actifs) : tous les sorts dorsaux gagnent +50 dégâts ET appliquent PLAIE OUVERTE (40 dégâts/tour pendant 2 tours).

- ANGLE 3 (3 leurres actifs) : la Ghostra peut FAIRE PERMUTER une de ses positions avec un leurre, 1 fois par tour, gratuitement (0 PA). Les sorts dorsaux gagnent +80 dégâts.

- Permutation = swap instantané entre la Ghostra et un de ses leurres. INVISIBLE côté adversaire.

*POURQUOI : Le passif récompense la DENSITÉ de leurres. La Ghostra ne devient pas plus puissante en infligeant des dégâts — elle devient plus illisible. Le passif est NON-LINÉAIRE.*

### **COMMENT LA CLASSE TRANSFORME LE COMBAT**

La Ghostra démolit le concept même de "savoir où est l'ennemi". À 3 leurres + permutation, l'adversaire ne sait JAMAIS où la vraie Ghostra va frapper. Le combat devient un jeu de probabilités. Aucune autre classe ne fait ça.

### **FORCES & FAIBLESSES ASSUMÉES**

**FORCES**

- Lisibilité brisée — l'adversaire ne sait jamais qui frappe quoi

- Mobilité ultime via téléportations + permutations

- Burst monstrueux sur cible désorientée

- Domine les classes statiques (Colossar enfermé) et les sorts à cibles précises

**FAIBLESSES**

- Fragile — HP de base + faible accès au heal

- Vulnérable aux DoT (Soulrender bleed, Necram poison) qui ignorent les leurres

- Vulnérable aux AoE massives (Salve Mortelle, Détonation Sanglante, Effondrement)

- Setup-dépendante : sans leurres en place, c'est juste une assassin fragile

### **STYLE DE PRESSION**

INVISIBLE. La pression Ghostra ne se voit pas. Elle est dans la TÊTE de l'adversaire qui doute. Chaque tour, l'adversaire doit deviner laquelle des silhouettes est la vraie. Une mauvaise lecture = burst dorsal massif.

### **STYLE DE COMBO**

Le combo Ghostra s'appelle un FANTÔME. Il chaîne une création de leurre, une permutation, et un finisher dorsal. Réplique Fantôme + Pas dans l'Ombre + Saigne-Âme dorsal = la cible a vu la Ghostra à un endroit, mais elle frappe d'un autre. Le Fantôme exploite l'écart entre ce que voit l'adversaire et ce qui se passe réellement.

### **GESTION DU TERRAIN**

Pas de modification de map directe — la Ghostra ne pose pas d'obstacles, ne crée pas de DoT au sol. Elle pollue VISUELLEMENT la map avec ses leurres. Sa "modification de terrain" est cognitive, pas physique.

### **GESTION DU TEMPO**

EXPLOSIF et SACCADÉ. Tour 1-2 : pose de leurres, premiers tests. Tour 3-4 : le passif passe en Angle 2-3, les Fantômes commencent. Tour 5+ : burst final ou décrochage. La Ghostra ne joue PAS un long combat — elle joue par fenêtres de 1 tour où elle dicte tout, suivies de tours de retraite.

## **SORT SIGNATURE — EXÉCUTION SPECTRALE**

*Sort débloqué automatiquement quand la ressource atteint son cap. Cooldown 4 tours après usage. Slot séparé du deck de 6.*

| EXÉCUTION SPECTRALE | EXÉCUTION SPECTRALE | EXÉCUTION SPECTRALE |
|---|---|---|
| PA | 3 | PORTÉE  1 (mêlée, dorsal requis) |
| TYPE | Sort signature | COÛT RESS.  Coûte 3/3 LEURRES (consomme TOUS les leurres actifs) |
| EFFET | Inflige 350 dégâts SI la cible est dorsale (regarde ailleurs). Applique PLAIE OUVERTE (50 dégâts/tour × 3 tours). Si la cible meurt sur ce sort, la Ghostra regagne 100 HP ET 2 leurres réapparaissent immédiatement (2 prêts pour le cycle suivant). Si la cible n'est PAS dorsale au moment du cast, le sort RATE et les 3 leurres sont quand même consommés. Décision à très haut risque. | Inflige 350 dégâts SI la cible est dorsale (regarde ailleurs). Applique PLAIE OUVERTE (50 dégâts/tour × 3 tours). Si la cible meurt sur ce sort, la Ghostra regagne 100 HP ET 2 leurres réapparaissent immédiatement (2 prêts pour le cycle suivant). Si la cible n'est PAS dorsale au moment du cast, le sort RATE et les 3 leurres sont quand même consommés. Décision à très haut risque. |
| PRESSION | Le coup le plus risqué du jeu. Exécution Spectrale demande une LECTURE PARFAITE — la cible doit être dorsale. Ratée, la Ghostra perd tout son setup. Réussie, elle finit le match en 1 tour. Le simple fait que ce sort existe dans le deck force l'adversaire à pivoter constamment. | Le coup le plus risqué du jeu. Exécution Spectrale demande une LECTURE PARFAITE — la cible doit être dorsale. Ratée, la Ghostra perd tout son setup. Réussie, elle finit le match en 1 tour. Le simple fait que ce sort existe dans le deck force l'adversaire à pivoter constamment. |

**COOLDOWN : Cooldown 4 tours après usage. Réutilisable si 3 leurres reposés.**

## **SORTS OFFENSIFS — 5 sorts**

*Sorts dont la fonction primaire est d'infliger des dégâts. Le cœur du DPS. Choisissez 1 à 4 dans votre deck selon votre style.*

| LAME SPECTRALE | LAME SPECTRALE | LAME SPECTRALE |
|---|---|---|
| PA | 3 | PORTÉE  1 (mêlée) |
| TYPE | Frappe de base | COÛT RESS.  — |
| EFFET | Inflige 170 dégâts. Si dorsal : +50 dégâts (Angle 2) ou +80 (Angle 3) du passif. Si la cible a PLAIE OUVERTE : +60 dégâts. | Inflige 170 dégâts. Si dorsal : +50 dégâts (Angle 2) ou +80 (Angle 3) du passif. Si la cible a PLAIE OUVERTE : +60 dégâts. |
| PRESSION | La frappe la plus banale du jeu — sauf que personne ne sait d'où elle vient. La banalité du sort est sa force : il sort de partout, depuis n'importe quel leurre. | La frappe la plus banale du jeu — sauf que personne ne sait d'où elle vient. La banalité du sort est sa force : il sort de partout, depuis n'importe quel leurre. |

| FRAPPE FANTÔME | FRAPPE FANTÔME | FRAPPE FANTÔME |
|---|---|---|
| PA | 4 | PORTÉE  4 (téléport adjacent) |
| TYPE | Engage / Burst | COÛT RESS.  — |
| EFFET | La Ghostra se téléporte à 1 case de la cible (côté libre). Inflige 200 dégâts. Si dorsal : +bonus passif. Si la cible avait été VOLTE-FACE ou que sa direction a été modifiée ce tour : APPLIQUE PLAIE OUVERTE (40/tour × 2t). | La Ghostra se téléporte à 1 case de la cible (côté libre). Inflige 200 dégâts. Si dorsal : +bonus passif. Si la cible avait été VOLTE-FACE ou que sa direction a été modifiée ce tour : APPLIQUE PLAIE OUVERTE (40/tour × 2t). |
| PRESSION | Le finisseur. Frappe Fantôme arrive de nulle part. Combinée à Volte-Face, c'est un combo qui peut shred 350+ HP en un tour. | Le finisseur. Frappe Fantôme arrive de nulle part. Combinée à Volte-Face, c'est un combo qui peut shred 350+ HP en un tour. |

| LAME VORACE SPECTRALE | LAME VORACE SPECTRALE | LAME VORACE SPECTRALE |
|---|---|---|
| PA | 3 | PORTÉE  1 (mêlée) |
| TYPE | Combo bleed | COÛT RESS.  — |
| EFFET | Inflige 130 dégâts + 60 si la cible a PLAIE OUVERTE. Si dorsal : +bonus passif. La Plaie Ouverte n'est PAS consommée. | Inflige 130 dégâts + 60 si la cible a PLAIE OUVERTE. Si dorsal : +bonus passif. La Plaie Ouverte n'est PAS consommée. |
| PRESSION | Le coup qui ronge. Lame Vorace empile sur une plaie ouverte sans la fermer. Ghostra qui a posé une Plaie Ouverte au tour précédent peut la rentabiliser pendant 2 tours. | Le coup qui ronge. Lame Vorace empile sur une plaie ouverte sans la fermer. Ghostra qui a posé une Plaie Ouverte au tour précédent peut la rentabiliser pendant 2 tours. |

| SAIGNE-ÂME | SAIGNE-ÂME | SAIGNE-ÂME |
|---|---|---|
| PA | 4 | PORTÉE  2 |
| TYPE | Finisher conditionnel | COÛT RESS.  — |
| EFFET | Inflige 200 dégâts + 70 si la cible a PLAIE OUVERTE (consomme la plaie). Si la cible meurt : la Ghostra regagne 60 HP. | Inflige 200 dégâts + 70 si la cible a PLAIE OUVERTE (consomme la plaie). Si la cible meurt : la Ghostra regagne 60 HP. |
| PRESSION | L'aboutissement. Saigne-Âme consomme la plaie pour un payoff massif. Le sort de fin du combo Plaie Ouverte → Lame Vorace → Saigne-Âme. Aucune autre frappe Ghostra n'est aussi rentable sur cible préparée. | L'aboutissement. Saigne-Âme consomme la plaie pour un payoff massif. Le sort de fin du combo Plaie Ouverte → Lame Vorace → Saigne-Âme. Aucune autre frappe Ghostra n'est aussi rentable sur cible préparée. |

| DANSE DES LAMES | DANSE DES LAMES | DANSE DES LAMES |
|---|---|---|
| PA | 5 | PORTÉE  0 (AoE 1 autour) |
| TYPE | Burst final | COÛT RESS.  Optionnel : consommer 1 leurre adjacent à la cible → bonus dorsal automatique |
| EFFET | AoE 8 cases adjacentes. Inflige 180 dégâts à toutes les cibles. Toute cible touchée subit le bonus dorsal du passif si dorsale OU si un leurre est adjacent à elle (consommation optionnelle : -1 leurre, applique bonus dorsal automatique). | AoE 8 cases adjacentes. Inflige 180 dégâts à toutes les cibles. Toute cible touchée subit le bonus dorsal du passif si dorsale OU si un leurre est adjacent à elle (consommation optionnelle : -1 leurre, applique bonus dorsal automatique). |
| PRESSION | L'apocalypse en miniature. Danse des Lames est le moment où la Ghostra cesse d'être un assassin et devient un cyclone. Tout converge en une seconde. | L'apocalypse en miniature. Danse des Lames est le moment où la Ghostra cesse d'être un assassin et devient un cyclone. Tout converge en une seconde. |

## **SORTS TACTIQUES — 5 sorts**

*Sorts de setup, de contrôle, de manipulation. Pas ou peu de dégâts directs, mais ils dictent la grille et les décisions adverses. Choisissez 1 à 4 dans votre deck.*

| RÉPLIQUE FANTÔME | RÉPLIQUE FANTÔME | RÉPLIQUE FANTÔME |
|---|---|---|
| PA | 3 | PORTÉE  4 |
| TYPE | Création de leurre | COÛT RESS.  — |
| EFFET | Pose un Leurre sur une case vide à 4 cases. Le Leurre est visuellement identique à la Ghostra. Dure 2 tours ou jusqu'à interaction. Si le Leurre survit 2 tours, la Ghostra regagne 80 HP. Si le Leurre est détruit par un sort adverse, la Ghostra regagne 40 HP. | Pose un Leurre sur une case vide à 4 cases. Le Leurre est visuellement identique à la Ghostra. Dure 2 tours ou jusqu'à interaction. Si le Leurre survit 2 tours, la Ghostra regagne 80 HP. Si le Leurre est détruit par un sort adverse, la Ghostra regagne 40 HP. |
| PRESSION | Le clone qui paye les frais. Réplique Fantôme FORCE l'adversaire à choisir : 'je frappe ce qui ressemble à la Ghostra ?' Toute lecture coûte. La Ghostra gagne quoi qu'il arrive. | Le clone qui paye les frais. Réplique Fantôme FORCE l'adversaire à choisir : 'je frappe ce qui ressemble à la Ghostra ?' Toute lecture coûte. La Ghostra gagne quoi qu'il arrive. |

| PAS DANS L'OMBRE | PAS DANS L'OMBRE | PAS DANS L'OMBRE |
|---|---|---|
| PA | 2 | PORTÉE  5 (téléport) |
| TYPE | Mobilité / Génération | COÛT RESS.  Optionnel : laisser un Leurre sur la case quittée |
| EFFET | Téléporte la Ghostra jusqu'à 5 cases. Si une case adjacente à l'arrivée contient une cible ennemie : la cible PIVOTE pour faire face à la Ghostra. Coût optionnel : laisser un leurre sur la case quittée (compte dans le cap 3). | Téléporte la Ghostra jusqu'à 5 cases. Si une case adjacente à l'arrivée contient une cible ennemie : la cible PIVOTE pour faire face à la Ghostra. Coût optionnel : laisser un leurre sur la case quittée (compte dans le cap 3). |
| PRESSION | Le saut de l'absent. Pas dans l'Ombre n'est pas seulement une mobilité — c'est un GÉNÉRATEUR de leurre. | Le saut de l'absent. Pas dans l'Ombre n'est pas seulement une mobilité — c'est un GÉNÉRATEUR de leurre. |

| VOLTE-FACE | VOLTE-FACE | VOLTE-FACE |
|---|---|---|
| PA | 2 | PORTÉE  4 |
| TYPE | Mindgame / Setup dorsal | COÛT RESS.  — |
| EFFET | Force la cible ennemie à faire DEMI-TOUR (180°). Sa direction de regard est inversée. Pendant 1 tour, sa direction est VERROUILLÉE — elle ne peut pas pivoter par déplacement. Toute attaque dorsale sur elle ce tour est garantie. | Force la cible ennemie à faire DEMI-TOUR (180°). Sa direction de regard est inversée. Pendant 1 tour, sa direction est VERROUILLÉE — elle ne peut pas pivoter par déplacement. Toute attaque dorsale sur elle ce tour est garantie. |
| PRESSION | L'ouverture chirurgicale. Volte-Face est un sort PRÉPARATOIRE — il ouvre la cible pour le combo. À lui seul il ne fait rien. Combiné, il transforme une cible en lapin frappé dans le dos. | L'ouverture chirurgicale. Volte-Face est un sort PRÉPARATOIRE — il ouvre la cible pour le combo. À lui seul il ne fait rien. Combiné, il transforme une cible en lapin frappé dans le dos. |

| DAGUE LANCÉE | DAGUE LANCÉE | DAGUE LANCÉE |
|---|---|---|
| PA | 1 | PORTÉE  5 |
| TYPE | Harcèlement / Repositionnement | COÛT RESS.  — |
| EFFET | Inflige 80 dégâts ET force la cible à faire face à la Ghostra (la cible pivote vers le lanceur). Si le lanceur est un LEURRE (Angle 3 + permutation) : la cible regarde le leurre, pas la vraie Ghostra. Idéal pour positionner un dorsal. | Inflige 80 dégâts ET force la cible à faire face à la Ghostra (la cible pivote vers le lanceur). Si le lanceur est un LEURRE (Angle 3 + permutation) : la cible regarde le leurre, pas la vraie Ghostra. Idéal pour positionner un dorsal. |
| PRESSION | Le caillou dans la vitre. Dague Lancée est l'outil le plus subtile : 1 PA, 80 dégâts, ça paraît minuscule. Mais elle MANIPULE le regard de la cible. | Le caillou dans la vitre. Dague Lancée est l'outil le plus subtile : 1 PA, 80 dégâts, ça paraît minuscule. Mais elle MANIPULE le regard de la cible. |

| MARQUE DE L'OMBRE | MARQUE DE L'OMBRE | MARQUE DE L'OMBRE |
|---|---|---|
| PA | 2 | PORTÉE  4 |
| TYPE | Buff de pression | COÛT RESS.  — |
| EFFET | Pendant 2 tours, tous les sorts de la Ghostra sur la cible gagnent +20 dégâts. Si la cible est touchée en dorsal pendant ces 2 tours : applique automatiquement PLAIE OUVERTE. | Pendant 2 tours, tous les sorts de la Ghostra sur la cible gagnent +20 dégâts. Si la cible est touchée en dorsal pendant ces 2 tours : applique automatiquement PLAIE OUVERTE. |
| PRESSION | Le sceau. Marque de l'Ombre pré-charge une cible. La Ghostra peut ensuite alterner Réplique → permutation → Lame Spectrale dorsal et l'effet plaie est garanti. Anti-tank par contournement. | Le sceau. Marque de l'Ombre pré-charge une cible. La Ghostra peut ensuite alterner Réplique → permutation → Lame Spectrale dorsal et l'effet plaie est garanti. Anti-tank par contournement. |

## **SORTS DE SURVIE — 5 sorts**

*Sorts de heal, bouclier, protection, panic-button. Choisissez 1 à 3 dans votre deck — trop d'outils défensifs et vous perdez en pression.*

| VOILE SPECTRAL | VOILE SPECTRAL | VOILE SPECTRAL |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Reset DoT | COÛT RESS.  — |
| EFFET | Retire INSTANTANÉMENT tous les DoT actifs sur la Ghostra (saignements, marques venin, plaies). Pendant 1 tour, la Ghostra est immunisée à toute nouvelle application de DoT. UTILISABLE 1x PAR MATCH. | Retire INSTANTANÉMENT tous les DoT actifs sur la Ghostra (saignements, marques venin, plaies). Pendant 1 tour, la Ghostra est immunisée à toute nouvelle application de DoT. UTILISABLE 1x PAR MATCH. |
| PRESSION | Le seul anti-DoT du kit Ghostra. Sans lui, la Ghostra se fait fondre par Soulrender et Necram. Avec, elle peut plonger dans le brouillard et ressortir propre. | Le seul anti-DoT du kit Ghostra. Sans lui, la Ghostra se fait fondre par Soulrender et Necram. Avec, elle peut plonger dans le brouillard et ressortir propre. |

| LINCEUL D'OMBRES | LINCEUL D'OMBRES | LINCEUL D'OMBRES |
|---|---|---|
| PA | 3 | PORTÉE  0 (self) |
| TYPE | Bouclier épineux | COÛT RESS.  — |
| EFFET | La Ghostra gagne un BOUCLIER de 130 HP pendant 2 tours. Toute attaque mêlée subie pendant la durée renvoie 40 dégâts à l'attaquant. | La Ghostra gagne un BOUCLIER de 130 HP pendant 2 tours. Toute attaque mêlée subie pendant la durée renvoie 40 dégâts à l'attaquant. |
| PRESSION | Le suaire. Linceul d'Ombres est le bouclier qui mord. Anti-Soulrender qui charge. | Le suaire. Linceul d'Ombres est le bouclier qui mord. Anti-Soulrender qui charge. |

| PAS DE L'AU-DELÀ | PAS DE L'AU-DELÀ | PAS DE L'AU-DELÀ |
|---|---|---|
| PA | 2 | PORTÉE  0 (self) |
| TYPE | Mobilité défensive | COÛT RESS.  — |
| EFFET | La Ghostra gagne +2 PM ce tour ET son prochain déplacement ignore les unités (peut traverser ennemis et leurres). Si elle traverse un ennemi, elle déclenche un sort dorsal automatique sur lui (frappe gratuite à 60 dégâts). | La Ghostra gagne +2 PM ce tour ET son prochain déplacement ignore les unités (peut traverser ennemis et leurres). Si elle traverse un ennemi, elle déclenche un sort dorsal automatique sur lui (frappe gratuite à 60 dégâts). |
| PRESSION | Le glissement. Pas de l'Au-Delà transforme la Ghostra en fantôme physique. Anti-Empoignade Soulrender, anti-Mur Colossar. | Le glissement. Pas de l'Au-Delà transforme la Ghostra en fantôme physique. Anti-Empoignade Soulrender, anti-Mur Colossar. |

| RÉPLIQUE PROTECTRICE | RÉPLIQUE PROTECTRICE | RÉPLIQUE PROTECTRICE |
|---|---|---|
| PA | 3 | PORTÉE  3 |
| TYPE | Leurre tank | COÛT RESS.  — |
| EFFET | Pose un Leurre PROTECTEUR (200 HP, redirige 40% des dégâts subis par la Ghostra pendant 2 tours). Si le Leurre est détruit, la Ghostra regagne 60 HP. Compte dans le cap 3 leurres. | Pose un Leurre PROTECTEUR (200 HP, redirige 40% des dégâts subis par la Ghostra pendant 2 tours). Si le Leurre est détruit, la Ghostra regagne 60 HP. Compte dans le cap 3 leurres. |
| PRESSION | Le clone-bouclier. Réplique Protectrice n'est pas un leurre offensif — c'est un sustain caché. Elle prolonge la vie de la Ghostra de 1-2 tours. | Le clone-bouclier. Réplique Protectrice n'est pas un leurre offensif — c'est un sustain caché. Elle prolonge la vie de la Ghostra de 1-2 tours. |

| DERNIER PAS | DERNIER PAS | DERNIER PAS |
|---|---|---|
| PA | 4 | PORTÉE  0 (self) |
| TYPE | Panic button mobile | COÛT RESS.  — |
| EFFET | Utilisable uniquement à <30% HP. La Ghostra se soigne de 200 HP, se téléporte jusqu'à 5 cases, ET pose un leurre sur la case quittée. UTILISABLE 1 FOIS PAR MATCH. | Utilisable uniquement à <30% HP. La Ghostra se soigne de 200 HP, se téléporte jusqu'à 5 cases, ET pose un leurre sur la case quittée. UTILISABLE 1 FOIS PAR MATCH. |
| PRESSION | L'évasion finale. Dernier Pas n'est pas qu'un heal — c'est un tour offert. La Ghostra à 200 HP se retrouve à 50% HP, à 5 cases de l'engagement, avec un leurre fraîchement posé. L'adversaire perd un tour entier à comprendre ce qui s'est passé. | L'évasion finale. Dernier Pas n'est pas qu'un heal — c'est un tour offert. La Ghostra à 200 HP se retrouve à 50% HP, à 5 cases de l'engagement, avec un leurre fraîchement posé. L'adversaire perd un tour entier à comprendre ce qui s'est passé. |

# **DE LA V6.1 À LA V7.1 — IMPLÉMENTATION**

## **CE QUI RESTE EN PLACE (PROJET UNITY)**

La fondation technique du projet (Unity 2022, URP, Photon PUN 2, grille iso, pathfinding, HUD combat, pipeline CombatInitializer) reste pertinente.

- Le moteur de combat tour par tour (initiative, timer, PA/PM, cooldowns, logs)

- Le système de deck de 6 sorts

- Le pipeline réseau 1v1 (HubMatchmaker, OracleCombatNetBridge)

- La grille iso, génération d'arène, highlights de portée

## **CE QUI DOIT ÊTRE REFAIT/AJOUTÉ**

| MODULE | TRAVAIL |
|---|---|
| Système de ressource | Refondre SpellResource pour supporter 5 systèmes (HG, PR, FD, PT, RM). |
| Système de passif | PassiveBehaviour modulaire avec conditions runtime (HP cible, leurres, marques, piliers). |
| Slot Signature | Nouveau slot SÉPARÉ du deck de 6. UI illuminée à ressource max. Cooldown 4 tours. |
| Système de marques | Étendre pour Traqué/Voilé/Empreinté (NS), marques venin (NE), Plaie Ouverte (SR/GH). |
| Cases modifiées | ZoneEffect persistants (Vapeur Carmin, Sang Coagulé, Brume Toxique, Voilé). |
| Système de leurres (Ghostra) | Le plus complexe. Indiscernables côté adversaire. Autorité serveur. |
| Système de fog (Nightseer) | Cases Voilées rendues différemment selon le joueur. |
| Permutations Ghostra | Swap instantané Ghostra ↔ leurre. 0 PA, invisible côté adversaire. |
| Obstacles dynamiques (Colossar) | Piliers/Murs ajoutés à la grille de pathfinding en temps réel. |

## **PHASING DE DÉVELOPPEMENT**

| PHASE | DURÉE | OBJECTIF |
|---|---|---|
| PHASE 1 — Squelette | 2 semaines | SpellResource, PassiveBehaviour, MarkSystem, Slot Signature. |
| PHASE 2 — Soulrender + Nightseer | 3 semaines | Implémentation des 2 premières classes (15+1 sorts). |
| PHASE 3 — Colossar + Necram | 3 semaines | Obstacles dynamiques + marques venin. |
| PHASE 4 — Ghostra | 3 semaines | Leurres + permutations + autorité réseau. |
| PHASE 5 — Équilibrage | 4 semaines | 20-30 matchs PvP réels, ajustements numériques. |
| PHASE 6 — Polish ranked | ongoing | Ban/pick, replays, observer mode, métriques. |

## **MÉTRIQUES À SURVEILLER POST-LANCEMENT**

- Durée moyenne de match (cible : 5-8 minutes en ranked)

- Winrate par classe (cible : 45-55%)

- Pick rate par classe (cible : 15-25% chacune, pas plus de 30%)

- Pick rate par sort (cible : tous les sorts utilisés à au moins 25%)

- Nombre moyen d'utilisations du Sort Signature par match (cible : 1.5 à 2.5)

- Taux de victoire par initiative (cible : 50-55% max)

## **MOT DE LA FIN**

*"Un grand jeu compétitif n'est pas équilibré parce que toutes ses classes font la même chose. Il est équilibré parce que toutes ses classes font des choses différentes — mais avec la même profondeur."*

*Nymora V7.1 vise cet équilibre. 15 sorts par classe pour la liberté de build, 1 sort signature par classe pour le payoff identitaire, 5 jeux différents pour 5 expériences différentes.*