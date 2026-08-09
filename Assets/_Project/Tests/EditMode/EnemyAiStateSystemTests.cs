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

        // target-persistence unit 2 (D2) — **계약이 뒤집혔다.**
        //
        // 예전 기대값은 `Marching` 이었고 근거는 "락 때문에 발사를 못 하니 영구 정지를 막으려면
        // 걸어가야 한다"였다. 그런데 그 조합이 정확히 B2 다 — 적이 **바로 옆 방어유닛을 두고
        // 골로 걸어간다**. 락을 놓지 않는 것이 전제였고, 이제 그 전제가 사라졌다.
        //
        // D2 이후: 사거리 이탈은 락 해제 사유다 → 사거리 안의 다른 디펜더를 새로 잡고 `Engaging`.
        // 옛 근거였던 "영구 정지 방지"도 함께 만족된다(멈추지도, 지나치지도 않는다).
        [Test]
        public void Focus_LockOutOfRange_OtherNear_ReleasesAndEngages()
        {
            var far = MakeDefender(20f);
            MakeDefender(2f); // near, 비-락
            var enemy = MakeEnemy(0f, 5f, EnemyTargetMode.FocusUntilDead);
            _em.SetComponentData(enemy, new FocusTarget { current = far });
            _sim.Update();
            Assert.AreEqual(AiState.Engaging, StateOf(enemy),
                "사거리 안에 대상이 있으면 락을 놓고 교전한다 (Marching = B2)");
        }
    }
}
