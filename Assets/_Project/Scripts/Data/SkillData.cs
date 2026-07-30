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

    [CreateAssetMenu(fileName = "Skill", menuName = "Wassup/Skill", order = 12)]
    public class SkillData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public SkillEffectType effect;

        // active-dreamcatcher-tile-aim unit 0 — 효과 반경(체비셰프 타일, GridMath.RangeToTiles
        // 로 변환). 0 = 지정 타일 1칸. 구 SkillTargetType(TilePoint/DefenderUnit) 축은 폐기됐다:
        // 모든 스킬이 타일 대상이고, 아군 버프도 지정 타일 반경으로 걸린다.
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

        // Slot background tint — identifies the skill in skill UI (오늘은 Active
        // 드림캐쳐 손패 카드의 아트 폴백) without an icon texture.
        public Color uiTint = Color.white;

        // active-dreamcatcher-tile-aim unit 0 — 이 스킬이 겨누는 것이 아군인가(= 반경 내
        // 아군에게 모디파이어). 조준 UI 가 유효성/예고 문안을 가르는 데 쓴다. 필드가 아니라
        // effect 파생값이라 직렬화 무변경이고, 무엇이 실제로 적용되는지의 권위는 여전히
        // BattleBridge.CastSkillAtTile 의 effect switch 다.
        public bool TargetsAllies
            => effect == SkillEffectType.PowerSurge || effect == SkillEffectType.RapidFire;

        // 두 타일(입구/출구)을 요구하는 유일한 스킬. 조준 문안·라우팅·커밋 3곳이 같은 판별을 쓴다.
        public bool IsPortal => effect == SkillEffectType.Portal;
    }
}
