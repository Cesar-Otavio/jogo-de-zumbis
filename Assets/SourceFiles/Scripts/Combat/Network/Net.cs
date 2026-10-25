using Unity.Netcode;

namespace Combat
{
    // where the other systems ask "is this a co-op match, and am I the one in charge?"
    public static class Net
    {
        public static bool Listening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // a co-op match is being played (not just a lobby)
        public static bool Active => Listening && NetGame.Instance != null && NetGame.Instance.Started;

        // authority over enemies, waves and damage: the host, or single player
        public static bool HasAuthority => !Listening || NetworkManager.Singleton.IsServer;

        public static bool IsClientOnly => Active && !NetworkManager.Singleton.IsServer;
    }
}
