using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // enemy-ai-fsm 3a — AttackSystem fire 게이트: 적은 EnemyAiState ∈ {Engaging, Standoff} 에서만 발사.
    // Marching/Chasing 이면 cooldown 은 돌되 발사하지 않는다. 디펜더는 상태머신 대상이 아니라 항상 발사.
    public class AttackSystemStateGateTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _sim;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AttackStateGateTestWorld");
            _em = _world.EntityManager;
            _sim = _world.CreateSystemManaged<SimulationSystemGroup>();
            _sim.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeDefender(float x)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100000f, max = 100000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private Entity MakeEnemy(float x, AiState state)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100000f, max = 100000f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0, 0)));
            _em.AddComponentData(e, new AttackState
            {
                range = 5f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Defender,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 10f } });
            _em.AddComponentData(e, new EnemyBehavior { targetMode = EnemyTargetMode.Nearest });
            _em.AddComponentData(e, new EnemyTargetFilter { classMask = -1, priorityClass = -1 });
            _em.AddComponentData(e, new EnemyAiState { value = state });
            return e;
        }

        private int Incoming(Entity e) => _em.GetBuffer<IncomingDamage>(e).Length;

        private void Update()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.1, 0.1f));
            _sim.Update();
        }

        [Test]
        public void Marching_Enemy_DoesNotFire()
        {
            var def = MakeDefender(0f);
            MakeEnemy(2f, AiState.Marching); // 사거리 내 디펜더 존재하나 상태가 Marching
            Update();
            Assert.AreEqual(0, Incoming(def), "Marching 적은 fire 안 함");
        }

        [Test]
        public void Engaging_Enemy_Fires()
        {
            var def = MakeDefender(0f);
            MakeEnemy(2f, AiState.Engaging);
            Update();
            Assert.Greater(Incoming(def), 0, "Engaging 적은 fire");
        }

        [Test]
        public void Standoff_Enemy_Fires()
        {
            var def = MakeDefender(0f);
            MakeEnemy(2f, AiState.Standoff);
            Update();
            Assert.Greater(Incoming(def), 0, "Standoff 적은 fire");
        }

        [Test]
        public void Chasing_Enemy_DoesNotFire()
        {
            var def = MakeDefender(0f);
            MakeEnemy(2f, AiState.Chasing);
            Update();
            Assert.AreEqual(0, Incoming(def), "Chasing 적은 fire 안 함(가디언 사거리 밖 추격 중)");
        }

        // M1 회귀 가드 — Marching 중엔 발사 스킵하되 cooldown 은 리셋하지 않아(fire-ready),
        // Engaging 으로 전이하면 다음 틱 즉발한다.
        [Test]
        public void Marching_KeepsCooldownReady_FiresOnEngagingTransition()
        {
            var def = MakeDefender(0f);
            var enemy = MakeEnemy(2f, AiState.Marching);
            Update();
            Assert.AreEqual(0, Incoming(def), "Marching 중 미발사");
            Assert.AreEqual(0f, _em.GetComponentData<AttackState>(enemy).cooldownRemaining, 1e-4f,
                "Marching 은 START 를 스킵하므로 cooldown 미리셋(fire-ready 유지)");

            _em.SetComponentData(enemy, new EnemyAiState { value = AiState.Engaging });
            Update();
            Assert.Greater(Incoming(def), 0, "Engaging 전이 직후 즉발");
        }
    }
}
