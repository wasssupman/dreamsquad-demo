using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // multi-goal-map 유닛 6 회귀 가드 — 풀 5맵의 핵심 불변식: "분리 복도 = 각 스폰이 자기 골로만".
    // MapConnectivity 는 "각 스폰이 아무 골이든 도달"만 검사하므로 복도가 병합돼도 통과한다.
    // 이 테스트는 각 스폰의 walk 연결 컴포넌트에 골이 정확히 1개, 스폰마다 다른 골임을 강제한다.
    // (throwaway scratchpad/akmaps_mg.py 검증기를 영구 회귀 테스트로 승격.)
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
        public void PoolMap_EachSpawnReachesExactlyOwnSeparateGoal([ValueSource(nameof(PoolPaths))] string path)
        {
            var doc = AssetDatabase.LoadAssetAtPath<MapDocument>(path);
            Assert.IsNotNull(doc, path + " 로드 실패");
            var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.Temp);
            try
            {
                Assert.AreEqual(map.spawns.Length, map.goals.Length,
                    path + ": 스폰 수 = 골 수 (각 복도 자기 골)");

                var reachedGoals = new HashSet<int2>();
                for (int s = 0; s < map.spawns.Length; s++)
                {
                    var comp = FloodWalk(map, map.spawns[s]);
                    int goalsInComp = 0;
                    int2 theGoal = default;
                    for (int g = 0; g < map.goals.Length; g++)
                        if (comp.Contains(map.goals[g])) { goalsInComp++; theGoal = map.goals[g]; }

                    Assert.AreEqual(1, goalsInComp,
                        $"{path}: 스폰 {map.spawns[s]} 컴포넌트에 골이 정확히 1개여야 (분리 복도)");
                    Assert.IsTrue(reachedGoals.Add(theGoal),
                        $"{path}: 스폰들이 서로 다른 골에 도달해야 (복도 병합 금지)");
                }
            }
            finally { map.Dispose(); }
        }

        private static HashSet<int2> FloodWalk(GeneratedMap map, int2 start)
        {
            var vis = new HashSet<int2>();
            var q = new Queue<int2>();
            vis.Add(start);
            q.Enqueue(start);
            int2[] d = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
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
            return vis;
        }
    }
}
