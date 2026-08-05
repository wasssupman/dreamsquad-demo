using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wassup.Sim.Combat;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/2 — 드림캐쳐 어휘 이식의 차등 오라클.
    ///
    /// 앞부분은 레거시 `DcTriggerTests` 의 **어서션을 그대로 복제**한다(재작성하지 않는다) —
    /// 같은 입력에 같은 답이 나오는지가 이식의 전부이므로, 표현을 손대면 그 비교가 흐려진다.
    ///
    /// 뒷부분(<see cref="VocabularyParity"/>)이 이 파일의 진짜 이유다. 신 sim 은
    /// `Wassup.Data` 를 참조할 수 없어서(asmdef I3) **어휘를 복제**했고, 복제된 두 enum 이
    /// 갈라져도 **컴파일러는 아무 말도 하지 않는다**. bake 가 int 로 건너뛰는 순간
    /// `AreaSleep` 카드가 `EmitProjectilePattern` 이 되는 식의 조용한 오작동이 된다.
    /// ⇒ 값 대조를 테스트로 박제한다.
    /// </summary>
    public class SimDcTriggerTests
    {
        // ── AttackN 카운팅 계약 (레거시 복제) ────────────────────────────────

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

        // ── WouldFire (비변이 쌍둥이) ────────────────────────────────────────

        [Test]
        public void WouldFire_MatchesTick_ForEveryCounterInPeriod()
        {
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

        // ── PeriodicTimer 누산기 ─────────────────────────────────────────────

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
            float elapsed = 0f;
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 3.5f, 1f), "spike tick fires once");
            Assert.AreEqual(2.5f, elapsed, 1e-4f);
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f), "banked period drips next tick");
            Assert.IsTrue(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f));
            Assert.IsFalse(DcTrigger.PeriodicTick(ref elapsed, 0f, 1f), "bank exhausted");
        }

        // ── HealthThreshold (반복·래치·다중 돌파) ───────────────────────────

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
            Assert.IsTrue(DcTrigger.HealthThresholdEval(0f, 100f, 0.10f, ref k));
            Assert.AreEqual(10, k, "경계 10%·k=10 에서 0 < 100·(1-1.0)=0 이 거짓 — 정지");
        }

        // ── 게이트 순수 함수 계약 ────────────────────────────────────────────

        [Test]
        public void GatePass_HpBelow_BoundaryIsInclusive()
        {
            Assert.IsTrue(DcTrigger.GatePass(DcGateKind.HpBelow, 0.30f, 29.9f, 100f), "29.9% 통과");
            Assert.IsTrue(DcTrigger.GatePass(DcGateKind.HpBelow, 0.30f, 30.0f, 100f), "정확히 30.0% 통과 (이하)");
            Assert.IsFalse(DcTrigger.GatePass(DcGateKind.HpBelow, 0.30f, 30.1f, 100f), "30.1% 실패");
            Assert.IsTrue(DcTrigger.GatePass(DcGateKind.None, 0f, 100f, 100f), "None 게이트는 항상 통과");
        }

        [Test]
        public void GatePass_HpBelow_GuardsZeroValueAndUnbakedMax()
        {
            Assert.IsFalse(DcTrigger.GatePass(DcGateKind.HpBelow, 0f, 1f, 100f), "무값 카드(gateValue 0) 가드");
            Assert.IsFalse(DcTrigger.GatePass(DcGateKind.HpBelow, 0.30f, 1f, 0f), "미베이크(max 0) 가드");
        }

        [Test]
        public void GateComboSupported_WiredTableIsExact()
        {
            Assert.IsTrue(DcTrigger.GateComboSupported(DcTriggerKind.OnDamagedN, DcGateKind.HpBelow, DcGateSubject.Self));
            Assert.IsTrue(DcTrigger.GateComboSupported(DcTriggerKind.AttackN, DcGateKind.HpBelow, DcGateSubject.EventTarget));
            Assert.IsTrue(DcTrigger.GateComboSupported(DcTriggerKind.OnDeath, DcGateKind.None, DcGateSubject.Self), "None 은 항상 지원");
            Assert.IsFalse(DcTrigger.GateComboSupported(DcTriggerKind.OnDeath, DcGateKind.HpBelow, DcGateSubject.Self), "OnDeath×HpBelow: 사망 시 항상 참 = 퇴화");
            Assert.IsFalse(DcTrigger.GateComboSupported(DcTriggerKind.HealthThreshold, DcGateKind.HpBelow, DcGateSubject.Self), "상태 트리거에 게이트 중첩 거절");
            Assert.IsFalse(DcTrigger.GateComboSupported(DcTriggerKind.OnKill, DcGateKind.HpBelow, DcGateSubject.EventTarget), "사망 대상 항상 참 = 퇴화");
            Assert.IsFalse(DcTrigger.GateComboSupported(DcTriggerKind.AttackN, DcGateKind.HpBelow, DcGateSubject.Self), "미배선 (후속 후보)");
            Assert.IsFalse(DcTrigger.GateComboSupported(DcTriggerKind.OnDamagedN, DcGateKind.HpBelow, DcGateSubject.EventTarget), "미배선 (다중 source subject 규칙 미정)");
            Assert.IsFalse(DcTrigger.GateComboSupported(DcTriggerKind.OnDamagedN, (DcGateKind)999, DcGateSubject.Self), "미래 gate enum 은 명시 배선 전까지 거절");
        }

        [Test]
        public void Gate_PreScanPrediction_MatchesCountedTick_AcrossPeriodsAndGateStates()
        {
            // 합성 불변식: pre-scan(WouldFire ∧ Pass) == 루프(if(Pass) Tick 발화).
            foreach (ushort period in new ushort[] { 1, 3 })
            {
                ushort counter = 0;
                float[] hpSeq = { 20f, 80f, 20f, 20f, 80f, 20f, 20f, 20f, 80f, 20f, 20f, 20f };
                foreach (float hp in hpSeq)
                {
                    ushort before = counter;
                    bool pass = DcTrigger.GatePass(DcGateKind.HpBelow, 0.30f, hp, 100f);
                    bool predicted = DcTrigger.WouldFire(counter, period) && pass;
                    bool fired = false;
                    if (pass) fired = DcTrigger.Tick(ref counter, period);
                    Assert.AreEqual(predicted, fired, $"period {period}, hp {hp}: pre-scan 예측과 실제 발화 불일치");
                    if (!pass) Assert.AreEqual(before, counter, $"period {period}, hp {hp}: 게이트 실패 사건은 counter 무변화(카운트 게이트)");
                }
            }
        }

        // ── 어휘 평행성 — 이 파일의 존재 이유 ────────────────────────────────

        /// <summary>
        /// sim 어휘와 데이터 계층 어휘의 **이름→값 사상이 완전히 같아야** 한다.
        ///
        /// 신 sim 이 `Wassup.Data` 를 참조할 수 없으니(I3) 어휘가 복제됐고, 복제본이 갈라져도
        /// 컴파일 에러가 나지 않는다. bake 가 두 어휘를 int 로 건너뛰는 자리(18-K)에서 그 어긋남은
        /// **다른 payload 가 발동하는** 조용한 오작동이 된다. 한쪽에만 값을 추가/삽입하면 여기서 터진다.
        ///
        /// 양방향 대조다 — 한쪽 누락(sim 이 안 따라감)과 한쪽 초과(sim 이 앞서감) 둘 다 잡는다.
        /// </summary>
        [Test]
        public void VocabularyParity_SimEnumsMatchDataLayerExactly()
        {
            AssertEnumParity(typeof(DcTriggerKind), typeof(Wassup.Data.DcTriggerKind));
            AssertEnumParity(typeof(DcPayloadKind), typeof(Wassup.Data.DcPayloadKind));
            AssertEnumParity(typeof(DcGateKind), typeof(Wassup.Data.DcGateKind));
            AssertEnumParity(typeof(DcGateSubject), typeof(Wassup.Data.DcGateSubject));
        }

        private static void AssertEnumParity(Type sim, Type data)
        {
            Dictionary<string, long> Map(Type t) => Enum.GetValues(t)
                .Cast<object>()
                .ToDictionary(v => Enum.GetName(t, v), v => Convert.ToInt64(v));

            var simMap = Map(sim);
            var dataMap = Map(data);

            var missing = dataMap.Keys.Except(simMap.Keys).OrderBy(k => k).ToArray();
            var extra = simMap.Keys.Except(dataMap.Keys).OrderBy(k => k).ToArray();
            Assert.IsEmpty(missing, $"{sim.Name}: 데이터 계층에 있는데 sim 에 없다 — {string.Join(", ", missing)}");
            Assert.IsEmpty(extra, $"{sim.Name}: sim 에만 있다(데이터 계층 미추가) — {string.Join(", ", extra)}");

            foreach (var kv in dataMap)
                Assert.AreEqual(kv.Value, simMap[kv.Key], $"{sim.Name}.{kv.Key}: 값이 갈라졌다 (bake 가 int 로 건너뛴다)");
        }
    }
}
