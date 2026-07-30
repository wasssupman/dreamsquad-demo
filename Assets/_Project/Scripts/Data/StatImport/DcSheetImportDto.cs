using Wassup.Data;

namespace Wassup.Data.StatImport
{
    // dreamcatcher-sheet-sync unit 2 — JSON contract per
    // docs/spec/dreamcatcher-sheet-sync/0_json_schema_contract.md.
    // Same conventions as UnitStatImportDto: nullable field = partial-update
    // (blank cell deserializes to null and is left untouched), flat field names
    // match their SO counterpart 1:1 for the reflection mapper. Nested
    // trigger/payload fields are prefix-flattened (trigger.kind -> triggerKind)
    // and mapped manually in DcSheetApplier. `_`-prefixed sheet columns
    // (_skillId/_projectileId/_effect) have no DTO field on purpose —
    // Json.NET drops unknown keys, so they stay informational.

    public class DcCardDto
    {
        public string id;
        public string displayName;
        public CardType? type;
        public CardTargetAxis? axis;
        public string description;
        // dreamcatcher-card-visibility unit 0 — 0 = 인벤토리 숨김. 이름이 SO 와 1:1 이라
        // exporter/applier 변경 없이 reflection 이 양방향을 처리하고, 서버는 새 키를
        // 오른쪽 새 열로 추가한다. 빈 셀은 null → 기존 값 유지(blank=keep).
        public int? visible;
        // dreamcatcher-attach-requirement unit 2(+unit 7 rev) — 부착 대상 제한 2열.
        // 이름이 SO 와 1:1 이라 reflection 매퍼가 양방향을 처리한다. attachType 은 이름
        // 문자열로 오간다(StringEnumConverter — "None"/"Class"/"UnitId").
        // attachValue 는 type 이 정하는 대로 읽힌다: Class→클래스 이름("Guardian"),
        // UnitId→유닛 id("shield_shuttle").
        //
        // ⚠ 제한 **해제**는 attachType 열에 `None` 을 명시해야 한다. 빈 셀은
        // blank=keep(ApplyNonNullFields 가 null 만 skip)이라 해제가 아니다. None 을 적으면
        // attachValue 는 읽히지 않으므로 값 칸은 비우지 않아도 된다.
        //
        // ⚠ 미검증 전제(review): "string 은 빈칸으로 지울 수 없다" 는 빈 셀이 JSON 에
        // **키 생략**으로 도착할 때만 참이다. 서버 시트→JSON 단계가 빈 텍스트 셀을
        // ""(빈 문자열)로 내보내면 non-null 이라 그대로 써서 attachValue 가
        // 지워진다(그 뒤 fail-closed 경고). 그 단계는 이 repo 밖이라 확인하지 못했다 —
        // 시트 연동 후 실제 페이로드로 한 번 확인할 것.
        public DcAttachType? attachType;
        public string attachValue;
    }

    // Sheet-SoT child row (effects[] rebuild): slot is the ordering/identity key.
    public class DcCardEffectDto
    {
        public string cardId;
        public int? slot;
        public CardBuffKind? kind;
        public float? percent;
    }

    // Unity-SoT child row (mechanics[] value overlay; projectile ref preserved).
    public class DcMechanicDto
    {
        public string cardId;
        public int? slot;
        public DcTriggerKind? triggerKind;
        public int? triggerPeriod;
        public DcPayloadKind? payloadKind;
        public float? magnitude;
        public int? tileRange;
        public float? duration;
        public float? triggerPeriodSeconds; // trigger.periodSeconds — PeriodicTimer 주기 초
        public float? triggerFraction;   // trigger.fraction — HealthThreshold 경계비율
        public DcCcKind? ccKind;          // payload.ccKind — ApplyCcToTarget
        public DcStackKind? stackKind;    // payload.stackKind — ApplyStackToTarget
        public CardBuffKind? buffStat;    // payload.buffStat — SelfStatBuff 대상 스탯
    }

    // Sheet-SoT child row (attackMods[] rebuild).
    public class DcAttackModDto
    {
        public string cardId;
        public int? slot;
        public DcAttackModKind? kind;
        public int? count;
        public int? tileRange;
        public float? damageMul;
    }

    public class DcSkillDto
    {
        public string id;
        public string displayName;
        public string description;
        public float? range;
        public float? magnitude;
        public float? durationSec;
        public float? cooldownSec;
        public int? cost;
        public float? warningSec;
    }

    // DcConfig union tab: one row per config SO (awakening_default /
    // deck_rule_default). A row only fills its own columns; the reflection
    // mapper never sees the other SO's fields because blank cells are null.
    public class DcConfigDto
    {
        public string id;
        // AwakeningConfig
        public int? gaugeMax;
        public int? gaugeStart;
        public int? costSquad;
        public int? costUnit;
        public int? costActive;
        public int? handSize;
        public int? maxAttachPerUnit;
        public float? slomoTimeScale;
        // DeckRuleConfig
        public int? deckSize;
        public int? maxSquad;
        public int? maxUnit;
    }

    // A null section = that tab's fetch failed = section untouched (per-tab
    // independent failure, same policy as UnitStatApplier.BuildPayload).
    public class DcSheetPayload
    {
        public DcCardDto[] cards;
        public DcCardEffectDto[] cardEffects;
        public DcMechanicDto[] mechanics;
        public DcAttackModDto[] attackMods;
        public DcSkillDto[] skills;
        public DcConfigDto[] configs;
    }
}
