# Nymora — Roadmap V2 Novice

> Source : `Nymora_Roadmap_V2_Novice.docx`

---

# **NYMORA**

## **ROADMAP V2 — VERSION NOVICE**

*Workflow brique par brique • One-shot • Solo dev assisté*

Mai 2026 → Mai 2027 (~12 mois pour alpha Windows complète)

> 🪟 **DÉCISION VERROUILLÉE — 8 mai 2026**
>
> Cette roadmap cible **Windows uniquement** pour la phase alpha. Mac et Mobile sont reportés en Phases 8 et 9 post-alpha. Cela permet de livrer un jeu Windows totalement fonctionnel et stable avant d'élargir, plutôt que de coder 4 plateformes à moitié. Gain estimé : ~1.5 à 2 mois sur la roadmap initiale.
>
> 🛡️ **OUTILS DE SCAN AUTOMATIQUE — intégrés en Phase 0**
>
> Le projet intègre dès la Phase 0 plusieurs garde-fous techniques pour détecter les bugs avant le runtime : Roslyn Analyzers (analyse statique du code à chaque compilation), Editor Script Nymora_HealthCheck (scan complet du projet en 30 secondes), pre-commit Git hook (bloque les commits sales), et console filter Nymora (filtre le bruit Unity). Philosophie : FAIL FAST.

# **Comment on bosse ensemble**

Cette roadmap est conçue pour un développeur novice qui veut construire un jeu compétitif sans accumuler de dette technique. Le principe : on découpe le projet en BRIQUES ultra-petites (1 brique = 1 à 5 jours de travail max). Chaque brique est livrée en mode one-shot — c'est-à-dire qu'on ne passe à la brique suivante qu'une fois la précédente 100% validée et fonctionnelle.

## **Les 3 rôles**

| RÔLE DE CLAUDE (moi) • Écrire 100% du code C#, des shaders, des configs Quantum, des ScriptableObjects • Te dire EXACTEMENT où coller chaque fichier (chemin précis dans Unity) • Te guider clic par clic dans l'éditeur Unity quand il faut configurer un asset • Debug tes erreurs quand tu colles les logs de la console • Expliquer chaque ligne si tu veux comprendre • Garder en tête la roadmap globale pour qu'aucune brique ne casse les suivantes |
|---|

