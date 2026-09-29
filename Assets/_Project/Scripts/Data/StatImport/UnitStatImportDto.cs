using Newtonsoft.Json;
using Wassup.Data;

namespace Wassup.Data.StatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — JSON contract per
    // docs/spec/unit-stat-spreadsheet-schema/0_json_schema_contract.md.
    // Field names match their DefenderUnitData/AttackUnitData counterpart 1:1 so
    // UnitStatFieldMapper can copy by name. Nullable fields are the partial-update
    // contract: a missing spreadsheet cell deserializes to null and is left untouched.
    // skill-data-table unit 9 — 시트 열 이름 = 스네이크(`[JsonProperty]` — `SkillSheetDto` 선례). C# 필드 이름은 SO 와 1:1 그대로라
    // `UnitStatFieldMapper`(C# 이름으로 짝) · 봉투 파서의 헤더 계약(JSON 이름) 둘 다 그대로 돈다. 옛 카멜 헤더는 「계약 밖 헤더」로 보고된다.
    public class UnitStatImportPayload
    {
        public DefenderStatDto[] defenders;
        public EnemyStatDto[] enemies;
    }

    public class DefenderStatDto
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("display_name")] public string displayName;
        // squad-character-page unit 7 — unit description. Plain string field like
        // displayName: reflection-mapped both ways (no projection, no skip-list).
        [JsonProperty("desc")] public string desc;
        // defender-unit-visibility unit 0 — 목록 노출 스위치(0 = 숨김). live 필드라
        // 투영/스킵 목록에 넣지 않는다 — cost·maxOnBoard 와 동형으로 이름만 맞추면 된다.
        [JsonProperty("visible")] public int? visible;
        [JsonProperty("role")] public DefenderClass? role;
        [JsonProperty("rarity")] public DefenderRarity? rarity;
        [JsonProperty("health")] public float? health;
        [JsonProperty("attack_range")] public float? attackRange;
        // unit-stat-projection Unit 3 — projected onto the unique Damage/Heal output
        // magnitude (not a reflection-mapped field; see skip-list in UnitStatFieldMapper).
        [JsonProperty("atk")] public float? atk;
        [JsonProperty("heal")] public float? heal;
        // Deprecation shim: renamed to `atk`. Kept 1 release to warn instead of
        // silently no-op if the sheet still sends the old column.
        [JsonProperty("attack_damage")] public float? attackDamage;
        [JsonProperty("attack_cooldown")] public float? attackCooldown;
        [JsonProperty("hit_delay_sec")] public float? hitDelaySec;
        // (`deployDelaySec` 컬럼은 defender-deploy-phase 에서 은퇴 — 시트가 보내도 무시된다.)
        [JsonProperty("attack_target_count")] public int? attackTargetCount;
        [JsonProperty("cost")] public int? cost;
        // defender-placement-cooldown — 배치 성공이 거는 연사 게이트(초). 0 = 없음.
        [JsonProperty("placement_cooldown")] public float? placementCooldown;
        // defender-clock-out unit 5 — 이탈(퇴근/사망) 재배치 대기. 사망 초 하나 + 퇴근 비율로
        // 저작한다(퇴근 초를 따로 두면 뒤집힌 채 조용히 배포된다). 비율은 0~1 이며 시트가
        // 벗어난 값을 밀어 넣어도 DefenderUnitData 의 Clamp01 이 읽는 자리에서 조인다.
        [JsonProperty("death_cooldown")] public float? deathCooldown;
        [JsonProperty("retire_cooldown_ratio")] public float? retireCooldownRatio;
        // defender-board-limit 0 — 판 위 동시 존재 상한. 기본 1, 무제한은 큰 수(100).
        [JsonProperty("max_on_board")] public int? maxOnBoard;
        // defender-footprint unit 0 — 점유 크기(가로×세로, 셀). 이름 일치 리플렉션 계약으로
        // 임포트/익스포트 양방향 자동. 시트 컬럼 부재 → null → SO 값 유지.
        [JsonProperty("footprint_width")] public int? footprintWidth;
        [JsonProperty("footprint_height")] public int? footprintHeight;
        [JsonProperty("aggro_capacity")] public int? aggroCapacity;
        [JsonProperty("aggro_range")] public float? aggroRange;
        // dreamcatcher-sheet-sync unit 4 — awakening gauge granted on this
        // defender's death (hardcoded-value audit gap: live scalar, was missing
        // from the sheet contract).
        [JsonProperty("awakening_reward")] public int? awakeningReward;
    }

    public class EnemyStatDto
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("display_name")] public string displayName;
        [JsonProperty("enemy_class")] public EnemyClass? enemyClass;
        [JsonProperty("attack_method")] public EnemyAttackMethod? attackMethod;
        [JsonProperty("target_mode")] public EnemyTargetMode? targetMode;
        [JsonProperty("engage_movement")] public EngageMovement? engageMovement;
        [JsonProperty("target_priority_class")] public DefenderClass? targetPriorityClass;

        [JsonConverter(typeof(DefenderClassFlagsJsonConverter))]
        [JsonProperty("target_class_mask")] public DefenderClassFlags? targetClassMask;

        [JsonProperty("health")] public float? health;
        [JsonProperty("move_speed")] public float? moveSpeed;
        // unit-stat-projection Unit 3 — projected onto the unique Damage output magnitude.
        [JsonProperty("atk")] public float? atk;
        // Deprecation shim: renamed to `atk`. Warns instead of silent no-op.
        [JsonProperty("attack_damage")] public float? attackDamage;
        [JsonProperty("attack_range")] public float? attackRange;
        [JsonProperty("attack_cooldown")] public float? attackCooldown;
        [JsonProperty("attack_target_count")] public int? attackTargetCount;
        [JsonProperty("hit_delay_sec")] public float? hitDelaySec;
        // aggroAttackDamage is a LIVE scalar (AggroAttackProfile → TauntAttackGrantSystem);
        // reflection-mapped to the SO, NOT in the projection skip-list.
        [JsonProperty("aggro_attack_damage")] public float? aggroAttackDamage;
        [JsonProperty("aggro_attack_cooldown")] public float? aggroAttackCooldown;
        [JsonProperty("aggro_attack_range")] public float? aggroAttackRange;
        // dreamcatcher-sheet-sync unit 4 — awakening gauge granted on kill.
        [JsonProperty("awakening_reward")] public int? awakeningReward;
    }
}
