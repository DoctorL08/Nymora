using Nymora.Core.Data;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Tooltip world-space affiche au survol d'un combatant en combat (IA ou PvP).
    /// 3 lignes (haut -> bas) :
    ///   - Clan en rouge (uniquement si dans un clan)
    ///   - Pseudo en blanc
    ///   - HP en jaune
    ///
    /// Pattern mirror du HubAvatarHoverTooltip : auto-instancie via
    /// [RuntimeInitializeOnLoadMethod] uniquement dans les scenes combat. AUCUN
    /// SerializeField -> impossible de subir le re-bind YAML legacy qui avait cause
    /// le bug "tooltip gigantesque" (cf historique fix 19 mai).
    ///
    /// Content rebuild a chaque frame tant que visible -> HP se met a jour en live pendant
    /// qu'on survole (utile pour pre-cast feedback en POLISH-6a). Skip rebuild si meme texte.
    ///
    /// Drive par TileHoverView qui appelle Show/Hide via Instance.
    /// </summary>
    public class CombatantTooltipView : MonoBehaviour
    {
        public static CombatantTooltipView Instance { get; private set; }

        // Defaults calibres pour la camera combat orthoSize 3.5 (hauteur ecran 7 units world).
        // fontSize 1.0 = ~14% de la hauteur ecran -> bien lisible au-dessus du sprite combatant.
        // Constantes uniquement -> impossible d'override par YAML stale.
        private const float FontSize = 1.0f;
        private const float YOffsetAboveSprite = 0.40f;
        private const float BgPaddingX = 0.30f;
        private const float BgPaddingY = 0.10f;
        private const int BgSortingOrder = 4999;
        private const int TextSortingOrder = 5000;

        private TextMeshPro _tooltipText;
        private SpriteRenderer _tooltipBg;
        private GameObject _tooltipRoot;
        private Transform _anchoredSprite;
        private bool _visible;
        // 19 mai — entity courante tracked pour refresh live du content (HP + event line)
        // pendant que la souris reste sur le combattant. Sinon le tooltip ne montrerait que
        // le HP/event au moment du Show initial et ne se rafraichirait pas si un coup tombe.
        private EntityRef _currentEntity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            // Skip si pas dans une scene combat (overhead nul en hub/menu).
            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Contains("Combat")) return;
            var hostGo = new GameObject("CombatantTooltipView");
            hostGo.AddComponent<CombatantTooltipView>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SpawnTooltipGO();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void SpawnTooltipGO()
        {
            _tooltipRoot = new GameObject("CombatantTooltipRoot");
            _tooltipRoot.transform.SetParent(transform, false);
            // Force inactif des le spawn : sinon le carre noir + sprite est visible au centre
            // du monde (0,0,0) jusqu'au premier hover.
            _tooltipRoot.SetActive(false);

            // Background : SpriteRenderer noir semi-transparent, scale ajuste a chaque Show().
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_tooltipRoot.transform, false);
            _tooltipBg = bgGo.AddComponent<SpriteRenderer>();
            _tooltipBg.sprite = CreateWhitePixelSprite();
            _tooltipBg.color = new Color(0f, 0f, 0f, 0.78f);
            _tooltipBg.sortingOrder = BgSortingOrder;

            // Texte
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(_tooltipRoot.transform, false);
            _tooltipText = textGo.AddComponent<TextMeshPro>();

            // CRITICAL 19 mai — fontSize DOIT etre setee EN PREMIER. Bug observe : l'assignation
            // `_tooltipText.font = TMP_Settings.defaultFontAsset` peut crash en NullReferenceException
            // dans LoadFontAsset() en early Awake (TMP_Settings pas encore pret). L'exception
            // interrompait SpawnTooltipGO -> fontSize restait au default TMP (36 world units) ->
            // tooltip gigantesque (5x la hauteur ecran a orthoSize 3.5). En settant fontSize
            // AVANT toute autre operation potentiellement explosive, on garantit que la valeur
            // const est appliquee meme si TMP crash plus loin.
            _tooltipText.fontSize = FontSize;
            _tooltipText.color = Color.white;
            _tooltipText.alignment = TextAlignmentOptions.Center;
            _tooltipText.fontStyle = FontStyles.Bold;
            _tooltipText.sortingOrder = TextSortingOrder;
            _tooltipText.enableWordWrapping = false;
            _tooltipText.raycastTarget = false;
            _tooltipText.richText = true;
            var rt = (RectTransform)textGo.transform;
            rt.sizeDelta = new Vector2(6f, 2f);

            // Font asset : wrap en try/catch defensif. Si le default font est pas dispo a ce
            // stade, on laisse TMP utiliser son fallback interne — le tooltip rendra peut-etre
            // avec une font generique mais la fontSize reste correcte (= pas gigantesque).
            try
            {
                if (_tooltipText.font == null && TMPro.TMP_Settings.defaultFontAsset != null)
                {
                    _tooltipText.font = TMPro.TMP_Settings.defaultFontAsset;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CombatantTooltipView] Skip font assignment (TMP not ready): {ex.Message}");
            }
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// Affiche le tooltip 3 lignes au-dessus du sprite combatant.
        /// </summary>
        public void Show(EntityRef entity, int hpCurrent, int hpMax, Transform anchorSprite)
        {
            if (_tooltipText == null || _tooltipRoot == null) return;
            // PATCH 22 mai (test designer) — Voile d'Ombre : un Nightseer Untargetable est
            // invisible cote adversaire (cf CombatantRenderer.SetCloaked). On ne doit donc
            // pas reveler sa presence via le tooltip de survol si l'adversaire devine sa case.
            if (IsCloakedFromLocalViewer(entity)) { Hide(); return; }
            _currentEntity = entity;
            ApplyContent(BuildContent(entity, hpCurrent, hpMax));
            _anchoredSprite = anchorSprite;
            _tooltipRoot.SetActive(true);
            _visible = true;
            UpdatePosition();
        }

        public void Hide()
        {
            if (_tooltipRoot != null && _visible)
            {
                _tooltipRoot.SetActive(false);
            }
            _visible = false;
            _anchoredSprite = null;
            _currentEntity = default;
        }

        private void Update()
        {
            if (!_visible) return;
            RefreshLiveContent();
            UpdatePosition();
        }

        /// <summary>
        /// Re-query HP/MaxHP + event courants du combattant et rebuild le content. Permet au
        /// tooltip de refleter en live les degats/soins/shield qui tombent pendant qu'on le
        /// survole, sans avoir a re-hover.
        /// </summary>
        private void RefreshLiveContent()
        {
            if (_currentEntity == default) return;
            if (!TryGetCombatantSnapshot(_currentEntity, out int hp, out int maxHp))
            {
                // Entity disparue (KO + cleanup) -> tooltip cache.
                Hide();
                return;
            }
            // PATCH 22 mai — si le combattant survole devient Untargetable (Voile d'Ombre)
            // pendant qu'on le survole cote adversaire, on cache le tooltip immediatement.
            if (IsCloakedFromLocalViewer(_currentEntity)) { Hide(); return; }
            ApplyContent(BuildContent(_currentEntity, hp, maxHp));
        }

        private void ApplyContent(string content)
        {
            if (_tooltipText == null) return;
            // Skip rebuild si meme texte (perf-friendly : evite TMP ForceMeshUpdate + bg resize
            // a chaque frame quand rien ne change).
            if (_tooltipText.text == content) return;
            _tooltipText.text = content;
            _tooltipText.ForceMeshUpdate(true);
            ResizeBackgroundToText();
        }

        private static bool TryGetCombatantSnapshot(EntityRef entity, out int hp, out int maxHp)
        {
            hp = 0; maxHp = 0;
            var runner = QuantumRunner.Default;
            if (runner == null || runner.Game == null) return false;
            var frame = runner.Game.Frames.Verified;
            if (!frame.Exists(entity) || !frame.Has<Combatant>(entity)) return false;
            var c = frame.Get<Combatant>(entity);
            hp = c.HP;
            maxHp = c.MaxHP;
            return true;
        }

        private void UpdatePosition()
        {
            if (_anchoredSprite == null || _tooltipRoot == null) return;
            var sr = _anchoredSprite.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            Vector3 pos = sr.bounds.center;
            pos.y = sr.bounds.max.y + YOffsetAboveSprite;
            pos.z = 0f;
            _tooltipRoot.transform.position = pos;
        }

        private void ResizeBackgroundToText()
        {
            if (_tooltipBg == null || _tooltipText == null) return;
            float w = _tooltipText.preferredWidth + BgPaddingX * 2f;
            float h = _tooltipText.preferredHeight + BgPaddingY * 2f;
            _tooltipBg.transform.localScale = new Vector3(w, h, 1f);
            _tooltipBg.transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Construit le texte 3 lignes en RichText.
        /// </summary>
        private static string BuildContent(EntityRef entity, int hp, int maxHp)
        {
            int slot = ResolvePlayerIndex(entity);
            int localSlot = LocalPlayerResolver.Resolve();
            bool isLocal = (slot == localSlot);

            string pseudo = isLocal ? PlayerProfileBridge.LocalPseudo : PlayerProfileBridge.OpponentPseudo;
            string clan   = isLocal ? PlayerProfileBridge.LocalClan   : PlayerProfileBridge.OpponentClan;

            if (string.IsNullOrEmpty(pseudo))
            {
                pseudo = isLocal ? "Joueur" : "Bot";
            }

            string clanLine = string.IsNullOrEmpty(clan)
                ? ""
                : $"<color=#e85060>[{clan}]</color>\n";
            string pseudoLine = $"<color=#ffffff>{pseudo}</color>\n";
            string hpLine = $"<color=#ffd060>HP : {hp} / {maxHp}</color>";
            string previewLine = BuildSpellPreviewLine(entity);
            return clanLine + pseudoLine + hpLine + previewLine;
        }

        /// <summary>
        /// POLISH-6a (19 mai 2026) — Ligne preview du sort arme sur la cible survolee.
        /// Affiche "- X degats" (rouge) / "+ X soins" (vert) / "+ X bouclier" (bleu) sous les HP.
        /// Si un shield absorbe une partie/tout du coup, suffix " (Y absorbes)" ou " (absorbes)".
        ///
        /// No-op si :
        ///   - aucun sort arme (CombatHUDController.Instance.ArmedSpell == null)
        ///   - pas de runner/frame Quantum
        ///   - sort pas encore supporte par SpellPreview (POLISH-6a = Frappe Fantome only)
        ///   - caster local introuvable dans le frame
        /// </summary>
        private static string BuildSpellPreviewLine(EntityRef target)
        {
            var hud = CombatHUDController.Instance;
            if (hud == null || !hud.ArmedSpell.HasValue) return "";
            SpellId armed = hud.ArmedSpell.Value;

            var runner = QuantumRunner.Default;
            if (runner == null || runner.Game == null) return "";
            var frame = runner.Game.Frames.Verified;
            if (frame == null) return "";

            int localSlot = LocalPlayerResolver.Resolve();
            EntityRef casterEntity = ResolveCombatantByPlayerIndex(frame, localSlot);
            if (casterEntity == default) return "";

            if (!SpellPreview.TryCompute(frame, casterEntity, target, armed, out var p)) return "";
            if (!p.Valid) return "";

            if (p.FinalDamage > 0)
            {
                if (p.AbsorbedByShield > 0 && p.HpLost > 0)
                    return $"\n<color=#ff7060>- {p.FinalDamage} degats ({p.AbsorbedByShield} absorbes)</color>";
                if (p.AbsorbedByShield > 0)
                    return $"\n<color=#ff7060>- {p.FinalDamage} degats (absorbes)</color>";
                return $"\n<color=#ff7060>- {p.FinalDamage} degats</color>";
            }
            if (p.Healed > 0)        return $"\n<color=#80ff80>+ {p.Healed} soins</color>";
            if (p.ShieldGained > 0)  return $"\n<color=#80c0ff>+ {p.ShieldGained} bouclier</color>";
            return "";
        }

        private static EntityRef ResolveCombatantByPlayerIndex(Frame frame, int playerIndex)
        {
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef e, out Combatant c))
            {
                if (c.PlayerIndex == playerIndex) return e;
            }
            return default;
        }

        /// <summary>
        /// PATCH 22 mai (test designer) — True si `entity` est un combattant ENNEMI du viewer
        /// local, vivant, et porteur de StatusKind.Untargetable (Voile d'Ombre Nightseer).
        /// Dans ce cas le tooltip ne doit pas s'afficher (invisibilite cote adversaire). Le
        /// Nightseer voit toujours son propre tooltip (own unit -> false).
        /// </summary>
        private static bool IsCloakedFromLocalViewer(EntityRef entity)
        {
            var runner = QuantumRunner.Default;
            if (runner == null || runner.Game == null) return false;
            var frame = runner.Game.Frames.Verified;
            if (frame == null || !frame.Exists(entity) || !frame.Has<Combatant>(entity)) return false;

            var c = frame.Get<Combatant>(entity);
            if (c.HP <= 0) return false;
            if (c.PlayerIndex == LocalPlayerResolver.Resolve()) return false; // sa propre unite

            for (int s = 0; s < StatusHelper.SlotCount; s++)
            {
                if (c.Statuses[s].Kind == StatusKind.Untargetable && c.Statuses[s].TurnsLeft > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static int ResolvePlayerIndex(EntityRef entity)
        {
            var runner = QuantumRunner.Default;
            if (runner == null || runner.Game == null) return -1;
            var frame = runner.Game.Frames.Verified;
            if (!frame.Exists(entity) || !frame.Has<Combatant>(entity)) return -1;
            return frame.Get<Combatant>(entity).PlayerIndex;
        }
    }
}
