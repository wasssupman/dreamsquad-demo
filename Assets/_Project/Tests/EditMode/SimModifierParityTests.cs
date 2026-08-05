using System;
using System.Linq;
using NUnit.Framework;
using Old = Wassup.Battle.Effects;
using New = Wassup.Sim.Effects;

// battle-sim-extraction unit 18-C/1 — 모디파이어 기반의 **차등 오라클**.
//
// 계획서 증인 전략 5: "어서션만 salvage" 가 아니라 **복제(어서션 동일)** 다. 여기서는 한 걸음 더
// 가서 손으로 고른 케이스를 옮기는 대신 **두 구현을 같은 코퍼스로 대조**한다 — 구 sim 이 살아
// 있는 지금만 가능하고, 케이스 선택 편향이 없다.
//
// enum 정수값 대조가 특히 중요하다: 상태 해시는 enum 을 `Convert.ToInt64` 로 **정수화해 찍는다**
// (`BattleBridge.LegacyTrace.cs:336`). 이름만 맞고 값이 밀리면 같은 상태가 다른 문자열로 나가
// A/B parity 가 조용히 깨진다. 기존 `ModifierMathTests`/`ModifierAuthoringTests` 는 이 축을
// 아예 보지 않는다.
namespace Wassup.Tests.EditMode
{
    public class SimModifierParityTests
    {
        static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        static void SameBits(float expected, float actual, string what)
            => Assert.AreEqual(Bits(expected), Bits(actual),
                $"{what}: old={expected} (0x{Bits(expected):X8}) new={actual} (0x{Bits(actual):X8})");

        static readonly float[] Corpus =
        {
            0f, -0f, 1f, -1f, 0.2f, 0.6f, 1.2f, 1.3f, 5f, 6f, -2f, 0.0778f,
            float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            float.Epsilon, 1e-30f, 1e30f, 0.999999f, 1.000001f,
        };

        // ── 결합 산식 ────────────────────────────────────────────────────────

        [Test]
        public void CombineMul_이_비트까지_같다()
        {
            const float floor = 0.2f, ceil = 5f;
            foreach (bool hasOver in new[] { false, true })
            foreach (float over in Corpus)
            foreach (float add in Corpus)
            foreach (float mul in Corpus)
            {
                SameBits(Old.ModifierMath.CombineMul(hasOver, over, add, mul, floor, ceil),
                         New.SimModifierMath.CombineMul(hasOver, over, add, mul, floor, ceil),
                         $"CombineMul(over={hasOver}/{over}, add={add}, mul={mul})");
            }
        }

        [Test]
        public void CombineMul_이_다른_바닥_천장에서도_같다()
        {
            // `MaxHealthMul` 은 전용 floor(0.05) 를 쓴다 — 라스트런 ×0.1 이 통과해야 해서.
            foreach (var (floor, ceil) in new[] { (0.05f, 5f), (0f, 1f), (1f, 1f), (-1f, 1f) })
            foreach (float add in Corpus)
            foreach (float mul in Corpus)
            {
                SameBits(Old.ModifierMath.CombineMul(false, 0f, add, mul, floor, ceil),
                         New.SimModifierMath.CombineMul(false, 0f, add, mul, floor, ceil),
                         $"CombineMul(add={add}, mul={mul}, [{floor},{ceil}])");
            }
        }

        // ── 방향 분류 ────────────────────────────────────────────────────────

        [Test]
        public void FromMultiplier_가_op_와_크기까지_같다()
        {
            foreach (float m in Corpus)
            {
                Old.ModifierAuthoring.FromMultiplier(m, out var oOp, out float oMag);
                New.SimModifierAuthoring.FromMultiplier(m, out var nOp, out float nMag);
                Assert.AreEqual((int)oOp, (int)nOp, $"FromMultiplier({m}) op");
                SameBits(oMag, nMag, $"FromMultiplier({m}) magnitude");
            }
        }

