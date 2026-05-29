# ⚖️ Refonte d'équilibrage des 5 classes — 29 mai 2026

> **Design figé, PAS encore codé.** Référence d'implémentation pour la grosse brique de rééquilibrage.
> Touche la sim → bump `CombatRulesVersion` 85→86 + rebuild standalone + resync des 3 copies tooltip
> (`HubMenuDeckBuilder.PhasesText` + `HubClassSelectorPanel.BuildPhasesTooltip` + `PassiveTooltipBuilder`)
> + deck builder + SpellDefinition + VFX + IA.
> Détail par classe : fiches mémoire `project_<classe>_refonte_design.md`.
>
> **Légende :** 🆕 nouveau · ✏️ modifié · ⚪ inchangé · « 1× actif » = refresh (pas de stack).

---

## ⚔️ SOULRENDER
**1500 HP · 8 PA · 3 PM · Hémoglyphe (cap 5)**
**Passif Appel du Sang** : <70% = −1 PA sur le 1er sort du tour · <40% = vol de vie 20% · <20% = Le Cri (Sang Coagulé croix 5).
**HG** : +1 inflige / +1 subit (max 1/tour adverse). **Plus d'anti-DoT cleanse** (assumé, le bleed reste menaçant).

| Sort | Effet | Limite/tour | Relance |
|---|---|---|---|
| Tranche-Âme | 3 PA, p1 — 220 (recule 2 si kill) | illimité | — |
| Ouvre-Plaie ✏️ | 2 PA, p1 — 110 (1 HG : 230 + soins/boucliers cible ÷2 1t) | 2× | — |
| Charge Brutale ✏️ | 4 PA, p4 ligne — fonce + 180 + Vapeur Carmin | illimité | — |
| Détonation Sanglante | 4 PA, p4 croix3 — 60 +40/HG (max 260) + Sang Coagulé | illimité | — |
| Éventration 🆕 | 5 PA, p1 — 220 + Plaie Ouverte 50/t × 3t | 1× | — |
| Empoignade ✏️ | 3 PA, p3 — pull CaC + 90 + −2 PM | 1× | — |
| Pacte de Sang | 1 PA — −80 HP, +3 HG, prochain offensif +50% | 1×/match | — |
| Marque de Carnage | 2 PA, p5 — marque 3t (+1 HG sur la cible) | 1× | — |
| Rugissement | 3 PA, rayon 3 — −1 PM (−2 si <50%) + pas de TP | 1× | 2t |
| Frénésie ✏️ | 3 PA — 2t : chaque offensif +1 HG + +10% dmg | 1× actif | — |
| Riposte Carmin | 2 PA — 1t : mêlée subie → 100 + −1 PM | 1× actif | — |
| Sang Bouillant 🆕 | 2 PA — 2t : subir dégâts → +1 HG + prochaine frappe +30 | 1× actif | — |
| Peau de Fer | 3 PA — bouclier 200 (2t) + sorts p1 +30 | 1× actif | **2t** |
| Sève Vive ✏️ | 2 PA — heal 100 (+60 HG, +50 si saigne) | 1× | — |
| Dernier Souffle | 4 PA — <30% HP : heal 200 + 3 HG | 1×/match | — |
| **Âme Lacérée** (signature) | 2 PA, p1, 5/5 HG — 320 + heal 50% + Sang Coagulé si kill | — | 4t |

---

## 🏹 NIGHTSEER
**1500 HP · 8 PA · 3 PM · Prescience (cap 5)**
**Passif phasé sur la Prescience** : P1 (1-2) +15% dégâts piège · P2 (3-4) +30 dmg & portée +1 · P3 (5) ignore 50% bouclier + **pièges invisibles** + Traquenard.
Marque unique **Traqué**. Pièges **visibles par défaut** (invisibles à P3). **PR** : +1 piège posé / +1 piège déclenché / +1 marque (cap +3/tour). Voilé & Empreinté supprimés.

