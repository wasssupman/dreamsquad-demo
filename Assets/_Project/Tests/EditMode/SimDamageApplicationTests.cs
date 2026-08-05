using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 피해 정산(#34) 이식의 오라클.
    ///
    /// 앞 세 묶음은 레거시 `ShieldMathTests`·`KillAttributionTests`·`ModifierAuthoringTests` 의
    /// **어서션 복제**다. 뒤 묶음은 시스템 자체의 계약인데, 레거시에는 EditMode 오라클이 없다
    /// (골든 트레이스가 유일한 검증이었다) — 그래서 여기서 **처음** 계약을 코드로 고정한다.
    /// 골든 A/B 는 unit 19 의 권한이고, 이 테스트는 그 전에 이식이 조용히 어긋나는 걸 막는 그물이다.
    /// </summary>
    public class SimDamageApplicationTests
    {
        private SimWorld _world;
        private SimChannels _channels;
        private DamageApplicationSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _sut = new DamageApplicationSystem(_channels);
            _world.SetDeltaTime(0.1f);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        /// 피해를 받을 수 있는 최소 구성: Health + IncomingDamage 버퍼 + 위치.
        private SimEntityId NewVictim(float hp, float max = -1f)
        {
            var e = _world.Create();
            _world.Set(e, new Health { value = hp, max = max < 0f ? hp : max });
            _world.AddBuffer<IncomingDamage>(e);
            _world.Set(e, SimTransform.FromPosition(new SimVec3(1f, 2f, 3f)));
            return e;
        }

        private void Hit(SimEntityId target, float amount, SimEntityId source = default)
            => _world.GetBuffer<IncomingDamage>(target).Add(new IncomingDamage { amount = amount, source = source });

        private float Hp(SimEntityId e) => _world.Get<Health>(e).value;

        // ═════ ShieldMath (레거시 복제) ═══════════════════════════════════════

        private List<ShieldSlot> _slots;
        private SimEntityId _srcA, _srcB;

        private void ShieldSetUp()
        {
            _srcA = _world.Create();
            _srcB = _world.Create();
            var holder = _world.Create();
            _slots = _world.AddBuffer<ShieldSlot>(holder);
        }

        [Test]
        public void Merge_SameSource_TakesMax_NoStack()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 100f);
            ShieldMath.Merge(_slots, _srcA, 150f);
            Assert.AreEqual(1, _slots.Count, "same source must stay one slot");
            Assert.AreEqual(150f, _slots[0].value, "grant above remainder raises to B");

            ShieldMath.Merge(_slots, _srcA, 100f);
            Assert.AreEqual(150f, _slots[0].value, "grant below remainder keeps max");
        }

        [Test]
        public void Merge_DifferentSources_AddUp()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 100f);
            ShieldMath.Merge(_slots, _srcB, 100f);
            Assert.AreEqual(2, _slots.Count);
            Assert.AreEqual(200f, ShieldMath.Sum(_slots), "a 100 + b 100 = 200");
        }

        [Test]
        public void Absorb_Partial_ConsumesOldestFirst()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 100f);
            ShieldMath.Merge(_slots, _srcB, 100f);

            float pierced = ShieldMath.Absorb(_slots, 50f);

            Assert.AreEqual(0f, pierced);
            Assert.AreEqual(2, _slots.Count);
            Assert.AreEqual(_srcA, _slots[0].source, "oldest slot consumed first");
            Assert.AreEqual(50f, _slots[0].value);
            Assert.AreEqual(100f, _slots[1].value, "newer slot untouched");
        }

        [Test]
        public void Absorb_AcrossSlotBoundary_RemovesDepleted()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 100f);
            ShieldMath.Merge(_slots, _srcB, 100f);

            float pierced = ShieldMath.Absorb(_slots, 150f);

            Assert.AreEqual(0f, pierced);
            Assert.AreEqual(1, _slots.Count, "depleted oldest slot removed");
            Assert.AreEqual(_srcB, _slots[0].source);
            Assert.AreEqual(50f, _slots[0].value);
        }

        [Test]
        public void Absorb_FullAbsorb_ZeroPierce()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 200f);
            Assert.AreEqual(0f, ShieldMath.Absorb(_slots, 150f), "shield eats the whole hit");
            Assert.AreEqual(50f, _slots[0].value);
        }

        [Test]
        public void Absorb_NoShield_FullPierce()
        {
            ShieldSetUp();
            Assert.AreEqual(80f, ShieldMath.Absorb(_slots, 80f), "empty slots pass damage through unchanged");
        }

        [Test]
        public void Absorb_Overkill_PiercesRemainder()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 100f);
            Assert.AreEqual(150f, ShieldMath.Absorb(_slots, 250f));
            Assert.AreEqual(0, _slots.Count, "depleted slot removed");
        }

        [Test]
        public void ValueFromSource_ReturnsSlotValue_OrZeroForUnknown()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 120f);
            Assert.AreEqual(120f, ShieldMath.ValueFromSource(_slots, _srcA), "known source returns its slot value");
            Assert.AreEqual(0f, ShieldMath.ValueFromSource(_slots, _srcB), "unknown source returns 0 (no-op grant guard)");
        }

        [Test]
        public void Merge_NonPositiveAmount_Ignored()
        {
            ShieldSetUp();
            ShieldMath.Merge(_slots, _srcA, 0f);
            ShieldMath.Merge(_slots, _srcA, -10f);
            Assert.AreEqual(0, _slots.Count, "0/negative grants add nothing");
        }

        // ═════ KillAttribution (레거시 복제) ══════════════════════════════════

        [Test]
        public void SingleAttributed_Wins()
        {
            var a = _world.Create();
            var best = SimEntityId.Null; float amt = 0f;
            KillAttribution.Consider(10f, a, ref best, ref amt);
            Assert.AreEqual(a, best);
        }

        [Test]
        public void HighestAmount_Wins_RegardlessOfOrder()
        {
            SimEntityId a = _world.Create(), b = _world.Create(), c = _world.Create();
            var best = SimEntityId.Null; float amt = 0f;
            KillAttribution.Consider(5f, a, ref best, ref amt);
            KillAttribution.Consider(15f, b, ref best, ref amt);
            KillAttribution.Consider(9f, c, ref best, ref amt);
            Assert.AreEqual(b, best);
        }

        [Test]
        public void Tie_FirstConsideredWins()
        {
            SimEntityId a = _world.Create(), b = _world.Create();
            var best = SimEntityId.Null; float amt = 0f;
            KillAttribution.Consider(12f, a, ref best, ref amt);
            KillAttribution.Consider(12f, b, ref best, ref amt); // 동점 — 덮어쓰지 않음 (strict >)
            Assert.AreEqual(a, best);
        }

        [Test]
        public void NullSource_Ignored_EvenIfLargest()
        {
            var a = _world.Create();
            var best = SimEntityId.Null; float amt = 0f;
            KillAttribution.Consider(100f, SimEntityId.Null, ref best, ref amt); // DoT/환경 — 후보 아님
            KillAttribution.Consider(7f, a, ref best, ref amt);
            Assert.AreEqual(a, best, "비귀속(Null) 이 최대여도 killer 아님");
        }

        [Test]
        public void AllNullSource_NoKiller()
        {
            var best = SimEntityId.Null; float amt = 0f;
            KillAttribution.Consider(50f, SimEntityId.Null, ref best, ref amt);
            KillAttribution.Consider(30f, SimEntityId.Null, ref best, ref amt);
            Assert.AreEqual(SimEntityId.Null, best);
        }

        // ═════ ModifierAuthoring (레거시 복제) ════════════════════════════════

        [Test]
        public void Increase_AuthorsAsAdditiveDelta()
        {
            ModifierAuthoring.FromMultiplier(1.3f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Additive, op);
            Assert.AreEqual(0.3f, magnitude, 1e-5f, "×1.3 buff → +0.3 additive delta");
        }

        [Test]
        public void Identity_AuthorsAsAdditiveZero()
        {
            ModifierAuthoring.FromMultiplier(1f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Additive, op);
            Assert.AreEqual(0f, magnitude, 1e-5f, "×1.0 → +0.0 (identity)");
        }

        [Test]
        public void Reduction_StaysMultiplicativeWithRawMultiplier()
        {
            ModifierAuthoring.FromMultiplier(0.6f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Multiplicative, op);
            Assert.AreEqual(0.6f, magnitude, 1e-5f, "×0.6 debuff stays multiplicative");
        }

        [Test]
        public void ZeroMultiplier_StaysMultiplicative()
        {
            ModifierAuthoring.FromMultiplier(0f, out var op, out var magnitude);
            Assert.AreEqual(CombineOp.Multiplicative, op);
            Assert.AreEqual(0f, magnitude, 1e-5f);
        }

        // ═════ 정산 순서 (신규 계약 — 레거시 EditMode 오라클 없음) ═══════════

        [Test]
        public void Drain_AppliesSum_AndClearsBuffer()
        {
            var v = NewVictim(100f);
            Hit(v, 10f);
            Hit(v, 5f);

            _sut.Run(_world);

            Assert.AreEqual(85f, Hp(v), 1e-4f);
            Assert.AreEqual(0, _world.GetBuffer<IncomingDamage>(v).Count, "버퍼는 소비 후 비워진다");
        }

        [Test]
        public void DmgTakenMul_ScalesBeforeShieldAbsorb()
        {
            // ⚠ 순서가 계약이다 — 배율 뒤에 흡수해야 "표시 데미지 = 흡수량" 이 성립한다.
            var v = NewVictim(100f);
            _world.Set(v, new ModifierStats { dmgTakenMul = 2f, regenPerSec = 0f });
            var shield = _world.AddBuffer<ShieldSlot>(v);
            shield.Add(new ShieldSlot { source = _world.Create(), value = 30f });
            Hit(v, 20f); // ×2 = 40 → 실드 30 흡수 → 관통 10

            _sut.Run(_world);

            Assert.AreEqual(90f, Hp(v), 1e-4f, "배율을 먼저 곱하지 않으면 관통이 0 이 된다");
            Assert.AreEqual(0, shield.Count, "소진된 슬롯 제거");
        }

        [Test]
        public void Heal_PulseAndRegen_ClampToMax_ButOnlyPulseEmitsVfx()
        {
            var v = NewVictim(50f, 100f);
            _world.Set(v, new ModifierStats { dmgTakenMul = 1f, regenPerSec = 20f }); // ×0.1s = 2
            _world.AddBuffer<IncomingHeal>(v).Add(new IncomingHeal { amount = 5f });

            _sut.Run(_world);

            Assert.AreEqual(57f, Hp(v), 1e-4f, "펄스 5 + 재생 2");
            Assert.AreEqual(0, _world.GetBuffer<IncomingHeal>(v).Count, "펄스 버퍼는 매 프레임 비워진다");
            var vfx = _channels.HealApplied.Drain();
            Assert.AreEqual(1, vfx.Count);
            Assert.AreEqual(5f, vfx[0].amount, 1e-4f, "⚠ 재생분은 VFX 에 포함되지 않는다");
        }

        [Test]
        public void Heal_RegenOnly_EmitsNoVfx()
        {
            var v = NewVictim(50f, 100f);
            _world.Set(v, new ModifierStats { dmgTakenMul = 1f, regenPerSec = 20f });

            _sut.Run(_world);

            Assert.AreEqual(52f, Hp(v), 1e-4f);
            Assert.AreEqual(0, _channels.HealApplied.Count, "재생만으로는 VFX 도배가 없다");
        }

        [Test]
        public void Health_ClampsToMax_ButNotToZeroFloor()
        {
            var over = NewVictim(95f, 100f);
            _world.AddBuffer<IncomingHeal>(over).Add(new IncomingHeal { amount = 50f });
            var lethal = NewVictim(10f, 100f);
            Hit(lethal, 40f);

            _sut.Run(_world);

            Assert.AreEqual(100f, Hp(over), 1e-4f, "상한은 클램프된다");
            Assert.AreEqual(-30f, Hp(lethal), 1e-4f, "⚠ 하한 클램프는 없다 — 음수 HP 가 그대로 남는다");
        }

        [Test]
        public void UltimateLeap_DropsBuffer_InsteadOfDeferringDamage()
        {
            // ⚠ 쿼리 제외였다면 피해가 적립됐다가 착지에 터진다. 드랍이라 사라져야 한다.
            var v = NewVictim(100f);
            _world.Set(v, new UltimateLeapState { remaining = 2f });
            Hit(v, 999f);

            _sut.Run(_world);

            Assert.AreEqual(100f, Hp(v), 1e-4f, "이탈 중 피해는 버려진다");
            Assert.AreEqual(0, _world.GetBuffer<IncomingDamage>(v).Count, "적립이 아니라 드랍");
            Assert.IsFalse(_world.Has<DeadTag>(v), "공중 사망은 없다 = 착지가 보장된다");
        }

        [Test]
        public void DeadAndPending_AreSkippedEntirely()
        {
            var dead = NewVictim(100f);
            _world.Set(dead, new DeadTag());
            Hit(dead, 10f);
            var pending = NewVictim(100f);
            _world.Set(pending, new PendingDeployment());
            Hit(pending, 10f);

            _sut.Run(_world);

            Assert.AreEqual(100f, Hp(dead));
            Assert.AreEqual(100f, Hp(pending));
            Assert.AreEqual(1, _world.GetBuffer<IncomingDamage>(pending).Count, "스킵은 버퍼도 건드리지 않는다");
        }

        [Test]
        public void ShieldGrants_MergeEveryFrame_EvenWithoutDamage()
        {
            var v = NewVictim(100f);
            var slots = _world.AddBuffer<ShieldSlot>(v);
            var caster = _world.Create();
            _world.AddBuffer<IncomingShield>(v).Add(new IncomingShield { source = caster, amount = 40f });

            _sut.Run(_world); // 무피격 프레임

            Assert.AreEqual(40f, ShieldMath.Sum(slots), "무피격 프레임의 부여가 유실되면 안 된다");
            Assert.AreEqual(0, _world.GetBuffer<IncomingShield>(v).Count);
        }

        [Test]
        public void ShieldBreak_FiresOnDepletion_AndCarriesOnShieldBreakSlot()
        {
            var v = NewVictim(100f);
            var slots = _world.AddBuffer<ShieldSlot>(v);
            slots.Add(new ShieldSlot { source = _world.Create(), value = 30f });
            _world.AddBuffer<DcTriggerSlot>(v).Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnShieldBreak,
                payload = DcPayloadKind.AreaSleep,
                magnitude = 3f, tileRange = 2, duration = 1.5f, patternIndex = -1,
            });
            Hit(v, 50f); // 30 흡수 + 20 관통

            _sut.Run(_world);

            Assert.AreEqual(80f, Hp(v), 1e-4f);
            var events = _channels.ShieldBreak.Drain();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(DcPayloadKind.AreaSleep, events[0].payload);
            Assert.AreEqual(1.5f, events[0].duration, 1e-4f);
            Assert.AreEqual(-1, events[0].aoeDataIndex, "SelfTileAoe 가 아니면 index 는 -1");
            Assert.IsFalse(events[0].fromDamagedTrigger, "실드 파열 발(피격 폭발 아님)");
        }

        [Test]
        public void ShieldBreak_DoesNotFire_WhenShieldSurvives()
        {
            var v = NewVictim(100f);
            _world.AddBuffer<ShieldSlot>(v).Add(new ShieldSlot { source = _world.Create(), value = 30f });
            _world.AddBuffer<DcTriggerSlot>(v).Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnShieldBreak, payload = DcPayloadKind.AreaSleep, patternIndex = -1,
            });
            Hit(v, 10f);

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.ShieldBreak.Count, "Sum>0 이 유지되면 파열이 아니다");
        }

        [Test]
        public void ShieldBreak_FiresEvenOnLethalFrame()
        {
            // ⚠ 파열은 사망 분기와 **독립**이다 — 관통 킬에도 발동한다.
            var v = NewVictim(10f);
            _world.AddBuffer<ShieldSlot>(v).Add(new ShieldSlot { source = _world.Create(), value = 5f });
            _world.AddBuffer<DcTriggerSlot>(v).Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnShieldBreak, payload = DcPayloadKind.AreaSleep, patternIndex = -1,
            });
            Hit(v, 100f);

            _sut.Run(_world);

            Assert.IsTrue(_world.Has<DeadTag>(v));
            Assert.AreEqual(1, _channels.ShieldBreak.Count, "죽는 프레임에도 파열은 난다");
        }

        [Test]
        public void DamageNumbers_OnePerHit_WithSettledRatio()
        {
            var v = NewVictim(100f);
            _world.Set(v, new AttackUnitTag());
            _world.Set(v, new ModifierStats { dmgTakenMul = 2f, regenPerSec = 0f });
            Hit(v, 10f);
            Hit(v, 15f);

            _sut.Run(_world);

            var fonts = _channels.DamageNumber.Drain();
            Assert.AreEqual(2, fonts.Count, "히트당 하나 — 프레임 합이 아니다");
            Assert.AreEqual(20f, fonts[0].amount, 1e-4f, "경감 후 값");
            Assert.AreEqual(30f, fonts[1].amount, 1e-4f);
            Assert.AreEqual(0.5f, fonts[0].hpRatio, 1e-4f, "정산 후 비율 — 두 폰트가 같은 값");
            Assert.AreEqual(fonts[0].hpRatio, fonts[1].hpRatio);
        }

        [Test]
        public void DamageNumbers_FullyAbsorbedFrame_ShowsNothing()
        {
            var v = NewVictim(100f);
            _world.Set(v, new AttackUnitTag());
            _world.AddBuffer<ShieldSlot>(v).Add(new ShieldSlot { source = _world.Create(), value = 100f });
            Hit(v, 40f);

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.DamageNumber.Count, "완전 흡수 = 피격 아님(계약)");
        }

        [Test]
        public void DamageNumbers_PartialAbsorb_ScalesEachFontByPierceRatio()
        {
            var v = NewVictim(100f);
            _world.Set(v, new AttackUnitTag());
            _world.AddBuffer<ShieldSlot>(v).Add(new ShieldSlot { source = _world.Create(), value = 20f });
            Hit(v, 20f);
            Hit(v, 20f); // 총 40 중 20 흡수 → pierceRatio 0.5

            _sut.Run(_world);

            var fonts = _channels.DamageNumber.Drain();
            Assert.AreEqual(2, fonts.Count);
            Assert.AreEqual(10f, fonts[0].amount, 1e-4f, "관통분 비례 배분");
            Assert.AreEqual(10f, fonts[1].amount, 1e-4f);
        }

        [Test]
        public void WakeOnHit_RequestsSleepClear_OnlyWhenSleepingAndHit()
        {
            var sleeper = NewVictim(100f);
            _world.AddBuffer<CcEffect>(sleeper).Add(new CcEffect { kind = CcKind.Sleep });
            Hit(sleeper, 5f);

            var stunned = NewVictim(100f);
            _world.AddBuffer<CcEffect>(stunned).Add(new CcEffect { kind = CcKind.Stun });
            Hit(stunned, 5f);

            var untouchedSleeper = NewVictim(100f);
            _world.AddBuffer<CcEffect>(untouchedSleeper).Add(new CcEffect { kind = CcKind.Sleep });

            _sut.Run(_world);

            var requests = _channels.CcClear.Drain();
            Assert.AreEqual(1, requests.Count, "Stun 은 대상 아님 · 무피격 수면도 대상 아님");
            Assert.AreEqual(sleeper, requests[0].entity);
            Assert.AreEqual(CcKind.Sleep, requests[0].kind);
        }

        [Test]
        public void WakeOnHit_FiresOnLethalHitToo()
        {
            var v = NewVictim(5f);
            _world.AddBuffer<CcEffect>(v).Add(new CcEffect { kind = CcKind.Sleep });
            Hit(v, 100f);

            _sut.Run(_world);

            Assert.AreEqual(1, _channels.CcClear.Count, "치명타 프레임에도 보낸다(소비자가 생존 확인)");
        }

        // ── OnDamagedN ───────────────────────────────────────────────────────

        private SimEntityId NewDefenderWithCounter(DamagedCounter slot, float hp = 100f)
        {
            var d = NewVictim(hp);
            _world.Set(d, new DefenderUnitTag());
            _world.AddBuffer<DamagedCounter>(d).Add(slot);
            return d;
        }

        [Test]
        public void DamagedCounter_FiresOnNthDamagedFrame_GrantingDoubleFire()
        {
            var d = NewDefenderWithCounter(new DamagedCounter
            {
                period = 2, payload = DcPayloadKind.NextAttackDoubleFire, aoeDataIndex = -1,
            });

            Hit(d, 1f); Hit(d, 1f); // 같은 프레임 2히트 = 피격 1회
            _sut.Run(_world);
            Assert.IsFalse(_world.Has<NextAttackDoubleFire>(d), "프레임당 피격은 1로 센다");

            Hit(d, 1f);
            _sut.Run(_world);
            Assert.IsTrue(_world.Has<NextAttackDoubleFire>(d), "두 번째 피격 프레임에 발동");
            Assert.AreEqual(1, _world.Get<NextAttackDoubleFire>(d).charges);
        }

        [Test]
        public void DamagedCounter_GateFailure_LeavesCounterUntouched()
        {
            // HpBelow 30% 게이트 — 판정은 **이 피격 적용 후**(newHp) 기준이다.
            var d = NewDefenderWithCounter(new DamagedCounter
            {
                period = 1, payload = DcPayloadKind.NextAttackDoubleFire,
                gate = DcGateKind.HpBelow, gateValue = 0.30f, aoeDataIndex = -1,
            });

            Hit(d, 10f); // 90 남음 — 게이트 실패
            _sut.Run(_world);
            Assert.IsFalse(_world.Has<NextAttackDoubleFire>(d));
            Assert.AreEqual(0, _world.GetBuffer<DamagedCounter>(d)[0].counter, "카운트 게이트 — 무변화");

            Hit(d, 65f); // 25 남음 = 25% — "그 이하로 만든 그 피격부터"
            _sut.Run(_world);
            Assert.IsTrue(_world.Has<NextAttackDoubleFire>(d), "적용 후 기준이라 이 피격이 카운트된다");
        }

        [Test]
        public void DamagedCounter_SelfTileAoe_SharesShieldBreakChannel()
        {
            var d = NewDefenderWithCounter(new DamagedCounter
            {
                period = 1, payload = DcPayloadKind.SelfTileAoe,
                magnitude = 12f, tileRange = 2, aoeDataIndex = 7,
            });
            Hit(d, 5f);

            _sut.Run(_world);

            var events = _channels.ShieldBreak.Drain();
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].fromDamagedTrigger, "실드 파열과 구분하는 유일한 축");
            Assert.AreEqual(12f, events[0].magnitude, 1e-4f);
            Assert.AreEqual(7, events[0].aoeDataIndex);
            Assert.AreEqual(0f, events[0].duration, 1e-4f);
        }

        [Test]
        public void DamagedCounter_UnhandledPayload_WarnsInsteadOfSilentNoOp()
        {
            var d = NewDefenderWithCounter(new DamagedCounter
            {
                period = 1, payload = DcPayloadKind.EmitProjectilePattern, aoeDataIndex = -1,
            });
            Hit(d, 5f);

            _sut.Run(_world);

            var warnings = _channels.Warnings.Drain();
            Assert.AreEqual(1, warnings.Count, "arm 없는 발동은 소리를 내야 한다");
            Assert.AreEqual(SimWarningCode.DamagedCounterUnhandledPayload, warnings[0].code);
            Assert.AreEqual((int)DcPayloadKind.EmitProjectilePattern, warnings[0].detail);
        }

        [Test]
        public void DamagedCounter_DoesNotCount_OnLethalFrame()
        {
            var d = NewDefenderWithCounter(new DamagedCounter
            {
                period = 1, payload = DcPayloadKind.NextAttackDoubleFire, aoeDataIndex = -1,
            }, hp: 5f);
            Hit(d, 10f);

            _sut.Run(_world);

            Assert.IsTrue(_world.Has<DeadTag>(d));
            Assert.IsFalse(_world.Has<NextAttackDoubleFire>(d), "죽는 프레임은 세지 않는다(newHp > 0 조건)");
        }

        // ── 사망 정산 ────────────────────────────────────────────────────────

        [Test]
        public void Death_MarksButDoesNotDestroy()
        {
            // ⚠ 파괴는 P12 다 — 마킹과 파괴 사이의 1틱 창이 사망 릴레이의 전제다.
            var v = NewVictim(10f);
            Hit(v, 50f);

            _sut.Run(_world);

            Assert.IsTrue(_world.Has<DeadTag>(v));
            Assert.IsTrue(_world.Exists(v), "이 시스템은 파괴하지 않는다");
        }

        [Test]
        public void EnemyKilled_CopiesRewardsBeforeDestruction()
        {
            var killer = _world.Create();
            var enemy = NewVictim(10f);
            _world.Set(enemy, new AttackUnitTag());
            _world.Set(enemy, new AwakeningReward { value = 3 });
            _world.Set(enemy, new KillScore { value = 250 });
            Hit(enemy, 4f, killer);
            Hit(enemy, 20f, killer);

            _sut.Run(_world);

            var kills = _channels.EnemyKilled.Drain();
            Assert.AreEqual(1, kills.Count);
            Assert.AreEqual(3, kills[0].awakeningReward);
            Assert.AreEqual(250, kills[0].killScore);
            Assert.AreEqual(enemy, kills[0].entity, "등록부 키");
            Assert.AreEqual(killer, kills[0].killer);
            Assert.IsFalse(kills[0].hasKillBurst, "killer 에 OnKill 슬롯이 없으면 무폭발");
        }

        [Test]
        public void EnemyKilled_NotEmittedForNonEnemy()
        {
            var d = NewVictim(10f); // AttackUnitTag 없음
            Hit(d, 50f);

            _sut.Run(_world);

            Assert.IsTrue(_world.Has<DeadTag>(d));
            Assert.AreEqual(0, _channels.EnemyKilled.Count, "점수는 적 처치에서만 나온다");
        }

        [Test]
        public void KillBurst_StampsFirstMatchingOnKillSlot()
        {
            var killer = _world.Create();
            var slots = _world.AddBuffer<DcTriggerSlot>(killer);
            slots.Add(new DcTriggerSlot { trigger = DcTriggerKind.OnKill, payload = DcPayloadKind.SelfStatBuff, patternIndex = -1 });
            slots.Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnKill, payload = DcPayloadKind.SelfTileAoe,
                magnitude = 44f, tileRange = 3, projectileDataIndex = 9, patternIndex = -1,
            });
            slots.Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnKill, payload = DcPayloadKind.SelfTileAoe,
                magnitude = 99f, patternIndex = -1,
            });

            var enemy = NewVictim(10f);
            _world.Set(enemy, new AttackUnitTag());
            Hit(enemy, 50f, killer);

            _sut.Run(_world);

            var kill = _channels.EnemyKilled.Drain()[0];
            Assert.IsTrue(kill.hasKillBurst);
            Assert.AreEqual(44f, kill.burstDamage, 1e-4f, "첫 매칭 슬롯만 — 두 번째는 무시");
            Assert.AreEqual(3, kill.burstTileRange);
            Assert.AreEqual(9, kill.burstDataIndex);
        }

        [Test]
        public void OnKillSelfStatBuff_EnqueuesToKillerWithPermanentTtlWhenDurationUnset()
        {
            var killer = _world.Create();
            _world.AddBuffer<DcTriggerSlot>(killer).Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnKill, payload = DcPayloadKind.SelfStatBuff,
                buffStat = StatKind.AttackSpeedMul, magnitude = 1.05f,
                duration = 0f, statBuffStackId = 77, patternIndex = -1,
            });
            var enemy = NewVictim(10f);
            _world.Set(enemy, new AttackUnitTag());
            Hit(enemy, 50f, killer);

            _sut.Run(_world);

            var mods = _channels.StatApply.Drain();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(killer, mods[0].target, "자기 버프 — 대상은 killer");
            Assert.AreEqual(killer, mods[0].source);
            Assert.AreEqual(StatKind.AttackSpeedMul, mods[0].stat);
            Assert.AreEqual(CombineOp.Additive, mods[0].op, "×1.05 = 증가 → 가산 버킷");
            Assert.AreEqual(0.05f, mods[0].magnitude, 1e-5f);
            Assert.AreEqual(float.PositiveInfinity, mods[0].duration, "duration<=0 = 영구");
            Assert.AreEqual(77, mods[0].stackId, "슬롯 고정 stackId → 비스택 refresh");
            Assert.AreEqual(ModifierOrigin.Dreamcatcher, mods[0].origin);
        }

        [Test]
        public void OnKillSelfStatBuff_IsFactionNeutral_AndSkipsUnattributedKills()
        {
            var killer = _world.Create();
            _world.AddBuffer<DcTriggerSlot>(killer).Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnKill, payload = DcPayloadKind.SelfStatBuff,
                buffStat = StatKind.DamageMul, magnitude = 1.1f, duration = 4f, patternIndex = -1,
            });

            // ① victim 이 적이 아니어도 발동한다(진영 무관).
            var ally = NewVictim(10f);
            Hit(ally, 50f, killer);
            // ② source 없는 피해(DoT/환경)로 죽으면 killer 가 없어 미발동.
            var byDot = NewVictim(10f);
            Hit(byDot, 50f);

            _sut.Run(_world);

            var mods = _channels.StatApply.Drain();
            Assert.AreEqual(1, mods.Count, "귀속된 킬 하나만");
            Assert.AreEqual(4f, mods[0].duration, 1e-4f, "duration>0 은 그대로 TTL");
        }
    }
}
