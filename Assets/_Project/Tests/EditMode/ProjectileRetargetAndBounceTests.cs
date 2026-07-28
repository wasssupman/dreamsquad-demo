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
    // 투사체 생존 규칙 두 가지의 **글루** 테스트. 순수 함수(BounceRetarget.FindNext)는
    // 자기 테스트가 지키므로, 여기서는 그것이 실제 시스템 루프에 붙어 있는지를 본다:
    //   ① 재조준 — 대상이 죽거나 사라졌을 때 파괴 대신 다시 겨누는가(opt-in)
    //   ② 방향탄 bounce — pierce 예산이 끝나면 호밍으로 전환해 튕기는가
    // 특히 **무회귀**(retargetTileRange 0 인 기존 투사체는 예전처럼 소멸)를 고정한다.
    public class ProjectileRetargetAndBounceTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ProjectileRetargetAndBounceTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileMoveSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            // 그리드 기준(타일 1). BounceRetarget/재조준이 Chebyshev 판정에 쓴다.
            _em.AddComponentData(_em.CreateEntity(), new FlowFieldSingleton
            {
                tileSize = 1f,
                gridSize = new int2(64, 64),
                origin = float3.zero,
            });
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        private Entity CreateEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e); // 재조준 후보 풀의 진영 축
            return e;
        }

        private Entity CreateHomingProjectile(float3 pos, Entity target, int retargetTileRange)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<ProjectileTag>(e);
            _em.AddComponentData(e, new ProjectileState
            {
                movement = MovementKind.HomingToEntity,
                payload = PayloadKind.SingleSplash,
                target = target,
                damage = 20f,
                speed = 1f,          // 느리게 — 재조준 판정이 도착보다 먼저 오게
                hitThreshold = 0.2f,
                retargetTileRange = retargetTileRange,
            });
            return e;
        }

        // ── ① 재조준 ────────────────────────────────────────────────────────

        [Test]
        public void Retarget_TargetDestroyed_RepicksNearbyEnemy()
        {
            var doomed = CreateEnemy(new float3(3f, 0f, 0f));
            var other = CreateEnemy(new float3(2f, 0f, 1f));
            var proj = CreateHomingProjectile(float3.zero, doomed, retargetTileRange: 4);

            _em.DestroyEntity(doomed);
            Tick();

            Assert.IsTrue(_em.Exists(proj), "재조준 대상이 있으면 파괴되면 안 된다");
            Assert.AreEqual(other, _em.GetComponentData<ProjectileState>(proj).target,
                "반경 안의 남은 적으로 다시 겨눈다");
        }

        // DamageApplicationSystem 은 DeadTag 가 붙는 순간부터 데미지를 뽑지 않는다 —
        // "죽었지만 아직 파괴 전"인 창에 도착하면 니들이 통째로 증발한다. 그 창까지 덮는다.
        [Test]
        public void Retarget_TargetTaggedDead_RepicksBeforeDestruction()
        {
            var dying = CreateEnemy(new float3(3f, 0f, 0f));
            var other = CreateEnemy(new float3(2f, 0f, 1f));
            var proj = CreateHomingProjectile(float3.zero, dying, retargetTileRange: 4);

            _em.AddComponent<DeadTag>(dying); // 파괴는 아직 안 됨
            Tick();

            Assert.IsTrue(_em.Exists(proj));
            Assert.AreEqual(other, _em.GetComponentData<ProjectileState>(proj).target,
                "DeadTag 만 붙은 시체도 재조준 트리거다");
        }

        [Test]
        public void Retarget_NoCandidateInRange_Destroys()
        {
            var doomed = CreateEnemy(new float3(1f, 0f, 0f));
            CreateEnemy(new float3(40f, 0f, 0f)); // 반경 밖
            var proj = CreateHomingProjectile(float3.zero, doomed, retargetTileRange: 3);

            _em.DestroyEntity(doomed);
            Tick();

            Assert.IsFalse(_em.Exists(proj), "반경 안에 후보가 없으면 기존대로 소멸");
        }

        // 무회귀 핀 — 화살 등 기존 투사체(retargetTileRange 0)는 예전 그대로 사라진다.
        [Test]
        public void Retarget_Disabled_KeepsLegacyDestroyBehaviour()
        {
            var doomed = CreateEnemy(new float3(1f, 0f, 0f));
            CreateEnemy(new float3(2f, 0f, 0f)); // 바로 옆에 후보가 있어도
            var proj = CreateHomingProjectile(float3.zero, doomed, retargetTileRange: 0);

            _em.DestroyEntity(doomed);
            Tick();

            Assert.IsFalse(_em.Exists(proj), "opt-in 하지 않은 투사체는 재조준하지 않는다");
        }

        // ── ② 방향탄 bounce ────────────────────────────────────────────────

        [Test]
        public void DirectionalBounce_OnPierceSpent_SwitchesToHoming()
        {
            var victim = CreateEnemy(new float3(1f, 0f, 0f));
            var next = CreateEnemy(new float3(2f, 0f, 0f));

            var proj = _em.CreateEntity();
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0.9f, 0f, 0f)));
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, new ProjectileState
            {
                movement = MovementKind.DirectionalLinear,
                payload = PayloadKind.PathHit,
                direction = new float2(1f, 0f),
                maxDistance = 10f,
                prevPos = float3.zero,
                damage = 20f,
                speed = 8f,
                hitThreshold = 0.6f,
                pierceRemaining = 1,      // 관통 없음(머신건 탄과 동일)
                bounceRemaining = 2,
                bounceTileRange = 4,
                bounceDamageMul = 0.5f,
            });
            // BattleBridge 가 PathHit 스폰 시 미리 붙이는 버퍼(:3409). 없으면 ECB 의
            // AppendToBuffer 가 playback 중 끊겨 **뒤따르는 SetComponent/DestroyEntity 가
            // 통째로 유실**된다 — "데미지는 들어갔는데 상태만 안 바뀐" 유령 증상이 된다.
            _em.AddBuffer<PathHitRecord>(proj);

            Tick();

            Assert.IsTrue(_em.Exists(proj), "튕길 대상이 있으면 살아남는다");
            var st = _em.GetComponentData<ProjectileState>(proj);
            Assert.Greater(_em.GetBuffer<IncomingDamage>(victim).Length, 0, "첫 적은 정상 피격");
            Assert.AreEqual(0, st.pierceRemaining, "pierce 예산 소진");
            Assert.AreEqual(MovementKind.HomingToEntity, st.movement, "방향 → 호밍 전환");
            Assert.AreEqual(PayloadKind.SingleSplash, st.payload, "스윕 → 단일 착탄 전환");
            Assert.AreEqual(next, st.target, "맞힌 적이 아닌 다음 적을 겨눈다");
            Assert.AreEqual(1, st.bounceRemaining, "홉을 하나 소비한다");
            Assert.AreEqual(10f, st.damage, 1e-3f, "bounceDamageMul 감쇠");
        }

        [Test]
        public void DirectionalBounce_NoCandidate_Destroys()
        {
            CreateEnemy(new float3(1f, 0f, 0f));

            var proj = _em.CreateEntity();
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0.9f, 0f, 0f)));
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, new ProjectileState
            {
                movement = MovementKind.DirectionalLinear,
                payload = PayloadKind.PathHit,
                direction = new float2(1f, 0f),
                maxDistance = 10f,
                prevPos = float3.zero,
                damage = 20f,
                speed = 8f,
                hitThreshold = 0.6f,
                pierceRemaining = 1,
                bounceRemaining = 2,
                bounceTileRange = 4,
                bounceDamageMul = 1f,
            });
            _em.AddBuffer<PathHitRecord>(proj);

            Tick();

            Assert.IsFalse(_em.Exists(proj), "맞힐 적이 하나뿐이면 튕길 곳이 없어 소멸");
        }
    }
}
