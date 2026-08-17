using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class ProjectileSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ProjectileSystemTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileMoveSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        private Entity MakeTarget(float3 pos, PlacementLayer traversalLayer = PlacementLayer.None)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100, max = 100 });
            _em.AddBuffer<IncomingDamage>(e);
            // battle-structures unit 9 — 광역 피해자 풀이 FactionTag 로 진영을 가른다.
            // 프로덕션 적 스폰(BattleBridge:7674)은 항상 이것을 붙이므로 픽스처가 빠뜨린 것이다.
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            if (traversalLayer != PlacementLayer.None)
                _em.AddComponentData(e, new PathFollowState
                {
                    traversalLayers = (byte)traversalLayer,
                });
            return e;
        }

        private Entity MakeProjectile(float3 origin, Entity target, float speed, float damage, float hitThreshold)
        {
            var e = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(e);
            _em.AddComponentData(e, LocalTransform.FromPosition(origin));
            _em.AddComponentData(e, new ProjectileState
            {
                target = target,
                speed = speed,
                damage = damage,
                hitThreshold = hitThreshold,
            });
            return e;
        }

        [Test]
        public void Move_Advances_Toward_Target_At_Configured_Speed()
        {
            var target = MakeTarget(new float3(10f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 5f, damage: 10f, hitThreshold: 0.1f);

            Tick(1f); // move 5 units toward (10,0,0)

            var pos = _em.GetComponentData<LocalTransform>(proj).Position;
            Assert.AreEqual(5f, pos.x, 1e-3f);
            Assert.IsTrue(_em.Exists(proj), "projectile must survive when target is far");
        }

        [Test]
        public void Move_Destroys_Projectile_When_Target_Missing()
        {
            var target = MakeTarget(new float3(10f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 5f, damage: 10f, hitThreshold: 0.1f);

            _em.DestroyEntity(target);

            Tick(0.1f);

            Assert.IsFalse(_em.Exists(proj), "projectile must self-destroy when its target no longer exists");
        }

        [Test]
        public void Hit_Appends_IncomingDamage_And_Destroys_Projectile_When_In_Range()
        {
            var target = MakeTarget(new float3(0.1f, 0f, 0f)); // within threshold from origin
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 100f, damage: 42f, hitThreshold: 0.5f);

            Tick(0.016f);

            Assert.IsFalse(_em.Exists(proj), "projectile must be destroyed on hit");
            var buffer = _em.GetBuffer<IncomingDamage>(target);
            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(42f, buffer[0].amount, 1e-3f);
        }

        // defender-knockback-on-impact unit 1 — 사용자 증상: "넉백이 발사 시점에 걸리는 느낌".
        // 실제로 그랬다 — 유닛 넉백은 AttackSystem 이 공격 성사 순간에 걸었고 화살은 그때
        // 막 출발했다. 이 테스트가 단언하는 것은 **증상 그대로**다: 화살이 맞는 그 순간에
        // 넉백이 나고, 방향은 적이 가던 방향의 반대다.
        [Test]
        public void Hit_EmitsOwnerKnockback_OppositeVictimTravel()
        {
            var ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            try
            {
                var ccSingleton = _em.CreateEntity();
                _em.AddComponentData(ccSingleton, new EnemyCcEventsSingleton { queue = ccQueue });

                // 사수 = 넉백을 저작한 방어유닛. 넉백은 유닛 소유라 화살 SO 가 아니라
                // 여기(DefenderCcData)에서 나온다.
                var shooter = _em.CreateEntity();
                _em.AddComponentData(shooter, LocalTransform.FromPosition(new float3(-5f, 0f, 0f)));
                _em.AddComponent<DefenderUnitTag>(shooter);
                _em.AddComponentData(shooter, new DefenderCcData
                {
                    knockbackDistance = 0.3f,
                    knockbackDuration = 0.08f,
                });

                // 적은 +Z 로 걷는 중 → 밀리는 방향은 −Z 여야 한다.
                var target = MakeTarget(new float3(0.1f, 0f, 0f));
                _em.AddComponentData(target, new PathFollowState { lastMoveDir = new float2(0f, 1f) });

                var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 100f, damage: 7f, hitThreshold: 0.5f);
                var st = _em.GetComponentData<ProjectileState>(proj);
                st.owner = shooter;
                _em.SetComponentData(proj, st);

                Tick(0.016f);

                Assert.AreEqual(1, ccQueue.Count, "착탄 순간에 넉백이 한 번 난다");
                var ev = ccQueue.Dequeue();
                Assert.AreEqual(target, ev.target);
                Assert.AreEqual(CcKind.Impulse, ev.effect.kind);
                // 속력 = 거리 ÷ 지속 = 0.3 / 0.08 = 3.75, 방향 = 진행(+Z)의 반대(−Z).
                Assert.AreEqual(-3.75f, ev.effect.vector.z, 1e-3f, "적이 가던 방향의 반대로 민다");
                Assert.AreEqual(0f, ev.effect.vector.x, 1e-3f);
                Assert.AreEqual(0.08f, ev.effect.remainingTime, 1e-4f);
            }
            finally { ccQueue.Dispose(); }
        }

        // 사수가 넉백을 저작하지 않았으면 착탄해도 아무도 밀지 않는다 — 기존 투사체 전부
        // (마크스맨이 같은 화살을 쓴다)가 이 경로로 무변화임을 고정한다.
        [Test]
        public void Hit_WithoutOwnerKnockbackAuthoring_EmitsNothing()
        {
            var ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            try
            {
                var ccSingleton = _em.CreateEntity();
                _em.AddComponentData(ccSingleton, new EnemyCcEventsSingleton { queue = ccQueue });

                var shooter = _em.CreateEntity();
                _em.AddComponentData(shooter, LocalTransform.FromPosition(new float3(-5f, 0f, 0f)));
                _em.AddComponent<DefenderUnitTag>(shooter);
                _em.AddComponentData(shooter, new DefenderCcData()); // 넉백 미저작

                var target = MakeTarget(new float3(0.1f, 0f, 0f));
                _em.AddComponentData(target, new PathFollowState { lastMoveDir = new float2(0f, 1f) });

                var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 100f, damage: 7f, hitThreshold: 0.5f);
                var st = _em.GetComponentData<ProjectileState>(proj);
                st.owner = shooter;
                _em.SetComponentData(proj, st);

                Tick(0.016f);

                Assert.AreEqual(0, ccQueue.Count, "넉백 미저작 투사체는 아무도 밀지 않는다");
            }
            finally { ccQueue.Dispose(); }
        }

        [Test]
        public void Hit_Splash_Damages_Neighbors_Excluding_Direct_Target_And_Non_AttackUnit()
        {
            var direct = MakeTarget(new float3(0f, 0f, 0f));
            var nearbyAttacker = MakeTarget(new float3(0.4f, 0f, 0f));      // within splash
            var farAttacker = MakeTarget(new float3(3f, 0f, 0f));           // outside splash
            // Non-attack-unit entity inside splash radius — must not receive damage.
            var decoy = _em.CreateEntity();
            _em.AddComponentData(decoy, LocalTransform.FromPosition(new float3(0.3f, 0f, 0f)));
            _em.AddBuffer<IncomingDamage>(decoy);

            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                target = direct,
                speed = 0f,
                damage = 100f,
                hitThreshold = 0.1f,
                onHitEffect = OnHitEffectType.Splash,
                splashRadius = 1.0f,
                splashDamageMul = 0.5f,
            });

            Tick(0.016f);

            Assert.IsFalse(_em.Exists(proj), "projectile should consume itself on hit");
            // Direct target: full 100 damage (splash loop excludes it).
            // Rather than read Health (which DamageApplicationSystem would drain),
            // we skip that system in SetUp — so IncomingDamage buffer is the source of truth.
            var directBuf = _em.GetBuffer<IncomingDamage>(direct);
            var nearbyBuf = _em.GetBuffer<IncomingDamage>(nearbyAttacker);
            var farBuf = _em.GetBuffer<IncomingDamage>(farAttacker);
            var decoyBuf = _em.GetBuffer<IncomingDamage>(decoy);
            Assert.AreEqual(1, directBuf.Length, "direct target gets one damage event");
            Assert.AreEqual(100f, directBuf[0].amount, 1e-4f);
            Assert.AreEqual(1, nearbyBuf.Length, "neighbor within radius gets splash");
            Assert.AreEqual(50f, nearbyBuf[0].amount, 1e-4f, "splash damage = damage * splashDamageMul");
            Assert.AreEqual(0, farBuf.Length, "attacker outside splashRadius is untouched");
            Assert.AreEqual(0, decoyBuf.Length, "non-AttackUnit entity must be filtered from splash pool");
        }

        [Test]
        public void PathOnly_Projectile_DirectAndSplash_DoNotDamageAir()
        {
            var directPath = MakeTarget(float3.zero, PlacementLayer.Path);
            var nearbyPath = MakeTarget(new float3(0.3f, 0f, 0f), PlacementLayer.Path);
            var nearbyAir = MakeTarget(new float3(0.4f, 0f, 0f), PlacementLayer.Air);

            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(proj, new ProjectileState
            {
                target = directPath,
                speed = 0f,
                damage = 100f,
                hitThreshold = 0.1f,
                onHitEffect = OnHitEffectType.Splash,
                splashRadius = 1f,
                splashDamageMul = 0.5f,
                targetTraversalLayers = (byte)PlacementLayer.Path,
            });

            Tick(0.016f);

            Assert.AreEqual(100f, _em.GetBuffer<IncomingDamage>(directPath)[0].amount, 1e-4f);
            Assert.AreEqual(50f, _em.GetBuffer<IncomingDamage>(nearbyPath)[0].amount, 1e-4f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(nearbyAir).Length,
                "공중 적은 지상탄의 스플래시에도 맞지 않는다");
        }

        [Test]
        public void Hit_Skips_When_Outside_Threshold()
        {
            var target = MakeTarget(new float3(5f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 0f, damage: 10f, hitThreshold: 0.1f);

            Tick(0.016f);

            Assert.IsTrue(_em.Exists(proj), "projectile must keep flying when not yet in range");
            var buffer = _em.GetBuffer<IncomingDamage>(target);
            Assert.AreEqual(0, buffer.Length, "no damage should have been applied");
        }

        // Directly exercises the trajectory/payload seam introduced in
        // projectile-trajectory-payload unit 1: a projectile flies for several
        // frames, arrives on a *later* frame (MoveSystem sets impactReached), and
        // ProjectileHitSystem resolves it that same frame. The other hit tests all
        // resolve on frame 1 or never arrive, so none pin down this hand-off.
        [Test]
        public void Move_Then_Arrives_And_Resolves_On_A_Later_Frame()
        {
            var target = MakeTarget(new float3(10f, 0f, 0f));
            var proj = MakeProjectile(new float3(0f, 0f, 0f), target, speed: 4f, damage: 25f, hitThreshold: 0.5f);

            Tick(1f); // x: 0 → 4, not yet in range
            Assert.IsTrue(_em.Exists(proj), "still flying after frame 1");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(target).Length, "no damage mid-flight (frame 1)");

            Tick(1f); // x: 4 → 8, still not in range
            Assert.IsTrue(_em.Exists(proj), "still flying after frame 2");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(target).Length, "no damage mid-flight (frame 2)");

            Tick(1f); // dist 2 <= step 4 → snap to target → arrives → resolves this frame
            Assert.IsFalse(_em.Exists(proj), "projectile arrives and is consumed on a later frame");
            var buffer = _em.GetBuffer<IncomingDamage>(target);
            Assert.AreEqual(1, buffer.Length, "damage applied on the arrival frame");
            Assert.AreEqual(25f, buffer[0].amount, 1e-3f);
        }

        // Exercises the BallisticArc movement arm (unit 3): a target-less projectile
        // flies a fixed arc to a locked impact over flightTime, arriving by elapsed
        // rather than distance. Payload is left SingleSplash with no target, so it
        // simply consumes itself on arrival (TileAoe damage payload arrives in unit 4).
        [Test]
        public void Ballistic_Arcs_To_Impact_And_Arrives_By_FlightTime()
        {
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                movement = MovementKind.BallisticArcToPoint,
                payload = PayloadKind.SingleSplash, // no target → no damage, just consumes on arrival
                origin = new float3(0f, 0f, 0f),
                impact = new float3(4f, 0f, 0f),
                flightTime = 1f,
                arcHeight = 2f,
            });

            Tick(0.5f); // t=0.5 → XZ midpoint + apex
            Assert.IsTrue(_em.Exists(proj), "still flying mid-arc");
            var pos = _em.GetComponentData<LocalTransform>(proj).Position;
            Assert.AreEqual(2f, pos.x, 1e-3f, "XZ lerped to midpoint");
            Assert.AreEqual(0f, pos.z, 1e-3f);
            Assert.AreEqual(2f, pos.y, 1e-3f, "arc apex = arcHeight at t=0.5");

            Tick(0.5f); // elapsed 1.0 >= flightTime → arrives, consumed
            Assert.IsFalse(_em.Exists(proj), "arrives at flightTime and is consumed");
        }

        // TileAoe payload (unit 4): on arrival, every enemy within impactTileRange of
        // the locked impact cell takes the same flat damage; those outside are spared.
        // No FlowFieldSingleton in the test world → WorldToCell uses tileSize=1. The
        // impact sits at an interior cell (10,0,10) with all candidates at positive
        // coordinates, so no negative-axis clamp distorts the cell distances — the
        // diagonal candidate is a genuine Chebyshev-1 diagonal.
        [Test]
        public void TileAoe_Payload_Damages_Every_Enemy_In_Impact_Range()
        {
            var inRangeAxis = MakeTarget(new float3(11f, 0f, 10f));  // cell (11,10) → Chebyshev 1
            var inRangeDiag = MakeTarget(new float3(9f, 0f, 11f));   // cell (9,11)  → Chebyshev 1 (true diagonal)
            var outOfRange = MakeTarget(new float3(13f, 0f, 10f));   // cell (13,10) → Chebyshev 3

            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(10f, 0f, 10f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                movement = MovementKind.BallisticArcToPoint,
                payload = PayloadKind.TileAoe,
                origin = new float3(10f, 0f, 10f),
                impact = new float3(10f, 0f, 10f),
                flightTime = 0f,       // arrives immediately (t clamps to 1)
                arcHeight = 0f,
                impactTileRange = 1,
                damage = 30f,
            });

            Tick(0.016f);

            Assert.IsFalse(_em.Exists(proj), "consumed on impact");
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(inRangeAxis).Length);
            Assert.AreEqual(30f, _em.GetBuffer<IncomingDamage>(inRangeAxis)[0].amount, 1e-3f, "flat damage, no falloff");
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(inRangeDiag).Length);
            Assert.AreEqual(30f, _em.GetBuffer<IncomingDamage>(inRangeDiag)[0].amount, 1e-3f, "diagonal within range still hit");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(outOfRange).Length, "outside impactTileRange untouched");
        }

        [Test]
        public void PathOnly_TileAoe_DoesNotDamageAirInImpactRange()
        {
            var path = MakeTarget(new float3(11f, 0f, 10f), PlacementLayer.Path);
            var air = MakeTarget(new float3(9f, 0f, 10f), PlacementLayer.Air);
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(10f, 0f, 10f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                movement = MovementKind.BallisticArcToPoint,
                payload = PayloadKind.TileAoe,
                origin = new float3(10f, 0f, 10f),
                impact = new float3(10f, 0f, 10f),
                flightTime = 0f,
                impactTileRange = 1,
                damage = 30f,
                targetTraversalLayers = (byte)PlacementLayer.Path,
            });

            Tick(0.016f);

            Assert.AreEqual(30f, _em.GetBuffer<IncomingDamage>(path)[0].amount, 1e-3f);
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(air).Length,
                "공중 적은 지상탄의 타일 범위 피해에도 맞지 않는다");
        }

        // ── dreamcatcher-content-2 unit 3 (끝을 보는 눈) — priority direct-victim +20% ──

        [Test]
        public void Priority_DirectVictim_TakesBonus()
        {
            var target = MakeTarget(new float3(0.1f, 0f, 0f));
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                target = target, speed = 100f, damage = 100f, hitThreshold = 0.5f,
                priorityTarget = target, priorityDamageMul = 1.2f,
            });
            Tick(0.016f);
            Assert.AreEqual(120f, _em.GetBuffer<IncomingDamage>(target)[0].amount, 1e-3f, "direct victim == priority → ×1.2");
        }

        [Test]
        public void Priority_NonMatchingDirectVictim_StaysBase()
        {
            var target = MakeTarget(new float3(0.1f, 0f, 0f));
            var elsewherePrio = MakeTarget(new float3(50f, 0f, 0f)); // priority points at a different entity
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                target = target, speed = 100f, damage = 100f, hitThreshold = 0.5f,
                priorityTarget = elsewherePrio, priorityDamageMul = 1.2f,
            });
            Tick(0.016f);
            Assert.AreEqual(100f, _em.GetBuffer<IncomingDamage>(target)[0].amount, 1e-3f, "non-priority victim stays base");
        }

        [Test]
        public void Priority_SplashSecondary_StaysBase_EvenIfPriorityEntity()
        {
            var direct = MakeTarget(new float3(0f, 0f, 0f));
            var neighbor = MakeTarget(new float3(0.4f, 0f, 0f)); // within splash radius
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                target = direct, speed = 100f, damage = 100f, hitThreshold = 0.5f,
                onHitEffect = OnHitEffectType.Splash, splashRadius = 1f, splashDamageMul = 0.5f,
                priorityTarget = neighbor, priorityDamageMul = 1.2f, // priority is a SPLASH secondary
            });
            Tick(0.016f);
            Assert.AreEqual(100f, _em.GetBuffer<IncomingDamage>(direct)[0].amount, 1e-3f, "direct (non-priority) base");
            Assert.AreEqual(50f, _em.GetBuffer<IncomingDamage>(neighbor)[0].amount, 1e-3f, "splash secondary stays base even when it is the priority entity");
        }

        [Test]
        public void Priority_TileAoe_OnlyPriorityVictimBoosted()
        {
            var prio = MakeTarget(new float3(11f, 0f, 10f));   // in range, priority
            var other = MakeTarget(new float3(9f, 0f, 10f));   // in range, base
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(10f, 0f, 10f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                movement = MovementKind.BallisticArcToPoint, payload = PayloadKind.TileAoe,
                origin = new float3(10f, 0f, 10f), impact = new float3(10f, 0f, 10f),
                flightTime = 0f, arcHeight = 0f, impactTileRange = 1, damage = 30f,
                priorityTarget = prio, priorityDamageMul = 1.2f,
            });
            Tick(0.016f);
            Assert.AreEqual(36f, _em.GetBuffer<IncomingDamage>(prio)[0].amount, 1e-3f, "priority victim in AOE → ×1.2");
            Assert.AreEqual(30f, _em.GetBuffer<IncomingDamage>(other)[0].amount, 1e-3f, "other AOE victims stay base");
        }
    }
}
