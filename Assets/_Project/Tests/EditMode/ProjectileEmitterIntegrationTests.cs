using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;

        private const int Grid = 16;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ProjectileEmitterIntegrationTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileEmitterSystem>());

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
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            _world?.Dispose();
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
            return e;
        }

        // 패턴 host(적) — bake 가 하는 일의 최소 재현: PatternSlot + EmitterInstance 버퍼.
        private Entity CreatePatternHost(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddBuffer<PatternSlot>(e);
            _em.AddBuffer<EmitterInstance>(e);
            return e;
        }

        private static PatternSpec Spec(int shotCount, float interval) => new PatternSpec
        {
            barrelDataIndex = 0,
            damage = 40f,
            selection = PatternSelectionRule.RoundRobin,
            shotCount = shotCount,
            shotIntervalSec = interval,
            reselectPerShot = true,
            telegraphSec = 0f,
        };

        // arm(BossPeriodicTriggerSystem)이 하는 push 를 그대로 재현.
        private void PushInstance(Entity host, in PatternSpec spec)
        {
            var pats = _em.GetBuffer<PatternSlot>(host);
            if (pats.Length == 0)
                pats.Add(new PatternSlot { spec = spec, template = new ProjectileSpawnRequest
                {
                    movement = MovementKind.HomingToEntity,
                    payload = PayloadKind.SingleSplash,
                    speed = 10f,
                    dataIndex = 0,
                    owner = host,
                    targetFaction = ProjectileTargetFaction.Defender,
                }, fireCountBase = 0 });

            var pat = pats[0];
            var inst = new EmitterInstance { spec = pat.spec, template = pat.template, lockedTarget = Entity.Null };
            EmitterTick.Begin(ref inst.runtime, inst.spec, pat.fireCountBase);
            pat.fireCountBase += math.max(1, pat.spec.shotCount);
            pats[0] = pat;
            _em.GetBuffer<EmitterInstance>(host).Add(inst);
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

            PushInstance(host, Spec(shotCount: 3, interval: 0f));
            Tick();

            Assert.AreEqual(3, CarrierCount(), "shotCount 만큼 캐리어가 나와야 한다(발-루프 회귀 핀)");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length, "완주한 인스턴스는 제거된다");
        }

        // 리뷰 CRITICAL 회귀 핀 ② — 후보가 0 이어도 발사는 소모되고 인스턴스는 제거돼야
        // 한다. 제거되지 않으면 방어유닛이 없는 구간(전멸 직후)에 인스턴스가 무한 적재되고,
        // 재배치하는 순간 쌓인 전량이 한 프레임에 일제 발사된다.
        [Test]
        public void EmptyCandidatePool_ConsumesShots_AndStillRetiresInstance()
        {
            var host = CreatePatternHost(new float3(8f, 0f, 8f));

            PushInstance(host, Spec(shotCount: 2, interval: 0f));
            Tick();

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

            for (int i = 0; i < 5; i++)
            {
                PushInstance(host, Spec(shotCount: 1, interval: 0f));
                Tick();
            }
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length);

            CreateDefender(new float3(3f, 0f, 3f));
            PushInstance(host, Spec(shotCount: 1, interval: 0f));
            Tick();

            Assert.AreEqual(1, CarrierCount(), "밀린 발사가 쏟아지지 않고 이번 1발만 나가야 한다");
        }

        // 잠금(reselectPerShot=false): 버스트 전 발이 같은 대상을 향하고, 그 대상이
        // 사라지면 남은 발을 소모한 뒤 인스턴스가 완주 제거된다.
        [Test]
        public void LockedTarget_LostMidBurst_ConsumesRemainingShots_AndRetires()
        {
            var keep = CreateDefender(new float3(2f, 0f, 2f));
            var host = CreatePatternHost(new float3(8f, 0f, 8f));

            var spec = Spec(shotCount: 3, interval: 0.1f);
            spec.reselectPerShot = false;
            PushInstance(host, spec);

            Tick();                     // 첫 발 — 여기서 대상이 잠긴다
            Assert.AreEqual(1, CarrierCount());
            DestroyCarriers();

            _em.DestroyEntity(keep);    // 잠근 대상 소실
            for (int i = 0; i < 20 && _em.GetBuffer<EmitterInstance>(host).Length > 0; i++) Tick(0.05f);

            Assert.AreEqual(0, CarrierCount(), "잠근 대상이 사라지면 남은 발은 소모된다");
            Assert.AreEqual(0, _em.GetBuffer<EmitterInstance>(host).Length, "인스턴스는 완주 제거된다");
        }

        // 계약 7 — 진영은 host 에서 도출한다. 적 host 의 탄은 방어유닛을 겨눈다.
        [Test]
        public void EnemyHost_TargetsDefenders_WithDefenderVictimFaction()
        {
            var def = CreateDefender(new float3(4f, 0f, 4f));
            var host = CreatePatternHost(new float3(9f, 0f, 9f));

            PushInstance(host, Spec(shotCount: 1, interval: 0f));
            Tick();

            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
            using var reqs = q.ToComponentDataArray<ProjectileSpawnRequest>(Allocator.Temp);
            Assert.AreEqual(1, reqs.Length);
            Assert.AreEqual(def, reqs[0].target);
            Assert.AreEqual(ProjectileTargetFaction.Defender, reqs[0].targetFaction);
            Assert.AreEqual(40f, reqs[0].damage, "damage 는 패턴 소유(spec) 값이다");
        }
    }
}
