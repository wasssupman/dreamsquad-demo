using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // summon-patrol-defender — 순수 함수 밖의 세 지점. 셋 다 **조용히 깨진다**(예외·로그 0)라
    // 회귀 테스트가 없으면 다음 사람이 지워도 리뷰에서 잡히지 않는다.
    //   ① MovementSystem goal 게이트 — 스펙이 스스로 "최대 회귀 위험"이라 지목한 곳
    //   ② PatrolLifecycleSystem 연쇄 소멸
    //   ③ AttackSystem blind 소환 순환(1기 고정 / stale 핸들 재소환)
    public class PatrolSystemIntegrationTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity = Entity.Null;

        [SetUp]
        public void SetUp()
        {
            _world = new World("PatrolSystemIntegrationTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity)
                && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // 5x1 직선. goal = (4,0).
        private void CreateLinearFlowField(int width = 5)
        {
            int n = width;
            var flow = new NativeArray<float2>(n, Allocator.Persistent);
            var dist = new NativeArray<int>(n, Allocator.Persistent);
            for (int i = 0; i < width - 1; i++) { flow[i] = new float2(1, 0); dist[i] = (width - 1) - i; }
            flow[width - 1] = float2.zero; dist[width - 1] = 0;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(width, 1),
                goalCell = new int2(width - 1, 0),
                tileSize = 1f, version = 1,
            });
        }

        // ───────────────────────── ① goal 게이트 ─────────────────────────

        [Test]
        public void Patrol_On_Goal_Cell_Does_Not_Get_PastGoalTag()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());
            CreateLinearFlowField();

            // 거점 구역 안에 goal 셀이 들어온 상황. 게이트가 없으면 PastGoalTag 가 붙고
            // 그 순간 ⑴ MovementSystem(WithNone<PastGoalTag>)에서 영구 동결 ⑵ PastGoal
            // 파괴 루프는 AttackUnitTag 를 요구해 파괴도 안 됨 ⑶ SummonerState.current 가
            // 계속 유효해 남은 판 내내 재소환이 멈춘다.
            var patrol = _em.CreateEntity();
            _em.AddComponentData(patrol, LocalTransform.FromPosition(new float3(4f, 0f, 0f)));
            _em.AddComponentData(patrol, new PathFollowState { speed = 1f });
            _em.AddComponentData(patrol, new PatrolStep { dir = float2.zero });

            Tick();

            Assert.IsFalse(_em.HasComponent<PastGoalTag>(patrol),
                "순찰병은 goal 셀 위에서도 PastGoalTag 를 받으면 안 된다");
        }

        [Test]
        public void NonPatrol_On_Goal_Cell_Still_Gets_PastGoalTag()
        {
            // 대조군 — 게이트가 적의 누수 판정을 망가뜨리지 않았음을 같이 고정한다.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());
            CreateLinearFlowField();

            var enemy = _em.CreateEntity();
            _em.AddComponentData(enemy, LocalTransform.FromPosition(new float3(4f, 0f, 0f)));
            _em.AddComponentData(enemy, new PathFollowState { speed = 1f });

            Tick();

            Assert.IsTrue(_em.HasComponent<PastGoalTag>(enemy),
                "일반 적은 기존대로 goal 도달 처리돼야 한다");
        }

        // ───────────────────────── ② 연쇄 소멸 ─────────────────────────

        private (Entity owner, Entity patrol) CreateSummonerAndPatrol(float ownerHp = 100f)
        {
            var owner = _em.CreateEntity();
            _em.AddComponentData(owner, new Health { value = ownerHp, max = 100f });

            var patrol = _em.CreateEntity();
            _em.AddComponentData(patrol, new Health { value = 50f, max = 50f });
            _em.AddComponentData(patrol, new SummonedBy { owner = owner });
            return (owner, patrol);
        }

        [Test]
        public void Patrol_Survives_While_Owner_Is_Alive()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<PatrolLifecycleSystem>());
            var (_, patrol) = CreateSummonerAndPatrol();

            Tick();

            Assert.IsFalse(_em.HasComponent<DeadTag>(patrol));
        }

        [Test]
        public void Patrol_Dies_When_Owner_Gets_DeadTag()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<PatrolLifecycleSystem>());
            var (owner, patrol) = CreateSummonerAndPatrol();
            _em.AddComponent<DeadTag>(owner);

            Tick();

            Assert.IsTrue(_em.HasComponent<DeadTag>(patrol), "소환사 사망 → 순찰병도 사망");
        }

        [Test]
        public void Patrol_Dies_When_Owner_Entity_Is_Destroyed()
        {
            // ECB 로 파괴된 소환사(Exists=false). DeadTag 경로와 같은 결과여야 한다.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<PatrolLifecycleSystem>());
            var (owner, patrol) = CreateSummonerAndPatrol();
            _em.DestroyEntity(owner);

            Tick();

            Assert.IsTrue(_em.HasComponent<DeadTag>(patrol));
        }

        [Test]
        public void Patrol_Dies_When_Owner_Health_Hits_Zero()
        {
            // DeadTag 가 아직 안 붙은 프레임에도 HP<=0 이면 죽은 것으로 본다(3중 판정).
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<PatrolLifecycleSystem>());
            var (_, patrol) = CreateSummonerAndPatrol(ownerHp: 0f);

            Tick();

            Assert.IsTrue(_em.HasComponent<DeadTag>(patrol));
        }

        // ───────────────────────── ③ blind 소환 순환 ─────────────────────────

        private Entity CreateSummoner(float cooldownRemaining, Entity current)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new AttackState
            {
                range = 1f,
                cooldownDuration = 5f,
                cooldownRemaining = cooldownRemaining,
                attackTargetCount = 1,
                targetMask = (int)Faction.Enemy,
            });
            _em.AddComponentData(e, new SummonerState
            {
                patrolDataIndex = 0,
                leashTileRadius = 2,
                current = current,
            });
            return e;
        }

        private int CarrierCount()
        {
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<PatrolRequestCarrier>());
            return q.CalculateEntityCount();
        }

        [Test]
        public void Summoner_Stages_One_Request_When_No_Patrol_Alive()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            var summoner = CreateSummoner(cooldownRemaining: 0f, current: Entity.Null);

            Tick();

            Assert.AreEqual(1, CarrierCount(), "적이 없어도(blind) 쿨다운 만료에 소환 요청이 나가야 한다");
            Assert.AreEqual(5f, _em.GetComponentData<AttackState>(summoner).cooldownRemaining,
                "성사 여부와 무관하게 쿨다운이 리셋돼야 재스캔 스팸이 없다");
        }

        [Test]
        public void Summoner_Does_Not_Stage_While_Patrol_Alive()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            var alive = _em.CreateEntity();
            _em.AddComponentData(alive, new Health { value = 50f, max = 50f });
            CreateSummoner(cooldownRemaining: 0f, current: alive);

            Tick();

            Assert.AreEqual(0, CarrierCount(), "순찰병이 살아 있으면 소환을 건너뛴다(1기 고정)");
        }

        [Test]
        public void Summoner_Restages_When_Current_Handle_Is_Stale()
        {
            // 계약 8 — `current != Entity.Null` 만 보면 파괴된 순찰병의 stale 핸들로
            // 소환사가 영구 대기한다. Exists 까지 봐야 재소환이 돈다.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            var dead = _em.CreateEntity();
            _em.AddComponentData(dead, new Health { value = 50f, max = 50f });
            CreateSummoner(cooldownRemaining: 0f, current: dead);
            _em.DestroyEntity(dead);

            Tick();

            Assert.AreEqual(1, CarrierCount(), "stale 핸들이면 재소환해야 한다");
        }

        [Test]
        public void Summoner_Restages_When_Current_Is_Dead_But_Not_Destroyed()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            var dying = _em.CreateEntity();
            _em.AddComponentData(dying, new Health { value = 0f, max = 50f });
            _em.AddComponent<DeadTag>(dying);
            CreateSummoner(cooldownRemaining: 0f, current: dying);

            Tick();

            Assert.AreEqual(1, CarrierCount());
        }

        [Test]
        public void Summoner_Waits_While_Cooldown_Remains()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            CreateSummoner(cooldownRemaining: 2f, current: Entity.Null);

            Tick();

            Assert.AreEqual(0, CarrierCount(), "쿨다운 중엔 소환하지 않는다");
        }
    }
}