| TON RÔLE (Lorenzo) • Copier-coller les fichiers que je te livre aux bons emplacements • Cliquer dans Unity selon mes instructions (création d'assets, configuration) • Lancer le jeu et tester en mode Play après chaque brique • Me remonter les erreurs console (copier-coller le texte rouge) • Faire des captures d'écran quand un truc visuel cloche • Commit Git à chaque fin de brique avec le message que je te donne • Surtout : NE PAS modifier les scripts sans me prévenir |
|---|

| CE QU'ON NE FAIT PAS • On ne passe pas à la brique suivante si la console a des erreurs (warnings OK) • On ne mélange pas plusieurs features dans une même brique • On ne refactorise pas en cours de brique (on note et on traite après) • On ne saute pas une étape même si elle paraît évidente • On ne commit jamais du code qui ne compile pas |
|---|

## **Le rythme one-shot**

Chaque brique suit toujours la même structure en 4 temps :

**1. SETUP — Je te dis ce qu'on va faire et pourquoi (5 min de lecture).**

**2. LIVRAISON — Je te donne tous les fichiers (scripts, assets, configs) avec leur emplacement exact.**

**3. MANIP UNITY — Je te guide clic par clic pour créer/configurer ce qui doit être fait dans l'éditeur.**

**4. VALIDATION — Tu lances le jeu, tu vérifies une checklist précise, tu me confirmes que tout marche.**

Si la validation échoue, on debug AVANT de passer à la suite. C'est la règle d'or qui évite 90% des galères.

## **Pourquoi cette méthode est cruciale pour toi**

Quand on est novice et seul, le piège classique c'est d'enchaîner les features en se disant que les bugs seront corrigés "plus tard". Sauf que plus tard, on a 50 features qui s'imbriquent et un bug peut venir de n'importe laquelle. On passe alors plus de temps à debugger qu'à coder. La méthode brique-par-brique élimine ce piège : à chaque instant, tu sais que tout ce qui est derrière toi fonctionne. Si un nouveau bug apparaît, il vient forcément de la brique en cours.

## **Combien de temps par brique ?**

Une brique fait entre 1 et 5 jours selon sa complexité. Si tu codes 2-3h par jour en moyenne, compte :

- Brique XS (1 jour) : un script simple + manip Unity rapide.

- Brique S (2 jours) : plusieurs scripts liés + assets à créer.

- Brique M (3 jours) : un système complet (ex : système de PA/PM).

- Brique L (5 jours) : une feature majeure (ex : un sort signature avec ses VFX).

Au total, la roadmap contient environ 110 briques sur 14 mois. Ça fait une moyenne de 8 briques par mois, soit 2 briques par semaine. C'est un rythme tenable même avec un job à côté.

# **Les 7 règles sacrées (à imprimer et coller au mur)**

Ces 7 règles sont non-négociables. Elles ne servent pas à te brider, elles servent à t'éviter de réécrire 3 mois de code. Chaque règle correspond à un piège réel dans lequel tombent 95% des solo devs novices.

**Règle 1 — ****Ne jamais modifier un script en cours de brique sans me prévenir**

Si tu changes une variable, un nom de classe, ou que tu ajoutes une méthode "de ton côté", je continue à coder en pensant que ton code est dans l'état que je t'ai livré. Résultat : la brique suivante référencera des trucs qui n'existent plus. Si tu veux tester un truc, fais-le dans un script séparé et préviens-moi avant qu'on continue.

**Règle 2 — ****Toujours commit Git en fin de brique**

Un commit en fin de brique = un point de sauvegarde fonctionnel. Si dans 3 semaines on casse quelque chose et qu'on comprend pas d'où ça vient, on peut revenir au commit de la brique 47 et faire un diff pour identifier ce qui a foiré. Sans commit régulier, ce diagnostic devient impossible et tu perds des jours.

**Règle 3 — ****Ne pas optimiser avant la fin de la phase**

Le code novice cherche souvent à optimiser dès la première version. C'est une erreur. On code d'abord du code lisible et fonctionnel. L'optimisation se fait après, avec des outils de mesure (Unity Profiler), pas au feeling. Optimiser à l'aveugle introduit des bugs et complexifie le code sans gain réel.

**Règle 4 — Lancer le HealthCheck avant chaque commit important**

À partir de la Brique 0.10, tu auras un outil custom Nymora_HealthCheck dans Unity qui scanne tout le projet en 30 secondes et te dit ce qui ne va pas (ScriptableObjects orphelins, valeurs hardcodées, scènes cassées, violations Quantum, etc.). Lance-le avant chaque commit important. Si le rapport indique des issues, on les fixe AVANT le commit, jamais après. C'est ta seconde ceinture de sécurité après Roslyn.

> Note : la règle initiale "tester sur mobile dès Phase 1" est suspendue pour l'alpha Windows-only. Elle reviendra en Phase 8 lors de l'extension Mac, et en Phase 9 pour le mobile.

**Règle 5 — ****Aucune valeur magique en dur dans le code**

Si tu vois "hp = 1500" écrit en dur dans un script, c'est un piège. Toutes les valeurs de gameplay (HP, PA, dégâts, coûts) doivent venir de ScriptableObjects ou de fichiers de config. Sinon, à chaque fois que tu voudras équilibrer le jeu, tu devras recompiler. Avec des ScriptableObjects, tu modifies à chaud sans toucher au code.

**Règle 6 — ****Le code de combat est sacré — versionné strictement**

À partir du moment où on aura mis en place le système de replay et l'anti-cheat, toute modification du code de combat devra incrémenter une version. Si un joueur lance un combat en version 1.4.2 et que tu push une nouvelle build 1.4.3 pendant ce temps, le combat doit pouvoir se finir avec les anciennes règles. Sinon, desync garantie.

**Règle 7 — ****Quand tu doutes, tu me demandes**

Tu n'es pas censé deviner. Si je t'ai dit "ajoute un Rigidbody2D" et que tu vois 4 options dans le menu, tu me demandes laquelle. Si tu vois une erreur que tu ne comprends pas, tu me la colles. Le but n'est pas que tu fasses tout seul, le but est que le projet avance proprement. Demander prend 30 secondes, deviner peut coûter 3 jours.

# **Vocabulaire à connaître (lexique novice)**

Avant qu'on attaque, voici les mots que je vais utiliser tout au long de la roadmap. Tu n'as pas besoin de tout retenir maintenant, juste de savoir que ce lexique existe pour t'y référer quand tu rencontres un terme inconnu.

**Script — **Un fichier .cs (C#) qui contient du code. Dans Unity, c'est un comportement qu'on peut attacher à un objet du jeu.

**GameObject — **Un objet du jeu (un personnage, une case, un bouton, une caméra). Tout dans une scène Unity est un GameObject.

**Component — **Un comportement attaché à un GameObject. Un même GameObject peut avoir plusieurs Components (ex : un sprite + un collider + un script).

**Prefab — **Un GameObject "modèle" sauvegardé comme asset. Tu peux l'instancier autant de fois que tu veux dans la scène.

**ScriptableObject — **Un asset qui contient des données (pas du comportement). Idéal pour les configs (sorts, classes, niveaux).

**Scène — **Un fichier qui contient un état du jeu (le menu, un combat, la map commu). On charge/décharge des scènes pour passer d'un état à l'autre.

**Inspector — **Le panneau Unity à droite qui montre les propriétés du GameObject sélectionné.

**Hierarchy — **Le panneau Unity à gauche qui liste tous les GameObjects de la scène en cours.

**Project — **Le panneau Unity en bas qui liste tous les assets du projet (scripts, sprites, prefabs, scènes).

**Console — **Le panneau Unity qui affiche les logs (messages blancs), warnings (jaunes) et erreurs (rouges).

**Build — **L'action de compiler le jeu en exécutable (.exe, .apk, .app). Différent de "compiler le code" qui se fait à chaque sauvegarde de script.

**Asmdef — **Assembly Definition. Un fichier qui définit un "groupe" de scripts pour accélérer la compilation. On en mettra plusieurs dans le projet.

**Photon Quantum — **Le moteur réseau déterministe qu'on va utiliser pour le combat. Garantit zéro desync.

**Photon Fusion — **Le moteur réseau classique qu'on va utiliser pour la map commu et le chat (pas besoin de déterminisme).

**Backend — **Le serveur Node.js qu'on va héberger sur un VPS. Stocke les comptes, les MMR, les inventaires, etc.

**VPS — **Virtual Private Server. Un serveur loué chez un hébergeur (Hetzner) où tournera notre backend.

**MMR — **Match Making Rating. Le score caché qui détermine contre qui tu joues en ranked.

**F2P — **Free-to-Play. Le jeu est gratuit, monétisé via cosmétiques et battle pass.

**Live service — **Un jeu mis à jour régulièrement avec du nouveau contenu (vs un jeu solo qu'on finit et qu'on lâche).

**IAP — **In-App Purchase. Achat dans l'application. Pour l'alpha Windows : Stripe uniquement. Apple/Google IAP en Phase 9 post-alpha (mobile).

| PHASE 0 Fondations du projet 2 semaines • 8 briques |
|---|

Avant la moindre ligne de gameplay, on installe l'environnement de travail propre. Cette phase paraît longue mais elle évite les drames classiques (Git pété, dossiers en bordel, scripts qui se mélangent). Si tu fais ça bien, tu gagnes des semaines plus tard.

## **Objectifs de la Phase 0**

À la fin de cette phase, tu auras :

- Unity 2022.3.62f3 installé avec le projet Universal 2D créé.

- Git + Git LFS configurés avec un .gitignore propre + pre-commit hook actif.

- Une structure de dossiers professionnelle dans Assets/.

- Les Assembly Definitions (asmdef) en place pour des compilations rapides.

- Les enums et data containers de base (NymoraClass, ResourceType, Element).

- Le ScriptableObject de classe avec les 5 classes créées.

- Le ScriptableObject de sort avec un template prêt.

- Un système de versioning (GameVersion + CombatRulesVersion).

- **Roslyn Analyzers + ruleset Nymora** qui scanne le code à chaque compilation.

- **Editor Script Nymora_HealthCheck** qui scanne tout le projet en 30 secondes.

> ⚠️ **Note importante (8 mai 2026)** : Phase 0 vise uniquement Windows. Pas besoin d'installer Mac Build Support ni Android Build Support. On les ajoutera en Phase 8 et 9 (post-alpha).

| BRIQUE 0.1  •  1/2 jour • XS Installation Unity et création du projet |
|---|

| CE QU'ON FAIT • Installer Unity Hub si pas déjà fait • Installer Unity 2022.3.62f3 (LTS) sans modules supplémentaires (alpha Windows-only) • Créer un nouveau projet vide avec le template Universal 2D • Configurer les paramètres de base (serialization, version control mode) |
|---|

| CE QUE TU FAIS • Télécharger Unity Hub depuis unity.com/download • Dans Unity Hub, onglet Installs, ajouter la version 2022.3.62f3 (Windows Build Support déjà inclus par défaut) • Onglet Projects, New project, sélectionner Universal 2D • Nommer le projet "Nymora" et le placer dans D:\Dev\Nymora • Décocher Connect to Unity Cloud et Unity Version Control • Cliquer Create |
|---|

| VALIDATION • Unity s'ouvre sur une scène vide avec un fond gris-vert • Aucune erreur rouge dans la console • Le titre de la fenêtre indique bien "Nymora - Unity 2022.3.62f3" |
|---|

| BRIQUE 0.2  •  1/2 jour • XS Configuration Editor et structure dossiers |
|---|

| CE QU'ON FAIT • Activer Asset Serialization en Force Text (essentiel pour Git) • Activer Visible Meta Files (essentiel pour Git) • Créer la structure de dossiers _Nymora/ avec tous les sous-dossiers • Créer un fichier README.md à la racine |
|---|

| CE QUE TU FAIS • Edit > Project Settings > Editor : passer Mode sur Visible Meta Files et Asset Serialization sur Force Text • Dans Project, créer manuellement les dossiers selon le schéma que je te fournis • Créer un README.md à la racine du projet (hors Assets/) avec le contenu que je te livre |
|---|

| VALIDATION • Le dossier _Nymora apparaît en haut du panneau Project (à cause du underscore) • Tous les sous-dossiers sont présents : Art, Audio, Prefabs, Scenes, Scripts (avec ses 5 sous-dossiers), ScriptableObjects, Settings • README.md existe à la racine du projet |
|---|

| BRIQUE 0.3  •  1/2 jour • XS Git + Git LFS + .gitignore |
|---|

| CE QU'ON FAIT • Installer Git si pas déjà fait + Git LFS • Initialiser le dépôt Git dans le projet • Mettre en place un .gitignore Unity propre (template officiel + ajouts) • Configurer Git LFS pour les binaires (sprites, audio, modèles) • Créer un repo GitHub privé et faire le premier push |
|---|

| CE QUE TU FAIS • Installer Git depuis git-scm.com et Git LFS depuis git-lfs.github.com • Créer un compte GitHub si pas déjà fait, créer un repo privé "nymora" • Suivre la liste de commandes que je te donne pour init le repo et push • Créer le .gitignore et .gitattributes que je te livre |
|---|

| VALIDATION • git status dans le projet ne montre que les fichiers attendus (pas les Library/, Temp/, etc.) • Le repo GitHub contient le projet Unity avec les bons fichiers • Le premier commit est nommé exactement "chore: phase 0 - initial unity project setup" |
|---|

| BRIQUE 0.4  •  1/2 jour • XS IDE et auto-complétion |
|---|

| CE QU'ON FAIT • Configurer ton IDE (Visual Studio, VS Code ou Rider) pour Unity • Vérifier que l'auto-complétion C# fonctionne sur un script de test • Mettre en place le formatage de code automatique (.editorconfig) |
|---|

| CE QUE TU FAIS • Selon ton IDE choisi, suivre la procédure que je te donne • Edit > Preferences > External Tools : sélectionner ton IDE • Créer un script de test "HelloNymora.cs" pour valider l'auto-complétion • Coller le .editorconfig que je te livre à la racine |
|---|

| VALIDATION • Quand tu tapes "Debug." dans un script, l'auto-complétion propose Log, LogWarning, etc. • Le formatage automatique se déclenche quand tu sauvegardes (Ctrl+S) • Aucune erreur de référence dans la console Unity |
|---|

| BRIQUE 0.5  •  1 jour • S Assembly Definitions (asmdef) |
|---|

| CE QU'ON FAIT • Créer 5 Assembly Definitions : Nymora.Core, Nymora.Combat, Nymora.Hub, Nymora.UI, Nymora.Network • Définir les dépendances entre asmdef (Combat dépend de Core, etc.) • Vérifier que la compilation est bien partitionnée |
|---|

Pourquoi c'est important : sans asmdef, Unity recompile TOUT le projet à chaque modification. Avec asmdef, il ne recompile que le module modifié. Sur un projet de 100k lignes, ça passe de 30 secondes à 3 secondes par modification.

| CE QUE TU FAIS • Clic droit dans chaque dossier Scripts/Core, Scripts/Combat, etc. • Create > Assembly Definition, nommer selon ma convention • Configurer les references via l'Inspector selon le diagramme que je te livre • Apply |
|---|

| VALIDATION • 5 fichiers .asmdef apparaissent dans les bons dossiers • La console n'affiche aucune erreur de référence • Test : modifier un script dans Combat → seul Nymora.Combat se recompile (visible en bas à droite d'Unity) |
|---|

| BRIQUE 0.6  •  1 jour • S Enums et data containers de base |
|---|

| CE QU'ON FAIT • Créer les enums fondamentaux : NymoraClass, ResourceType, Element, SpellCategory • Créer les structs de base : Damage, Position2D, ResourceCost • Créer la classe statique GameVersion (numéro de version semver) |
|---|

| CE QUE TU FAIS • Coller les 8 fichiers .cs que je te livre dans Assets/_Nymora/Scripts/Core/Enums/ et /Data/ • Vérifier que la compilation passe (en bas à droite : pas de symbole rouge) |
|---|

| VALIDATION • Les 8 fichiers sont bien présents et compilent • GameVersion.Current renvoie "0.1.0" si tu fais Debug.Log dans un test • Aucune erreur ni warning |
|---|

| BRIQUE 0.7  •  1 jour • S ScriptableObject NymoraClassDefinition |
|---|

| CE QU'ON FAIT • Créer le ScriptableObject NymoraClassDefinition qui décrit une classe (HP, PA, PM, ressource, passifs) • Instancier les 5 classes : Soulrender, Nightseer, Colossar, Necram, Ghostra • Remplir les valeurs de base depuis le V7.1 Bible |
|---|

| CE QUE TU FAIS • Coller le script NymoraClassDefinition.cs • Dans Project, clic droit > Create > Nymora > Class Definition (5 fois, une par classe) • Remplir chaque ScriptableObject avec les valeurs que je te liste • Placer les 5 assets dans Assets/_Nymora/ScriptableObjects/Classes/ |
|---|

| VALIDATION • 5 assets .asset visibles dans le dossier Classes/ • Chaque asset affiche correctement HP=1500, PA=8, PM=3 dans l'Inspector • Les couleurs accent sont bien renseignées (rouge B22222 pour Soulrender, etc.) |
|---|

| BRIQUE 0.8  •  1 jour • S ScriptableObject SpellDefinition (template vide) |
|---|

| CE QU'ON FAIT • Créer le ScriptableObject SpellDefinition avec tous les champs nécessaires (nom, coût PA, dégâts, portée, effets, etc.) • Créer un template Empty Spell pour valider la structure • On ne crée PAS encore les 75 sorts, juste la structure |
|---|

| CE QUE TU FAIS • Coller le script SpellDefinition.cs • Créer un asset SpellDefinition vide nommé "_Template_Spell" dans ScriptableObjects/Spells/ • Vérifier que tous les champs sont éditables dans l'Inspector |
|---|

| VALIDATION • L'asset _Template_Spell existe et tous les champs sont accessibles • Les enums (SpellCategory, ResourceType) s'affichent comme des dropdowns • Aucune erreur console |
|---|

| BRIQUE 0.9  •  1 jour • S Roslyn Analyzers + ruleset custom Nymora |
|---|

**Pourquoi cette brique** : tu vas écrire des milliers de lignes de C# sur 12 mois. Sans analyseur statique, des centaines de bugs vont se glisser dans le code et tu ne les verras qu'au moment où ça crashe en runtime. Roslyn scanne ton code à chaque compilation et te signale les problèmes **avant** qu'ils n'apparaissent en jeu. C'est la première barrière de sécurité du projet.

| CE QU'ON FAIT • Installer le package NuGet Microsoft.CodeAnalysis.NetAnalyzers dans Unity • Créer un ruleset custom Nymora.ruleset dans Assets/_Nymora/AnalyzerConfig/ • Configurer les règles : warnings sur allocations dans Update, erreurs sur Random.Range/Time.time/float dans Combat/Simulation/, etc. • Lier le ruleset au projet via un fichier .editorconfig • Tester en écrivant volontairement une violation pour vérifier que ça remonte |
|---|

| CE QUE TU FAIS • Coller les fichiers que je te fournis (csproj.template, Nymora.ruleset, .editorconfig) • Suivre la procédure d'installation NuGet pour Unity • Ouvrir un script Combat de test, ajouter Random.Range pour vérifier que Roslyn signale l'erreur • Supprimer le test |
|---|

| VALIDATION • Visual Studio affiche les warnings/erreurs Roslyn dans l'onglet Error List • Un script avec Random.Range dans Combat/Simulation/ génère une erreur de compilation • Aucun warning Roslyn injustifié sur le code Nymora existant • Le ruleset est bien dans Assets/_Nymora/AnalyzerConfig/Nymora.ruleset |
|---|

| BRIQUE 0.10  •  1 jour • S Editor Script Nymora_HealthCheck |
|---|

**Pourquoi cette brique** : Roslyn scanne le **code**, mais pas les **assets** (ScriptableObjects, scènes, prefabs). Le HealthCheck c'est l'outil custom qui scanne TOUT le projet et te sort un rapport en 30 secondes. Tu vas le lancer avant chaque commit important pour t'assurer que le projet est sain. C'est ce qui te sépare d'un dev pro et d'un dev qui galère.

| CE QU'ON FAIT • Créer Assets/_Nymora/Editor/Tools/HealthCheckTool.cs avec un menu Nymora > Validation > Project Health Check • Implémenter les scans : ScriptableObjects orphelins, valeurs hardcodées dans les scripts, asmdef violations, missing scripts dans les scènes, sprites/audios non utilisés, tags/layers orphelins, violations Quantum • Output : rapport en console Unity + fichier _docs/healthcheck_report.md horodaté • Bonus : lancement auto avant chaque build via UnityEditor.Build.IPreprocessBuildWithReport |
|---|

| CE QUE TU FAIS • Coller le script HealthCheckTool.cs dans Assets/_Nymora/Editor/Tools/ • Vérifier qu'il compile (apparition du menu Nymora > Validation) • Lancer Project Health Check sur le projet vierge → doit dire "0 issues found" • Créer volontairement un asset orphelin pour vérifier qu'il est détecté • Supprimer l'asset de test |
|---|

| VALIDATION • Le menu Nymora > Validation > Project Health Check existe • Le scan tourne en moins de 30 secondes sur le projet • Un fichier _docs/healthcheck_report.md est généré • L'asset orphelin de test est bien détecté • Le rapport est lisible et structuré (catégories : Code / Assets / Scenes / Quantum) |
|---|

| FIN DE PHASE 0 — Commit Git : feat(phase0): foundations complete - project setup, asmdef, base data structures, scan tools |
|---|

| PHASE 1 Netcode Quantum + Backend de base 2 mois • ~14 briques |
|---|

Cette phase est la plus technique de toute la roadmap. On installe Photon Quantum (le moteur réseau déterministe), on monte un backend Node.js minimal, et on connecte les deux. À la fin, tu auras un client Unity capable de se connecter à un serveur, créer un compte, et lancer une simulation Quantum vide. Ce n'est pas excitant visuellement, mais c'est la fondation de tout le combat.

## **Pourquoi on commence par ça**

Si on commençait par les sorts et les visuels, on aurait du code qui tourne en local et qui devrait être réécrit pour s'intégrer à Quantum. En commençant par le netcode, tout le code de combat qu'on écrira ensuite sera nativement compatible. C'est contre-intuitif mais c'est la bonne approche.

| BRIQUE 1.1  •  1/2 jour • XS Compte Photon + dashboard |
|---|

| CE QU'ON FAIT • Créer un compte Photon sur dashboard.photonengine.com • Créer une App Quantum 3 (note l'AppId) • Créer une App Fusion 2 (note l'AppId) • Configurer les régions (EU, US, Asia) |
|---|

| CE QUE TU FAIS • S'inscrire sur le dashboard • Cliquer Create New App, choisir Quantum 3 • Répéter pour Fusion 2 • Me communiquer les AppIds (privés) |
|---|

| VALIDATION • Tu as 2 AppIds Photon en main • Le dashboard affiche les 2 apps avec 0 CCU |
|---|

| BRIQUE 1.2  •  1 jour • S Installation Photon Quantum 3 SDK |
|---|

| CE QU'ON FAIT • Télécharger Quantum 3 SDK depuis le dashboard • Importer le package dans le projet Unity • Configurer le PhotonServerSettings avec ton AppId • Vérifier que les samples Quantum compilent |
|---|

| CE QUE TU FAIS • Télécharger le .unitypackage depuis Photon • Assets > Import Package > Custom Package • Coller l'AppId dans la fenêtre de config Quantum • Lancer un sample Quantum pour valider |
|---|

| VALIDATION • Le sample Quantum (ex : AsteroidsLite) tourne sans erreur • Tu vois apparaître un menu "Quantum" dans la barre Unity • Aucune erreur dans la console |
|---|

| BRIQUE 1.3  •  2 jours • M Premier projet Quantum vide |
|---|

| CE QU'ON FAIT • Créer une simulation Quantum vide pour Nymora • Définir le frame rate (10 ticks/sec en combat tour par tour, on calibrera) • Mettre en place les fichiers .qtn (DSL Quantum) de base • Faire tourner une simulation locale qui ne fait rien |
|---|

| CE QUE TU FAIS • Suivre mes instructions clic par clic dans la fenêtre Quantum • Coller les fichiers .qtn et .cs que je livre • Vérifier qu'une room Quantum se crée |
|---|

| VALIDATION • Tu peux créer une room Quantum locale en mode offline • La simulation tourne sans crasher pendant 30 secondes • Le checksum déterministe est stable |
|---|

| BRIQUE 1.4  •  1/2 jour • XS Backend Node.js : init du projet |
|---|

| CE QU'ON FAIT • Créer un repo GitHub séparé "nymora-backend" • Initialiser un projet Node.js + TypeScript + Express • Configurer ESLint + Prettier • Mettre en place la structure de dossiers (routes, services, db, middlewares) |
|---|

| CE QUE TU FAIS • Installer Node.js LTS si pas déjà fait • Créer le repo GitHub privé • Suivre mes commandes npm pour init le projet • Coller les fichiers de config que je livre |
|---|

| VALIDATION • npm run dev démarre un serveur Express sur localhost:3000 • GET / renvoie {"status":"ok"} |
|---|

| BRIQUE 1.5  •  1 jour • S Backend : PostgreSQL + Redis en local Docker |
|---|

| CE QU'ON FAIT • Installer Docker Desktop • Lancer un conteneur PostgreSQL 16 et un Redis 7 en local • Créer un docker-compose.yml dans le projet backend • Tester la connexion depuis Node.js |
|---|

| CE QUE TU FAIS • Installer Docker Desktop • Coller le docker-compose.yml que je livre • Lancer docker-compose up -d • Tester la connexion via un script de test |
|---|

| VALIDATION • docker ps montre les 2 conteneurs running • Le script de test affiche "PostgreSQL connected" et "Redis connected" |
|---|

| BRIQUE 1.6  •  1 jour • S Backend : schéma DB v1 (users, profiles) |
|---|

| CE QU'ON FAIT • Créer les tables users, profiles via une migration Prisma • Mettre en place Prisma ORM dans le projet • Vérifier que les tables sont bien créées en base |
|---|

| CE QUE TU FAIS • Coller le schema.prisma que je livre • Lancer npx prisma migrate dev • Vérifier en base via TablePlus ou DBeaver (gratuit) |
|---|

| VALIDATION • Les tables users et profiles existent en base • prisma generate ne lève pas d'erreur |
|---|

| BRIQUE 1.7  •  2 jours • M Backend : auth (signup + login + JWT) |
|---|

| CE QU'ON FAIT • Créer les endpoints POST /auth/signup et /auth/login • Hasher les mots de passe en bcrypt cost 12 • Générer un JWT à la connexion • Mettre en place un middleware d'auth |
|---|

| CE QUE TU FAIS • Coller les fichiers de routes et services que je livre • Tester avec Postman ou Thunder Client (extension VS Code gratuite) |
|---|

| VALIDATION • POST /auth/signup avec un body valide crée un user en base • POST /auth/login renvoie un token JWT • GET /me avec le token renvoie les infos du user |
|---|

| BRIQUE 1.8  •  2 jours • M Client Unity : connexion HTTP au backend |
|---|

| CE QU'ON FAIT • Créer un service NymoraApiClient en C# qui wrap UnityWebRequest • Implémenter signup + login + me • Stocker le token JWT en PlayerPrefs (temporaire) • Créer un menu de login basique pour tester |
|---|

| CE QUE TU FAIS • Coller les scripts ApiClient.cs et AuthService.cs • Créer une scène 00_Login avec un Canvas + 2 InputFields + 2 Boutons • Suivre mes instructions Inspector |
|---|

| VALIDATION • En lançant la scène, tu peux créer un compte depuis Unity • Le token JWT s'affiche en console après login • Si tu redémarres le jeu, le token est toujours là |
|---|

| BRIQUE 1.9  •  2 jours • M Client Unity : intégration Photon Quantum + Auth |
|---|

| CE QU'ON FAIT • Lier l'authentification backend avec la connexion Photon (Custom Auth) • Le client envoie le JWT à Photon, Photon valide via webhook backend • Si JWT invalide, connexion Photon refusée |
|---|

| CE QUE TU FAIS • Coller les scripts d'intégration côté client • Coller le webhook côté backend • Configurer le webhook dans le dashboard Photon |
|---|

| VALIDATION • Tu te connectes à Photon Quantum uniquement si tu as un JWT valide • Le backend logge la validation à chaque connexion Photon |
|---|

| BRIQUE 1.10  •  1 jour • S Système de versioning runtime |
|---|

| CE QU'ON FAIT • Implémenter GameVersion (semver) côté client • Implémenter CombatRulesVersion (incrémentée à chaque modif gameplay) • Côté backend : endpoint /version qui renvoie les versions supportées • Bloquer la connexion si client trop vieux |
|---|

| CE QUE TU FAIS • Coller les scripts client + endpoint backend que je livre • Tester en modifiant manuellement la version pour valider le block |
|---|

| VALIDATION • Si tu fakes une vieille version client, le serveur refuse la connexion • Le client affiche un message "Mise à jour requise" |
|---|

| BRIQUE 1.11  •  1 jour • S Logger structuré (client + serveur) |
|---|

| CE QU'ON FAIT • Côté client : un wrapper NymoraLog qui structure les logs (info/warn/error/critical) • Côté serveur : Pino logger configuré pour JSON output • Plus tard on enverra ces logs à Loki |
|---|

| CE QUE TU FAIS • Coller les scripts NymoraLog.cs et logger.ts • Remplacer tous les Debug.Log par NymoraLog.Info dans le code existant |
|---|

| VALIDATION • Tous les logs côté client passent par NymoraLog • Côté serveur, les logs sont en JSON avec timestamp et level |
|---|

| BRIQUE 1.12  •  1 jour • S CI/CD basique GitHub Actions |
|---|

| CE QU'ON FAIT • Côté backend : workflow qui run les tests + lint à chaque push • Côté Unity : workflow qui compile le projet en build Windows à chaque push • Les builds Mac/Android/iOS automatiques seront ajoutés en Phase 8/9 post-alpha |
|---|

| CE QUE TU FAIS • Coller les workflows .github/workflows/ que je livre • Vérifier qu'ils tournent vert sur GitHub à chaque push |
|---|

| VALIDATION • Push sur main déclenche les workflows • Tous passent au vert |
|---|

| BRIQUE 1.13  •  1 jour • S Hosting Phase 1 : VPS Hetzner |
|---|

| CE QU'ON FAIT • Créer un compte Hetzner Cloud • Provisionner un CX22 (4€/mois) en datacenter Falkenstein • Installer Docker + déployer le backend • Configurer un sous-domaine api-dev.nymora.fr |
|---|

| CE QUE TU FAIS • Acheter un domaine nymora.fr (ou autre) chez OVH/Namecheap • Suivre ma procédure pas à pas pour Hetzner • Tester que l'API est accessible depuis l'extérieur |
|---|

| VALIDATION • GET https://api-dev.nymora.fr/version renvoie les versions • Le client Unity peut se connecter au backend distant • Certificat HTTPS valide (Let's Encrypt) |
|---|

| BRIQUE 1.14  •  1 jour • S Test bout-en-bout Phase 1 |
|---|

| CE QU'ON FAIT • Créer un compte de test depuis Unity • Se logger, récupérer le token • Se connecter à Photon Quantum • Faire tourner une simulation déterministe vide pendant 1 minute • Vérifier les checksums identiques entre 2 clients |
|---|

| CE QUE TU FAIS • Suivre le scénario de test que je livre • Lancer 2 instances Unity (Build + Editor) pour tester en multi |
|---|

| VALIDATION • Les 2 clients se connectent au même match Quantum • Les checksums sont identiques tout au long de la simulation • Aucune desync |
|---|

| FIN DE PHASE 1 — Commit Git : feat(phase1): netcode quantum + backend foundations operational |
|---|

# **Phases 2 à 7 : vue d'ensemble**

Les phases suivantes seront détaillées brique par brique au moment où on les attaquera. Voici un aperçu pour que tu visualises le plan global. À chaque fin de phase précédente, je te livrerai la liste détaillée des briques de la phase suivante.

| PHASE 2 Combat : Soulrender + Nightseer 2 mois • ~16 briques |
|---|

Première phase de gameplay visible. On implémente le système de combat de base (grille, PA/PM, tour par tour) et les 2 premières classes complètes.

| OBJECTIFS PHASE 2 • Système de grille de combat 15x17 cases • Système de tour par tour avec PA/PM/HP • Pathfinding A* pour les déplacements • Soulrender complète : 15 sorts + signature + ressource Hémoglyphe + passif L'Appel du Sang • Nightseer complète : 15 sorts + signature + ressource Prescience + passif L'Œil qui n'est pas • Brouillard de guerre fonctionnel • IA de combat niveau Easy et Medium • Combat 1v1 IA jouable bout en bout |
|---|

| PHASE 3 Combat : Colossar + Necram + Ghostra 2 mois • ~18 briques |
|---|

On finit les 3 classes restantes avec leurs mécaniques uniques (obstacles dynamiques, densité toxique, leurres). À la fin, les 5 classes sont jouables contre l'IA en 1v1.

| OBJECTIFS PHASE 3 • Colossar complète : ressource Fondation + passif Densité Inerte/Effondrement + obstacles dynamiques • Necram complète : ressource Putréfaction + passif La Floraison + zones toxiques persistantes • Ghostra complète : ressource Rémanence + passif L'Angle Mort + système de leurres • IA niveau Hard avec MCTS (Monte Carlo Tree Search) • Replay system (rejouer un match depuis le frame 0) • Outils de debug combat (afficher dégâts, zones, états) |
|---|

| PHASE 4 Map communautaire + Social 2 mois • ~14 briques |
|---|

Première feature "hors combat" majeure. On crée la map communautaire avec Photon Fusion, le chat multi-canal, le système de clans, les amis, et le profil joueur.

| OBJECTIFS PHASE 4 • Scène CommunityHub avec carte tile-based explorable • Connexion Photon Fusion (50 joueurs/instance) • Mouvement multi-joueur en temps réel sur la map • Système de challenges casuels (cliquer un joueur > défier) • Chat 5 canaux (Global, Clan, Privé, Combat, System) • Système d'amis (ajouter, accepter, supprimer) • Système de clans (créer, rejoindre, hiérarchie 4 rôles) • Profil joueur avec 5 onglets |
|---|

| PHASE 5 Méta-progression + Économie 2 mois • ~14 briques |
|---|

On met en place tout ce qui maintient l'engagement long terme : niveaux de classe, achievements, deck builder, shop, battle pass, monétisation.

| OBJECTIFS PHASE 5 • Système de levels par classe (1-50 par compte par classe) • Déblocage progressif des sorts par level • 200 achievements répartis en 3 catégories • Deck Builder UI complet (5 decks max par classe, 6 sorts équipés sur 15) • Shop in-game (Nymos = monnaie de jeu) • Shop premium (Shards = monnaie payante) • Battle Pass 100 tiers (saisons 90 jours) • IAP Stripe pour Windows alpha (Apple/Google IAP en Phase 9 post-alpha) |
|---|

| PHASE 6 Ranked + 2v2 + 3v3 2 mois • ~16 briques |
|---|

Le mode compétitif. On implémente le matchmaking, les MMR, les ladders, et les modes 2v2 et 3v3 avec scènes physiquement séparées.

| OBJECTIFS PHASE 6 • Scène 40_CombatRanked1v1 (Quantum) • Scène 41_CombatRanked2v2 (Quantum) • Scène 42_CombatRanked3v3 (Quantum) • Matchmaking par MMR avec fenêtre adaptive • 8 ranks Bronze → Légende • ELO modifié avec K-factor variable • Saisons 90 jours avec rewards de fin • Leaderboards (global, classe, pays, clan) • Anti-smurf et anti-cheat actif |
|---|

| PHASE 7 Polish + Soft Launch 2 mois • ~14 briques |
|---|

Phase critique : on transforme un jeu fonctionnel en un jeu publiable. Optimisations, accessibilité, localisation, tutoriel, soft launch limité à 1000 invités.

| OBJECTIFS PHASE 7 • Tutoriel interactif (5 missions guidées) • Localisation FR + EN (extensible plus tard) • Mode accessibilité (daltonisme, reduce motion, font scaling) • Optimisation Windows (batching, atlas, audio compression) • Build PC Steam Playtest ou itch.io alpha fermée • Soft launch fermé : 500-1000 invités Windows, monitoring intensif 8 semaines • Patch hebdomadaire pendant le soft launch • PHASE 8 (post-alpha) : extension Mac • PHASE 9 (post-alpha) : extension Mobile (Android + iOS) |
|---|

# **Post-launch (au-delà des 14 mois)**

Une fois le soft launch validé et le jeu stabilisé, on entre en mode live service. La roadmap post-launch sera affinée à ce moment-là, mais voici les grandes étapes prévues.

## **Mois 15-18 : stabilisation et croissance**

Patches hebdomadaires d'équilibrage, première saison BP officielle, ajout de cosmétiques événementiels (Halloween, Noël), monitoring du churn et de la rétention.

## **Mois 19-24 : nouvelle classe + clan wars**

Une 6e classe (à designer pendant le soft launch en fonction des retours), introduction des guerres de clans hebdomadaires, premier tournoi communautaire avec cashprize cosmétique.

## **Mois 25-30 : extension géographique**

Localisation Allemand, Espagnol, Portugais (BR), Japonais. Lancement régional prioritaire sur l'Europe et l'Amérique du Sud (latence + prix accessible).

# **Annexe A : checklist hebdomadaire**

Chaque semaine, vérifie ces points pour que le projet reste sain. Ça prend 15 minutes le dimanche soir.

**☐  **Tu as commit au moins 3 fois cette semaine

**☐  **La branche main compile sans erreur

**☐  **Aucun warning non documenté en console

**☐  **Le backend tourne (vérifier le monitoring)

**☐  **Tu as lancé le HealthCheck au moins une fois cette semaine

**☐  **Tu as documenté les décisions importantes dans le README

**☐  **Tu n'as pas dépassé le scope de la phase actuelle (pas de feature "en plus")

**☐  **Tu as pris au moins un jour de repos (oui c'est dans la checklist, sérieux)

# **Annexe B : convention de nommage Git**

Pour que l'historique reste lisible, on utilise les conventional commits.

**feat: **nouvelle feature ("feat(combat): add soulrender hemoglyph resource")

**fix: **correction de bug ("fix(ui): deck builder save button not responding")

**chore: **tâche de maintenance ("chore: update photon quantum to 3.0.2")

**refactor: **réorganisation sans changement fonctionnel

**docs: **documentation

**test: **ajout de tests

**perf: **optimisation de performance

# **Annexe C : ce que tu dois savoir avant la Phase 1**

Avant qu'on attaque la Phase 1 (qui est très technique), tu devrais avoir survolé ces concepts. Pas besoin de les maîtriser, juste de savoir qu'ils existent. Je te guiderai sur le reste.

- Bases du C# : variables, classes, méthodes, héritage. Si tu as fait Codecademy ou OpenClassrooms sur C#, c'est suffisant.

- Unity Editor : navigation dans l'interface, créer un GameObject, ajouter un Component. 2h de tuto Brackeys ou Code Monkey suffisent.

- Concept de programmation orientée objet : tu n'as pas besoin de la maîtriser, juste de comprendre qu'une "classe" est un modèle et un "objet" est une instance.

- Bases Git : git add, git commit, git push, git pull. Le reste je te montrerai quand on en aura besoin.

- Lecture d'erreurs console : savoir distinguer une NullReferenceException d'une CompilerError. Je t'aiderai à décoder.

| Tu es novice mais tu n'es pas seul. À chaque doute, tu me demandes. À chaque erreur, tu me colles le log. À chaque doute sur un clic Unity, tu me décris ce que tu vois. C'est mon rôle de te guider, c'est ton rôle d'exécuter et de tester. Si on respecte ce pacte, on aura un jeu fonctionnel et stable dans 14 mois. |
|---|

