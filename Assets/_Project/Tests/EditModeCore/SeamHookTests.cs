using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b2 — seam 번호와 `[Periodic]` 호출부.
    [TestFixture]
    public class SeamHookTests
    {
        [Test]
        public void Periodic_은_append_라_앞_번호가_안_밀렸다()
        {
            Assert.AreEqual(0, (int)Seam.Attack);
            Assert.AreEqual(1, (int)Seam.Death);
            Assert.AreEqual(2, (int)Seam.Lifecycle);
            Assert.AreEqual(3, (int)Seam.Threshold);
            Assert.AreEqual(4, (int)Seam.Periodic);
            Assert.AreEqual(5, (int)Seam._Count, "새 seam 은 `_Count` 앞에 붙는다");
        }

        [Test]
        public void 핸들러가_없어도_판은_돌고_호출부는_매_틱_실행된다()
        {
            var m = new BattleMatch(CoreCombatFixtures.Definition());
            m.Begin();
            Assert.AreEqual(0, m.Seams.CountAt(Seam.Periodic), "이 unit 의 산출 = 핸들러 0 인 호출부");
            CoreCombatFixtures.Tick(m, 3);   // 빈 seam 으로도 돈다

            int calls = 0, lastTick = -1;
            m.Seams.Register(Seam.Periodic, ctx => { calls++; lastTick = ctx.Tick; });
            CoreCombatFixtures.Tick(m, 10);
            Assert.AreEqual(10, calls, "unit 7 이 붙으면 매 틱 불린다");
            Assert.AreEqual(m.Clock.Tick - 1, lastTick);
        }

        [Test]
        public void Periodic_은_장_준비_끝_이동_앞에서_돈다()
        {
            // 번호(4)는 실행 순서가 아니다 — 이 seam 은 이동·전투보다 **앞**이다(7a 의 SeamTickOrder).
            var map = CoreMapFixtures.Open(9, 5, new int2(8, 2), new int2(0, 2));
            var m = new BattleMatch(CoreMapFixtures.Definition(map));
            m.Begin();
            m.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            var enemy = m.World.Units[m.World.Units.Count - 1];
            m.Tick();
            float3 seen = default;
            m.Seams.Register(Seam.Periodic, _ => seen = enemy.Position);
            float3 before = enemy.Position;
            m.Tick();
            Assert.AreNotEqual(before, enemy.Position, "이 틱에 적이 움직였다(단언이 공허하지 않다)");
            Assert.AreEqual(before, seen, "핸들러가 본 자리 = 이번 틱 이동 전");
        }
    }
}
