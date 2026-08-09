using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // battle-structures unit 4 — 거점 스폰(두 소스) + 붕괴 ⓐ(셀 단위).
    // 하네스는 GoalTowerArchetypeTests 동형(실 ECS World 주입 + 리플렉션으로 브리지
    // private 경로 구동) — 합성 엔티티가 아니라 **생산 코드가 만든 것**을 검증한다.
    public class StructureSpawnAndBreachTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _deck;
        private GeneratedMap _map;
        private readonly List<Object> _cleanup = new();

        [SetUp]
        public void SetUp()
        {
            _world = new World("StructureSpawnAndBreachTests");
            _deck = ScriptableObject.CreateInstance<AttackDeck>();   // defeatGoalReachedCount 기본 5 → 스트레스 경로

            _go = new GameObject("BattleBridge_StructureSpawnTest");
            _bridge = _go.AddComponent<BattleBridge>();

            // 10×8 전부 Walk. 골 2개 = 분리 복도 컨셉의 최소형.
            _map = BuildMap(new int2(10, 8),
                spawns: new[] { new int2(0, 2), new int2(0, 5) },
                goals: new[] { new int2(9, 2), new int2(9, 5) });

            SetField(_bridge, "deck", _deck);
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _world.EntityManager);
            SetField(_bridge, "_generatedMap", _map);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_deck != null) Object.DestroyImmediate(_deck);
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
            _map.Dispose();
            _world?.Dispose();
        }

        private static GeneratedMap BuildMap(int2 gridSize, int2[] spawns, int2[] goals)
        {
            int n = gridSize.x * gridSize.y;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Walk;
            var spawnArr = new NativeArray<int2>(spawns.Length, Allocator.Persistent);
            for (int i = 0; i < spawns.Length; i++) spawnArr[i] = spawns[i];
            var goalArr = new NativeArray<int2>(goals.Length, Allocator.Persistent);
            for (int i = 0; i < goals.Length; i++) goalArr[i] = goals[i];
            return new GeneratedMap
            {
                tiles = tiles,
                spawns = spawnArr,
                goals = goalArr,
                goal = goals[0],
                gridSize = gridSize,
            };
        }

        private StructureData MakeStructureData(StructureKind kind, float hp)
        {
            var d = ScriptableObject.CreateInstance<StructureData>();
            d.kind = kind;
            d.health = hp;
            _cleanup.Add(d);
            return d;
        }

        private MapDocument MakeDocWithStructures(params StructureEntry[] entries)
        {
            // 스폰 경로는 doc 의 Structures 만 읽는다 — 타일/골은 _generatedMap 이 정본.
            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetStructures(entries);
            _cleanup.Add(doc);
            return doc;
        }

        private void Spawn()
        {
            CallPrivateMethod(_bridge, "ResetGoalStability");
            CallPrivateMethod(_bridge, "SpawnStructureEntities");
        }

        private HashSet<Vector2Int> BreachedCells()
            => (HashSet<Vector2Int>)GetField(_bridge, "_breachedCells");

        private Entity TowerAt(int2 cell)
        {
            var em = _world.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<StructureTag>());
            var entities = q.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var e in entities)
                    if (em.GetComponentData<StructureTag>(e).cell.Equals(cell)) return e;
            }
            finally { entities.Dispose(); }
            return Entity.Null;
        }

        // ── 붕괴 ⓐ — 셀 단위 ────────────────────────────────────────────────────

        [Test]
        public void OneTowerDestroyed_BreachesOnlyThatCell_OtherStands()
        {
            Spawn();
            var em = _world.EntityManager;
            var towerA = TowerAt(new int2(9, 2));
            var towerB = TowerAt(new int2(9, 5));
            Assert.AreNotEqual(Entity.Null, towerA);
            Assert.AreNotEqual(Entity.Null, towerB);

            em.DestroyEntity(towerA);   // 표준 사망 경로의 결과(엔티티 부재)를 재현
            CallPrivateMethod(_bridge, "SyncGoalStability");

            var breached = BreachedCells();
            Assert.AreEqual(1, breached.Count, "부서진 골의 셀 하나만 breached");
            Assert.IsTrue(breached.Contains(new Vector2Int(9, 2)));
            Assert.IsTrue(em.Exists(towerB), "다른 골은 그대로 선다 — 계약 7. 구현이 전역 bool 이던 시절엔 전부 파괴됐다");
            Assert.IsFalse((bool)GetField(_bridge, "_resultShown"),
                "StressLimit>0 이므로 붕괴는 패배가 아니라 유출 전환이다");
        }

        [Test]
        public void MirrorScalar_ZeroOnBreachFrame_ThenTracksSurvivingCore()
        {
            Spawn();
            var em = _world.EntityManager;
            var towerA = TowerAt(new int2(9, 2));
            em.DestroyEntity(towerA);

            // 붕괴 프레임 — «가장 위험한 골» 은 방금 0 이 되어 죽은 그 골이다. 생존 골 체력으로
            // 덮으면 HUD 에 «부서졌는데 1000» 이 뜨고, StressLimit 0 즉시 패배는 이 값으로
            // 얼어붙는다(PlayMode GoalStability/EndlessSmoke 가 이 계약을 라이브로 잰다).
            CallPrivateMethod(_bridge, "SyncGoalStability");
            Assert.AreEqual(0, _bridge.GoalStabilityCurrent, "붕괴 프레임의 미러는 0");

            // 다음 프레임 — 살아남은 마음 중 최저 체력(= 만피 타워 B). 구 전역 붕괴는 여기서도
            // 0 이었다(전 타워 파괴).
            CallPrivateMethod(_bridge, "SyncGoalStability");
            Assert.AreEqual(_deck.goalStabilityMax, _bridge.GoalStabilityCurrent,
                "한 골이 부서져도 미러는 살아있는 골을 보여준다");
        }

        // ── 스폰 — 저작 거점(SO HP) ─────────────────────────────────────────────

        [Test]
        public void AuthoredInstinct_SpawnsWithSoHp_AndNineBlockedCells()
        {
            var instinct = MakeStructureData(StructureKind.Instinct, hp: 321f);
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(5, 4), side = StructureSide.Enemy, data = instinct }));

            Spawn();
            var em = _world.EntityManager;
            var e = TowerAt(new int2(5, 4));
            Assert.AreNotEqual(Entity.Null, e, "본능이 스폰된다");
            Assert.AreEqual(Faction.EnemyInstinct, em.GetComponentData<FactionTag>(e).value, "(적 × 본능) 파생");
            Assert.AreEqual(321f, em.GetComponentData<Health>(e).value, 1e-4f, "HP 는 SO 에서 온다");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Effects.BlockingHazardCellsBuffer>(e));
            Assert.AreEqual(9, em.GetBuffer<Wassup.Battle.Effects.BlockingHazardCellsBuffer>(e).Length,
                "3×3 본체가 통행을 막는다(계약 12)");
        }

        [Test]
        public void AuthoredEnemyCore_SpawnsWithoutBlocking()
        {
            var core = MakeStructureData(StructureKind.Core, hp: 777f);
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(3, 3), side = StructureSide.Enemy, data = core }));

            Spawn();
            var em = _world.EntityManager;
            var e = TowerAt(new int2(3, 3));
            Assert.AreNotEqual(Entity.Null, e);
            Assert.AreEqual(Faction.EnemyCore, em.GetComponentData<FactionTag>(e).value);
            Assert.IsFalse(em.HasBuffer<Wassup.Battle.Effects.BlockingHazardCellsBuffer>(e),
                "마음은 통행을 막지 않는다 — 적 마음은 스폰 셀이다(계약 12)");
            Assert.IsFalse(em.HasComponent<GoalTowerTag>(e),
                "적 마음은 패배 판정(GoalTowerTag 부재 감지) 밖이다");
        }

        [Test]
        public void DefenderCoreEntry_IsNotSpawned_GoalsArrayIsTheSource()
        {
            var core = MakeStructureData(StructureKind.Core, hp: 500f);
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(4, 4), side = StructureSide.Defender, data = core }));

            Spawn();
            Assert.AreEqual(Entity.Null, TowerAt(new int2(4, 4)),
                "(Defender, Core) 는 검증을 뚫고 와도 안 세운다 — 골이 두 벌이 되는 것의 마지막 방어선");
        }

        [Test]
        public void InstinctDestroyed_LogOnly_NoBreachNoDefeat()
        {
            var instinct = MakeStructureData(StructureKind.Instinct, hp: 100f);
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(5, 4), side = StructureSide.Enemy, data = instinct }));

            Spawn();
            var em = _world.EntityManager;
            em.DestroyEntity(TowerAt(new int2(5, 4)));
            CallPrivateMethod(_bridge, "SyncGoalStability");

            Assert.AreEqual(0, BreachedCells().Count,
                "본능 붕괴는 연출·로그만(결정 2) — 유출 전환·스트레스는 방어 마음 전용");
            Assert.IsFalse((bool)GetField(_bridge, "_resultShown"));
        }

        // ── Helpers (GoalTowerArchetypeTests 동형) ──────────────────────────────

        private static void CallPrivateMethod(object target, string name)
        {
            var mi = target.GetType().GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"Method '{name}' not found on {target.GetType().Name}");
            mi.Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
        {
            var fi = FindField(target, name);
            fi.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            var fi = FindField(target, name);
            return fi.GetValue(target);
        }

        private static FieldInfo FindField(object target, string name)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                       | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            return fi;
        }
    }
}
