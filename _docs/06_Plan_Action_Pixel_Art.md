# NYMORA — Plan d'Action Direction Artistique

> **Pour :** Le collègue qui rejoint la prod côté pixel-art
> **De :** Lorenzo (lead dev) + Claude (lead tech)
> **Date :** 9 mai 2026 — V1.0
> **Format cible :** Pixel-art 128×128 · Aseprite + extension Pixel Lab · Unity 2022.3.62f3 (URP 2D)

---

## 0. RÉSUMÉ EN 30 SECONDES

Bienvenue. Tu rejoins **Nymora**, un PvP tactique 1v1 dark fantasy en pixel-art, dev en solo par Lorenzo (alpha Windows visée pour mai 2027). Ton job : produire **toute la direction artistique** du jeu — sprites de combat, icônes de sorts, tilesets, UI, map sociale, cosmétiques.

On bosse **brique par brique, en séquentiel**, calé sur la roadmap technique de Lorenzo. **Tu n'as PAS à tout produire d'un coup** : chaque sprint vise une livraison utile au code en cours. Mieux vaut 1 classe finie et intégrée que 5 classes en draft.

**Trois règles non-négociables :**

1. **Toujours valider la palette + le styleframe AVANT de scaler la prod.** Une erreur de direction sur la première classe = 5 classes à refaire.
2. **Pixel Lab est un assistant, pas un livreur.** Tout sprite passe par un cleanup manuel (palette, contours, lisibilité combat).
3. **Lisibilité en stream avant esthétique.** Chaque sort, chaque marque, chaque ressource doit être identifiable en 1 seconde sur un stream Twitch.

---

## 1. CONTEXTE PROJET

### 1.1 Nymora c'est quoi

Un **PvP 1v1 tactique au tour par tour**, vue grille (top-down ou ¾), dark fantasy. 5 classes radicalement asymétriques (Soulrender, Nightseer, Colossar, Necram, Ghostra). Match court (15-20 min), 6-8 tours, 1500 HP, 8 PA, 3 PM.

