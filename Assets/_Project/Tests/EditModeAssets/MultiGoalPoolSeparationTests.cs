using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // multi-goal-map 유닛 6 회귀 가드 — 풀 맵의 골/복도 불변식.
    //
    // map-rework unit 1 — 불변식이 **컨셉별로 갈라졌다.** 개편된 맵(ReworkedPaths)은
    // «광장 합류»가 컨셉의 핵심이라 옛 «복도는 골에서만 만난다»가 정의상 성립하지 않는다.
    // 대신 새 계약(골 1 · 폭1 Walk 0칸 · 4×4 광장 존재)을 고정한다. 미개편 맵은 자기
    // unit(map-rework 2~5)이 올 때까지 옛 계약을 유지한다 — 개편 중 조용히 썩지 않게.
    public class MultiGoalPoolSeparationTests
    {
        private static readonly string[] PoolPaths =
        {
            "Assets/_Project/Data/Maps/MapDocument_Serpent.asset",
            "Assets/_Project/Data/Maps/MapDocument_Coil.asset",
            "Assets/_Project/Data/Maps/MapDocument_Twin.asset",
            "Assets/_Project/Data/Maps/MapDocument_Spiral.asset",
            "Assets/_Project/Data/Maps/MapDocument_Zig.asset",
            // map-rework unit 8b — 12×7 소형 맵. **풀 미등록**(풀 파일은 병행 WIP 와 공유라
            // 건드리지 않는다) 이지만 계약은 여기서 받는다 — 저작물이 규칙 밖에 있으면
            // 「만들었는데 아무도 안 재는」 맵이 된다.
            "Assets/_Project/Data/Maps/MapDocument_Comb.asset",
        };

        // map-rework 진행표 — 맵 unit 이 완료될 때마다 여기로 옮긴다.
        private static readonly HashSet<string> ReworkedPaths = new()
        {
            "Assets/_Project/Data/Maps/MapDocument_Serpent.asset",   // unit 1
            "Assets/_Project/Data/Maps/MapDocument_Coil.asset",      // unit 2
            "Assets/_Project/Data/Maps/MapDocument_Twin.asset",      // unit 3
            "Assets/_Project/Data/Maps/MapDocument_Spiral.asset",    // unit 4
            "Assets/_Project/Data/Maps/MapDocument_Zig.asset",       // unit 5
            "Assets/_Project/Data/Maps/MapDocument_Comb.asset",      // unit 8b (12×7, 신설)
        };

        // test-suite-fast-lane unit 2 — 근접 차단칸(≥40%) 재저작 대기 목록.
        // 옛 방식은 이 4맵을 «의도적으로 빨갛게» 뒀는데, 그러면 스위트의 빨강이
        // 평상시 상태가 되어 진짜 회귀가 묻힌다. 대기 중에는 choke/width2 단언만
        // 건너뛰고(골·광장·연결성은 계속 지킨다), 재저작이 끝나 계약을 통과하기
        // 시작하면 아래 래칫 테스트가 빨개지며 «목록에서 빼라»고 알린다.
        private static readonly HashSet<string> PendingMeleeRework = new()
        {
            "Assets/_Project/Data/Maps/MapDocument_Coil.asset",      // map-rework unit 9 대기
            "Assets/_Project/Data/Maps/MapDocument_Twin.asset",      // map-rework unit 10 대기
            "Assets/_Project/Data/Maps/MapDocument_Spiral.asset",    // map-rework unit 11 대기
            "Assets/_Project/Data/Maps/MapDocument_Zig.asset",       // map-rework unit 12 대기
        };

        [Test]
        public void PoolMap_GoalsCapped_And_CorridorsSeparateExceptAtGoals([ValueSource(nameof(PoolPaths))] string path)
        {
            var doc = AssetDatabase.LoadAssetAtPath<MapDocument>(path);
            Assert.IsNotNull(doc, path + " 로드 실패");

            if (ReworkedPaths.Contains(path))
            {
                // 새 컨셉 계약 (map-rework 계약 1~3, unit 7 에서 폭 규칙 반전)
                Assert.AreEqual(1, doc.Goals?.Count ?? 1, $"{path}: 개편 맵의 마음은 1개");

                // unit 7 — ~~폭1 금지~~ → **근접이 설 자리를 요구한다.** 옛 계약대로 폭2 로만
                // 깔았더니 근접 유닛이 판에서 사라졌다(완전차단칸 0%). 재저작 대기 맵은
                // PendingMeleeRework 가 choke 계약만 유예한다 (fast-lane unit 2).
                MapConceptRules.MeasureMeleeLanes(
                    doc.Tiles, doc.PlaceMask, doc.Width, doc.Height,
                    out int walk, out int choke, out int width2);
                Assert.Greater(walk, 0, $"{path}: Walk 칸이 없다");
                if (!PendingMeleeRework.Contains(path))
                {
                    Assert.GreaterOrEqual(choke / (float)walk, MapConceptRules.MinChokeRatio,
                        $"{path}: 근접 완전차단칸 {choke}/{walk} — 직선 구간이 폭1 이어야 근접이 선다");
                    Assert.LessOrEqual(width2 / (float)walk, MapConceptRules.MaxWidth2Ratio,
                        $"{path}: 폭2 Walk {width2}/{walk} — 폭2 는 제한적으로");
                }
                Assert.IsTrue(MapConceptRules.HasPlaza(doc.Tiles, doc.Width, doc.Height),
                    $"{path}: 4×4 광장 없음");
                // 연결성은 아래 flood 가 계속 확인한다(골 도달 단언 공유).
            }

            var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.Temp);
            try
            {
                Assert.IsTrue(map.goals.Length >= 1 && map.goals.Length <= 2,
                    $"{path}: 골 {map.goals.Length}개 (1~2 여야)");

                var goalSet = new HashSet<int2>();
                for (int i = 0; i < map.goals.Length; i++) goalSet.Add(map.goals[i]);

                var corridors = new List<HashSet<int2>>();
                for (int s = 0; s < map.spawns.Length; s++)
                {
                    var corridor = FloodStopAtGoals(map, map.spawns[s], goalSet, out bool reachedGoal);
                    Assert.IsTrue(reachedGoal, $"{path}: 스폰 {map.spawns[s]} 이 어떤 골에도 도달 못함");
                    corridors.Add(corridor);
                }

                // 복도 분리는 **옛 컨셉의 계약**이다 — 개편 맵은 광장 합류가 정의라 제외.
                if (!ReworkedPaths.Contains(path))
                    for (int a = 0; a < corridors.Count; a++)
                        for (int b = a + 1; b < corridors.Count; b++)
                            foreach (var c in corridors[a])
                                Assert.IsFalse(corridors[b].Contains(c),
                                    $"{path}: 스폰 {a}·{b} 복도가 non-goal 셀 {c} 를 공유(병합) — 골에서만 만나야");
            }
            finally { map.Dispose(); }
        }

        // 래칫 — pending 맵이 실제로는 근접 계약을 통과하면 여기가 빨개진다.
        // 그때 할 일은 하나다: 그 맵을 PendingMeleeRework 에서 빼서 본 계약을 재무장한다.
        // (이게 없으면 재저작이 끝나도 목록이 조용히 남아 choke 계약이 영구 유예된다.)
        [Test]
        public void PendingMeleeRework_OnlyHoldsMapsThatStillFailTheContract(
            [ValueSource(nameof(PoolPaths))] string path)
        {
            if (!PendingMeleeRework.Contains(path)) return;

            var doc = AssetDatabase.LoadAssetAtPath<MapDocument>(path);
            Assert.IsNotNull(doc, path + " 로드 실패");
            MapConceptRules.MeasureMeleeLanes(
                doc.Tiles, doc.PlaceMask, doc.Width, doc.Height,
                out int walk, out int choke, out _);
            Assert.Less(choke / (float)walk, MapConceptRules.MinChokeRatio,
                $"{path}: 근접 계약을 이미 통과한다({choke}/{walk}) — 재저작 완료. "
                + "PendingMeleeRework 에서 이 맵을 제거해 choke 계약을 재무장할 것");
        }

        // 스폰에서 walk flood, 골 셀에서 흡수(확장 안 함). corridor = 도달한 non-goal walk 셀.
        private static HashSet<int2> FloodStopAtGoals(GeneratedMap map, int2 start, HashSet<int2> goals, out bool reachedGoal)
        {
            reachedGoal = false;
            var vis = new HashSet<int2> { start };
            var q = new Queue<int2>();
            q.Enqueue(start);
            int2[] d = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (goals.Contains(c)) { reachedGoal = true; continue; } // 골에서 흡수
                for (int k = 0; k < 4; k++)
                {
                    var nn = c + d[k];
                    if (nn.x < 0 || nn.x >= map.gridSize.x || nn.y < 0 || nn.y >= map.gridSize.y) continue;
                    if (vis.Contains(nn)) continue;
                    if (map.tiles[nn.y * map.gridSize.x + nn.x] != MapTileType.Walk) continue;
                    vis.Add(nn);
                    q.Enqueue(nn);
                }
            }
            vis.RemoveWhere(goals.Contains); // corridor = non-goal
            return vis;
        }
    }
}
