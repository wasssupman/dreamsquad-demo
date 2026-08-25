using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // projectile-emission-pattern — emitter 통합 게이트. 순수 계층(EmitterTick/
    // PatternTargeting)은 각자의 테스트가 지키고, 여기서는 그것들이 **실제 시스템
    // 루프에 붙어 있는지**를 본다.
    //
    // 이 테스트가 존재하는 이유: 리뷰에서 잡힌 CRITICAL 두 건이 정확히 이 틈에
    // 있었다. `EmitterTickTests.ZeroInterval_DumpsEntireBurstInOneFrame` 은 "5 반환"
    // 으로 초록인데 emitter 가 캐리어를 1개만 만들어 4발이 증발했고, 후보 0 프레임에는
    // 인스턴스가 제거되지 않아 무한 적재됐다. 순수 계층이 전부 통과하는 동안 게임이
    // 깨져 있었다 — 소비 측을 보는 핀이 필요하다.
    //
    // 투사체 엔티티 자체는 BattleBridge(Mono)가 만들므로 여기서는 그 직전 산출물인
    // ProjectileSpawnRequest 캐리어를 센다(DirectionalVolleyIntegrationTests 선례).
    public class ProjectileEmitterIntegrationTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<Wassup.Battle.Skills.SkillFiredEvent> _skillQueue;
        private int _nextSimId;
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;

        private const int Grid = 16;

        [SetUp]
        public void SetUp()
        {
            _nextSimId = 0;
            _world = new World("ProjectileEmitterIntegrationTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            // 감지 → **스킬 레이어** → emitter 이음매까지 덮는다. 실행부를 빼고 테스트가
            // 직접 push 하면 시드 규약이 두 곳에 손으로 적혀 서로 어긋나도 초록으로 남는다.
            //
            // ⚠ skill-layer-migration unit 1 — 예전엔 `BossPeriodicTriggerSystem` 이
            // 발사까지 했다. 지금은 그 시스템이 **발화만 감지**하고 실행은 concrete 가
            // 한다. 그래서 하네스에 디스패처를 끼운다 — 여기를 빼면 이 그물은
            // 「감지는 되는데 아무도 안 쏜다」를 못 본다.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<BossPeriodicTriggerSystem>());
            _simGroup.AddSystemToUpdateList(
                _world.CreateSystemManaged<Wassup.Battle.Skills.SkillDispatchPeriodicSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileEmitterSystem>());
            _simGroup.SortSystems();

            // 레지스트리·어댑터는 라이브에선 브리지가 주입한다. 여기선 테스트가 한다.
            _skillQueue = new NativeQueue<Wassup.Battle.Skills.SkillFiredEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(),
                new Wassup.Battle.Skills.SkillFiredEventsSingleton { queue = _skillQueue });
            var registry = new Wassup.Skills.SkillRegistry();
            registry.Register(new Wassup.Skills.Concrete.EmitPatternSkill());
            Wassup.Battle.Skills.SkillDispatchSystemBase.Install(
                registry, new Wassup.Battle.Skills.EcsSkillContext());

            _flow = new NativeArray<float2>(Grid * Grid, Allocator.Persistent);
            _dist = new NativeArray<int>(Grid * Grid, Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new FlowFieldSingleton
            {
                flow = _flow,
                dist = _dist,
                gridSize = new int2(Grid, Grid),
                tileSize = 1f,
                origin = float3.zero,
            });
        }

        [TearDown]
        public void TearDown()
        {
            // ⚠ 정적 주입은 **테스트마다 푼다.** 안 풀면 다음 테스트의 월드가 죽은
            // 어댑터를 물려받아, 그 실패가 이 테스트 탓처럼 보인다.
            Wassup.Battle.Skills.SkillDispatchSystemBase.Uninstall();
            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            _world?.Dispose();
            if (_skillQueue.IsCreated) _skillQueue.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        private Entity CreateDefender(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new SimEntityId { value = _nextSimId++ });
            return e;
        }

        // 패턴 host(적) — bake 가 하는 일의 최소 재현: PatternSlot + EmitterInstance 버퍼.
        private Entity CreatePatternHost(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<AttackUnitTag>(e);
            // ⚠ **스킬 레이어의 핸들 축.** 어댑터가 이 값으로 엔티티를 되찾으므로,
            // 없으면 concrete 는 불리지만 자기 자신도 못 찾아 조용히 아무것도 안 한다.
            // 라이브는 스폰 지점에서 발급한다 — 이 하네스가 그 아키타입을 흉내낸다.
            _em.AddComponentData(e, new SimEntityId { value = _nextSimId++ });
            _em.AddBuffer<PatternSlot>(e);
            _em.AddBuffer<EmitterInstance>(e);
            _em.AddBuffer<DcTriggerSlot>(e); // arm 이 읽는 트리거 슬롯
            return e;
        }

        private static PatternSpec Spec(int shotCount, float interval)
        {
            var shots = default(FixedList128Bytes<PatternShotSpec>);
            for (int i = 0; i < shotCount; i++)
            {
                shots.Add(new PatternShotSpec
                {
                    directionT = 0.5f,
                    intervalAfterPreviousSec = i == 0 ? 0f : interval,
                });
            }

            return new PatternSpec
            {
                barrelDataIndex = 0,
                damage = 40f,
                selection = PatternSelectionRule.RoundRobin,
                minAngleDeg = 0f,
                maxAngleDeg = 0f,
                shots = shots,
                // shipped 두 패턴이 모두 false 라 라이브 경로에 가깝다(잠금 코드가 실제로 돈다).
                reselectPerShot = false,
                telegraphSec = 0f,
            };
        }

        private static PatternSpec DirectionSpec()
        {
            var shots = default(FixedList128Bytes<PatternShotSpec>);
            shots.Add(new PatternShotSpec { directionT = 0f, intervalAfterPreviousSec = 0f });
            shots.Add(new PatternShotSpec { directionT = 0.5f, intervalAfterPreviousSec = 0f });
            shots.Add(new PatternShotSpec { directionT = 1f, intervalAfterPreviousSec = 0f });
            return new PatternSpec
            {
                barrelDataIndex = 3,
                damage = 6f,
                selection = PatternSelectionRule.None,
                minAngleDeg = -30f,
                maxAngleDeg = 30f,
                shots = shots,
            };
        }

        // 패턴 슬롯을 host 에 얹는다. **push 는 하지 않는다** — 실제 arm
        // (BossPeriodicTriggerSystem)이 `PeriodicTimer` 발화로 밀어넣게 두어야
        // 시드 규약(fireCountBase += shots.Length)이 테스트에 복제되지 않는다.
        // 복제하면 arm 이 규약을 바꿔도 테스트는 옛 규약을 검증하며 초록으로 남는다.
        private void InstallPattern(Entity host, in PatternSpec spec, float periodSeconds)
        {
            InstallPattern(host, spec, periodSeconds, new ProjectileSpawnRequest
            {
                movement = MovementKind.HomingToEntity,
                payload = PayloadKind.SingleSplash,
                speed = 10f,
                dataIndex = 0,
                owner = host,
                targetFaction = ProjectileTargetFaction.Defender,
            });
        }

        private void InstallPattern(Entity host, in PatternSpec spec, float periodSeconds,
                                    in ProjectileSpawnRequest template)
        {
            var pats = _em.GetBuffer<PatternSlot>(host);
            pats.Add(new PatternSlot
            {
                spec = spec,
                template = template,
                fireCountBase = 0,
            });

            var slots = _em.GetBuffer<DcTriggerSlot>(host);
            slots.Add(new DcTriggerSlot
            {
                // ⚠ **라우팅 키.** 0(legacy)으로 두면 감지자가 이 payload 의 arm 을 찾는데
                // 그 arm 은 은퇴했다 — 슬롯은 발화하는데 아무도 안 쏜다.
                skillId = Wassup.Skills.Concrete.EmitPatternSkill.Id,
                trigger = DcTriggerKind.PeriodicTimer,
                payload = DcPayloadKind.EmitProjectilePattern,
                periodSeconds = periodSeconds,
                elapsed = 0f,
                patternIndex = pats.Length - 1,
                projectileDataIndex = -1,
            });
        }

        // arm 이 발화하도록 주기만큼 시간을 보낸다(같은 프레임에 emitter 가 이어 돈다).
        // ⚠ 큰 dt 를 한 번에 주므로 **버스트 tick 에도 그 dt 가 적용된다** — interval > 0
        // 인 패턴은 이 헬퍼로 한 프레임에 전량이 나간다(lag spike 사양). 버스트가 여러
        // 프레임에 걸쳐 진행되는 걸 관찰하려면 아래 FireOnceThenDetach 를 쓴다.
        private void TickTrigger(float periodSeconds) => Tick(periodSeconds + 0.001f);

        // 작은 dt 를 누적해 arm 을 **한 번만** 발화시키고, 슬롯을 떼어 재발화를 막는다.
        // 버스트 진행(interval > 0)을 프레임 단위로 관찰하는 테스트용.
        private void FireOnceThenDetach(Entity host)
        {
            for (int i = 0; i < 120 && _em.GetBuffer<EmitterInstance>(host).Length == 0; i++)
                Tick(0.016f);
            _em.GetBuffer<DcTriggerSlot>(host).Clear();
        }

        private int CarrierCount()
        {
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileRequestCarrier>());
            return q.CalculateEntityCount();
        }

        private void DestroyCarriers()
        {
            // 브리지 드레인이 스폰 후 캐리어를 파괴하는 것을 대신한다.
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileRequestCarrier>());
            _em.DestroyEntity(q);
        }

        // 리뷰 CRITICAL 회귀 핀 ① — Advance 가 반환한 발수를 emitter 가 전부 소비해야
        // 한다. 발-루프가 없으면 burstRemaining 은 N 차감됐는데 캐리어는 1개만 생긴다.
        [Test]
        public void Burst_ProducesOneCarrierPerShot_ThenRetires()
        {
            CreateDefender(new float3(2f, 0f, 2f));
            CreateDefender(new float3(5f, 0f, 5f));
            var host = CreatePatternHost(new float3(8f, 0f, 8f));

            InstallPattern(host, Spec(shotCount: 3, interval: 0f), periodSeconds: 1f);
            TickTrigger(1f);

            // ⚠ **두 경계를 따로 본다.** 「캐리어 0」만으로는 감지가 안 됐는지,
            // concrete 가 안 불렸는지, 불렸는데 못 쐈는지 구분이 안 된다.
            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Periodic), 1,
                "주기 seam 이 concrete 를 실제로 불렀나");
            Assert.AreEqual(3, CarrierCount(), "step 수만큼 캐리어가 나와야 한다(발-루프 회귀 핀)");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length, "완주한 인스턴스는 제거된다");
        }

        // arm 가드 회귀 핀 — 죽은 host 는 새 발동을 시작하지 않는다.
        [Test]
        public void DeadHost_DoesNotStartNewBurst()
        {
            CreateDefender(new float3(2f, 0f, 2f));
            var host = CreatePatternHost(new float3(8f, 0f, 8f));
            InstallPattern(host, Spec(shotCount: 1, interval: 0f), periodSeconds: 1f);

            _em.AddComponent<DeadTag>(host);
            TickTrigger(1f);

            Assert.AreEqual(0, CarrierCount(), "시체는 스킬을 쓰지 않는다");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length);
        }

        // 리뷰 CRITICAL 회귀 핀 ② — 후보가 0 이어도 발사는 소모되고 인스턴스는 제거돼야
        // 한다. 제거되지 않으면 방어유닛이 없는 구간(전멸 직후)에 인스턴스가 무한 적재되고,
        // 재배치하는 순간 쌓인 전량이 한 프레임에 일제 발사된다.
        [Test]
        public void EmptyCandidatePool_ConsumesShots_AndStillRetiresInstance()
        {
            var host = CreatePatternHost(new float3(8f, 0f, 8f));

            InstallPattern(host, Spec(shotCount: 2, interval: 0f), periodSeconds: 1f);
            TickTrigger(1f);

            Assert.AreEqual(0, CarrierCount(), "후보가 없으면 발사되지 않는다");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length,
                "후보 0 이어도 인스턴스는 완주 제거돼야 한다 — 남으면 적재 후 일제사격");
        }

        // 위 두 핀의 결합: 빈 풀에서 여러 번 발화한 뒤 방어유닛을 배치해도
        // 밀린 발사가 쏟아지지 않는다.
        [Test]
        public void RepeatedEmptyFires_DoNotAccumulate_IntoBurstWhenTargetsReturn()
        {
            var host = CreatePatternHost(new float3(8f, 0f, 8f));
            InstallPattern(host, Spec(shotCount: 1, interval: 0f), periodSeconds: 0.5f);

            for (int i = 0; i < 5; i++) TickTrigger(0.5f);
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length,
                "후보 0 구간의 발화가 인스턴스로 적재되면 안 된다");

            CreateDefender(new float3(3f, 0f, 3f));
            TickTrigger(0.5f);

            Assert.AreEqual(1, CarrierCount(), "밀린 발사가 쏟아지지 않고 이번 1발만 나가야 한다");
        }

        // 잠금(reselectPerShot=false): 버스트 전 발이 같은 대상을 향하고, 그 대상이
        // 사라지면 남은 발을 소모한 뒤 인스턴스가 완주 제거된다.
        [Test]
        public void LockedTarget_LostMidBurst_ConsumesRemainingShots_AndRetires()
        {
            var keep = CreateDefender(new float3(2f, 0f, 2f));
            var host = CreatePatternHost(new float3(8f, 0f, 8f));

            // 주기를 짧게 두고 작은 dt 로 한 번만 발화시킨다 — 큰 dt 를 주면 버스트
            // 전량이 한 프레임에 나가 "버스트 도중"이라는 상황 자체가 성립하지 않는다.
            InstallPattern(host, Spec(shotCount: 3, interval: 0.1f), periodSeconds: 0.05f);

            FireOnceThenDetach(host);   // 첫 발 — 여기서 대상이 잠긴다
            Assert.AreEqual(1, CarrierCount(), "interval 0.1s 라 첫 프레임엔 1발만");
            DestroyCarriers();

            _em.DestroyEntity(keep);    // 잠근 대상 소실
            for (int i = 0; i < 20 && _em.GetBuffer<EmitterInstance>(host).Length > 0; i++) Tick(0.05f);

            Assert.AreEqual(0, CarrierCount(), "잠근 대상이 사라지면 남은 발은 소모된다");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length, "인스턴스는 완주 제거된다");
        }

        [Test]
        public void DirectionPattern_FiresWithoutTargets_OnTriggerFrame_WithSnapshotPayload()
        {
            var host = CreatePatternHost(new float3(8f, 0f, 8f));
            var origin = new float3(3f, 0.25f, 7f);
            var template = new ProjectileSpawnRequest
            {
                movement = MovementKind.DirectionalLinear,
                payload = PayloadKind.PathHit,
                origin = origin,
                direction = new float2(0f, 1f),
                maxDistance = 2.5f,
                speed = 10f,
                dataIndex = 3,
                owner = host,
                targetFaction = ProjectileTargetFaction.Defender,
            };

            InstallPattern(host, DirectionSpec(), periodSeconds: 1f, template: template);
            TickTrigger(1f);

            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
            using var requests = query.ToComponentDataArray<ProjectileSpawnRequest>(Allocator.Temp);
            Assert.AreEqual(3, requests.Length, "타겟 후보가 0이어도 trigger 프레임에 3발 모두 생성");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length);

            for (int i = 0; i < requests.Length; i++)
            {
                Assert.AreEqual(origin, requests[i].origin, "host 현재 위치로 snapshot 원점을 덮으면 안 된다");
                Assert.AreEqual(2.5f, requests[i].maxDistance);
                Assert.AreEqual(6f, requests[i].damage);
                Assert.AreEqual(3, requests[i].dataIndex);
                Assert.AreEqual(Entity.Null, requests[i].target);
            }

            float diagonal = math.sqrt(3f) * 0.5f;
            Assert.IsTrue(ContainsDirection(requests, new float2(0.5f, diagonal)));
            Assert.IsTrue(ContainsDirection(requests, new float2(0f, 1f)));
            Assert.IsTrue(ContainsDirection(requests, new float2(-0.5f, diagonal)));
        }

        [Test]
        public void DirectionPattern_ActiveInstanceOnDeadHost_DoesNotEmit()
        {
            var host = CreatePatternHost(float3.zero);
            var spec = DirectionSpec();
            var instance = new EmitterInstance
            {
                spec = spec,
                template = new ProjectileSpawnRequest
                {
                    movement = MovementKind.DirectionalLinear,
                    direction = new float2(1f, 0f),
                    maxDistance = 2f,
                },
            };
            EmitterTick.Begin(ref instance.runtime, spec, 0);
            _em.GetBuffer<EmitterInstance>(host).Add(instance);
            _em.AddComponent<DeadTag>(host);

            Tick();

            Assert.AreEqual(0, CarrierCount(), "DeadTag host의 진행 중 sequence는 더 발사하지 않는다");
        }

        // 계약 7 — 진영은 host 에서 도출한다. 적 host 의 탄은 방어유닛을 겨눈다.
        [Test]
        public void EnemyHost_TargetsDefenders_WithDefenderVictimFaction()
        {
            var def = CreateDefender(new float3(4f, 0f, 4f));
            var host = CreatePatternHost(new float3(9f, 0f, 9f));

            InstallPattern(host, Spec(shotCount: 1, interval: 0f), periodSeconds: 1f);
            TickTrigger(1f);

            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
            using var reqs = q.ToComponentDataArray<ProjectileSpawnRequest>(Allocator.Temp);
            Assert.AreEqual(1, reqs.Length);
            Assert.AreEqual(def, reqs[0].target);
            Assert.AreEqual(ProjectileTargetFaction.Defender, reqs[0].targetFaction);
            Assert.AreEqual(40f, reqs[0].damage, "damage 는 패턴 소유(spec) 값이다");
        }

        private static bool ContainsDirection(in NativeArray<ProjectileSpawnRequest> requests,
                                              float2 expected)
        {
            for (int i = 0; i < requests.Length; i++)
                if (math.distance(requests[i].direction, expected) < 0.0001f)
                    return true;
            return false;
        }
    }
}
