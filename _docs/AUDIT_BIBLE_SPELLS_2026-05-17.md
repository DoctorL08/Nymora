# Audit Bible V7.1 vs code — 2026-05-17

> Préreq brique 5.3.a Deck Builder. Diff systématique entre la Bible V7.1 et le code Quantum implémenté. Règle : code wins (validé E2E Phase 3 clôturée).

## Résumé exécutif

Audit complet des 75 sorts (5 classes × 15 sorts) + 5 signatures = **80 sorts**. Le code est cohérent avec la Bible V7.1. Tous les amendements du 16 mai 2026 ont été appliqués en code.

**Écarts détectés :**
- **0 écart critique** (stats PA / range / damage / effets)
- **2 amendments à back-porter dans la Bible** (Réplique Fantôme durée + Réplique Protectrice paramètres)
- **1 note nomenclature** : `NecramPulseSanguinVert` (code) = "Régénération Nécrotique" (Bible) — choix volontaire de rétrocompatibilité

---

## SOULRENDER (16 sorts : 15 + 1 signature)

| # | Nom | PA | Range | Dmg/Effet | Status |
|---|---|---|---|---|---|
| 1 | Tranche-Âme | 3 | 1 | 220 | ✅ |
| 2 | Ouvre-Plaie | 2 | 1 | 110 (+ 120 si 1 HG) | ✅ |
| 3 | Charge Brutale | 4 | 5 | 180 | ✅ |
| 4 | Détonation Sanglante | 4 | 4 | 60 + 40/HG | ✅ |
| 5 | Curée | 2 | 2 | 150 (2 HG mand.) | ✅ |
| 6 | Pacte de Sang | 1 | 0 | -80 HP self | ✅ |
| 7 | Marque de Carnage | 2 | 5 | marque 3t | ✅ |
| 8 | Empoignade | 3 | 3 | pull | ✅ |
| 9 | Rugissement | 3 | 0 | AoE rayon 3 | ✅ |
| 10 | Rage Insatiable | 3 | 0 | buff 2t | ✅ |
| 11 | Riposte Carmin | 2 | 0 | reflect melee | ✅ |
| 12 | Cautérisation | 2 | 0 | retire DoT | ✅ |
| 13 | Peau de Fer | 3 | 0 | shield 200 | ✅ |
| 14 | Sève Vive | 2 | 0 | heal 100 | ✅ |
| 15 | Dernier Souffle | 4 | 0 | heal 200 (1×/match) | ✅ |
| SIGN. | Âme Lacérée | 2 | 1 | 320 (5 HG mand.) | ✅ |

---

## NIGHTSEER (16 sorts : 15 + 1 signature)

| # | Nom | PA | Range | Dmg/Effet | Status |
|---|---|---|---|---|---|
| 1 | Tir Précis | 3 | 6 | 200 (280 si Traqué) | ✅ |
| 2 | Volée d'Épines | 4 | 5 | 130 par cible | ✅ |
| 3 | Détonation Onirique | 4 | 5 (10 + 2 PR) | 170 (80 + Voile) | ✅ |
| 4 | Frappe de l'Ombre | 4 | 3 | 200 (300 si PM<50%) | ✅ |
| 5 | Salve Mortelle | 5 | 6 | 220 centre / 130 côtés (3 PR) | ✅ |
| 6 | Marque du Chasseur | 1 | 5 | Traqué 3t | ✅ |
| 7 | Filet de Ronces | 2 | 4 | piège | ✅ |
| 8 | Champ de Mines | 4 | 3 | 3 mines | ✅ |
| 9 | Bourrasque | 3 | 5 | push 3, 5 + 1 PR | ✅ |
| 10 | Souffle Glacial | 3 | 0 | 70 + push 1 | ✅ |
| 11 | Voile d'Ombre | 3 | 0 | Untargetable 1t | ✅ |
| 12 | Pas Furtif | 2 | 4 | téléport | ✅ |
| 13 | Camouflage Ronces | 3 | 0 | shield + aura | ✅ |
| 14 | Sève Sauvage | 3 | 0 | heal 130 | ✅ |
| 15 | Évanescence | 4 | 7 | téléport (1×/match) | ✅ |
| SIGN. | Traquenard | 2 | 5 | 280 (4 PR) + Paralysie | ✅ |

