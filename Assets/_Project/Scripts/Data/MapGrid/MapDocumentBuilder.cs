using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data.MapGrid
{
    public static class MapDocumentBuilder
    {
        public static GeneratedMap ToGeneratedMap(MapDocument doc, Allocator allocator)
        {
            int w = doc.Width;
            int h = doc.Height;
            int n = w * h;

            var tiles = new NativeArray<MapTileType>(n, allocator);
            var mergeDegree = new NativeArray<byte>(n, allocator);
            var chokepoint = new NativeArray<byte>(n, allocator);
            var propLayerId = new NativeArray<byte>(n, allocator);
            var placeMask = new NativeArray<byte>(n, allocator);

            // placeMask: doc 저작본(길이 일치) 채택 + 0/1 정규화, 아니면 tiles==Place 파생.
            // 빌더 산출물 불변식: IsCreated ⇒ placeMask 생성됨 (placement-mask unit 0).
            var docMask = doc.PlaceMask;
            bool hasAuthoredMask = docMask != null && docMask.Count == n;

            for (int i = 0; i < n; i++)
            {
                tiles[i] = doc.Tiles[i];
                mergeDegree[i] = doc.MergeDegree[i];
                chokepoint[i] = (byte)(doc.Chokepoint[i] ? 1 : 0);
                propLayerId[i] = doc.PropLayerId[i];
                placeMask[i] = hasAuthoredMask
                    ? PlacementLayers.Sanitize(docMask[i])
                    : PlacementLayers.Derive(doc.Tiles[i]);
            }

            var spawns = new NativeArray<int2>(doc.Spawns.Count, allocator);
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = new int2(doc.Spawns[i].x, doc.Spawns[i].y);

            // 멀티골: doc.Goals 있으면 그대로, 없으면 primary [doc.Goal] 폴백 (유닛 0 계약).
            var docGoals = doc.Goals;
            bool hasGoals = docGoals != null && docGoals.Count > 0;
            var goals = new NativeArray<int2>(hasGoals ? docGoals.Count : 1, allocator);
            if (hasGoals)
                for (int i = 0; i < goals.Length; i++) goals[i] = new int2(docGoals[i].x, docGoals[i].y);
            else
                goals[0] = new int2(doc.Goal.x, doc.Goal.y);

            // 안정도: doc 배열이 (폴백 후) goals 와 길이 일치할 때만 채택, 아니면 전 골 0 (goal-stability 유닛 0).
            var docStability = doc.GoalMaxStability;
            var goalMaxStability = new NativeArray<float>(goals.Length, allocator);
            if (docStability != null && docStability.Count == goals.Length)
                for (int i = 0; i < goals.Length; i++)
                    goalMaxStability[i] = math.max(0f, docStability[i]);

            return new GeneratedMap
            {
                tiles = tiles,
                mergeDegree = mergeDegree,
                chokepoint = chokepoint,
                propLayerId = propLayerId,
                placeMask = placeMask,
                gridSize = new int2(w, h),
                spawns = spawns,
                goal = goals[0],
                goals = goals,
                goalMaxStability = goalMaxStability,
                seed = doc.AuthoringSeed,
                generatorVersion = doc.GeneratorVersion,
            };
        }

        public static void WriteToDocument(MapDocument doc, in GeneratedMap map)
        {
            int n = map.gridSize.x * map.gridSize.y;
            var tiles = new MapTileType[n];
            var mergeDegree = new byte[n];
            var chokepoint = new bool[n];
            var propLayerId = new byte[n];
            var placeMask = new byte[n];

            for (int i = 0; i < n; i++)
            {
                tiles[i] = map.tiles[i];
                mergeDegree[i] = map.mergeDegree[i];
                chokepoint[i] = map.chokepoint[i] != 0;
                propLayerId[i] = map.propLayerId[i];
                // 미생성 map(직접 구성) 은 파생으로 채워 내보냄 — 빌더 경유 map 은 항상 생성돼 있다.
                placeMask[i] = map.placeMask.IsCreated
                    ? PlacementLayers.Sanitize(map.placeMask[i])
                    : PlacementLayers.Derive(map.tiles[i]);
            }

            var spawns = new Vector2Int[map.spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = new Vector2Int(map.spawns[i].x, map.spawns[i].y);

            // 멀티골: map.goals 있으면 그대로, 없으면 primary [map.goal] 폴백.
            Vector2Int[] goals;
            if (map.goals.IsCreated && map.goals.Length > 0)
            {
                goals = new Vector2Int[map.goals.Length];
                for (int i = 0; i < goals.Length; i++)
                    goals[i] = new Vector2Int(map.goals[i].x, map.goals[i].y);
            }
            else
            {
                goals = new[] { new Vector2Int(map.goal.x, map.goal.y) };
            }

            // 안정도: goals 와 길이 일치 시 보존, 아니면 전 골 0 (goal-stability 유닛 0).
            var goalStability = new float[goals.Length];
            if (map.goalMaxStability.IsCreated && map.goalMaxStability.Length == goals.Length)
                for (int i = 0; i < goals.Length; i++)
                    goalStability[i] = Mathf.Max(0f, map.goalMaxStability[i]);

            doc.SetFrom(
                map.gridSize.x, map.gridSize.y,
                tiles, mergeDegree, chokepoint, propLayerId,
                goals,
                spawns,
                map.seed, map.generatorVersion,
                goalStability,
                placeMask);
        }
    }
}