Chaque classe a :
- Une **identité visuelle forte** (couleur d'accent, ressource unique, fantasy de gameplay)
- 15 sorts répartis en 5 Offensifs / 5 Tactiques / 5 Survie
- 1 sort Signature débloqué à ressource max
- Un passif visuel non-linéaire qui s'amplifie au fil du match

Hors combat, le jeu propose une **map communautaire** isométrique-2.5D (50 joueurs/instance) façon hub MMO, avec chat multi-canal, deck builder, ranked, clans, boutique, battle pass.

### 1.2 Stack technique côté Lorenzo

- **Unity 2022.3.62f3 LTS**, render pipeline **Universal 2D (URP 2D)**
- **2D Lights** activées (donc on peut prévoir des normal maps si on veut, mais **pas pour l'alpha**)
- Build target alpha = **Windows Standalone uniquement** (Mac/Mobile post-alpha)
- Asset pipeline = sprites en **PNG**, import Unity (filtre Point, no compression)
- Animations = sprite-sheets ou frames séquencées (à voir avec Lorenzo selon l'animator choisi : Aseprite Importer Unity / standard frame anim)

### 1.3 État actuel du projet (au 9 mai 2026)

- **Phase 1 — Fondations techniques** en cours (backend + netcode)
- **Phase 2 — Combat 1v1 (Soulrender + Nightseer)** prévue Juillet-Août 2026
- **Phase 3 — Combat 1v1 (3 autres classes)** prévue Sept-Oct 2026
- **Phase 4 — Map commu + social** prévue Nov-Déc 2026
- **Phase 5 — Progression + économie** prévue Jan-Fév 2027
- **Phase 6 — Ranked + 2v2/3v3** prévue Mars-Avr 2027
- **Phase 7 — Polish + soft launch** prévue Mai-Juin 2027

> **Implication pour toi :** ta deadline critique #1 = **Soulrender complet pour fin juillet 2026**. Tout le reste découle de ça.

---

## 2. SPÉCIFICATIONS TECHNIQUES PIXEL ART

### 2.1 Résolutions cibles

| Asset | Résolution | Notes |
|---|---|---|
| **Sprite perso (combat + map commu)** | **128×128 px** | Pivot bas-centre, 1 frame par direction si nécessaire |
| **Tile arène combat** | **128×128 px** | 1 tile = 1 case du grid Quantum |
| **Tile map commu** | **64×64 px** | Plus dense, 60×40 cases ≈ 8 écrans |
| **Icône de sort** | **128×128 px** | Affichée 64-96 px en jeu mais on rend 128 pour scaling propre |
| **Avatar profil** | **256×256 px** | Portrait, plus détaillé que sprite combat |
| **Bannière profil** | **512×128 px** | Background derrière l'avatar |
| **VFX sort signature** | **256×256 px (3-5 frames)** | Spectaculaire, doit être lisible à 1 sec |
| **Marque visuelle (overlay cible)** | **64×64 px** | S'affiche au-dessus du sprite touché |
| **UI cadre/bouton** | **9-slice scalable** | Bordures pixel-art, partie centrale tilable |
| **Emote map commu** | **128×128 px (4-12 frames)** | Anim courte (1-2s) |

### 2.2 Format de fichier

- **Source de travail :** `.aseprite` (un fichier par asset ou par animation)
- **Export pour Unity :** `.png` 32-bit RGBA, transparence préservée
- **Sprite-sheets :** export Aseprite « JSON Hash + spritesheet » si anim multi-frames, sinon PNG simple
- **Naming :** voir section 8

### 2.3 Palette

- **Indexée 32-64 couleurs max par classe** (lisibilité + signature visuelle forte)
- Chaque classe a sa **palette dérivée d'une palette globale Nymora** (à définir au sprint 1)
- Couleurs d'accent par classe **DÉJÀ VERROUILLÉES dans le code** :

| Classe | Accent (hex) | Mood |
|---|---|---|
| **Soulrender** | `#B22222` | Rouge sang, brûlant |
| **Nightseer** | `#6A4FB6` | Violet voilé, mystique |
| **Colossar** | `#7A6B5C` | Terre/pierre, massif |
| **Necram** | `#5A8B3E` | Vert putride, mort |
| **Ghostra** | `#6F8FA8` | Bleu spectral, froid |

> ⚠️ Ces accents sont **codés dans `NymoraClassDefinition.asset`** et utilisés par l'UI. Tu dois t'aligner dessus, pas l'inverse. Si tu veux changer, faut convaincre Lorenzo et il devra patcher les SO.

### 2.4 Style visuel cible

- **Pixel art 128×128 dark fantasy** dans la lignée de :
  - **Owlboy** (lighting, lisibilité, palette)
  - **Blasphemous** (mood gothique, contraste)
  - **Children of Morta** (combat top-down, anims)
  - **Hyper Light Drifter** (économie de pixels, posture, palette restreinte)
  - **Eastward** (ambiance + UI riche)
- **Combat = top-down ou ¾ vue plongée** (à valider sprint 1)
- **Map commu = ¾ isométrique-light** (vue de Stardew Valley / Eastward)
- **Lighting :** 2D Lights URP (lampes-torches, halos magiques) — donc **prévoir des sprites avec des zones « lumineuses »** qui réagissent bien aux lights (pas de zones aplaties)
- **Animations :** snappy (combat = 4-6 frames par anim, pas 12), satisfaisantes au hit

---

## 3. PIPELINE ASEPRITE + PIXEL LAB

### 3.1 Setup recommandé

1. **Aseprite version stable** (≥ 1.3.x) avec Pixel Lab installé
2. **Templates Aseprite** à créer une fois pour toutes :
   - `template_char_128.aseprite` — canvas 128×128, layer onion-skin, frame tags `idle/walk/attack/cast/hurt/death`
   - `template_icon_128.aseprite` — canvas 128×128, layer cadre + layer pictogramme
   - `template_tile_128.aseprite` — canvas 128×128, grid 16/8 visible
3. **Profil colorimétrique :** sRGB. **Pas de gamma correction.**
4. **Export profile :** créer 1 export profile par type (`Export PNG only`, `Export sprite-sheet + JSON`)

### 3.2 Comment exploiter Pixel Lab proprement

Pixel Lab est puissant mais **dangereux si utilisé brut**. Règles d'usage :

✅ **À FAIRE avec Pixel Lab :**
- Générer des **bases de pose** (skeleton tool) pour les anims combat
- Générer des **walk cycles génériques** comme starting point
- Générer des **variantes de pose** (idle alt, victory) pour gagner du temps
- Générer des **mockups d'objets/props** pour la map commu (statues, arbres, ruines)
- Tester rapidement plusieurs **directions de design** avant validation

❌ **À NE PAS FAIRE :**
- Pousser un sprite généré sans cleanup palette + retouche manuelle
- Générer 5 classes d'un coup avec des prompts différents (= perte de cohérence)
- Utiliser Pixel Lab pour les **icônes de sort** (trop spécifique au lore, à faire à la main)
- Utiliser Pixel Lab pour les **VFX signature** (trop important pour la lisibilité gameplay)

### 3.3 Workflow type pour un sprite perso

```
1. Concept rapide à la main (croquis dans Aseprite, 64×64) — 30 min
2. Validation auprès de Lorenzo (preview PNG) — async
3. Pixel Lab : générer 3-4 variations de pose idle 128×128 — 15 min
4. Choisir la meilleure base, cleanup palette manuelle — 1-2h
5. Animer (idle 4 frames, walk 6 frames, attack 4 frames, cast 5 frames, hurt 2 frames, death 6 frames) — 4-8h selon classe
6. Export sprite-sheet PNG + JSON
7. Drop dans Assets/_Nymora/Art/Sprites/Classes/{Classe}/
8. Notifier Lorenzo (commit + screenshot)
```

### 3.4 Workflow type pour une icône de sort

```
1. Lire le sort dans la Bible V7.1 (effet, fantasy) — 5 min
2. Croquis manuel rapide (3 idées) — 15 min
3. Choisir une idée, dessiner cadre + pictogramme — 30-45 min
4. Glow / accent couleur de la classe
5. Si sort = Glyphe / Combo → ajouter un overlay rune subtil
6. Si sort = Signature → cadre doré + glow plus fort
7. Export PNG 128×128
```

### 3.5 Pièges connus de Pixel Lab

- **Drift de palette** entre 2 générations → toujours forcer la palette de la classe via "constrain to palette" si l'option existe, sinon cleanup manuel
- **Anti-aliasing parasite** sur les bords → toujours passer un dernier coup de crayon manuel pour nettoyer
- **Outlines incohérentes** → établir une règle (outline 1px noir pur OU outline contextuelle plus sombre que la couleur du sprite, mais **pas un mix**)
- **Animations Pixel Lab parfois molles** → garder les keyframes générées mais redessiner les inbetweens à la main pour le snap

---

## 4. CHARTE GRAPHIQUE

### 4.1 Identités visuelles par classe

| Classe | Silhouette | Matière dominante | Lumière | Détail signature |
|---|---|---|---|---|
| **SOULRENDER** | Trapue, large, posture penchée vers l'avant | Cuir + métal rouillé | Aura rouge progressive (vide → 5/5 fissures écarlates dans la peau) | Lames recourbées, sang qui coule |
| **NIGHTSEER** | Élancée, encapuchonnée, posture floue | Tissu + verre teinté | Halos violets, fragments lumineux qui flottent | Œil(s) sur le visage caché, gravures runiques |
| **COLOSSAR** | Massive, immobile, posture ancrée | Pierre + roches | Faible lumière, accents ocre/terreux | Fissures dans la peau qui rayonnent quand FD monte |
| **NECRAM** | Mince, voûtée, posture instable | Os + chair pourrie | Brume verte, particules putrides qui s'élèvent | Marques de putréfaction qui se propagent à 6/6 PT |
| **GHOSTRA** | Spectrale, flottante, posture irréelle | Brume + chaînes | Halo bleu froid, transparence variable | Leurres qui se détachent de la silhouette |

### 4.2 Évolutions visuelles selon ressource

C'est **CRITIQUE pour la lisibilité gameplay**. Chaque classe doit avoir des **stages visuels** indexés sur sa jauge de ressource :

| Classe | Stage 0/cap | Stage mid | Stage cap | Quand signature dispo |
|---|---|---|---|---|
| Soulrender (HG, cap 5) | aura discrète | rouge marqué | fissures écarlates | tout le sprite « pulse » rouge |
| Nightseer (PR, cap 4) | halo violet faible | runes qui s'allument | tout le sprite scintille | un œil supplémentaire s'ouvre |
| Colossar (FD, cap 3) | normal | fissures lumineuses | corps minéralisé | strates de pierre qui s'empilent |
| Necram (PT, cap 6) | brume légère | particules denses | cadavre ambulant | aura putride bouillonnante |
| Ghostra (RM, 3 leurres) | netteté pleine | 1-2 leurres autour | 3 leurres répartis | la « vraie » Ghostra et ses leurres deviennent indistinguables |

> **Implémentation Unity :** ces stages seront probablement **3-5 sprites alternatifs** par classe (idle stage 0, idle stage mid, idle stage cap, idle signature ready). Lorenzo gérera le swap côté code via l'état Quantum.

### 4.3 Marques visibles sur cible (Mark System)

Chaque classe a une marque qu'elle pose sur l'adversaire. Tu dois fournir **un overlay 64×64 par marque**, animé en boucle 4 frames :

| Marque | Posée par | Visuel |
|---|---|---|
| **MARQUE DE CARNAGE** | Soulrender | Croix de sang qui coule, rouge `#B22222` |
| **TRAQUÉ** | Nightseer | Œil violet pulsant, `#6A4FB6` |
| **VOILÉ** | Nightseer (sur case) | Brume violet semi-transparente sur tile |
| **EMPREINTÉ** | Nightseer | Empreinte de pas lumineuse violet |
| **PLAIE OUVERTE** | Soulrender (Ouvre-Plaie+HG) | Plaie béante rouge qui pulse |
| **VENIN / PUTRÉFACTION** | Necram | Goutte verte qui s'écoule, `#5A8B3E` |
| **CIBLAGE GHOSTRA** | Ghostra | Marque spectrale clignotante, `#6F8FA8` |
| **FONDATION** | Colossar (sur case) | Plaque de pierre qui apparaît, `#7A6B5C` |

### 4.4 Tiles spéciales du combat

| Tile | Effet gameplay | Visuel |
|---|---|---|
| **VAPEUR CARMIN** | -1 PM si traversée, 1 tour | Brume rouge basse sur tile, semi-transparente |
| **SANG COAGULÉ** | 30 dégâts / début de tour, 2 tours | Flaque rouge épaisse avec petites bulles |
| **CASE VOILÉE** | Invisible côté adversaire | Côté joueur = brume violette, côté adversaire = tile vide normal |
| **EMBÛCHE NIGHTSEER** | 100 dgt + Empreinté | Marque triangulaire piège (visible joueur uniquement) |
| **FONDATION** | Bonus défensif Colossar | Plaque de pierre surélevée |
| **ZONE PUTRIDE** | DoT zone Necram | Mare verte bouillonnante |

---

## 5. INVENTAIRE COMPLET DES SPRITES À PRODUIRE

> Cet inventaire est **exhaustif pour l'alpha Windows**. Les chiffres entre parenthèses sont les estimations de volumes.

### 5.1 Combat — Par classe (×5)

Pour CHAQUE classe (Soulrender, Nightseer, Colossar, Necram, Ghostra) :

| Asset | Quantité | Format |
|---|---|---|
| Sprite combat — idle (3 stages : low/mid/cap) | 3 anims × 4 frames = 12 frames | 128×128 PNG sheet |
| Sprite combat — walk (8 directions ou 4) | 4 anims × 6 frames = 24 frames | 128×128 PNG sheet |
| Sprite combat — attack (1 anim générique) | 1 anim × 4-6 frames | 128×128 PNG sheet |
| Sprite combat — cast (1 anim générique) | 1 anim × 5-7 frames | 128×128 PNG sheet |
| Sprite combat — hurt | 1 anim × 2 frames | 128×128 PNG sheet |
| Sprite combat — death | 1 anim × 6-8 frames | 128×128 PNG sheet |
| Sprite map commu — idle (variant simplifié) | 4 directions × 4 frames | 128×128 PNG sheet |
| Sprite map commu — walk | 4 directions × 6 frames | 128×128 PNG sheet |
| Avatar profil | 1 portrait | 256×256 PNG |
| Icônes de sorts (15 sorts + 1 signature) | 16 icônes | 128×128 PNG chacune |
| VFX sort signature (anim spectaculaire) | 1 anim × 8-12 frames | 256×256 PNG sheet |
| Marque sur cible (overlay anim) | 1 marque × 4 frames | 64×64 PNG sheet |
| Tiles spéciales générées par la classe | 2-4 tiles × 4 frames | 128×128 PNG sheet |

**Volume par classe : ~120-150 frames + 16 icônes + 1 avatar + 1 VFX signature.**

### 5.2 Combat — Tileset arène (commun à tous les combats)

| Asset | Quantité |
|---|---|
| Tile sol arène (variations 1-4) | 4 tiles 128×128 |
| Tile bord/délimitation arène | 8 tiles (coins + côtés) |
| Tile décor arène (props ambiance, ruines, statues, torches) | 10-15 tiles |
| Indicateur de case sélectionnée (curseur) | 1 anim × 2 frames |
| Indicateur de portée de sort (overlay tile) | 4 variants (allié / ennemi / neutre / empty) |
| Indicateur de mouvement disponible | 1 sprite |

### 5.3 UI de combat

| Asset | Description |
|---|---|
| Cadre HP (barre + numérique) | Style sang, crénelé pixel-art |
| Cadre PA (8 segments) | Cristaux, pleins/vides |
| Cadre PM (3 segments) | Plumes/pas, pleins/vides |
| Slot signature (illuminé à ressource max) | Animation glow 4 frames quand actif |
| 6 slots de sorts du deck | Cadres avec icône + cooldown overlay |
| Bouton "Fin de tour" | État normal / hover / pressed |
| Timer de tour (15s, anim countdown) | Cercle qui se vide |
| Indicateur "Ton tour / Tour adverse" | Bandeau haut écran |
| Popup dégâts (texte flottant) | Police pixel chiffrée |
| Popup heal | Police pixel verte |
| Popup miss / esquive | Police pixel grise |

### 5.4 Map communautaire (Phase 4)

| Asset | Quantité |
|---|---|
| Tileset sol map commu (cathédrale ruinée) | ~30 tiles 64×64 |
| Tileset murs / piliers | ~20 tiles |
| Props fixes (statues brisées, torches, bannières, fontaines) | ~25 props |
| Effets ambiance (brume, particules) | 5-8 anims looped |
| PNJ Marchand (idle + interaction) | 1 sprite 128×128 + anim 4f |
| PNJ Maître de Clan | 1 sprite |
| PNJ Guide Ranked | 1 sprite |
| Tableau d'événements (interactif) | 1 sprite + anim glow |
| Zone Arène d'entraînement (signage visuel) | 1 sprite décor |
| Zone Salon des Champions | 1 sprite décor |
| Zone Forum des Clans | 1 sprite décor |
| Emotes joueur (8 de base : saluer, danser, applaudir, taunt, rire, pleurer, victory, defeat) | 8 anims × 8-12 frames |

### 5.5 UI hors-combat

| Écran | Assets |
|---|---|
| **WelcomeScreen / Login / Register** | Logo Nymora, fond animé, cadres input, boutons CTA |
| **MainMenu** | Background ambiance, boutons (Map commu, Arène, Profil, Boutique, BP, Settings) |
| **Profil** | Avatar frame, bannière frame, onglets, icônes stats |
| **Deck Builder** | Cadres slots × 6, pool de sorts, filtres OFF/TAC/SUR, boutons save/rename/delete |
| **Menu Arène** | Cadres mode IA (3 difficultés) + cadres ranked (1v1/2v2/3v3) |
| **Boutique** | Tuiles items (3 tailles : small/medium/featured), badges prix, currency icons (Nymos + Shards) |
| **Battle Pass** | Track 100 tiers, voie gratuite + premium + élite, cadenas, étoiles |
| **Clans** | Roster, banner clan, rôles (Leader/Off/Vét/Mb), création clan |
| **Chat** | Onglets canaux, bulles messages, badges role, emoji picker |
| **Settings** | Sliders, toggles, dropdowns, onglets (Audio/Vidéo/Contrôles/Accessibilité/Privacy) |

### 5.6 Cosmétiques (Phase 5)

| Catégorie | Volume alpha | Notes |
|---|---|---|
| **Skins de classe** | 5 par classe × 5 classes = **25 skins** | Variantes des sprites combat + map commu (mêmes anims, palette/détails différents) |
| **Bannières profil** | 50 bannières | 512×128 PNG, illustrations |
| **Titres** | (texte, pas d'art) | — |
| **Effets de sort cosmétiques** | 3-5 par sort populaire | Overlay particules, ne change pas le gameplay |
| **Emotes premium** | 10-15 emotes payantes | Anims plus longues, plus spectaculaires |
| **Stickers chat** | 30-50 stickers animés | 64×64 GIF/sprite-sheet |
| **Bannières combat (pre/post-match)** | 10 bannières | 1920×400 ish |

### 5.7 Extras

| Asset | Notes |
|---|---|
| **Logo Nymora** | Plusieurs variantes (full, monogramme, mono) — peut être délégué à un specialist logo si besoin |
| **Splash screen Unity** | Affiché au démarrage |
| **Loading screens** | 3-5 illustrations rotation |
| **Icônes succès** | 3 catégories × ~15 styles = ~50 icônes |
| **Icônes UI génériques** | Settings, friends, mute, signaler, etc. — ~30 icônes 32×32 ou 64×64 |
| **Curseur custom** | Point + interact + combat |

---

## 6. ORDRE DE PRODUCTION RECOMMANDÉ (sprints)

> **Hypothèse :** ~3-5h/jour, calé sur la roadmap dev. Si tu peux + tu prends de l'avance, c'est cadeau.

### Sprint 0 — SETUP + CONCEPT (1 semaine)

**Objectif :** valider la direction artistique avant de produire à l'échelle.

- [ ] Installer Aseprite + Pixel Lab + valider workflow
- [ ] Créer les templates Aseprite (cf. §3.1)
- [ ] Définir la **palette globale Nymora** (16-24 couleurs core, neutres + ambiances)
- [ ] Étudier la Bible V7.1 (Soulrender + Nightseer en priorité)
- [ ] Produire 1 **styleframe combat** : Soulrender en pose idle + 1 attack frame
- [ ] Produire 1 **styleframe map commu** : un coin de cathédrale ruinée + 1 sprite joueur
- [ ] Produire 3 **icônes de sorts test** (Soulrender : Tranche-Âme, Pacte de Sang, Sève Vive)
- [ ] **Validation Lorenzo** sur les styleframes

### Sprint 1 — SOULRENDER COMPLET (3 semaines, prio absolue)

**Pourquoi en premier :** Soulrender est la classe « pillier » du jeu (mascotte naturelle), et c'est la première classe que Lorenzo va coder en Phase 2 (juillet 2026). Si tu finis Soulrender avant que Lorenzo ait fini de coder le combat, **tu seras systématiquement en avance toute la suite du projet**.

- [ ] Sprite combat complet (idle 3 stages + walk + attack + cast + hurt + death)
- [ ] Sprite map commu (idle + walk)
- [ ] Avatar profil 256×256
- [ ] **16 icônes de sorts** (5 Off + 5 Tac + 5 Sur + Âme Lacérée)
- [ ] VFX Âme Lacérée (anim spectaculaire 8-12 frames)
- [ ] Marque de Carnage + Plaie Ouverte (overlays animés)
- [ ] Tiles Vapeur Carmin + Sang Coagulé
- [ ] Stages visuels HG (0/5, mid, 5/5)

### Sprint 2 — NIGHTSEER COMPLET (3 semaines)

- [ ] Mêmes assets que Soulrender, adaptés Nightseer
- [ ] Marques Traqué + Voilé + Empreinté
- [ ] VFX Traquenard
- [ ] Tile Voilé (deux variants : POV joueur + POV adversaire)
- [ ] Stages visuels PR (0/4, mid, 4/4)

### Sprint 3 — TILESET ARÈNE + UI COMBAT (2 semaines)

**À faire en parallèle ou intercalé** des classes, car ça débloque les tests de combat de Lorenzo.

- [ ] Tileset arène (sol + bords + décor)
- [ ] Indicateurs de portée / mouvement / sélection
- [ ] Cadre HP / PA / PM
- [ ] Slot signature
- [ ] 6 slots de sorts
- [ ] Timer + bandeau de tour
- [ ] Popups dégâts / heal / miss

### Sprint 4 — COLOSSAR COMPLET (3 semaines)

(Phase 3 dev — à partir de septembre 2026)

### Sprint 5 — NECRAM COMPLET (3 semaines)

### Sprint 6 — GHOSTRA COMPLET (3 semaines)

### Sprint 7 — TILESET MAP COMMU + PNJ (3 semaines)

(Phase 4 dev — novembre 2026)

- [ ] Tileset cathédrale ruinée (sol + murs + props)
- [ ] PNJ (Marchand, Clan, Ranked, Tableau)
- [ ] Effets ambiance (brume, torches, particules)
- [ ] Emotes joueurs (8 emotes de base)

### Sprint 8 — UI HORS-COMBAT (3 semaines)

- [ ] WelcomeScreen / Login
- [ ] MainMenu
- [ ] Profil + onglets
- [ ] Deck Builder
- [ ] Menu Arène
- [ ] Settings

### Sprint 9 — BOUTIQUE + BATTLE PASS + CHAT (2 semaines)

(Phase 5 dev — janvier 2027)

### Sprint 10+ — COSMÉTIQUES (continu jusqu'au soft launch)

- [ ] 5 skins par classe (priorité Soulrender + Nightseer)
- [ ] Bannières profil (lot de 20-30 pour démarrer)
- [ ] Stickers chat (lot de 20)
- [ ] Effets de sort cosmétiques

### Sprint POLISH — Avant soft launch (2-3 semaines)

- [ ] Logo final + splash
- [ ] Loading screens
- [ ] Icônes succès
- [ ] Curseur custom
- [ ] Pass de polish global (revue de tout, harmonisation palettes, fix incohérences)

---

## 7. WORKFLOW DE COLLABORATION AVEC LORENZO

### 7.1 Règle d'or : « Brique par brique » s'applique aussi à toi

Lorenzo bosse en briques atomiques (1-5 jours). Toi pareil : pas de gros sprint sans validation intermédiaire. Si tu pars sur 3 classes en parallèle sans valider la première, c'est mort.

### 7.2 Cadence de validation

| Type de livraison | Cadence validation |
|---|---|
| **Styleframe / nouvelle direction** | 24-48h (faut Lorenzo dispo, échange dédié) |
| **Sprite/anim individuelle** | Async, batch en fin de sprint |
| **Sprint complet** | Demande explicite, max 48h |
| **Nouveau type d'asset (ex: 1ère icône)** | 24h |

### 7.3 Format de livraison à Lorenzo

Pour chaque livraison, fournir un dossier avec :

```
{nom_asset}/
├── source/              ← .aseprite originaux (pour modifs futures)
├── export/              ← .png prêts pour Unity
│   ├── {nom}_idle.png
│   ├── {nom}_walk.png
│   └── {nom}_attack.png
├── preview.gif          ← pour valider rapidement le rendu (anims)
└── README.md            ← résumé : asset, dimensions, usage, particularités
```

### 7.4 Outils de communication

- **Discord/Slack** (à voir avec Lorenzo) — discussions courtes, partages preview
- **GitHub** — repo `Nymora` (Lorenzo te donnera l'accès Git LFS pour les assets binaires)
- **Trello / Linear** (à choisir) — board partagé pour tracker les sprints
- **Lorenzo héberge** : `C:\Users\Lorenzo\Documents\Unity\Nymora\Nymora\Assets\_Nymora\Art\`

### 7.5 Quand poser une question vs avancer

✅ **Pose une question (DM Lorenzo) si :**
- Le sort/feature n'est pas clair dans la Bible V7.1
- Tu hésites entre 2 directions stylistiques
- Tu as besoin de prioriser entre 2 sprints
- Pixel Lab génère un truc bizarre et tu veux confirmer si on garde

✅ **Avance sans demander si :**
- C'est dans la Bible V7.1 et c'est explicite
- C'est un cleanup / variation d'un asset déjà validé
- C'est dans le sprint en cours et la spec est claire

---

## 8. CONVENTIONS NAMING + ARBORESCENCE UNITY

### 8.1 Arborescence Unity

```
Assets/
└── _Nymora/
    └── Art/
        ├── Sprites/
        │   ├── Classes/
        │   │   ├── Soulrender/
        │   │   │   ├── Combat/
        │   │   │   │   ├── soulrender_idle_stage0.png
        │   │   │   │   ├── soulrender_idle_stage1.png
        │   │   │   │   ├── soulrender_idle_stage2.png
        │   │   │   │   ├── soulrender_walk.png
        │   │   │   │   ├── soulrender_attack.png
        │   │   │   │   └── ...
        │   │   │   ├── Hub/
        │   │   │   │   ├── soulrender_hub_idle.png
        │   │   │   │   └── soulrender_hub_walk.png
        │   │   │   └── Avatar/
        │   │   │       └── soulrender_avatar.png
        │   │   ├── Nightseer/
        │   │   ├── Colossar/
        │   │   ├── Necram/
        │   │   └── Ghostra/
        │   ├── Spells/
        │   │   ├── Soulrender/
        │   │   │   ├── icon_tranche_ame.png
        │   │   │   ├── icon_ouvre_plaie.png
        │   │   │   ├── icon_charge_brutale.png
        │   │   │   ├── ...
        │   │   │   └── icon_ame_laceree.png
        │   │   └── ...
        │   ├── UI/
        │   │   ├── Combat/
        │   │   ├── Menu/
        │   │   ├── Profile/
        │   │   ├── DeckBuilder/
        │   │   ├── Shop/
        │   │   ├── BattlePass/
        │   │   └── Chat/
        │   ├── Tiles/
        │   │   ├── Arena/
        │   │   └── Hub/
        │   ├── Marks/
        │   │   ├── mark_carnage.png
        │   │   ├── mark_traque.png
        │   │   └── ...
        │   ├── Cosmetics/
        │   │   ├── Skins/
        │   │   ├── Banners/
        │   │   └── Stickers/
        │   └── Misc/
        │       ├── Logo/
        │       ├── Cursor/
        │       └── Loading/
        ├── Animations/      ← AnimationClip Unity (Lorenzo gère côté code)
        ├── VFX/             ← Particle systems + sprites VFX
        │   ├── Soulrender/
        │   │   ├── ame_laceree_anim.png
        │   │   └── ...
        │   └── ...
        └── _Source/         ← .aseprite (gitignored si trop lourd, sinon LFS)
            ├── Classes/
            ├── Spells/
            └── ...
```

### 8.2 Conventions de naming

- **Toujours en `snake_case`**, jamais d'espaces ni de majuscules
- **Préfixer par le contexte :**
  - `class_action_variant.png` → ex. `soulrender_walk_north.png`
  - `icon_<spell_id>.png` → ex. `icon_tranche_ame.png`
  - `mark_<name>.png` → ex. `mark_carnage.png`
  - `tile_<biome>_<variant>.png` → ex. `tile_arena_floor_01.png`
  - `ui_<screen>_<element>.png` → ex. `ui_combat_pa_filled.png`
  - `vfx_<class>_<spell>.png` → ex. `vfx_soulrender_ame_laceree.png`
- **Sprite-sheets :** suffixe `_sheet.png` + `.json` associé
  - `soulrender_walk_sheet.png` + `soulrender_walk_sheet.json`

### 8.3 Settings d'import Unity (à appliquer à tes PNG)

> Lorenzo configurera un **Texture Importer Preset** côté code. Tu n'as pas à te soucier de ça, mais pour info :
> - Texture Type : Sprite (2D and UI)
> - Filter Mode : **Point (no filter)**
> - Compression : **None**
> - Pixels Per Unit : **128** (1 sprite = 1 unit Unity)
> - Mesh Type : Tight
> - Generate Mip Maps : **OFF**

---

## 9. BUDGET TEMPS ESTIMÉ

> Ces estimations sont indicatives, basées sur un solo artist semi-expérimenté pixel art avec assistance Pixel Lab.

| Sprint | Volume | Estimation |
|---|---|---|
| Sprint 0 — Concept + styleframes | Setup + 2 styleframes + 3 icônes | **5 jours** |
| Sprint 1 — Soulrender complet | 1 classe complète | **15 jours** |
| Sprint 2 — Nightseer complet | 1 classe complète | **15 jours** |
| Sprint 3 — Tileset arène + UI combat | Tileset + UI in-game | **10 jours** |
| Sprint 4-6 — 3 autres classes | 3 classes complètes | **45 jours** |
| Sprint 7 — Map commu + PNJ | Tileset hub + 4 PNJ + 8 emotes | **15 jours** |
| Sprint 8 — UI hors-combat | 6-8 écrans UI | **15 jours** |
| Sprint 9 — Boutique + BP + Chat | UI commerce + BP track + chat | **10 jours** |
| Sprint 10+ — Cosmétiques | 25 skins + bannières + stickers | **30 jours** (étalé) |
| Sprint POLISH | Logo + loading + finitions | **10 jours** |
| **TOTAL** | **Alpha Windows** | **~170 jours = 8-9 mois** |

> **Marge :** prévoir **+30% de buffer** pour les retouches, les nouvelles demandes Lorenzo, les imprévus. Donc viser **~11 mois**, ce qui colle pile-poil à la deadline alpha (mai 2027).

---

## 10. CHECKLIST AVANT DE COMMENCER

- [ ] Installer Aseprite (≥ 1.3.x) + Pixel Lab
- [ ] Créer les 3 templates Aseprite (char / icon / tile)
- [ ] Lire **`_docs/01_BIBLE_V7.1_Combat.md`** (au moins les sections Soulrender + Nightseer)
- [ ] Lire **`_docs/03_GDD_Features.md`** (au moins les sections Map Commu + Deck Builder + Profil)
- [ ] Demander à Lorenzo l'accès Git du repo `Nymora` (LFS activé)
- [ ] Demander à Lorenzo le canal de communication (Discord ? Slack ?)
- [ ] Caler la première session de validation (sprint 0 styleframes)

---

## 11. CONTACTS + RÉFÉRENCES

| Sujet | Source |
|---|---|
| **Combat — sorts, classes, ressources** | `_docs/01_BIBLE_V7.1_Combat.md` |
| **Hub, UI, social, économie** | `_docs/03_GDD_Features.md` |
| **Stack technique** | `_docs/02_Architecture_Technique.md` |
| **Roadmap globale** | `_docs/04_Roadmap_14_mois.md` |
| **Roadmap dev semaine par semaine** | `_docs/05_Roadmap_V2_Novice.md` |
| **État présent du projet** | `_docs/STATUT_ACTUEL.md` |
| **Couleurs accent par classe (code)** | `Assets/_Nymora/ScriptableObjects/Classes/*.asset` |
| **Lorenzo (lead dev)** | À définir — Discord/Slack |

---

## 12. RÈGLES FINALES

1. **Une seule classe complète vaut mieux que cinq classes en draft.**
2. **Pixel Lab assiste, ne livre pas.** Cleanup manuel obligatoire.
3. **Lisibilité gameplay > esthétique pure.** Si un sort n'est pas identifiable en 1 sec, c'est raté.
4. **Palette indexée, pas de free pixel.** Une classe = une sous-palette stricte.
5. **Validation Lorenzo avant de scaler.** Aucun sprint long sans go intermédiaire.
6. **Backups Aseprite.** Source `.aseprite` sauvegardés dans `_Source/` ET sur ton drive personnel.
7. **Documente ce que tu livres.** README court par dossier, ça sauve des questions.

> Bienvenue dans Nymora. **On a une vraie chance de finir un jeu propre. Reste rigoureux, valide brique par brique, et on l'a.** 💪

---

*Document généré le 9 mai 2026 — V1.0*
*À mettre à jour à chaque fin de phase ou changement majeur de scope.*
