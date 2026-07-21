using NUnit.Framework;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // shield-guardian-defender unit 0 — 출처별 병합(같은 출처 max·교차 출처 합산)과
    // 오래된-슬롯-우선 흡수의 순수 검증. World 패턴은 CcApplySystemTests 선례.
    public class ShieldMathTests
    {
        private World _world;
        private EntityManager _em;
        private Entity _sourceA, _sourceB, _holder;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ShieldMathTestWorld");
            _em = _world.EntityManager;
            _sourceA = _em.CreateEntity();
            _sourceB = _em.CreateEntity();
            _holder = _em.CreateEntity();
            _em.AddBuffer<ShieldSlot>(_holder);
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        private DynamicBuffer<ShieldSlot> Buf() => _em.GetBuffer<ShieldSlot>(_holder);

        [Test]
        public void Merge_SameSource_TakesMax_NoStack()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 100f);
            ShieldMath.Merge(ref buf, _sourceA, 150f);
            Assert.AreEqual(1, buf.Length, "same source must stay one slot");
            Assert.AreEqual(150f, buf[0].value, "grant above remainder raises to B");

            ShieldMath.Merge(ref buf, _sourceA, 100f);
            Assert.AreEqual(150f, buf[0].value, "grant below remainder keeps max");
        }

        [Test]
        public void Merge_DifferentSources_AddUp()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 100f);
            ShieldMath.Merge(ref buf, _sourceB, 100f);
            Assert.AreEqual(2, buf.Length);
            Assert.AreEqual(200f, ShieldMath.Sum(buf), "a 100 + b 100 = 200");
        }

        [Test]
        public void Absorb_Partial_ConsumesOldestFirst()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 100f);
            ShieldMath.Merge(ref buf, _sourceB, 100f);

            float pierced = ShieldMath.Absorb(ref buf, 50f);

            Assert.AreEqual(0f, pierced);
            Assert.AreEqual(2, buf.Length);
            Assert.AreEqual(_sourceA, buf[0].source, "oldest slot consumed first");
            Assert.AreEqual(50f, buf[0].value);
            Assert.AreEqual(100f, buf[1].value, "newer slot untouched");
        }

        [Test]
        public void Absorb_AcrossSlotBoundary_RemovesDepleted()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 100f);
            ShieldMath.Merge(ref buf, _sourceB, 100f);

            float pierced = ShieldMath.Absorb(ref buf, 150f);

            Assert.AreEqual(0f, pierced);
            Assert.AreEqual(1, buf.Length, "depleted oldest slot removed");
            Assert.AreEqual(_sourceB, buf[0].source);
            Assert.AreEqual(50f, buf[0].value);
        }

        [Test]
        public void Absorb_FullAbsorb_ZeroPierce()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 200f);

            float pierced = ShieldMath.Absorb(ref buf, 150f);

            Assert.AreEqual(0f, pierced, "shield eats the whole hit");
            Assert.AreEqual(50f, buf[0].value);
        }

        [Test]
        public void Absorb_NoShield_FullPierce()
        {
            var buf = Buf();
            float pierced = ShieldMath.Absorb(ref buf, 80f);
            Assert.AreEqual(80f, pierced, "empty slots pass damage through unchanged");
        }

        [Test]
        public void Absorb_Overkill_PiercesRemainder()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 100f);

            float pierced = ShieldMath.Absorb(ref buf, 250f);

            Assert.AreEqual(150f, pierced);
            Assert.AreEqual(0, buf.Length, "depleted slot removed");
        }

        [Test]
        public void ValueFromSource_ReturnsSlotValue_OrZeroForUnknown()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 120f);
            Assert.AreEqual(120f, ShieldMath.ValueFromSource(buf, _sourceA), "known source returns its slot value");
            Assert.AreEqual(0f, ShieldMath.ValueFromSource(buf, _sourceB), "unknown source returns 0 (no-op grant guard)");
        }

        [Test]
        public void Merge_NonPositiveAmount_Ignored()
        {
            var buf = Buf();
            ShieldMath.Merge(ref buf, _sourceA, 0f);
            ShieldMath.Merge(ref buf, _sourceA, -10f);
            Assert.AreEqual(0, buf.Length, "0/negative grants add nothing");
        }
    }
}
