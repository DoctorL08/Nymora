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

    /// <summary>Format d'erreur renvoye par le backend (champ "error" en string).</summary>
    [Serializable]
    public class ApiErrorBody
    {
        public string error;
    }

    /// <summary>Reponse de GET /version (versioning serveur).</summary>
    [Serializable]
    public class VersionResponse
    {
        public string minClientVersion;
        public string currentClientVersion;
        public int minCombatRulesVersion;
    }
}
