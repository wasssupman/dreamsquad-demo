using Unity.Mathematics;

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
        };

        public static Scenario ByName(string name)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Name == name) return All[i];
            return null;
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
