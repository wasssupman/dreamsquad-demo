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
                // ⚠ unit 4 에서 `PlaceDefender` → `DebugSpawnDefender` 로 바꿨다. 이 시나리오가
                // 묻는 것은 **우회**이지 배치 판정이 아니고, 판정이 실리면 그 사건들이
                // 이동 골든을 덮어 「무엇이 깨졌나」를 읽을 수 없게 된다. 결과 트레이스는
                // 바꾸기 전과 **완전히 같다**(같은 틱·같은 자리·같은 점유). 배치 판정의
                // 골든은 `kill_race_3min` 이 따로 진다.
                BuildSchedule = () => new CommandSchedule()
                    .Add(2, Command.DebugSpawnDefender(0, new int2(5, 1)))
                    .Add(10, Command.DebugSpawnEnemyInLane(0, 0)),
            },
            new Scenario
            {
                // unit 3 — 조각 A 의 검증 질문 그 자체: **헤드리스로 3분 판이 완주하는가.**
                // 적 30기(레인 2 교대) vs 방어유닛 4기. 옛 코퍼스 `basic` 의 후계이고,
                // 그쪽과는 **거시 지표**(킬 수·유출 수·생존 방어유닛)로만 대조한다(계약 3).
                //
                // ⚠ 여기 수치도 **고정구**다. 게임 값이 아니라 「3분 안에 전부 죽고 판이
                // 끝나는가」를 물을 수 있는 최소 조건이고, 시트 드리프트가 이 축을 흔들면 안 된다.
                Name = "kill_race_basic",
                Seed = 3001,
                Ticks = 10800,
                BuildDefinition = () => KillRaceFixture(3001),
                BuildSchedule = () =>
                {
                    var s = new CommandSchedule();
                    // 방어유닛 4기 — 두 레인에 둘씩. 배치 판정(코스트·쿨다운·상한)은 unit 4 라
                    // 디버그 스폰으로 세운다(시나리오가 곧 의도다).
                    // ⚠ 레인마다 **둘씩 붙여** 세운다. 적은 교전 정책이 `Halt` 라 사거리 안에
                    // 들어오면 **거기서 멈춘다** — 멀리 둔 방어유닛은 판 내내 한 발도 안 쏜다.
                    // (레인당 하나 + 뒤쪽에 하나로 세웠더니 뒤쪽 둘이 통째로 놀았다.)
                    s.Add(2, Command.DebugSpawnDefender(0, new int2(3, 1)));
                    s.Add(2, Command.DebugSpawnDefender(0, new int2(4, 1)));
                    s.Add(2, Command.DebugSpawnDefender(0, new int2(3, 3)));
                    s.Add(2, Command.DebugSpawnDefender(0, new int2(4, 3)));
                    // 적 30기 — 5초 간격으로 레인 교대.
                    for (int i = 0; i < 30; i++)
                        s.Add(60 + i * 300, Command.DebugSpawnEnemyInLane(0, i % 2));
                    return s;
                },
            },
            new Scenario
            {
                // unit 2 — 유한 감지 적이 방어유닛 앞에서 멈추고 표식은 **한 번**만 난다.
                Name = "detect_and_chase",
                Seed = 2003,
                Ticks = 1200,
                BuildDefinition = () => DetectFixture(2003),
                // 위와 같은 이유로 판정 없는 스폰을 쓴다(이 시나리오가 묻는 것은 **감지**다).
                BuildSchedule = () => new CommandSchedule()
                    .Add(2, Command.DebugSpawnDefender(0, new int2(6, 1)))
                    .Add(10, Command.DebugSpawnEnemyInLane(0, 0)),
            },
            new Scenario
            {
                // unit 4 — **조각 A 의 검증 질문에 진짜 웨이브로 답하는 판.**
                // 라이브 덱(`Deck_Duel`)의 손잡이를 그대로 옮긴 고정구 덱으로 3분을 완주한다:
                // 고정 웨이브 시드 · 상한 간격 20초 · 리드인 2초 · 지수 성장 1.12 · 보스 9웨이브마다 ·
                // 컨셉 블록 3웨이브 · 램프 break 15/12. 배치 판정(코스트·상한·창)도 이 판이 진다.
                Name = "kill_race_3min",
                Seed = 4242,
                Ticks = 11100,   // 배치 창 180틱(3초 카운트다운) + 180초 전투 + 여유
                BuildDefinition = () => WaveFixture(4242, GoalKind.KillScoreTimed, ClockKind.FixedLimit, 0),
                BuildSchedule = () =>
                {
                    var s = new CommandSchedule();
                    // ⚠ 라이브는 배치 **입력이 꺼진** 모드라 창 안의 배치는 거절된다. 그래서
                    // 창이 자동으로 닫힌 뒤(180틱)에 놓는다 — 전투 중 배치가 라이브의 모습이다.
                    s.Add(181, Command.PlaceDefender(0, new int2(3, 1)));
                    s.Add(181, Command.PlaceDefender(0, new int2(4, 1)));
                    s.Add(181, Command.PlaceDefender(0, new int2(3, 3)));
                    s.Add(181, Command.PlaceDefender(0, new int2(4, 3)));
                    // 못 놓는 자리 한 번 — 거절도 규칙이라 골든이 증언한다.
                    s.Add(182, Command.PlaceDefender(0, new int2(3, 1)));
                    return s;
                },
            },
            new Scenario
            {
                // unit 4 — 「8웨이브를 끝까지 막았나」. 시계는 세기만 하고(만료 없음) 끝내는 것은 **목표**다.
                Name = "wave_clear_8",
                Seed = 8080,
                Ticks = 20000,
                BuildDefinition = () => WaveFixture(8080, GoalKind.WaveClear, ClockKind.CountUp, 8),
                BuildSchedule = () => ClearSchedule(),
            },
            new Scenario
            {
                // unit 4 — 같은 신호, 다른 점수. 정렬이 반대라 제출 payload 가 방향을 동봉한다.
                Name = "time_attack_8",
                Seed = 8081,
                Ticks = 20000,
                BuildDefinition = () => WaveFixture(8081, GoalKind.TimeAttack, ClockKind.CountUp, 8),
                BuildSchedule = () => ClearSchedule(),
            },
            new Scenario
            {
                // unit 4 — **마음이 부서지는 판.** 돌격형(마음을 못 때리는 적)이 골에 닿아
                // 산화하며 안정도를 깎고, 첫 붕괴가 곧 판의 끝이다. `WaveClear` 목표라
                // 그 판의 «의미»는 **패배**다 — 통로는 여전히 `stress_full` 하나다.
                Name = "heart_collapse",
                Seed = 9090,
                Ticks = 6000,
                BuildDefinition = () => CollapseFixture(9090),
                // **커맨드가 하나도 없다.** 방어유닛을 안 놓고, 배치 창은 카운트다운으로
                // 스스로 닫힌다 — 그 뒤는 규칙이 알아서 한다는 것이 이 시나리오의 질문이다.
                BuildSchedule = () => new CommandSchedule(),
            },
            new Scenario
            {
                // unit 4 — **공성형이 마음을 깎는 판.** 다른 골든이 한 번도 안 지나는 구간이라
                // (그쪽은 방어유닛이 다 잡아 버린다) 이 unit 이 연 경로를 여기서만 증언한다:
                //   방어 본능이 먼저 서서 마음을 **표적에서 뺀다** → 적이 본능을 부순다 →
                //   방패가 떨어지고 마음 타워가 조준 후보가 된다 → 그 피해가 담당자의
                //   저수지로 흘러 판이 `stress_full` 로 끝난다.
                // 방어유닛을 한 기도 안 놓는 것이 이 시나리오의 입력이다.
                Name = "siege_instinct_fall",
                Seed = 9191,
                Ticks = 6000,
                BuildDefinition = () => SiegeFixture(9191),
                BuildSchedule = () => new CommandSchedule(),
            },
        };

        // 8웨이브를 «막는» 판의 입력: 배치 창이 자동으로 닫힌 뒤 방어유닛 4기를 두 레인에 붙여 세운다.
        private static CommandSchedule ClearSchedule()
            => new CommandSchedule()
                .Add(181, Command.PlaceDefender(0, new int2(3, 1)))
                .Add(181, Command.PlaceDefender(0, new int2(4, 1)))
                .Add(181, Command.PlaceDefender(0, new int2(3, 3)))
                .Add(181, Command.PlaceDefender(0, new int2(4, 3)));

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
            def.Enemies[0].TargetFactions = (int)Wassup.Skills.Faction.DefenderCore;
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>
        /// unit 3 골든의 판 — 같은 12×5 격자에 **서로 때릴 수 있는** 저작을 얹는다.
        /// 방어유닛은 사거리 3·초당 25, 적은 체력 180·사거리 1·초당 5 다.
        /// 이 배합이 노린 것: 적이 **사거리 안으로 들어와 멈추고 맞받아친다**(교전 정책 Halt).
        /// 방어유닛이 너무 세면 적이 스폰 지점에서 녹아 「맞는 쪽」 규칙이 한 번도 안 돈다.
        /// </summary>
        public static MatchDefinition KillRaceFixture(int seed)
        {
            var def = MapFixture(seed, detectionRange: 0f);

            ref var d = ref def.Units[0];
            d.Health = 1000f;
            d.AttackRange = 3f;
            d.AttackCooldown = 1f;
            d.FootprintWidth = 1;
            d.FootprintHeight = 1;
            d.BodyRadiusTiles = 0.5f;
            d.Attack = AttackDef.Default();
            d.Attack.Outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 25f },
            };

            ref var e = ref def.Enemies[0];
            e.Health = 180f;
            e.MoveSpeed = 1.5f;
            e.AttackRange = 1f;
            e.AttackCooldown = 1f;
            e.Attack = AttackDef.Default();
            e.Attack.Outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 5f },
            };

            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>
        /// unit 4 의 판 — **진짜 웨이브가 도는 고정구.**
        ///
        /// 덱 손잡이는 라이브(`Deck_Duel`)에서 그대로 옮겼다(고정 웨이브 시드 · 상한 간격 20 ·
        /// 리드인 2 · 내부 간격 0.5 · 수량 5~24 · 성장 1.12 · 당김 3 · 보스 9웨이브마다 호위 3~4 ·
        /// 컨셉 블록 3 · 램프 break 15/12). **스탯은 고정구**다 — 시트 드리프트가 이 축을
        /// 흔들면 안 되기 때문이고, 그것이 코퍼스가 SO 를 안 읽는 이유다.
        /// </summary>
        public static MatchDefinition WaveFixture(int seed, GoalKind goal, ClockKind clock, int targetWaves)
        {
            var def = MapFixture(seed, detectionRange: 0f);

            // 방어유닛 — 사거리 3 · 초당 25. 코스트 2 에 판 상한 4 라 라이브 시작 자원 10 으로
            // 정확히 4기를 세울 수 있다(다섯 번째는 자리가 물려 거절된다).
            ref var d = ref def.Units[0];
            d.Health = 1000f;
            d.AttackRange = 3f;
            d.AttackCooldown = 1f;
            d.FootprintWidth = 1;
            d.FootprintHeight = 1;
            d.BodyRadiusTiles = 0.5f;
            d.Cost = 2;
            d.MaxOnBoard = 4;
            d.DeathCooldown = 10f;
            d.RetireCooldownRatio = 0.4f;
            d.AwakeningReward = 4;
            d.Attack = AttackDef.Default();
            d.Attack.Outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 25f },
            };

            def.Enemies = new[]
            {
                Enemy(def.Enemies[0], "grunt", health: 120f, cls: 1, reward: 2, minWave: 1),
                Enemy(def.Enemies[0], "ranger", health: 90f, cls: 2, reward: 2, minWave: 1),
                Enemy(def.Enemies[0], "boss", health: 600f, cls: 3, reward: 5, minWave: 1),
            };

            def.WaveDeck = new Wave.WaveDeckDef
            {
                GeneratorVersion = 7,
                WaveSeed = 20261972,        // 비0 = **같은 맵 같은 웨이브**(라이브와 같은 손잡이)
                TimerDurationSec = 180f,
                MinWaveCount = 100,
                MaxWaveCount = 100,
                MinUnitsPerWave = 5,
                MaxUnitsPerWave = 24,
                WaveCountJitter = 1,
                IntraWaveSpacingSec = 0.5f,
                MaxWaveIntervalSec = 20f,
                SpawnLeadInSec = 2f,
                UnitGrowthPerWave = 1.12f,
                MaxPullsPerClear = 3,
                BossWaveInterval = 9,
                BossEscortMin = 3,
                BossEscortMax = 4,
                EnemyPool = new[] { 0, 1 },
                BossPool = new[] { 2 },
                Concepts = Concepts(),
                ConceptHoldWaves = 3,
                RampBreakWave = 15,
                RampBreakUnits = 12,
            };

            // ⚠ 모드 값은 **라이브 에셋**에서 옮겼다(코드 기본값이 아니다 — 「기획 그대로」의
            // 기준은 에셋이다). 출처:
            //   `Data/Config/BattleConfig.asset`      placementPhaseEnabled 0 · countdown 3
            //   `Data/Config/DefaultCostConfig.asset` 시작 10 · 상한 10 · 초당 0.35
            //   `Data/Dreamcatcher/AwakeningConfig.asset`     게이지 20/100 · 손패 4 · 부착 3
            //   `Data/Dreamcatcher/DeckRuleConfig_Default.asset`  덱 10
            // **유닛 스탯은 여전히 고정구**다 — 그쪽은 시트가 매일 움직이고, 이 골든이 묻는 것은
            // 「판이 규칙대로 도는가」이지 「스탯이 얼마인가」가 아니다.
            var mode = ModeDef.Default();
            mode.ModeId = goal == GoalKind.KillScoreTimed ? "kill_score_timed"
                        : goal == GoalKind.WaveClear ? "wave_clear"
                        : "time_attack";
            mode.Goal = goal;
            mode.Clock = clock;
            mode.TargetWaves = targetWaves;
            mode.AllowSubmit = goal == GoalKind.KillScoreTimed;
            mode.SubmitsReport = goal == GoalKind.KillScoreTimed;
            // 라이브는 배치 **입력이 꺼져 있고** 3초 카운트다운으로 자동 시작한다. 창이 닫히는
            // 그 신호가 코스트 재생을 켠다(X24) — 그래서 골든이 그 순간을 지난다.
            mode.PlacementInputEnabled = false;
            mode.PlacementSeconds = 3f;
            mode.Cost = new CostDef { Start = 10f, Max = 10f, RegenPerSec = 0.35f };
            mode.HandSize = 4;
            mode.AttachCap = 3;
            mode.Awakening = new AwakeningDef { Start = 20f, Max = 100f };
            mode.DeckSize = 10;
            mode.PublicActiveCount = 2;
            mode.BoardCap = 8;
            def.Mode = mode;

            // 8웨이브를 끝낼 수 있게 플랜을 그만큼만 굽는다(목표 웨이브 = 플랜 길이).
            if (targetWaves > 0)
            {
                def.WaveDeck.MinWaveCount = targetWaves;
                def.WaveDeck.MaxWaveCount = targetWaves;
            }

            def.Heart = HeartDef.Default();
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>
        /// **마음이 부서지는 판.** 적이 `DefenderUnit` 만 노리므로 마음을 때릴 수 없고
        /// (= 돌격형), 골에 닿으면 안정도를 깎고 산화한다. 방어유닛이 없어 전부 통과한다.
        /// 마음 체력을 작게 두어 몇 기만에 무너진다.
        /// </summary>
        public static MatchDefinition CollapseFixture(int seed)
        {
            var def = WaveFixture(seed, GoalKind.WaveClear, ClockKind.CountUp, 8);
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                def.Enemies[i].TargetFactions = (int)Wassup.Skills.Faction.DefenderUnit;
                def.Enemies[i].StabilityDamage = 60;
            }
            def.Heart = new HeartDef { MaxHealth = 200f, KillHealPerAwakening = 0f };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>
        /// **공성으로 마음이 무너지는 판.** `CollapseFixture` 의 짝이고 다른 축을 묻는다 —
        /// 그쪽은 돌격형(마음을 **못 때리는** 적)이 닿아서 산화하는 길이고, 이쪽은
        /// 공성형이 마음 타워를 **때려서** 깎는 길이다. 통로는 둘 다 `stress_full` 하나다.
        ///
        /// 적의 타겟 마스크를 **손대지 않는다** — 미저작(0)의 기본값이 「상대 진영 전부」라
        /// 마음도 본능도 그 안에 있다. 손대는 순간 이 골든이 묻는 것이 기본값이 아니게 된다.
        /// </summary>
        public static MatchDefinition SiegeFixture(int seed)
        {
            var def = WaveFixture(seed, GoalKind.WaveClear, ClockKind.CountUp, 8);
            def.Heart = new HeartDef { MaxHealth = 200f, KillHealPerAwakening = 0f };

            // 방어 본능 하나 — 공격은 안 한다(방패로만 선다). 체력을 얇게 둬서 판 안에서
            // 실제로 무너지게 한다: 「방패가 깨지는 순간」이 이 골든의 가운데 토막이다.
            def.Structures = new[]
            {
                new StructureDef
                {
                    Id = "fixture_instinct",
                    Health = 150f,
                    AttackRange = 0f,
                    AttackCooldown = 1f,
                    AttackTargetCount = 1,
                    TargetFactions = 0,
                    Attack = AttackDef.Default(),
                },
            };
            def.Map.Structures = new[]
            {
                new StructureSpot
                {
                    Cell = new int2(8, 2),
                    Faction = (int)Wassup.Skills.Faction.DefenderInstinct,
                    Footprint = StructureSize.Instinct,
                    DefIndex = 0,
                },
            };
            def.Map.CloseReservedPlacement();
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static EnemyDef Enemy(EnemyDef template, string id, float health, int cls,
                                      int reward, int minWave)
        {
            var e = template;
            e.Id = id;
            e.Health = health;
            e.EnemyClass = cls;
            e.AwakeningReward = reward;
            e.MinWaveNumber = minWave;
            e.MaxPerWave = 0;
            e.MoveSpeed = 1.5f;
            e.AttackRange = 1f;
            e.AttackCooldown = 1f;
            e.StabilityDamage = 1;
            e.Attack = AttackDef.Default();
            e.Attack.Outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 5f },
            };
            return e;
        }

        // 컨셉 2종 — 「협공」(두 입구 동시)과 「밀집」(한 입구 집중). 둘 다 지상이고
        // 요구 입구 수가 맵의 스폰 수(2) 이하라 폴백으로 떨어지지 않는다.
        private static Wave.WaveConceptDef[] Concepts() => new[]
        {
            new Wave.WaveConceptDef
            {
                Id = "pincer",
                DisplayName = "협공",
                Weight = 1f,
                MinWaveNumber = 1,
                CountMul = 1f,
                Slots = new[]
                {
                    new Wave.WaveSlotDef { ClassFilter = 0, LaneGroup = 0, PathIndex = -1 },
                    new Wave.WaveSlotDef { ClassFilter = 0, LaneGroup = 1, PathIndex = -1 },
                },
                VariantSlots = System.Array.Empty<Wave.WaveSlotDef>(),
            },
            new Wave.WaveConceptDef
            {
                Id = "massed",
                DisplayName = "밀집",
                Weight = 1f,
                MinWaveNumber = 1,
                CountMul = 0.6f,
                Slots = new[]
                {
                    new Wave.WaveSlotDef { ClassFilter = 0, LaneGroup = 0, PathIndex = -1 },
                },
                // 변주 1슬롯 — 블록 가운데 웨이브에 **끼어든다**(교체가 아니다).
                VariantSlots = new[]
                {
                    new Wave.WaveSlotDef { ClassFilter = 0, LaneGroup = 1, PathIndex = -1 },
                },
            },
        };

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
                        // 공격 저작은 **명시**한다. `default(AttackDef)` 도 안전하게 접히지만
                        // (표 밖 참조는 빌드에서 근접으로 접힌다), 고정구가 그 안전망에
                        // 기대면 안전망이 언제 깨졌는지 아무도 모른다.
                        Attack = AttackDef.Default(),
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
                        Attack = AttackDef.Default(),
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
