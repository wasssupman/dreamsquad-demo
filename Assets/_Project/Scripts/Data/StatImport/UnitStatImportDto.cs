using Newtonsoft.Json;
using Wassup.Data;

namespace Wassup.Data.StatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — JSON contract per
    // docs/spec/unit-stat-spreadsheet-schema/0_json_schema_contract.md.
    // Field names match their DefenderUnitData/AttackUnitData counterpart 1:1 so
    // UnitStatFieldMapper can copy by name. Nullable fields are the partial-update
    // contract: a missing spreadsheet cell deserializes to null and is left untouched.
    public class UnitStatImportPayload
    {
        public DefenderStatDto[] defenders;
        public EnemyStatDto[] enemies;
    }

    public class DefenderStatDto
    {
        public string id;
        public string displayName;
        // squad-character-page unit 7 — unit description. Plain string field like
        // displayName: reflection-mapped both ways (no projection, no skip-list).
        public string desc;
        // defender-unit-visibility unit 0 — 목록 노출 스위치(0 = 숨김). live 필드라
        // 투영/스킵 목록에 넣지 않는다 — cost·maxOnBoard 와 동형으로 이름만 맞추면 된다.
        public int? visible;
        public DefenderClass? role;
        public DefenderRarity? rarity;
        public float? health;
        public float? attackRange;
        // unit-stat-projection Unit 3 — projected onto the unique Damage/Heal output
        // magnitude (not a reflection-mapped field; see skip-list in UnitStatFieldMapper).
        public float? atk;
        public float? heal;
        // Deprecation shim: renamed to `atk`. Kept 1 release to warn instead of
        // silently no-op if the sheet still sends the old column.
        public float? attackDamage;
        public float? attackCooldown;
        public float? hitDelaySec;
        public float? deployDelaySec;
        public int? attackTargetCount;
        public int? cost;
        // defender-placement-cooldown — 배치 성공이 거는 연사 게이트(초). 0 = 없음.
        public float? placementCooldown;
        // defender-clock-out unit 5 — 이탈(퇴근/사망) 재배치 대기. 사망 초 하나 + 퇴근 비율로
        // 저작한다(퇴근 초를 따로 두면 뒤집힌 채 조용히 배포된다). 비율은 0~1 이며 시트가
        // 벗어난 값을 밀어 넣어도 DefenderUnitData 의 Clamp01 이 읽는 자리에서 조인다.
        public float? deathCooldown;
        public float? retireCooldownRatio;
        // defender-board-limit 0 — 판 위 동시 존재 상한. 기본 1, 무제한은 큰 수(100).
        public int? maxOnBoard;
        public int? aggroCapacity;
        public float? aggroRange;
        // dreamcatcher-sheet-sync unit 4 — awakening gauge granted on this
        // defender's death (hardcoded-value audit gap: live scalar, was missing
        // from the sheet contract).
        public int? awakeningReward;
    }

    public class EnemyStatDto
    {
        public string id;
        public string displayName;
        public EnemyClass? enemyClass;
        public EnemyAttackMethod? attackMethod;
        public EnemyTargetMode? targetMode;
        public EngageMovement? engageMovement;
        public DefenderClass? targetPriorityClass;

        [JsonConverter(typeof(DefenderClassFlagsJsonConverter))]
        public DefenderClassFlags? targetClassMask;

        public float? health;
        public float? moveSpeed;
        // unit-stat-projection Unit 3 — projected onto the unique Damage output magnitude.
        public float? atk;
        // Deprecation shim: renamed to `atk`. Warns instead of silent no-op.
        public float? attackDamage;
        public float? attackRange;
        public float? attackCooldown;
        public int? attackTargetCount;
        public float? hitDelaySec;
        // aggroAttackDamage is a LIVE scalar (AggroAttackProfile → TauntAttackGrantSystem);
        // reflection-mapped to the SO, NOT in the projection skip-list.
        public float? aggroAttackDamage;
        public float? aggroAttackCooldown;
        public float? aggroAttackRange;
        // dreamcatcher-sheet-sync unit 4 — awakening gauge granted on kill.
        public int? awakeningReward;
    }
}
