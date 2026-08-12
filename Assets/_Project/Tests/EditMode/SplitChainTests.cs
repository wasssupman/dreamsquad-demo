using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // elite-enemy-tier unit 6 rev — 분열 사슬 검증의 순수 단위 테스트.
    //
    // 이 술어가 무한 분열의 유일한 방어선이다. 초판 가드는 «자식이 메커닉을 갖고 있으면 경고»
    // 였는데 2단계 분열이 의도가 되면서 거짓 신호가 됐고, 판정을 «사슬이 순환하나» 로 옮겼다.
    // 그 교체가 방어를 **약화시키지 않았다**는 것을 여기서 못 박는다.
    public class SplitChainTests
    {
        private static AttackUnitData Unit(string name)
        {
            var u = ScriptableObject.CreateInstance<AttackUnitData>();
            u.displayName = name;
            return u;
        }

        private static void SetSplit(AttackUnitData host, AttackUnitData child, float count = 2f)
        {
            host.nightmareMechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDeath },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.SplitOnDeath,
                        magnitude = count,
                        splitUnit = child,
                    },
                },
            };
        }

        [Test]
        public void NoMechanics_IsValid_AndChainEndsImmediately()
        {
            var u = Unit("plain");
            Assert.IsTrue(SplitChain.Validate(u, out string err), err);
            Assert.IsNull(SplitChain.NextInChain(u));
            Object.DestroyImmediate(u);
        }

        [Test]
        public void TwoStageChain_IsValid()
        {
            var big = Unit("big"); var mid = Unit("mid"); var small = Unit("small");
            SetSplit(big, mid);
            SetSplit(mid, small);

            Assert.IsTrue(SplitChain.Validate(big, out string err), err);
            Assert.AreSame(mid, SplitChain.NextInChain(big));
            Assert.AreSame(small, SplitChain.NextInChain(mid));
            Assert.IsNull(SplitChain.NextInChain(small));

            Object.DestroyImmediate(big); Object.DestroyImmediate(mid); Object.DestroyImmediate(small);
        }

        // 진짜 무한 분열 — 자기 자신을 낳는다.
        [Test]
        public void SelfCycle_IsRejected()
        {
            var u = Unit("ouroboros");
            SetSplit(u, u);
            Assert.IsFalse(SplitChain.Validate(u, out string err));
            StringAssert.Contains("순환", err);
            Object.DestroyImmediate(u);
        }

        // 두 단계를 거쳐 돌아오는 순환 — 초판 가드로는 «자식이 메커닉을 가졌다» 경고만 났고
        // 이게 실제 무한인지 아닌지 구분하지 못했다.
        [Test]
        public void IndirectCycle_IsRejected()
        {
            var a = Unit("a"); var b = Unit("b");
            SetSplit(a, b);
            SetSplit(b, a);
            Assert.IsFalse(SplitChain.Validate(a, out string err));
            StringAssert.Contains("순환", err);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        // 깊이 상한을 **고립** 검증한다 — 단계마다 자식 1기라 자손 총수는 사슬 길이와 같고
        // MaxTotalOffspring 예산에 안 걸린다. (count=2 로 두면 총수 상한이 먼저 잡아서
        // 이 테스트가 깊이 상한을 검증하지 못한다 — 실제로 그렇게 빨개졌다.)
        [Test]
        public void ChainLongerThanMaxDepth_IsRejected()
        {
            var units = new AttackUnitData[SplitChain.MaxDepth + 3];
            for (int i = 0; i < units.Length; i++) units[i] = Unit($"u{i}");
            for (int i = 0; i < units.Length - 1; i++) SetSplit(units[i], units[i + 1], count: 1f);

            Assert.IsFalse(SplitChain.Validate(units[0], out string err));
            StringAssert.Contains("단계", err);

            foreach (var u in units) Object.DestroyImmediate(u);
        }

        // 팬아웃 곱 — 깊이·폭 상한이 서로 독립이라 둘 다 통과하면서 폭발하는 저작이 있었다
        // (폭 8 × 깊이 8 = 8⁸). 그 구멍을 막은 것이 자손 총수 예산이다(리뷰 B-M1).
        [Test]
        public void WideAndDeepChain_IsRejected_EvenThoughEachCapPasses()
        {
            const int depth = 5;   // MaxDepth(8) 이하 — 깊이 상한은 통과한다
            var units = new AttackUnitData[depth + 1];
            for (int i = 0; i < units.Length; i++) units[i] = Unit($"w{i}");
            // 단계마다 4기 → 4 + 16 + 64 + … 로 예산(32)을 금방 넘는다.
            for (int i = 0; i < units.Length - 1; i++) SetSplit(units[i], units[i + 1], count: 4f);

            Assert.IsFalse(SplitChain.Validate(units[0], out string err),
                "폭×깊이 조합이 통과했다 — 두 상한이 서로 독립이라 생기는 구멍");
            StringAssert.Contains("자손 총수", err);

            foreach (var u in units) Object.DestroyImmediate(u);
        }

        // 배송 콘텐츠(2단계 × 2기 = 자손 6)는 예산 안이다 — 상한이 정상 저작을 막지 않는 것도
        // 계약이다.
        [Test]
        public void ShippedTwoStageFanout_IsWithinBudget()
        {
            var big = Unit("big"); var mid = Unit("mid"); var small = Unit("small");
            SetSplit(big, mid, count: 2f);
            SetSplit(mid, small, count: 2f);
            Assert.IsTrue(SplitChain.Validate(big, out string err), err);
            Assert.AreEqual(2, SplitChain.CountAt(big));
            Assert.AreEqual(2, SplitChain.CountAt(mid));
            Assert.AreEqual(0, SplitChain.CountAt(small), "마지막 단계는 자식이 없다");
            Object.DestroyImmediate(big); Object.DestroyImmediate(mid); Object.DestroyImmediate(small);
        }

        // splitUnit 이 비면 사슬은 그냥 끝난다 — 여기서 «오류» 로 보지 않는다.
        // 그 저작 실수는 bake 가 별도로 loud 거절한다(책임 분리).
        [Test]
        public void NullSplitUnit_EndsChain_WithoutError()
        {
            var u = Unit("broken");
            SetSplit(u, null);
            Assert.IsTrue(SplitChain.Validate(u, out string err), err);
            Assert.IsNull(SplitChain.NextInChain(u));
            Object.DestroyImmediate(u);
        }
    }
}
