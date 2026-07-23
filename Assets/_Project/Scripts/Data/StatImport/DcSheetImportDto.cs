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
    // (_skillId/_projectileId/_effect/_target) have no DTO field on purpose —
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
