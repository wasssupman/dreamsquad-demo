using UnityEngine;

namespace Wassup.Data
{
    public enum SkillEffectType
    {
        SlowField,
        PowerSurge,
        RapidFire,
        Tornado,
        Meteor,
        Portal,
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

        // Phase 7 (Meteor): seconds a telegraph/warning visual is displayed before
        // the effect resolves. 0 = immediate effect on cast (legacy behavior).
        public float warningSec;

        // projectile-trajectory-payload unit 7 — projectile ridden when the skill
        // fires through the unified projectile pipeline (Meteor: SkyFall×TileAoe,
        // flightTime = warningSec). Null for non-projectile skills; a projectile
        // skill with this unassigned drops the cast with a warning (config error).
        public ProjectileData projectile;

        // Slot background tint — identifies the skill in the SkillBar without an
        // icon texture.
        public Color uiTint = Color.white;
    }
}
