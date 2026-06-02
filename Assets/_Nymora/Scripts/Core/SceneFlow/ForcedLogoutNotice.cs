namespace Nymora.Core.SceneFlow
{
    /// <summary>
    /// Porte un message de déconnexion forcée (kick / ban / maintenance) à travers la
    /// transition vers la scène de login, où il est affiché en pop-up.
    ///
    /// Statique = survit au changement de scène. Posé par HubChatClient à la réception
    /// d'un FORCE_DISCONNECT, lu + nettoyé par l'écran de login.
    /// </summary>
    public static class ForcedLogoutNotice
    {
        public static bool HasPending { get; private set; }
        public static string Title { get; private set; }
        public static string Message { get; private set; }

        public static void Set(string title, string message)
        {
            HasPending = true;
            Title = title;
            Message = message;
        }

        public static void Clear()
        {
            HasPending = false;
            Title = null;
            Message = null;
        }
    }
}
