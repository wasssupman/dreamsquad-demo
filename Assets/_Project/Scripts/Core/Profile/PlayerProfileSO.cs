using System;
using UnityEngine;

namespace Wassup.Core
{
    // outgame-scene-and-flow Unit 0 — cross-scene runtime holder for the player
    // profile. Not a singleton; OutgameScene and BattleScene reference the same
    // asset, which stays in memory across scene loads. ProfileStore drives disk
    // persistence; this SO is an in-memory cache only.
    [CreateAssetMenu(fileName = "PlayerProfile", menuName = "Wassup/PlayerProfileSO", order = 0)]
    public class PlayerProfileSO : ScriptableObject
    {
        public PlayerProfile profile = new PlayerProfile();

        // first-session-tutorial unit 0 — the asset always contains a default
        // non-null profile, so null alone cannot distinguish a real lobby load
        // from opening BattleScene directly in the editor. Never serialize this:
        // only OutgameMenuController's ProfileStore path may arm disk writes.
        private static int s_sessionToken = 1;
        [NonSerialized] private int _loadedSessionToken;

        public bool IsLoadedThisSession => _loadedSessionToken == s_sessionToken;

        // SubsystemRegistration also runs when Enter Play Mode Options disables
        // domain reload. Advancing the token invalidates a previous Play run's
        // in-memory profile before a direct BattleScene start can write it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginRuntimeSession()
        {
            unchecked { s_sessionToken++; }
            if (s_sessionToken == 0) s_sessionToken = 1;
        }

        public void SetLoadedProfile(PlayerProfile loaded)
        {
            profile = loaded;
            _loadedSessionToken = loaded != null ? s_sessionToken : 0;
        }
    }
}
