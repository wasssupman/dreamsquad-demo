using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // bonus-wave-pull unit 5·9 — 등장 규칙의 값 수준 고정.
    //
    // 여기가 지키는 것은 산수가 아니라 **세 가지 사고**다:
    //  ⓐ 자기증식 — 트리거가 보너스 킬을 세면 보너스 웨이브가 자기 자신을 재발화한다.
    //  ⓑ 크레딧 증발 — 스트레스에 막혀 쌓인 초과분을 소비 때 통째로 버리면 안 된다.
    //  ⓒ 버튼 떨림 — 스트레스는 매 프레임 오르내리는 값이라 매 프레임 재평가하면 깜빡인다.
    public class BonusPullTriggerTests
    {
        private const int T = 30;      // killThreshold
        private const float S = 30f;   // maxStressToOffer

        // ── 크레딧 축 ────────────────────────────────────────────────────────────
        [Test]
        public void 임계에_닿기_전에는_크레딧이_없다()
        {
            Assert.IsFalse(BonusPullTrigger.HasCredit(29, 0, T));
        }

        [Test]
        public void 임계에_닿으면_크레딧이_생긴다()
        {
            Assert.IsTrue(BonusPullTrigger.HasCredit(30, 0, T));
            Assert.IsTrue(BonusPullTrigger.HasCredit(95, 0, T));
        }

        [Test]
        public void 임계가_0_이하면_영영_크레딧이_없다()
        {
            Assert.IsFalse(BonusPullTrigger.HasCredit(9999, 0, 0));
            Assert.IsFalse(BonusPullTrigger.HasCredit(9999, 0, -5));
        }

        // ⓐ 자기증식 차단 — 호출부가 «일반 처치» 만 넘긴다는 계약을 값으로 표현한다.
        [Test]
        public void 특수_처치는_크레딧을_밀지_않는다()
        {
            int normalKills = 30, consumed = 0;
            Assert.IsTrue(BonusPullTrigger.HasCredit(normalKills, consumed, T));

            consumed += T;   // 한 회 소비
            // 보너스 적 10기를 전부 잡았다 — 그래도 normalKills 는 변하지 않는다.
            Assert.IsFalse(BonusPullTrigger.HasCredit(normalKills, consumed, T),
                "특수 처치가 크레딧을 채우면 보너스 웨이브가 자기 자신을 재발화한다");
        }

        // ⓑ 크레딧 보존 — 소비는 «한 회분» 이다. `consumed = normalKills` 로 두면 증발한다.
        [Test]
        public void 밀린_크레딧은_소비해도_남는다()
        {
            int normalKills = 95, consumed = 0;   // 3회분 + 5킬
            Assert.IsTrue(BonusPullTrigger.HasCredit(normalKills, consumed, T));

            consumed += T;
            Assert.IsTrue(BonusPullTrigger.HasCredit(normalKills, consumed, T), "2회분 남아야 한다");
            consumed += T;
            Assert.IsTrue(BonusPullTrigger.HasCredit(normalKills, consumed, T), "1회분 남아야 한다");
            consumed += T;
            Assert.IsFalse(BonusPullTrigger.HasCredit(normalKills, consumed, T), "3회를 다 썼다");
        }

        // ── 스트레스 축 ──────────────────────────────────────────────────────────
        [Test]
        public void 스트레스는_이하일_때_통과한다()
        {
            Assert.IsTrue(BonusPullTrigger.StressAllows(0f, S), "만피 = 스트레스 0");
            Assert.IsTrue(BonusPullTrigger.StressAllows(30f, S), "경계는 포함(30 이하)");
            Assert.IsFalse(BonusPullTrigger.StressAllows(30.1f, S));
            Assert.IsFalse(BonusPullTrigger.StressAllows(99f, S));
        }

        // ── 래치(등장 조건 ≠ 유지 조건) ─────────────────────────────────────────
        [Test]
        public void 크레딧이_없으면_래치가_꺼진다()
        {
            Assert.IsFalse(BonusPullTrigger.NextLatched(true, 29, 0, T, 0f, S),
                "크레딧이 없으면 켜져 있던 래치도 꺼져야 한다(소비 직후가 이 경우다)");
        }

        [Test]
        public void 스트레스가_높으면_크레딧이_있어도_안_뜬다()
        {
            Assert.IsFalse(BonusPullTrigger.NextLatched(false, 30, 0, T, 55f, S));
        }

        // 사용자 시나리오 그대로 — 30킬 시점 스트레스 55 → 안 뜸, 이후 28 로 내려가면 그때 뜸.
        [Test]
        public void 스트레스가_내려오면_그때_뜬다()
        {
            bool latched = false;
            latched = BonusPullTrigger.NextLatched(latched, 30, 0, T, 55f, S);
            Assert.IsFalse(latched, "스트레스 55 — 아직");

            latched = BonusPullTrigger.NextLatched(latched, 34, 0, T, 28f, S);
            Assert.IsTrue(latched, "스트레스가 28 로 내려온 프레임에 등장해야 한다");
        }

        // ⓒ 떨림 차단 — 뜬 뒤에 스트레스가 다시 올라가도 유지된다.
        [Test]
        public void 한번_뜨면_스트레스가_올라가도_유지된다()
        {
            bool latched = BonusPullTrigger.NextLatched(false, 30, 0, T, 10f, S);
            Assert.IsTrue(latched);

            latched = BonusPullTrigger.NextLatched(latched, 30, 0, T, 80f, S);
            Assert.IsTrue(latched,
                "등장 조건은 유지 조건이 아니다 — 매 프레임 재평가하면 문턱 근처에서 버튼이 떨린다");
        }

        // 소비 → 남은 크레딧이 있으면 다시 게이트를 통과해야 한다(자동 재등장 아님).
        [Test]
        public void 소비_후_남은_크레딧은_게이트를_다시_통과해야_한다()
        {
            int normalKills = 70, consumed = 0;
            bool latched = BonusPullTrigger.NextLatched(false, normalKills, consumed, T, 10f, S);
            Assert.IsTrue(latched);

            consumed += T;              // 한 회 소비 — 호출부가 래치도 내린다
            latched = false;
            Assert.IsFalse(BonusPullTrigger.NextLatched(latched, normalKills, consumed, T, 90f, S),
                "남은 크레딧이 있어도 스트레스가 높으면 다시 뜨지 않는다");
            Assert.IsTrue(BonusPullTrigger.NextLatched(latched, normalKills, consumed, T, 5f, S),
                "스트레스가 낮으면 남은 크레딧으로 이어서 뜬다");
        }

        // 마음이 없는 맵은 StressMath 가 0 을 준다 → 게이트가 항상 열린다(fail-open).
        [Test]
        public void 마음이_없으면_스트레스_0_이라_게이트가_열린다()
        {
            float noHeartStress = Wassup.Core.StressMath.FromHealth(0f, 0f);
            Assert.AreEqual(0f, noHeartStress, "마음 미저작은 스트레스 0 이 계약이다");
            Assert.IsTrue(BonusPullTrigger.NextLatched(false, 30, 0, T, noHeartStress, S));
        }
    }
}
