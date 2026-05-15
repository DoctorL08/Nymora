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
}
