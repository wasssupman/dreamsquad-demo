using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // target-persistence units 1·2 — 락 유지 술어 + Focus 적의 범위 이탈 해제(B2).
    public class TargetPersistenceTests
    {
        // ── unit 1: 순수 술어 ──────────────────────────────────────────────────

        [Test]
        public void KeepsLock_AliveAndInRange_Keeps()
        {
            Assert.IsTrue(TargetPersistence.KeepsLock(true, 2, 3));
            Assert.IsTrue(TargetPersistence.KeepsLock(true, 3, 3), "경계는 포함");
        }

        [Test]
        public void KeepsLock_OutOfRange_Releases()
        {
            // D2 — 사거리 이탈은 **해제 사유**다. 이전 계약("이탈해도 락 유지")이 B2 였다.
            Assert.IsFalse(TargetPersistence.KeepsLock(true, 4, 3));
        }

        [Test]
        public void KeepsLock_Dead_Releases()
        {
            Assert.IsFalse(TargetPersistence.KeepsLock(false, 0, 3));
        }

        // ── unit 2: Focus 적이 사거리를 벗어나면 새 대상을 고른다 (B2) ─────────

        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<UnitAttackVisualEvent> _attackQ;
        private NativeQueue<EnemyCcEvent> _ccQ;
        private Entity _fieldEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("TargetPersistenceTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<EnemyAiStateSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            _attackQ = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new UnitAttackVisualEventsSingleton { queue = _attackQ });
            _ccQ = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new EnemyCcEventsSingleton { queue = _ccQ });

            int w = 16;
            var flow = new NativeArray<float2>(w, Allocator.Persistent);
            var dist = new NativeArray<int>(w, Allocator.Persistent);
            for (int i = 0; i < w - 1; i++) { flow[i] = new float2(1, 0); dist[i] = (w - 1) - i; }
            flow[w - 1] = float2.zero; dist[w - 1] = 0;
            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist, gridSize = new int2(w, 1),
                goalCell = new int2(w - 1, 0), tileSize = 1f, version = 1,
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackQ.IsCreated) _attackQ.Dispose();
            if (_ccQ.IsCreated) _ccQ.Dispose();
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity) && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // FocusUntilDead 적 — 즉시 타격(hitDelay 0)이라 unit 0 커밋과 얽히지 않는다.
        private Entity CreateFocusEnemy(float3 pos, float range = 3f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = range, cooldownDuration = 0.05f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.DefenderUnit, hitDelaySec = 0f,
            });
            _em.AddComponentData(e, new EnemyBehavior
            {
                targetMode = EnemyTargetMode.FocusUntilDead,
                engageMovement = EngageMovement.Halt,
            });
            _em.AddComponentData(e, new FocusTarget { current = Entity.Null });
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            var outs = _em.AddBuffer<AttackOutputElement>(e);
            outs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 4f },
            });
            return e;
        }

        private Entity CreateDefender(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            return e;
        }

        private float IncomingSum(Entity e)
        {
            var buf = _em.GetBuffer<IncomingDamage>(e);
            float s = 0f;
            for (int i = 0; i < buf.Length; i++) s += buf[i].amount;
            return s;
        }

        [Test]
        public void FocusEnemy_LockedDefenderLeavesRange_SwitchesToAnotherInRange()
        {
            // B2 회귀. 예전엔 락을 붙든 채 발사를 보류하고 FSM 이 Marching 으로 떨어져
            // **바로 옆 방어유닛을 영원히 무시**했다.
            // ⚠ 락은 «먼저 만든 것»이 아니라 그 프레임의 **최근접**을 잡는다.
            // 잠기길 원하는 쪽을 더 가깝게 둬야 한다.
            var enemy = CreateFocusEnemy(new float3(0f, 0f, 0f));
            var locked = CreateDefender(new float3(1f, 0f, 0f));   // 최근접 → 이쪽이 잠긴다
            var other  = CreateDefender(new float3(2f, 0f, 0f));   // 사거리(3) 안의 대체 후보

            Tick();
            Assert.AreEqual(locked, _em.GetComponentData<FocusTarget>(enemy).current,
                "먼저 잡힌 대상을 잠근다");

            // 락 대상이 사거리 밖으로 (재배치·넉백·적의 전진으로 실제로 일어난다)
            _em.SetComponentData(locked, LocalTransform.FromPosition(new float3(12f, 0f, 0f)));
            float otherBefore = IncomingSum(other);
            for (int i = 0; i < 20; i++) Tick();

            Assert.Greater(IncomingSum(other), otherBefore,
                "사거리 안의 다른 방어유닛을 때려야 한다");
            Assert.AreEqual(other, _em.GetComponentData<FocusTarget>(enemy).current,
                "락이 새 대상으로 넘어간다");
        }

        [Test]
        public void FocusEnemy_LockedLeavesRange_FsmDoesNotFallToMarching()
        {
            // B2 의 나머지 절반 — AttackSystem 은 새 대상을 골랐는데 FSM 미러가 옛 규칙이면
            // Marching 이 되어 적이 골로 걸어간다. 두 시스템이 같은 술어를 봐야 한다.
            var enemy = CreateFocusEnemy(new float3(0f, 0f, 0f));
            var locked = CreateDefender(new float3(1f, 0f, 0f));   // 최근접 → 잠긴다
            CreateDefender(new float3(2f, 0f, 0f));               // 대체 후보

            Tick();
            _em.SetComponentData(locked, LocalTransform.FromPosition(new float3(12f, 0f, 0f)));
            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(AiState.Engaging, _em.GetComponentData<EnemyAiState>(enemy).value,
                "사거리 안에 대상이 있으면 Engaging 이어야 한다 (Marching = B2)");
        }

        [Test]
        public void FocusEnemy_NoOtherTargetInRange_MarchesOn()
        {
            // 락을 놓았는데 사거리 안에 아무도 없으면 Marching 이 **맞다**. 이건 버그가 아니다.
            var enemy = CreateFocusEnemy(new float3(0f, 0f, 0f));
            var locked = CreateDefender(new float3(2f, 0f, 0f));

            Tick();
            _em.SetComponentData(locked, LocalTransform.FromPosition(new float3(12f, 0f, 0f)));
            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(AiState.Marching, _em.GetComponentData<EnemyAiState>(enemy).value);
        }

        [Test]
        public void FocusEnemy_LockedStaysInRange_DoesNotSwitchToNearer()
        {
            // 유지 계약은 그대로다 — 더 가까운 대상이 나타나도 락을 놓지 않는다.
            var enemy = CreateFocusEnemy(new float3(0f, 0f, 0f));
            var locked = CreateDefender(new float3(3f, 0f, 0f));

            Tick();
            Assert.AreEqual(locked, _em.GetComponentData<FocusTarget>(enemy).current);

            var nearer = CreateDefender(new float3(1f, 0f, 0f));
            float nearerBefore = IncomingSum(nearer);
            for (int i = 0; i < 20; i++) Tick();

            Assert.AreEqual(locked, _em.GetComponentData<FocusTarget>(enemy).current,
                "사거리 안이면 더 가까운 대상이 와도 유지");
            Assert.AreEqual(nearerBefore, IncomingSum(nearer), 1e-4f);
        }
    }
}
