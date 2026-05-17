using System.Text;
using Quantum;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Brique 3.E.3 — Overlay debug textuel. Lit le frame Quantum verifie en
    /// lecture seule et formate un dump compact des infos non visibles dans
    /// le HUD normal :
    ///
    /// Toggle clavier (F11) supprime a la cloture polish Phase 3 (17 mai 2026).
    /// Visibilite controlee via `_showAtStart` au start, ou via Inspector
    /// (toggle SetActive sur le panel) en Play Mode.
    ///   - Header : Tick / Round / Phase / ActivePlayer / Winner
    ///   - Combatants : PlayerIndex, Class, EntityRef, Pos, HP/PA/PM/Resource,
    ///                  statuses textuels (Kind + Magnitude + TurnsLeft), marque
    ///   - Obstacles : Kind, EntityRef, Pos, HP/MaxHP, Owner, ExpiresOnTurn
    ///
    /// Marche aussi pendant un replay (meme pipeline View).
    /// Zero impact simu : lecture seule, hors-Update Quantum.
    /// </summary>
    public class CombatDebugOverlay : MonoBehaviour
    {
        [Tooltip("Panel racine a toggle (background + texte). Si null, on toggle le GameObject porteur.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("TMP_Text monospace ou s'affiche le dump.")]
        [SerializeField] private TMP_Text _content;

        [Tooltip("Affichage initial au start de la scene.")]
        [SerializeField] private bool _showAtStart = false;

        private readonly StringBuilder _sb = new StringBuilder(4096);
        private bool _visible;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
            SetVisible(_showAtStart);
        }

        private void SetVisible(bool on)
        {
            _visible = on;
            // Si _panel est un GameObject distinct, on toggle directement.
            if (_panel != null && _panel != gameObject)
            {
                _panel.SetActive(on);
                return;
            }
            // Sinon (_panel == self ou null) : on NE PEUT PAS faire SetActive(false)
            // sur le GameObject porteur — ca tuerait Update() et empecherait le retoggle.
            // On hide les enfants + le background Image a la place.
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(on);
            }
            var bg = GetComponent<UnityEngine.UI.Image>();
            if (bg != null) bg.enabled = on;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_visible || _content == null) return;
            var frame = game.Frames.Verified;
            if (frame == null) return;

            _sb.Clear();
            AppendHeader(_sb, frame);
            AppendCombatants(_sb, frame);
            AppendObstacles(_sb, frame);
            _content.text = _sb.ToString();
        }

        private static void AppendHeader(StringBuilder sb, Frame frame)
        {
            sb.AppendFormat("Tick {0}", frame.Number);
            if (frame.TryGetSingleton<CombatState>(out var state))
            {
                sb.AppendFormat(" | Round {0} (sub {1}) | Phase {2} | Active P{3} | Winner {4}\n",
                    state.TurnNumber, state.SubTurnInRound, state.CurrentPhase, state.ActivePlayerIndex,
                    state.WinnerPlayerIndex < 0 ? "—" : "P" + state.WinnerPlayerIndex);
                sb.AppendFormat("Timer ticks {0}\n", state.TurnTimerTicks);
            }
            else
            {
                sb.AppendLine(" | CombatState absent");
            }
            sb.AppendLine();
        }

        private static void AppendCombatants(StringBuilder sb, Frame frame)
        {
            sb.AppendLine("=== Combatants ===");
            int count = 0;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef e, out Combatant c))
            {
                count++;
                sb.AppendFormat("[P{0}] {1}  E#{2}  Pos({3},{4})\n",
                    c.PlayerIndex, c.Class, e.Index, c.GridX, c.GridY);
                sb.AppendFormat("    HP {0}/{1}  PA {2}/{3}  PM {4}/{5}  Res {6}\n",
                    c.HP, c.MaxHP, c.PA, c.MaxPA, c.PM, c.MaxPM, c.Resource);

                bool anyStatus = false;
                for (int i = 0; i < c.Statuses.Length; i++)
                {
                    var s = c.Statuses[i];
                    if (s.Kind == StatusKind.None) continue;
                    if (!anyStatus) { sb.Append("    Statuses: "); anyStatus = true; }
                    else sb.Append(", ");
                    sb.AppendFormat("{0}({1}T,M{2})", s.Kind, s.TurnsLeft, s.Magnitude);
                }
                if (anyStatus) sb.AppendLine();

                if (c.CurrentMark != MarkKind.None)
                {
                    sb.AppendFormat("    Mark: {0} ({1}T, owner=P{2})\n",
                        c.CurrentMark, c.MarkTurnsLeft, c.MarkOwnerPlayer);
                }
                if (c.VeninStacks > 0)
                {
                    sb.AppendFormat("    Venin: {0}/{1} marques (lastTick={2})\n",
                        c.VeninStacks, Quantum.VeninHelpers.MaxStacksPerTarget, c.LastVeninTickOnTurn);
                }
                // 3.6 — Ghostra : affiche le nombre de leurres actifs + l'Angle Mort + cooldown Permutation.
                if (c.Class == NymoraClass.Ghostra)
                {
                    int activeDecoys = 0;
                    for (int d = 0; d < 3; d++)
                    {
                        if (c.Decoys[d].Kind != DecoyKind.None) activeDecoys++;
                    }
                    int angle = activeDecoys >= 3 ? 3 : (activeDecoys >= 1 ? 2 : 1);
                    sb.AppendFormat("    Decoys: {0}/3  Angle {1}  lastPermut={2}\n",
                        activeDecoys, angle, c.LastPermutationOnTurn);
                    for (int d = 0; d < 3; d++)
                    {
                        if (c.Decoys[d].Kind == DecoyKind.None) continue;
                        sb.AppendFormat("      [{0}] {1} pos=({2},{3}) spawned@{4} hp={5}\n",
                            d, c.Decoys[d].Kind, c.Decoys[d].PosX, c.Decoys[d].PosY,
                            c.Decoys[d].SpawnedOnTurn, c.Decoys[d].HP);
                    }
                }
                sb.AppendLine();
            }
            if (count == 0) sb.AppendLine("  (none)");
        }

        private static void AppendObstacles(StringBuilder sb, Frame frame)
        {
            sb.AppendLine("=== Obstacles ===");
            int count = 0;
            var filter = frame.Filter<Obstacle>();
            while (filter.Next(out EntityRef e, out Obstacle o))
            {
                count++;
                sb.AppendFormat("  {0}  E#{1}  Pos({2},{3})  HP {4}/{5}  Owner=P{6}  Exp@{7}\n",
                    o.Kind, e.Index, o.GridX, o.GridY, o.HP, o.MaxHP, o.OwnerPlayerIndex,
                    o.ExpiresOnTurn == 0 ? "perm" : o.ExpiresOnTurn.ToString());
            }
            if (count == 0) sb.AppendLine("  (none)");
        }
    }
}
