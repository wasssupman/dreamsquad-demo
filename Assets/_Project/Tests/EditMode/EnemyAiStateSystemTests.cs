using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // enemy-ai-fsm Unit 1 — EnemyAiStateSystem.OnUpdate 통합. 전이가 AttackSystem fire 조건을
    // 미러하는지(특히 FocusUntilDead 락)와 aggro standoff/chase 전이를 고정한다.
    public class EnemyAiStateSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _sim;

        [SetUp]
        public void SetUp()
        {
            _world = new World("EnemyAiStateTestWorld");
            _em = _world.EntityManager;
            _sim = _world.CreateSystemManaged<SimulationSystemGroup>();
            _sim.AddSystemToUpdateList(_world.CreateSystem<EnemyAiStateSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeDefender(float x, DefenderClass cls = DefenderClass.Guardian)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddComponentData(e, new DefenderClassTag { value = cls });
            return e;
        }

        private Entity MakeEnemy(float x, float range, EnemyTargetMode mode = EnemyTargetMode.Nearest)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddComponentData(e, new AttackState
            {
                range = range, cooldownDuration = 1f, attackTargetCount = 1,
                targetMask = (int)Faction.Defender,
            });
            _em.AddComponentData(e, new EnemyBehavior { targetMode = mode });
            _em.AddComponentData(e, new EnemyTargetFilter { classMask = -1, priorityClass = -1 });
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            if (mode == EnemyTargetMode.FocusUntilDead)
                _em.AddComponentData(e, new FocusTarget { current = Entity.Null });
            return e;
        }

        private AiState StateOf(Entity e) => _em.GetComponentData<EnemyAiState>(e).value;

        [Test]
        public void NoAggro_DefenderInRange_Engaging()
        {
            MakeDefender(2f);
            var enemy = MakeEnemy(0f, 5f);
            _sim.Update();
            Assert.AreEqual(AiState.Engaging, StateOf(enemy));
        }

        [Test]
        public void NoAggro_DefenderOutOfRange_Marching()
        {
            MakeDefender(20f);
            var enemy = MakeEnemy(0f, 5f);
            _sim.Update();
            Assert.AreEqual(AiState.Marching, StateOf(enemy));
        }

        [Test]
        public void Aggro_GuardianInRange_Standoff()
        {
            var g = MakeDefender(2f);
            var enemy = MakeEnemy(0f, 5f);
            _em.AddComponentData(enemy, new Aggroed { guardian = g });
            _sim.Update();
            Assert.AreEqual(AiState.Standoff, StateOf(enemy));
        }

        [Test]
        public void Aggro_GuardianOutOfRange_Chasing()
        {
            var g = MakeDefender(20f);
            var enemy = MakeEnemy(0f, 5f);
            _em.AddComponentData(enemy, new Aggroed { guardian = g });
            _sim.Update();
            Assert.AreEqual(AiState.Chasing, StateOf(enemy));
        }

        // H2 회귀 가드 — FocusUntilDead 락 타겟이 사거리 밖이면, 다른 디펜더가 사거리 안에 있어도
        // AttackSystem 은 락 때문에 발사하지 않으므로 전이는 Marching 이어야 한다(영구 정지 방지).
        [Test]
        public void Focus_LockOutOfRange_OtherNear_Marching()
        {
            var far = MakeDefender(20f);
            MakeDefender(2f); // near, 비-락
            var enemy = MakeEnemy(0f, 5f, EnemyTargetMode.FocusUntilDead);
            _em.SetComponentData(enemy, new FocusTarget { current = far });
            _sim.Update();
            Assert.AreEqual(AiState.Marching, StateOf(enemy));
        }
    }
}
