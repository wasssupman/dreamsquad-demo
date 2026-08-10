using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-structures unit 0 — 골 타워 아키타입의 **단일 소스** 방지선.
    //
    // 왜 브리지를 직접 호출하나: goal-stability 의 최후순위 계약이 라이브에서 한 번도
    // 발효되지 않았던 원인이 «테스트가 만드는 골» 과 «EnsureGoalTowers 가 만드는 골» 의
    // 아키타입 drift 였다. 테스트는 Faction.Goal + GoalPoint 를 달았고 브리지는 (구)
    // Faction.Defender + GoalTowerTag 를 달았다. 그래서 3개짜리 스위트가 통과하는 동안
    // 라이브 타워는 그 계약 밖에 있었다. 케이스를 늘려도 이 drift 는 다시 벌어진다 —
    // 그래서 여기서 **생산 코드가 만든 엔티티**를 직접 검사한다.
    //
    // (그 계약 자체는 폐기됐다 — 거점은 타입으로 특별 취급하지 않고 거리로 경쟁한다.
    //  하지만 아키타입을 생산 경로에 고정한다는 이 파일의 목적은 그와 무관하게 유효하다.)
    public class GoalTowerArchetypeTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _deck;
        private GeneratedMap _map;

        [SetUp]
        public void SetUp()
        {
            _world = new World("GoalTowerArchetypeTests");
            _deck = ScriptableObject.CreateInstance<AttackDeck>();

            _go = new GameObject("BattleBridge_GoalTowerArchetypeTest");
            _bridge = _go.AddComponent<BattleBridge>();

            // 3×3 전부 Walk, 스폰 1, 골 1 (2,1). EnsureGoalTowers 는 goals 와 tileSize 만 본다.
            _map = BuildMap(new int2(3, 3), spawn: new int2(0, 1), goal: new int2(2, 1));

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
            _map.Dispose();
            _world?.Dispose();
        }

        private static GeneratedMap BuildMap(int2 gridSize, int2 spawn, int2 goal)
        {
            int n = gridSize.x * gridSize.y;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Walk;
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            spawns[0] = spawn;
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            goals[0] = goal;
            return new GeneratedMap
            {
                tiles = tiles,
                spawns = spawns,
                goals = goals,
                goal = goal,
                gridSize = gridSize,
            };
        }

        // 브리지의 라이브 스폰 경로. ResetGoalStability 가 덱에서 상한을 받아야
        // SpawnStructureEntities 가 타워를 세운다(상한 0 이면 세우지 않는 것이 계약).
        // (unit 4 — EnsureGoalTowers 가 SpawnStructureEntities 로 일반화됐다.)
        private void SpawnTowersViaBridge()
        {
            CallPrivateMethod(_bridge, "ResetGoalStability");
            CallPrivateMethod(_bridge, "SpawnStructureEntities");
        }

        private Entity SingleTower()
        {
            var em = _world.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoalTowerTag>());
            var entities = q.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length, "골 1개짜리 맵 → 타워 1기");
            var e = entities[0];
            entities.Dispose();
            return e;
        }

        // 핵심 — 라이브 타워의 진영은 DefenderCore 다. 이것이 최후순위 술어
        // ((faction & AnyStructure) != 0)에 걸리는 유일한 근거다.
        [Test]
        public void SpawnStructureEntities_TagsTowerAsDefenderCore()
        {
            SpawnTowersViaBridge();
            var em = _world.EntityManager;
            var tower = SingleTower();

            Assert.AreEqual(Faction.DefenderCore, em.GetComponentData<FactionTag>(tower).value,
                "타워 진영 = DefenderCore. DefenderUnit 이면 힐러·보스 사냥 필드가 타워를 유닛으로 오인한다");
            Assert.AreNotEqual(0, (int)em.GetComponentData<FactionTag>(tower).value & Factions.AnyStructure,
                "타워는 거점 분류(AnyStructure)에 속한다 — 거점 전담 적의 저작 마스크가 이 비트로 타워를 겨눈다");
            Assert.AreEqual(0, (int)em.GetComponentData<FactionTag>(tower).value & (int)Faction.DefenderUnit,
                "DefenderUnit 비트는 없어야 한다 — 지원계(힐·버프)가 거점을 대상으로 고르면 버퍼 부재 예외");
        }

        // 리뷰 M-d — 아키타입 단일 소스의 강제. 브리지 산물과 공용 픽스처 빌더
        // (StructureFixtures.MakeGoalTower) 산물의 **컴포넌트 집합이 동일**해야 한다.
        // 브리지 스폰이 바뀌면(컴포넌트 추가/제거) 이 단정이 깨져 빌더를 따라오게 만든다 —
        // 손으로 맞춘 사본이 조용히 낡던 원죄(최후순위 미발효)의 구조적 재발 방지선이다.
        [Test]
        public void BridgeTower_ComponentSet_MatchesSharedFixtureBuilder()
        {
            SpawnTowersViaBridge();
            var em = _world.EntityManager;
            var bridgeTower = SingleTower();
            var builderTower = StructureFixtures.MakeGoalTower(em, new float3(9f, 0f, 9f));

            using var bridgeTypes = em.GetComponentTypes(bridgeTower, Allocator.Temp);
            using var builderTypes = em.GetComponentTypes(builderTower, Allocator.Temp);

            var bridgeSet = new System.Collections.Generic.HashSet<ComponentType>();
            foreach (var t in bridgeTypes) bridgeSet.Add(t);
            var builderSet = new System.Collections.Generic.HashSet<ComponentType>();
            foreach (var t in builderTypes) builderSet.Add(t);

            bridgeSet.SymmetricExceptWith(builderSet);
            Assert.IsEmpty(bridgeSet,
                "브리지 타워와 픽스처 빌더 타워의 컴포넌트 집합이 갈렸다 — " +
                "StructureFixtures.MakeGoalTower 를 브리지 스폰과 동기화하라: " +
                string.Join(", ", bridgeSet));
        }

        // 아키타입 구성 — 피해를 받을 수 있고(IncomingDamage), 후보 스냅샷에 들어가고
        // (Health+LocalTransform), 유닛 축 시스템에는 안 잡힌다(DefenderUnitTag 없음).
        [Test]
        public void SpawnStructureEntities_ProducesDamageableNonUnitArchetype()
        {
            SpawnTowersViaBridge();
            var em = _world.EntityManager;
            var tower = SingleTower();

            Assert.IsTrue(em.HasBuffer<IncomingDamage>(tower), "IncomingDamage 버퍼 사전 부착");
            Assert.IsTrue(em.HasComponent<Health>(tower));
            Assert.IsTrue(em.HasComponent<LocalTransform>(tower));
            Assert.IsFalse(em.HasComponent<DefenderUnitTag>(tower), "타워는 유닛이 아니다");
            Assert.IsFalse(em.HasComponent<AttackState>(tower), "마음은 공격하지 않는다");

            var health = em.GetComponentData<Health>(tower);
            Assert.AreEqual(_deck.goalStabilityMax, health.max, 1e-4f, "상한은 덱에서 온다");
            Assert.AreEqual(health.max, health.value, 1e-4f, "만피로 시작");
        }

        // 브리지가 만든 타워가 **일반 후보로서 거리로 경쟁**하는지. 합성 골이 아니라 생산
        // 경로 산물을 대상으로 재므로 아키타입 drift 가 있으면 여기서 깨진다.
        // (goal-stability 의 «거점 최후순위» 는 폐기됐다 — 타입이 순위를 뒤집지 않는다.)
        [Test]
        public void BridgeSpawnedTower_CompetesByDistance_AsOrdinaryCandidate()
        {
            SpawnTowersViaBridge();
            var em = _world.EntityManager;
            var tower = SingleTower();
            float3 towerPos = em.GetComponentData<LocalTransform>(tower).Position;

            // 타워보다 **먼** 방어유닛 — 거리로 타워가 이겨야 한다.
            var defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(towerPos + new float3(1.5f, 0f, 0f)));
            em.AddComponentData(defender, new Health { value = 100f, max = 100f });
            em.AddComponentData(defender, new FactionTag { value = Faction.DefenderUnit });
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddBuffer<IncomingDamage>(defender);

            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(towerPos - new float3(0.5f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = 10f, max = 10f });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponent<AttackUnitTag>(enemy);
            em.AddBuffer<IncomingDamage>(enemy);
            em.AddComponentData(enemy, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                // BattleBridge.CreateAttackerEntity 가 굽는 base 마스크와 같은 조합.
                targetMask = (int)(Faction.DefenderUnit | Faction.BlockingHazard | Faction.DefenderCore),
            });
            var outputs = em.AddBuffer<AttackOutputElement>(enemy);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 4f },
            });

            var simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(tower).Length,
                "브리지가 만든 타워도 마스크에 들어온 일반 후보다 — 더 가까우면 맞는다");
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(defender).Length,
                "더 먼 방어유닛은 이 프레임에 맞지 않는다");
        }

        // -----------------------------------------------------------------------
        // Helpers (구 BattleBridgeGoalStabilityTests 에서 승계 — 567facbc^)

        private static void CallPrivateMethod(object target, string name)
        {
            var mi = target.GetType().GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"Method '{name}' not found on {target.GetType().Name}");
            mi.Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
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
            fi.SetValue(target, value);
        }
    }
}
