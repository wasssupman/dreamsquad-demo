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
