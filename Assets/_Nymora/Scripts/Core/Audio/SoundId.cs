namespace Nymora.Core.Audio
{
    /// <summary>
    /// Clés logiques de tous les événements sonores du jeu. Le code joue un SOUND via
    /// son SoundId (ex: NymoraAudioManager.Instance.PlaySfx(SoundId.UiClick)), jamais via
    /// un AudioClip en dur — le mapping SoundId -> clip vit dans le SoundBank (ScriptableObject).
    ///
    /// Ajouter une entrée ici puis relancer "Nymora > Setup > Setup Audio System" pour que
    /// le slot apparaisse dans la banque (le tool ajoute les manquants, ne touche pas l'existant).
    ///
    /// 100% View — aucun usage dans la simulation Quantum.
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // ----- UI (bus Ui) -----
        UiClick = 100,
        UiHover = 101,
        UiBack = 102,
        UiError = 103,
        PanelOpen = 104,
        PanelClose = 105,
        PurchaseSuccess = 106,
        RewardClaim = 107,

        // ----- Notifications hub (bus Ui) -----
        MatchFound = 200,
        ChallengeReceived = 201,
        MessageReceived = 202,
        FriendOnline = 203,

        // ----- Flux de combat (bus Sfx) -----
        CombatStart = 300,
        CoinFlip = 301,
        TurnStartMine = 302,
        TurnStartEnemy = 303,
        TimerWarning = 304,
        Victory = 305,
        Defeat = 306,

        // ----- Actions de combat (bus Sfx) -----
        SpellCastGeneric = 400,
        Footstep = 401,
        Impact = 402,
        Damage = 403,
        Heal = 404,
        Death = 405,
        Shield = 406,
        TrapTrigger = 407,
        MarkApply = 408,
        Teleport = 409,

        // ----- Musique (bus Music) -----
        MusicMenu = 500,
        MusicHub = 501,
        MusicCombat = 502,
        MusicVictory = 503,
        MusicDefeat = 504,

        // ----- Ambiance (bus Ambience) -----
        AmbienceHub = 600,
        AmbienceCombat = 601,
    }
}
