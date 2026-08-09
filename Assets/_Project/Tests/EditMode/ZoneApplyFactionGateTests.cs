using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // summon-patrol-defender unit 0 — 존 해저드의 진영 게이트 회귀.
    //
    // 이전 구현은 `PathFollowState` 보유만으로 효과를 걸었다. 그건 "이동체 = 적"이라는
    // 암묵 전제에 기댄 것이고, 거점 수비 아군(Faction.DefenderUnit + PathFollowState)이
    // 그 전제를 깬다. 같은 셀에 아군과 적을 세워두고 **적에게만** 걸리는지 확인한다.
    public class ZoneApplyFactionGateTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeParallelMultiHashMap<int2, HazardEffect> _cellToEffects;
        private NativeQueue<EnemyCcEvent> _ccQueue;
        private NativeQueue<StatModifierApplyEvent> _statQueue;
        private NativeQueue<DotApplyEvent> _dotQueue;
        private NativeQueue<HazardRuntimeEvent> _runtimeQueue;
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;

        private static readonly int2 ZoneCell = new int2(3, 3);
        private static readonly float3 ZoneWorld = new float3(3f, 0f, 3f);

        [SetUp]
        public void SetUp()
        {
            _world = new World("ZoneApplyFactionGateTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ZoneApplySystem>());

            _flow = new NativeArray<float2>(100, Allocator.Persistent);
            _dist = new NativeArray<int>(100, Allocator.Persistent);
            var flowEntity = _em.CreateEntity();
            _em.AddComponentData(flowEntity, new FlowFieldSingleton
            {
                flow = _flow,
                dist = _dist,
                gridSize = new int2(10, 10),
                goalCell = new int2(9, 9),
                tileSize = 1f,
                version = 1,
            });

            _cellToEffects = new NativeParallelMultiHashMap<int2, HazardEffect>(8, Allocator.Persistent);
            var hazardEntity = _em.CreateEntity();
            _em.AddComponentData(hazardEntity, new HazardSingleton { cellToEffects = _cellToEffects });

            _ccQueue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new EnemyCcEventsSingleton { queue = _ccQueue });

            _statQueue = new NativeQueue<StatModifierApplyEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new StatModifierApplyEventsSingleton { queue = _statQueue });

            _dotQueue = new NativeQueue<DotApplyEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new DotApplyEventsSingleton { queue = _dotQueue });

            _runtimeQueue = new NativeQueue<HazardRuntimeEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new HazardRuntimeEventsSingleton { queue = _runtimeQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_cellToEffects.IsCreated) _cellToEffects.Dispose();
            if (_ccQueue.IsCreated) _ccQueue.Dispose();
            if (_statQueue.IsCreated) _statQueue.Dispose();
            if (_dotQueue.IsCreated) _dotQueue.Dispose();
            if (_runtimeQueue.IsCreated) _runtimeQueue.Dispose();
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            _world?.Dispose();
        }

        [Test]
        public void Slow_Zone_Applies_To_Enemy_And_Not_To_Ally()
        {
            AddZoneEffect(CcKind.Slow, param1: 0.5f);
            var enemy = CreateMover(Faction.EnemyUnit);
            CreateMover(Faction.DefenderUnit);

            Tick();

            Assert.AreEqual(1, _statQueue.Count, "존 슬로우는 적 1체에만 걸려야 한다");
            Assert.IsTrue(_statQueue.TryDequeue(out var evt));
            Assert.AreEqual(enemy, evt.target);
        }

        [Test]
        public void Dot_Zone_Applies_To_Enemy_And_Not_To_Ally()
        {
            AddZoneEffect(CcKind.DoT, param1: 12f);
            var enemy = CreateMover(Faction.EnemyUnit);
            CreateMover(Faction.DefenderUnit);

            Tick();

            Assert.AreEqual(1, _dotQueue.Count, "존 DoT 는 적 1체에만 걸려야 한다");
            Assert.IsTrue(_dotQueue.TryDequeue(out var evt));
            Assert.AreEqual(enemy, evt.target);
            Assert.AreEqual(DotOrigin.Zone, evt.effect.origin);
        }

        [Test]
        public void Cc_Zone_Applies_To_Enemy_And_Not_To_Ally()
        {
            AddZoneEffect(CcKind.Stun, param1: 1f);
            var enemy = CreateMover(Faction.EnemyUnit);
            CreateMover(Faction.DefenderUnit);

            Tick();

            Assert.AreEqual(1, _ccQueue.Count, "존 CC 는 적 1체에만 걸려야 한다");
            Assert.IsTrue(_ccQueue.TryDequeue(out var evt));
            Assert.AreEqual(enemy, evt.target);
            Assert.AreEqual(CcKind.Stun, evt.effect.kind);
        }

        [Test]
        public void Runtime_Event_Is_Not_Emitted_For_Ally()
        {
            AddZoneEffect(CcKind.Slow, param1: 0.5f);
            var enemy = CreateMover(Faction.EnemyUnit);
            CreateMover(Faction.DefenderUnit);

            Tick();

            Assert.AreEqual(1, _runtimeQueue.Count, "런타임 로그도 적 1건이어야 한다");
            Assert.IsTrue(_runtimeQueue.TryDequeue(out var evt));
            Assert.AreEqual(enemy, evt.target);
        }

        [Test]
        public void Ally_Alone_In_Zone_Produces_No_Events()
        {
            AddZoneEffect(CcKind.DoT, param1: 12f);
            CreateMover(Faction.DefenderUnit);

            Tick();

            Assert.IsTrue(_dotQueue.IsEmpty());
            Assert.IsTrue(_ccQueue.IsEmpty());
            Assert.IsTrue(_statQueue.IsEmpty());
            Assert.IsTrue(_runtimeQueue.IsEmpty());
        }

        [Test]
        public void Multiple_Enemies_In_Zone_All_Receive_Effect()
        {
            // 무회귀 확인: 게이트가 적 다중 적용을 막지 않는다.
            AddZoneEffect(CcKind.DoT, param1: 12f);
            CreateMover(Faction.EnemyUnit);
            CreateMover(Faction.EnemyUnit);
            CreateMover(Faction.DefenderUnit);

            Tick();

            Assert.AreEqual(2, _dotQueue.Count);
        }

        [Test]
        public void Enemy_Outside_Zone_Cell_Receives_Nothing()
        {
            AddZoneEffect(CcKind.DoT, param1: 12f);
            CreateMover(Faction.EnemyUnit, new float3(7f, 0f, 7f));

            Tick();

            Assert.IsTrue(_dotQueue.IsEmpty());
        }

        private void AddZoneEffect(CcKind kind, float param1, float restDuration = 2f)
        {
            _cellToEffects.Add(ZoneCell, new HazardEffect
            {
                kind = kind,
                param1 = param1,
                param2 = 0f,
                restDuration = restDuration,
                tickInterval = 0f,
                element = DotElement.Fire,
            });
        }

        private Entity CreateMover(Faction faction) => CreateMover(faction, ZoneWorld);

        private Entity CreateMover(Faction faction, float3 worldPos)
        {
            var entity = _em.CreateEntity();
            _em.AddComponentData(entity, new FactionTag { value = faction });
            _em.AddComponentData(entity, LocalTransform.FromPosition(worldPos));
            _em.AddComponentData(entity, new PathFollowState { speed = 1f });
            return entity;
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }
    }
}
