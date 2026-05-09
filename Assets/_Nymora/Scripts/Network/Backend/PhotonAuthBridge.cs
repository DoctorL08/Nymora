using System;
using System.Collections.Generic;
using Photon.Realtime;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// Convertit un JWT Nymora en AuthenticationValues utilisable par Photon Realtime / Quantum.
    /// Quand le client connecte avec ces AuthValues, Photon Cloud appelle notre webhook backend
    /// (POST /auth/photon-webhook configure dans le dashboard) avec le JWT en POST body, et
    /// notre backend repond ResultCode 1 / 2 / 3.
    /// </summary>
    public static class PhotonAuthBridge
    {
        public static AuthenticationValues BuildAuthValues(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
            {
                throw new ArgumentException("JWT vide ou null", nameof(jwt));
            }

            var auth = new AuthenticationValues
            {
                AuthType = CustomAuthenticationType.Custom,
                // UserId laisse null : Photon prendra celui retourne par le webhook
                // (UserId = user.id en DB, defini dans la reponse JSON ResultCode=1).
            };

            // Photon transmet ce dictionnaire en JSON au webhook, en POST body
            // (Content-Type: application/json).
            auth.SetAuthPostData(new Dictionary<string, object>
            {
                ["token"] = jwt,
            });

            return auth;
        }
    }
}
