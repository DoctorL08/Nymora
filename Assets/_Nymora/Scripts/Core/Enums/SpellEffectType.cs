namespace Nymora.Core.Enums
{
    /// <summary>
    /// Types d'effets applicables par un sort.
    /// Une SpellDefinition peut composer plusieurs effets via sa liste Effects
    /// (ex : "inflige 90 degats ET applique 2 marques de venin" = 2 effets).
    ///
    /// Cette liste sera enrichie au fil des Phases 2-3 quand on implementera les classes.
    /// </summary>
    public enum SpellEffectType
    {
        None = 0,

        // Dommages / soin
        Damage = 10,
        Heal = 11,
        DispelAllDoTs = 12,

        // Marques (saignements, venin, traces, leurres...)
        ApplyMark = 20,
        ConsumeMark = 21,

        // Mouvement
        PushTarget = 30,
        PullTarget = 31,
        TeleportSelf = 32,
        TeleportToTarget = 33,

        // Defenses & buffs
        ApplyShield = 40,
        ApplyBuff = 41,
        ApplyDebuff = 42,

        // Terrain
        SpawnObstacle = 50,
        SpawnLure = 51,
        SpawnHazardZone = 52,

        // Ressource de classe
        GrantClassResource = 60,
        ConsumeClassResource = 61
    }
}
