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

        [Test]
        public void ChainLongerThanMaxDepth_IsRejected()
        {
            var units = new AttackUnitData[SplitChain.MaxDepth + 3];
            for (int i = 0; i < units.Length; i++) units[i] = Unit($"u{i}");
            for (int i = 0; i < units.Length - 1; i++) SetSplit(units[i], units[i + 1]);

            Assert.IsFalse(SplitChain.Validate(units[0], out string err));
            StringAssert.Contains("단계", err);

            foreach (var u in units) Object.DestroyImmediate(u);
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