---

## COLOSSAR (16 sorts : 15 + 1 signature)

| # | Nom | PA | Range | Dmg/Effet | Status |
|---|---|---|---|---|---|
| 1 | Frappe Lourde | 3 | 1 | 180 (280 si Épinglé) | ✅ |
| 2 | Onde de Choc | 3 | 1 | 80 + push 2 | ✅ |
| 3 | Marteau Punisseur | 4 | 2 | 160 (240 + Trauma si PA<4) | ✅ |
| 4 | Choc Sismique | 4 | 4 | 130 ligne | ✅ |
| 5 | Représailles | 3 | 1 | 100 + reflect 80 | ✅ |
| 6 | Pilier | 3 | 3 | obstruction | ✅ |
| 7 | Mur de Pierre | 4 | 4 | 3×2 cases | ✅ |
| 8 | Ancrage | 2 | 4 | -2 PM, anti-mobilité | ✅ |
| 9 | Provocation | 2 | 5 | debuff | ✅ |
| 10 | Brisure | 3 | 2 | 90 + retire buff | ✅ |
| 11 | Stoïcisme | 3 | 0 | shield 200 | ✅ |
| 12 | Garde Protectrice | 2 | 0 | réduction 30% | ✅ |
| 13 | Ressac Vital | 2 | 0 | heal 80+ | ✅ |
| 14 | Renvoi du Bouclier | 3 | 0 | Ripost 60 | ✅ |
| 15 | Soin Lourd | 3 | 0 | heal 150 | ✅ |
| SIGN. | Effondrement | 4 | 2 | 200 + Failles (3 FD) | ✅ |

---

## NECRAM (16 sorts : 15 + 1 signature)

> Note nomenclature : Le sort #14 est nommé `NecramPulseSanguinVert` en code mais "Régénération Nécrotique" en Bible. Choix volontaire de rétrocompatibilité (sort livré Phase 3.5.c.iv, replays/prefabs déjà bindés). Documenté `SpellRegistry.cs` ligne 386-388.

| # | Nom (Bible) | Nom (Code) | PA | Range | Status |
|---|---|---|---|---|---|
| 1 | Crachat Acide | CrachatAcide | 3 | 4 | ✅ |
| 2 | Morsure Putride | MorsurePutride | 4 | 1 | ✅ |
| 3 | Détonation Virulente | DetonationVirulente | 4 | 4 | ✅ |
| 4 | Faux Décharnée | FauxDecharnee | 4 | 1 | ✅ |
| 5 | Brume Toxique | BrumeToxique | 4 | 4 | ✅ |
| 6 | Inoculation | Inoculation | 1 | 5 | ✅ |
| 7 | Marque Sacrificielle | MarqueSacrificielle | 2 | 5 | ✅ |
| 8 | Symbiose Morbide | SymbioseMorbide | 3 | 0 | ✅ |
| 9 | Contagion | Contagion | 3 | 5 | ✅ |
| 10 | Pas Spectral | PasSpectral | 2 | 0 | ✅ |
| 11 | Voile Pestilence | VoilePestilence | 3 | 0 | ✅ |
| 12 | Carapace Visqueuse | CarapaceVisqueuse | 3 | 0 | ✅ |
| 13 | Drain Vital | DrainVital | 3 | 4 | ✅ |
| 14 | **Régénération Nécrotique** | PulseSanguinVert | 2 | 0 | ⚠️ nom code volontaire |
| 15 | Cocon Putride | CoconPutride | 4 | 0 | ✅ |
| SIGN. | Virus Fatal | VirusFatal | 2 | 5 | ✅ |

---

## GHOSTRA (16 sorts : 15 + 1 signature)

> Amendements 16 mai 2026 confirmés en code (Volte-Face damage 50→80, Dague Lancée damage 80→40, leurres lifetime 2→4, Réplique Protectrice nerf 3PA/40%/4t/+60HP → 4PA/30%/3t/+80HP).

