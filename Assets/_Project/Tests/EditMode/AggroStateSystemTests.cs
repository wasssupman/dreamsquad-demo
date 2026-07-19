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
            // aggro-tile-chase unit 1 — 전투수단 없는 적은 획득 거부되므로, 실데이터처럼
            // 도발 프로파일을 기본 부여(모든 적 에셋이 aggroAttackDamage>0 로 베이크됨).
            _em.AddComponentData(e, new AggroAttackProfile { damage = 5f, cooldown = 1f, range = 1f });
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

        // ── aggro-tile-chase unit 1 — 획득 게이트 + chase field ──────────────

        [Test]
        public void NoAttackNoProfile_Refused()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = _em.CreateEntity(); // MakeEnemy 미사용 — 전투수단 없음
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(1, 0, 0)));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "전투수단 없는 적은 획득 거부");
        }

        // 4×3 flow field: y1 행만 walk (goal (0,1)). 나머지 벽(zero-flow).
        private FlowFieldSingleton MakeFlowField()
        {
            int2 gridSize = new int2(4, 3);
            var flow = new NativeArray<float2>(12, Allocator.Persistent);
            var dist = new NativeArray<int>(12, Allocator.Persistent);
            for (int i = 0; i < 12; i++) { flow[i] = float2.zero; dist[i] = int.MaxValue; }
            for (int x = 0; x < 4; x++)
            {
                int idx = 1 * 4 + x;
                dist[idx] = x;
                flow[idx] = x == 0 ? float2.zero : new float2(-1f, 0f); // goal (0,1) 는 zero(특례)
            }
            var f = new FlowFieldSingleton
            {
                flow = flow, dist = dist, gridSize = gridSize,
                goalCell = new int2(0, 1), tileSize = 1f, origin = float3.zero,
            };
            var s = _em.CreateEntity();
            _em.AddComponentData(s, f);
            return f;
        }

        [Test]
        public void ChaseField_AttachedWhenReachable_RemovedOnRelease()
        {
            var f = MakeFlowField();
            var g = MakeGuardian(2, new float3(2f, 0f, 0f));   // 셀 (2,0) — 통로 y1 인접 (range1 소스 존재)
            var e = MakeEnemy(new float3(3f, 0f, 1f));          // 셀 (3,1) — 통로 위
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e), "도달 가능 — 획득");
            Assert.IsTrue(_em.HasBuffer<AggroChaseCell>(e), "chase field 부착");
            var buf = _em.GetBuffer<AggroChaseCell>(e);
            Assert.AreEqual(12, buf.Length);
            Assert.AreNotEqual(int.MaxValue, buf[1 * 4 + 3].dist, "적 셀 도달 가능");

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "가디언 사망 해제");
            Assert.IsFalse(_em.HasBuffer<AggroChaseCell>(e), "해제 시 chase field 도 제거");
            f.Dispose();
        }

        [Test]
        public void ChaseField_UnreachableEnemy_Refused()
        {
            var f = MakeFlowField();
            var g = MakeGuardian(2, new float3(2f, 0f, 0f));    // 통로 인접 — 목적지 후보는 있음
            var e = MakeEnemy(new float3(3f, 0f, 2f));          // 셀 (3,2) — 벽 위(BFS 도달 불가)
            Hit(g, e);
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "도달 불가(벽/고립 셀) — 거부");
            f.Dispose();
        }

        // (무후보 거부 srcCount==0 경로는 순수함수 테스트 PerpendicularPin_Range1_NoSources 가 커버 —
        //  3행 합성 필드에선 range1 무후보 가디언 배치가 기하적으로 성립하지 않는다.)

        [Test]
        public void TauntAttack_GrantedOnAggro_StrippedOnRelease()
        {
            var g = MakeGuardian(2, float3.zero);
            var runner = MakeEnemy(new float3(1, 0, 0)); // AttackState/outputs 없음 (프로파일은 MakeEnemy 기본)
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
