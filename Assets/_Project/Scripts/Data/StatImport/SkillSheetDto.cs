using Newtonsoft.Json;
using Wassup.BattleCore.Trigger;
using Wassup.Data.Authoring;

namespace Wassup.Data.StatImport
{
    // skill-data-table unit 5 — 새 시트 두 탭의 줄 계약(`tables.md` §2 · §4 · 이름 = README U19).
    //
    //  - `Skills`      한 줄 = 효과 하나(`EffectData`) — 키 `effect_id`.
    //  - `SkillOwners` 한 줄 = 소유 줄 하나(`BindingSpec`) — 키 (`owner_kind`, `owner_id`, `slot`).
    //
    // 열 이름은 스네이크(`[JsonProperty]`)이고 **C# 필드 이름은 저작 칸 이름과 같다**(`EffectValues` · `TriggerSpec`) — 임포터 ·
    // 익스포터 · diff 가 이름으로 짝을 찾는다(`SkillSheet`). 저작 칸을 늘리면 여기에 같은 이름 칸 하나를 보탠다(짝 누락은 테스트가 잡는다).
    // 규약은 옛 탭과 같다: nullable = 빈 칸이면 그 값을 그대로 둔다 · enum = C# 멤버 이름.

    public class SkillRowDto
    {
        [JsonProperty("effect_id")] public string id;
        [JsonProperty("kind")] public EffectKind? kind;
        // U19 — 효과 종류의 한국어 표시(보기 전용 — 임포터는 읽지 않는다 · 값은 익스포터가 채운다).
        [JsonProperty("kind_ko")] public string kindKo;
        [JsonProperty("deprecated")] public bool? deprecated;
        [JsonProperty("magnitude_mode")] public MagnitudeMode? magnitudeMode;
        [JsonProperty("basis_stat")] public BasisStat? basisStat;
        [JsonProperty("ratio")] public float? ratio;
        [JsonProperty("damage")] public float? damage;
        [JsonProperty("shield")] public float? shield;
        [JsonProperty("percent")] public float? percent;
        [JsonProperty("mul")] public float? mul;
        [JsonProperty("count")] public int? count;
        [JsonProperty("radius_tiles")] public int? radiusTiles;
        [JsonProperty("range_tiles")] public int? rangeTiles;
        [JsonProperty("duration_sec")] public float? durationSec;
        [JsonProperty("flight_sec")] public float? flightSec;
        [JsonProperty("tick_sec")] public float? tickSec;
        [JsonProperty("stack_cap")] public int? stackCap;
        [JsonProperty("speed")] public float? speed;
        [JsonProperty("cone_half_deg")] public float? coneHalfDeg;
        [JsonProperty("density_radius_tiles")] public int? densityRadiusTiles;
        [JsonProperty("landing_ring_tiles")] public int? landingRingTiles;
        [JsonProperty("cc_kind")] public CcKind? ccKind;
        [JsonProperty("stack_kind")] public StackKind? stackKind;
        [JsonProperty("buff_stat")] public CardBuffKind? buffStat;
        [JsonProperty("shield_filter")] public ShieldTargetFilter? shieldFilter;
        [JsonProperty("includes_self")] public bool? includesSelf;
        [JsonProperty("telegraph")] public bool? telegraph;
        // skill-data-table unit 8 — 수혜 대상(진영 버프 · 배치 오라 — `EffectValues.allyFilter`). 이름 짝이라 export · import 가 그대로 읽는다.
        [JsonProperty("ally_filter")] public CardTargetAxis? allyFilter;
        // 참조(표마다 id — `tables.md` §10). 장판 id = 에셋 이름.
        [JsonProperty("projectile_id")] public string projectileId;
        [JsonProperty("pattern_id")] public string patternId;
        [JsonProperty("hazard_id")] public string hazardId;
    }

    public class SkillOwnerRowDto
    {
        // `card` · `defender` · `enemy`(U19 — 스탯 탭 `Defenders` 와 같은 어휘).
        [JsonProperty("owner_kind")] public string ownerKind;
        [JsonProperty("owner_id")] public string ownerId;
        [JsonProperty("slot")] public int? slot;
        // ↓ `TriggerSpec` 칸(이름 짝)
        [JsonProperty("trigger")] public TriggerKind? kind;
        [JsonProperty("period")] public int? period;
        [JsonProperty("period_sec")] public float? periodSeconds;
        [JsonProperty("fraction")] public float? fraction;
        [JsonProperty("subject")] public BindingSubject? subject;
        [JsonProperty("gate")] public GateKind? gate;
        [JsonProperty("gate_subject")] public GateSubject? gateSubject;
        [JsonProperty("gate_value")] public float? gateValue;
        // ↑
        [JsonProperty("fire_cap")] public int? fireCap;
        [JsonProperty("effect_id")] public string effectId;
    }

    /// <summary>탭 하나의 fetch 가 실패하면 그 칸은 null = 그 탭은 손대지 않는다(옛 탭과 같은 탭별 독립 실패).</summary>
    public class SkillSheetPayload
    {
        public SkillRowDto[] skills;
        public SkillOwnerRowDto[] owners;
    }
}