| Sort | Effet | Limite/tour | Relance |
|---|---|---|---|
| Tir Précis | 3 PA, p6 — 200 (280 si Traqué) | illimité | — |
| Volée d'Épines ✏️ | 4 PA, p5 ligne — 130/cible + pose Filet | 1× | — |
| Détonation Onirique ✏️ | 4 PA, p5 AoE 2×2 — 170 (+80 si couvre un piège) | illimité | — |
| Frappe de l'Ombre ✏️ | 4 PA, p3 — 160 (+50 si 3 PM dépensés au dernier tour) + Traqué | illimité | — |
| Salve Mortelle ✏️ | 5 PA, p6 croix5 — 200/120 + chaîne tes embûches + 60/Traqué | 1× | — |
| Marque du Chasseur | 1 PA, p5 — Traqué 3t (+1 PR) | 1× | — |
| Filet de Ronces ✏️ | 2 PA, p4 — piège visible 100 + −1 PM + Traqué | 2× | — |
| Champ de Mines ✏️ | 4 PA, p3 AoE 3×3 — 3 pièges, chaîne 70+40+40 + Traqué | 1× | — |
| Bourrasque ✏️ | 3 PA, p5 — push 3 **direction choisie** → piège | 2× | — |
| Piège Bondissant 🆕 | 2 PA — piège-catapulte directionnel (éjecte dans une dir. choisie) | 1× | — |
| Flèche Traçante 🆕 | 3 PA, p5 — si Traqué : 60/PM dépensé au dernier tour (max 180) | 1× | — |
| Pas Furtif ✏️ | 2 PA — TP 4 + option pose Filet | 1× | — |
| Camouflage Ronces | 3 PA — bouclier 130 + aura 70 + Traqué | 1× actif | — |
| Sève Sauvage ✏️ | 3 PA — heal 130 (+60 si piège déclenché) | 1× | — |
| Évanescence ✏️ | 4 PA — <30% HP : TP 7 + heal 150 + pose piège | 1×/match | — |
| **Traquenard** (signature) | 2 PA, p5, 5/5 PR — TP + 280 + Paralysie (−3 PM, −2 PA) + 80 si Traqué | — | 4t |

> ⚠️ Paralysie −2 PA jugée verrouillante → à arbitrer au playtest.

---

## 🛡️ COLOSSAR
**1500 HP · 8 PA · 3 PM (était 2) · Fondation (cap 5, était 3)**
**Passif Densité Inerte** : −6%/obstacle (cap −18%) + 30 HP/pilier détruit + +20 dmg sorts p1-2 si adjacent. Cap combiné avec Garde Protectrice **−45%**. **Bypass : DoT only** (pas le dorsal).
**FD** : +1 pose Pilier/Mur / +1 ennemi push contre obstacle.

| Sort | Effet | Limite/tour | Relance |
|---|---|---|---|
| Frappe Lourde | 3 PA, p1 — 180 (280 si épinglé) | illimité | — |
| Onde de Choc | 3 PA, p2 AoE1 — 80 + push 2 (+80 + Trauma si mur) | illimité | — |
| Marteau Punisseur | 4 PA, p2 — 160 (240 + Trauma si cible <4 PA) | illimité | — |
| Choc Sismique | 4 PA, p4 ligne — 130 (+50 si traverse son mur) | illimité | — |
| Représailles | 3 PA, p1 — 100 + reflect 80 mêlée 2t | 1× actif | — |
| Pilier | 3 PA, p3 — pilier 200 HP, +1 FD, bloque LoS | 2× | — |
| Mur de Pierre | 4 PA, p4 — mur 3 cases 2t (5 si 1 FD) | 1× | — |
| Ancrage | 2 PA, p4 — −2 PM 2t + immobile 1t | 1× | 2t |
| Provocation | 2 PA, p5 — force à attaquer 1t + −1 PM (+100 si pas adj.) | 1× | — |
| Brisure | 3 PA, p2 — 90 + retire buff/bouclier (Trauma si rien) | 1× | — |
| Stoïcisme | 3 PA — bouclier 200 2t + immobile + heal 80 si survit | 1× actif | **2t** |
| Garde Protectrice ✏️ | 2 PA — −15% dégâts 2t (était −30%) | 1× actif | — |
| Ressac Vital | 2 PA — heal 80 + 30/attaque subie (max 200) | 1× | — |
| Renvoi du Bouclier | 3 PA — reflect 60 (mêlée+distance) 1t | 1× actif | — |
| Éboulement 🆕 | 3 PA, p3 — détruis un Pilier → AoE 150 + push (+30 HP) | 1× | — |
| **Effondrement** (signature) ✏️ | 4 PA, rayon 2, 5/5 FD — **immédiat** : 200 AoE + éjection + Failles 2t + buff (−1 PA, −30%) | — | 4t |

