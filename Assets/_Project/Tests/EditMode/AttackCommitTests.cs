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
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // target-persistence unit 0 — 공격 1회 타겟 커밋.
    //
    // 결함: START 게이트는 hitDelayRemaining==0 일 때만 걸리는데 타겟 선정 사슬은 매 프레임
    // 돌고 RESOLVE 는 그 프레임의 bestTarget 을 쓴다. 방어유닛 24/26 이 hitDelaySec 0.3 이라
    // 창이 상시 열려 있어 **A를 겨누고 B를 때린다**.
    //
    // 하네스는 FrontmostAttackLockTests 를 그대로 따르되 **frontmost 락이 없는** 평범한
    // 방어유닛을 쓴다 — 그래야 일반 커밋 블록이 발동한다.
    public class AttackCommitTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<UnitAttackVisualEvent> _attackEventQueue;
        private NativeQueue<EnemyCcEvent> _ccQueue;
        private Entity _fieldEntity;

        private const float HitDelay = 0.3f;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AttackCommitTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            _attackEventQueue = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new UnitAttackVisualEventsSingleton { queue = _attackEventQueue });
            _ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new EnemyCcEventsSingleton { queue = _ccQueue });

            CreateLinearFlowField();
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackEventQueue.IsCreated) _attackEventQueue.Dispose();
            if (_ccQueue.IsCreated) _ccQueue.Dispose();
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity) && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // wind-up 이 끝나 RESOLVE 가 나기까지 충분히 돌린다.
        private void TickThroughWindUp() { for (int i = 0; i < 25; i++) Tick(); }

        private void CreateLinearFlowField(int width = 12, float tileSize = 1f)
        {
            var flow = new NativeArray<float2>(width, Allocator.Persistent);
            var dist = new NativeArray<int>(width, Allocator.Persistent);
            for (int i = 0; i < width - 1; i++) { flow[i] = new float2(1, 0); dist[i] = (width - 1) - i; }
            flow[width - 1] = float2.zero; dist[width - 1] = 0;
            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(width, 1),
                goalCell = new int2(width - 1, 0),
                tileSize = tileSize, version = 1,
            });
        }

        // 근접(ProjectileRef 없음) 방어유닛 — RESOLVE 가 IncomingDamage 를 직접 넣는다.
        // frontmost 락도 DcAttackMod 도 주지 않는다: 일반 커밋 경로를 타야 한다.
        private Entity CreatePlainDefender(float3 pos, float range = 6f, float hitDelaySec = HitDelay)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(e, new Health { value = 10f, max = 10f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = range, cooldownDuration = 10f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Enemy, hitDelaySec = hitDelaySec,
            });
            _em.AddComponent<DefenderUnitTag>(e);
            var outputs = _em.AddBuffer<AttackOutputElement>(e);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 5f },
            });
            return e;
        }

        private Entity CreateEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private void MoveTo(Entity e, float3 pos) => _em.SetComponentData(e, LocalTransform.FromPosition(pos));

        private float IncomingSum(Entity e)
        {
            var buf = _em.GetBuffer<IncomingDamage>(e);
            float s = 0f;
            for (int i = 0; i < buf.Length; i++) s += buf[i].amount;
            return s;
        }

        // ── 핵심 계약 ──────────────────────────────────────────────────────────

        [Test]
        public void WindUp_NearerEnemyAppears_CommittedTargetStillTakesTheHit()
        {
            // 이 unit 의 존재 이유. 예전엔 wind-up 중 B 가 더 가까워지면 RESOLVE 가 B 를 때렸다
            // — 애니는 A 를 향한 채로.
            var def = CreatePlainDefender(new float3(0f, 0f, 0f));
            var a = CreateEnemy(new float3(2f, 0f, 0f));   // START 시 최근접
            var b = CreateEnemy(new float3(5f, 0f, 0f));

            Tick();                                        // START — a 커밋
            MoveTo(b, new float3(1f, 0f, 0f));             // wind-up 중 b 가 더 가까워짐
            TickThroughWindUp();

            Assert.Greater(IncomingSum(a), 0f, "겨눈 대상이 맞아야 한다");
            Assert.AreEqual(0f, IncomingSum(b), 1e-4f, "wind-up 중 끼어든 대상은 맞지 않는다");
        }

        [Test]
        public void WindUp_CommittedTargetDies_StrictLapse_NoReselect()
        {
            // 커밋 대상이 죽으면 이번 공격은 불발이다. 다른 적으로 갈아타지 않는다
            // (frontmost 락의 strict lapse 와 같은 규칙).
            var def = CreatePlainDefender(new float3(0f, 0f, 0f));
            var a = CreateEnemy(new float3(2f, 0f, 0f));
            var b = CreateEnemy(new float3(3f, 0f, 0f));

            Tick();
            _em.SetComponentData(a, new Health { value = 0f, max = 100f });
            _em.AddComponent<DeadTag>(a);
            TickThroughWindUp();

            Assert.AreEqual(0f, IncomingSum(b), 1e-4f, "재선정 없음 — 이번 공격은 불발");
        }

        [Test]
        public void WindUp_CommittedTargetLeavesRange_StrictLapse()
        {
            var def = CreatePlainDefender(new float3(0f, 0f, 0f));
            var a = CreateEnemy(new float3(2f, 0f, 0f));
            var b = CreateEnemy(new float3(3f, 0f, 0f));

            Tick();
            MoveTo(a, new float3(11f, 0f, 0f));   // 사거리(6) 밖으로
            TickThroughWindUp();

            Assert.AreEqual(0f, IncomingSum(a), 1e-4f, "사거리 밖 대상은 안 맞는다");
            Assert.AreEqual(0f, IncomingSum(b), 1e-4f, "그렇다고 다른 적으로 갈아타지도 않는다");
        }

        [Test]
        public void WindUp_PastGoalIsNotALapseReason()
        {
            // goal-tower-siege unit 1 선례 — 골에 붙은 적은 살아 있는 유효 대상이다.
            // frontmost 락이 같은 규칙이라 여기서도 같아야 한다.
            var def = CreatePlainDefender(new float3(0f, 0f, 0f));
            var a = CreateEnemy(new float3(2f, 0f, 0f));

            Tick();
            _em.AddComponent<PastGoalTag>(a);
            TickThroughWindUp();

            Assert.Greater(IncomingSum(a), 0f, "PastGoal 은 커밋 해제 사유가 아니다");
        }

        [Test]
        public void Commit_IsClearedAfterResolve()
        {
            var def = CreatePlainDefender(new float3(0f, 0f, 0f));
            CreateEnemy(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreNotEqual(0, _em.GetComponentData<AttackState>(def).hasCommittedTarget,
                "START 직후엔 커밋이 서 있어야 한다");

            TickThroughWindUp();
            Assert.AreEqual(0, _em.GetComponentData<AttackState>(def).hasCommittedTarget,
                "RESOLVE 가 커밋을 비운다 — 다음 공격은 그때 다시 고른다");
        }

        [Test]
        public void ZeroHitDelay_BehaviorUnchanged()
        {
            // 즉시 RESOLVE 경로는 커밋을 저장했다가 같은 프레임에 해제한다. 거동 불변.
            var def = CreatePlainDefender(new float3(0f, 0f, 0f), hitDelaySec: 0f);
            var a = CreateEnemy(new float3(2f, 0f, 0f));

            Tick();

            Assert.Greater(IncomingSum(a), 0f, "지연 0 이면 같은 프레임에 맞는다");
            Assert.AreEqual(0, _em.GetComponentData<AttackState>(def).hasCommittedTarget,
                "같은 프레임에 해제된다");
        }

        [Test]
        public void NextAttack_ReselectsFreshly()
        {
            // 이 unit 은 **공격 1회 안**만 책임진다. 다음 공격은 그때의 선정 사슬로 다시 고른다
            // (지속 sticky 는 unit 4 소관 — 여기서 미리 만들지 않는다).
            var def = CreatePlainDefender(new float3(0f, 0f, 0f));
            _em.SetComponentData(def, new AttackState
            {
                range = 6f, cooldownDuration = 0.1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Enemy, hitDelaySec = HitDelay,
            });
            var a = CreateEnemy(new float3(2f, 0f, 0f));
            var b = CreateEnemy(new float3(5f, 0f, 0f));

            Tick();                                  // 1회차 START — a 커밋
            TickThroughWindUp();                     // 1회차 RESOLVE
            Assert.Greater(IncomingSum(a), 0f, "1회차는 a 를 때린다");

            // a 를 사거리(6) 밖으로 보내고 b 를 최근접으로. 이후 공격은 b 를 골라야 한다.
            //
            // ⚠ 여기서 프레임 수를 세지 않는다 — 쿨다운(0.1)과 hitDelay(0.3)가 맞물려
            // 다음 START 시점이 틱 계산에 민감하다. 커밋이 **공격 1회 안**만 산다는 계약은
            // "언젠가 b 로 갈아탄다"로 충분히 증명된다.
            float aAfterFirst = IncomingSum(a);
            MoveTo(a, new float3(9f, 0f, 0f));
            MoveTo(b, new float3(1f, 0f, 0f));
            for (int i = 0; i < 80; i++) Tick();

            Assert.Greater(IncomingSum(b), 0f, "다음 공격은 새로 고른다");
            Assert.AreEqual(aAfterFirst, IncomingSum(a), 1e-4f, "사거리 밖이 된 1회차 대상은 더 맞지 않는다");
        }
    }
}
