using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Hub
{
    /// <summary>
    /// Tooltip world-space affiché au survol d'un HubAvatar — version BANDEAU ORNEMENTAL "ruban".
    ///
    /// Compose (world-space, sprites SVG teintés + TextMeshPro) :
    ///   - une plaque centrale 9-sliced (pseudo en gros),
    ///   - deux bouts de ruban (gauche + droit miroir) qui encadrent la plaque,
    ///   - le NOM DE CLAN encadré par une fioriture gauche + son miroir droit (— ◆ CLAN ◆ —),
    ///   - le titre équipé sous la plaque.
    /// Tous les textes ont un contour noir (outline TMP) pour ressortir sur le bandeau.
    ///
    /// Les pièces (bout/fioriture) et couleurs sont des DÉFAUTS pour l'instant — la
    /// customisation par clan (réseau des pièces/couleurs choisies) viendra avec le backend.
    /// Re-skinnable plus tard par les bannières de la boutique.
    ///
    /// Auto-créé dans la scène hub via [RuntimeInitializeOnLoadMethod] (mirror de l'ancienne
    /// version). Détection survol sprite-based inchangée.
    /// </summary>
    public sealed class HubAvatarHoverTooltip : MonoBehaviour
    {
        public static HubAvatarHoverTooltip Instance { get; private set; }

        // Sorting : même layer que les avatars ("Personnages") pour que l'ordre élevé place
        // bien le bandeau DEVANT les persos (le sortingOrder ne départage qu'au sein d'un layer).
        private const string TooltipSortingLayer = "Personnages";
        private const float YOffsetAboveSprite = 0.30f;

        // ===== Pièces + couleurs (défaut si clan sans config / fetch en cours) =====
        private const string BannerResRoot = "UI/Icons/Banner/";
        private const string DefaultEndKey = "pennon";
        private const string DefaultFlourishKey = "diamond";
        private static readonly Color OrnamentColor = new Color(0.83f, 0.69f, 0.36f, 1f);

        // Cache config bandeau par nom de clan (statique : survit aux recréations de scène).
        private struct BannerConfig { public string End; public string Flourish; public string ColorHex; }
        private static readonly Dictionary<string, BannerConfig> _bannerCache = new Dictionary<string, BannerConfig>();
        private static readonly HashSet<string> _pendingFetch = new HashSet<string>();
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private NymoraApiClient _api;
        private static readonly Color PlateColor = new Color(0.07f, 0.07f, 0.09f, 0.93f);
        private static readonly Color PseudoColor = new Color(1f, 0.97f, 0.90f, 1f);
        private static readonly Color ClanColor = new Color(0.96f, 0.84f, 0.58f, 1f);
        private static readonly Color TitleColor = new Color(1f, 0.85f, 0.20f, 1f);

        // ===== Métriques =====
        // Police en points TMP ; le RESTE est dérivé de la HAUTEUR RÉELLEMENT RENDUE du texte
        // (preferredHeight) — sinon les sprites (unités monde brutes) écrasent le texte.
        private const float PseudoFont = 2.7f;
        private const float ClanFont = 1.8f;
        private const float TitleFont = 1.8f;
        // Ratios (× hauteur de texte rendue) :
        private const float PlatePadXFactor = 0.95f;  // × hauteur pseudo, padding horizontal (plaque large = rectangulaire)
        private const float PlatePadYFactor = 0.28f;  // × hauteur pseudo, padding vertical
        private const float LineGapFactor = 0.12f;    // × hauteur pseudo, écart entre lignes
        private const float EndSizeFactor = 0.6f;     // × hauteur plaque (ruban ~ moitié du tooltip)
        private const float EndWidthFactor = 0.7f;    // écrase la largeur du ruban (moins long)
        private const float EndOverlapFactor = 0.20f; // × hauteur plaque
        private const float FlourishHeightFactor = 0.55f; // × hauteur clan
        private const float FlourishWidthFactor = 0.7f;   // écrase la largeur des fioritures (moins long)
        private const float FlourishGapFactor = 0.35f;    // × hauteur clan
        private const float LisereThicknessFactor = 0.045f; // × hauteur plaque
        // Bannière cosmétique (emblème) posée au-dessus de la plaque.
        private const string CosmeticBannerResRoot = "UI/Cosmetics/Banners/";
        private const float BannerHeightFactor = 4.2f;    // × hauteur pseudo rendue
        private const float BannerOverlapFactor = 0.22f;  // × hauteur bannière (chevauche le haut de la plaque)

        private Transform _root;
        private SpriteRenderer _plate, _lisereTop, _lisereBottom, _endL, _endR, _flourishL, _flourishR;
        private SpriteRenderer _cosmeticBanner; // emblème cosmétique au-dessus de la plaque
        private TextMeshPro _clanText, _pseudoText, _titleText;

        private HubAvatar _currentHovered;
        private Camera _camera;
        private float _bottomExtent = 0.5f; // recalculé dans RefreshLayout

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            SceneManager.sceneLoaded += OnSceneLoadedStatic;
            TryCreateForActiveScene();
        }

        private static void OnSceneLoadedStatic(Scene scene, LoadSceneMode mode) => TryCreateForActiveScene();

        private static void TryCreateForActiveScene()
        {
            if (Instance != null) return;
            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Contains("Hub")) return;
            var hostGo = new GameObject("HubAvatarHoverTooltip");
            hostGo.AddComponent<HubAvatarHoverTooltip>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildBanner();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===== Construction (une fois) =====

        private void BuildBanner()
        {
            var rootGo = new GameObject("Tooltip");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            int layer = SortingLayer.NameToID(TooltipSortingLayer);

            // Plaque RECTANGULAIRE (pixel blanc teinté, dimensionnée via localScale -> coins nets).
            var px = CreateWhitePixelSprite();
            _plate = MakeSprite("Plate", px, PlateColor, layer, 4997);

            // Liserés haut/bas (pixel blanc teinté ornement).
            _lisereTop = MakeSprite("LisereTop", px, OrnamentColor, layer, 4999);
            _lisereBottom = MakeSprite("LisereBottom", px, OrnamentColor, layer, 4999);

            // Bouts de ruban (le droit = miroir via scale.x négatif). Sprite par défaut ;
            // remplacé par la config du clan dans RefreshLayout/ApplyBannerConfig.
            var endSprite = LoadBannerSprite("ui_banner_end_", DefaultEndKey);
            _endL = MakeSprite("EndL", endSprite, OrnamentColor, layer, 4998);
            _endR = MakeSprite("EndR", endSprite, OrnamentColor, layer, 4998);

            // Fioritures de nom de clan.
            var flSprite = LoadBannerSprite("ui_banner_flourish_", DefaultFlourishKey);
            _flourishL = MakeSprite("FlourishL", flSprite, OrnamentColor, layer, 4999);
            _flourishR = MakeSprite("FlourishR", flSprite, OrnamentColor, layer, 4999);

            // Bannière cosmétique (sprite full-color, pas de teinte) au-dessus de la plaque.
            // Sprite assigné dynamiquement dans RefreshLayout selon la bannière équipée.
            _cosmeticBanner = MakeSprite("CosmeticBanner", null, Color.white, layer, 5001);
            _cosmeticBanner.gameObject.SetActive(false);

            // Textes.
            _clanText = MakeText("Clan", ClanFont, ClanColor, layer, 5000, FontStyles.Bold);
            _pseudoText = MakeText("Pseudo", PseudoFont, PseudoColor, layer, 5000, FontStyles.Bold);
            _titleText = MakeText("Title", TitleFont, TitleColor, layer, 5000, FontStyles.Italic);

            _root.gameObject.SetActive(false);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, int layerId, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerID = layerId;
            sr.sortingOrder = order;
            return sr;
        }

        private TextMeshPro MakeText(string name, float fontSize, Color color, int layerId, int order, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = style;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            t.richText = true;
            t.sortingLayerID = layerId;
            t.sortingOrder = order;
            // Contour noir pour ressortir sur le bandeau. On passe par fontMaterial (instance
            // par renderer) + ShaderUtilities pour NE PAS baver sur les autres TMP qui partagent
            // le matériau de police (l'API t.outlineWidth écrirait sur le matériau partagé).
            var mat = t.fontMaterial; // accède = instancie le matériau pour ce texte uniquement
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
            var rt = (RectTransform)t.transform;
            rt.sizeDelta = new Vector2(20f, 2f);
            return t;
        }

        // ===== Boucle survol + position =====

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            HubAvatar hovered = FindAvatarAtMouse(mouseWorld);
            if (hovered != _currentHovered)
            {
                _currentHovered = hovered;
                if (hovered == null)
                {
                    if (_root != null) _root.gameObject.SetActive(false);
                }
                else
                {
                    RefreshLayout(GetClan(hovered), GetPseudo(hovered), GetTitle(hovered), GetBanner(hovered));
                    if (_root != null) _root.gameObject.SetActive(true);
                }
            }

            if (_currentHovered != null && _root != null && _root.gameObject.activeSelf)
            {
                var sr = _currentHovered.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    Vector3 pos = sr.bounds.center;
                    pos.y = sr.bounds.max.y + YOffsetAboveSprite + _bottomExtent;
                    pos.z = 0f;
                    // Snap sur la grille de pixels caméra : le perso bouge en continu, donc sans
                    // snap le bord de la plaque tombe à un offset sous-pixel différent chaque frame
                    // -> colonne de pixels qui clignote (la "ligne verticale"). En bougeant par pas
                    // d'un pixel entier, les bords restent alignés frame à frame.
                    if (_camera.orthographic)
                    {
                        float upp = (_camera.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
                        if (upp > 0f)
                        {
                            pos.x = Mathf.Round(pos.x / upp) * upp;
                            pos.y = Mathf.Round(pos.y / upp) * upp;
                        }
                    }
                    _root.position = pos;
                }
            }
        }

        /// <summary>Met en page le bandeau (origine root = centre de la plaque). La plaque
        /// ENGLOBE les 3 lignes (clan + pseudo + titre) empilées ; dimensions dérivées de la
        /// hauteur RENDUE du texte → compact.</summary>
        private void RefreshLayout(string clan, string pseudo, string title, string bannerId)
        {
            if (string.IsNullOrEmpty(pseudo)) pseudo = "?";
            bool hasClan = !string.IsNullOrEmpty(clan);
            bool hasTitle = !string.IsNullOrEmpty(title);

            // Applique les pièces + couleur du clan (cache, sinon défauts + fetch async).
            ApplyBannerConfig(hasClan ? clan : null);

            // Mesure des 3 lignes.
            _pseudoText.text = pseudo; _pseudoText.ForceMeshUpdate(true);
            float ph = _pseudoText.preferredHeight, pw = _pseudoText.preferredWidth;

            float ch = 0f, wc = 0f, flW = 0f, clanLineW = 0f;
            if (hasClan)
            {
                _clanText.text = clan; _clanText.ForceMeshUpdate(true);
                ch = _clanText.preferredHeight; wc = _clanText.preferredWidth;
                flW = FitHeight(_flourishL, ch * FlourishHeightFactor, false, FlourishWidthFactor);
                FitHeight(_flourishR, ch * FlourishHeightFactor, true, FlourishWidthFactor);
                clanLineW = wc + 2f * (ch * FlourishGapFactor + flW);
            }
            float th = 0f, tw = 0f;
            if (hasTitle)
            {
                _titleText.text = title; _titleText.ForceMeshUpdate(true);
                th = _titleText.preferredHeight; tw = _titleText.preferredWidth;
            }

            // Bannière cosmétique chargée TÔT : son chevauchement (dip) détermine le headroom haut.
            var bannerSprite = LoadCosmeticBanner(bannerId);
            float bH = bannerSprite != null ? ph * BannerHeightFactor : 0f;
            // Chevauchement sous le bord haut (+ ajustement par emblème). Le headroom haut s'adapte
            // à ce dip -> abaisser un emblème ne le fait pas mordre sur le nom de clan.
            float bannerDip = bH * (BannerOverlapFactor + BannerExtraOverlap(bannerId));

            // Dimensions plaque : englobe les lignes présentes + paddings. Padding HAUT élargi si
            // bannière -> l'emblème déborde sur le padding, pas sur la 1ère ligne (clan).
            float lineGap = ph * LineGapFactor;
            float contentH = ph
                           + (hasClan ? ch + lineGap : 0f)
                           + (hasTitle ? th + lineGap : 0f);
            float padY = ph * PlatePadYFactor;
            float topPad = bannerSprite != null ? Mathf.Max(padY, bannerDip - ph * 0.06f) : padY;
            float plateH = contentH + topPad + padY;
            float contentW = Mathf.Max(pw, Mathf.Max(clanLineW, tw));
            float plateW = contentW + 2f * ph * PlatePadXFactor;

            _plate.transform.localScale = new Vector3(plateW, plateH, 1f);
            _plate.transform.localPosition = Vector3.zero;

            // Empilement vertical (haut -> bas). Contenu décalé vers le bas si padding haut élargi.
            float cursor = contentH * 0.5f + (padY - topPad) * 0.5f;
            if (hasClan)
            {
                float clanY = cursor - ch * 0.5f;
                _clanText.rectTransform.localPosition = new Vector3(0f, clanY, 0f);
                float flX = wc * 0.5f + ch * FlourishGapFactor + flW * 0.5f;
                _flourishL.transform.localPosition = new Vector3(-flX, clanY, 0.01f);
                _flourishR.transform.localPosition = new Vector3(flX, clanY, 0.01f);
                cursor -= ch + lineGap;
            }
            _pseudoText.rectTransform.localPosition = new Vector3(0f, cursor - ph * 0.5f, 0f);
            cursor -= ph;
            if (hasTitle)
            {
                cursor -= lineGap;
                _titleText.rectTransform.localPosition = new Vector3(0f, cursor - th * 0.5f, 0f);
            }

            _clanText.gameObject.SetActive(hasClan);
            _flourishL.gameObject.SetActive(hasClan);
            _flourishR.gameObject.SetActive(hasClan);
            _titleText.gameObject.SetActive(hasTitle);

            // Liserés haut/bas de la plaque (ornement de clan, couleur du clan) -> seulement si clan.
            _lisereTop.gameObject.SetActive(hasClan);
            _lisereBottom.gameObject.SetActive(hasClan);
            if (hasClan)
            {
                float lisere = plateH * LisereThicknessFactor;
                _lisereTop.transform.localScale = new Vector3(plateW, lisere, 1f);
                _lisereBottom.transform.localScale = new Vector3(plateW, lisere, 1f);
                _lisereTop.transform.localPosition = new Vector3(0f, plateH * 0.5f - lisere * 0.5f, 0f);
                _lisereBottom.transform.localPosition = new Vector3(0f, -plateH * 0.5f + lisere * 0.5f, 0f);
            }

            // Bouts de ruban (taille ~ plaque), flanquant la plaque, centrés verticalement.
            // Pièces de clan -> affichées UNIQUEMENT si le joueur a un clan (sinon un perso sans
            // clan se retrouvait avec le ruban quand même).
            _endL.gameObject.SetActive(hasClan);
            _endR.gameObject.SetActive(hasClan);
            if (hasClan)
            {
                float endSize = plateH * EndSizeFactor;
                float endW = FitHeight(_endL, endSize, false, EndWidthFactor);
                FitHeight(_endR, endSize, true, EndWidthFactor);
                float endX = plateW * 0.5f + endW * 0.5f - plateH * EndOverlapFactor;
                _endL.transform.localPosition = new Vector3(-endX, 0f, 0.01f);
                _endR.transform.localPosition = new Vector3(endX, 0f, 0.01f);
            }

            // Bannière cosmétique : emblème centré au-dessus de la plaque, chevauchant un peu le
            // haut (sprite + bH déjà résolus plus haut pour le headroom). Calé par hauteur.
            if (bannerSprite != null)
            {
                _cosmeticBanner.sprite = bannerSprite;
                FitHeight(_cosmeticBanner, bH, false, 1f);
                float bY = plateH * 0.5f + bH * 0.5f - bannerDip;
                _cosmeticBanner.transform.localPosition = new Vector3(0f, bY, 0.02f);
                _cosmeticBanner.gameObject.SetActive(true);
            }
            else
            {
                _cosmeticBanner.gameObject.SetActive(false);
            }

            // Le bandeau (hors rubans) tient dans la plaque -> extension basse = demi-plaque.
            _bottomExtent = plateH * 0.5f;
        }

        /// <summary>Met le sprite à une hauteur world donnée en préservant son aspect.
        /// Renvoie la largeur world résultante. mirror = scale.x négatif (bout/fioriture droit).</summary>
        private static float FitHeight(SpriteRenderer sr, float worldH, bool mirror, float widthMul = 1f)
        {
            if (sr == null || sr.sprite == null) return worldH;
            Vector2 size = sr.sprite.bounds.size;
            if (size.y <= 0.0001f) return worldH;
            float s = worldH / size.y;
            float sx = s * widthMul; // widthMul < 1 = écrase la largeur (ornement moins long)
            sr.transform.localScale = new Vector3(mirror ? -sx : sx, s, 1f);
            return size.x * sx;
        }

        // ===== Config bandeau par clan (fetch + cache) =====

        /// <summary>Vide l'entrée cache d'un clan (appelé après édition du bandeau par le chef)
        /// -> le prochain survol refetch la config à jour.</summary>
        public static void InvalidateClanBanner(string clanName)
        {
            if (string.IsNullOrEmpty(clanName)) return;
            _bannerCache.Remove(clanName);
            _pendingFetch.Remove(clanName);
        }


        /// <summary>Applique pièces (bout/fioriture) + couleur ornement selon la config du clan.
        /// Cache hit -> applique direct. Cache miss -> défauts maintenant + fetch async (re-applique
        /// au retour si on survole toujours ce clan).</summary>
        private void ApplyBannerConfig(string clanName)
        {
            string endKey = DefaultEndKey, flourishKey = DefaultFlourishKey;
            Color ornament = OrnamentColor;

            if (!string.IsNullOrEmpty(clanName))
            {
                if (_bannerCache.TryGetValue(clanName, out var cfg))
                {
                    if (!string.IsNullOrEmpty(cfg.End)) endKey = cfg.End;
                    if (!string.IsNullOrEmpty(cfg.Flourish)) flourishKey = cfg.Flourish;
                    if (!string.IsNullOrEmpty(cfg.ColorHex) && ColorUtility.TryParseHtmlString(cfg.ColorHex, out var c))
                        ornament = c;
                }
                else
                {
                    TryFetchBanner(clanName);
                }
            }

            var endSprite = LoadBannerSprite("ui_banner_end_", endKey)
                            ?? LoadBannerSprite("ui_banner_end_", DefaultEndKey);
            var flSprite = LoadBannerSprite("ui_banner_flourish_", flourishKey)
                           ?? LoadBannerSprite("ui_banner_flourish_", DefaultFlourishKey);
            if (endSprite != null) { _endL.sprite = endSprite; _endR.sprite = endSprite; }
            if (flSprite != null) { _flourishL.sprite = flSprite; _flourishR.sprite = flSprite; }

            _endL.color = _endR.color = ornament;
            _flourishL.color = _flourishR.color = ornament;
            _lisereTop.color = _lisereBottom.color = ornament;
        }

        private void TryFetchBanner(string clanName)
        {
            if (_pendingFetch.Contains(clanName)) return;
            string token = HubChatClient.Instance != null ? HubChatClient.Instance.DevToken : null;
            if (string.IsNullOrEmpty(token)) return; // pas connecté -> on retentera au prochain survol
            var api = ResolveApi();
            if (api == null) return;
            _pendingFetch.Add(clanName);
            FetchBannerAsync(clanName, api, token).Forget();
        }

        private async UniTaskVoid FetchBannerAsync(string clanName, NymoraApiClient api, string token)
        {
            api.SetBearerToken(token);
            var res = await api.GetClanBannerByNameAsync(clanName);
            if (res.IsSuccess && res.Data != null)
            {
                _bannerCache[clanName] = new BannerConfig
                {
                    End = res.Data.bannerEnd,
                    Flourish = res.Data.bannerFlourish,
                    ColorHex = res.Data.bannerColor,
                };
            }
            else
            {
                // Échec (clan supprimé/introuvable) -> cache défaut pour ne pas re-spammer.
                _bannerCache[clanName] = new BannerConfig { End = DefaultEndKey, Flourish = DefaultFlourishKey, ColorHex = null };
            }
            _pendingFetch.Remove(clanName);

            // Re-applique si on survole toujours ce clan.
            if (this == null || _root == null || !_root.gameObject.activeSelf || _currentHovered == null) return;
            if (GetClan(_currentHovered) == clanName)
                RefreshLayout(clanName, GetPseudo(_currentHovered), GetTitle(_currentHovered), GetBanner(_currentHovered));
        }

        /// <summary>Construit (lazy) un NymoraApiClient à partir du SO settings déjà chargé en
        /// mémoire (référencé par les panneaux du hub) — pas de ref de scène ni de Resources.</summary>
        private NymoraApiClient ResolveApi()
        {
            if (_api != null) return _api;
            var all = Resources.FindObjectsOfTypeAll<NymoraBackendSettings>();
            if (all == null || all.Length == 0) return null;
            _api = new NymoraApiClient(all[0]);
            return _api;
        }

        /// <summary>Bannière cosmétique (emblème) du joueur hover, lue depuis le [Networked]
        /// NetBanner → visible pour SON propre avatar ET pour les avatars distants (sync Fusion),
        /// comme GetTitle/GetClan. "" = aucune bannière équipée.</summary>
        private static string GetBanner(HubAvatar a) => a != null ? a.NetBanner.ToString() : "";

        /// <summary>Ajustement vertical par emblème (× hauteur bannière, ajouté au chevauchement) :
        /// certains emblèmes plats/horizontaux (parchemin) flottent trop haut → on les abaisse un
        /// peu. Le headroom haut de la plaque compense (pas de morsure sur le nom de clan).</summary>
        private static float BannerExtraOverlap(string bannerId)
        {
            switch (bannerId)
            {
                case "banner_parchemin": return 0.08f; // parchemin abaissé (léger)
                default: return 0f;
            }
        }

        /// <summary>Charge (lazy + cache) un sprite de bannière cosmétique par cosmeticId
        /// (Resources/UI/Cosmetics/Banners/&lt;id&gt;). Null si absent (slot non livré).</summary>
        private static Sprite LoadCosmeticBanner(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId)) return null;
            string path = CosmeticBannerResRoot + cosmeticId;
            if (_spriteCache.TryGetValue(path, out var sp)) return sp;
            sp = Resources.Load<Sprite>(path);
            _spriteCache[path] = sp; // cache même null (évite de recharger une clé invalide)
            return sp;
        }

        private static Sprite LoadBannerSprite(string prefix, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string path = BannerResRoot + prefix + key;
            if (_spriteCache.TryGetValue(path, out var sp)) return sp;
            sp = Resources.Load<Sprite>(path);
            _spriteCache[path] = sp; // cache même null (évite de recharger une clé invalide)
            return sp;
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
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

        private static string GetPseudo(HubAvatar a)
        {
            string p = a.NetDisplayName.ToString();
            return string.IsNullOrEmpty(p) ? "?" : p;
        }

        private static string GetClan(HubAvatar a) => a.NetClanName.ToString();
        private static string GetTitle(HubAvatar a) => a.NetTitle.ToString();
    }
}
