using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // defender-directional-volley — AttackSystem 통합 게이트. 순수 함수(LaneMath/VolleyMath)는
    // 각자의 테스트가 지키고, 여기서는 그것들이 실제 시스템 루프에 **붙어 있는지**를 본다:
    // 레인만 보고 쏘는가, 볼리가 발수대로 나가는가, 방향은 facing 인가, 그리고 이 모든 것이
    // facing 없는 유닛에게는 아무 일도 일어나지 않는가.
    //
    // 투사체 엔티티 자체는 BattleBridge(Mono) 가 만들므로 여기서는 그 직전 산출물인
    // ProjectileSpawnRequest 를 센다 — ECS 가 책임지는 경계가 정확히 거기까지다.
    public class DirectionalVolleyIntegrationTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<UnitAttackVisualEvent> _attackEventQueue;
        private NativeQueue<EnemyCcEvent> _ccQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DirectionalVolleyIntegrationTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            _attackEventQueue = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new UnitAttackVisualEventsSingleton { queue = _attackEventQueue });
            _ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new EnemyCcEventsSingleton { queue = _ccQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackEventQueue.IsCreated) _attackEventQueue.Dispose();
            if (_ccQueue.IsCreated) _ccQueue.Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // 머신건 형상: 사거리 5타일, 즉발, Directional 투사체.
        private Entity CreateVolleyDefender(
            float3 pos, int2? facing, int shotCount, float intervalSec, float spreadDeg, float cooldown = 1.6f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 5f,
                cooldownDuration = cooldown,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.Enemy,
            });
            var outputs = _em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 8f }
            });
            _em.AddComponentData(e, new ProjectileRef
            {
                movement = MovementKind.DirectionalLinear,
                payload = PayloadKind.PathHit,
                speed = 22f,
                hitThreshold = 0.4f,
                visualScale = 0.5f,
                dataIndex = 0,
            });
            if (facing.HasValue)
                _em.AddComponentData(e, new DeployedFacing { value = facing.Value });
            if (shotCount > 1)
                _em.AddComponentData(e, new VolleyFireState
                {
                    shotCount = shotCount,
                    shotIntervalSec = intervalSec,
                    spreadAngleDeg = spreadDeg,
                });
            return e;
        }

        private Entity CreateEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        // 이번 프레임까지 스테이징된 발사 요청(공격자 본인 + 캐리어) 전부.
        private ProjectileSpawnRequest[] CollectRequests()
        {
            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
            var arr = q.ToComponentDataArray<ProjectileSpawnRequest>(Allocator.Temp);
            var result = arr.ToArray();
            arr.Dispose();
            return result;
        }

        private void ClearRequests()
        {
            // BattleBridge 드레인의 EditMode 대역: 요청을 소비해 다음 발만 세게 한다.
            var carrierQ = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileRequestCarrier>());
            _em.DestroyEntity(carrierQ);
            var reqQ = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
            _em.RemoveComponent<ProjectileSpawnRequest>(reqQ);
        }

        [Test]
        public void LaneGate_FiresOnlyWhenEnemyStandsInTheFacingLane()
        {
            var defender = CreateVolleyDefender(float3.zero, new int2(1, 0), shotCount: 1, intervalSec: 0f, spreadDeg: 0f);
            CreateEnemy(new float3(0f, 0f, 3f)); // 사거리 안이지만 +Z 레인 — facing 은 +X

            Tick();
            Assert.AreEqual(0, CollectRequests().Length, "레인 밖 적만 있으면 탄을 낭비하지 않는다");

            var inLane = CreateEnemy(new float3(3f, 0f, 0f));
            Tick();
            var reqs = CollectRequests();
            Assert.AreEqual(1, reqs.Length, "레인에 적이 들어오면 발사");
            Assert.AreEqual(new float2(1f, 0f), reqs[0].direction, "타겟 방향이 아니라 facing 방향으로 나간다");
            Assert.AreEqual(Entity.Null, reqs[0].target, "방향탄은 타겟을 싣지 않는다 — 경로에 있는 것을 맞힌다");
            Assert.Greater(reqs[0].maxDistance, 0f);

            _em.DestroyEntity(inLane);
        }

        [Test]
        public void LaneGate_IgnoresEnemyOneTileOffTheLane()
        {
            CreateVolleyDefender(float3.zero, new int2(0, 1), shotCount: 1, intervalSec: 0f, spreadDeg: 0f);
            CreateEnemy(new float3(1f, 0f, 3f)); // 폭 1타일 레인에서 한 칸 옆
            Tick();
            Assert.AreEqual(0, CollectRequests().Length);
        }

        [Test]
        public void Burst_FiresEveryShotAtTheAuthoredInterval()
        {
            CreateVolleyDefender(float3.zero, new int2(1, 0), shotCount: 10, intervalSec: 0.1f, spreadDeg: 0f);
            CreateEnemy(new float3(3f, 0f, 0f));

            Tick();
            Assert.AreEqual(1, CollectRequests().Length, "트리거 프레임엔 0번 발만");
            ClearRequests();

            // 0.9초 = (10-1) × 0.1 → 나머지 9발이 전부 나가야 한다.
            int fired = 0;
            for (int i = 0; i < 60; i++) // 60 × 0.016 = 0.96s
            {
                Tick();
                fired += CollectRequests().Length;
                ClearRequests();
            }
            Assert.AreEqual(9, fired, "버스트가 발수대로 완주");
        }

        [Test]
        public void Burst_CompletesEvenAfterTheLaneEmpties()
        {
            CreateVolleyDefender(float3.zero, new int2(1, 0), shotCount: 5, intervalSec: 0.1f, spreadDeg: 0f);
            var enemy = CreateEnemy(new float3(3f, 0f, 0f));

            Tick();
            Assert.AreEqual(1, CollectRequests().Length);
            ClearRequests();

            _em.DestroyEntity(enemy); // 첫 발 직후 적이 사라져도 시작된 버스트는 완주(계약 8)

            int fired = 0;
            for (int i = 0; i < 40; i++) { Tick(); fired += CollectRequests().Length; ClearRequests(); }
            Assert.AreEqual(4, fired, "레인이 비어도 남은 발은 나간다");
        }

        [Test]
        public void Burst_NextTriggerWaitsForTheVolleyToFinish()
        {
            const float dt = 0.016f;
            CreateVolleyDefender(float3.zero, new int2(1, 0), shotCount: 10, intervalSec: 0.1f, spreadDeg: 0f, cooldown: 1.6f);
            CreateEnemy(new float3(3f, 0f, 0f));

            Tick(dt); // 트리거 프레임 = 0번 발
            ClearRequests();

            // 발사 시각을 전부 기록해 사이클을 직접 읽는다. 창을 잘라 세면 창 길이가
            // 곧 기대값이 되어(0.1s 간격을 삼키면 한 발 더 잡힌다) 테스트가 계약이 아니라
            // 자기 산수를 검증하게 된다.
            float elapsed = dt;
            float lastBurstShotAt = 0f;
            float nextVolleyAt = -1f;
            int seen = 0;
            for (int i = 0; i < 220 && nextVolleyAt < 0f; i++)
            {
                Tick(dt);
                elapsed += dt;
                int n = CollectRequests().Length;
                ClearRequests();
                for (int s = 0; s < n; s++)
                {
                    seen++;
                    if (seen <= 9) lastBurstShotAt = elapsed;   // 첫 볼리의 나머지 9발
                    else nextVolleyAt = elapsed;                // 그 다음 발 = 두 번째 볼리의 0번
                }
            }

            Assert.AreEqual(9, math.min(seen, 9), "첫 볼리는 10발(트리거 발 + 9)");
            Assert.AreEqual(0.9f, lastBurstShotAt, 0.03f, "마지막 발은 (10-1)×0.1 = 0.9s 지점");
            Assert.AreEqual(2.5f, nextVolleyAt, 0.03f, "다음 볼리는 쿨다운 1.6 + 버스트 0.9 = 2.5s 뒤 — 버스트와 겹치지 않는다");
        }

        [Test]
        public void Spread_FiresEveryShotInOneFrame_FannedAroundFacing()
        {
            CreateVolleyDefender(float3.zero, new int2(1, 0), shotCount: 3, intervalSec: 0f, spreadDeg: 30f);
            CreateEnemy(new float3(3f, 0f, 0f));

            Tick();
            var reqs = CollectRequests();
            Assert.AreEqual(3, reqs.Length, "확산형은 같은 프레임에 전탄");

            // 가운데 발은 facing 그대로, 바깥 두 발은 ±15° — 전부 단위 길이.
            float maxAngle = 0f;
            bool hasStraight = false;
            foreach (var r in reqs)
            {
                Assert.AreEqual(1f, math.length(r.direction), 1e-4f);
                float deg = math.degrees(math.acos(math.clamp(math.dot(r.direction, new float2(1f, 0f)), -1f, 1f)));
                maxAngle = math.max(maxAngle, deg);
                if (deg < 1e-3f) hasStraight = true;
            }
            Assert.IsTrue(hasStraight, "3발 중 가운데는 facing 직진");
            Assert.AreEqual(15f, maxAngle, 1e-3f, "바깥 발은 총 확산각의 절반");
        }

        [Test]
        public void NonFacingDefender_KeepsLegacyTargetedFire()
        {
            // facing 없는 유닛은 이 spec 이 없던 때와 똑같이 동작해야 한다 — 회귀 가드.
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 5f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Enemy,
            });
            var outputs = _em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 8f } });
            _em.AddComponentData(e, new ProjectileRef
            {
                movement = MovementKind.HomingToEntity, payload = PayloadKind.SingleSplash,
                speed = 12f, hitThreshold = 0.35f, visualScale = 0.9f, dataIndex = 0,
            });
            var offLane = CreateEnemy(new float3(1f, 0f, 3f)); // 레인 개념이 없으니 이것도 사거리 안이면 쏜다

            Tick();
            var reqs = CollectRequests();
            Assert.AreEqual(1, reqs.Length);
            Assert.AreEqual(MovementKind.HomingToEntity, reqs[0].movement);
            Assert.AreEqual(offLane, reqs[0].target, "기존 유닛은 여전히 최근접 타겟을 추적한다");
        }
    }
}
