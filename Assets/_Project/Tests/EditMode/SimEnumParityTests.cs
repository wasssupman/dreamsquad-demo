using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/5c — **저작 enum ↔ sim enum 평행성.**
    ///
    /// ## 왜 필요한가
    ///
    /// 무장 지점은 저작값을 sim 으로 옮겨 담아야 하는데, 두 계층의 enum 은 **이름만 같고 서로
    /// 다른 타입**이다(`SimConfig` 주석: *"Battle enum 은 여기 못 온다 — 저작 계층이 옮겨 담고
    /// 그 대응표는 18-K 의 주입 지점이 소유한다"*).
    ///
    /// 옮겨 담는 가장 싼 방법은 `(byte)` 캐스트다. 실제로 지금 모든 쌍이 **멤버 순서까지 같다.**
    /// 그러나 캐스트는 **누가 한쪽에 멤버를 끼워 넣는 순간 조용히 어긋난다** — 컴파일도 통과하고
    /// 테스트도 통과하고, 골든만 갈린다. 그때 원인이 "enum 값 하나가 밀렸다" 라는 것을 찾는 데
    /// 드는 비용이 이 파일의 존재 이유다.
    ///
    /// ⚠ enum 은 트레이스에 **정수로** 나간다(`SimLegacyTrace.Enum`). 즉 이 평행성은 편의가
    /// 아니라 **상태 해시의 전제**다.
    ///
    /// ⚠ 이 대조는 구 sim 이 살아 있는 units 18~20 동안만 가능하다 — 그 창이 닫히면 두 벌은
    /// 각자 표류하고 아무도 모른다.
    /// </summary>
    public class SimEnumParityTests
    {
        /// 이름 → 정수값 표. **이름과 값이 모두** 같아야 캐스트가 안전하다.
        private static Dictionary<string, long> Map(Type t)
        {
            Assert.IsTrue(t.IsEnum, $"{t.FullName} 은 enum 이 아니다");
            return Enum.GetNames(t).ToDictionary(
                n => n,
                n => Convert.ToInt64(Enum.Parse(t, n), System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void SameEnum(Type legacy, Type sim)
        {
            Assert.AreEqual(Enum.GetUnderlyingType(legacy), Enum.GetUnderlyingType(sim),
                $"{legacy.FullName}: 기반 타입이 다르다 — 캐스트가 값을 자를 수 있다");

            Dictionary<string, long> a = Map(legacy), b = Map(sim);

            CollectionAssert.AreEqual(a.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
                                      b.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
                $"{legacy.FullName}: 멤버 집합이 갈렸다");

            foreach (var kv in a)
                Assert.AreEqual(kv.Value, b[kv.Key],
                    $"{legacy.FullName}.{kv.Key}: 값이 갈렸다 — `(byte)` 캐스트가 다른 멤버가 된다");
        }

        [Test]
        public void 모디파이어_enum_이_평행하다()
        {
            SameEnum(typeof(Wassup.Battle.Effects.StackKind), typeof(Wassup.Sim.Effects.StackKind));
            SameEnum(typeof(Wassup.Battle.Effects.StatKind), typeof(Wassup.Sim.Effects.StatKind));
            SameEnum(typeof(Wassup.Battle.Effects.CombineOp), typeof(Wassup.Sim.Effects.CombineOp));
            SameEnum(typeof(Wassup.Battle.Effects.ModifierOrigin), typeof(Wassup.Sim.Effects.ModifierOrigin));
        }

        [Test]
        public void 스택_임계_저작_enum_이_평행하다()
        {
            // ⚠ 이 둘은 `Wassup.Data`(저작 계층)다 — `Wassup.Battle.*` 가 아니다.
            SameEnum(typeof(Wassup.Data.ThresholdMode), typeof(Wassup.Sim.Effects.ThresholdMode));
            SameEnum(typeof(Wassup.Data.DerivedEffectKind), typeof(Wassup.Sim.Effects.DerivedEffectKind));
        }

        [Test]
        public void 상태이상_enum_이_평행하다()
        {
            SameEnum(typeof(Wassup.Battle.Effects.CcKind), typeof(Wassup.Sim.Effects.CcKind));
            SameEnum(typeof(Wassup.Battle.Effects.DotElement), typeof(Wassup.Sim.Effects.DotElement));
            SameEnum(typeof(Wassup.Battle.Effects.DotOrigin), typeof(Wassup.Sim.Effects.DotOrigin));
        }

        [Test]
        public void 평행성_검사가_실제로_불일치를_잡는다()
        {
            // ⚠ 이 게이트가 없으면 위 단정들이 "둘 다 비어 있어도" 통과할 수 있다.
            Assert.Throws<AssertionException>(
                () => SameEnum(typeof(Wassup.Battle.Effects.StackKind), typeof(Wassup.Sim.Effects.StatKind)),
                "서로 다른 enum 을 넣으면 반드시 실패해야 한다");
        }
    }
}
