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
        [Tooltip("L'Oeil qui n'est pas — Nightseer (icon_passif_oeil_qui_nest_pas.png).")]
        [SerializeField] private Sprite _passifNightseerIcon;
        [Tooltip("Densite Inerte — Colossar (icon_passif_densite_inerte.png).")]
        [SerializeField] private Sprite _passifColossarIcon;
        [Tooltip("Floraison — Necram (icon_passif_floraison.png).")]
        [SerializeField] private Sprite _passifNecramIcon;
        [Tooltip("Angle Mort — Ghostra (icon_passif_angle_mort.png).")]
        [SerializeField] private Sprite _passifGhostraIcon;

        [Header("Avatars (portrait HUD, 1 par classe — 2.13.e)")]
        [Tooltip("Portrait Soulrender (SR_avatar_128px.png).")]
        [SerializeField] private Sprite _avatarSoulrenderIcon;
        [Tooltip("Portrait Nightseer (NS_avatar_128px.png).")]
        [SerializeField] private Sprite _avatarNightseerIcon;
        [Tooltip("Portrait Colossar (colossar_avatar_128px.png).")]
        [SerializeField] private Sprite _avatarColossarIcon;
        [Tooltip("Portrait Necram (necram_avatar_128px.png).")]
        [SerializeField] private Sprite _avatarNecramIcon;
        [Tooltip("Portrait Ghostra (GHOSTRA_Avatar_128px.png).")]
        [SerializeField] private Sprite _avatarGhostraIcon;

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
                case NymoraClass.Nightseer:  return _passifNightseerIcon;
                case NymoraClass.Colossar:   return _passifColossarIcon;
                case NymoraClass.Necram:     return _passifNecramIcon;
                case NymoraClass.Ghostra:    return _passifGhostraIcon;
                default: return null;
            }
        }

        /// <summary>
        /// Portrait du combattant (HUD ResourcePanel, 2.13.e). Retourne null si la classe
        /// n'a pas encore d'avatar livre par le designer.
        /// </summary>
        public Sprite AvatarFor(NymoraClass nymoraClass)
        {
            switch (nymoraClass)
            {
                case NymoraClass.Soulrender: return _avatarSoulrenderIcon;
                case NymoraClass.Nightseer:  return _avatarNightseerIcon;
                case NymoraClass.Colossar:   return _avatarColossarIcon;
                case NymoraClass.Necram:     return _avatarNecramIcon;
                case NymoraClass.Ghostra:    return _avatarGhostraIcon;
                default: return null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Setter Editor uniquement (utilise par l'auto-populate). NE PAS appeler en runtime.
        /// avatarSoulrenderIcon peut etre null si non trouve — laissera le slot vide.
        /// </summary>
        public void EditorSetEntries(
            Entry[] entries,
            Sprite passifSoulrenderIcon,
            Sprite avatarSoulrenderIcon = null,
            Sprite avatarNightseerIcon = null,
            Sprite passifNightseerIcon = null,
            Sprite passifColossarIcon = null,
            Sprite passifNecramIcon = null,
            Sprite passifGhostraIcon = null,
            Sprite avatarColossarIcon = null,
            Sprite avatarNecramIcon = null,
            Sprite avatarGhostraIcon = null)
        {
            _entries = entries ?? Array.Empty<Entry>();
            _passifSoulrenderIcon = passifSoulrenderIcon;
            _avatarSoulrenderIcon = avatarSoulrenderIcon;
            _avatarNightseerIcon = avatarNightseerIcon;
            _passifNightseerIcon = passifNightseerIcon;
            _passifColossarIcon = passifColossarIcon;
            _passifNecramIcon = passifNecramIcon;
            _passifGhostraIcon = passifGhostraIcon;
            _avatarColossarIcon = avatarColossarIcon;
            _avatarNecramIcon = avatarNecramIcon;
            _avatarGhostraIcon = avatarGhostraIcon;
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
