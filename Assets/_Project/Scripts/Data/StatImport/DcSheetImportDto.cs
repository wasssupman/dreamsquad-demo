using Newtonsoft.Json;
using Somnia.Battle.Data;

namespace Somnia.Battle.Data.StatImport
{
    // dreamcatcher-sheet-sync unit 2 — JSON contract per
    // docs/spec/dreamcatcher-sheet-sync/0_json_schema_contract.md.
    // Same conventions as UnitStatImportDto: nullable field = partial-update
    // (blank cell deserializes to null and is left untouched), flat field names
    // match their SO counterpart 1:1 for the reflection mapper. skill-data-table unit 9 — 시트 열 = 스네이크(`[JsonProperty]`) ·
    // 정보 열은 `_` 머리(`_skill_id` · `_effect` — import DTO 에 필드가 없고 봉투 파서 · 매퍼가 `_` 머리를 건너뛴다 · export 전용 행 타입만 든다).

    public class DcCardDto
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("display_name")] public string displayName;
        [JsonProperty("type")] public CardType? type;
        [JsonProperty("axis")] public CardTargetAxis? axis;
        [JsonProperty("description")] public string description;
        // dreamcatcher-card-visibility unit 0 — 0 = 인벤토리 숨김. 이름이 SO 와 1:1 이라
        // exporter/applier 변경 없이 reflection 이 양방향을 처리하고, 서버는 새 키를
        // 오른쪽 새 열로 추가한다. 빈 셀은 null → 기존 값 유지(blank=keep).
        [JsonProperty("visible")] public int? visible;
        // dreamcatcher-attach-requirement unit 2(+unit 7 rev) — 부착 대상 제한 2열.
        // 이름이 SO 와 1:1 이라 reflection 매퍼가 양방향을 처리한다. attachType 은 이름
        // 문자열로 오간다(StringEnumConverter — "None"/"Class"/"UnitId").
        // attachValue 는 type 이 정하는 대로 읽힌다: Class→클래스 이름("Guardian"),
        // UnitId→유닛 id("shield_shuttle").
        //
        // ⚠ 제한 **해제**는 attach_type 열에 `None` 을 명시해야 한다. 빈 셀은
        // blank=keep(ApplyNonNullFields 가 null 만 skip)이라 해제가 아니다. None 을 적으면
        // attachValue 는 읽히지 않으므로 값 칸은 비우지 않아도 된다.
        //
        // ⚠ 미검증 전제(review): "string 은 빈칸으로 지울 수 없다" 는 빈 셀이 JSON 에
        // **키 생략**으로 도착할 때만 참이다. 서버 시트→JSON 단계가 빈 텍스트 셀을
        // ""(빈 문자열)로 내보내면 non-null 이라 그대로 써서 attachValue 가
        // 지워진다(그 뒤 fail-closed 경고). 그 단계는 이 repo 밖이라 확인하지 못했다 —
        // 시트 연동 후 실제 페이로드로 한 번 확인할 것.
        [JsonProperty("attach_type")] public DcAttachType? attachType;
        [JsonProperty("attach_value")] public string attachValue;
        // skill-data-table unit 9 — 오늘 에셋에만 있어 시트로 못 고치던 카드 칸 셋(`tables.md` §7). 이름 짝(SO 필드와 같은 C# 이름).
        // host_kinds = 붙을 수 있는 숙주(U5 · 플래그 — "Defender" · "Enemy" · "Defender, Enemy") · 액티브 대기(초) · 두 칸 조준(포탈).
        [JsonProperty("host_kinds")] public HostKinds? hostKinds;
        [JsonProperty("cooldown_sec")] public float? cooldownSec;
        [JsonProperty("needs_two_tiles")] public bool? needsTwoTiles;
    }

    // skill-data-table unit 5 — 옛 `DcMechanicDto`(DcMechanics 탭 · 카드 메커닉 값 overlay)는 은퇴. 카드 · 방어유닛 · 적의 규칙은
    // 새 두 탭 `Skills` · `SkillOwners`(`SkillSheetDto.cs` · `SkillSheet`)가 맡는다.
    // skill-data-table unit 8 단계 B — 카드 자식 행 둘(`DcCardEffectDto` · `DcAttackModDto` — 탭 `DcCardEffects` · `DcAttackMods`)도 은퇴.
    // 스쿼드 스탯 효과 · 공격 수식자는 효과 줄 + 카드 소유 줄(같은 두 탭)이다.

    // skill-data-table unit 9 — 액티브 **문안** 탭(U18 분리 전까지). 수치 칸(range · magnitude · durationSec · cooldownSec · warningSec · cost)은
    // 삭제했다 — 굽기 · 문안이 읽는 값은 `Skills`(시전 줄 효과) + `Cards.cooldown_sec` 이고, 이 칸들은 아무도 안 읽는 두 번째 원천이었다
    // (고쳐도 게임이 안 바뀐다 · `SkillData` 에셋 칸 자체는 분리 spec 까지 남는다).
    public class DcSkillDto
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("display_name")] public string displayName;
        [JsonProperty("description")] public string description;
    }

    // DcConfig union tab: one row per config SO (awakening_default /
    // deck_rule_default). A row only fills its own columns; the reflection
    // mapper never sees the other SO's fields because blank cells are null.
    public class DcConfigDto
    {
        [JsonProperty("id")] public string id;
        // AwakeningConfig
        [JsonProperty("gauge_max")] public int? gaugeMax;
        [JsonProperty("gauge_start")] public int? gaugeStart;
        [JsonProperty("cost_squad")] public int? costSquad;
        [JsonProperty("cost_unit")] public int? costUnit;
        [JsonProperty("cost_active")] public int? costActive;
        [JsonProperty("hand_size")] public int? handSize;
        [JsonProperty("max_attach_per_unit")] public int? maxAttachPerUnit;
        [JsonProperty("slomo_time_scale")] public float? slomoTimeScale;
        // DeckRuleConfig
        [JsonProperty("deck_size")] public int? deckSize;
        [JsonProperty("max_squad")] public int? maxSquad;
        [JsonProperty("max_unit")] public int? maxUnit;
    }

    // A null section = that tab's fetch failed = section untouched (per-tab
    // independent failure, same policy as UnitStatApplier.BuildPayload).
    public class DcSheetPayload
    {
        public DcCardDto[] cards;
        public DcSkillDto[] skills;
        public DcConfigDto[] configs;
    }
}
