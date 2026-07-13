using NUnit.Framework;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-kill-and-threshold unit 3 — 킬 귀속(contract 4) 결정성 회귀 고정.
    // Consider fold 가 "source 非Null 최대 amount, 동점은 먼저 접힌 것" 을 지키는지.
    // 실 Entity 가 필요해 throwaway World 로 서로 다른 엔티티를 만든다(값 비교만이라
    // 시스템 실행은 불필요).
    public class KillAttributionTests
    {
        private World _world;
        private Entity _a, _b, _c;

        [SetUp]
        public void SetUp()
        {
            _world = new World("KillAttributionTests");
            var em = _world.EntityManager;
            _a = em.CreateEntity();
            _b = em.CreateEntity();
            _c = em.CreateEntity();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void SingleAttributed_Wins()
        {
            Entity best = Entity.Null; float amt = 0f;
            KillAttribution.Consider(10f, _a, ref best, ref amt);
            Assert.AreEqual(_a, best);
        }

        [Test]
        public void HighestAmount_Wins_RegardlessOfOrder()
        {
            Entity best = Entity.Null; float amt = 0f;
            KillAttribution.Consider(5f, _a, ref best, ref amt);
            KillAttribution.Consider(15f, _b, ref best, ref amt);
            KillAttribution.Consider(9f, _c, ref best, ref amt);
            Assert.AreEqual(_b, best);
        }

        [Test]
        public void Tie_FirstConsideredWins()
        {
            Entity best = Entity.Null; float amt = 0f;
            KillAttribution.Consider(12f, _a, ref best, ref amt);
            KillAttribution.Consider(12f, _b, ref best, ref amt); // 동점 — 덮어쓰지 않음 (strict >)
            Assert.AreEqual(_a, best);
        }

        [Test]
        public void NullSource_Ignored_EvenIfLargest()
        {
            Entity best = Entity.Null; float amt = 0f;
            KillAttribution.Consider(100f, Entity.Null, ref best, ref amt); // DoT/환경 — 후보 아님
            KillAttribution.Consider(7f, _a, ref best, ref amt);
            Assert.AreEqual(_a, best, "비귀속(Null) 이 최대여도 killer 아님");
        }

        [Test]
        public void AllNullSource_NoKiller()
        {
            Entity best = Entity.Null; float amt = 0f;
            KillAttribution.Consider(50f, Entity.Null, ref best, ref amt);
            KillAttribution.Consider(30f, Entity.Null, ref best, ref amt);
            Assert.AreEqual(Entity.Null, best);
        }
    }
}
