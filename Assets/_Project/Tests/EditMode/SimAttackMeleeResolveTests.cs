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
    /// battle-sim-extraction unit 18-I/2 arm F — RESOLVE 의 **근접/Outputs 경로** + 주 타겟 CC +
    /// 발동형 카드 카운트. `ProjectileRef` 없는 공격자의 자리다.
    ///
    /// 어서션 복제 출처(구 `AttackSystemUnifiedLoopTests`): `U2` · `U3` · `U4`(피해) · `U5` · `U6` ·
    /// `Melee_PokeNeedle_Fires_Needle_Carrier_On_Fifth_Attack`.
    ///
    /// 계약 셋: **① 공격 1회 = 카드 1카운트**(다중 산출물이어도 1, 불발 resolve 는 0)
    /// **② 최전방 보너스는 잠근 주 타겟 1체만**(부수·AoE 는 기본) **③ 같은 `dmg` 가 피해와 위협
    /// 양쪽에 들어간다**(갈리면 어그로가 desync).
    /// </summary>
    public class SimAttackMeleeResolveTests
    {
        private SimWorld _world;
        private SimChannels _channels;
        private AttackSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _sut = new AttackSystem(_channels);
            _world.SetDeltaTime(0.016f);
        }

        private SimEntityId Attacker(
            Faction faction, SimVec3 pos, float damage, float range = 10f, float cooldown = 1f,
            int targetMask = (int)Faction.Enemy, int attackTargetCount = 1,
            bool defenderTag = false, bool attackerTag = false)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new FactionTag { value = faction });
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, new AttackState
            {
                range = range, cooldownDuration = cooldown, cooldownRemaining = 0f,
                attackTargetCount = attackTargetCount, targetMask = targetMask,
            });
            if (defenderTag) _world.Set(e, new DefenderUnitTag());
            if (attackerTag) _world.Set(e, new AttackUnitTag());
            if (damage > 0f)
                _world.AddBuffer<AttackOutputElement>(e).Add(new AttackOutputElement
                {
                    value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = damage },
                });
            return e;
        }

        private SimEntityId Target(Faction faction, SimVec3 pos, float hp = 10f, float max = 10f,
                                   bool defenderTag = false, bool attackerTag = false)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new FactionTag { value = faction });
            _world.Set(e, new Health { value = hp, max = max });
            if (defenderTag) _world.Set(e, new DefenderUnitTag());
            if (attackerTag) _world.Set(e, new AttackUnitTag());
            return e;
        }

        private List<IncomingDamage> Damage(SimEntityId e) => _world.GetBuffer<IncomingDamage>(e);

        private int DamageCount(SimEntityId e)
        {
            var b = _world.GetBuffer<IncomingDamage>(e);
            return b?.Count ?? 0;
        }

        private int CarrierCount()
        {
            int n = 0;
            foreach (var _ in _world.With<ProjectileRequestCarrier>()) n++;
            return n;
        }

        // ── 구 오라클 복제 ────────────────────────────────────────────────────

        [Test]
        public void U2_MeleeAoe_HitsTwoTargets()
        {
            Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 4f,
                     attackTargetCount: 2, defenderTag: true);
            var enemy1 = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);
            var enemy2 = Target(Faction.Enemy, new SimVec3(2f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, DamageCount(enemy1));
            Assert.AreEqual(4f, Damage(enemy1)[0].amount, 1e-4f);
            Assert.AreEqual(1, DamageCount(enemy2), "부수 대상도 AoE 로 맞는다");
            Assert.AreEqual(4f, Damage(enemy2)[0].amount, 1e-4f);
        }

        [Test]
        public void U3_DefenderCcData_EnqueuesKnockback()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 3f, defenderTag: true);
            _world.Set(defender, new DefenderCcData { knockbackDistance = 2f, knockbackDuration = 0.5f });
            var enemy = Target(Faction.Enemy, new SimVec3(3f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var cc = _channels.EnemyCc.Drain();
            Assert.AreEqual(1, cc.Count);
            Assert.AreEqual(enemy, cc[0].target);
            Assert.AreEqual(CcKind.Impulse, cc[0].effect.kind);
        }

        [Test]
        public void U4_DamageMul_MultipliesTheOutput()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 10f, defenderTag: true);
            _world.Set(defender, new ModifierStats { damageMul = 3f, attackSpeedMul = 1f, damageVsCcMul = 1f });
            var enemy = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, DamageCount(enemy));
            Assert.AreEqual(30f, Damage(enemy)[0].amount, 1e-4f);
        }

        [Test]
        public void U5_EnemyAttacksDefender_DirectDamage_NoProjectile()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), damage: 6f,
                                 targetMask: (int)(Faction.Defender | Faction.BlockingHazard),
                                 attackerTag: true);
            var defender = Target(Faction.Defender, new SimVec3(2f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, DamageCount(defender));
            Assert.AreEqual(6f, Damage(defender)[0].amount, 1e-4f);
            Assert.AreEqual(enemy, Damage(defender)[0].source);
            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(enemy));
        }

        [Test]
        public void U6_EnemyAttacksTheNearerHazard()
        {
            Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), damage: 4f,
                     targetMask: (int)(Faction.Defender | Faction.BlockingHazard), attackerTag: true);
            var hazard = Target(Faction.BlockingHazard, new SimVec3(1f, 0f, 0f));
            var defender = Target(Faction.Defender, new SimVec3(5f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, DamageCount(hazard));
            Assert.AreEqual(4f, Damage(hazard)[0].amount, 1e-4f);
            Assert.AreEqual(0, DamageCount(defender), "더 먼 방어유닛은 대상이 아니다");
        }

        [Test]
        public void Melee_PokeNeedle_FiresOnTheFifthAttack()
        {
            // 근접 유닛도 부착 카드를 돌린다 — 부착/발동 경로에 근접 게이트가 없다.
            var melee = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 4f,
                                 cooldown: 0.001f, defenderTag: true);
            Assert.IsFalse(_world.Has<ProjectileRef>(melee), "sanity: 이 유닛은 근접이다");
            _world.AddBuffer<DcTriggerSlot>(melee).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 5, counter = 0,
                payload = DcPayloadKind.ProjectileToTarget, magnitude = 20f,
                projectileDataIndex = 0, speed = 10f, hitThreshold = 0.3f, visualScale = 1f,
                patternIndex = -1,
            });
            var enemy = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            for (int i = 0; i < 4; i++) _sut.Run(_world);
            Assert.AreEqual(0, CarrierCount(), "니들은 5회째 전에 나가면 안 된다");
            Assert.AreEqual(4, DamageCount(enemy), "근접 직접타는 매 공격 들어간다(투사체 경로 아님)");

            _sut.Run(_world);
            Assert.AreEqual(1, CarrierCount(), "근접 유닛도 5회째에 니들 캐리어를 스폰한다");
            Assert.AreEqual(5, DamageCount(enemy), "니들은 별도 산출물 — 직접타는 그대로");
        }

        // ── 산출물 kind 분기 ──────────────────────────────────────────────────

        [Test]
        public void HealOutput_AppendsIncomingHeal()
        {
            var healer = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 0f,
                                  targetMask: (int)Faction.Defender, defenderTag: true);
            _world.AddBuffer<AttackOutputElement>(healer).Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Heal, magnitude = 7f },
            });
            var ally = Target(Faction.Defender, new SimVec3(1f, 0f, 0f), hp: 3f, defenderTag: true);

            _sut.Run(_world);

            var heals = _world.GetBuffer<IncomingHeal>(ally);
            Assert.AreEqual(1, heals.Count);
            Assert.AreEqual(7f, heals[0].amount, 1e-4f);
        }

        [Test]
        public void ApplyStatOutput_GoesToTheModifierChannel_WithOnHitOrigin()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 0f, defenderTag: true);
            _world.AddBuffer<AttackOutputElement>(defender).Add(new AttackOutputElement
            {
                value = new AttackOutput
                {
                    kind = AttackOutputKind.ApplyStat, stat = StatKind.MoveSpeedMul,
                    op = CombineOp.Multiplicative, magnitude = 0.5f, duration = 2f,
                },
            });
            var enemy = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var evts = _channels.StatApply.Drain();
            Assert.AreEqual(1, evts.Count);
            Assert.AreEqual(enemy, evts[0].target);
            Assert.AreEqual(StatKind.MoveSpeedMul, evts[0].stat);
            Assert.AreEqual(0.5f, evts[0].magnitude, 1e-4f);
            Assert.AreEqual(2f, evts[0].duration, 1e-4f);
            Assert.AreEqual(defender, evts[0].source);
            Assert.AreEqual(ModifierOrigin.OnHit, evts[0].origin);
        }

        [Test]
        public void ApplyStackOutput_UsesTheAuthoredCap_OrTheDefault()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 0f, defenderTag: true);
            var outs = _world.AddBuffer<AttackOutputElement>(defender);
            outs.Add(new AttackOutputElement
            {
                value = new AttackOutput
                {
                    kind = AttackOutputKind.ApplyStack, stackKind = StackKind.Fire,
                    magnitude = 2f, duration = 3f, stackMaxStack = 9,
                },
            });
            outs.Add(new AttackOutputElement
            {
                value = new AttackOutput
                {
                    kind = AttackOutputKind.ApplyStack, stackKind = StackKind.Ice,
                    magnitude = 1f, duration = 3f, stackMaxStack = 0,
                },
            });
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var evts = _channels.StackApply.Drain();
            Assert.AreEqual(2, evts.Count);
            Assert.AreEqual(9, evts[0].maxStack, "저작 cap 을 그대로 쓴다");
            Assert.AreEqual(2, evts[0].countDelta);
            Assert.AreEqual(StackDefaults.MaxStack, evts[1].maxStack, "미지정(0)이면 소비자 디폴트");
        }

        [Test]
        public void MultipleOutputs_StillCountAsOneCardTick()
        {
            // ⚠ **공격 1회 = 1카운트**. 산출물 수와 무관하다.
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 5f, defenderTag: true);
            _world.GetBuffer<AttackOutputElement>(defender).Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Heal, magnitude = 1f },
            });
            _world.AddBuffer<DcTriggerSlot>(defender).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 5, counter = 0,
                payload = DcPayloadKind.ProjectileToTarget, patternIndex = -1,
            });
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, _world.GetBuffer<DcTriggerSlot>(defender)[0].counter);
        }

        [Test]
        public void LapsedResolve_CountsZero()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 5f,
                                    range: 1f, defenderTag: true);
            _world.AddBuffer<DcTriggerSlot>(defender).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 5, counter = 0,
                payload = DcPayloadKind.ProjectileToTarget, patternIndex = -1,
            });
            Target(Faction.Enemy, new SimVec3(9f, 0f, 0f), attackerTag: true); // 사거리 밖

            _sut.Run(_world);

            Assert.AreEqual(0, _world.GetBuffer<DcTriggerSlot>(defender)[0].counter,
                "유효 대상 없이 불발한 resolve 는 세지 않는다");
        }

        [Test]
        public void CastCountedHost_SkipsTheResolveCount()
        {
            // ⚠ attack-decoupling 계약 2 — host 당 사건 지점 하나. 캐스트로 이미 셌으면 RESOLVE 는 건너뛴다.
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 5f, defenderTag: true);
            _world.AddBuffer<DcTriggerSlot>(defender).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 5, counter = 0,
                payload = DcPayloadKind.ProjectileToTarget, patternIndex = -1,
            });
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);
            _channels.Cast.Enqueue(new CastEvent { caster = defender, casterPos = new SimVec3(0f, 0f, 0f) });

            _sut.Run(_world);

            Assert.AreEqual(1, _world.GetBuffer<DcTriggerSlot>(defender)[0].counter,
                "캐스트가 1 을 셌고 RESOLVE 는 더 세지 않는다 — 2 가 되면 카드 주기가 절반이 된다");
        }

        // ── payload 분기 ──────────────────────────────────────────────────────

        // ⚠ `CcKind` 에 `None` 이 없다 — 0 은 `Slow` 다. CC 가 아닌 payload 에선 이 인자가 무의미하다.
        private SimEntityId CardDefender(DcPayloadKind payload, CcKind ccKind = CcKind.Slow,
                                         StackKind stackKind = StackKind.None,
                                         float magnitude = 2f, float duration = 1.5f, int tileRange = 0)
        {
            var e = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 5f, defenderTag: true);
            _world.AddBuffer<DcTriggerSlot>(e).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 1, counter = 0,
                payload = payload, magnitude = magnitude, duration = duration,
                ccKind = ccKind, stackKind = stackKind, tileRange = tileRange, patternIndex = -1,
            });
            return e;
        }

        [Test]
        public void ApplyCcToTarget_HitsTheIntendedTarget()
        {
            CardDefender(DcPayloadKind.ApplyCcToTarget, ccKind: CcKind.Stun, duration: 1.5f);
            var enemy = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var cc = _channels.EnemyCc.Drain();
            Assert.AreEqual(1, cc.Count);
            Assert.AreEqual(enemy, cc[0].target);
            Assert.AreEqual(CcKind.Stun, cc[0].effect.kind);
            Assert.AreEqual(1.5f, cc[0].effect.remainingTime, 1e-4f);
        }

        [Test]
        public void ApplyCcToTarget_SuppressesAPhantomImpulse()
        {
            // 같은 셀이면 방향이 0 → 방향 없는 CC 를 내보내지 않는다(넉백 가드와 대칭).
            CardDefender(DcPayloadKind.ApplyCcToTarget, ccKind: CcKind.Impulse);
            Target(Faction.Enemy, new SimVec3(0f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.EnemyCc.Count);
        }

        [Test]
        public void ApplyStackToTarget_ClampsTheCountDelta()
        {
            // ⚠ 무경계 `(byte)` 캐스트는 256 → 0 wrap = 조용한 no-op 이다.
            CardDefender(DcPayloadKind.ApplyStackToTarget, stackKind: StackKind.Bleed,
                         magnitude: 900f, tileRange: 7);
            var enemy = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var evts = _channels.StackApply.Drain();
            Assert.AreEqual(1, evts.Count);
            Assert.AreEqual(enemy, evts[0].target);
            Assert.AreEqual(255, evts[0].countDelta, "상한으로 clamp — wrap 되면 0 이 되어 사라진다");
            Assert.AreEqual(7, evts[0].maxStack, "cap 은 카드 저작(tileRange 재사용)");
        }

        [Test]
        public void HeavyStrikePayload_DoesNotWarn()
        {
            // 강공은 pre-scan 이 이미 처리했다 — 이 분기는 **unhandled 경고에 걸리지 않기 위함**이다.
            CardDefender(DcPayloadKind.HeavyStrike, magnitude: 2f);
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.Warnings.Count);
            Assert.AreEqual(1, _channels.DcTriggerFired.Count, "그래도 발동 신호는 나간다");
        }

        [Test]
        public void UnhandledPayload_WarnsWithTheResolveCode()
        {
            CardDefender(DcPayloadKind.SelfBlink);
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var warnings = _channels.Warnings.Drain();
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(SimWarningCode.ResolveUnhandledPayload, warnings[0].code);
            Assert.AreEqual((int)DcPayloadKind.SelfBlink, warnings[0].detail);
        }

        [Test]
        public void Gate_BlocksTheCount_WithoutConsumingIt()
        {
            // 게이트 실패 사건은 counter 를 움직이지 않는다(카운트 게이트) — 다음 통과 공격이
            // 같은 카운트로 재도전한다.
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 1f,
                                    cooldown: 0.001f, defenderTag: true);
            _world.AddBuffer<DcTriggerSlot>(defender).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 2, counter = 0,
                payload = DcPayloadKind.ProjectileToTarget, patternIndex = -1,
                gate = DcGateKind.HpBelow, gateSubject = DcGateSubject.EventTarget, gateValue = 0.3f,
            });
            var enemy = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), hp: 10f, max: 10f, attackerTag: true);

            _sut.Run(_world);
            Assert.AreEqual(0, _world.GetBuffer<DcTriggerSlot>(defender)[0].counter, "만피 → 게이트 실패");

            _world.Set(enemy, new Health { value = 2f, max = 10f }); // 20% → 통과
            _sut.Run(_world);
            Assert.AreEqual(1, _world.GetBuffer<DcTriggerSlot>(defender)[0].counter);
        }

        // ── 넉업 · 위협 · 어그로 ──────────────────────────────────────────────

        [Test]
        public void Knockup_AppliesToEveryHitTarget_AndSignalsTheView()
        {
            // ⚠ 넉백·수면(주 타겟 1체)과 **스코프가 다르다**.
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 3f,
                                    attackTargetCount: 2, defenderTag: true);
            _world.Set(defender, new DefenderCcData { knockupOnHitSec = 0.4f, knockupVisualHeight = 1.5f });
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);
            Target(Faction.Enemy, new SimVec3(2f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var cc = _channels.EnemyCc.Drain();
            Assert.AreEqual(2, cc.Count, "히트한 전 대상");
            Assert.AreEqual(CcKind.Stun, cc[0].effect.kind, "심에서 넉업의 실체는 짧은 Stun 이다");
            var visual = _channels.KnockupVisual.Drain();
            Assert.AreEqual(2, visual.Count);
            Assert.AreEqual(0.4f, visual[0].durationSec, 1e-4f, "떠 있는 시간 = 스턴 시간");
            Assert.AreEqual(1.5f, visual[0].height, 1e-4f);
        }

        [Test]
        public void Knockup_SkipsBosses_ForBothCcAndVisual()
        {
            // ⚠ 연출만 나가면 떠오르는데 스턴은 안 걸리는 desync 가 된다 — **함께** 건너뛴다.
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 3f, defenderTag: true);
            _world.Set(defender, new DefenderCcData { knockupOnHitSec = 0.4f });
            _world.Set(Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true), new BossTag());

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.EnemyCc.Count);
            Assert.AreEqual(0, _channels.KnockupVisual.Count);
        }

        [Test]
        public void ThreatCredit_MatchesTheDamage_AndIsDefenderOnly()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 10f, defenderTag: true);
            _world.Set(defender, new ModifierStats { damageMul = 2f, attackSpeedMul = 1f, damageVsCcMul = 1f });
            var boss = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);
            _world.AddBuffer<ThreatEntry>(boss);

            _sut.Run(_world);

            var threat = _channels.ThreatHit.Drain();
            Assert.AreEqual(1, threat.Count);
            Assert.AreEqual(boss, threat[0].victim);
            Assert.AreEqual(defender, threat[0].attacker);
            Assert.AreEqual(Damage(boss)[0].amount, threat[0].amount, 1e-4f,
                "같은 값이어야 한다 — 갈리면 어그로가 desync 된다");
        }

        [Test]
        public void ThreatCredit_RequiresTheVictimBuffer()
        {
            Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 10f, defenderTag: true);
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true); // ThreatEntry 없음

            _sut.Run(_world);

            Assert.AreEqual(0, _channels.ThreatHit.Count, "보스가 아닌 적은 위협을 누적하지 않는다");
        }

        [Test]
        public void Guardian_PrefersFreshEnemies_AndEmitsAggroHits()
        {
            var guardian = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 3f,
                                    attackTargetCount: 1, defenderTag: true);
            _world.Set(guardian, new AggroCapacity { held = 0, max = 3 });
            var alreadyAggroed = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);
            _world.Set(alreadyAggroed, new Aggroed { guardian = guardian });
            var fresh = Target(Faction.Enemy, new SimVec3(3f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, DamageCount(fresh), "여유가 있으면 아직 안 끌린 적을 우선 때린다");
            Assert.AreEqual(0, DamageCount(alreadyAggroed));
            var hits = _channels.AggroHit.Drain();
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(guardian, hits[0].guardian);
            Assert.AreEqual(fresh, hits[0].enemy);
        }

        [Test]
        public void Guardian_AtCapacity_FallsBackToNearest()
        {
            var guardian = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), damage: 3f,
                                    attackTargetCount: 1, defenderTag: true);
            _world.Set(guardian, new AggroCapacity { held = 3, max = 3 });
            var near = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f), attackerTag: true);
            _world.Set(near, new Aggroed { guardian = guardian });
            Target(Faction.Enemy, new SimVec3(3f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, DamageCount(near), "상한이 차면 겹친 팩을 정리한다(일반 최근접)");
        }

        [Test]
        public void AggroedAttacker_IsForcedToSingleTarget()
        {
            // ⚠ AoE 후속이 다른 방어유닛을 끌어오지 못하게 강제한다.
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), damage: 3f,
                                 targetMask: (int)Faction.Defender, attackTargetCount: 3, attackerTag: true);
            var guardian = Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true);
            _world.Set(enemy, new Aggroed { guardian = guardian });
            var other = Target(Faction.Defender, new SimVec3(2f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);

            Assert.AreEqual(1, DamageCount(guardian));
            Assert.AreEqual(0, DamageCount(other), "어그로 걸린 적은 가디언만 때린다");
        }

        // ── 최전방 보너스 ─────────────────────────────────────────────────────

        [Test]
        public void FrontmostBonus_AppliesOnlyToTheLockedPrimary()
        {
            var dist = new int[16 * 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    dist[y * 16 + x] = 16 - x;
            var f = _world.Create();
            _world.Set(f, new FlowFieldSingleton
            {
                flow = new SimVec2[16 * 16], dist = dist,
                gridSize = new SimInt2(16, 16), tileSize = 1f, origin = default,
            });

            var defender = Attacker(Faction.Defender, new SimVec3(3f, 0f, 3f), damage: 10f,
                                    attackTargetCount: 2, defenderTag: true);
            _world.Set(defender, new FrontmostAttackLock());
            _world.AddBuffer<DcAttackModSlot>(defender).Add(new DcAttackModSlot
            {
                kind = DcAttackModKind.FrontmostTarget, damageMul = 2f,
            });
            var behind = Target(Faction.Enemy, new SimVec3(2f, 0f, 3f), attackerTag: true); // dist 14
            var ahead = Target(Faction.Enemy, new SimVec3(5f, 0f, 3f), attackerTag: true);  // dist 11

            _sut.Run(_world);

            Assert.AreEqual(20f, Damage(ahead)[0].amount, 1e-4f, "잠근 최전방만 배율을 받는다");
            Assert.AreEqual(10f, Damage(behind)[0].amount, 1e-4f, "부수 대상은 기본값");
        }
    }
}
