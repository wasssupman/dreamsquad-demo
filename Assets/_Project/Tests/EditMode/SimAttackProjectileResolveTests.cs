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
    /// battle-sim-extraction unit 18-I/2 arm E — RESOLVE 의 **투사체 경로**.
    ///
    /// 어서션 복제 출처(구 `AttackSystemUnifiedLoopTests`):
    /// `U1_Defender_ProjectileRef_Produces_SpawnRequest_Not_Direct_Damage` ·
    /// `Ballistic_ProjectileRef_Stages_Ballistic_Request_With_Locked_Impact`.
    ///
    /// ⚠ 근접/Outputs 경로(구 `1104-1617`)는 **arm F** 다 — 여기서 `ProjectileRef` 없는 공격자는
    /// 아직 아무것도 하지 않는다.
    /// </summary>
    public class SimAttackProjectileResolveTests
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

        private SimEntityId Shooter(SimVec3 pos, float damage, ProjectileRef projRef,
                                    float range = 10f, float cooldown = 1f, bool defenderTag = true,
                                    float hitDelaySec = 0f)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new FactionTag { value = defenderTag ? Faction.Defender : Faction.Enemy });
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, new AttackState
            {
                range = range, cooldownDuration = cooldown, cooldownRemaining = 0f,
                attackTargetCount = 1, hitDelaySec = hitDelaySec,
                targetMask = defenderTag ? (int)Faction.Enemy : (int)Faction.Defender,
            });
            if (defenderTag) _world.Set(e, new DefenderUnitTag());
            else _world.Set(e, new AttackUnitTag());
            _world.Set(e, projRef);
            if (damage > 0f)
                _world.AddBuffer<AttackOutputElement>(e).Add(new AttackOutputElement
                {
                    value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = damage },
                });
            return e;
        }

        private SimEntityId Enemy(SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new FactionTag { value = Faction.Enemy });
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, new AttackUnitTag());
            return e;
        }

        private ProjectileSpawnRequest Request(SimEntityId e)
        {
            Assert.IsTrue(_world.Has<ProjectileSpawnRequest>(e), "발사 요청이 있어야 한다");
            return _world.Get<ProjectileSpawnRequest>(e);
        }

        // ── 구 오라클 복제 ────────────────────────────────────────────────────

        [Test]
        public void U1_ProjectileRef_StagesASpawnRequest_NotDirectDamage()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 5f, projRef: new ProjectileRef
            {
                speed = 10f, hitThreshold = 0.3f, visualScale = 1f, dataIndex = 0,
                splashRadius = 0f, splashDamageMul = 1f,
            });
            var enemy = Enemy(new SimVec3(2f, 0f, 0f));

            _sut.Run(_world);

            var req = Request(defender);
            Assert.AreEqual(MovementKind.HomingToEntity, req.movement,
                "기본 `ProjectileRef`(movement=0)는 호밍 요청이다");
            Assert.AreEqual(5f, req.damage, 1e-4f, "Damage output 합을 투사체 피해로 스냅샷");
            Assert.AreEqual(enemy, req.target);
            Assert.AreEqual(defender, req.owner, "위협 귀속");
            Assert.IsNull(_world.GetBuffer<IncomingDamage>(enemy),
                "투사체 경로는 직접 피해를 넣지 않는다");
        }

        [Test]
        public void Ballistic_LocksTheImpactCell()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 7f, projRef: new ProjectileRef
            {
                speed = 10f, visualScale = 1f, dataIndex = 0,
                movement = MovementKind.BallisticArcToPoint, payload = PayloadKind.TileAoe,
                arcHeight = 2f, impactTileRange = 1,
            });
            var enemy = Enemy(new SimVec3(3f, 0f, 0f));

            _sut.Run(_world);

            var req = Request(defender);
            Assert.AreEqual(MovementKind.BallisticArcToPoint, req.movement, "탄도, 호밍 아님");
            Assert.AreEqual(PayloadKind.TileAoe, req.payload);
            Assert.AreEqual(SimEntityId.Null, req.target, "탄도는 대상 엔티티를 추적하지 않는다");
            Assert.AreEqual(7f, req.damage, 1e-4f, "Damage output 합");
            Assert.AreEqual(1, req.impactTileRange);
            Assert.AreEqual(2f, req.arcHeight, 1e-4f);
            Assert.Greater(req.impact.x, 2f, "착탄점이 대상 셀에 고정된다(사수 자리가 아니다)");
            Assert.Less(req.impact.x, 4f);
            Assert.IsNull(_world.GetBuffer<IncomingDamage>(enemy), "발사 프레임엔 직접 피해가 없다");
        }

        // ── 산출물 스냅샷 ─────────────────────────────────────────────────────

        [Test]
        public void OutputsAreSnapshotOntoTheRequest_WithDamageMulApplied()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 10f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.Set(defender, new ModifierStats { damageMul = 3f, attackSpeedMul = 1f, damageVsCcMul = 1f });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(30f, Request(defender).damage, 1e-4f, "damageMul 이 곱해진다");
            var outs = _world.GetBuffer<ProjectileSpawnOutputElement>(defender);
            Assert.AreEqual(1, outs.Count, "산출물이 요청에 함께 실린다");
            Assert.AreEqual(30f, outs[0].value.magnitude, 1e-4f, "실린 값도 배율 적용 후");
        }

        [Test]
        public void OutputBuffer_IsReplaced_NotAppended()
        {
            // ⚠ 구 `ecb.AddBuffer` 의 **교체** 의미를 유지한다 — 누적되면 두 번째 발사부터
            //   산출물이 배로 늘어난다.
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 4f,
                                   projRef: new ProjectileRef { speed = 10f }, cooldown: 0.001f);
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);
            _sut.Run(_world);

            Assert.AreEqual(1, _world.GetBuffer<ProjectileSpawnOutputElement>(defender).Count);
        }

        [Test]
        public void ShatterHymn_MultipliesWhenTheIntendedTargetIsCcd()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 10f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.Set(defender, new ModifierStats { damageMul = 1f, attackSpeedMul = 1f, damageVsCcMul = 2f });
            var enemy = Enemy(new SimVec3(1f, 0f, 0f));
            _world.AddBuffer<CcEffect>(enemy).Add(new CcEffect { kind = CcKind.Stun, remainingTime = 1f });

            _sut.Run(_world);

            Assert.AreEqual(20f, Request(defender).damage, 1e-4f,
                "발사 시점 의도 대상이 CC 상태면 배율이 붙는다");
        }

        [Test]
        public void ShatterHymn_DoesNotApply_WhenTheCcHasExpired()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 10f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.Set(defender, new ModifierStats { damageMul = 1f, attackSpeedMul = 1f, damageVsCcMul = 2f });
            var enemy = Enemy(new SimVec3(1f, 0f, 0f));
            _world.AddBuffer<CcEffect>(enemy).Add(new CcEffect { kind = CcKind.Stun, remainingTime = 0f });

            _sut.Run(_world);

            Assert.AreEqual(10f, Request(defender).damage, 1e-4f, "남은 시간 0 은 활성 CC 가 아니다");
        }

        [Test]
        public void AttackOutputLog_RecordsThePostMultiplierAmount()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 10f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.Set(defender, new ModifierStats { damageMul = 2f, attackSpeedMul = 1f, damageVsCcMul = 1f });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            var log = _channels.AttackOutputLog.Drain();
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(defender, log[0].attacker);
            Assert.AreEqual(20f, log[0].magnitude, 1e-4f);
        }

        // ── 방향 직선 ─────────────────────────────────────────────────────────

        [Test]
        public void Directional_FiresAlongFacing_AndCarriesNoTarget()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f, range: 5f,
                                   projRef: new ProjectileRef
                                   {
                                       speed = 10f, movement = MovementKind.DirectionalLinear,
                                       hitThreshold = 0.3f,
                                   });
            _world.Set(defender, new DeployedFacing { value = new SimInt2(1, 0) });
            Enemy(new SimVec3(3f, 0f, 0f));

            _sut.Run(_world);

            var req = Request(defender);
            Assert.AreEqual(MovementKind.DirectionalLinear, req.movement);
            Assert.AreEqual(SimEntityId.Null, req.target, "경로에 있는 것을 맞히는 탄 — 대상을 싣지 않는다");
            Assert.AreEqual(1f, req.direction.x, 1e-4f);
            Assert.AreEqual(0f, req.direction.y, 1e-4f);
            Assert.AreEqual(5f, req.maxDistance, 1e-4f, "레인 게이트와 같은 타일 단위 환산");
        }

        [Test]
        public void Directional_WithoutFacing_UsesTheCommittedDirection_EvenAfterTheWitnessIsGone()
        {
            // START 에서 얼린 기준축으로 완주한다 — wind-up 중 witness 가 사라져도 취소되지 않는다.
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f, range: 5f, hitDelaySec: 0.01f,
                                   projRef: new ProjectileRef
                                   {
                                       speed = 10f, movement = MovementKind.DirectionalLinear,
                                   });
            var enemy = Enemy(new SimVec3(3f, 0f, 0f));

            _sut.Run(_world); // START — 방향 스냅샷
            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(defender), "지연 중엔 아직 발사가 없다");

            _world.Destroy(enemy); // witness 소실
            _sut.Run(_world);      // 지연 만료 → RESOLVE

            var req = Request(defender);
            Assert.AreEqual(1f, req.direction.x, 1e-4f, "얼린 +X 방향으로 완주");
            Assert.AreEqual(0f, req.direction.y, 1e-4f);
        }

        [Test]
        public void Facing_Directional_CompletesWithoutAWitness()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f, range: 4f, hitDelaySec: 0.01f,
                                   projRef: new ProjectileRef
                                   {
                                       speed = 10f, movement = MovementKind.DirectionalLinear,
                                   });
            _world.Set(defender, new DeployedFacing { value = new SimInt2(0, 1) });
            var enemy = Enemy(new SimVec3(0f, 0f, 2f));

            _sut.Run(_world);
            _world.Destroy(enemy);
            _sut.Run(_world);

            var req = Request(defender);
            Assert.AreEqual(0f, req.direction.x, 1e-4f);
            Assert.AreEqual(1f, req.direction.y, 1e-4f, "고정 facing 으로 발사한다");
        }

        [Test]
        public void BounceMods_AreAggregated_CountSum_RangeMax_MulProduct()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                   projRef: new ProjectileRef { speed = 10f });
            var mods = _world.AddBuffer<DcAttackModSlot>(defender);
            mods.Add(new DcAttackModSlot { kind = DcAttackModKind.ProjectileBounce, count = 1, tileRange = 3, damageMul = 0.5f });
            mods.Add(new DcAttackModSlot { kind = DcAttackModKind.ProjectileBounce, count = 2, tileRange = 5, damageMul = 0.8f });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            var req = Request(defender);
            Assert.AreEqual(3, req.bounceRemaining, "count 는 합");
            Assert.AreEqual(5, req.bounceTileRange, "range 는 max");
            Assert.AreEqual(0.4f, req.bounceDamageMul, 1e-4f, "mul 은 곱");
        }

        [Test]
        public void Ballistic_DoesNotCarryBounce()
        {
            // ⚠ 착탄 셀이 발사 시점에 고정돼 **재조준할 대상이 없다**.
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f, projRef: new ProjectileRef
            {
                speed = 10f, movement = MovementKind.BallisticArcToPoint,
            });
            _world.AddBuffer<DcAttackModSlot>(defender).Add(new DcAttackModSlot
            {
                kind = DcAttackModKind.ProjectileBounce, count = 2, tileRange = 4, damageMul = 0.5f,
            });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(0, Request(defender).bounceRemaining);
        }

        [Test]
        public void BounceMods_AreDefenderOnly()
        {
            var enemy = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                projRef: new ProjectileRef { speed = 10f }, defenderTag: false);
            _world.AddBuffer<DcAttackModSlot>(enemy).Add(new DcAttackModSlot
            {
                kind = DcAttackModKind.ProjectileBounce, count = 2, tileRange = 4, damageMul = 0.5f,
            });
            var target = _world.Create();
            _world.Set(target, SimTransform.FromPosition(new SimVec3(1f, 0f, 0f)));
            _world.Set(target, new FactionTag { value = Faction.Defender });
            _world.Set(target, new Health { value = 10f, max = 10f });
            _world.Set(target, new DefenderUnitTag());

            _sut.Run(_world);

            Assert.AreEqual(0, Request(enemy).bounceRemaining);
        }

        // ── 최전방 · 강공 배율 ────────────────────────────────────────────────

        [Test]
        public void FrontmostPriority_RidesOnTheRequest_OnlyForARealFrontmostPick()
        {
            // 흐름장이 없으면 최전방 후보가 없다 → 폴백 최근접이라 배율은 inert(0).
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.Set(defender, new FrontmostAttackLock());
            _world.AddBuffer<DcAttackModSlot>(defender).Add(new DcAttackModSlot
            {
                kind = DcAttackModKind.FrontmostTarget, damageMul = 1.2f,
            });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(0f, Request(defender).priorityDamageMul, 1e-4f,
                "폴백 최근접은 배율 수령자가 아니다 — 0 = 보너스 없음");
            Assert.AreEqual(SimEntityId.Null, Request(defender).priorityTarget);
        }

        [Test]
        public void HeavyStrike_PreScan_PredictsTheNthAttack()
        {
            // ⚠ 비변이 peek(`WouldFire`)이라 counter 를 건드리지 않는다 — 쓰기 소유는 dc 루프(arm F).
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.AddBuffer<DcTriggerSlot>(defender).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 1, counter = 0,
                payload = DcPayloadKind.HeavyStrike, magnitude = 2.5f, patternIndex = -1,
            });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(2.5f, Request(defender).heavyDamageMul, 1e-4f, "이번 공격이 N 번째다");
            Assert.AreEqual(0, _world.GetBuffer<DcTriggerSlot>(defender)[0].counter,
                "pre-scan 은 카운터를 건드리지 않는다(arm F 의 루프가 소유)");
        }

        [Test]
        public void HeavyStrike_IsInert_WhenThisAttackIsNotTheNth()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.AddBuffer<DcTriggerSlot>(defender).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 5, counter = 0,
                payload = DcPayloadKind.HeavyStrike, magnitude = 2.5f, patternIndex = -1,
            });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(1f, Request(defender).heavyDamageMul, 1e-4f, "기본 1 = inert");
        }

        [Test]
        public void HeavyStrike_RespectsTheGate()
        {
            // 게이트 실패 = 이번 공격은 강공이 아니다. 아래 counter 루프도 안 오르므로
            // 다음 게이트 통과 공격이 같은 카운트로 재도전한다(합성 불변식).
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.AddBuffer<DcTriggerSlot>(defender).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 1, counter = 0,
                payload = DcPayloadKind.HeavyStrike, magnitude = 2.5f, patternIndex = -1,
                gate = DcGateKind.HpBelow, gateSubject = DcGateSubject.EventTarget, gateValue = 0.3f,
            });
            Enemy(new SimVec3(1f, 0f, 0f)); // 만피 → 게이트 실패

            _sut.Run(_world);

            Assert.AreEqual(1f, Request(defender).heavyDamageMul, 1e-4f, "게이트 실패 = 강공 아님");
        }

        [Test]
        public void HeavyStrike_IsDefenderOnly()
        {
            var enemy = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                projRef: new ProjectileRef { speed = 10f }, defenderTag: false);
            _world.AddBuffer<DcTriggerSlot>(enemy).Add(new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.AttackN, period = 1, counter = 0,
                payload = DcPayloadKind.HeavyStrike, magnitude = 2.5f, patternIndex = -1,
            });
            var target = _world.Create();
            _world.Set(target, SimTransform.FromPosition(new SimVec3(1f, 0f, 0f)));
            _world.Set(target, new FactionTag { value = Faction.Defender });
            _world.Set(target, new Health { value = 10f, max = 10f });
            _world.Set(target, new DefenderUnitTag());

            _sut.Run(_world);

            Assert.AreEqual(1f, Request(enemy).heavyDamageMul, 1e-4f);
        }

        // ── 발사 패턴 ─────────────────────────────────────────────────────────

        [Test]
        public void PatternDefender_PushesAnEmitterInstance_InsteadOfARequest()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 9f, range: 5f,
                                   projRef: new ProjectileRef
                                   {
                                       speed = 10f, movement = MovementKind.DirectionalLinear,
                                   });
            _world.Set(defender, new DeployedFacing { value = new SimInt2(1, 0) });
            _world.AddBuffer<PatternSlot>(defender).Add(new PatternSlot
            {
                spec = new PatternSpec
                {
                    shots = new[]
                    {
                        new PatternShotSpec { directionT = 0f },
                        new PatternShotSpec { directionT = 1f, intervalAfterPreviousSec = 0.1f },
                    },
                },
                template = new ProjectileSpawnRequest { movement = MovementKind.DirectionalLinear },
                fireCountBase = 0,
            });
            _world.AddBuffer<EmitterInstance>(defender);
            Enemy(new SimVec3(2f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(defender),
                "패턴 유닛은 요청을 직접 만들지 않는다 — 인스턴스 하나로 번역한다");
            var instances = _world.GetBuffer<EmitterInstance>(defender);
            Assert.AreEqual(1, instances.Count);
            Assert.AreEqual(9f, instances[0].spec.damage, 1e-4f,
                "패턴 저작의 damage 를 트리거 시점 실효값으로 덮어 전탄에 스냅샷한다");
            Assert.AreEqual(defender, instances[0].template.owner);
            Assert.AreEqual(2, _world.GetBuffer<PatternSlot>(defender)[0].fireCountBase,
                "fireCountBase 가 발수만큼 전진한다");
            Assert.Greater(_world.Get<AttackState>(defender).cooldownRemaining, 1f,
                "다음 트리거는 버스트가 끝난 뒤부터 기다린다");
        }

        [Test]
        public void PatternSeed_IsDerivedFromSimIdAndFireCount()
        {
            // ⚠ 시드가 `math.hash(int2)` 라 상수 하나만 달라도 랜덤 패턴이 통째로 갈린다.
            //   `SimMathParityTests.Hash_int2_가_비트까지_같다` 가 그 식을 박제한다.
            Assert.AreEqual(SimMath.Hash(new SimInt2(7, 3)), SimMath.Hash(new SimInt2(7, 3)));
            Assert.AreNotEqual(SimMath.Hash(new SimInt2(7, 3)), SimMath.Hash(new SimInt2(7, 4)),
                "같은 host 의 연속 트리거가 같은 시퀀스를 반복하지 않는다");
            Assert.AreNotEqual(SimMath.Hash(new SimInt2(7, 3)), SimMath.Hash(new SimInt2(8, 3)),
                "여러 host 가 같은 시퀀스를 반복하지 않는다");
        }

        // ── 경계 ──────────────────────────────────────────────────────────────

        [Test]
        public void ResolveEpilogue_ReleasesTheFrontmostLock_AndClearsTheCommittedDirection()
        {
            // ⚠ 본문이 어느 분기로 끝나든 에필로그를 지나야 한다. 안 그러면 잠금이 영구 활성으로
            //   남아 유닛이 죽은 대상을 계속 겨눈다.
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 6f,
                                   projRef: new ProjectileRef
                                   {
                                       speed = 10f, movement = MovementKind.DirectionalLinear,
                                   });
            _world.Set(defender, new FrontmostAttackLock());
            _world.AddBuffer<DcAttackModSlot>(defender).Add(new DcAttackModSlot
            {
                kind = DcAttackModKind.FrontmostTarget, damageMul = 1.2f,
            });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsFalse(_world.Get<FrontmostAttackLock>(defender).active,
                "해결된 공격은 잠금을 푼다 — 다음 공격이 현재 최전방을 다시 고른다");
            Assert.AreEqual(0, _world.Get<AttackState>(defender).hasCommittedDirection,
                "기준축은 이번 발사에만 유효하다");
        }

        [Test]
        public void NoOutputs_MeansNoRequest()
        {
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 0f,
                                   projRef: new ProjectileRef { speed = 10f });
            Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(defender),
                "산출물 버퍼가 없으면 RESOLVE 가 아무것도 만들지 않는다");
        }

        [Test]
        public void MeleeAttacker_IsUntouchedHere()
        {
            // ⚠ `ProjectileRef` 없는 경로는 **arm F** 다 — arm E 는 손대지 않는다.
            var defender = Shooter(new SimVec3(0f, 0f, 0f), damage: 5f,
                                   projRef: new ProjectileRef { speed = 10f });
            _world.RemoveComponent<ProjectileRef>(defender);
            var enemy = Enemy(new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.IsFalse(_world.Has<ProjectileSpawnRequest>(defender));
            Assert.IsNull(_world.GetBuffer<IncomingDamage>(enemy), "근접 피해는 arm F 가 넣는다");
        }
    }
}
