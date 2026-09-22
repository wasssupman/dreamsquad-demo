using Wassup.BattleCore;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 1 — SO → 정의표. 계약 6 의 «판 밖에서 안으로» 의 문이다.
    //
    // 여기가 Unity 층인 이유: `ScriptableObject` 를 아는 마지막 자리이기 때문이다.
    // 이 함수를 지나면 값은 plain 이고, 코어는 시트도 SO 도 아트도 모른다.
    //
    // **읽는 것은 plain 수치·열거형뿐**이다. Mesh·Material·Prefab·AudioClip·
    // SkeletonDataAsset 같은 아트 참조는 읽지 않는다 — 옛 `MatchConfigSnapshot` 은
    // 리플렉션으로 SO 를 통째로 접느라 「아트 타입 제외 목록」을 손으로 유지해야 했고,
    // 목록이 낡으면 스킨 교체가 「조건이 바뀌었다」로 읽혔다. 명시 필드에는 그 함정이
    // 원리적으로 없다(정의표 타입에 아트 필드가 아예 없다).
    //
    // ⚠ 정의표에 필드를 추가하면 `MatchDefinition.Canonicalize` 도 같이 고친다.
    // 안 고치면 「스탯을 바꿨는데 해시가 그대로」라는 조용한 실패가 된다.
    public static class MatchDefinitionBuilder
    {
        public static MatchDefinition Build(DefenderUnitData[] defenders,
                                            AttackUnitData[] enemies,
                                            int seed,
                                            ModeDef mode)
        {
            var def = new MatchDefinition
            {
                Seed = seed,
                Mode = mode,
                Units = BuildUnits(defenders),
                Enemies = BuildEnemies(enemies),
                Map = MapSnapshot.Empty(),   // unit 2 에서 `MapStageScanner` 가 채운다
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static UnitDef[] BuildUnits(DefenderUnitData[] src)
        {
            if (src == null) return System.Array.Empty<UnitDef>();
            var outp = new UnitDef[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var d = src[i];
                if (d == null) continue;
                var fp = d.Footprint;
                outp[i] = new UnitDef
                {
                    Id = d.id,
                    Health = d.health,
                    AttackRange = d.attackRange,
                    AttackCooldown = d.attackCooldown,
                    HitDelaySeconds = d.hitDelaySec,
                    AttackTargetCount = d.attackTargetCount,
                    BodyRadiusTiles = d.BodyRadiusTiles,
                    FootprintWidth = fp.x,
                    FootprintHeight = fp.y,
                    PlacementLayers = (int)d.EffectivePlacementLayers,
                    TraversalLayers = (int)d.EffectiveTraversalLayers,
                    Role = (int)d.role,
                    AttackShape = (int)d.attackShape,
                };
            }
            return outp;
        }

        private static EnemyDef[] BuildEnemies(AttackUnitData[] src)
        {
            if (src == null) return System.Array.Empty<EnemyDef>();
            var outp = new EnemyDef[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var e = src[i];
                if (e == null) continue;
                outp[i] = new EnemyDef
                {
                    Id = e.id,
                    Health = e.health,
                    MoveSpeed = e.moveSpeed,
                    AttackRange = e.attackRange,
                    AttackCooldown = e.attackCooldown,
                    HitDelaySeconds = e.hitDelaySec,
                    AttackTargetCount = e.attackTargetCount,
                    BodyRadius = e.bodyRadius,
                    TraversalLayers = (int)e.EffectiveTraversalLayers,
                    EnemyClass = (int)e.enemyClass,
                    Tier = (int)e.tier,
                    MinWaveNumber = e.minWaveNumber,
                    MaxPerWave = e.maxPerWave,
                    StabilityDamage = e.stabilityDamage,
                    DetectionRange = e.detectionRange,
                    AwakeningReward = e.awakeningReward,
                    AttackShape = (int)e.attackShape,
                };
            }
            return outp;
        }
    }
}
