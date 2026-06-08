# 📍 STATUT ACTUEL — NYMORA

> Source de vérité du présent. À garder **léger** (l'historique d'avant le 8 juin 2026 est dans `STATUT_ARCHIVE_jusqua_8juin2026.md`).
> Workflow actif : **`09_ROADMAP_POST_PREALPHA.md`**.

---

## Où on en est (8 juin 2026)

- **Pré-alpha fermée terminée et réussie** : 60+ joueurs enregistrés, retours positifs.
- Version client **0.1.19** · **CombatRulesVersion 140** · Bible V7.1.
- 5 classes complètes (80 sorts), hub/social, ranked 1v1, spectateur live, replay, méta-progression (deck builder, 100 succès, BP 100 tiers, shop) : **livrés et en prod**.
- Backend prod OVH `api.nymora.fr` opérationnel.
- Healthcheck : 0 erreur / 0 warning.

## Nouvelle roadmap cadrée (cf `09_ROADMAP_POST_PREALPHA.md`)

Phase 0 housekeeping → **Phase 1 patchs perso (équilibre parfait)** → Phase 2 mort subite → Phase 3 patchs mineur UI → Phase 4 migration hub WS (libère CCU) → Phase 5 2v2/3v3 → Phase 6 6e classe → Phase 7 Mac → Phase 8 tuto + analytics → Phase 9 comm/KS/cosmétiques. Backlog : sonorisation (Lorenzo décide).

## ⚠️ En attente / à faire

- **v140 non commitée** : nerf buffs Soulrender (Peau de Fer 30→10 / Sang Bouillant 30→15 / Frénésie 10%→5%). Fichiers modifiés : `SpellRegistry.cs`, `GameVersion.cs`, `SpellBibleTexts.cs`. → commit Phase 0.
- Patch list complète : `Desktop/Patch à faire.txt`.

## Prochaine action

Phase 0 : commit v140 + docs. Puis **Phase 1, brique 1** (à définir : probablement 2000 HP + audit bugs miroir).

---

*Dernière mise à jour : 8 juin 2026.*
