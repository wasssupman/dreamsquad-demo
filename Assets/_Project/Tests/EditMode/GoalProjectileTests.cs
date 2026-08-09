using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // goal-stability unit 3 — 원거리 개통: 직격 호밍(SingleSplash)은 타겟 엔티티 직결이라
    // 골도 그대로 맞고, Defender 풀 TileAoe 는 골을 피해자 풀에 포함한다. Enemy 풀
    // (플레이어 메테오/방어 광역)은 무변경. 픽스처는 ProjectileSystemTests 동형.
    public class GoalProjectileTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("GoalProjectileTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // battle-structures unit 0 — 라이브 아키타입으로 재조준했다. goal-stability 의
        // GoalPoint 골 풀이 제거되어 광역 포함은 defender 풀의 WithAny<DefenderUnitTag,
        // GoalTowerTag> 가 담당한다. 즉 이 테스트는 이제 EnsureGoalTowers 가 실제로 만드는
        // 아키타입(GoalTowerTag + Faction.DefenderCore)을 검증한다. 풀 포함은 태그가
        // 결정하므로 진영 비트는 판정에 관여하지 않지만, 픽스처는 생산 산물과 일치시킨다.
        private Entity MakeGoal(float3 pos, float m = 300f)
        {
            var e = _em.CreateEntity();
            _em.AddComponent<GoalTowerTag>(e);
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new Health { value = m, max = m });
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderCore });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private Entity MakeEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private Entity MakeDefender(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        [Test]
        public void Homing_DirectHit_DamagesGoal()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileMoveSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            var goal = MakeGoal(new float3(2f, 0f, 0f));
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                target = goal,
                speed = 10f,
                damage = 7f,
                hitThreshold = 0.2f,
            });

            for (int i = 0; i < 30 && _em.Exists(proj); i++) Tick();

            var damage = _em.GetBuffer<IncomingDamage>(goal);
            Assert.AreEqual(1, damage.Length, "직격 호밍은 타겟 엔티티 직결 — 골도 맞는다");
            Assert.AreEqual(7f, damage[0].amount, 1e-4f);
        }

        [Test]
        public void TileAoe_DefenderFaction_IncludesGoal()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            var goal = MakeGoal(new float3(2f, 0f, 0f));
            var defender = MakeDefender(new float3(3f, 0f, 0f));
            var enemy = MakeEnemy(new float3(2f, 0f, 1f));

            SpawnTileAoe(targetFaction: ProjectileTargetFaction.Defender);
            Tick();

            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(goal).Length,
                "Defender 풀 광역은 골을 포함한다");
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(defender).Length,
                "기존 defender 피해자도 그대로");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "Defender 풀 광역이 적을 때리지 않는 것도 그대로");
        }

        [Test]
        public void TileAoe_EnemyFaction_IgnoresGoal()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            var goal = MakeGoal(new float3(2f, 0f, 0f));
            var enemy = MakeEnemy(new float3(2f, 0f, 1f));

            SpawnTileAoe(targetFaction: ProjectileTargetFaction.Enemy);
            Tick();

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(goal).Length,
                "Enemy 풀(레거시 — 플레이어 메테오/방어 광역)은 골 무관 (무회귀)");
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemy).Length);
        }

        private void SpawnTileAoe(ProjectileTargetFaction targetFaction)
        {
            var proj = _em.CreateEntity();
            _em.AddComponent<ProjectileTag>(proj);
            _em.AddComponentData(proj, LocalTransform.FromPosition(new float3(2f, 0f, 0f)));
            _em.AddComponentData(proj, new ProjectileState
            {
                payload = PayloadKind.TileAoe,
                targetFaction = targetFaction,
                impact = new float3(2f, 0f, 0f),
                impactTileRange = 1,
                damage = 5f,
                impactReached = true,
            });
        }
    }
}
