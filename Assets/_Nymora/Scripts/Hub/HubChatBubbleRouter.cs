using Nymora.Hub.Menu;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique C1 — Route les messages du canal global vers une bulle de chat au-dessus de l'avatar
    /// de l'expéditeur. Le backend diffuse déjà chaque CHANNEL_MESSAGE à TOUS les clients du canal
    /// (y compris l'expéditeur) → aucun RPC : chaque client mappe le message à sa copie locale de
    /// l'avatar via le displayName.
    ///
    /// Instancié par HubBootstrap (donc vit avec la scène hub → meurt en combat = bulles hub only).
    /// Ne traite QUE le canal "global" (pas clan, pas MP).
    /// </summary>
    public sealed class HubChatBubbleRouter : MonoBehaviour
    {
        private const string HubChannel = "global";

        private HubChatClient _chat;

        private void Update()
        {
            // Abonnement paresseux : HubChatClient (DontDestroyOnLoad) est dispo dès le hub, mais on
            // sécurise contre l'ordre d'init en s'abonnant au premier frame où l'instance existe.
            if (_chat == null)
            {
                var c = HubChatClient.Instance;
                if (c != null)
                {
                    _chat = c;
                    _chat.OnMessageReceived += HandleMessage;
                }
            }
        }

        private void OnDestroy()
        {
            if (_chat != null) _chat.OnMessageReceived -= HandleMessage;
        }

        private void HandleMessage(string channel, string fromDisplayName, string text)
        {
            if (channel != HubChannel) return;
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(fromDisplayName)) return;

            var font = HubMenuShell.MenuFont;
            foreach (var av in HubAvatar.All)
            {
                if (av != null && av.DisplayName == fromDisplayName)
                {
                    av.ShowChatBubble(text, font);
                    return;
                }
            }
        }
    }
}
