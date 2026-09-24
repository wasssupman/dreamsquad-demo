using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — 사직서 임계 → **운석 barrage**(옛 `DrainMeteorBarrageRequests`). 임계는 6b2 의 사건,
    // 이 파일은 그 소비자다. 이동 칸 · 겹침 없음 · 순차 예고 · 적 피해 · 자리형(원점 몸 0).
    [TestFixture]
    public class ResignationBarrageTests
    {
        private static BattleMatch Board(int threshold, int meteors, bool withProjectile = true)
        {
            var def = CoreCombatFixtures.Definition();
            int proj = withProjectile ? CoreTriggerFixtures.AddBlastProjectile(def) : -1;
            CoreGimmickFixtures.With(def, CoreGimmickFixtures.ClockOut(threshold, meteors, proj));
            return CoreMatchFixtures.BeginBattle(def);
        }

        [Test]
        public void 임계에_닿으면_이동_칸에_겹치지_않게_운석이_순차로_떨어진다()
        {
            var m = Board(threshold: 3, meteors: 4);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            for (int i = 0; i < 3; i++) m.Apply(Command.DebugDropResignation(new int2(i, 0)));
            m.Tick();   // 임계 → 사건(틱 끝 배달) → 요청
            m.Tick();   // 요청 → 탄

            Assert.AreEqual(4, spawned.Count);
            var cells = new HashSet<int2>();
            var flights = new List<float>();
            foreach (var e in spawned)
            {
                var cell = m.Map.CellOf(e.SiteTarget.Pos);
                Assert.AreEqual(Wassup.BattleCore.Map.MapTile.Walk, m.Map.Snapshot.Tiles[cell.y * m.Map.Snapshot.Width + cell.x]);
                Assert.IsTrue(cells.Add(cell), "같은 칸에 두 번 떨어지지 않는다");
                Assert.AreEqual(0f, e.SiteFired.OriginBody, 1e-6f, "자리에 떨어지는 것 — 몸 0(제약 13)");
                Assert.AreEqual(Wassup.Battle.Units.Faction.DefenderUnit, e.Faction, "플레이어 쪽 탄 — 적을 때린다");
            }
            foreach (var p in m.World.Projectiles) flights.Add(p.FlightTime);
            flights.Sort();
            for (int i = 1; i < flights.Count; i++)
                Assert.AreEqual(0.2f, flights[i] - flights[i - 1], 1e-4f, "예고 + k × 시차");
        }

        [Test]
        public void 한_틱에_임계를_두_번_넘으면_두_번_떨어진다()
        {
            var m = Board(threshold: 2, meteors: 3);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            for (int i = 0; i < 4; i++) m.Apply(Command.DebugDropResignation(new int2(i, 0)));
            m.Tick();
            m.Tick();
            Assert.AreEqual(6, spawned.Count);
        }

        [Test]
        public void 운석_탄이_없으면_조용히_떨어뜨리지_않고_말한다()
        {
            var m = Board(threshold: 1, meteors: 3, withProjectile: false);
            var said = new List<string>();
            m.Report = said.Add;
            m.Apply(Command.DebugDropResignation(new int2(1, 0)));
            m.Tick(); m.Tick();
            Assert.IsEmpty(m.World.Projectiles);
            Assert.IsTrue(said.Exists(s => s.Contains("운석 탄")));
        }

        [Test]
        public void 같은_시드는_같은_칸이다()
        {
            List<int2> Run()
            {
                var m = Board(threshold: 1, meteors: 5);
                var got = new List<int2>();
                m.Bus.Subscribe(CoreEventKind.ProjectileSpawned, 0, e => got.Add(m.Map.CellOf(e.SiteTarget.Pos)));
                m.Apply(Command.DebugDropResignation(new int2(1, 0)));
                m.Tick(); m.Tick();
                return got;
            }
            CollectionAssert.AreEqual(Run(), Run());
        }
    }
}
