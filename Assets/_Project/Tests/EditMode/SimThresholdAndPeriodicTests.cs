using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/4 — 캡처 #42(체력 임계) · #4(주기 트리거) + 착지점 수학.
    ///
    /// 18-J 의 마지막 조각이고 **44 시스템 전부가 이식된다**.
    /// </summary>
    public class SimThresholdAndPeriodicTests
    {
        private SimWorld _world;
        private SimChannels _channels;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _world.SetDeltaTime(0.5f);
        }

        /// 전 칸 도달 가능한 8×8 흐름장.
        private void FlowField(int w = 8, int h = 8)
        {
            var e = _world.Create();
            _world.Set(e, new FlowFieldSingleton
            {
                flow = new SimVec2[w * h], dist = new int[w * h],
                gridSize = new SimInt2(w, h), tileSize = 1f, origin = default,
            });
        }

        private SimEntityId Host(SimVec3 pos, float hp = 100f, float max = 100f,
                                 bool enemy = false, bool defender = false)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new Health { value = hp, max = max });
            if (enemy) { _world.Set(e, new AttackUnitTag()); _world.Set(e, new FactionTag { value = Faction.Enemy }); }
            if (defender) { _world.Set(e, new DefenderUnitTag()); _world.Set(e, new FactionTag { value = Faction.Defender }); }
            _world.AddBuffer<DcTriggerSlot>(e);
            return e;
        }

        /// ⚠ `patternIndex` 를 여기서 손보지 않는다 — 0 은 **유효 index** 라 덮어쓰면
        /// 패턴 arm 이 조용히 no-op 이 된다(팩토리들이 이미 -1 을 기본값으로 넣는다).
        private void Slot(SimEntityId host, DcTriggerSlot slot)
            => _world.GetBuffer<DcTriggerSlot>(host).Add(slot);

        private static DcTriggerSlot Threshold(DcPayloadKind payload, float fraction = 0.5f,
                                               float maxHpRef = 100f, float magnitude = 1.35f,
                                               float duration = 0f, int tileRange = 3)
            => new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.HealthThreshold,
                fraction = fraction, maxHpRef = maxHpRef, nextBoundaryIndex = 1,
                payload = payload, magnitude = magnitude, duration = duration,
                tileRange = tileRange, buffStat = StatKind.DamageMul, statBuffStackId = 9,
                projectileDataIndex = 5, slamDamage = 30f, slamTileRange = 2, patternIndex = -1,
            };

        private int CarrierCount()
        {
            int n = 0;
            foreach (var _ in _world.With<ProjectileRequestCarrier>()) n++;
            return n;
        }

        // ── #42 체력 임계 ─────────────────────────────────────────────────────

        [Test]
        public void Threshold_DrainsThreatIntoTheVictimTable()
        {
            FlowField();
            var boss = Host(new SimVec3(1f, 0f, 1f), enemy: true);
            _world.AddBuffer<ThreatEntry>(boss);
            var attacker = Host(new SimVec3(0f, 0f, 0f), defender: true);
            _channels.ThreatHit.Enqueue(new ThreatHitEvent { victim = boss, attacker = attacker, amount = 12f });
            _channels.ThreatHit.Enqueue(new ThreatHitEvent { victim = boss, attacker = attacker, amount = 8f });

            new HealthThresholdSystem(_channels).Run(_world);

            var table = _world.GetBuffer<ThreatEntry>(boss);
            Assert.AreEqual(1, table.Count, "공격자당 한 줄로 접는다");
            Assert.AreEqual(20f, table[0].cumulativeDamage, 1e-4f);
        }

        [Test]
        public void Threshold_DropsThreatForAVictimWithoutATable()
        {
            FlowField();
            var plain = Host(new SimVec3(1f, 0f, 1f), enemy: true);
            _channels.ThreatHit.Enqueue(new ThreatHitEvent { victim = plain, attacker = plain, amount = 5f });

            Assert.DoesNotThrow(() => new HealthThresholdSystem(_channels).Run(_world));
        }

        [Test]
        public void Threshold_SelfStatBuff_IsPermanentWhenDurationIsUnset()
        {
            FlowField();
            var host = Host(new SimVec3(1f, 0f, 1f), hp: 40f, defender: true);
            Slot(host, Threshold(DcPayloadKind.SelfStatBuff, magnitude: 1.5f, duration: 0f));

            new HealthThresholdSystem(_channels).Run(_world);

            var mods = _channels.StatApply.Drain();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(host, mods[0].target);
            Assert.AreEqual(float.PositiveInfinity, mods[0].duration, "duration <= 0 = 영구");
            Assert.AreEqual(StatKind.DamageMul, mods[0].stat);
            Assert.AreEqual(9, mods[0].stackId);
            Assert.AreEqual(ModifierOrigin.HealthThreshold, mods[0].origin);
            SimModifierAuthoring.FromMultiplier(1.5f, out var op, out float mag);
            Assert.AreEqual(op, mods[0].op, "+% 는 Additive 버킷");
            Assert.AreEqual(mag, mods[0].magnitude, 1e-4f);
        }

        [Test]
        public void Threshold_DoesNotFireAboveTheBoundary()
        {
            FlowField();
            var host = Host(new SimVec3(1f, 0f, 1f), hp: 80f, defender: true);
            Slot(host, Threshold(DcPayloadKind.SelfStatBuff, fraction: 0.5f));

            new HealthThresholdSystem(_channels).Run(_world);

            Assert.AreEqual(0, _channels.StatApply.Count, "50% 경계 위다");
        }

        [Test]
        public void Threshold_SkipsDeadHosts()
        {
            // ⚠ `DeadTag` 는 #34 가 붙이므로 **죽는 프레임에 이미** 붙어 있다 — 오버킬로 여러
            //   경계를 관통해도 시체가 마지막 경계에서 폭발하지 않는다.
            FlowField();
            var host = Host(new SimVec3(1f, 0f, 1f), hp: 1f, defender: true);
            _world.Set(host, new DeadTag());
            Slot(host, Threshold(DcPayloadKind.SelfStatBuff));

            new HealthThresholdSystem(_channels).Run(_world);

            Assert.AreEqual(0, _channels.StatApply.Count);
        }

        [Test]
        public void Threshold_SelfTileAoe_DerivesTheDamagePoolFromTheHostFaction()
        {
            // ⚠ 기본값이 Enemy 라 그냥 두면 **보스의 폭발이 자기 진영을 때린다**.
            FlowField();
            var boss = Host(new SimVec3(2f, 0f, 2f), hp: 40f, enemy: true);
            Slot(boss, Threshold(DcPayloadKind.SelfTileAoe, magnitude: 25f));

            new HealthThresholdSystem(_channels).Run(_world);

            Assert.AreEqual(1, CarrierCount());
            foreach (var c in _world.With<ProjectileRequestCarrier>())
            {
                var req = _world.Get<ProjectileSpawnRequest>(c);
                Assert.AreEqual(ProjectileTargetFaction.Defender, req.targetFaction, "적 host → 방어유닛을 때린다");
                Assert.AreEqual(boss, req.owner, "폭발 킬은 이 유닛에 귀속");
                Assert.AreEqual(new SimVec3(2f, 0f, 2f), req.impact);
                Assert.AreEqual(25f, req.damage, 1e-4f);
            }
        }

        [Test]
        public void Threshold_SelfTileAoe_DefenderHostKeepsHittingEnemies()
        {
            FlowField();
            var defender = Host(new SimVec3(2f, 0f, 2f), hp: 40f, defender: true);
            Slot(defender, Threshold(DcPayloadKind.SelfTileAoe));

            new HealthThresholdSystem(_channels).Run(_world);

            foreach (var c in _world.With<ProjectileRequestCarrier>())
                Assert.AreEqual(ProjectileTargetFaction.Enemy, _world.Get<ProjectileSpawnRequest>(c).targetFaction);
        }

        [Test]
        public void Threshold_BossLeap_TargetsTheDensestDefenderCell()
        {
            FlowField();
            var boss = Host(new SimVec3(7f, 0f, 7f), hp: 40f, enemy: true);
            Slot(boss, Threshold(DcPayloadKind.SelfBlink, magnitude: 1f, tileRange: 3));
            Host(new SimVec3(2f, 0f, 2f), defender: true);
            Host(new SimVec3(2f, 0f, 2f), defender: true); // 밀집
            Host(new SimVec3(6f, 0f, 0f), defender: true);

            new HealthThresholdSystem(_channels).Run(_world);

            var blinks = _channels.BlinkRequest.Drain();
            Assert.AreEqual(1, blinks.Count);
            Assert.AreEqual(boss, blinks[0].entity);
            Assert.AreEqual(new SimVec3(2f, 0f, 2f), blinks[0].destWorld, "밀집 셀로 뛴다");

            var leaps = _channels.BossLeapVisual.Drain();
            Assert.AreEqual(1, leaps.Count, "뷰가 아치로 날린다 — 퍼프 타이밍도 이 채널이 소유");
            Assert.AreEqual(new SimVec3(7f, 0f, 7f), leaps[0].fromWorld);
            Assert.AreEqual(new SimVec3(2f, 0f, 2f), leaps[0].toWorld);
            Assert.AreEqual(30f, leaps[0].slamDamage, 1e-4f);
        }

        [Test]
        public void Threshold_BossLeap_SkipsWhenNoDefendersRemain()
        {
            FlowField();
            var boss = Host(new SimVec3(7f, 0f, 7f), hp: 40f, enemy: true);
            Slot(boss, Threshold(DcPayloadKind.SelfBlink));

            new HealthThresholdSystem(_channels).Run(_world);

            Assert.AreEqual(0, _channels.BlinkRequest.Count);
            Assert.AreEqual(2, _world.GetBuffer<DcTriggerSlot>(boss)[0].nextBoundaryIndex,
                "래치는 이미 전진했다 — 재발동이 없다");
        }

        [Test]
        public void Threshold_UltimateLeap_PinsTheLandingCellAndLocksTheUnit()
        {
            // ⚠ **예고는 약속이다** — 착지 직전 재계산하면 회피 플레이가 거짓말이 된다.
            FlowField();
            var boss = Host(new SimVec3(7f, 0f, 7f), hp: 40f, enemy: true);
            Slot(boss, Threshold(DcPayloadKind.UltimateLeap, magnitude: 1f, duration: 2f));
            Host(new SimVec3(3f, 0f, 3f), defender: true);

            new HealthThresholdSystem(_channels).Run(_world);

            var leap = _world.Get<UltimateLeapState>(boss);
            Assert.AreEqual(2f, leap.remaining, 1e-4f);
            Assert.AreEqual(new SimInt2(3, 3), leap.landingCell);
            Assert.AreEqual(new SimVec3(3f, 0f, 3f), leap.landingWorld);
            Assert.AreEqual(30f, leap.slamDamage, 1e-4f);
            Assert.IsTrue(_world.Has<LeapFlight>(boss), "잠금과 무적은 함께 붙는다");

            var vis = _channels.UltimateLeapVisual.Drain();
            Assert.AreEqual(UltimateLeapVisualKind.Ascend, vis[0].kind);
        }

        [Test]
        public void Threshold_UltimateLeap_WarnsLoudlyWhenTheLandingFails()
        {
            // ⚠ 생존당 1회라 **재시도가 없다** — 조용히 넘기면 원인을 영영 알 수 없다.
            FlowField();
            var boss = Host(new SimVec3(7f, 0f, 7f), hp: 40f, enemy: true);
            Slot(boss, Threshold(DcPayloadKind.UltimateLeap, duration: 2f));

            new HealthThresholdSystem(_channels).Run(_world);

            Assert.IsFalse(_world.Has<UltimateLeapState>(boss));
            var warnings = _channels.Warnings.Drain();
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(SimWarningCode.UltimateLeapNoLanding, warnings[0].code);
        }

        [Test]
        public void Threshold_UnhandledPayload_Warns()
        {
            FlowField();
            var host = Host(new SimVec3(1f, 0f, 1f), hp: 40f, defender: true);
            Slot(host, Threshold(DcPayloadKind.DreamCocoon));

            new HealthThresholdSystem(_channels).Run(_world);

            var warnings = _channels.Warnings.Drain();
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(SimWarningCode.HealthThresholdUnhandledPayload, warnings[0].code);
        }

        // ── 착지점 수학 ───────────────────────────────────────────────────────

        [Test]
        public void Density_TieBreaksByRowMajorCellKey()
        {
            // ⚠ 순회 순서에 의존하면 같은 배치에서 프레임마다 다른 셀이 뽑힌다.
            var cells = new List<SimInt2> { new SimInt2(5, 1), new SimInt2(2, 1) };
            Assert.IsTrue(DefenderDensity.TryFindDensestCell(cells, 0, new SimInt2(8, 8), out var densest, out _));
            Assert.AreEqual(new SimInt2(2, 1), densest, "동점이면 낮은 row-major 키");
        }

        [Test]
        public void Density_FailsWithNoCandidates()
        {
            Assert.IsFalse(DefenderDensity.TryFindDensestCell(new List<SimInt2>(), 1, new SimInt2(8, 8), out _, out _));
        }

        [Test]
        public void Landing_SnapsToTheNearestReachableCell()
        {
            var dist = new int[16];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            dist[2 * 4 + 1] = 3; // (1,2) 만 도달 가능

            Assert.IsTrue(BlinkMath.TryFindLandingCell(new SimInt2(1, 1), dist, new SimInt2(4, 4), 3, out var landing));
            Assert.AreEqual(new SimInt2(1, 2), landing);
        }

        [Test]
        public void Landing_FailsInsideTheRingCap()
        {
            var dist = new int[16];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;

            Assert.IsFalse(BlinkMath.TryFindLandingCell(new SimInt2(1, 1), dist, new SimInt2(4, 4), 2, out _),
                "봉인된 포켓에 떨어지지 않는다");
        }

        [Test]
        public void OffsetDest_FallsBackOnADegenerateDirection()
        {
            var dest = BlinkMath.OffsetDest(new SimVec3(3f, 0f, 3f), new SimVec3(3f, 0f, 3f), 1f);
            Assert.AreEqual(3f, dest.x, 1e-4f);
            Assert.AreEqual(2f, dest.z, 1e-4f, "상수 축(-Z)으로 폴백 — NaN 을 만들지 않는다");
        }

        // ── #4 주기 트리거 ────────────────────────────────────────────────────

        private static DcTriggerSlot Periodic(DcPayloadKind payload, float period = 1f,
                                              float magnitude = 20f, float duration = 3f,
                                              int tileRange = 2, int patternIndex = -1,
                                              int dataIndex = 5)
            => new DcTriggerSlot
            {
                instanceId = 1, trigger = DcTriggerKind.PeriodicTimer,
                periodSeconds = period, elapsed = 0f, payload = payload,
                magnitude = magnitude, duration = duration, tileRange = tileRange,
                patternIndex = patternIndex, projectileDataIndex = dataIndex,
            };

        [Test]
        public void Periodic_WhipPulse_BuffsSameFactionInRange_ExcludingTheHost()
        {
            FlowField();
            _world.SetDeltaTime(1f); // 주기 이상이어야 발동한다
            var boss = Host(new SimVec3(2f, 0f, 2f), enemy: true);
            Slot(boss, Periodic(DcPayloadKind.AllyMoveSpeedAura, magnitude: 20f, tileRange: 2));
            var nearAlly = Host(new SimVec3(3f, 0f, 2f), enemy: true);
            Host(new SimVec3(7f, 0f, 7f), enemy: true);   // 사거리 밖
            Host(new SimVec3(2f, 0f, 3f), defender: true); // 다른 진영

            new BossPeriodicTriggerSystem(_channels).Run(_world);

            var mods = _channels.StatApply.Drain();
            Assert.AreEqual(1, mods.Count, "host 자신과 사거리 밖·타 진영은 빠진다");
            Assert.AreEqual(nearAlly, mods[0].target);
            Assert.AreEqual(StatKind.MoveSpeedMul, mods[0].stat);
            Assert.AreEqual(1.2f, mods[0].magnitude, 1e-4f, "1 + magnitude/100");
            Assert.AreEqual(3f, mods[0].duration, 1e-4f, "해제는 TTL 만료뿐이다");
            Assert.AreEqual(ModifierOrigin.Boss, mods[0].origin);

            Assert.AreEqual(1, _channels.ProjectileHit.Count, "버프가 나간 펄스만 연출한다");
        }

        [Test]
        public void Periodic_WhipPulse_NoVisualWhenNothingWasBuffed()
        {
            FlowField();
            _world.SetDeltaTime(1f);
            var boss = Host(new SimVec3(2f, 0f, 2f), enemy: true);
            Slot(boss, Periodic(DcPayloadKind.AllyMoveSpeedAura, tileRange: 1));

            new BossPeriodicTriggerSystem(_channels).Run(_world);

            Assert.AreEqual(0, _channels.StatApply.Count);
            Assert.AreEqual(0, _channels.ProjectileHit.Count, "효과 없는 연출 금지");
        }

        [Test]
        public void Periodic_DegenerateAuthoring_ConsumesTheFireQuietly()
        {
            FlowField();
            _world.SetDeltaTime(1f);
            var boss = Host(new SimVec3(2f, 0f, 2f), enemy: true);
            Slot(boss, Periodic(DcPayloadKind.AllyMoveSpeedAura, magnitude: 0f));
            Host(new SimVec3(3f, 0f, 2f), enemy: true);

            new BossPeriodicTriggerSystem(_channels).Run(_world);

            Assert.AreEqual(0, _channels.StatApply.Count);
            Assert.AreEqual(0, _channels.Warnings.Count, "퇴화 저작은 경고가 아니라 조용한 소모다");
        }

        [Test]
        public void Periodic_DoesNotFireBeforeThePeriod()
        {
            FlowField();
            var boss = Host(new SimVec3(2f, 0f, 2f), enemy: true);
            Slot(boss, Periodic(DcPayloadKind.AllyMoveSpeedAura, period: 2f));
            Host(new SimVec3(3f, 0f, 2f), enemy: true);

            new BossPeriodicTriggerSystem(_channels).Run(_world); // dt 0.5

            Assert.AreEqual(0, _channels.StatApply.Count);
            Assert.AreEqual(0.5f, _world.GetBuffer<DcTriggerSlot>(boss)[0].elapsed, 1e-4f, "누산기는 이월된다");
        }

        [Test]
        public void Periodic_SkipsDeadHosts()
        {
            FlowField();
            _world.SetDeltaTime(1f);
            var boss = Host(new SimVec3(2f, 0f, 2f), enemy: true);
            _world.Set(boss, new DeadTag());
            Slot(boss, Periodic(DcPayloadKind.AllyMoveSpeedAura));
            Host(new SimVec3(3f, 0f, 2f), enemy: true);

            new BossPeriodicTriggerSystem(_channels).Run(_world);

            Assert.AreEqual(0, _channels.StatApply.Count, "시체가 한 번 더 스킬을 쓰지 않는다");
        }

        [Test]
        public void Periodic_PushesAnEmitterInstance_AndAdvancesTheFireCounter()
        {
            FlowField();
            _world.SetDeltaTime(1f);
            var boss = Host(new SimVec3(2f, 0f, 2f), enemy: true);
            Slot(boss, Periodic(DcPayloadKind.EmitProjectilePattern, patternIndex: 0));
            _world.AddBuffer<PatternSlot>(boss).Add(new PatternSlot
            {
                spec = new PatternSpec
                {
                    shots = new[] { new PatternShotSpec(), new PatternShotSpec(), new PatternShotSpec() },
                },
                template = new ProjectileSpawnRequest { movement = MovementKind.DirectionalLinear },
                fireCountBase = 4,
            });
            _world.AddBuffer<EmitterInstance>(boss);

            new BossPeriodicTriggerSystem(_channels).Run(_world);

            Assert.AreEqual(1, _world.GetBuffer<EmitterInstance>(boss).Count);
            Assert.AreEqual(7, _world.GetBuffer<PatternSlot>(boss)[0].fireCountBase,
                "발사 카운터만 durable 소유자에 남아 다음 발화가 이어받는다");
        }

        [Test]
        public void Periodic_UnhandledPayload_Warns()
        {
            FlowField();
            _world.SetDeltaTime(1f);
            var boss = Host(new SimVec3(2f, 0f, 2f), enemy: true);
            Slot(boss, Periodic(DcPayloadKind.AreaBarrage));

            new BossPeriodicTriggerSystem(_channels).Run(_world);

            var warnings = _channels.Warnings.Drain();
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(SimWarningCode.PeriodicUnhandledPayload, warnings[0].code,
                "arm 이 제거된 enum 값은 bake 가 거절하지만 sim 도 소리를 낸다");
        }

        [Test]
        public void Aura_ExcludesNothingByCell_TheCallerOwnsIdentity()
        {
            // 같은 셀 아군은 맞아야 하므로 host 제외는 신원 비교로만 한다.
            var cells = new List<SimInt2> { new SimInt2(2, 2), new SimInt2(2, 2), new SimInt2(9, 9) };
            var results = new List<int>();
            AuraPulse.SelectTargets(cells, new SimInt2(2, 2), 1, results);
            CollectionAssert.AreEqual(new[] { 0, 1 }, results);

            AuraPulse.SelectTargets(cells, new SimInt2(2, 2), -1, results);
            Assert.IsEmpty(results, "음수 반경은 아무것도 고르지 않는다");
        }

        // ── 44/44 ─────────────────────────────────────────────────────────────

        [Test]
        public void EveryCaptureNumberIsRegisteredExactlyOnce()
        {
            // 🎯 **18-J 의 완료 기준** — 44 시스템 전부가 파이프라인에 있다.
            //
            // ⚠ 18-K/5: 조립을 **여기서 하지 않는다.** 초판은 8 클러스터를 이 테스트가 직접
            //   모았는데, 그러면 프로덕션(`SimRuntime`)과 조립이 두 벌이 되어 이 단정이
            //   프로덕션을 증언하지 않는다 — A/B 가 서로 다른 파이프라인을 비교하게 되는
            //   바로 그 모양이다. 이제 조립 지점을 그대로 검사한다.
            var pipeline = new SimRuntime(new SimConfig(1u, 1u)).Pipeline;

            var orders = pipeline.Steps.Select(s => s.Order).OrderBy(o => o).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, 44).ToArray(), orders,
                "{1..44} 전수 등록 — `SimPipeline` 은 중복만 막고 누락은 못 막는다");

            foreach (var s in pipeline.Steps)
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase,
                    $"#{s.Order}({s.Name}) 의 phase 가 캡처 번호 구간과 어긋난다");
        }
    }
}
