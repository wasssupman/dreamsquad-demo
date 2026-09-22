using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
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
                                            ModeDef mode,
                                            in GeneratedMap map = default,
                                            float tileSize = 1f)
        {
            var def = new MatchDefinition
            {
                Seed = seed,
                Mode = mode,
                Units = BuildUnits(defenders),
                Enemies = BuildEnemies(enemies),
                Map = BuildMap(in map, tileSize),
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>
        /// 스캐너 산출(`GeneratedMap`)을 plain 스냅샷으로 접는다. **스캐너는 무변**이다 —
        /// 그쪽은 씬 계층을 훑어 칸 격자를 파는 저작 파이프라인이고, 여기는 그 결과를 읽기만 한다.
        ///
        /// ⚠ **통행 층은 `tiles` 에서만 파생한다**(M1). `placeMask` 는 저작 그대로 싣되 통행에
        /// 쓰지 않는다 — 저작 의미가 «어느 유닛이 여기 설 수 있나» 라서, 통행으로 읽으면
        /// 「배치 금지」로 칠한 통로가 라우팅에서 사라진다(실측 사고).
        /// </summary>
        public static MapSnapshot BuildMap(in GeneratedMap map, float tileSize)
        {
            var snap = MapSnapshot.Empty();
            snap.TileSize = tileSize > 0f ? tileSize : 1f;
            if (!map.IsCreated) return snap;

            snap.Width = map.gridSize.x;
            snap.Height = map.gridSize.y;
            int n = snap.Width * snap.Height;

            snap.Tiles = new MapTile[n];
            snap.PlaceMask = new byte[n];
            snap.CellLayers = new byte[n];
            for (int i = 0; i < n; i++)
            {
                var tile = (MapTile)(byte)map.tiles[i];
                snap.Tiles[i] = tile;
                snap.PlaceMask[i] = map.placeMask.IsCreated
                    ? PlacementLayers.Sanitize(map.placeMask[i])
                    : LayerBits.Derive(tile);
                snap.CellLayers[i] = LayerBits.Derive(tile);
            }

            snap.Spawns = Copy(map.spawns);
            snap.Goals = map.goals.IsCreated && map.goals.Length > 0
                ? Copy(map.goals)
                : new[] { new int2(map.goal.x, map.goal.y) };
            snap.WaypointCells = Copy(map.waypointCells);
            snap.WaypointRanges = Copy(map.waypointRanges);

            if (map.spawnRoutes.IsCreated)
            {
                snap.SpawnRoutes = new int[map.spawnRoutes.Length];
                for (int i = 0; i < snap.SpawnRoutes.Length; i++) snap.SpawnRoutes[i] = map.spawnRoutes[i];
            }

            if (map.structures.IsCreated)
            {
                snap.Structures = new StructureSpot[map.structures.Length];
                for (int i = 0; i < snap.Structures.Length; i++)
                {
                    var st = map.structures[i];
                    snap.Structures[i] = new StructureSpot
                    {
                        Cell = st.cell,
                        Faction = (int)st.faction,
                        // 크기는 **종류에서 파생한다**. 상수를 박으면 1×1 마음이 3×3 을
                        // 차지한다고 거짓말한다.
                        Footprint = StructurePlacements.FootprintOf(st.faction),
                    };
                }
            }

            snap.BonusSpawns = Copy(map.bonusSpawns);
            return snap;
        }

        private static int2[] Copy(Unity.Collections.NativeArray<int2> src)
        {
            if (!src.IsCreated || src.Length == 0) return System.Array.Empty<int2>();
            var outp = new int2[src.Length];
            for (int i = 0; i < outp.Length; i++) outp[i] = src[i];
            return outp;
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
                    AggroCapacity = d.aggroCapacity,
                    TargetFactions = (int)d.targetFactions,
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
                    EngageMovement = (int)e.engageMovement,
                    TargetFactions = (int)e.targetFactions,
                    WaypointPathIndex = e.waypointPathIndex,
                };
            }
            return outp;
        }
    }
}
