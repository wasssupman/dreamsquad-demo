using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 2 — 감지·어그로·사냥판의 규칙.
    public class DetectionRulesTests
    {
        private static List<CoreEvent> Listen(BattleMatch m, CoreEventKind kind)
        {
            var got = new List<CoreEvent>();
            m.Bus.Subscribe(kind, 0, e => got.Add(e));
            return got;
        }

        private static BattleMatch WithDefenderAndHunter(float detectionRange, out List<CoreEvent> marks)
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2), new int2(0, 2));
            var def = CoreMapFixtures.Definition(map, detectionRange: detectionRange,
                                                 defenderW: 1, defenderH: 1);
            var match = new BattleMatch(def);
            marks = Listen(match, CoreEventKind.Detected);
            match.Begin();
            match.Apply(Command.PlaceDefender(0, new int2(5, 1)));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            return match;
        }

        [Test]
        public void 감지_0_은_오늘과_같은_경로다()
        {
            // 부착 자체가 게이트다 — 분기가 아니라 부재로 표현한다.
            var match = WithDefenderAndHunter(0f, out var marks);
            for (int t = 0; t < 300; t++) match.Tick();

            var enemy = FindEnemy(match);
            Assert.IsNull(enemy.Detection, "감지 반경 0 이면 감지 상태 자체가 없다");
            Assert.AreEqual(0, marks.Count);
        }

        [Test]
        public void 발견은_전이_1회다()
        {
            // 매 틱 쏘면 초당 60건이라 표식이 화면을 덮고 트레이스가 무의미해진다.
            var match = WithDefenderAndHunter(4f, out var marks);
            for (int t = 0; t < 600; t++) match.Tick();

            Assert.AreEqual(1, marks.Count, $"전이 1회여야 한다(실제 {marks.Count})");
        }

        [Test]
        public void 유한_감지는_방어유닛_앞에서_멈춘다()
        {
            var match = WithDefenderAndHunter(4f, out _);
            for (int t = 0; t < 600; t++) match.Tick();

            var enemy = FindEnemy(match);
            Assert.IsTrue(enemy.Detection.Hunting, "물고 있다");
            Assert.IsTrue(enemy.Move.HoldingGround, "사격 칸에 도착해 멈춘다");
            Assert.Less(math.distance(enemy.Position, new float3(5f, 0f, 1f)), 2.5f,
                "방어유닛 근처까지 왔다");
        }

        [Test]
        public void 표식_쿨이_억제보다_길다()
        {
            // M9 — 관계가 뒤집히면 표식이 억제 창 안에서 두 번 난다.
            Assert.Greater(AiMovePhase.MarkCooldownSeconds, AiMovePhase.SuppressSeconds);
        }

        [Test]
        public void 무제한_감지는_유출_면제를_받는다()
        {
            // 「방어유닛을 전멸시켜야 골에 간다」는 저작된 성질이다. 유한 감지에 상속시키면
            // 감지가 **이 게임의 유일한 패배 통로**의 조절기가 된다.
            var map = CoreMapFixtures.Open(10, 5, new int2(9, 2), new int2(0, 2));
            var def = CoreMapFixtures.Definition(map, detectionRange: -1f, defenderW: 1, defenderH: 1);
            var match = new BattleMatch(def);
            var goals = Listen(match, CoreEventKind.GoalReached);
            match.Begin();
            match.Apply(Command.PlaceDefender(0, new int2(2, 1)));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));

            for (int t = 0; t < 1200; t++) match.Tick();
            Assert.AreEqual(0, goals.Count, "사냥판이 서 있는 동안엔 골을 밟아도 유출이 아니다");

            // 방어유닛이 사라지면 사냥판이 무너지고 다시 골로 간다(계약 5).
            match.Apply(Command.Retire(new SimEntityId(1)));
            for (int t = 0; t < 1200 && goals.Count == 0; t++) match.Tick();
            Assert.AreEqual(1, goals.Count, "전멸 뒤에는 마칭으로 돌아간다");
        }

        [Test]
        public void 사냥판_반경은_가장_짧은_사거리로_내려간다()
        {
            // M7 — 이질 사거리 헌터가 섞이면 짧은 쪽이 판을 정한다. min fold 를 빼면
            // 근접이 못 붙는다.
            var map = CoreMapFixtures.Open(11, 5, new int2(10, 2), new int2(0, 2));
            var def = CoreMapFixtures.Definition(map, detectionRange: -1f, defenderW: 1, defenderH: 1);
            def.Enemies = new[]
            {
                def.Enemies[0],                                    // 사거리 1
                CloneWithRange(def.Enemies[0], 4f),                // 사거리 4
            };
            var match = new BattleMatch(def);
            match.Begin();
            match.Apply(Command.PlaceDefender(0, new int2(5, 2)));
            match.Apply(Command.DebugSpawnEnemyInLane(1, 0));      // 원거리만 먼저
            match.Tick();

            int longOnly = match.Map.Hunt.DistAt(new int2(1, 2));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));      // 근접 합류
            match.Tick();
            int folded = match.Map.Hunt.DistAt(new int2(1, 2));

            Assert.Greater(folded, longOnly,
                "짧은 사거리가 합류하면 소스 디스크가 줄어 도착점이 더 안쪽으로 간다");
        }

        [Test]
        public void 어그로는_수용량을_넘지_않고_도발은_그것을_우회한다()
        {
            var map = CoreMapFixtures.Open(10, 5, new int2(9, 2), new int2(0, 2));
            var def = CoreMapFixtures.Definition(map, defenderW: 1, defenderH: 1, aggroCapacity: 1);
            var match = new BattleMatch(def);
            var acquired = Listen(match, CoreEventKind.AggroAcquired);
            match.Begin();
            match.Apply(Command.PlaceDefender(0, new int2(5, 2)));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            match.Tick();

            var guardian = new SimEntityId(1);
            match.World.AggroRequests.Add(AggroRequest.Hit(new SimEntityId(2), guardian));
            match.World.AggroRequests.Add(AggroRequest.Hit(new SimEntityId(3), guardian));
            match.Tick();
            Assert.AreEqual(1, acquired.Count, "수용량 1 — 먼저 온 쪽이 이긴다");

            match.World.AggroRequests.Add(AggroRequest.Taunted(new SimEntityId(3), guardian, 3f));
            match.Tick();
            Assert.AreEqual(2, acquired.Count, "도발은 수용량과 선점 둘을 우회한다");
            Assert.IsTrue(match.World.Find(new SimEntityId(3)).Aggro.Taunted);
        }

        [Test]
        public void 장애물이_바뀌면_어그로가_풀리고_도발은_표시만_남는다()
        {
            // M10 — 리무버 둘. 도발을 통째로 풀면 재획득 경로가 없어 도발이 그 자리에서 사라진다.
            var map = CoreMapFixtures.Open(10, 5, new int2(9, 2), new int2(0, 2));
            var def = CoreMapFixtures.Definition(map, defenderW: 1, defenderH: 1, aggroCapacity: 4);
            var match = new BattleMatch(def);
            match.Begin();
            match.Apply(Command.PlaceDefender(0, new int2(5, 2)));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            match.Tick();

            var guardian = new SimEntityId(1);
            match.World.AggroRequests.Add(AggroRequest.Hit(new SimEntityId(2), guardian));
            match.World.AggroRequests.Add(AggroRequest.Taunted(new SimEntityId(3), guardian, 10f));
            match.Tick();

            var hit = match.World.Find(new SimEntityId(2));
            var taunted = match.World.Find(new SimEntityId(3));
            Assert.IsFalse(hit.Aggro.Target.IsNone);
            Assert.IsFalse(taunted.Aggro.Target.IsNone);

            match.Apply(Command.DebugSetObstacle(new int2(3, 3), true));
            match.Tick();

            Assert.IsTrue(hit.Aggro.Target.IsNone, "히트 어그로는 풀린다");
            Assert.IsFalse(taunted.Aggro.Target.IsNone, "도발은 표시가 남는다");
            Assert.IsNull(taunted.Aggro.Chase, "필드만 뗀다");
        }

        [Test]
        public void 도발_시한이_지나면_풀린다()
        {
            var map = CoreMapFixtures.Open(10, 5, new int2(9, 2), new int2(0, 2));
            var def = CoreMapFixtures.Definition(map, defenderW: 1, defenderH: 1, aggroCapacity: 1);
            var match = new BattleMatch(def);
            match.Begin();
            match.Apply(Command.PlaceDefender(0, new int2(5, 2)));
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            match.Tick();

            match.World.AggroRequests.Add(AggroRequest.Taunted(new SimEntityId(2), new SimEntityId(1), 0.5f));
            match.Tick();
            Assert.IsFalse(match.World.Find(new SimEntityId(2)).Aggro.Target.IsNone);

            // ⚠ unit 3 부터 **히트가 어그로를 다시 문다.** 이 테스트가 묻는 것은 「시한이 준다」
            // 하나라, 가디언의 공격을 떼어 재획득이 끼어들지 못하게 한다. 재획득 쪽 증언은
            // `CombatRulesTests.가디언은_때린_적을_끌어온다` 가 따로 한다.
            match.World.Find(new SimEntityId(1)).Attack = null;

            for (int t = 0; t < 60; t++) match.Tick();
            Assert.IsTrue(match.World.Find(new SimEntityId(2)).Aggro.Target.IsNone,
                "0 은 무기한 센티널이고 >0 만 감소한다");
        }

        private static Unit FindEnemy(BattleMatch match)
        {
            for (int i = 0; i < match.World.Units.Count; i++)
                if (match.World.Units[i].Kind == UnitKind.Enemy) return match.World.Units[i];
            return null;
        }

        private static EnemyDef CloneWithRange(EnemyDef src, float range)
        {
            src.AttackRange = range;
            src.Id = src.Id + "_long";
            return src;
        }
    }
}
