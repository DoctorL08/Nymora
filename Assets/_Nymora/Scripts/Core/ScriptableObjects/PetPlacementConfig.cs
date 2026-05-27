using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Brique 5.10 (B4 — reliquat placement) — Réglages de placement du familier dans le hub.
    ///
    /// Sort les valeurs auparavant en dur (PetAnchorOffset dans HubAvatar, SizeFactor dans
    /// HubPetView) pour qu'elles soient éditables sans recompiler. Un offset distinct par
    /// direction iso (SE/SW/NE/NW), car le familier ne tombe pas au même endroit selon
    /// l'orientation (le perso de dos cachait/écrasait le familier avec un offset unique).
    ///
    /// Réglable en direct via le panneau Play Mode HubPetPlacementTuner (curseurs X/Y/taille),
    /// qui mute cette instance chaque frame et peut la sauvegarder dans l'asset.
    ///
    /// Chargé via Resources : "config/PetPlacementConfig". Si l'asset n'existe pas, une instance
    /// transitoire avec les valeurs par défaut (= anciennes constantes) est utilisée -> le jeu
    /// tourne même sans l'asset. Crée l'asset via "Nymora > Setup > Create Pet Placement Config".
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Pet Placement Config", fileName = "PetPlacementConfig", order = 104)]
    public class PetPlacementConfig : ScriptableObject
    {
        [Header("Offset du familier vs l'avatar (unités monde), par direction")]
        [Tooltip("Avatar regarde en bas-droite (SE). Valeur validée à l'origine : (0.42, -0.12).")]
        public Vector2 SeOffset = new Vector2(0.42f, -0.12f);

        [Tooltip("Avatar regarde en bas-gauche (SW).")]
        public Vector2 SwOffset = new Vector2(0.42f, -0.12f);

        [Tooltip("Avatar regarde en haut-droite (NE), de dos.")]
        public Vector2 NeOffset = new Vector2(0.42f, -0.12f);

        [Tooltip("Avatar regarde en haut-gauche (NW), de dos.")]
        public Vector2 NwOffset = new Vector2(0.42f, -0.12f);

        [Header("Taille (hub)")]
        [Tooltip("Facteur de taille du familier (× sa taille native). Compagnon discret -> < 1.")]
        [Range(0.1f, 2f)]
        public float SizeFactor = 0.65f;

        // --- Combat (B5) : le familier se tient sur la MÊME case que le combattant, légèrement
        // décalé. Offsets distincts du hub (échelle/perspective iso différentes). Direction =
        // facing View IsoFacing { NE=0, SE=1, NW=2, SW=3 } (auto-aim vers l'ennemi). ---

        [Header("Offset du familier en COMBAT (unités monde), par direction")]
        [Tooltip("Combattant regarde NE (haut-droite).")]
        public Vector2 CombatNeOffset = new Vector2(0.35f, -0.1f);

        [Tooltip("Combattant regarde SE (bas-droite).")]
        public Vector2 CombatSeOffset = new Vector2(0.35f, -0.1f);

        [Tooltip("Combattant regarde NW (haut-gauche).")]
        public Vector2 CombatNwOffset = new Vector2(-0.35f, -0.1f);

        [Tooltip("Combattant regarde SW (bas-gauche).")]
        public Vector2 CombatSwOffset = new Vector2(-0.35f, -0.1f);

        [Header("Taille (combat)")]
        [Range(0.1f, 2f)]
        public float CombatSizeFactor = 0.6f;

        // --- Accès global (lazy-load Resources, fallback transitoire si l'asset est absent) ---

        private const string ResourcePath = "config/PetPlacementConfig";
        private static PetPlacementConfig _instance;

        /// <summary>
        /// Instance partagée. Chargée depuis Resources la 1re fois ; à défaut, une instance par
        /// défaut en mémoire (le familier reste placé avec les valeurs d'origine).
        /// </summary>
        public static PetPlacementConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<PetPlacementConfig>(ResourcePath);
                    if (_instance == null)
                    {
                        _instance = CreateInstance<PetPlacementConfig>();
                        _instance.name = "PetPlacementConfig (defaults)";
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Retourne l'offset pour une direction. L'index suit l'enum HubFacing :
        /// 0 = SE, 1 = NE, 2 = SW, 3 = NW. Tout autre valeur retombe sur SE.
        /// </summary>
        public Vector2 OffsetForFacing(int facingIndex)
        {
            switch (facingIndex)
            {
                case 1: return NeOffset;
                case 2: return SwOffset;
                case 3: return NwOffset;
                default: return SeOffset;
            }
        }

        /// <summary>
        /// Offset COMBAT pour une direction. L'index suit l'enum View IsoFacing :
        /// 0 = NE, 1 = SE, 2 = NW, 3 = SW. Tout autre valeur retombe sur SE.
        /// </summary>
        public Vector2 CombatOffsetForFacing(int facingIndex)
        {
            switch (facingIndex)
            {
                case 0: return CombatNeOffset;
                case 2: return CombatNwOffset;
                case 3: return CombatSwOffset;
                default: return CombatSeOffset; // 1 = SE
            }
        }

        /// <summary>
        /// Force l'instance partagée (utilisé par le tuner après création/sauvegarde de l'asset
        /// pour que les lectures suivantes viennent du fichier persistant, pas du transitoire).
        /// </summary>
        public static void SetInstance(PetPlacementConfig cfg) { if (cfg != null) _instance = cfg; }

        /// <summary>
        /// Copie TOUTES les valeurs (hub + combat) depuis une autre config. Utilisé par les tuners
        /// pour persister dans l'asset sans qu'un tuner (hub) n'écrase les champs de l'autre (combat).
        /// </summary>
        public void CopyValuesFrom(PetPlacementConfig o)
        {
            if (o == null) return;
            SeOffset = o.SeOffset; SwOffset = o.SwOffset; NeOffset = o.NeOffset; NwOffset = o.NwOffset;
            SizeFactor = o.SizeFactor;
            CombatSeOffset = o.CombatSeOffset; CombatSwOffset = o.CombatSwOffset;
            CombatNeOffset = o.CombatNeOffset; CombatNwOffset = o.CombatNwOffset;
            CombatSizeFactor = o.CombatSizeFactor;
        }
    }
}
