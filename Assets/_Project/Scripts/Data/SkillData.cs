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

        // 표시·로그 전용. **실지불은 이 필드가 아니다** — 액티브 시전은 각성 게이지에서
        // `DreamcatcherGaugeConfig.CostFor(card.type)` 만큼 차감한다
        // (`DreamcatcherHandController`). 소비자는 로그 `cost_spent`·카드 문안·드래프트 표시뿐.
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

        // (`TargetsAllies` 는 active-ally-zone unit 1 에서 삭제 — 아군 버프가 장판이 되면서 조준이
        //  아군 유무를 물을 이유가 사라졌고, 아군/적 구분은 스폰 경로에서 구조적으로 갈린다.)

        // skill-layer-migration unit 7e — **조준이 몇 칸을 받나.** 저작이 선언한다.
        //
        // ⚠ 예전엔 `effect == Portal` 로 «이름표를 보고» 알았다(2026-08-26 사용자 결정으로
        // 바꿨다): 「포탈같이 특수한 액티브는 각 액티브 스킬 **명세와 execute** 에서
        // 처리하는 구조」. 이름표는 그 카드가 **무엇인가**(색·문안)를 말하고, 조준이
        // 몇 칸인가는 그 스킬의 **명세**다 — 둘을 한 값으로 읽으면 새 두 칸 스킬을
        // 저작할 때 이름표를 보는 곳 전부를 다시 찾아야 한다.
        //
        // 판정 지점 셋(조준 문안 · 커밋 라우팅 · 입구≠출구 규칙)이 이 하나를 공유한다.
        [Tooltip("두 칸(입구/출구)을 찍어야 하는가. 포탈이 유일하다.")]
        public bool needsTwoTiles;

        public bool NeedsTwoTiles => needsTwoTiles;
    }
}
