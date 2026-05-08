# Nymora

> Jeu PvP **tactique tour par tour** dark fantasy — 2.5D isométrique
> Cible alpha : **Windows uniquement** (Mac + Mobile post-alpha)

---

## Stack

- **Engine :** Unity 2022.3.62f3 LTS — Universal 2D
- **Netcode combat :** Photon Quantum 3 (déterministe)
- **Netcode hub/social :** Photon Fusion 2 (Shared Mode)
- **Backend :** Node.js + TypeScript + Express
- **DB :** PostgreSQL 16 + Redis 7 (ORM : Prisma)
- **Hosting :** Hetzner CX22 (Phase 1)

## Mécaniques de combat (V7.1)

- 1500 HP / 8 PA / 3 PM par perso
- 6 sorts équipés parmi 15 par classe (5 Off / 5 Tac / 5 Sur) + 1 Sort Signature
- 5 classes asymétriques : Soulrender · Nightseer · Colossar · Necram · Ghostra
- Modes : vs IA (Easy/Medium/Hard), Ranked 1v1 (priorité), 2v2, 3v3

## Modèle économique

Free-to-Play + battle pass + cosmétiques. **Zéro pay-to-win.**

---

## Documentation

Toute la doc projet est dans [`_docs/`](./_docs/) :

| Fichier | Contenu |
|---|---|
| [`STATUT_ACTUEL.md`](./_docs/STATUT_ACTUEL.md) | **État vivant du projet** (brique en cours) |
| [`00_README_CLAUDE.md`](./_docs/00_README_CLAUDE.md) | Briefing complet Claude Code |
| [`01_BIBLE_V7.1_Combat.md`](./_docs/01_BIBLE_V7.1_Combat.md) | Sorts, classes, ressources, signatures |
| [`02_Architecture_Technique.md`](./_docs/02_Architecture_Technique.md) | Stack technique détaillée |
| [`03_GDD_Features.md`](./_docs/03_GDD_Features.md) | UI, social, économie |
| [`04_Roadmap_14_mois.md`](./_docs/04_Roadmap_14_mois.md) | Roadmap V1 (vue d'ensemble) |
| [`05_Roadmap_V2_Novice.md`](./_docs/05_Roadmap_V2_Novice.md) | Roadmap V2 (workflow brique par brique — **actif**) |

## Workflow

Développement **brique par brique** sur ~12 mois (mai 2026 → mai 2027).
Chaque brique = SETUP → LIVRAISON → MANIP UNITY → VALIDATION.
Une seule brique en cours à la fois, pas de passage à la suivante sans validation.

Voir [`CLAUDE.md`](./CLAUDE.md) à la racine pour les règles complètes.

---

## Build

- Target : **Windows Standalone (Mono x64)**
- Pas d'IL2CPP au début, Mono suffit pour le dev
- Mac et Mobile : reportés en Phase 8/9 post-alpha