        [Test]
        public void 경계_1_은_가산으로_분류된다()
        {
            // `>= 1f` 다. `> 1f` 로 옮기면 배율 1.0(항등 refresh — revoke 중립화가 쓴다)이
            // Multiplicative 로 가서 슬롯이 갈린다.
            New.SimModifierAuthoring.FromMultiplier(1f, out var op, out float mag);
            Assert.AreEqual(New.CombineOp.Additive, op);
            Assert.AreEqual(0f, mag);
        }

        // ── enum 정수값 — 상태 해시가 정수로 찍는다 ──────────────────────────

        static void SameEnum<TOld, TNew>() where TOld : Enum where TNew : Enum
        {
            var oldNames = Enum.GetNames(typeof(TOld));
            var newNames = Enum.GetNames(typeof(TNew));
            CollectionAssert.AreEqual(oldNames, newNames,
                $"{typeof(TNew).Name}: 멤버 이름/순서가 다르다");
            foreach (string n in oldNames)
            {
                long o = Convert.ToInt64(Enum.Parse(typeof(TOld), n));
                long v = Convert.ToInt64(Enum.Parse(typeof(TNew), n));
                Assert.AreEqual(o, v,
                    $"{typeof(TNew).Name}.{n}: 정수값이 다르다 — 상태 해시는 enum 을 정수로 찍는다");
            }
        }

        [Test] public void StatKind_정수값이_같다() => SameEnum<Old.StatKind, New.StatKind>();
        [Test] public void StackKind_정수값이_같다() => SameEnum<Old.StackKind, New.StackKind>();
        [Test] public void CombineOp_정수값이_같다() => SameEnum<Old.CombineOp, New.CombineOp>();
        [Test] public void ModifierOrigin_정수값이_같다() => SameEnum<Old.ModifierOrigin, New.ModifierOrigin>();

        [Test]
        public void CombineOp_의_기본값은_Multiplicative_다()
        {
            // `op` 를 안 채운 생산자가 곱셈으로 들어간다 — EffectTile 이 실제로 그 경로다.
            // 재현 대상이지 고칠 것이 아니다.
            Assert.AreEqual(0, (int)default(New.CombineOp));
            Assert.AreEqual(New.CombineOp.Multiplicative, default(New.CombineOp));
        }

        // ── 슬롯/집계 struct 모양 ────────────────────────────────────────────

        [Test]
        public void 슬롯_필드_이름이_구_타입과_같다()
        {
            // 상태 해시가 ordinal 정렬된 public 필드를 찍는다. 이름이 바뀌면 A/B 가 불가능해진다
            // (`LegacyTraceKeyContractTests` 가 구 쪽을 박제하고, 여기가 신 쪽을 맞춘다).
            string[] Names(Type t) => t.GetFields(System.Reflection.BindingFlags.Instance
                                                | System.Reflection.BindingFlags.Public)
                                       .Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

            CollectionAssert.AreEqual(Names(typeof(Old.ModifierHeader)), Names(typeof(New.ModifierHeader)));
            CollectionAssert.AreEqual(Names(typeof(Old.StatModifierSlot)), Names(typeof(New.StatModifierSlot)));
            CollectionAssert.AreEqual(Names(typeof(Old.StackModifierSlot)), Names(typeof(New.StackModifierSlot)));
            CollectionAssert.AreEqual(Names(typeof(Old.ModifierStats)), Names(typeof(New.ModifierStats)));
        }

        [Test]
        public void 집계_기본값은_배율_1_과_regen_0_이다()
        {
            var s = New.ModifierStats.Identity;
            Assert.AreEqual(1f, s.damageMul);
            Assert.AreEqual(1f, s.attackSpeedMul);
            Assert.AreEqual(1f, s.dmgTakenMul);
            Assert.AreEqual(1f, s.moveSpeedMul);
            Assert.AreEqual(1f, s.damageVsCcMul);
            Assert.AreEqual(1f, s.maxHealthMul);
            Assert.AreEqual(0f, s.regenPerSec, "regenPerSec 는 배율이 아니라 자원값이다");
        }
    }
}
