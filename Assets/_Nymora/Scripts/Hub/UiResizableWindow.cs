using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Rend un panneau UI déplaçable (drag sur son fond) et redimensionnable (poignée en haut-droite).
    /// Position + taille persistées en PlayerPrefs (clé = nom du GameObject). Clampé à [min,max] et
    /// gardé à l'écran. Posé sur le fond du chat par le tool de restyle.
    ///
    /// Le déplacement n'intercepte QUE les drags qui démarrent sur le fond (les enfants — scroll,
    /// input, boutons — gèrent leurs propres drags). 100% View.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiResizableWindow : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Vector2 _minSize = new Vector2(340f, 170f);
        [SerializeField] private Vector2 _maxSize = new Vector2(980f, 760f);
        [SerializeField] private float _gripSize = 20f;
        // Largeur min dynamique : la fenêtre ne peut pas devenir plus étroite que le bord droit de
        // cet élément (ex : onglet Clan) + marge, pour ne jamais le couper/le faire dépasser.
        [SerializeField] private RectTransform _minWidthAnchor;
        [SerializeField] private float _minWidthAnchorMargin = 14f;

        private const string PrefPrefix = "nymora.uiwin.";

        private RectTransform _rt;
        private Canvas _canvas;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            BuildGrip();
        }

        private void Start() => LoadState();

        private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

        // ===== Déplacement (drag sur le fond) =====

        public void OnDrag(PointerEventData e)
        {
            _rt.anchoredPosition += e.delta / Scale;
            ClampOnScreen();
        }

        public void OnEndDrag(PointerEventData e) => SaveState();

        // ===== Redimensionnement (poignée haut-droite) =====

        public void Resize(Vector2 deltaScreen)
        {
            Vector2 d = deltaScreen / Scale;
            Vector2 old = _rt.sizeDelta;
            Vector2 ns = new Vector2(
                Mathf.Clamp(old.x + d.x, EffectiveMinWidth(), _maxSize.x),
                Mathf.Clamp(old.y + d.y, _minSize.y, _maxSize.y));
            Vector2 realDelta = ns - old;
            // garde le coin bas-gauche fixe (la fenêtre grandit vers le haut-droite)
            _rt.anchoredPosition += new Vector2(realDelta.x * _rt.pivot.x, realDelta.y * _rt.pivot.y);
            _rt.sizeDelta = ns;
            ClampOnScreen();
        }

        public void SaveStatePublic() => SaveState();

        // Largeur min effective = max(_minSize.x, bord droit de l'ancre relatif au bord gauche du
        // panneau + marge). Garantit que l'ancre (onglet Clan) n'est jamais coupée/dépassée.
        private float EffectiveMinWidth()
        {
            if (_minWidthAnchor == null) return _minSize.x;
            var ac = new Vector3[4]; _minWidthAnchor.GetWorldCorners(ac); // 2 = top-right
            var pc = new Vector3[4]; _rt.GetWorldCorners(pc);             // 0 = bottom-left
            float needed = (ac[2].x - pc[0].x) / Scale + _minWidthAnchorMargin;
            return Mathf.Max(_minSize.x, needed);
        }

        private void BuildGrip()
        {
            var go = new GameObject("ResizeGrip", typeof(RectTransform), typeof(Image), typeof(ResizeGripHandler));
            var grt = (RectTransform)go.transform;
            grt.SetParent(_rt, false);
            grt.anchorMin = new Vector2(1f, 1f); grt.anchorMax = new Vector2(1f, 1f); grt.pivot = new Vector2(1f, 1f);
            grt.sizeDelta = new Vector2(_gripSize, _gripSize);
            grt.anchoredPosition = new Vector2(-2f, -2f);
            var img = go.GetComponent<Image>();
            // Patch UI combat 8 juin (3e) — vraie icône SVG de redimension (3 diagonales propres)
            // au lieu de l'ancien sprite procédural (une seule diagonale). Fallback sur le procédural
            // si le SVG n'est pas (encore) importé en Sprite.
            var gripSprite = Resources.Load<Sprite>("UI/Icons/ui_icon_resize");
            bool hasSvg = gripSprite != null;
            img.sprite = hasSvg ? gripSprite : HubMenuUiRoundedHelper.Corner();
            img.color = new Color(1f, 1f, 1f, hasSvg ? 0.5f : 0.22f);
            img.preserveAspect = true;
            go.GetComponent<ResizeGripHandler>().Init(this);
        }

        // Confine le panneau ENTIÈREMENT dans l'écran (coins en espace monde/écran -> robuste
        // quels que soient les ancrages/pivot). Le joueur ne peut jamais perdre son chat hors champ.
        private void ClampOnScreen()
        {
            if (_canvas == null) return;
            var canvasRT = (RectTransform)_canvas.transform;

            var pc = new Vector3[4]; _rt.GetWorldCorners(pc);      // 0=BL 1=TL 2=TR 3=BR
            var cc = new Vector3[4]; canvasRT.GetWorldCorners(cc);

            float minX = cc[0].x, maxX = cc[2].x, minY = cc[0].y, maxY = cc[2].y;
            float pMinX = pc[0].x, pMaxX = pc[2].x, pMinY = pc[0].y, pMaxY = pc[2].y;

            Vector3 offset = Vector3.zero;
            if (pMinX < minX) offset.x += minX - pMinX;
            else if (pMaxX > maxX) offset.x -= pMaxX - maxX;
            if (pMinY < minY) offset.y += minY - pMinY;
            else if (pMaxY > maxY) offset.y -= pMaxY - maxY;

            if (offset != Vector3.zero) _rt.position += offset;
        }

        // ===== Persistance =====

        private void SaveState()
        {
            string k = PrefPrefix + gameObject.name;
            PlayerPrefs.SetFloat(k + ".px", _rt.anchoredPosition.x);
            PlayerPrefs.SetFloat(k + ".py", _rt.anchoredPosition.y);
            PlayerPrefs.SetFloat(k + ".sw", _rt.sizeDelta.x);
            PlayerPrefs.SetFloat(k + ".sh", _rt.sizeDelta.y);
            PlayerPrefs.Save();
        }

        private void LoadState()
        {
            string k = PrefPrefix + gameObject.name;
            if (!PlayerPrefs.HasKey(k + ".sw")) return;
            var size = new Vector2(
                Mathf.Clamp(PlayerPrefs.GetFloat(k + ".sw"), EffectiveMinWidth(), _maxSize.x),
                Mathf.Clamp(PlayerPrefs.GetFloat(k + ".sh"), _minSize.y, _maxSize.y));
            _rt.sizeDelta = size;
            _rt.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(k + ".px"), PlayerPrefs.GetFloat(k + ".py"));
            ClampOnScreen();
        }

        // Poignée de redimensionnement (enfant haut-droite).
        private sealed class ResizeGripHandler : MonoBehaviour, IDragHandler, IEndDragHandler
        {
            private UiResizableWindow _win;
            public void Init(UiResizableWindow win) => _win = win;
            public void OnDrag(PointerEventData e) { if (_win != null) _win.Resize(e.delta); }
            public void OnEndDrag(PointerEventData e) { if (_win != null) _win.SaveStatePublic(); }
        }
    }

    /// <summary>Petit sprite arrondi pour la poignée (évite une dépendance à HubMenuUIFactory côté runtime).</summary>
    internal static class HubMenuUiRoundedHelper
    {
        private static Sprite _corner;
        public static Sprite Corner()
        {
            if (_corner != null) return _corner;
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // triangle coin haut-droite (diagonale) pour évoquer une poignée
                    bool on = (x + (s - 1 - y)) >= s - 2;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                }
            tex.SetPixels32(px); tex.Apply();
            _corner = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            _corner.name = "ResizeGripSprite";
            return _corner;
        }
    }
}
