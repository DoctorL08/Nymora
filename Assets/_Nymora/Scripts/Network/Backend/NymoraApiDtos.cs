using System;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// DTOs serialisables pour les echanges HTTP avec le backend.
    /// Tous les champs sont publics (contrainte JsonUtility natif Unity).
    /// </summary>

    [Serializable]
    public class RegisterRequest
    {
        public string email;
        public string password;
        public string displayName;
    }

    [Serializable]
    public class LoginRequest
    {
        // Login par pseudo (le backend accepte aussi email pour la retrocompat, mais le
        // client epure n'envoie que le pseudo). JsonUtility n'envoie que les champs presents.
        public string displayName;
        public string password;
    }

    [Serializable]
    public class ApiUserDto
    {
        public string id;
        public string email;
        public string displayName;
    }

    [Serializable]
    public class AuthResponse
    {
        public string token;
        public ApiUserDto user;
    }

    [Serializable]
    public class MeResponse
    {
        public string id;
        public string email;
        public string displayName;
        public string lastLoginAt; // ISO string
        public string createdAt;
    }

    /// <summary>Reponse de GET /profile/me (Brique 4.12). Etend MeResponse avec mmr.</summary>
    [Serializable]
    public class ProfileMeResponse
    {
        public string id;
        public string email;
        public string displayName;
        public int mmr;
        // 31 mai — stats ranked (K-factor variable cote serveur). rankedGames pilote le K du
        // preview ELO affiche dans le menu de fin de combat (cf RankedEloPreview).
        public int rankedGames;
        public int rankedWins;
        public int rankedLosses;
        // Tutoriel T6 — false => le client propose le tuto au login (pop-up onboarding).
        public bool tutorialCompleted;
        public string createdAt;   // ISO string
        public string lastLoginAt; // ISO string, peut etre null (avant 1er login)
    }

    /// <summary>Format d'erreur renvoye par le backend (champ "error" en string).</summary>
    [Serializable]
    public class ApiErrorBody
    {
        public string error;
    }

    /// <summary>Marker pour les reponses HTTP sans body (204 No Content).</summary>
    [Serializable]
    public class EmptyResponse { }

    /// <summary>Reponse de GET /version (versioning serveur).</summary>
    [Serializable]
    public class VersionResponse
    {
        public string minClientVersion;
        public string currentClientVersion;
        public int minCombatRulesVersion;

        // Brique L1 (launcher) : URL du zip de la derniere version (vide si rien publie)
        // et son hash sha256 (hex lowercase) verifie apres telechargement.
        public string downloadUrl;
        public string sha256;
    }

    // ====== Brique 4.10 — Amis (Friendships) ======

    [Serializable]
    public class FriendDto
    {
        public string friendshipId;
        public string userId;
        public string displayName;
        public string since; // ISO string
    }

    [Serializable]
    public class FriendsListResponse
    {
        public FriendDto[] friends;
    }

    [Serializable]
    public class IncomingFriendRequestDto
    {
        public string friendshipId;
        public string fromUserId;
        public string fromDisplayName;
        public string createdAt;
    }

    [Serializable]
    public class OutgoingFriendRequestDto
    {
        public string friendshipId;
        public string toUserId;
        public string toDisplayName;
        public string createdAt;
    }

    [Serializable]
    public class FriendRequestsResponse
    {
        public IncomingFriendRequestDto[] incoming;
        public OutgoingFriendRequestDto[] outgoing;
    }

    [Serializable]
    public class FriendRequestBody
    {
        public string targetDisplayName;
    }

    [Serializable]
    public class FriendRequestCreatedResponse
    {
        public string friendshipId;
        public string toUserId;
        public string toDisplayName;
    }

    [Serializable]
    public class FriendRespondBody
    {
        public string friendshipId;
        public bool accepted;
    }

    [Serializable]
    public class FriendRespondResponse
    {
        public string friendshipId;
        public string status; // "ACCEPTED" | "DECLINED"
        public string fromUserId;
    }

    // ====== Brique 4.11 — Clans ======

    [Serializable]
    public class ClanMemberDto
    {
        public string userId;
        public string displayName;
        public string role;      // "Leader" | "Officer" | "Member" | "Recruit"
        public string joinedAt;
    }

    [Serializable]
    public class ClanDto
    {
        public string clanId;
        public string name;
        public string bannerColor;
        public string bannerEnd;       // clé pièce ruban (ex "pennon")
        public string bannerFlourish;  // clé pièce fioriture (ex "diamond")
        public string description;
        public string createdAt;
        public string myRole;    // peut etre null si pas membre
        public ClanMemberDto[] members;
    }

    /// <summary>Réponse de GET /clans/banner-by-name/:name (config bandeau pour le tooltip).</summary>
    [Serializable]
    public class ClanBannerDto
    {
        public string name;
        public string bannerColor;
        public string bannerEnd;
        public string bannerFlourish;
    }

    /// <summary>Corps de PATCH /clans/me (chef édite le bandeau). Champs vides ("") = ignorés
    /// côté backend (emptyStringsToUndef), donc on n'écrase pas ce qu'on ne renseigne pas.</summary>
    [Serializable]
    public class ClanUpdateBody
    {
        public string description;
        public string bannerColor;
        public string bannerEnd;
        public string bannerFlourish;
    }

    [Serializable]
    public class ClanInviteDto
    {
        public string inviteId;
        public string clanId;
        public string clanName;
        public string clanBannerColor;
        public string fromUserId;
        public string fromDisplayName;
        public string createdAt;
    }

    [Serializable]
    public class ClanInvitesListResponse
    {
        public ClanInviteDto[] invites;
    }

    [Serializable]
    public class CreateClanBody
    {
        public string name;
        public string description;
        public string bannerColor;
    }

    [Serializable]
    public class ClanInviteBody
    {
        public string targetDisplayName; // null si on utilise targetUserId
        public string targetUserId;      // null si on utilise targetDisplayName
    }

    [Serializable]
    public class ClanInviteCreatedResponse
    {
        public string inviteId;
        public string toUserId;
        public string toDisplayName;
    }

    [Serializable]
    public class ClanRespondBody
    {
        public bool accepted;
    }

    [Serializable]
    public class ClanRespondResponse
    {
        public string status;     // "ACCEPTED" | "DECLINED"
        public string clanId;
        public string fromUserId;
    }

    [Serializable]
    public class ClanPromoteBody
    {
        public string targetUserId;
        public string newRole;    // "Officer" | "Member" | "Recruit"
    }

    [Serializable]
    public class ClanKickBody
    {
        public string targetUserId;
    }

    [Serializable]
    public class ClanLeaveResponse
    {
        public string status;
        public string clanId;
    }

    [Serializable]
    public class ClanDisbandResponse
    {
        public string status;
        public string clanId;
    }

    [Serializable]
    public class ClanGenericOkResponse
    {
        public string status;
        public string targetUserId;
        public string newRole;
        public string kickedUserId;
    }

    // ====== Brique 5.1 — Progression (XP par classe) ======

    [Serializable]
    public class ClassProgressionDto
    {
        public string classId;  // "Soulrender" | "Nightseer" | "Colossar" | "Necram" | "Ghostra"
        public int level;
        public int xp;
        public int xpToNext;    // 0 si level >= cap
    }

    [Serializable]
    public class ProgressionMeResponse
    {
        public ClassProgressionDto[] progressions;
    }

    [Serializable]
    public class AwardXpBody
    {
        public string classId;
        public int amount;
        public string source;
    }

    [Serializable]
    public class AwardXpResponse
    {
        public string classId;
        public int level;
        public int xp;
        public int xpToNext;
        public bool leveledUp;
    }

    // ====== Brique 5.2 — Succès ======

    [Serializable]
    public class AchievementDefDto
    {
        public string id;
        public string category;   // "first_steps" | "combat" | "progression"
        public string title;
        public string description;
        public int target;
        public int points;
    }

    [Serializable]
    public class AchievementCatalogResponse
    {
        public AchievementDefDto[] achievements;
    }

    [Serializable]
    public class UserAchievementDto
    {
        public string achievementId;
        public int progress;
        public int target;
        public bool unlocked;
        public string unlockedAt; // ISO string, null si non unlocked
    }

    [Serializable]
    public class AchievementsMeResponse
    {
        public int totalPoints;
        public int unlockedCount;
        public int totalCount;
        public UserAchievementDto[] items;
    }

    // ====== Brique 5.3 — Deck Builder ======

    [Serializable]
    public class DeckDto
    {
        public string id;
        public string classId;       // "Soulrender" | "Nightseer" | "Colossar" | "Necram" | "Ghostra"
        public string name;
        public string[] spellIds;    // exactement 6 SpellIdTech (snake_case, cf SpellCatalog Unity)
        public string createdAt;     // ISO 8601
        public string updatedAt;     // ISO 8601
    }

    [Serializable]
    public class DecksListResponse
    {
        public DeckDto[] decks;
    }

    [Serializable]
    public class CreateDeckBody
    {
        public string classId;
        public string name;
        public string[] spellIds;
    }

    [Serializable]
    public class UpdateDeckBody
    {
        // Champs optionnels (au moins un requis cote backend). JsonUtility serialise
        // toujours les 2, le backend accepte null/empty et update partiel.
        public string name;
        public string[] spellIds;
    }

    [Serializable]
    public class DeckCreatedResponse
    {
        public DeckDto deck;
    }

    [Serializable]
    public class DeckUpdatedResponse
    {
        public DeckDto deck;
    }

    // ====== Brique 5.4 — Wallet (Nymos + Shards) ======

    [Serializable]
    public class WalletSnapshotDto
    {
        public int nymos;
        public int shards;
    }

    [Serializable]
    public class WalletDeltaDto
    {
        public string currency;   // "Nymos" | "Shards"
        public int amount;        // signe : + award, - spend
        public string reason;
    }

    [Serializable]
    public class WalletTransactionDto
    {
        public string id;
        public string currency;   // "Nymos" | "Shards"
        public int amount;        // signe
        public string reason;
        public int balanceAfter;
        public string createdAt;  // ISO
    }

    [Serializable]
    public class WalletMeResponse
    {
        public int nymos;
        public int shards;
        public WalletTransactionDto[] transactions;
    }

    [Serializable]
    public class WalletAwardBody
    {
        public string currency;
        public int amount;
        public string reason;
        public string idempotencyKey;
    }

    [Serializable]
    public class WalletAwardResponse
    {
        public bool alreadyApplied;
        public WalletSnapshotDto wallet;
        public WalletDeltaDto delta;
        public int balanceAfter;
    }

    [Serializable]
    public class WalletSpendBody
    {
        public string currency;
        public int amount;
        public string reason;
        public string idempotencyKey;
    }

    [Serializable]
    public class WalletSpendResponse
    {
        public bool ok;
        public bool alreadyApplied;
        public WalletSnapshotDto wallet;
        public int balanceAfter;
    }

    // ====== Brique 6.3 — Resultat ranked ======

    [Serializable]
    public class RankedReportBody
    {
        public string matchId;
        public string opponentUserId;
        public string classId;        // classe jouee (pour l'XP par classe)
        public string result;         // "win" | "loss" | "draw"
        public string[] deck;         // item I : 6 spellIds tech du deck joue (stats sorts/decks)
    }

    // S-STATS.b — stats de combat auto-declarees (View-observed). Toutes optionnelles
    // cote backend ; n'envoyer que si reellement collectees (sinon flawless faussement
    // debloque par un damageTaken=0 par defaut). Cf RankedReportBodyWithStats.
    [Serializable]
    public class RankedReportStats
    {
        public int damageDealt;
        public int damageTaken;
        public int spellsCast;
        public int turns;
    }

    [Serializable]
    public class RankedReportBodyWithStats
    {
        public string matchId;
        public string opponentUserId;
        public string classId;
        public string result;
        public RankedReportStats stats;
        public string[] deck;         // item I : 6 spellIds tech du deck joue (stats sorts/decks)
    }

    [Serializable]
    public class RankedReportResponse
    {
        public string status;         // "pending" | "settled" | "already_settled" | "conflict"
        public int yourNewMmr;        // valable si status == "settled"
    }

    // ====== Brique 6.5 — Saison ======

    [Serializable]
    public class SeasonResponse
    {
        public int number;
        public string endsAt;         // ISO 8601
        public int daysRemaining;
    }

    // ====== Brique 6.6 — Leaderboard ======

    [Serializable]
    public class LeaderboardEntry
    {
        public int position;
        public string userId;
        public string displayName;
        public int mmr;
        public string rank;           // nom du palier (ex "Argent")
        public int rankedWins;
        public int rankedLosses;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public LeaderboardEntry[] entries;
    }

    // ====== Brique 5.7 — Battle Pass ======

    [Serializable]
    public class BpRewardDto
    {
        public string kind;    // "nymos" | "cosmetic" | "" (null cote serveur)
        public int amount;
        public string label;
    }

    [Serializable]
    public class BpTierDto
    {
        public int tier;
        public BpRewardDto free;
        public BpRewardDto premium;
    }

    [Serializable]
    public class BattlePassMeResponse
    {
        public int seasonNumber;
        public int xp;
        public int tier;
        public bool hasPremium;
        public int[] claimedFree;
        public int[] claimedPremium;
        public int xpPerTier;
        public int maxTier;
        public BpTierDto[] tiers;
    }

    [Serializable]
    public class BpClaimBody
    {
        public int tier;
        public string track;       // "free" | "premium"
    }

    // ====== Brique 5.8 — Quêtes ======

    [Serializable]
    public class QuestDto
    {
        public string id;
        public string period;     // "daily" | "weekly" | "seasonal"
        public string title;
        public int target;
        public int progress;
        public bool completed;
        public bool claimed;
        public int bpXp;
        public int nymos;
    }

    [Serializable]
    public class QuestsMeResponse
    {
        public QuestDto[] quests;
    }

    [Serializable]
    public class QuestClaimBody
    {
        public string questId;
    }

    [Serializable]
    public class QuestClaimResponse
    {
        public string status;     // "claimed" | "error"
        public int bpXp;
        public int nymos;
        public string error;
    }

    [Serializable]
    public class BpClaimResponse
    {
        public string status;       // "claimed" | "error"
        public int tier;
        public string track;
        public string rewardLabel;
        public string error;        // si status == "error"
    }

    // ====== Brique 5.5 — Boutique ======

    [Serializable]
    public class ShopItemDto
    {
        public string id;
        public string type;         // "skin" | "banner" | "title" | "emote"
        public string name;
        public string description;
        public string rarity;       // "common" | "rare" | "epic" | "legendary"
        public int priceNymos;      // 0 = non achetable en Nymos (champ absent cote serveur)
        public int priceShards;     // 0 = non achetable en Shards
        public string classLock;    // classe requise pour equiper, "" si aucune
        public string previewKey;   // cle Resources (ex "cosmetics/ashen_sovereign")
        public bool rotating;
        public bool owned;
        public bool equipped;
    }

    [Serializable]
    public class ShopRotationDto
    {
        public string key;          // "2026-W21"
        public string endsAt;       // ISO 8601 (lundi UTC suivant)
    }

    [Serializable]
    public class ShopResponse
    {
        public WalletSnapshotDto wallet;
        public ShopRotationDto rotation;
        public ShopItemDto[] items;
    }

    [Serializable]
    public class ShopInventoryResponse
    {
        public ShopItemDto[] items;
    }

    [Serializable]
    public class ShopBuyBody
    {
        public string itemId;
        public string currency;     // "Nymos" | "Shards"
    }

    [Serializable]
    public class ShopBuyResponse
    {
        public bool ok;
        public WalletSnapshotDto wallet;
        public string itemId;
        public string error;        // si !ok : INSUFFICIENT_FUNDS | ALREADY_OWNED | NOT_IN_ROTATION | ...
    }

    [Serializable]
    public class ShopEquipBody
    {
        public string itemId;
        public string activeClass;  // classe active du joueur (toujours envoyee, valide)
    }

    [Serializable]
    public class ShopUnequipBody
    {
        public string itemId;
        public string activeClass;  // requis pour un familier (desequipe pour CETTE classe)
    }

    [Serializable]
    public class ShopEquipResponse
    {
        public bool ok;
        public string itemId;
        public string error;         // si !ok : NOT_OWNED | CLASS_MISMATCH | CLASS_REQUIRED
        public string requiredClass; // classe requise si CLASS_MISMATCH/CLASS_REQUIRED
    }
}
