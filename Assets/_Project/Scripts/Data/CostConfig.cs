using UnityEngine;

namespace Wassup.Data
{
    // Phase 6: global cost economy parameters. Held in one SO so the designer
    // can tune resource pacing without code changes. Referenced by BattleBridge
    // (or GameManager) to configure CostRuntime at battle start.
    [CreateAssetMenu(fileName = "CostConfig", menuName = "Wassup/CostConfig", order = 14)]
    public class CostConfig : ScriptableObject
    {
        public int startingCost = 10;
        public int maxCost = 15;
        public float regenPerSec = 1f;
        public float placementPhaseDuration = 30f;
    }
}
