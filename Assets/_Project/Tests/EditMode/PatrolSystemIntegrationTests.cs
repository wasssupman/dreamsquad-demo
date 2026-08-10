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

        // unit 9 — 담당 구역 반경의 **유일한 출처는 AttackState.range** 다. 이 헬퍼가
        // range 를 2 로 두는 것은 이전 SummonerState.leashTileRadius = 2 를 그대로 옮긴
        // 것이고, 아래 구역 게이트 테스트들의 "안(2) / 밖(4)" 기준이 보존된다.
        private Entity CreateSummoner(
            float cooldownRemaining, Entity current, bool hasSummonedOnce = true, float range = 2f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new AttackState
            {
                range = range,
                cooldownDuration = 5f,
                cooldownRemaining = cooldownRemaining,
                attackTargetCount = 1,
                targetMask = (int)Faction.EnemyUnit,
            });
            _em.AddComponentData(e, new SummonerState
            {
                patrolDataIndex = 0,
                current = current,
                hasSummonedOnce = hasSummonedOnce,
            });
            return e;
        }

        // 타겟 스냅샷 조건(FactionTag + Health + LocalTransform)을 채운 적.
        private Entity CreateEnemyAt(float x)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0f, 0f)));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 50f, max = 50f });
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

            Assert.AreEqual(1, CarrierCount(), "게이트가 소비된 뒤엔 적 없이도 재소환한다");
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
            // 계약 9 — `current != Entity.Null` 만 보면 파괴된 순찰병의 stale 핸들로
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

        // ───────────────── 초회 게이트(거점 구역 기준) ─────────────────

        [Test]
        public void First_Summon_Waits_Until_An_Enemy_Enters_The_Area()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            var summoner = CreateSummoner(cooldownRemaining: 0f, current: Entity.Null, hasSummonedOnce: false);

            Tick();

            Assert.AreEqual(0, CarrierCount(), "적이 없으면 첫 순찰병을 내지 않는다");
            Assert.AreEqual(0f, _em.GetComponentData<AttackState>(summoner).cooldownRemaining,
                "게이트가 닫혀 있으면 쿨다운을 리셋하지 않는다 — 적이 들어온 프레임에 즉시 반응해야 한다");
        }

        [Test]
        public void First_Summon_Fires_When_Enemy_Is_Inside_The_Area()
        {
            // 소환사 셀 (1,0), 반경 2 → 구역 x∈[-1,3]. 적 (2,0) 은 안.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            CreateSummoner(cooldownRemaining: 0f, current: Entity.Null, hasSummonedOnce: false);
            CreateEnemyAt(2f);

            Tick();

            Assert.AreEqual(1, CarrierCount(), "구역 안 적이 첫 소환을 연다");
        }

        // unit 9 — 구역이 **소환사 공격범위에서 파생**되는지. 위 두 테스트가 "반경 2 에서
        // 2 는 안, 4 는 밖"을 고정하므로, range 만 넓혀 같은 적(4)이 안으로 들어오면 반경의
        // 출처가 range 라는 것이 증명된다. 상수를 지운 자리에 상수를 다시 심지 않는 축이다.
        [Test]
        public void Cover_Radius_Comes_From_The_Summoner_Attack_Range()
        {
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            CreateSummoner(cooldownRemaining: 0f, current: Entity.Null, hasSummonedOnce: false, range: 4f);
            CreateEnemyAt(4f);   // 반경 2 였다면 밖(Chebyshev 3), 반경 4 면 안

            Tick();

            Assert.AreEqual(1, CarrierCount(),
                "공격범위를 넓히면 담당 구역이 함께 넓어져야 한다 — 숫자가 하나뿐이라는 뜻");
        }

        [Test]
        public void First_Summon_Ignores_Enemy_Outside_The_Area()
        {
            // 적 (4,0) 은 소환사 (1,0) 기준 Chebyshev 3 > 반경 2 → 구역 밖.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            CreateSummoner(cooldownRemaining: 0f, current: Entity.Null, hasSummonedOnce: false);
            CreateEnemyAt(4f);

            Tick();

            Assert.AreEqual(0, CarrierCount(), "구역 밖 적은 소환 사유가 아니다");
        }

        [Test]
        public void First_Summon_Answers_Siege_Enemy_In_The_Area()
        {
            // goal-tower-siege unit 1 — **단언이 뒤집혔다**(구: PastGoal 적은 무시).
            // 그 태그는 이제 "유출 대기"(곧 사라질 적)가 아니라 "골에 붙어 타워를 때리는 중"이다.
            // 골을 두들기는 적이야말로 순찰을 부를 이유이므로 첫 소환 게이트가 열려야 한다.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            CreateSummoner(cooldownRemaining: 0f, current: Entity.Null, hasSummonedOnce: false);
            var enemy = CreateEnemyAt(2f);
            _em.AddComponent<PastGoalTag>(enemy);

            Tick();

            Assert.AreEqual(1, CarrierCount(), "공성 중인 적은 거점 구역 게이트를 연다");
        }

        [Test]
        public void Respawn_Ignores_The_Gate_Once_Consumed()
        {
            // "한 번 만들면 유지" — 게이트 소비 후엔 적이 사라져도 재소환이 끊기지 않는다.
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            CreateLinearFlowField();
            CreateSummoner(cooldownRemaining: 0f, current: Entity.Null, hasSummonedOnce: true);

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
