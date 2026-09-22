using Unity.Mathematics;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 새 코어의 골든 시나리오 목록.
    //
    // ⚠ 여기 수치는 **게임 값이 아니라 고정구(fixture)** 다. 게임 값의 정본은 판 밖이고
    // (시트 → SO → `MatchDefinitionBuilder`) 그 경로는 `configHash` 가 감시한다.
    // 이 코퍼스가 SO 를 읽지 않는 이유는 둘이다:
    //   ① 헤드리스 lane(Unity 없이 `dotnet test`)에서도 구워야 한다.
    //   ② 이 unit 의 골든이 묻는 것은 「비어 있는 판이 결정론으로 도는가」이지
    //      「스탯이 얼마인가」가 아니다. 시트 드리프트가 이 축을 흔들면 안 된다.
    // 실제 저작 값으로 도는 판의 골든은 담당자가 생기는 unit 4 부터다.
    //
    // 코퍼스에 시나리오를 **추가**할 때는 기존 파일을 건드리지 않는다(`Bake Missing`).
    // 전체 재생성은 기존 기준선을 «지금 코드» 로 덮어써 그 시나리오가 지키던 회귀
    // 감시를 그 자리에서 무효로 만든다 — 옛 `SimGoldenMenu` 가 같은 함정을 적어 두었다.
    public static class CoreGoldenCorpus
    {
        public sealed class Scenario
        {
            public string Name;
            public int Seed;
            public int Ticks;

            /// <summary>정의표를 새로 굽는다. 시나리오가 정의를 **공유하지 않는** 이유는 판마다 상태가 있기 때문이다.</summary>
            public System.Func<MatchDefinition> BuildDefinition;

            /// <summary>커맨드 스케줄을 새로 만든다(하네스가 커서를 움직이므로 재사용 금지).</summary>
            public System.Func<CommandSchedule> BuildSchedule;
        }

        public static readonly Scenario[] All =
        {
            new Scenario
            {
                // 「비어 있는 판이 180초를 결정론으로 돈다」 — 이 unit 의 검증 질문 그 자체.
                Name = "empty_board",
                Seed = 1234,
                Ticks = 10800,
                BuildDefinition = () => Fixture(1234),
                BuildSchedule = () => new CommandSchedule(),
            },
            new Scenario
            {
                // 스폰 3 · 소멸 1 — id 발급 순번과 「모든 소멸은 소멸 이벤트를 낸다」를 증언한다.
                Name = "spawn_destroy",
                Seed = 4321,
                Ticks = 300,
                BuildDefinition = () => Fixture(4321),
                BuildSchedule = () => new CommandSchedule()
                    .Add(10, Command.DebugSpawnEnemy(0, new int2(0, 0)))
                    .Add(20, Command.DebugSpawnEnemy(0, new int2(1, 0)))
                    .Add(30, Command.DebugSpawnEnemy(0, new int2(2, 0)))
                    // 두 번째로 스폰된 개체(id 2)를 지운다 — 가운데를 지워야 목록 정렬이
                    // 유지되는지가 골든에 드러난다(끝을 지우면 잘못된 구현도 통과한다).
                    .Add(40, Command.DebugDestroy(new SimEntityId(2))),
            },
            new Scenario
            {
                // unit 2 — 「레인 2 · 적 6 · 골 1」. `GoalReached` 순서가 **스폰 순서와 같아야**
                // 한다. 같은 레인에서 나온 적들이 순서를 바꾸면 분리 누적이 순회 순서에
                // 의존한다는 뜻이다(M27 이 닫은 축).
                Name = "march_to_goal",
                Seed = 2001,
                Ticks = 1800,
                BuildDefinition = () => MarchFixture(2001),
                BuildSchedule = () =>
                {
                    var s = new CommandSchedule();
                    for (int i = 0; i < 3; i++)
                    {
                        s.Add(2 + i * 20, Command.DebugSpawnEnemyInLane(0, 0));
                        s.Add(2 + i * 20, Command.DebugSpawnEnemyInLane(0, 1));
                    }
                    return s;
                },
            },
            new Scenario
            {
                // unit 2 — 길목에 2×2 방어유닛을 놓으면 흐름장이 다시 구워지고 우회로가 선다.
                // 교착 0 = 적이 결국 골에 닿는다.
                //
                // ⚠ 이 시나리오의 적은 **거점 전담**이다(유닛을 안 노린다 — 라이브의 마음사냥꾼과
                // 같은 저작). 안 그러면 사거리에 든 순간 교전으로 멈춰 서고, 그 정지를 풀 수단
                // (전투)이 unit 3 에 있어서 **우회를 묻는 시나리오가 교전을 묻게 된다.**
                Name = "detour_obstacle",
                Seed = 2002,
                Ticks = 1800,
                BuildDefinition = () => DetourFixture(2002),
                BuildSchedule = () => new CommandSchedule()
                    .Add(2, Command.PlaceDefender(0, new int2(5, 1)))
                    .Add(10, Command.DebugSpawnEnemyInLane(0, 0)),
            },
            new Scenario
            {
                // unit 2 — 유한 감지 적이 방어유닛 앞에서 멈추고 표식은 **한 번**만 난다.
                Name = "detect_and_chase",
                Seed = 2003,
                Ticks = 1200,
                BuildDefinition = () => DetectFixture(2003),
                BuildSchedule = () => new CommandSchedule()
                    .Add(2, Command.PlaceDefender(0, new int2(6, 1)))
                    .Add(10, Command.DebugSpawnEnemyInLane(0, 0)),
            },
        };

        public static Scenario ByName(string name)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Name == name) return All[i];
            return null;
        }


        /// <summary>unit 2 골든의 판 — 12×5 빈 격자, 레인 2, 골 1. 방어유닛은 2×2 다(M29).</summary>
        public static MatchDefinition MarchFixture(int seed) => MapFixture(seed, detectionRange: 0f);

        /// <summary>같은 판인데 적이 유한 감지를 갖는다.</summary>
        public static MatchDefinition DetectFixture(int seed) => MapFixture(seed, detectionRange: 4f);

        /// <summary>같은 판인데 적이 **거점 전담**이다 — 방어유닛을 안 노리므로 교전으로 멈추지 않는다.</summary>
        public static MatchDefinition DetourFixture(int seed)
        {
            var def = MapFixture(seed, detectionRange: 0f);
            def.Enemies[0].TargetFactions = (int)Wassup.Battle.Units.Faction.DefenderCore;
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static MatchDefinition MapFixture(int seed, float detectionRange)
        {
            var map = new MapSnapshot
            {
                Width = 12,
                Height = 5,
                TileSize = 1f,
                Goals = new[] { new int2(11, 2) },
                Spawns = new[] { new int2(0, 1), new int2(0, 3) },
            };
            map.Normalize();

            var def = Fixture(seed);
            def.Map = map;
            def.Units[0].FootprintWidth = 2;
            def.Units[0].FootprintHeight = 2;
            def.Enemies[0].DetectionRange = detectionRange;
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>고정구 정의표. 방어 1종 · 적 1종 · 3×3 빈 판.</summary>
        public static MatchDefinition Fixture(int seed)
        {
            var def = new MatchDefinition
            {
                Seed = seed,
                Mode = ModeDef.Default(),
                Units = new[]
                {
                    new UnitDef
                    {
                        Id = "fixture_defender",
                        Health = 50f,
                        AttackRange = 3f,
                        AttackCooldown = 1f,
                        HitDelaySeconds = 0f,
                        AttackTargetCount = 1,
                        BodyRadiusTiles = 0.5f,
                        FootprintWidth = 1,
                        FootprintHeight = 1,
                        PlacementLayers = 1,
                        TraversalLayers = 0,
                        Role = 0,
                        AttackShape = 0,
                    },
                },
                Enemies = new[]
                {
                    new EnemyDef
                    {
                        Id = "fixture_enemy",
                        Health = 100f,
                        MoveSpeed = 2f,
                        AttackRange = 1f,
                        AttackCooldown = 1f,
                        HitDelaySeconds = 0f,
                        AttackTargetCount = 1,
                        BodyRadius = 0.25f,
                        TraversalLayers = 2,
                        EnemyClass = 0,
                        Tier = 0,
                        MinWaveNumber = 1,
                        MaxPerWave = 0,
                        StabilityDamage = 1,
                        DetectionRange = 0f,
                        AwakeningReward = 1,
                        AttackShape = 0,
                    },
                },
                Map = new MapSnapshot
                {
                    Width = 3,
                    Height = 3,
                    Spawns = new[] { new int2(1, 0) },
                    Goals = new[] { new int2(1, 2) },
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>시나리오 하나를 돌린다. 메뉴·테스트가 **같은 함수**를 부른다(두 벌이면 갈린다).</summary>
        public static CoreTrace Run(Scenario sc)
        {
            var result = CoreHarness.Run(sc.BuildDefinition(), sc.BuildSchedule(), sc.Ticks, sc.Name);
            return result.Trace;
        }
    }
}
