using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Bridge;

namespace Wassup.Tests.EditMode
{
    public class HazardCasterTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<HazardSpawnRequest> _queue;
        private NativeQueue<UnitAttackVisualEvent> _visualQueue;
        private NativeArray<float2> _flow;
        private NativeArray<int> _dist;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HazardCasterTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<HazardCastSystem>());

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

            _queue = new NativeQueue<HazardSpawnRequest>(Allocator.Persistent);
            var singletonEntity = _em.CreateEntity();
            _em.AddComponentData(singletonEntity, new HazardSpawnRequestsSingleton { queue = _queue });

            _visualQueue = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            var visualSingletonEntity = _em.CreateEntity();
            _em.AddComponentData(visualSingletonEntity, new UnitAttackVisualEventsSingleton { queue = _visualQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_queue.IsCreated) _queue.Dispose();
            if (_visualQueue.IsCreated) _visualQueue.Dispose();
            if (_flow.IsCreated) _flow.Dispose();
            if (_dist.IsCreated) _dist.Dispose();
            _world?.Dispose();
        }

        [Test]
        public void Cast_Enqueues_Attack_Visual_Event_For_Spine_Animation()
        {
            var caster = CreateCaster(new float3(1f, 0f, 1f));
            CreateAttackUnit(new float3(3f, 0f, 2f));

            Tick();

            Assert.IsTrue(_visualQueue.TryDequeue(out var evt));
            Assert.AreEqual(caster, evt.attacker);
            Assert.AreEqual(new float3(3f, 0f, 2f), evt.targetWorld);
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        private Entity CreateCaster(float3 worldPos, float range = 4f, float cooldown = 1f, HazardCastKind kind = HazardCastKind.Zone, int dataIndex = 7)
        {
            var entity = _em.CreateEntity();
            _em.AddComponent<DefenderUnitTag>(entity);
            _em.AddComponentData(entity, new FactionTag { value = Faction.Defender });
            _em.AddComponentData(entity, LocalTransform.FromPosition(worldPos));
            _em.AddComponentData(entity, new HazardCastState
            {
                range = range,
                cooldownDuration = cooldown,
                cooldownRemaining = 0f,
                targetMask = (int)Faction.Enemy,
                dataIndex = dataIndex,
                kind = kind,
                footprintWidth = 5,
                footprintHeight = 5,
            });
            return entity;
        }

        private Entity CreateAttackUnit(float3 worldPos)
        {
            var entity = _em.CreateEntity();
            _em.AddComponentData(entity, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(entity, LocalTransform.FromPosition(worldPos));
            _em.AddComponentData(entity, new PathFollowState { speed = 1f });
            return entity;
        }

        [Test]
        public void Casts_Request_At_In_Range_Attack_Unit_Cell()
        {
            var caster = CreateCaster(new float3(1f, 0f, 1f));
            var target = CreateAttackUnit(new float3(3f, 0f, 2f));

            Tick();

            Assert.IsTrue(_queue.TryDequeue(out var req));
            Assert.AreEqual(caster, req.caster);
            Assert.AreEqual(target, req.target);
            Assert.AreEqual(new int2(3, 2), req.centerCell);
            Assert.AreEqual(HazardCastKind.Zone, req.kind);
            Assert.AreEqual(7, req.dataIndex);
            Assert.AreEqual(1, req.width);
            Assert.AreEqual(1, req.height);
        }

        [Test]
        public void Ignores_Out_Of_Range_Attack_Unit()
        {
            CreateCaster(new float3(1f, 0f, 1f), range: 2f);
            CreateAttackUnit(new float3(7f, 0f, 7f));

            Tick();

            Assert.IsTrue(_queue.IsEmpty());
        }

        [Test]
        public void Cooldown_Blocks_Duplicate_Cast_Until_Ready()
        {
            CreateCaster(new float3(1f, 0f, 1f), cooldown: 1f);
            CreateAttackUnit(new float3(2f, 0f, 1f));

            Tick(0.016f);
            Assert.IsTrue(_queue.TryDequeue(out _));

            Tick(0.25f);
            Assert.IsTrue(_queue.IsEmpty());

            Tick(0.75f);
            Assert.IsTrue(_queue.TryDequeue(out _));
        }

        [Test]
        public void Dead_Target_After_Request_Does_Not_Change_Cell_Snapshot()
        {
            CreateCaster(new float3(1f, 0f, 1f));
            var target = CreateAttackUnit(new float3(4f, 0f, 4f));

            Tick();
            _em.DestroyEntity(target);

            Assert.IsTrue(_queue.TryDequeue(out var req));
            Assert.AreEqual(new int2(4, 4), req.centerCell);
            Assert.AreEqual(target, req.target);
        }

        [Test]
        public void Blocking_Cast_Uses_Blocking_Kind_And_Data_Index()
        {
            CreateCaster(new float3(1f, 0f, 1f), kind: HazardCastKind.Blocking, dataIndex: 3);
            CreateAttackUnit(new float3(2f, 0f, 1f));

            Tick();

            Assert.IsTrue(_queue.TryDequeue(out var req));
            Assert.AreEqual(HazardCastKind.Blocking, req.kind);
            Assert.AreEqual(3, req.dataIndex);
        }

        [Test]
        public void Bridge_Drain_Drops_Request_When_Caster_Was_Destroyed()
        {
            var go = new GameObject("HazardCasterBridgeTest");
            var bridge = go.AddComponent<BattleBridge>();
            var caster = _em.CreateEntity();
            _em.DestroyEntity(caster);
            _queue.Enqueue(new HazardSpawnRequest
            {
                kind = HazardCastKind.Zone,
                dataIndex = 0,
                centerCell = new int2(2, 2),
                width = 1,
                height = 1,
                caster = caster,
                target = Entity.Null,
            });

            SetPrivateField(bridge, "_em", _em);
            SetPrivateField(bridge, "_hazardSpawnRequestQueue", _queue);

            Assert.DoesNotThrow(() => InvokePrivate(bridge, "DrainHazardSpawnRequests"));
            Assert.IsTrue(_queue.IsEmpty());

            SetPrivateField(bridge, "_hazardSpawnRequestQueue", default(NativeQueue<HazardSpawnRequest>));
            Object.DestroyImmediate(go);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, name);
            method.Invoke(target, null);
        }
    }
}
