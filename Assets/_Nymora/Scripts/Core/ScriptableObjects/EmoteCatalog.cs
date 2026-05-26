using System.Collections.Generic;
using Nymora.Core.Enums;
using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Brique E1 — Catalogue des émotes de classe (gratuites, distribuées à tous dès le départ).
    ///
    /// 5 classes × 3 émotes = 15 entrées. Chaque entrée mappe un id technique (ex "emote_cs_grrr")
    /// + sa classe + son sprite (chibi 147×201 transparent). Le gating se fait à l'usage : seul le
    /// jeu d'émotes de la classe ACTUELLEMENT équipée (HubAvatar.NetClassId) est proposé.
    ///
    /// Peuplé automatiquement par "Nymora > Setup > Emotes > Import Emotes &amp; Build Catalog"
    /// (parse les noms de fichiers Emote_XX_yyy.png dans Art/UI/Emotes/).
    ///
    /// NB : distinct des émotes PAYANTES de la boutique (emote_taunt / emote_bow), qui sont des
    /// cosmétiques backend séparés et ne passent pas par ce catalogue.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Emote Catalog", fileName = "EmoteCatalog", order = 102)]
    public sealed class EmoteCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct EmoteEntry
        {
            [Tooltip("Id technique stable, ex 'emote_cs_grrr' (= nom de fichier en minuscules).")]
            public string Id;

            [Tooltip("Classe propriétaire de l'émote.")]
            public NymoraClass ClassId;

            [Tooltip("Sprite chibi (147×201, fond transparent).")]
            public Sprite Sprite;
        }

        [Tooltip("15 entrées (5 classes × 3). Rempli par l'outil Import Emotes.")]
        public EmoteEntry[] Emotes;

        /// <summary>Les 3 émotes d'une classe donnée, dans l'ordre du catalogue.</summary>
        public List<EmoteEntry> GetEmotesForClass(NymoraClass cls)
        {
            var list = new List<EmoteEntry>();
            if (Emotes == null) return list;
            foreach (var e in Emotes)
                if (e.ClassId == cls && e.Sprite != null) list.Add(e);
            return list;
        }

        /// <summary>Résout un sprite par id technique. null si introuvable.</summary>
        public Sprite GetSprite(string id)
        {
            if (string.IsNullOrEmpty(id) || Emotes == null) return null;
            foreach (var e in Emotes)
                if (e.Id == id) return e.Sprite;
            return null;
        }
    }
}
