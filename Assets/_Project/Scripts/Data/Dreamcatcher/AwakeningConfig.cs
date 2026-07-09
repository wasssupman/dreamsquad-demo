using UnityEngine;

namespace Wassup.Data
{
    // dreamcatcher-awakening-hand unit 0 — tunable numbers for the awakening
    // currency + CR-style cycling hand. Designers adjust economy/UX pacing here
    // without code. Per-unit death rewards live on the unit SOs
    // (DefenderUnitData/AttackUnitData.awakeningReward), not here.
    [CreateAssetMenu(fileName = "AwakeningConfig", menuName = "Wassup/AwakeningConfig", order = 24)]
    public class AwakeningConfig : ScriptableObject
    {
        [Header("Gauge")]
        public int gaugeMax = 100;   // awakening cap; gains past this are lost
        public int gaugeStart = 0;   // value at match start (reset every Placement entry)

        // Per-CardType use cost. Serialized separately (not an array) so the
        // inspector reads clearly; CostFor maps by enum CASE, never by index —
        // CardType order is { Squad=0, Unit=1, Active=2 } which differs from the
        // spec's prose order (Unit 15 / Squad 30 / Active 20).
        [Header("Use Cost (per CardType)")]
        public int costSquad = 30;
        public int costUnit = 15;
        public int costActive = 20;

        [Header("Hand")]
        public int handSize = 5;              // hand = front N of the cycle queue
        public int maxAttachPerUnit = 3;      // Unit-type cards attached per defender

        [Header("Use UX")]
        // Battle-domain time scale while the hand is open (TimeManager lease).
        // Never 0 — the game must not pause (spec contract 8).
        public float slomoTimeScale = 0.3f;
        // Seconds between touchup and commit while the pending card can still be
        // cancelled. REALTIME (unscaled) — slomo must not stretch it. 0 = commit
        // immediately on touchup.
        public float confirmDelaySec = 1.5f;

        public int CostFor(CardType type)
        {
            switch (type)
            {
                case CardType.Squad: return costSquad;
                case CardType.Unit: return costUnit;
                case CardType.Active: return costActive;
                default: return costSquad;
            }
        }
    }
}
