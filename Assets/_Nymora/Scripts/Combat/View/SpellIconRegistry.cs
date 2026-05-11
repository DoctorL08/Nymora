using System;
using System.Collections.Generic;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Mapping SpellId -> Sprite (icone 128x128). Utilise par le HUD combat (2.13)
    /// pour afficher les 6 sorts du deck en bas-centre + l'icone du passif classe en
    /// bas-gauche.
    ///
    /// 2.12 : un seul asset peuple avec les 17 icones Soulrender (15 sorts + signature + passif).
    /// Phase 3 : ajouter les icones Nightseer, Colossar, Necram, Ghostra (15 + signature + passif chacune).
    ///
    /// Les passifs ne sont pas des SpellId (pas castables). On les expose via des champs
    /// dedies par classe.
    ///
    /// Le helper `Nymora > Setup > Populate Spell Icon Registry` (Editor) auto-remplit ce
    /// registry en scannant les dossiers d'icones.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Spell Icon Registry", fileName = "SpellIconRegistry", order = 110)]
    public class SpellIconRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SpellId Spell;
            public Sprite Icon;
        }

        [Header("Icones de sorts (1 par SpellId)")]
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        [Header("Icones de passifs (1 par classe)")]
        [Tooltip("Hemoglyphe Soulrender (icon_passif_hemoglyphe.png).")]
        [SerializeField] private Sprite _passifSoulrenderIcon;
        // Phase 3 : _passifNightseerIcon, _passifColossarIcon, _passifNecramIcon, _passifGhostraIcon

        private Dictionary<SpellId, Sprite> _cache;

        public Sprite GetIcon(SpellId id)
        {
            EnsureCache();
            return _cache.TryGetValue(id, out var sprite) ? sprite : null;
        }

        public Sprite PassifIconFor(NymoraClass nymoraClass)
        {
            switch (nymoraClass)
            {
                case NymoraClass.Soulrender: return _passifSoulrenderIcon;
                // Phase 3 : autres classes
                default: return null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Setter Editor uniquement (utilise par l'auto-populate). NE PAS appeler en runtime.
        /// </summary>
        public void EditorSetEntries(Entry[] entries, Sprite passifSoulrenderIcon)
        {
            _entries = entries ?? Array.Empty<Entry>();
            _passifSoulrenderIcon = passifSoulrenderIcon;
            _cache = null; // force rebuild au prochain GetIcon
        }
#endif

        private void OnEnable()
        {
            _cache = null; // lazy build au premier acces
        }

        private void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<SpellId, Sprite>(_entries.Length);
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Spell == SpellId.None) continue;
                _cache[_entries[i].Spell] = _entries[i].Icon;
            }
        }
    }
}
