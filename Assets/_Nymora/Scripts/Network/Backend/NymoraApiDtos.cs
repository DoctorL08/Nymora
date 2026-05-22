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
        public string email;
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
        public string description;
        public string createdAt;
        public string myRole;    // peut etre null si pas membre
        public ClanMemberDto[] members;
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
}
