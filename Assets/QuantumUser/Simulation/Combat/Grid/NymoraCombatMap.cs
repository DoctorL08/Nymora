namespace Quantum
{
    using System;

    /// <summary>
    /// 5.4 (2v2/3v3) — Un point de spawn d'une map : équipe + rang intra-équipe (ordre voté) +
    /// coordonnées grille. Le combattant (TeamId, TeamOrder) cherche le MapSpawn (Team, Rank)
    /// correspondant. Struct Unity-sérialisable (champ d'un AssetObject).
    /// </summary>
    [Serializable]
    public struct MapSpawn
    {
        public int Team;   // 0 ou 1
        public int Rank;   // 0..2 (ordre intra-équipe ; 1v1 = 0)
        public int X;
        public int Y;
    }

    /// <summary>
    /// 5.4 (2v2/3v3) — Données STATIQUES d'une map de combat : dimensions logiques, masque de cases
    /// jouables (forme irrégulière, bord découpé) et points de spawn groupés par équipe.
    ///
    /// AssetObject Quantum : déterministe et immuable, référencé par un AssetRef (un simple GUID
    /// synchronisé) -> chaque client charge la MÊME map localement, zéro risque de désync. Authoré
    /// par l'éditeur NymoraMapEditor (brique 5.4c) ; lu par GridSystem.OnInit (forme) et
    /// CombatantSystem (spawns). Le 1v1 garde sa zone 10x10 rectangulaire (pas de map asset -> fallback).
    /// </summary>
    public class NymoraCombatMap : AssetObject
    {
        // Dimensions LOGIQUES de la zone jouable (centrage View + bornes d'énumération). 2v2 = 12x12,
        //   3v3 = 15x15. Le tableau de tuiles reste dimensionné au MAX (GridConstants.Count = 225).
        public int Width;
        public int Height;

        // Masque des cases jouables, indexé par GridHelpers.Index (stride GridConstants.Width = 15),
        //   longueur attendue = GridConstants.Count (225). 1 = walkable, 0 = hors-forme (non jouable).
        //   C'est ce masque qui donne la forme NON rectangulaire (imperfections de bord).
        public byte[] Walkable;

        // Points de spawn (groupés par équipe). Au moins 1 par (Team, Rank) attendu pour le mode visé.
        public MapSpawn[] Spawns;
    }
}
