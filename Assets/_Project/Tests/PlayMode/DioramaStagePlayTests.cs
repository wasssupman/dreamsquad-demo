using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // map-diorama-stage unit 2 — 라이브 경로 스모크 (critic M-12: «행동 변화 0» 주장은 라이브
    // 경로 테스트가 있을 때만). traversal-layers 의 교훈이 검증 축이다: 순수 함수가 옳은 값을
    // 내는가가 아니라 **라이브에서 유닛의 셀이 실제로 바뀌는가**.
    //   ① 적이 열린 마당에서 셀을 실제로 옮긴다 (스폰 후 N프레임 내 셀 변화)
    //   ② 골 방향으로 실제 전진한다 (x 진행량)
    //   ③ 차단 프랍 footprint 셀을 한 번도 밟지 않는다
    // 판 = MapStage_Duel (unit 11 — main 현행 Duel 23×10 의 Street 제작방식 재저작). 구 Fixture(12×8
    // KayKit 마당)는 unit 12 에서 은퇴. 골 (2,4) 이 서쪽이라 전진 = −x.
    public class DioramaStagePlayTests
    {
        private const string DuelStageName = "MapStage_Duel";
        private const string SlimePath = "Assets/_Project/Data/Enemies/Enemy_Slime.asset";
        private const int GoalX = 2;

        // Duel 차단 셀 = 중앙 분리대 x=11 (MapStageDuelGenerator 레이아웃과 동기) — 통로 y1~2 · y7~8.
        private static readonly int2[] BlockedCells =
            { new int2(11, 0), new int2(11, 3), new int2(11, 4), new int2(11, 5), new int2(11, 6), new int2(11, 9) };

        private int _savedMap;

        // 스테이지 풀에서 이름으로 슬롯을 찾는다 — BattleBridgeTestAccess.MapSlot 의 스테이지판.
        private static int StageSlot(string stageName)
        {
            var pool = UnityEditor.AssetDatabase.LoadAssetAtPath<MapStagePool>(
                "Assets/_Project/Data/Maps/MapStagePool.asset");
            Assert.IsNotNull(pool, "MapStagePool.asset 이 없다");
            for (int i = 0; i < pool.Count; i++)
                if (pool.Get(i).stage != null && pool.Get(i).stage.name == stageName) return i;
            for (int i = 0; i < pool.DevCount; i++)
                if (pool.GetDev(i).stage != null && pool.GetDev(i).stage.name == stageName)
                    return pool.Count + i;
            Assert.Fail($"'{stageName}' 이 스테이지 풀에 없다");
            return -1;
        }

        [UnityTearDown]
        public IEnumerator RestoreMapPin()
        {
            DevMapOverride.Index = _savedMap;
            yield break;
        }

        [UnityTest]
        public IEnumerator Enemy_CrossesOpenYard_AvoidsFootprints_ProgressesToGoal()
        {
            // 스위트 관례 — 씬 언로드 시 PrimeTween «OnComplete callback was ignored» 에러 로그가 단언과 무관하게
            // 테스트를 떨어뜨린다. 맵 빌드 실패는 아래 HasGeneratedMap 단언이 명시적으로 잡는다.
            LogAssert.ignoreFailingMessages = true;
            _savedMap = DevMapOverride.Index;
            DevMapOverride.Index = StageSlot(DuelStageName);

            yield return BattleBridgeTestAccess.LoadBattleScene();
            var bridge = Object.FindFirstObjectByType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleScene 에 BattleBridge 가 없다");
            Assert.IsTrue(bridge.HasGeneratedMap, "스테이지 맵 빌드 실패 — 콘솔의 hard-fail 로그 확인");

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var slime = BattleBridgeTestAccess.LoadEnemy(SlimePath);
            var enemy = BattleBridgeTestAccess.SpawnEnemy(bridge, em, slime);
            Assert.AreNotEqual(Entity.Null, enemy, "적 스폰 실패");

            int2 startCell = CellOf(em, enemy);
            float startX = em.GetComponentData<LocalTransform>(enemy).Position.x;
            Assert.Greater(startX, GoalX, "Duel 은 동쪽 스폰(x 20) → 서쪽 골(x 2) 판이어야 한다");

            // ① 셀 변화 — 300프레임(≈5초) 내. «한 칸도 못 움직였다»가 이 단언에서 빨갛게 보인다.
            bool cellChanged = false;
            for (int f = 0; f < 300 && !cellChanged; f++)
            {
                yield return null;
                if (!em.Exists(enemy)) Assert.Fail("적이 이동 검증 전에 사라졌다");
                var cell = CellOf(em, enemy);
                AssertNotOnBlockedCell(cell, f);
                cellChanged = !cell.Equals(startCell);
            }
            Assert.IsTrue(cellChanged, "적의 셀이 300프레임 안에 바뀌지 않았다 — 열린 마당 이동 실패");

            // ②③ 골 방향 전진(−x 로 4셀 이상) — 30초 내. 매 프레임 차단 셀 침범 감시.
            float deadline = Time.time + 30f;
            float bestX = startX;
            while (Time.time < deadline)
            {
                yield return null;
                if (!em.Exists(enemy)) break;   // 골 도달/소멸(본능 사격 포함) — 전진 증거는 bestX 로 판단
                var cell = CellOf(em, enemy);
                AssertNotOnBlockedCell(cell, -1);
                bestX = math.min(bestX, em.GetComponentData<LocalTransform>(enemy).Position.x);
                if (startX - bestX >= 4f) break;
            }
            Assert.GreaterOrEqual(startX - bestX, 4f,
                $"적이 골 방향(−x)으로 전진하지 못했다 (start {startX:F1} → best {bestX:F1})");
        }

        // unit 10 라이브 검증 — StructureMarker 4기(방어 Guard 2 · 적 Watch 2)가 맵 빌드 시 프랍으로,
        // 전투 시작 시 StructureTag 엔티티로 선다. 마음(Core)은 없다(계약 11) — 골 타워 1 만 DefenderCore.
        [UnityTest]
        public IEnumerator Duel_BattleStart_SpawnsFourInstinctStructures()
        {
            LogAssert.ignoreFailingMessages = true;   // 위와 같은 이유
            _savedMap = DevMapOverride.Index;
            DevMapOverride.Index = StageSlot(DuelStageName);

            yield return BattleBridgeTestAccess.LoadBattleScene();
            var bridge = Object.FindFirstObjectByType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleScene 에 BattleBridge 가 없다");
            Assert.IsTrue(bridge.HasGeneratedMap, "스테이지 맵 빌드 실패");

            // 뷰는 맵 빌드 시점(배치 페이즈부터 보인다 — battle-structures 후속 2).
            int turretViews = Object.FindObjectsByType<Wassup.Presentation.StructureTurretView>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(4, turretViews, "본능 프랍(StructureTurretView) 4개가 맵 빌드 시 서야 한다");

            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();
            yield return null;
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StructureTag>());
            using var tags = query.ToComponentDataArray<StructureTag>(Allocator.Temp);
            int instincts = 0, defenderInstincts = 0, cores = 0;
            foreach (var t in tags)
            {
                if (StructurePlacements.IsInstinct(t.faction)) { instincts++; if (t.faction == Faction.DefenderInstinct) defenderInstincts++; }
                if (StructurePlacements.IsCore(t.faction)) cores++;
            }
            Assert.AreEqual(4, instincts, "본능 엔티티 4");
            Assert.AreEqual(2, defenderInstincts, "그중 방어 본능 2");
            Assert.AreEqual(1, cores, "마음은 골 타워(DefenderCore) 1 만 — 적 마음은 계약 11 비가용");
        }

        private static int2 CellOf(EntityManager em, Entity e)
        {
            // sim origin = float3.zero(계약) · tileSize = 1(BattleScene) — 셀 = floor(xz).
            var p = em.GetComponentData<LocalTransform>(e).Position;
            return new int2((int)math.floor(p.x), (int)math.floor(p.z));
        }

        private static void AssertNotOnBlockedCell(int2 cell, int frame)
        {
            for (int i = 0; i < BlockedCells.Length; i++)
                Assert.IsFalse(cell.Equals(BlockedCells[i]),
                    $"적이 차단 footprint 셀 {cell} 을 밟았다 (frame {frame})");
        }
    }
}
