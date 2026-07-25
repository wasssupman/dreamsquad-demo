using NUnit.Framework;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-unit-trigger Unit 2 — pins the AttackN counting contract:
    // fire exactly on every N-th resolve, reset after firing, period 0 inert,
    // and per-slot counters stay independent.
    public class DcTriggerTests
    {
        [Test]
        public void Period5_FiresOnlyOnFifthResolve_AndResets()
        {
            ushort counter = 0;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int i = 0; i < 4; i++)
                    Assert.IsFalse(DcTrigger.Tick(ref counter, 5), $"cycle {cycle}, resolve {i + 1} must not fire");
                Assert.IsTrue(DcTrigger.Tick(ref counter, 5), $"cycle {cycle}, 5th resolve must fire");
                Assert.AreEqual(0, counter, "counter must reset after firing");
            }
        }

        [Test]
        public void Period1_FiresEveryResolve()
        {
            ushort counter = 0;
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(DcTrigger.Tick(ref counter, 1));
                Assert.AreEqual(0, counter);
            }
        }

        [Test]
        public void Period0_NeverFires()
        {
            ushort counter = 0;
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(DcTrigger.Tick(ref counter, 0));
        }

        [Test]
        public void IndependentCounters_DoNotInterfere()
        {
            // Same card attached twice = two slots with their own counters,
            // acquired at different times (offset by one resolve here).
            ushort a = 0, b = 0;
            DcTrigger.Tick(ref a, 5); // slot A acquired one attack earlier

            bool aFired = false, bFired = false;
            for (int i = 0; i < 4; i++)
            {
                aFired = DcTrigger.Tick(ref a, 5);
                bFired = DcTrigger.Tick(ref b, 5);
            }
            Assert.IsTrue(aFired, "A saw its 5th resolve");
            Assert.IsFalse(bFired, "B has only seen 4 resolves");
            Assert.IsTrue(DcTrigger.Tick(ref b, 5), "B fires one resolve later");
        }

        // ── dreamcatcher-heavy-strike unit 1 — WouldFire (non-mutating twin) ──

        [Test]
        public void WouldFire_MatchesTick_ForEveryCounterInPeriod()
        {
            // The heavy pre-scan predicts firing without touching the counter; it must
            // equal what the counter-owning Tick returns for the same pre-increment
            // counter, so pre-scan == the dc-trigger loop's dcFired.
            const ushort period = 5;
            for (ushort c = 0; c < period; c++)
            {
                ushort probe = c;
                bool actual = DcTrigger.Tick(ref probe, period);
                Assert.AreEqual(actual, DcTrigger.WouldFire(c, period), $"counter {c}: WouldFire must equal Tick");
            }
            Assert.IsTrue(DcTrigger.WouldFire(4, period), "counter 4 (the 5th resolve) fires");
            Assert.IsFalse(DcTrigger.WouldFire(3, period), "counter 3 does not");
        }

        [Test]
        public void WouldFire_Period1_AlwaysTrue_Period0_NeverFires()
        {
            Assert.IsTrue(DcTrigger.WouldFire(0, 1));
            for (ushort c = 0; c < 10; c++)
                Assert.IsFalse(DcTrigger.WouldFire(c, 0), "period 0 never fires (guard)");
        }

        // ── nightmare-catcher unit 2 — PeriodicTimer accumulator ────────────

        [Test]
        public void PeriodicTick_FiresAtPeriod_WithRemainderCarry()
        {
            float elapsed = 0f;
            // 0.4 × 4 = 1.6 ≥ 1.5 → fires on the 4th tick, remainder 0.1 carries.
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0.4f, 1.5f));
            Assert.AreEqual(0.1f, elapsed, 1e-4f, "remainder must carry over (drift-free)");
        }

        [Test]
        public void PeriodicTick_FirstFire_ComesOneFullPeriodAfterSpawn()
        {
            // 시작 위상: 스폰 시 elapsed=0 → 즉발 아님, 첫 발동은 period 후.
            float elapsed = 0f;
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0.999f, 1f));
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0.001f, 1f));
        }

        [Test]
        public void PeriodicTick_NonPositivePeriod_NeverFires_AndNeverAccumulates()
        {
            float elapsed = 0f;
            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 100f, 0f), "period 0 must not fire");
                Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 100f, -1f), "negative period must not fire");
            }
            Assert.AreEqual(0f, elapsed, "guard must not accumulate (스핀-발동 방지)");
        }

        [Test]
        public void PeriodicTick_LagSpike_DripsOneFirePerTick()
        {
            // 대형 dt 가 여러 주기를 적립해도 틱당 1발만 — 이월분이 다음 틱에 소진.
            float elapsed = 0f;
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 3.5f, 1f), "spike tick fires once");
            Assert.AreEqual(2.5f, elapsed, 1e-4f);
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f), "banked period drips next tick");
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f));
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f), "bank exhausted");
        }

        // ── nightmare-catcher unit 3 — HealthThreshold (반복·래치·다중 돌파) ──

        [Test]
        public void HealthThreshold_FiresBelowEachBoundary_InSequence()
        {
            int k = 1; // 베이크 초기값 — 경계 90%, 80%, …
            Assert.IsFalse(DcTrigger.HealthThresholdEval(95f, 100f, 0.10f, ref k), "95 ≥ 90 no fire");
            Assert.IsTrue(DcTrigger.HealthThresholdEval(89f, 100f, 0.10f, ref k), "89 < 90 fires");
            Assert.AreEqual(2, k);
            Assert.IsFalse(DcTrigger.HealthThresholdEval(85f, 100f, 0.10f, ref k), "85 ≥ 80 no fire");
            Assert.IsTrue(DcTrigger.HealthThresholdEval(79f, 100f, 0.10f, ref k), "79 < 80 fires");
            Assert.AreEqual(3, k);
        }

        [Test]
        public void HealthThreshold_ExactBoundary_DoesNotFire()
        {
            int k = 1;
            Assert.IsFalse(DcTrigger.HealthThresholdEval(90f, 100f, 0.10f, ref k), "strict < — 경계 위는 미발동");
            Assert.AreEqual(1, k);
        }

        [Test]
        public void HealthThreshold_BigHit_CrossesMultipleBoundaries_FiresOnce()
        {
            int k = 1;
            // 100 → 55: 90/80/70/60 네 경계 관통 — 발동 1회, k 는 최심(60 아래 = 다음 경계 50, k=5).
            Assert.IsTrue(DcTrigger.HealthThresholdEval(55f, 100f, 0.10f, ref k));
            Assert.AreEqual(5, k, "k jumps to the deepest crossed boundary");
            Assert.IsFalse(DcTrigger.HealthThresholdEval(55f, 100f, 0.10f, ref k), "same hp — no re-fire");
        }

        [Test]
        public void HealthThreshold_HealBack_DoesNotRewindLatch()
        {
            int k = 1;
            Assert.IsTrue(DcTrigger.HealthThresholdEval(89f, 100f, 0.10f, ref k)); // k → 2
            // 힐로 95 회복 후 다시 89 로 — 같은 경계(90) 재돌파는 재발동 없음(래치 단조).
            Assert.IsFalse(DcTrigger.HealthThresholdEval(95f, 100f, 0.10f, ref k));
            Assert.IsFalse(DcTrigger.HealthThresholdEval(89f, 100f, 0.10f, ref k), "핑퐁 익스플로잇 차단");
            Assert.AreEqual(2, k);
            Assert.IsTrue(DcTrigger.HealthThresholdEval(79f, 100f, 0.10f, ref k), "다음 경계는 정상 발동");
        }

        [Test]
        public void HealthThreshold_NonPositiveFractionOrMaxHp_NeverFires()
        {
            int k = 1;
            Assert.IsFalse(DcTrigger.HealthThresholdEval(1f, 100f, 0f, ref k), "fraction 0 가드");
            Assert.IsFalse(DcTrigger.HealthThresholdEval(1f, 100f, -0.1f, ref k), "fraction 음수 가드");
            Assert.IsFalse(DcTrigger.HealthThresholdEval(1f, 0f, 0.10f, ref k), "maxHpRef 0 (미베이크 슬롯) 가드");
            Assert.AreEqual(1, k, "가드 경로는 k 를 움직이지 않는다");
        }

        [Test]
        public void HealthThreshold_ZeroHp_Terminates_AndFires()
        {
            int k = 1;
            // hp 0: 경계가 0 이하로 내려오면 0 < 0/음수 는 거짓 — 종료 보장.
            Assert.IsTrue(DcTrigger.HealthThresholdEval(0f, 100f, 0.10f, ref k));
            Assert.AreEqual(10, k, "경계 10%·k=10 에서 0 < 100·(1-1.0)=0 이 거짓 — 정지");
        }

        // ── dreamcatcher-trigger-gates unit 1 — 게이트 순수 함수 계약 ──

        [Test]
        public void GatePass_HpBelow_BoundaryIsInclusive()
        {
            // 30% 게이트: 정확히 경계값(30.0)은 통과(<=), 바로 위는 실패.
            Assert.IsTrue(DcTrigger.GatePass(Wassup.Data.DcGateKind.HpBelow, 0.30f, 29.9f, 100f), "29.9% 통과");
            Assert.IsTrue(DcTrigger.GatePass(Wassup.Data.DcGateKind.HpBelow, 0.30f, 30.0f, 100f), "정확히 30.0% 통과 (이하)");
            Assert.IsFalse(DcTrigger.GatePass(Wassup.Data.DcGateKind.HpBelow, 0.30f, 30.1f, 100f), "30.1% 실패");
            Assert.IsTrue(DcTrigger.GatePass(Wassup.Data.DcGateKind.None, 0f, 100f, 100f), "None 게이트는 항상 통과");
        }

        [Test]
        public void GatePass_HpBelow_GuardsZeroValueAndUnbakedMax()
        {
            Assert.IsFalse(DcTrigger.GatePass(Wassup.Data.DcGateKind.HpBelow, 0f, 1f, 100f), "무값 카드(gateValue 0) 가드");
            Assert.IsFalse(DcTrigger.GatePass(Wassup.Data.DcGateKind.HpBelow, 0.30f, 1f, 0f), "미베이크(max 0) 가드");
        }

        [Test]
        public void GateComboSupported_WiredTableIsExact()
        {
            // 배선 2조합만 true (v1). gate=None 은 전 트리거 통과.
            Assert.IsTrue(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.OnDamagedN, Wassup.Data.DcGateKind.HpBelow, Wassup.Data.DcGateSubject.Self));
            Assert.IsTrue(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.AttackN, Wassup.Data.DcGateKind.HpBelow, Wassup.Data.DcGateSubject.EventTarget));
            Assert.IsTrue(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.OnDeath, Wassup.Data.DcGateKind.None, Wassup.Data.DcGateSubject.Self), "None 은 항상 지원");
            // 퇴화/미배선 조합 — bake 거절 대상 (critic MED: 어서션 고정).
            Assert.IsFalse(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.OnDeath, Wassup.Data.DcGateKind.HpBelow, Wassup.Data.DcGateSubject.Self), "OnDeath×HpBelow: 사망 시 항상 참 = 퇴화");
            Assert.IsFalse(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.HealthThreshold, Wassup.Data.DcGateKind.HpBelow, Wassup.Data.DcGateSubject.Self), "상태 트리거에 게이트 중첩 거절");
            Assert.IsFalse(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.OnKill, Wassup.Data.DcGateKind.HpBelow, Wassup.Data.DcGateSubject.EventTarget), "사망 대상 항상 참 = 퇴화");
            Assert.IsFalse(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.AttackN, Wassup.Data.DcGateKind.HpBelow, Wassup.Data.DcGateSubject.Self), "미배선 (후속 후보)");
            Assert.IsFalse(DcTrigger.GateComboSupported(Wassup.Data.DcTriggerKind.OnDamagedN, Wassup.Data.DcGateKind.HpBelow, Wassup.Data.DcGateSubject.EventTarget), "미배선 (다중 source subject 규칙 미정)");
        }

        [Test]
        public void Gate_PreScanPrediction_MatchesCountedTick_AcrossPeriodsAndGateStates()
        {
            // HeavyStrike 합성 불변식: pre-scan(WouldFire ∧ Pass) == 루프(if(Pass) Tick 발화).
            // 같은 입력(동일 hp 스냅샷)으로 평가하는 한 period·게이트 상태 조합 전체에서 일치.
            foreach (ushort period in new ushort[] { 1, 3 })
            {
                ushort counter = 0;
                // hp 시나리오: 통과(20%) / 실패(80%) 를 섞어 12 사건 진행.
                float[] hpSeq = { 20f, 80f, 20f, 20f, 80f, 20f, 20f, 20f, 80f, 20f, 20f, 20f };
                foreach (float hp in hpSeq)
                {
                    ushort before = counter;
                    bool pass = DcTrigger.GatePass(Wassup.Data.DcGateKind.HpBelow, 0.30f, hp, 100f);
                    bool predicted = DcTrigger.WouldFire(counter, period) && pass;
                    bool fired = false;
                    if (pass) fired = DcTrigger.Tick(ref counter, period);
                    Assert.AreEqual(predicted, fired, $"period {period}, hp {hp}: pre-scan 예측과 실제 발화 불일치");
                    if (!pass) Assert.AreEqual(before, counter, $"period {period}, hp {hp}: 게이트 실패 사건은 counter 무변화(카운트 게이트)");
                }
            }
        }
    }
}
