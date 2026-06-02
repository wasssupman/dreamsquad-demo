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
    }
}
