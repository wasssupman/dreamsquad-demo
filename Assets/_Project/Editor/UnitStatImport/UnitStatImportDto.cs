using Newtonsoft.Json;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
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
        public DefenderClass? role;
        public DefenderRarity? rarity;
        public float? health;
        public float? attackRange;
        public float? attackDamage;
        public float? attackCooldown;
        public float? hitDelaySec;
        public float? deployDelaySec;
        public int? attackTargetCount;
        public int? cost;
        public int? aggroCapacity;
        public float? aggroRange;
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
        public float? attackDamage;
        public float? attackRange;
        public float? attackCooldown;
        public int? attackTargetCount;
        public float? hitDelaySec;
        public float? aggroAttackDamage;
        public float? aggroAttackCooldown;
        public float? aggroAttackRange;
    }
}
