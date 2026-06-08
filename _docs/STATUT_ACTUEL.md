# 📍 STATUT ACTUEL — NYMORA

> Source de vérité du présent. À garder **léger** (l'historique d'avant le 8 juin 2026 est dans `STATUT_ARCHIVE_jusqua_8juin2026.md`).
> Workflow actif : **`09_ROADMAP_POST_PREALPHA.md`**.

---

## Où on en est (8 juin 2026)

- **Pré-alpha fermée terminée et réussie** : 60+ joueurs enregistrés, retours positifs.
- Version client **0.1.19** · **CombatRulesVersion 154** · Bible V7.1.
- 5 classes complètes (80 sorts), hub/social, ranked 1v1, spectateur live, replay, méta-progression (deck builder, 100 succès, BP 100 tiers, shop) : **livrés et en prod**.
- Backend prod OVH `api.nymora.fr` opérationnel.
- Healthcheck : 0 erreur / 0 warning.

## Nouvelle roadmap cadrée (cf `09_ROADMAP_POST_PREALPHA.md`)

Phase 0 housekeeping → **Phase 1 patchs perso (équilibre parfait)** → Phase 2 mort subite → Phase 3 patchs mineur UI → Phase 4 migration hub WS (libère CCU) → Phase 5 2v2/3v3 → Phase 6 6e classe → Phase 7 Mac → Phase 8 tuto + analytics → Phase 9 comm/KS/cosmétiques. Backlog : sonorisation (Lorenzo décide).

## Travaux récents (8 juin) — grosse session, v140 → v154, tout commité

**Phase 1 « patchs perso » TERMINÉE** (toute la patch list `Desktop/Patch à faire.txt` hors items MINEUR) :
- **v141-143 Necram** : fix miroir (densité venin par-Necram), dégâts directs (Crachat 100 / Morsure 120+10 / Inoculation 30), Brume Toxique miroir complète (2PA/2t, superposable owner-mask, kick PM owner-based), Échange Spectral → Survie.
- **v144** : toutes classes **1500 → 2000 HP** (dégâts inchangés).
- **v145 Soulrender** : Sang Bouillant déclenche sur poison + Vapeur Carmin owner-immune (sim + prévisu).
- **v146-147 Ghostra** : Pas dans l'Ombre dos, Lame Spectrale 130 + retourne dos, Éveil priorise leurre dorsal, Voile anti-corner. **+ View** : anim d'attaque + facing parfait des leurres (NE controllers à assigner dans les scènes combat).
- **v148-150 Nightseer** : Filet 1×/tour, Pas Furtif 3PA+relance, pièges 6 tours + compteur casteur, signature Traquenard refonte (poussée 2 cases 2-clics + NS prend la case d'origine).
- **v151-153 Colossar** : Provocation sans -1PM, Renvoi Bouclier 2PA/relance/cap2, Représailles → survie (heal 200 + riposte, <50%PV, 1×/match), Piliers/Murs **cap 6 cases** (n° d'ordre casteur, mur persistant).
- **v154 ligne 7** : les 5 sorts panic low-HP (Dernier Souffle/Évanescence/Dernier Pas/Cocon Putride/Représailles) uniformisés à **<50% PV + 2 PA**.
- **Descriptions minimalistes** : les **80 sorts** réécrits (1 effet/ligne, `\n`, sans PA/portée, limites en dernière ligne). Source unique `SpellBibleTexts` → deck builder + tooltip combat.

## ⚠️ En attente / à faire

- **Côté Lorenzo (avant test/publish)** : re-**Populate Spell Catalog** (pour les nouvelles descriptions + valeurs) et **rebuild standalone** (CombatRulesVersion 154) avant ranked.
- **Assigner les 3 controllers NE des leurres** dans le DecoyView des scènes combat (33_CombatCasual / 40_CombatRanked1v1) — sinon facing leurres en SE seul (cf commit `176d280`).
- Reliquat mineur : exemption Vapeur Carmin owner-immune aussi côté IA (`AISystem`) ; IA traite encore Représailles comme une attaque (à réajuster).
- Patch list restante : **items MINEUR** (UI combat, spectateur, replay rewind, stats admin par classe).

## Prochaine action

Au choix Lorenzo : **Phase 2 — mort subite tour 25**, ou les **patchs mineurs UI**, ou 2v2/3v3.

---

*Dernière mise à jour : 8 juin 2026.*
