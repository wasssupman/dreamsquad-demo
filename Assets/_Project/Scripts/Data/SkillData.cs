using UnityEngine;

namespace Wassup.Data
{
    public enum SkillEffectType
    {
        SlowField,
        PowerSurge,
        RapidFire,
    }

    public enum SkillTargetType
    {
        TilePoint,
        DefenderUnit,
    }

    [CreateAssetMenu(fileName = "Skill", menuName = "Wassup/Skill", order = 12)]
    public class SkillData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public SkillEffectType effect;
        public SkillTargetType target;

        // For TilePoint, the effect-area tile radius. For DefenderUnit, unused (0).
        public float range;

        // Multiplier applied to the target's attribute. Example: 0.6 slows movement
        // to 60%, 2.0 doubles attack damage.
        public float magnitude = 1f;

        // Effect lifetime in seconds. EffectTickSystem removes the component when
        // `remaining` reaches zero.
        public float durationSec = 1f;

        public float cooldownSec = 10f;

        // Phase 6: cost subtracted from CostRuntime on cast.
        public int cost = 2;

        // Slot background tint — identifies the skill in the SkillBar without an
        // icon texture.
        public Color uiTint = Color.white;
    }
}
