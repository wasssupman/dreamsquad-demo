using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 2 — 판을 실제로 돌려 규칙을 묻는다.
    public class MovementRulesTests
    {
        private static List<CoreEvent> Listen(BattleMatch m, CoreEventKind kind)
        {
            var got = new List<CoreEvent>();
            m.Bus.Subscribe(kind, 0, e => got.Add(e));
            return got;
        }

        [Test]
        public void 적이_골까지_걸어간다()
        {
            var map = CoreMapFixtures.Open(8, 3, new int2(7, 1), new int2(0, 1));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            var goals = Listen(match, CoreEventKind.GoalReached);
            match.Begin();

            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            for (int t = 0; t < 600 && goals.Count == 0; t++) match.Tick();

            Assert.AreEqual(1, goals.Count, "7칸을 초속 2로 가면 4초 안에 닿는다");
        }

        [Test]
        public void 골_도달은_1회_고정이다()
        {
            var map = CoreMapFixtures.Open(6, 3, new int2(5, 1), new int2(0, 1));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            var goals = Listen(match, CoreEventKind.GoalReached);
            match.Begin();

            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            for (int t = 0; t < 600; t++) match.Tick();

            Assert.AreEqual(1, goals.Count, "골 칸에 앉아 있어도 사건은 한 번뿐이다");
            Assert.IsTrue(match.World.Units[0].Move.PastGoal);
        }

        [Test]
        public void 공성형은_골에서_살아남는다는_것을_사건이_말한다()
        {
            // `canSiege` = 그 적이 방어 마음을 때릴 수 있나. 소비는 unit 3·4 가 나눠 갖는다.
            var map = CoreMapFixtures.Open(5, 3, new int2(4, 1), new int2(0, 1));

            var charge = CoreMapFixtures.Definition(map);
            charge.Enemies[0].TargetFactions = (int)Faction.DefenderUnit;   // 마음 비트 없음
            var m1 = new BattleMatch(charge);
            var g1 = Listen(m1, CoreEventKind.GoalReached);
            m1.Begin();
            m1.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            for (int t = 0; t < 400 && g1.Count == 0; t++) m1.Tick();
            Assert.AreEqual(1, g1.Count);
            Assert.AreEqual(0, g1[0].Arg, "돌격형 — 유출");

            var siege = CoreMapFixtures.Definition(map);
            siege.Enemies[0].TargetFactions = (int)(Faction.DefenderUnit | Faction.DefenderCore);
            var m2 = new BattleMatch(siege);
            var g2 = Listen(m2, CoreEventKind.GoalReached);
            m2.Begin();
            m2.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            for (int t = 0; t < 400 && g2.Count == 0; t++) m2.Tick();
            Assert.AreEqual(1, g2.Count);
            Assert.AreEqual(1, g2[0].Arg, "공성형 — 살아서 거점을 판다");
        }

        [Test]
        public void 길을_막으면_돌아간다()
        {
            // 2×2 방어유닛이 복도를 덮으면 흐름장이 다시 구워지고 우회로가 선다.
            var map = CoreMapFixtures.Open(9, 5, new int2(8, 2), new int2(0, 2));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            var goals = Listen(match, CoreEventKind.GoalReached);
            match.Begin();

            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(4, 1))).Accepted);
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));

            for (int t = 0; t < 900 && goals.Count == 0; t++) match.Tick();

            Assert.AreEqual(1, goals.Count, "막힌 칸을 돌아 골까지 간다(교착 0)");
        }

        [Test]
        public void 배치_유닛이_장애물이_되고_퇴근하면_풀린다()
        {
            var map = CoreMapFixtures.Corridor(7, 3, 1);
            var match = new BattleMatch(CoreMapFixtures.Definition(map, defenderW: 1, defenderH: 1));
            match.Begin();

            // 복도 칸은 걷는 칸이라 배치도 받는다(배치 마스크는 통행과 다른 축이다).
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted);
            match.Tick();
            Assert.IsFalse(match.Map.Flow.GoalSlot(LayerBits.Path).Reaches(new int2(0, 1)),
                "폭 1 복도를 막으면 길이 끊긴다");

            Assert.IsTrue(match.Apply(Command.Retire(new SimEntityId(1))).Accepted);
            match.Tick();
            Assert.IsTrue(match.Map.Flow.GoalSlot(LayerBits.Path).Reaches(new int2(0, 1)),
                "퇴근하면 길이 돌아온다");
        }

        [Test]
        public void 디버그_장애물이_흐름장을_다시_굽는다()
        {
            var map = CoreMapFixtures.Corridor(7, 3, 1);
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            match.Begin();

            match.Apply(Command.DebugSetObstacle(new int2(3, 1), true));
            match.Tick();
            Assert.IsFalse(match.Map.Flow.GoalSlot(LayerBits.Path).Reaches(new int2(0, 1)));

            match.Apply(Command.DebugSetObstacle(new int2(3, 1), false));
            match.Tick();
            Assert.IsTrue(match.Map.Flow.GoalSlot(LayerBits.Path).Reaches(new int2(0, 1)));
        }

        [Test]
        public void 정지는_결과_관찰이다()
        {
            // `HoldingGround` 는 케이스 열거가 아니다 — 진입 시 1, **실제로 움직인 지점에서만** 0.
            var map = CoreMapFixtures.Open(6, 3, new int2(5, 1), new int2(0, 1));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            match.Begin();
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));

            var enemy = match.World.Units[0];
            Assert.IsTrue(enemy.Move.HoldingGround, "스폰 직후엔 아직 안 움직였다");

            match.Tick();
            Assert.IsFalse(enemy.Move.HoldingGround, "걷고 나면 0");

            enemy.Move.Locked = true;
            match.Tick();
            Assert.IsTrue(enemy.Move.HoldingGround, "CC 잠금도 정지에 접힌다");
        }

        [Test]
        public void 순간이동은_이동이_소유한다()
        {
            var map = CoreMapFixtures.Open(8, 5, new int2(7, 2), new int2(0, 2));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            var blinks = Listen(match, CoreEventKind.Blinked);
            match.Begin();
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));

            var enemy = match.World.Units[0];
            enemy.Move.HasBlink = true;
            enemy.Move.BlinkTo = new float3(5f, 0f, 2f);
            match.Tick();

            Assert.AreEqual(1, blinks.Count, "위치를 쓴 쪽이 사건을 낸다");
            Assert.Greater(enemy.Position.x, 4.5f);
        }

        [Test]
        public void 당김은_이동을_대체하지_않고_더해진다()
        {
            var map = CoreMapFixtures.Open(9, 5, new int2(8, 2), new int2(0, 2));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            match.Begin();
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            var enemy = match.World.Units[0];

            match.Tick();
            float withoutPull = enemy.Position.z;

            match.World.Fields.Add(new FieldCarrier
            {
                Kind = FieldKind.Pull,
                Center = new float3(1f, 0f, 4f),
                Range = 5f,
                Speed = 4f,
            });
            match.Tick();

            Assert.Greater(enemy.Position.z, withoutPull, "당김이 가산 변위로 얹힌다");
            Assert.Greater(enemy.Position.x, 0f, "자기주도 전진은 사라지지 않는다");
        }

        [Test]
        public void 포탈은_출구로_옮긴다()
        {
            var map = CoreMapFixtures.Open(9, 5, new int2(8, 2), new int2(0, 2));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            match.Begin();
            match.Apply(Command.DebugSpawnEnemyInLane(0, 0));

            match.World.Fields.Add(new FieldCarrier
            {
                Kind = FieldKind.Portal,
                Center = new float3(0f, 0f, 2f),
                Exit = new float3(6f, 0f, 2f),
                Range = 1f,
            });
            match.Tick();

            Assert.Greater(match.World.Units[0].Position.x, 5.5f);
        }

        [Test]
        public void 분리_누적은_id_오름차순이다()
        {
            // M27 — 옛 코어는 누적 순서가 청크 배치(스폰·사망 이력)에서 와서 스냅샷 부분
            // 재시뮬이 위험했다. 목록이 id 오름차순이라 순서가 **값**이 됐다.
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2), new int2(0, 2));
            var a = RunCrowd(map, 6, 120);
            var b = RunCrowd(map, 6, 120);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].x, b[i].x, 0f, $"#{i} x");
                Assert.AreEqual(a[i].z, b[i].z, 0f, $"#{i} z");
            }
        }

        private static List<float3> RunCrowd(MapSnapshot map, int count, int ticks)
        {
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            match.Begin();
            for (int i = 0; i < count; i++) match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            for (int t = 0; t < ticks; t++) match.Tick();

            var outp = new List<float3>();
            for (int i = 0; i < match.World.Units.Count; i++) outp.Add(match.World.Units[i].Position);
            return outp;
        }

        [Test]
        public void 골_도달_순서가_레인_안에서_스폰_순서와_같다()
        {
            // 골든 `march_to_goal` 의 완료 기준을 단언으로도 고정한다. 같은 레인에서 나온
            // 적이 서로를 추월하면 분리 누적이 순회 순서에 의존한다는 뜻이다(M27 이 닫은 축).
            //
            // ⚠ **레인 «사이» 순서는 규칙이 아니다** — 입구마다 골까지의 거리가 달라서
            // 섞이는 것이 정상이다. 규칙은 「같은 문에서 나온 순서는 뒤집히지 않는다」다.
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2), new int2(0, 1), new int2(0, 3));
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            var goals = Listen(match, CoreEventKind.GoalReached);
            var laneOf = new Dictionary<int, int>();
            var spawnOrder = new List<int>[] { new List<int>(), new List<int>() };
            match.Begin();

            for (int i = 0; i < 3; i++)
            {
                for (int lane = 0; lane < 2; lane++)
                {
                    match.Apply(Command.DebugSpawnEnemyInLane(0, lane));
                    int id = match.World.Units[match.World.Units.Count - 1].Id.Value;
                    laneOf[id] = lane;
                    spawnOrder[lane].Add(id);
                }
                for (int t = 0; t < 20; t++) match.Tick();
            }
            for (int t = 0; t < 1800 && goals.Count < 6; t++) match.Tick();

            Assert.AreEqual(6, goals.Count, "여섯 기 전원이 닿는다(교착 0)");

            var arrival = new List<int>[] { new List<int>(), new List<int>() };
            for (int i = 0; i < goals.Count; i++) arrival[laneOf[goals[i].A.Value]].Add(goals[i].A.Value);
            for (int lane = 0; lane < 2; lane++)
                CollectionAssert.AreEqual(spawnOrder[lane], arrival[lane], $"레인 {lane} 에서 추월이 났다");
        }

        [Test]
        public void 군집_통과_검산_1칸_복도_20기_100초()
        {
            // memory: 단독 통과 ≠ 군집 통과. 여유 < 밀어냄 폭이면 교착이 난다.
            var map = CoreMapFixtures.Corridor(14, 3, 1);
            var match = new BattleMatch(CoreMapFixtures.Definition(map));
            var goals = Listen(match, CoreEventKind.GoalReached);
            match.Begin();

            for (int i = 0; i < 20; i++) match.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            for (int t = 0; t < 6000; t++) match.Tick();   // 100초

            Assert.AreEqual(20, goals.Count, "20기 전원이 통과해야 한다(교착 0)");
        }
    }
}
