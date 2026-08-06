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
    /// battle-sim-extraction unit 18-I/2 arm B — 캐스트 사건 드레인(#33 P8)의 오라클.
    ///
    /// 어서션은 구 sim 의 `AttackSystemUnifiedLoopTests` 에서 **복제**했다(재작성 금지 규칙):
    /// `CastEvent_PokeNeedle_FiresOnFifthCastWithNearestTarget` ·
    /// `CastEvent_DropsStaleCasterWithoutThrowing`.
    ///
    /// ⚠ 픽스처가 `FlowFieldSingleton` 을 **만들지 않는다** — 구 오라클의 환경이 그랬고,
    /// 그래서 이 스위트는 그리드 폴백(tileSize 1 · 128×128 · origin 0)까지 함께 고정한다.
    ///
    /// 여기서 지키는 계약은 셋이다: **① 캐스트 성사가 곧 공격 사건**(사거리 0 캐스터는 RESOLVE 에
    /// 도달하지 못한다) **② host 당 사건 지점은 하나**(계약 2 — 캐스트로 센 host 표시)
    /// **③ 카운트 소비는 arm 성공과 무관**(계약 5).
    /// </summary>
    public class SimAttackCastDrainTests
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

        /// 캐스터는 공격 사거리가 없다(range 0) — RESOLVE 로는 절대 카운트되지 않는다는 것이
        /// 계약 2 의 상호배타 전제다.
        private SimEntityId Caster(
            SimVec3 pos, int period, int tileRange = 4,
            DcPayloadKind payload = DcPayloadKind.ProjectileToTarget,
            DcTriggerKind trigger = DcTriggerKind.AttackN)
        {
            var e = Attacker(pos);
            _world.AddBuffer<DcTriggerSlot>(e).Add(new DcTriggerSlot
            {
                instanceId = 1,
                trigger = trigger,
                period = (ushort)period,
                counter = 0,
                payload = payload,
                magnitude = 20f,
                projectileDataIndex = 0,
                speed = 10f,
                hitThreshold = 0.3f,
                visualScale = 1f,
                tileRange = tileRange,
                patternIndex = -1, // ⚠ 미배선의 유효 초기값(0 은 유효 index 라 0번 패턴을 쏜다)
            });
            return e;
        }

        /// 카드 없는 방어유닛 — `AttackState` 를 가지므로 시스템의 실행 게이트를 만족시킨다.
        private SimEntityId Attacker(SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Defender });
            _world.Set(e, new DefenderUnitTag());
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new AttackState
            {
                range = 0f,
                cooldownDuration = 999f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.Enemy,
            });
            return e;
        }

        private SimEntityId Enemy(SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Enemy });
            _world.Set(e, new AttackUnitTag());
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, SimTransform.FromPosition(pos));
            return e;
        }

        private void Cast(SimEntityId caster, SimVec3 pos = default)
            => _channels.Cast.Enqueue(new CastEvent { caster = caster, casterPos = pos });

        private List<ProjectileSpawnRequest> Carriers()
        {
            var reqs = new List<ProjectileSpawnRequest>();
            foreach (var e in _world.With<ProjectileRequestCarrier>())
                reqs.Add(_world.Get<ProjectileSpawnRequest>(e));
            return reqs;
        }

        private ushort Counter(SimEntityId e) => _world.GetBuffer<DcTriggerSlot>(e)[0].counter;

        // ── 구 오라클 복제 ────────────────────────────────────────────────────

        [Test]
        public void PokeNeedle_FiresOnFifthCast_WithNearestTarget()
        {
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 5);
            var far = Enemy(new SimVec3(3f, 0f, 0f));
            var near = Enemy(new SimVec3(1f, 0f, 0f));

            for (int i = 0; i < 4; i++)
            {
                Cast(caster);
                _sut.Run(_world);
            }
            Assert.AreEqual(0, Carriers().Count, "5회째 전에는 니들이 나가면 안 된다(캐스트만 카운트)");

            Cast(caster);
            _sut.Run(_world);

            var reqs = Carriers();
            Assert.AreEqual(1, reqs.Count, "캐스터도 5번째 캐스트에 니들 캐리어를 스폰해야 한다");
            Assert.AreEqual(near, reqs[0].target,
                "host 가 대상을 안 주므로 페이로드가 스스로 최근접 적을 고른다");
            Assert.AreNotEqual(far, reqs[0].target);
            Assert.AreEqual(caster, reqs[0].owner, "위협 귀속은 캐스터 본인");
            Assert.AreEqual(20f, reqs[0].damage, 1e-4f, "flat magnitude(계약 7 — damageMul 미적용)");
            Assert.AreEqual(MovementKind.HomingToEntity, reqs[0].movement, "니들은 대상 호밍");
            Assert.AreEqual(0, reqs[0].dataIndex, "슬롯의 투사체 데이터 인덱스 사용");
            Assert.AreEqual(4, reqs[0].retargetTileRange, "대상이 먼저 죽으면 같은 반경에서 재조준");
        }

        [Test]
        public void StaleCaster_IsDropped_WithoutThrowing()
        {
            // enqueue 후 드레인 전에 캐스터가 죽는 창이 있다 — 그 이벤트는 조용히 버린다.
            // ⚠ 구 오라클보다 강하다: 방관 공격자를 하나 두어 **드레인이 실제로 도는** 상태에서
            //   확인한다(구 테스트는 캐스터를 지우면 실행 게이트가 먼저 걸려 통과했다).
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);
            Attacker(new SimVec3(5f, 0f, 0f));
            Enemy(new SimVec3(1f, 0f, 0f));

            Cast(caster);
            _world.Destroy(caster);

            Assert.DoesNotThrow(() => _sut.Run(_world), "파괴된 캐스터의 이벤트가 드레인을 깨면 안 된다");
            Assert.AreEqual(0, Carriers().Count);
            Assert.IsEmpty(_sut.CastCountedHosts, "버려진 사건은 카운트도 남기지 않는다");
        }

        // ── 계약 2 — host 당 사건 지점 하나 ───────────────────────────────────

        [Test]
        public void CastCountedHosts_MarksTheHost_EvenWhenNoSlotFires()
        {
            // ⚠ 이 표시는 arm E(RESOLVE)로 가는 seam 이다 — 캐스트로 센 host 는 RESOLVE 의
            //   카운팅을 건너뛴다. 슬롯이 발동하지 **않아도** 사건은 났다.
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 5);
            Enemy(new SimVec3(1f, 0f, 0f));

            Cast(caster);
            _sut.Run(_world);

            CollectionAssert.AreEquivalent(new[] { caster }, _sut.CastCountedHosts.ToArray());
            Assert.AreEqual(1, Counter(caster), "카운터는 돌았지만 아직 발동은 아니다");
            Assert.AreEqual(0, Carriers().Count);
        }

        [Test]
        public void CastCountedHosts_IsFrameLocal()
        {
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 5);

            Cast(caster);
            _sut.Run(_world);
            Assert.AreEqual(1, _sut.CastCountedHosts.Count);

            _sut.Run(_world);
            Assert.IsEmpty(_sut.CastCountedHosts, "표시는 프레임 로컬이다 — 다음 틱으로 새지 않는다");
        }

        [Test]
        public void CardlessCaster_IsNotCounted()
        {
            // 생산자(#18)가 카드 보유를 게이트하지만 소비자도 같은 조건으로 막는다(이중 방어).
            var plain = Attacker(new SimVec3(0f, 0f, 0f));

            Cast(plain);
            _sut.Run(_world);

            Assert.IsEmpty(_sut.CastCountedHosts);
        }

        // ── 계약 5 — 카운트 소비는 arm 성공과 무관 ────────────────────────────

        [Test]
        public void UnhandledPayload_ConsumesTheCount_AndWarns()
        {
            // 발동했는데 arm 이 없으면 **소리를 낸다**. 조용히 태우면 카드가 죽은 채로 배포된다.
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1,
                                payload: DcPayloadKind.SelfTileAoe);
            Enemy(new SimVec3(1f, 0f, 0f));

            Cast(caster);
            _sut.Run(_world);

            Assert.AreEqual(0, Carriers().Count, "이 자리에 그 arm 은 없다");
            Assert.AreEqual(0, Counter(caster), "그래도 카운트는 소비됐다(계약 5)");

            var warnings = _channels.Warnings.Drain();
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(SimWarningCode.CastEventUnhandledPayload, warnings[0].code);
            Assert.AreEqual(caster, warnings[0].entity);
            Assert.AreEqual((int)DcPayloadKind.SelfTileAoe, warnings[0].detail,
                "detail 은 처리되지 않은 payload 의 정수값이다");
        }

        [Test]
        public void NoTargetInRange_StillConsumesTheCount_AndSignals()
        {
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1, tileRange: 1);
            Enemy(new SimVec3(4f, 0f, 0f)); // 체비셰프 4 > tileRange 1

            Cast(caster);
            _sut.Run(_world);

            Assert.AreEqual(0, Carriers().Count);
            Assert.AreEqual(0, Counter(caster), "반경 안에 적이 없어도 카운트는 이미 소비됐다");
            Assert.AreEqual(1, _channels.DcTriggerFired.Count,
                "발동 = 카운터 소비 성사 — 대상 유무와 무관하게 신호한다");
        }

        [Test]
        public void FiredSignal_CarriesTheHost()
        {
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);
            Enemy(new SimVec3(1f, 0f, 0f));

            Cast(caster);
            _sut.Run(_world);

            var fired = _channels.DcTriggerFired.Drain();
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(caster, fired[0].host, "귀속은 host 단위다");
        }

        [Test]
        public void NonAttackNSlots_AreUntouched()
        {
            // 캐스트는 `AttackN` 만 센다 — 주기·임계 트리거의 카운터는 다른 시스템 소유다.
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1,
                                trigger: DcTriggerKind.PeriodicTimer);
            Enemy(new SimVec3(1f, 0f, 0f));

            Cast(caster);
            _sut.Run(_world);

            Assert.AreEqual(0, Counter(caster), "AttackN 이 아닌 슬롯의 카운터는 움직이지 않는다");
            Assert.AreEqual(0, Carriers().Count);
            Assert.AreEqual(0, _channels.DcTriggerFired.Count);
            Assert.AreEqual(1, _sut.CastCountedHosts.Count, "그래도 캐스트 사건 자체는 났다");
        }

        // ── 폴백 선정 ─────────────────────────────────────────────────────────

        [Test]
        public void PastGoalEnemies_AreNotNeedleTargets()
        {
            // 유출 대기 적에 니들을 낭비하지 않는다 — 구 sim 에서 실제로 누락됐던 필터다.
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);
            var leaking = Enemy(new SimVec3(1f, 0f, 0f));
            _world.Set(leaking, new PastGoalTag());
            var healthy = Enemy(new SimVec3(3f, 0f, 0f));

            Cast(caster);
            _sut.Run(_world);

            Assert.AreEqual(healthy, Carriers()[0].target);
        }

        [Test]
        public void DeadOrPendingOrLeapedEnemies_AreNotCandidates()
        {
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);
            _world.Set(Enemy(new SimVec3(1f, 0f, 0f)), new DeadTag());
            _world.Set(Enemy(new SimVec3(0f, 0f, 1f)), new PendingDeployment());
            // 판 밖(궁극기 이탈) — 겨누면 방어유닛이 빈 타일에 쏜다.
            _world.Set(Enemy(new SimVec3(-1f, 0f, 0f)), new UltimateLeapState { remaining = 1f });
            var alive = Enemy(new SimVec3(3f, 0f, 0f));

            Cast(caster);
            _sut.Run(_world);

            Assert.AreEqual(alive, Carriers()[0].target);
        }

        [Test]
        public void DefenderFactionUnits_AreNotNeedleTargets()
        {
            // 폴백 진영은 Enemy 로 고정돼 있다(호출처가 전부 defender 게이트 안이라서).
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);
            Attacker(new SimVec3(1f, 0f, 0f)); // 아군, 더 가깝다
            var enemy = Enemy(new SimVec3(3f, 0f, 0f));

            Cast(caster);
            _sut.Run(_world);

            Assert.AreEqual(enemy, Carriers()[0].target, "아군 오사 금지");
        }

        [Test]
        public void EquidistantTie_GoesToTheLowerSimId()
        {
            // ⚠ 이 축이 없으면 결과가 스냅샷 순서에 걸려 같은 판이 실행마다 갈린다.
            var first = Enemy(new SimVec3(2f, 0f, 0f));
            Enemy(new SimVec3(-2f, 0f, 0f)); // 같은 거리, 나중에 생성 = 높은 simId
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);

            Cast(caster);
            _sut.Run(_world);

            Assert.AreEqual(first, Carriers()[0].target);
        }

        [Test]
        public void NeedleLog_RecordsTheShot()
        {
            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);
            var enemy = Enemy(new SimVec3(1f, 0f, 0f));

            Cast(caster, new SimVec3(0f, 0.5f, 0f));
            _sut.Run(_world);

            var log = _channels.AttackOutputLog.Drain();
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(caster, log[0].attacker);
            Assert.AreEqual(AttackOutputKind.Damage, log[0].kind);
            Assert.AreEqual(20f, log[0].magnitude, 1e-4f);
            Assert.AreEqual(new SimVec3(0f, 0.5f, 0f), log[0].sourcePos,
                "발사 원점은 사건이 실어 온 위치다(드레인이 다시 조회하지 않는다)");
            Assert.AreEqual(_world.Get<SimTransform>(enemy).Position, log[0].targetPos);
        }

        // ── 실행 게이트 ───────────────────────────────────────────────────────

        [Test]
        public void WithoutAnyAttacker_TheChannelIsNotDrained()
        {
            // 구 sim 의 `RequireForUpdate<AttackState>` — 시스템이 안 돌면 큐도 안 빈다.
            // 그 사건은 사라지지 않고 공격자가 생기는 첫 틱에 소비된다.
            var host = _world.Create();
            _world.AddBuffer<DcTriggerSlot>(host).Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.AttackN, period = 1, patternIndex = -1,
                payload = DcPayloadKind.ProjectileToTarget,
            });

            Cast(host);
            _sut.Run(_world);

            Assert.AreEqual(1, _channels.Cast.Count, "공격자가 0 명이면 드레인 자체가 없다");
            Assert.AreEqual(0, Counter(host));
        }

        // ── 클러스터 등록 (F6) ────────────────────────────────────────────────

        [Test]
        public void Cluster_RegistersBothHazardCastAndAttack()
        {
            // ⚠ `SimPipeline` 은 번호 **중복**만 막고 누락은 못 막는다 — #18 이 어느 클러스터에도
            //   없던 상태를 여기서 닫는다.
            var steps = new AttackCluster(new SimChannels()).Steps().ToList();

            CollectionAssert.AreEquivalent(new[] { 18, 33 }, steps.Select(s => s.Order).ToArray());
            foreach (var s in steps)
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase,
                    $"#{s.Order}({s.Name}) 의 phase 가 캡처 번호 구간과 어긋난다");
        }

        [Test]
        public void HazardCastAndDrain_HappenInTheSameTick()
        {
            // ⚠ 이것이 arm B 의 존재 이유다 — #18(P5) 이 낸 사건을 #33(P8) 이 **같은 틱**에
            //   소비한다. 뒤집히면 "가끔 한 프레임 늦게 나감" 이 된다.
            var channels = new SimChannels();
            var tick = new SimPipeline().Add(new AttackCluster(channels).Steps()).Build();

            var ff = _world.Create();
            _world.Set(ff, new FlowFieldSingleton
            {
                flow = new SimVec2[1], dist = new int[1],
                gridSize = new SimInt2(64, 64), tileSize = 1f, origin = default,
            });

            var caster = Caster(new SimVec3(0f, 0f, 0f), period: 1);
            _world.Set(caster, new HazardCastState
            {
                range = 3f, cooldownDuration = 4f, cooldownRemaining = 0f,
                targetMask = (int)Faction.Enemy, dataIndex = 0, kind = HazardCastKind.Zone,
                footprintWidth = 1, footprintHeight = 1,
            });
            var enemy = Enemy(new SimVec3(1f, 0f, 0f));
            _world.Set(enemy, new PathFollowState { speed = 1f }); // #18 의 후보 조건

            tick.Run(_world, 0.016f);

            Assert.AreEqual(1, Carriers().Count, "캐스트가 같은 틱에 카운트되어 니들이 나간다");
            Assert.AreEqual(enemy, Carriers()[0].target);
            Assert.AreEqual(0, channels.Cast.Count, "사건은 그 틱에 소비된다 — 이월 없음");
        }
    }
}
