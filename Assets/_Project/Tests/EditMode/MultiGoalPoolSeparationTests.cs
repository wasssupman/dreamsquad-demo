using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // multi-goal-map 유닛 6 회귀 가드 — 풀 맵의 골/복도 불변식(혼합: 분리 2골 또는 수렴 1골).
    //   • 골 1~2개(사용자 결정: 목표지점 1~2개만).
    //   • 각 스폰이 골에 도달.
    //   • 복도는 골 셀에서만 만난다 — 두 스폰의 corridor(non-goal walk)는 겹치지 않는다.
    //     (분리 맵=완전 분리, 수렴 맵=골에서만 합류. MapConnectivity 는 "아무 골이든 도달"만 봐서
    //      복도가 non-goal 에서 병합돼도 통과하므로 이 테스트가 유일 가드.)
    public class MultiGoalPoolSeparationTests
    {
        private static readonly string[] PoolPaths =
        {
            "Assets/_Project/Data/Maps/MapDocument_Serpent.asset",
            "Assets/_Project/Data/Maps/MapDocument_Coil.asset",
            "Assets/_Project/Data/Maps/MapDocument_Twin.asset",
            "Assets/_Project/Data/Maps/MapDocument_Spiral.asset",
            "Assets/_Project/Data/Maps/MapDocument_Zig.asset",
        };

        [Test]
        public void PoolMap_GoalsCapped_And_CorridorsSeparateExceptAtGoals([ValueSource(nameof(PoolPaths))] string path)
        {
            var doc = AssetDatabase.LoadAssetAtPath<MapDocument>(path);
            Assert.IsNotNull(doc, path + " 로드 실패");
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

                for (int a = 0; a < corridors.Count; a++)
                    for (int b = a + 1; b < corridors.Count; b++)
                        foreach (var c in corridors[a])
                            Assert.IsFalse(corridors[b].Contains(c),
                                $"{path}: 스폰 {a}·{b} 복도가 non-goal 셀 {c} 를 공유(병합) — 골에서만 만나야");
            }
            finally { map.Dispose(); }
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
