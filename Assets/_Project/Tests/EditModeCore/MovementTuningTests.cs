using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild 5a 후속 — **분산 값이 정의표에서 온다**는 배선을 재는 테스트.
    //
    // `SpawnSpread` 의 순수 수학은 `MovePureMathTests` 가 이미 잰다. 여기서 묻는 것은 다른
    // 질문이다: 그 함수에 **저작값이 실제로 닿는가**. 이식 중 이 자리에 리터럴
    // (`LaneFraction(_, 5, 0.4f, 1f)`)이 박혀 있었고, 그래서 적이 퍼지는 폭이 저작 밖에서
    // 정해지고 `configHash` 가 그것을 보지 못했다.
    public class MovementTuningTests
    {
        private static MatchDefinition DefinitionWith(int laneCount, float fraction,
                                                      float topScale = 1f, float radius = 0.25f)
        {
            var def = CoreMatchFixtures.Definition();
            def.Movement = new MovementTuningDef
            {
                AgentRadiusTiles = radius,
                SpawnSubLaneCount = laneCount,
                SpawnSpreadFraction = fraction,
                SpawnSpreadTopScale = topScale,
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        // 같은 레인 입구에서 연달아 나온 적들의 자리. 레인 배정은 스폰 **순번** 기반이라
        // 같은 순번이면 같은 자리다(RNG 없는 결정론).
        private static float3[] SpawnPositions(MatchDefinition def, int count)
        {
            var match = CoreMatchFixtures.BeginBattle(def);
            var positions = new float3[count];
            for (int i = 0; i < count; i++)
            {
                int before = match.World.Units.Count;
                match.Apply(Command.DebugSpawnEnemyInLane(defIndex: 0, lane: 0));
                Assert.Greater(match.World.Units.Count, before, $"{i}번째 적이 안 나왔다");
                positions[i] = match.World.Units[match.World.Units.Count - 1].Position;
            }
            return positions;
        }

        [Test]
        public void SubLaneCount_FromDefinition_DecidesWhenLanesRepeat()
        {
            // 3레인이면 4번째가 1번째와 같은 레인이다. 이 「언제 반복되나」가 곧 저작값이 닿았다는 증거다 —
            // 옛 리터럴(5레인)이 살아 있으면 4번째는 1번째와 다른 자리에 선다.
            var p = SpawnPositions(DefinitionWith(laneCount: 3, fraction: 0.2f), 4);

            Assert.AreEqual(p[0].x, p[3].x, 1e-5f, "3레인이면 4번째가 1번째와 같은 레인");
            Assert.AreEqual(p[0].z, p[3].z, 1e-5f, "3레인이면 4번째가 1번째와 같은 레인");
            Assert.AreNotEqual(p[0].z, p[1].z, "2번째는 다른 레인이라 옆으로 벌어진다");
        }

        [Test]
        public void SubLaneCount_Five_DoesNotRepeatAtFour()
        {
            // 위 테스트의 짝. 「4번째가 같다」가 레인 수와 **무관하게** 성립하는 우연이 아님을 못 박는다.
            var p = SpawnPositions(DefinitionWith(laneCount: 5, fraction: 0.2f), 4);
            Assert.AreNotEqual(p[0].z, p[3].z, "5레인이면 4번째는 아직 한 바퀴를 안 돌았다");
        }

        [Test]
        public void SpreadFractionZero_PutsEveryoneAtCellCenter()
        {
            // **0 이 곧 「분산 끔」**이다 — 옛 전투의 `spawnSpreadEnabled` bool 축을 따로 옮기지
            // 않은 근거가 이 줄이다. 끄는 방법이 이미 값 안에 있다.
            var def = DefinitionWith(laneCount: 3, fraction: 0f);
            var p = SpawnPositions(def, 4);
            float3 center = def.Map.CellCenter(def.Map.Spawns[0]);
            for (int i = 0; i < p.Length; i++)
            {
                Assert.AreEqual(center.x, p[i].x, 1e-5f, $"{i}번째가 칸 중앙이 아니다");
                Assert.AreEqual(center.z, p[i].z, 1e-5f, $"{i}번째가 칸 중앙이 아니다");
            }
        }

        [Test]
        public void AgentRadius_ComesFromDefinition()
        {
            var match = CoreMatchFixtures.BeginBattle(DefinitionWith(3, 0.2f, radius: 0.31f));
            match.Apply(Command.DebugSpawnEnemyInLane(defIndex: 0, lane: 0));
            var u = match.World.Units[match.World.Units.Count - 1];
            Assert.IsNotNull(u.Move);
            Assert.AreEqual(0.31f, u.Move.Radius, 1e-5f, "몸 반지름이 정의표에서 와야 한다");
        }

        [Test]
        public void Defaults_AreTheOldSceneValues()
        {
            // 기본값이 라이브와 갈리면, 고정구로 만든 판과 씬에서 도는 판이 **다른 게임**이 된다.
            var d = MovementTuningDef.Default();
            Assert.AreEqual(0.25f, d.AgentRadiusTiles, 1e-6f);
            Assert.AreEqual(3, d.SpawnSubLaneCount);
            Assert.AreEqual(0.2f, d.SpawnSpreadFraction, 1e-6f);
            Assert.AreEqual(0.5f, d.SpawnSpreadTopScale, 1e-6f);
        }

        [Test]
        public void Movement_MovesTheConfigHash()
        {
            // 이 값이 움직이면 같은 seed 라도 다른 판이다 — 골든이 그것을 「조건이 바뀌었다」로
            // 정직하게 말해야 하고, 그러려면 해시에 들어 있어야 한다.
            var a = DefinitionWith(laneCount: 3, fraction: 0.2f);
            var b = DefinitionWith(laneCount: 5, fraction: 0.2f);
            Assert.AreNotEqual(a.ConfigHash, b.ConfigHash, "레인 수가 해시에 들어야 한다");

            var c = DefinitionWith(laneCount: 3, fraction: 0.4f);
            Assert.AreNotEqual(a.ConfigHash, c.ConfigHash, "분산 폭이 해시에 들어야 한다");
        }

        // unit 7d 후속(M1) — 「기본값이면 canonical 줄을 안 쓴다」가 **float 정확 비교**면 SO 직렬화 왕복(0.83 → 0.83000001)
        // 1 ulp 로 해시가 뒤집힌다. 판이 같은데 골든이 「조건이 바뀌었다」고 오보한다.
        private static string HashWith(System.Func<MovementTuningDef, MovementTuningDef> edit)
        {
            var def = CoreMatchFixtures.Definition();
            def.Movement = edit(MovementTuningDef.Default());
            return def.ComputeConfigHash();
        }

        private static float Ulp(float v, int steps) => math.asfloat(math.asint(v) + steps);

        [Test]
        public void 기본값_이웃_1ulp_는_canonical_줄을_안_쓴다()
        {
            string baseline = HashWith(m => m);
            Assert.AreEqual(baseline, HashWith(m => { m.BossLeapFlightSeconds = Ulp(m.BossLeapFlightSeconds, +1); return m; }), "+1 ulp");
            Assert.AreEqual(baseline, HashWith(m => { m.BossLeapFlightSeconds = Ulp(m.BossLeapFlightSeconds, -1); return m; }), "-1 ulp");
            Assert.AreEqual(baseline, HashWith(m => { m.SplitSpreadFraction = Ulp(m.SplitSpreadFraction, +1); return m; }), "+1 ulp");
            Assert.AreEqual(baseline, HashWith(m => { m.SplitSpreadFraction = Ulp(m.SplitSpreadFraction, -1); return m; }), "-1 ulp");
        }

        [Test]
        public void 기본값에서_실제로_움직이면_해시가_움직인다()
        {
            string baseline = HashWith(m => m);
            Assert.AreNotEqual(baseline, HashWith(m => { m.BossLeapFlightSeconds *= 2f; return m; }));
            Assert.AreNotEqual(baseline, HashWith(m => { m.SplitSpreadFraction *= 0.5f; return m; }));
            Assert.AreNotEqual(baseline, HashWith(m => { m.SplitMaxChildren += 1; return m; }), "분열 상한도 해시에 든다");
        }
    }
}
