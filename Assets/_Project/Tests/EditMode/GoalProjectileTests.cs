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

        // battle-structures unit 4(리뷰 M-d) — 손 사본 대신 공용 픽스처 빌더. 빌더와 브리지
        // 산물의 동일성은 GoalTowerArchetypeTests 가 컴포넌트 집합 단정으로 강제한다.
        private Entity MakeGoal(float3 pos, float m = 300f)
            => StructureFixtures.MakeGoalTower(_em, pos, m);

        // battle-structures unit 9 — FactionTag 는 선택이 아니다. 광역 피해자 풀이 그것으로
        // 진영을 가르고, 프로덕션 스폰 5경로 전부 붙인다(적 :7674 · 방어 :6172 · 순찰 :6403 ·
        // 골 :4917 · 거점 :4947). 픽스처가 빠뜨리면 «프로덕션에 존재할 수 없는 상태» 를 테스트한다.
        private Entity MakeEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            return e;
        }

        private Entity MakeDefender(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            return e;
        }

        // 적 거점 — 스폰(BattleBridge:4940~4947)의 광역 피해자로서 필요한 최소 집합.
        private Entity MakeEnemyStructure(float3 pos, Faction faction)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new StructureTag
            {
                cell = new int2((int)pos.x, (int)pos.z),
                faction = faction,
            });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new FactionTag { value = faction });
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

        // ───────────────── unit 9 — 진영 대칭 (적 풀에 적 거점 편입) ─────────────────

        // 고치는 증상: 메테오가 적 마음 위에 정확히 떨어져도 0 데미지. 방어 측은
        // goal-tower-siege unit 2 가 고쳤지만 적 측은 그대로 남아 있었다.
        [Test]
        public void TileAoe_EnemyFaction_IncludesEnemyStructures()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            var enemyCore = MakeEnemyStructure(new float3(2f, 0f, 0f), Faction.EnemyCore);
            var enemyInstinct = MakeEnemyStructure(new float3(2f, 0f, 1f), Faction.EnemyInstinct);
            var enemy = MakeEnemy(new float3(1f, 0f, 0f));
            var goal = MakeGoal(new float3(3f, 0f, 0f));
            var defender = MakeDefender(new float3(2f, 0f, -1f));

            SpawnTileAoe(targetFaction: ProjectileTargetFaction.Enemy);
            Tick();

            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemyCore).Length,
                "방어 광역이 적 마음을 깎는다 — unit 9 이전엔 0 데미지였다");
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemyInstinct).Length,
                "적 본능도 같다 — 종류가 아니라 진영으로 가른다");
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(enemy).Length,
                "기존 적 유닛 피해자는 그대로");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(goal).Length,
                "자기편 오폭 금지 — 통합 풀의 최대 위험이 여기서 잡힌다");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(defender).Length,
                "방어 유닛도 자기편이다");
        }

        // 반대 방향 — 보스 광역이 적 거점을 때리지 않는다. 통합 풀에서 마스크가 한쪽만
        // 맞으면 이쪽이 조용히 깨진다(보스가 자기 본능을 부수는 형태).
        [Test]
        public void TileAoe_DefenderFaction_ExcludesEnemyStructures()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            var enemyInstinct = MakeEnemyStructure(new float3(2f, 0f, 0f), Faction.EnemyInstinct);
            var goal = MakeGoal(new float3(2f, 0f, 1f));

            SpawnTileAoe(targetFaction: ProjectileTargetFaction.Defender);
            Tick();

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(enemyInstinct).Length,
                "보스 광역이 자기 진영 본능을 부수지 않는다");
            Assert.AreEqual(1, _em.GetBuffer<IncomingDamage>(goal).Length,
                "골은 여전히 맞는다 — GoalTowerTag 특례 은퇴 후에도 :95 가 고친 동작이 보존된다");
        }

        // 방벽은 어느 풀에도 없었고 지금도 없다. 통합 시 BlockingHazard 비트가
        // AnyDefender/AnyEnemy 어디에도 없다는 사실이 그것을 보장한다.
        [Test]
        public void TileAoe_BlockingHazard_IsVictimOfNeitherPool()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            var barrier = _em.CreateEntity();
            _em.AddComponentData(barrier, LocalTransform.FromPosition(new float3(2f, 0f, 0f)));
            _em.AddComponentData(barrier, new Health { value = 80f, max = 80f });
            _em.AddBuffer<IncomingDamage>(barrier);
            _em.AddComponentData(barrier, new FactionTag { value = Faction.BlockingHazard });

            SpawnTileAoe(targetFaction: ProjectileTargetFaction.Enemy);
            Tick();
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(barrier).Length, "적 풀 아님");

            SpawnTileAoe(targetFaction: ProjectileTargetFaction.Defender);
            Tick();
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(barrier).Length, "방어 풀도 아님 (무회귀)");
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
