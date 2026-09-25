using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using static Wassup.Tests.EditMode.Core.CoreCombatFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 최대 체력 배율.
    [TestFixture]
    public class MaxHealthScaleTests
    {
        [Test]
        public void 바닥은_1_HP_다()
        {
            MaxHealthScale.Apply(100f, 100f, 0f, out float value, out float max);
            Assert.AreEqual(1f, max, 1e-4f, "배율이 0 이어도 죽지 않는다");
            Assert.AreEqual(1f, value, 1e-4f);
        }

        [Test]
        public void 축소하면_현재값을_잘라내고_복원에_무료_회복이_없다()
        {
            MaxHealthScale.Apply(100f, 100f, 0.5f, out float shrunkValue, out float shrunkMax);
            Assert.AreEqual(50f, shrunkMax, 1e-4f);
            Assert.AreEqual(50f, shrunkValue, 1e-4f, "넘치는 만큼 잘린다");

            MaxHealthScale.Apply(shrunkValue, 100f, 1f, out float restoredValue, out float restoredMax);
            Assert.AreEqual(100f, restoredMax, 1e-4f);
            Assert.AreEqual(50f, restoredValue, 1e-4f, "배율이 돌아와도 체력은 안 올라간다");
        }

        [Test]
        public void 기준은_언제나_스폰_시점_원본이다()
        {
            // 현재 최대치에 곱하면 누적 오염이 난다 — 같은 배율을 두 번 적용해도 결과가 같아야 한다.
            MaxHealthScale.Apply(100f, 200f, 0.5f, out _, out float once);
            MaxHealthScale.Apply(100f, 200f, 0.5f, out _, out float twice);
            Assert.AreEqual(once, twice, 1e-4f);
            Assert.AreEqual(100f, once, 1e-4f);
        }

        // ── 판 위에서 ────────────────────────────────────────────────────────

        [Test]
        public void 배율이_1_에서_벗어난_첫_틱에_기준을_잡는다()
        {
            var m = new BattleMatch(Definition(enemyHealth: 200f));
            m.Begin();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            Tick(m, 2);
            Assert.AreEqual(0f, e.BaseMaxHealth, 1e-4f, "배율이 1 이면 잡지 않는다(lazy-attach)");
            Assert.AreEqual(200f, e.MaxHealth, 1e-4f);

            e.Modifiers.Apply(ModifierKey.Of(e.Id, StatKind.MaxHealthMul, CombineOp.Multiplicative),
                              0.5f, 5f);
            Tick(m, 1);
            Assert.AreEqual(200f, e.BaseMaxHealth, 1e-4f);
            Assert.AreEqual(100f, e.MaxHealth, 1e-4f);
            Assert.AreEqual(100f, e.Health, 1e-4f);
        }

        [Test]
        public void 풀_재사용_뒤에는_기준이_0_에서_시작한다()
        {
            // E11 — 앞 점유자의 기준값이 물리면 최대 체력 배율이 통째로 어긋난다
            // (그 유닛의 체력이 남의 체격을 따라간다). 개체는 풀에서 **돌려쓴다.**
            var m = new BattleMatch(Definition(enemyHealth: 200f));
            m.Begin();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var first = First(m, UnitKind.Enemy);
            var firstId = first.Id;   // ⚠ 같은 객체가 돌아오므로 번호는 **미리** 붙든다
            first.Modifiers.Apply(ModifierKey.Of(first.Id, StatKind.MaxHealthMul,
                                                 CombineOp.Multiplicative), 0.5f, 60f);
            first.Cc.Apply(CcSlotKind.Stun, 5f, default, first.Id);
            first.Dot.Apply(DotOrigin.Stack, DotElement.Bleed, 5f, 1f, 60f);
            first.Stacks.Add(first.Id, StackKind.Fire, -1, 3, 5, 30f);
            Tick(m, 2);
            Assert.AreEqual(200f, first.BaseMaxHealth, 1e-4f);

            m.Apply(Command.DebugDestroy(firstId));
            Tick(m, 2);

            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));
            var second = First(m, UnitKind.Enemy);
            Assert.AreNotEqual(firstId, second.Id, "번호는 재사용하지 않는다");
            Assert.AreEqual(0f, second.BaseMaxHealth, 1e-4f);
            Assert.AreEqual(200f, second.MaxHealth, 1e-4f, "앞 점유자의 축소가 새지 않는다");
            Assert.AreEqual(0, second.Modifiers.Count);
            Assert.IsFalse(second.Cc.Any);
            Assert.IsFalse(second.Dot.Any);
            Assert.IsFalse(second.Stacks.Any);
        }

        [Test]
        public void 체력을_담당자가_드는_개체는_건너뛴다()
        {
            // 마음 타워의 최대치는 `HeartMeter` 의 것이다 — 여기서 건드리면 두 벌이 된다.
            var m = new BattleMatch(Definition());
            m.Begin();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);
            e.HealthExternal = true;
            float before = e.MaxHealth;

            e.Modifiers.Apply(ModifierKey.Of(e.Id, StatKind.MaxHealthMul, CombineOp.Multiplicative),
                              0.5f, 5f);
            Tick(m, 2);
            Assert.AreEqual(before, e.MaxHealth, 1e-4f);
            Assert.AreEqual(0f, e.BaseMaxHealth, 1e-4f);
        }
    }
}
