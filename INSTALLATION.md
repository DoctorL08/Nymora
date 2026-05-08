# 🚀 Setup Claude Code pour Nymora — Guide d'installation

## 📦 Ce que tu reçois

```
nymora_claudecode_setup/
├── CLAUDE.md                    ← lu auto par Claude Code à chaque session
├── .claudeignore                ← exclusions (Library/, Temp/, etc.)
├── .claude/
│   └── settings.json            ← permissions et config projet
└── _docs/
    ├── 00_README_CLAUDE.md      ← briefing complet
    ├── STATUT_ACTUEL.md         ← état vivant à update à chaque session
    ├── INDEX.md
    ├── 01_BIBLE_V7.1_Combat.md
    ├── 02_Architecture_Technique.md
    ├── 03_GDD_Features.md
    ├── 04_Roadmap_14_mois.md
    └── 05_Roadmap_V2_Novice.md
```

---

## 🛠️ Installation (3 minutes)

### Étape 1 — Crée le projet Unity en suivant la Brique 0.1

Si pas déjà fait, va d'abord créer ton projet Unity (cf. dernier message Claude). Tu auras alors un dossier `D:\Dev\Nymora\` avec la structure Unity standard (`Assets/`, `Packages/`, `ProjectSettings/`).

### Étape 2 — Copier le setup à la racine du projet

Une fois ton projet Unity créé, **dézippe** le contenu de `nymora_claudecode_setup/` directement à la racine de ton projet Unity.

Tu dois te retrouver avec :

```
D:\Dev\Nymora\
├── CLAUDE.md                    ← NOUVEAU
├── .claudeignore                ← NOUVEAU
├── .claude/                     ← NOUVEAU
│   └── settings.json
├── _docs/                       ← NOUVEAU
│   └── (8 fichiers .md)
├── Assets/                      ← (existant, créé par Unity)
├── Packages/                    ← (existant)
├── ProjectSettings/             ← (existant)
└── (autres dossiers Unity)
```

### Étape 3 — Vérifier que ça marche

Ouvre ton terminal dans le dossier projet :

```bash
cd D:\Dev\Nymora
claude
```

Quand Claude Code démarre, il devrait :
1. **Détecter automatiquement le `CLAUDE.md`** (tu verras un message du type "Read CLAUDE.md")
2. **Charger les permissions** depuis `.claude/settings.json`
3. **Ignorer** les dossiers listés dans `.claudeignore`

Pour tester, envoie ce premier message :
> Salut chef. Lis le STATUT_ACTUEL.md et dis-moi où on en est sur Nymora.

Claude Code devrait te répondre en sachant que :
- Tu es Lorenzo, novice solo dev
- On est en Phase 0, brique 0.1 en cours
- La stack est verrouillée (Unity 2022.3.62f3, Photon Quantum, etc.)
- Le workflow est brique par brique

Si la réponse fait du sens et qu'il t'appelle "chef", **c'est gagné**. ✅

---

## 🎯 Comment ça marche au quotidien

### Au démarrage de chaque session
Tu lances `claude` dans le terminal du projet. Claude Code lit automatiquement :
1. `CLAUDE.md` (instructions projet)
2. Les fichiers que tu lui demandes (il sait quoi lire grâce à `CLAUDE.md`)

**Pas besoin d'uploader quoi que ce soit.**

### Pendant la session
- **Claude peut lire** tout ce qui est dans `_docs/`, `Assets/_Nymora/`, `Packages/`
- **Claude peut éditer** tout ce qui est dans `Assets/_Nymora/`, et `_docs/STATUT_ACTUEL.md`, `_docs/05_Roadmap_V2_Novice.md`
- **Claude doit demander** avant d'éditer les autres docs `_docs/` ou `CLAUDE.md`
- **Claude ne peut pas** toucher `Library/`, `Temp/`, `Logs/`, etc.

### En fin de session
Demande à Claude :
> Update le STATUT_ACTUEL.md avec ce qu'on a fait aujourd'hui.

Il met à jour le fichier et tu commit :
```bash
git add _docs/STATUT_ACTUEL.md
git commit -m "docs: update statut actuel - fin session [date]"
```

---

## 🔐 Permissions configurées

Le `settings.json` est calibré pour :

| Action | Comportement |
|---|---|
| Lire les docs et le code | ✅ Auto-autorisé |
| Éditer les scripts dans `Assets/_Nymora/` | ✅ Auto-autorisé |
| Éditer `STATUT_ACTUEL.md` | ✅ Auto-autorisé |
| Éditer la Bible, l'Architecture, etc. | ⚠️ Demande confirmation |
| Éditer `CLAUDE.md` ou `.gitignore` | ⚠️ Demande confirmation |
| `git status`, `git diff`, `git log` | ✅ Auto-autorisé |
| `git commit`, `git push`, `git add` | ⚠️ Demande confirmation |
| `npm install`, `dotnet add` | ⚠️ Demande confirmation |
| `git push --force`, `rm -rf` | ❌ Bloqué |
| Lire `Library/`, `Temp/`, `obj/`, etc. | ❌ Bloqué (pollution contexte) |

Tu peux ajuster ces règles dans `.claude/settings.json` si tu veux plus de liberté.

---

## 💡 Astuces pro

### 1. Commit ce setup dans Git
Le `CLAUDE.md`, le `.claudeignore` et le `_docs/` doivent être **versionnés** dans Git :

```bash
git add CLAUDE.md .claudeignore _docs/ .claude/settings.json
git commit -m "chore: add claude code setup and documentation pack"
```

Comme ça :
- Si tu changes de machine, tu retrouves tout
- Tu peux faire des `git diff` pour voir l'évolution des décisions
- Si plusieurs Claude Code tournent en parallèle (futur), elles partagent le même contexte

### 2. Update régulier
Mets à jour `STATUT_ACTUEL.md` à chaque fin de session. C'est **le** fichier qui fait toute la magie de la transmission entre instances.

### 3. Si Claude Code dérape
Tu peux toujours lui rappeler les règles :
> Relis le CLAUDE.md et la règle sur les valeurs hardcodées. Tu viens de mettre 1500 en dur dans le script.

Il s'auto-corrige.

### 4. En cas de conflit de version Claude Code
Si un jour Anthropic update le format `settings.json`, vérifie que les permissions sont toujours valides via :
```bash
claude --check-config
```

---

## 🆘 Troubleshooting

**"Claude Code ne lit pas mon CLAUDE.md"**
→ Vérifie que tu lances `claude` **depuis la racine du projet** (`D:\Dev\Nymora\`), pas depuis un sous-dossier.

**"Claude Code lit Library/ et sature le contexte"**
→ Vérifie que `.claudeignore` est bien à la racine et nommé correctement (pas `.claudeIgnore` ou `claudeignore`).

**"Les permissions ne s'appliquent pas"**
→ Vérifie que `.claude/settings.json` est bien dans un dossier `.claude/` (avec le point devant), pas `claude/`.

**"Claude veut éditer un fichier de doc protégé"**
→ Normal, il demande confirmation. Tu acceptes au cas par cas, ou tu modifies les règles dans `settings.json`.

---

## 🎬 Prêt à coder

Une fois ce setup en place, **tu n'as plus jamais à expliquer le projet** à une nouvelle session Claude Code. Tu lances `claude`, il sait tout.

C'est le setup qui va te porter pendant 14 mois. Bon dev chef. 💪
