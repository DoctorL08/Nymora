using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Hub
{
    /// <summary>
    /// Tooltip world-space affiche au survol d'un HubAvatar. Affiche le nom du joueur
    /// (email pour le local, "Joueur <sub>" pour les remotes) fixe au-dessus du sprite.
    ///
    /// Auto-cree au demarrage de la scene hub via [RuntimeInitializeOnLoadMethod] :
    /// pas besoin d'ajouter manuellement le component dans la scene.
    ///
    /// Detection sprite-based (mirror du TileHoverView combat) : iterate tous les
    /// HubAvatar actifs, check si mouse worldPos est dans les bounds du SpriteRenderer
    /// du child Visual. Si plusieurs avatars chevauchent visuellement, le plus haut
    /// sortingOrder gagne.
    /// </summary>
    public sealed class HubAvatarHoverTooltip : MonoBehaviour
    {
        public static HubAvatarHoverTooltip Instance { get; private set; }

        // Offset Y appliquee au-dessus du top du sprite hovered (en unites world).
        // 0.45 = degage le sprite + le cadre noir reste lisible sans chevauchement.
        private const float YOffsetAboveSprite = 0.45f;
        // Padding interne du cadre noir autour du texte (en unites world).
        private const float BgPaddingX = 0.18f;
        private const float BgPaddingY = 0.08f;

        private TextMeshPro _tooltipText;
        private SpriteRenderer _tooltipBg;
        private HubAvatar _currentHovered;
        private Camera _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // POLISH-7 polish (20 mai) — AfterSceneLoad ne se declenche qu'au boot du jeu.
            // Sans listener sceneLoaded, au retour combat -> hub le tooltip etait detruit en
            // combat (pas de DontDestroyOnLoad) et jamais recree -> survol avatars sans tooltip.
            // On (re)cree maintenant a chaque scene loaded qui matche un nom de scene hub.
            SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            SceneManager.sceneLoaded += OnSceneLoadedStatic;
            TryCreateForActiveScene();
        }

        private static void OnSceneLoadedStatic(Scene scene, LoadSceneMode mode)
        {
            TryCreateForActiveScene();
        }

        private static void TryCreateForActiveScene()
        {
            if (Instance != null) return;
            // 19 mai — Garde scene : ne s'auto-cree QUE dans la scene hub. Sinon en combat
            // (30_CombatIA / 33_CombatCasual / 40-42 ranked) le tooltip hub etait instancie
            // a vide + son Update tournait pour rien chaque frame. Le combat a son propre
            // CombatantTooltipView (Combat.View.HUD).
            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Contains("Hub")) return;
            var hostGo = new GameObject("HubAvatarHoverTooltip");
            hostGo.AddComponent<HubAvatarHoverTooltip>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SpawnTooltipGO();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void SpawnTooltipGO()
        {
            // Conteneur racine : un GO "Tooltip" qui hoste le bg + le texte. On active/desactive
            // le conteneur d'un coup (les deux suivent ensemble).
            var rootGo = new GameObject("Tooltip");
            rootGo.transform.SetParent(transform, false);

            // Background : SpriteRenderer noir semi-transparent. Le sprite est un carre uni
            // genere en code (1x1 px blanc, teinte au runtime).
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(rootGo.transform, false);
            _tooltipBg = bgGo.AddComponent<SpriteRenderer>();
            _tooltipBg.sprite = CreateWhitePixelSprite();
            _tooltipBg.color = new Color(0f, 0f, 0f, 0.75f); // noir semi-transparent
            _tooltipBg.sortingOrder = 4999;
            // Drawmode Simple : on scale le transform pour ajuster la taille du cadre selon le texte.

            // Texte
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(rootGo.transform, false);
            _tooltipText = textGo.AddComponent<TextMeshPro>();
            _tooltipText.fontSize = 2.2f; // plus petit que la version precedente (3.2)
            _tooltipText.color = new Color(1f, 0.96f, 0.85f, 1f);
            _tooltipText.alignment = TextAlignmentOptions.Center;
            _tooltipText.fontStyle = FontStyles.Bold;
            _tooltipText.sortingOrder = 5000;
            _tooltipText.enableWordWrapping = false;
            _tooltipText.raycastTarget = false;
            _tooltipText.richText = true; // pour la ligne clan rouge en pre-pseudo
            var rt = (RectTransform)textGo.transform;
            rt.sizeDelta = new Vector2(6f, 1f);

            rootGo.SetActive(false);
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            HubAvatar hovered = FindAvatarAtMouse(mouseWorld);
            Transform tooltipRoot = _tooltipText != null ? _tooltipText.transform.parent : null;
            if (hovered != _currentHovered)
            {
                _currentHovered = hovered;
                if (hovered == null)
                {
                    if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(false);
                }
                else
                {
                    if (_tooltipText != null)
                    {
                        _tooltipText.text = ResolveDisplayName(hovered);
                        // Force ForceMeshUpdate pour avoir preferredWidth a jour cette frame.
                        _tooltipText.ForceMeshUpdate(true);
                        ResizeBackgroundToText();
                        if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(true);
                    }
                }
            }

            // Reposition chaque frame tant qu'un avatar est hovered (le sprite bouge si l'avatar walk).
            if (_currentHovered != null && tooltipRoot != null && tooltipRoot.gameObject.activeSelf)
            {
                var sr = _currentHovered.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    Vector3 pos = sr.bounds.center;
                    pos.y = sr.bounds.max.y + YOffsetAboveSprite;
                    pos.z = 0f;
                    tooltipRoot.position = pos;
                }
            }
        }

        /// <summary>
        /// Resize le sprite bg pour qu'il englobe le texte avec un peu de padding.
        /// Appele a chaque changement de texte (i.e. nouveau avatar hovered).
        /// </summary>
        private void ResizeBackgroundToText()
        {
            if (_tooltipBg == null || _tooltipText == null) return;
            float w = _tooltipText.preferredWidth + BgPaddingX * 2f;
            float h = _tooltipText.preferredHeight + BgPaddingY * 2f;
            // Le sprite source fait 1x1 unit, on scale le transform pour la taille voulue.
            _tooltipBg.transform.localScale = new Vector3(w, h, 1f);
            _tooltipBg.transform.localPosition = Vector3.zero;
        }

        private static HubAvatar FindAvatarAtMouse(Vector3 mouseWorld)
        {
            var avatars = Object.FindObjectsByType<HubAvatar>(FindObjectsSortMode.None);
            HubAvatar best = null;
            int bestSortingOrder = int.MinValue;
            for (int i = 0; i < avatars.Length; i++)
            {
                var a = avatars[i];
                if (a == null || !a.isActiveAndEnabled) continue;
                var sr = a.GetComponentInChildren<SpriteRenderer>();
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                Bounds b = sr.bounds;
                if (mouseWorld.x < b.min.x || mouseWorld.x > b.max.x) continue;
                if (mouseWorld.y < b.min.y || mouseWorld.y > b.max.y) continue;
                if (sr.sortingOrder > bestSortingOrder)
                {
                    bestSortingOrder = sr.sortingOrder;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// Resoud le contenu du tooltip pour un avatar : pseudo (Profile.displayName) lu via
        /// le [Networked] NetDisplayName de HubAvatar. Si vide (race au Spawn), fallback "?".
        /// Si le joueur est dans un clan (NetClanName non vide), prepend une ligne rouge.
        ///
        /// POLISH-7 (20 mai) : avant cette brique, le local utilisait email.split('@')[0] et
        /// les remotes affichaient "Joueur <sub raccourci>". Maintenant single source = backend.
        /// </summary>
        private static string ResolveDisplayName(HubAvatar avatar)
        {
            string pseudo = avatar.NetDisplayName.ToString();
            if (string.IsNullOrEmpty(pseudo)) pseudo = "?";

            string clanName = avatar.NetClanName.ToString();
            string clanLine = string.IsNullOrEmpty(clanName)
                ? ""
                : $"<color=#e85060>[{clanName}]</color>\n";
            return clanLine + pseudo;
        }
    }
}
