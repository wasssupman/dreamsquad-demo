using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 2 — 맵·이동 테스트의 공용 고정구.
    //
    // ⚠ 여기 수치는 **게임 값이 아니라 픽스처**다. 게임 값의 정본은 판 밖이고(시트 → SO →
    // 빌더) 그 경로는 `configHash` 가 감시한다. 이 파일이 SO 를 안 읽는 이유는 헤드리스
    // lane 에서도 돌아야 하기 때문이다.
    public static class CoreMapFixtures
    {
        /// <summary>전부 걷는 칸인 빈 판. 골은 지정, 스폰은 선택.</summary>
        public static MapSnapshot Open(int width, int height, int2 goal, params int2[] spawns)
        {
            var map = new MapSnapshot
            {
                Width = width,
                Height = height,
                TileSize = 1f,
                Goals = new[] { goal },
                Spawns = spawns ?? System.Array.Empty<int2>(),
            };
            map.Normalize();
            return map;
        }

        /// <summary>그 칸을 장식(못 걷는 칸)으로 만든다. 통행·배치가 함께 닫힌다.</summary>
        public static void Block(MapSnapshot map, int2 cell)
        {
            int i = map.Index(cell);
            map.Tiles[i] = MapTile.Deco;
            map.CellLayers[i] = LayerBits.Derive(MapTile.Deco);
            map.PlaceMask[i] = LayerBits.OpenPlacement(MapTile.Deco);
        }

        /// <summary>폭 1 복도. `y = corridorY` 행만 열려 있고 나머지는 장식이다.</summary>
        public static MapSnapshot Corridor(int width, int height, int corridorY)
        {
            var map = Open(width, height, new int2(width - 1, corridorY), new int2(0, corridorY));
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (y != corridorY) Block(map, new int2(x, y));
            return map;
        }

        /// <summary>정의표 하나 — 방어 1종 · 적 1종. 값은 인자로 흔든다.</summary>
        public static MatchDefinition Definition(MapSnapshot map, int seed = 1,
                                                 float enemySpeed = 2f, float enemyRange = 1f,
                                                 float detectionRange = 0f,
                                                 int enemyTraversal = LayerBits.Path,
                                                 int defenderW = 2, int defenderH = 2,
                                                 int aggroCapacity = 0)
        {
            var def = new MatchDefinition
            {
                Seed = seed,
                Mode = ModeDef.Default(),
                Map = map,
                Units = new[]
                {
                    new UnitDef
                    {
                        Id = "fixture_defender",
                        Health = 50f,
                        AttackRange = 3f,
                        AttackCooldown = 1f,
                        AttackTargetCount = 1,
                        BodyRadiusTiles = 0.5f,
                        FootprintWidth = defenderW,
                        FootprintHeight = defenderH,
                        PlacementLayers = LayerBits.Ground,
                        TraversalLayers = 0,
                        AggroCapacity = aggroCapacity,
                    },
                },
                Enemies = new[]
                {
                    new EnemyDef
                    {
                        Id = "fixture_enemy",
                        Health = 100f,
                        MoveSpeed = enemySpeed,
                        AttackRange = enemyRange,
                        AttackCooldown = 1f,
                        AttackTargetCount = 1,
                        BodyRadius = 0.25f,
                        TraversalLayers = enemyTraversal,
                        StabilityDamage = 1,
                        DetectionRange = detectionRange,
                        WaypointPathIndex = -1,
                    },
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>`nav` 하나 — 벽 없는 평지. 충돌·평활화 단위 테스트가 쓴다.</summary>
        public static NavGrid FlatNav(int width, int height, float tileSize = 1f)
        {
            var walk = new byte[width * height];
            for (int i = 0; i < walk.Length; i++) walk[i] = 1;
            return new NavGrid(walk, null, false, new int2(width, height), tileSize);
        }

        /// <summary>`walk` 배열을 직접 쥐는 nav. 벽을 찍어 넣고 싶을 때.</summary>
        public static NavGrid NavOf(byte[] walk, int width, int height, float tileSize = 1f)
            => new NavGrid(walk, null, false, new int2(width, height), tileSize);
    }
}
