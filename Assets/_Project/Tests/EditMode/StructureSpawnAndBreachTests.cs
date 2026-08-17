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
        public void AuthoredInstinct_SpawnsWithSoHp_AndNineOccupiedCells()
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
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(e));
            Assert.AreEqual(9, em.GetBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(e).Length,
                "3×3 본체를 **점유**한다 — 사거리는 가장 가까운 칸까지, 흐름장 소스도 이 9칸이다");
            AssertStructureHasNoEffectBuffers(em, e);
        }

        [Test]
        public void AuthoredEnemyCore_SpawnsWithoutOccupancyBuffer()
        {
            var core = MakeStructureData(StructureKind.Core, hp: 777f);
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(3, 3), side = StructureSide.Enemy, data = core }));

            Spawn();
            var em = _world.EntityManager;
            var e = TowerAt(new int2(3, 3));
            Assert.AreNotEqual(Entity.Null, e);
            Assert.AreEqual(Faction.EnemyCore, em.GetComponentData<FactionTag>(e).value);
            Assert.IsFalse(em.HasBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(e),
                "마음은 1×1 이라 다중 셀 점유 선언이 없다");
            Assert.IsFalse(em.HasComponent<GoalTowerTag>(e),
                "적 마음은 패배 판정(GoalTowerTag 부재 감지) 밖이다");
            AssertStructureHasNoEffectBuffers(em, e);
        }

        // three-minute-kill-race unit 0 — `SiegeCoreAlive_AllWavesCleared_DoesNotUseLegacyVictory`
        // 는 삭제했다. 「살아 있는 적 마음이 구 전멸 승리를 억제한다」를 고정하던 테스트인데,
        // 그 승리 경로(`CheckVictory`) 자체가 은퇴해 억제할 대상이 없다. 판을 끝내는 것은
        // 3분 만료와 유저 제출뿐이고, 그 성질은 `StructureLivePlayTest` (4) 가 지킨다.

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

        // 본능은 **통행을 막지 않는다** — 건물이지 벽이 아니다(instinct-content unit 1,
        // 사용자 결정 2026-08-12: 「배치 불가를 얘기했지 통행 불가를 지시하지 않았다」).
        //
        // 이 테스트는 battle-structures 시절 정반대(9칸이 들어간다)를 단정했다. 그때의
        // 결함은 «점유»와 «차단»이 버퍼 하나에 겸직한 것이었고, 당시엔 차단이 맞다고 보아
        // 컴포넌트 요구를 뗐다. 축을 가른 지금 답은 반대다 — 버퍼는 점유만 말하고,
        // 차단은 `BlockingHazard` 컴포넌트가 말한다. 버퍼 부재 단정으로는 못 잡는 구분이라
        // (본능은 버퍼를 **여전히 든다** — 다중 셀 거리 때문에) 시스템을 실제로 돌린다.
        [Test]
        public void InstinctCells_DoNotEnterBlockedCells_ButWallsStillDo()
        {
            var instinct = MakeStructureData(StructureKind.Instinct, hp: 100f);
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(5, 4), side = StructureSide.Enemy, data = instinct }));
            Spawn();

            var em = _world.EntityManager;
            var blocked = new NativeHashSet<int2>(32, Allocator.Persistent);
            try
            {
                var singleton = em.CreateEntity();
                em.AddComponentData(singleton, new Wassup.Battle.Effects.ObstacleSingleton
                {
                    blockedCells = blocked,
                });

                var simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
                simGroup.AddSystemToUpdateList(_world.CreateSystem<Wassup.Battle.Effects.ObstacleLifetimeSystem>());
                _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
                simGroup.Update();

                Assert.AreEqual(0, blocked.Count, "본능 3×3 은 통행을 막지 않는다 — 적은 그 위를 지나간다");

                // 점유 자체는 살아 있다 — 다중 셀 거리(AttackSystem)가 이 버퍼를 읽는다.
                var tower = TowerAt(new int2(5, 4));
                Assert.IsTrue(em.HasBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(tower),
                    "차단만 뗐다. 점유 선언은 남는다 — 3×3 옆구리까지가 사거리다");
                Assert.AreEqual(9, em.GetBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(tower).Length);

                // 회귀 방지: 이 변경이 방벽까지 뚫으면 안 된다. 방벽 = 버퍼 + BlockingHazard.
                var wall = em.CreateEntity();
                em.AddComponentData(wall, new Wassup.Battle.Effects.BlockingHazard { hazardSoIndex = 0, maxHp = 10f });
                var wallCells = em.AddBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(wall);
                wallCells.Add(new Wassup.Battle.Effects.OccupiedCellsBuffer { cell = new int2(1, 1) });
                wallCells.Add(new Wassup.Battle.Effects.OccupiedCellsBuffer { cell = new int2(2, 1) });

                _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
                simGroup.Update();

                Assert.AreEqual(2, blocked.Count, "방벽은 **여전히** 막는다 — 컴포넌트가 차단을 말한다");
                Assert.IsTrue(blocked.Contains(new int2(1, 1)) && blocked.Contains(new int2(2, 1)));

                // 죽은 방벽은 그 프레임부터 집합에서 빠진다 — 파괴 전 DeadTag 단계 포함.
                em.AddComponent<DeadTag>(wall);
                _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
                simGroup.Update();
                Assert.AreEqual(0, blocked.Count, "붕괴한 방벽은 통행을 막지 않는다");
            }
            finally { if (blocked.IsCreated) blocked.Dispose(); }
        }

        // ── unit 5 — 본능 공격 (전용 시스템 없음, 베이크로 통합 루프 합류) ──────

        private StructureData MakeArmedInstinct(float damage, ProjectileData projectile)
        {
            var d = MakeStructureData(StructureKind.Instinct, hp: 400f);
            d.attackDamage = damage;
            d.attackRange = 4f;
            d.attackCooldown = 1.25f;
            d.projectile = projectile;
            return d;
        }

        private ProjectileData MakeProjectile()
        {
            var p = ScriptableObject.CreateInstance<ProjectileData>();
            _cleanup.Add(p);
            return p;
        }

        [Test]
        public void ArmedInstinct_BakesAttackPipeline_WithAuthoredMask()
        {
            var instinct = MakeArmedInstinct(damage: 25f, MakeProjectile());
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(5, 4), side = StructureSide.Enemy, data = instinct }));

            Spawn();
            var em = _world.EntityManager;
            var e = TowerAt(new int2(5, 4));

            Assert.IsTrue(em.HasComponent<Wassup.Battle.Combat.AttackState>(e), "공격 저작 본능은 AttackState 를 갖는다");
            var atk = em.GetComponentData<Wassup.Battle.Combat.AttackState>(e);
            Assert.AreEqual(4f, atk.range, 1e-4f);
            Assert.AreEqual(1, atk.attackTargetCount, "v1 = 투사체 1발 고정");
            Assert.AreEqual((int)Faction.DefenderUnit, atk.targetMask,
                "SO 기본 마스크(DefenderUnit — 포탑)가 그대로 흐른다");
            Assert.IsTrue(em.HasComponent<Wassup.Battle.Combat.Projectile.ProjectileRef>(e));
            Assert.GreaterOrEqual(em.GetComponentData<Wassup.Battle.Combat.Projectile.ProjectileRef>(e).dataIndex, 0);
            var outputs = em.GetBuffer<Wassup.Battle.Combat.AttackOutputElement>(e);
            Assert.AreEqual(1, outputs.Length);
            Assert.AreEqual(25f, outputs[0].value.magnitude, 1e-4f);
        }

        // 발사 실증 — 합성이 아니라 실 AttackSystem 이 브리지가 세운 본능을 처리한다.
        [Test]
        public void ArmedInstinct_FiresProjectileRequest_AtDefenderInRange()
        {
            var instinct = MakeArmedInstinct(damage: 25f, MakeProjectile());
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(5, 4), side = StructureSide.Enemy, data = instinct }));

            Spawn();
            var em = _world.EntityManager;
            var e = TowerAt(new int2(5, 4));
            float3 instinctPos = em.GetComponentData<Unity.Transforms.LocalTransform>(e).Position;

            // 사거리 내 방어유닛.
            var defender = em.CreateEntity();
            em.AddComponentData(defender, Unity.Transforms.LocalTransform.FromPosition(
                instinctPos + new float3(2f, 0f, 0f)));
            em.AddComponentData(defender, new Health { value = 100f, max = 100f });
            em.AddComponentData(defender, new FactionTag { value = Faction.DefenderUnit });
            em.AddComponent<Wassup.Battle.Units.DefenderUnitTag>(defender);
            em.AddBuffer<IncomingDamage>(defender);

            var simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(_world.CreateSystem<Wassup.Battle.Combat.AttackSystem>());
            _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            Assert.IsTrue(em.HasComponent<Wassup.Battle.Combat.Projectile.ProjectileSpawnRequest>(e),
                "쿨다운 0 + 사거리 내 대상 → 통합 루프가 투사체 요청을 부착한다");
            Assert.AreEqual(defender,
                em.GetComponentData<Wassup.Battle.Combat.Projectile.ProjectileSpawnRequest>(e).target,
                "겨눈 것 = 저작 마스크의 후보(방어유닛) — 직격 호밍이라 피해풀 축과 갈릴 자리가 없다(계약 11)");
        }

        [Test]
        public void ArmedInstinct_WithoutProjectile_WarnsAndBakesUnarmed()
        {
            var instinct = MakeArmedInstinct(damage: 25f, projectile: null);
            SetField(_bridge, "_resolvedMapDoc", MakeDocWithStructures(
                new StructureEntry { cell = new Vector2Int(5, 4), side = StructureSide.Enemy, data = instinct }));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("projectile 미지정"));
            Spawn();

            Assert.IsFalse(_world.EntityManager.HasComponent<Wassup.Battle.Combat.AttackState>(
                TowerAt(new int2(5, 4))),
                "조용한 미발사 대신 경고 + 무공격 베이크(적 walk-only 선례)");
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

        private static void AssertStructureHasNoEffectBuffers(EntityManager em, Entity structure)
        {
            Assert.IsFalse(em.HasBuffer<Wassup.Battle.Effects.CcEffect>(structure),
                "Structures must not carry CcEffect buffers.");
            Assert.IsFalse(em.HasBuffer<Wassup.Battle.Effects.StatModifierSlot>(structure),
                "Structures must not carry stat modifier buffers.");
            Assert.IsFalse(em.HasBuffer<Wassup.Battle.Effects.StackModifierSlot>(structure),
                "Structures must not carry stack modifier buffers.");
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