| # | Nom | PA | Range | Dmg/Effet | Status |
|---|---|---|---|---|---|
| 1 | Lame Spectrale | 3 | 1 | 170 + dorsal | ✅ |
| 2 | Lame Vorace Spectrale | 3 | 1 | 130 + plaie | ✅ |
| 3 | Réplique Fantôme | 3 | 4 | leurre 4 rounds | ⚠️ Bible "2 tours" |
| 4 | Pas dans l'Ombre | 2 | 5 | téléport | ✅ |
| 5 | **Volte-Face** | 2 | 4 | **80 dmg** (amend 16 mai 50→80) | ✅ |
| 6 | Marque de l'Ombre | 2 | 4 | buff +20 | ✅ |
| 7 | **Dague Lancée** | 1 | 5 | **40 dmg** (amend 16 mai 80→40) | ✅ |
| 8 | Frappe Fantôme | 4 | 4 | 200 + téléport dorsal | ✅ |
| 9 | Saigne-Âme | 4 | 2 | 200 + plaie + heal kill | ✅ |
| 10 | Danse des Lames | 5 | 0 | 180 AoE 3×3 | ✅ |
| 11 | Linceul d'Ombres | 3 | 0 | shield épineux | ✅ |
| 12 | Voile Spectral | 2 | 0 | retire DoT (1×/match) | ✅ |
| 13 | Réplique Protectrice | **4** | 3 | leurre tank | ⚠️ Bible 3 PA, 4t, 40%, +60 |
| 14 | Dernier Pas | 4 | 0 | panic mobile (1×/match) | ✅ |
| 15 | Pas de l'Au-Delà | 2 | 0 | mobilité | ✅ |
| SIGN. | Exécution Spectrale | 3 | 1 | 350 (3 leurres) | ✅ |

---

## Synthèse — Écarts à back-porter dans la Bible

### #1 Réplique Fantôme — durée leurre

| Aspect | Bible V7.1 | Code | Action |
|---|---|---|---|
| Durée | "2 tours" | 4 rounds (`DecoyHelpers.LifetimeRounds = 4`) | Patcher Bible |

**Raison amendement** : permettre setup combo Ghostra (poser leurres tour 1, Volte-Face + Frappe Fantôme tour 2-3 avec Angle 2+ garanti).
**Date amendement** : 16 mai 2026.

### #2 Réplique Protectrice — paramètres balance

| Aspect | Bible V7.1 | Code | Action |
|---|---|---|---|
| PA | 3 | 4 | Patcher Bible |
| Durée | 4 rounds | 3 rounds | Patcher Bible |
| % redirection | 40% | 30% | Patcher Bible |
| Heal destruction | +60 HP | +80 HP | Patcher Bible |

**Raison amendement** : nerf balance Réplique Protectrice (trop forte stack tank), compensée par heal +80 sur destruction.
**Date amendement** : 16 mai 2026.

---

## Conclusions

### État Phase 5 Deck Builder

✅ **Le code peut être source-of-truth pour les SpellDefinition.asset**. Les valeurs PA / Range / Dmg / Effets exposées dans le Deck Builder seront pullées de `SpellRegistry.cs` (et complétées par les descriptions textuelles tirées de la Bible patchée).

### Checklist avant 5.3.b populator SpellDefinition

- [x] Audit complet 5 classes terminé
- [ ] Bible V7.1 patchée Réplique Fantôme (2t → 4 rounds)
- [ ] Bible V7.1 patchée Réplique Protectrice (3 PA → 4 PA, 4t → 3t, 40% → 30%, +60 → +80)
- [ ] Editor tool `PopulateSpellDefinitions` qui scan `SpellRegistry.cs` + descriptions Bible patchée et génère les 75 `.asset`
- [ ] QA : sample 5-10 sorts vérifier match UI vs combat runtime

---

**Audit complété : 2026-05-17**
**État : ✅ Cohérent | ⚠️ 2 items mineurs à back-porter | ❌ Aucune dérive critique**
