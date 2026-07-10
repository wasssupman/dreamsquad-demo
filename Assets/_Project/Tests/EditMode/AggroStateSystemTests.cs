using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // aggro-targeting Unit 12/14 — 히트 구동 AggroStateSystem 회귀. 근접 즉시 배정을
    // 폐기했으므로 획득은 AggroHitEvent 드레인으로만 일어난다. capacity 게이트, 선점,
    // 사망 해제, orphan 해제, H1(같은 틱 상한 초과 방지), 도발 grant/strip 을 고정.
    public class AggroStateSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<AggroHitEvent> _hitQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AggroStateTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AggroStateSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<TauntAttackGrantSystem>());

            // Combat→Effects 히트 채널 싱글턴(테스트 하네스가 브리지 역할).
            _hitQueue = new NativeQueue<AggroHitEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new AggroHitEventsSingleton { queue = _hitQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_hitQueue.IsCreated) _hitQueue.Dispose();
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeGuardian(int capacity, float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new AggroCapacity { max = capacity, held = 0 });
            return e;
        }

        private Entity MakeEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            return e;
        }

        private void Hit(Entity guardian, Entity enemy)
            => _hitQueue.Enqueue(new AggroHitEvent { guardian = guardian, enemy = enemy });

        private int AggroedCount()
        {
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<Aggroed>());
            return q.CalculateEntityCount();
        }

        [Test]
        public void HitDrivenAcquire_AggrosHitEnemy()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e), "명중한 적이 어그로됨");
            Assert.AreEqual(g, _em.GetComponentData<Aggroed>(e).guardian);
        }

        [Test]
        public void CapacityCap_SameTick_DoesNotExceed()
        {
            var g = MakeGuardian(2, float3.zero);
            var e1 = MakeEnemy(new float3(1, 0, 0));
            var e2 = MakeEnemy(new float3(2, 0, 0));
            var e3 = MakeEnemy(new float3(3, 0, 0));
            Hit(g, e1); Hit(g, e2); Hit(g, e3); // 같은 틱 3 히트, 상한 2
            _simGroup.Update();
            Assert.AreEqual(2, AggroedCount(), "capacity 만큼만 어그로");
        }

        [Test]
        public void CapacityCap_AcrossTicks_RespectsRunningHeld()
        {
            // critic H1 — held 가 이전 틱 값이어도 같은 틱 드레인이 상한을 소프트 초과하면 안 됨.
            var g = MakeGuardian(2, float3.zero);
            var e1 = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e1);
            _simGroup.Update(); // held 1
            Assert.AreEqual(1, AggroedCount());

            var e2 = MakeEnemy(new float3(2, 0, 0));
            var e3 = MakeEnemy(new float3(3, 0, 0));
            Hit(g, e2); Hit(g, e3); // 남은 슬롯 1, 히트 2
            _simGroup.Update();
            Assert.AreEqual(2, AggroedCount(), "held 1 + 여유 1 → 총 2 (3번째 거절)");
            // held 는 1-tick 지연(Pass 2 재계산은 드레인 전 커밋분 기준). 한 틱 더 돌리면 수렴.
            _simGroup.Update();
            Assert.AreEqual(2, _em.GetComponentData<AggroCapacity>(g).held, "held 재계산 수렴");
        }

        [Test]
        public void Preemption_SameTick_FirstGuardianWins()
        {
            var g1 = MakeGuardian(2, float3.zero);
            var g2 = MakeGuardian(2, new float3(1, 0, 0));
            var e = MakeEnemy(new float3(0.5f, 0, 0));
            Hit(g1, e); Hit(g2, e); // 같은 적, g1 먼저
            _simGroup.Update();
            Assert.AreEqual(g1, _em.GetComponentData<Aggroed>(e).guardian, "먼저 때린 가디언이 선점");
        }

        [Test]
        public void Preemption_AcrossTicks_KeepsFirstGuardian()
        {
            var g1 = MakeGuardian(2, float3.zero);
            var g2 = MakeGuardian(2, new float3(1, 0, 0));
            var e = MakeEnemy(new float3(0.5f, 0, 0));
            Hit(g1, e);
            _simGroup.Update();
            Hit(g2, e); // 이미 어그로된 적
            _simGroup.Update();
            Assert.AreEqual(g1, _em.GetComponentData<Aggroed>(e).guardian, "이미 어그로된 적은 유지");
        }

        [Test]
        public void GuardianDeath_ReleasesAggro()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e));

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "가디언 사망 → 해제");
        }

        [Test]
        public void LastGuardianDestroyed_ReleasesOrphan()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e));

            _em.DestroyEntity(g); // 마지막 가디언 소멸 — orphan 도 해제돼야
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "orphan 어그로 해제");
        }

        [Test]
        public void TauntAttack_GrantedOnAggro_StrippedOnRelease()
        {
            var g = MakeGuardian(2, float3.zero);
            var runner = MakeEnemy(new float3(1, 0, 0)); // AttackState/outputs 없음
            _em.AddComponentData(runner, new AggroAttackProfile { damage = 5f, cooldown = 1f, range = 1f });
            Hit(g, runner);

            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(runner));
            Assert.IsTrue(_em.HasComponent<AttackState>(runner), "도발 공격 부여");
            Assert.IsTrue(_em.HasComponent<TauntAttackGranted>(runner));
            Assert.IsTrue(_em.HasBuffer<AttackOutputElement>(runner));

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(runner));
            Assert.IsFalse(_em.HasComponent<AttackState>(runner), "해제 시 도발 공격 strip");
            Assert.IsFalse(_em.HasComponent<TauntAttackGranted>(runner));
        }
    }
}