> Effondrement : déclenchement immédiat (plus d'annonce) + retrait du +1 PM. Dégâts restent à 200.
> ⚠️ Sur-buff (3 PM + signature imparable) compensé par le nerf de tankiness — à surveiller au playtest.

---

## 🩸 GHOSTRA
**1500 HP · 8 PA · 3 PM · Rémanence (3 leurres max)**
**Passif Angle Mort** : A1 (0 leurre) neutre · A2 (1-2) +50 dorsal + Plaie · A3 (3) +80 dorsal + **permutation gratuite 0 PA 1×/tour**.

| Sort | Effet | Limite/tour | Relance |
|---|---|---|---|
| Lame Spectrale | 3 PA, p1 — 170 + dorsal + 60 si Plaie | illimité | — |
| Lame Vorace Spectrale | 3 PA, p1 — 130 + 60 si Plaie (non consommée) + dorsal | illimité | — |
| Frappe Fantôme | 4 PA, p4 — TP à 1 case + 200 + dorsal + Plaie si dir. modifiée | illimité | — |
| Saigne-Âme | 4 PA, p2 — 200 + 70 si Plaie (consomme) + heal 60 si kill | illimité | — |
| Nuée Spectrale 🆕 | 4 PA — 100 + 70/leurre + 30/leurre adjacent (max ~400), ne consomme pas | 1× | — |
| Permutation 🆕 | 1 PA — swap avec un leurre (dès 1 leurre) | 2× | — |
| Réplique Fantôme ✏️ | 2 PA, p4 — leurre 4 rounds (+80/+40 HP) | 1× | — |
| Éveil Spectral 🆕 | 2 PA — un leurre poignarde un ennemi adjacent (~100) + dorsal/Plaie | 2× | — |
| Marque de l'Ombre | 2 PA, p4 — +20 dmg 2t + Plaie auto si dorsal | 1× | — |
| Pas dans l'Ombre | 2 PA — TP 5 + pivote ennemi adjacent + leurre optionnel | 1× | — |
| Voile Spectral ✏️ | 2 PA — TP tous tes leurres autour de l'adversaire | 1× | — |
| Linceul d'Ombres | 3 PA — bouclier 130 + reflect 40 mêlée | 1× actif | — |
| Communion Spectrale 🆕 | 2 PA — consomme 1 leurre → heal 150 | 1× | — |
| Réplique Protectrice | 4 PA, p3 — leurre tank 200 HP / 30% / 3 rounds / +80 si détruit | 1× | — |
| Dernier Pas | 4 PA — <30% HP : heal 200 + TP 5 + leurre | 1×/match | — |
| **Exécution Spectrale** (signature) | 3 PA, p1 dorsal, 3/3 leurres — 350 + Plaie 50×3t, heal 100 + 2 leurres si kill (rate si pas dorsal) | — | 4t |

> Supprimés : Volte-Face, Dague Lancée, Danse des Lames, Pas de l'Au-Delà → remplacés ci-dessus.

---

## ☠️ NECRAM
**1500 HP · 8 PA · 3 PM · Putréfaction (cap 6)**
**Passif Floraison** (densité = total marques) : 1-2 → tick 40 · 3-6 → tick 50 + regen 10/marque + halo 20 (r3) · 7+ → tick 60. **Le DoT venin bypasse boucliers ET réductions.** **PT** : +1/tick / +1 marque posée (max 2/tour).

| Sort | Effet | Limite/tour | Relance |
|---|---|---|---|
| Crachat Acide | 3 PA, p4 — 90 + 2 marques (cap 4/cible) | illimité | — |
| Morsure Putride | 4 PA, p1 — 110 + 22/marque (max 200) + transfert si kill | illimité | — |
| Détonation Virulente ✏️ | 4 PA, p4 — tick venin complet instantané, **sans consommer** les marques | 1× | — |
| Faux Décharnée | 4 PA, p1 AoE1 — 130 + heal 30/marque (max +120) | illimité | — |
| Brume Toxique ✏️ | 4 PA, p4 AoE 3×3 — 3t : +1 marque/tour + tick majoré dedans | 1× | — |
| Inoculation | 1 PA, p5 — 2 marques | illimité | — |
| Contagion ✏️ | 3 PA, p5 — cible contagieuse 2t → +1 marque auto/fin de son tour | 1× | — |
| Marque Sacrificielle | 2 PA, p5 — +20 dmg/tick 3t | 1× | — |
| Échange Spectral 🆕 | 2 PA, p5 — swap de place avec l'ennemi + 80 dmg | 1× | — |
| Symbiose Morbide ✏️ | 3 PA — 2t : chaque tick venin → +15 HP (max +60/tour) | 1× actif | — |
| Nuée de Spores 🆕 | 3 PA — 2t : tous tes sorts +1 marque bonus | 1× actif | — |
| Carapace Visqueuse | 3 PA — bouclier 110 2t + attaquant mêlée → 1 marque | 1× actif | — |
| Drain Vital ✏️ | 3 PA, p4 — 40 HP/marque (max 160 heal) + 40 dmg | 1× | — |
| Régénération Nécrotique | 2 PA — heal 70 + 15/marque r4 (max +90), +30 si 1 PT | 1× | — |
| Cocon Putride | 4 PA — <30% HP : heal 220 + 1 marque ennemis r4 | 1×/match | — |
| **Virus Fatal** (signature) ✏️ | 2 PA, p5, 6/6 PT — toutes les marques tiquent ×2.5 (~500) | — | 4t |

> Détonation Virulente ne consomme plus les marques · clock renforcé 40/50/60 · Floraison palier 2 dès densité 3 · Virus Fatal ×3→×2.5.

---

## Ordre d'implémentation conseillé
Du moins risqué (tweaks) au plus neuf (sorts inédits) : **Soulrender → Colossar → Necram → Nightseer → Ghostra.** Une classe = une sous-brique validée en Play Mode avant la suivante.
