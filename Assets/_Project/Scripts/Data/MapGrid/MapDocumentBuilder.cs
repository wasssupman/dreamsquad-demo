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

            // unit 6 — 공성 문서는 spawns 를 저작하지 않는다(파생이 채운다). null/0 허용.
            var docSpawns = doc.Spawns;
            int spawnCount = docSpawns != null ? docSpawns.Count : 0;
            var spawns = new NativeArray<int2>(spawnCount, allocator);
            for (int i = 0; i < spawnCount; i++)
                spawns[i] = new int2(docSpawns[i].x, docSpawns[i].y);

            // 멀티골: doc.Goals 있으면 그대로, 없으면 primary [doc.Goal] 폴백 (유닛 0 계약).
            var docGoals = doc.Goals;
            bool hasGoals = docGoals != null && docGoals.Count > 0;
            var goals = new NativeArray<int2>(hasGoals ? docGoals.Count : 1, allocator);
            if (hasGoals)
                for (int i = 0; i < goals.Length; i++) goals[i] = new int2(docGoals[i].x, docGoals[i].y);
            else
                goals[0] = new int2(doc.Goal.x, doc.Goal.y);

            // battle-structures unit 3 — 거점 투영. data 가 빈 엔트리는 건너뛴다(저작 사고
            // 방어 — OnValidate 가 이미 에러로 알린다). 진영은 (편 × 종류) 파생.
            var docStructures = doc.Structures;
            int structureCount = 0;
            if (docStructures != null)
                for (int i = 0; i < docStructures.Count; i++)
                    if (docStructures[i].data != null) structureCount++;
            var structures = new NativeArray<StructurePlacement>(structureCount, allocator);
            if (structureCount > 0)
            {
                int written = 0;
                for (int i = 0; i < docStructures.Count; i++)
                {
                    var s = docStructures[i];
                    if (s.data == null) continue;
                    structures[written++] = new StructurePlacement
                    {
                        cell = new int2(s.cell.x, s.cell.y),
                        faction = StructurePlacements.DeriveFaction(s.side, s.data.kind),
                    };
                }
            }

            // battle-structures unit 6 — 공성 모드 파생. **적 마음의 유무가 곧 모드다**:
            // 적 마음이 있으면 그 셀이 스폰이다(저작 spawns 무시 — «공성 + spawns 저작» 은
            // 검증 에러지만 뚫고 와도 여기서 덮는다). spawns[] 소비처 8곳은 전부 «셀 좌표
            // 목록» 만 보므로 이 한 블록으로 하류 전체가 모드를 모른 채 공성으로 돈다.
            // 공성 규칙(적 마음 정확히 1)은 저작 검증 몫 — 파생은 기계적으로만 처리한다.
            int enemyCoreCount = 0;
            for (int i = 0; i < structures.Length; i++)
                if (structures[i].faction == Wassup.Battle.Units.Faction.EnemyCore) enemyCoreCount++;
            if (enemyCoreCount > 0)
            {
                spawns.Dispose();
                spawns = new NativeArray<int2>(enemyCoreCount, allocator);
                int sw = 0;
                for (int i = 0; i < structures.Length; i++)
                    if (structures[i].faction == Wassup.Battle.Units.Faction.EnemyCore)
                        spawns[sw++] = structures[i].cell;
            }

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
                structures = structures,
                seed = doc.AuthoringSeed,
                generatorVersion = doc.GeneratorVersion,
            };
        }

        // battle-structures unit 3 — structures 는 **별도 인자**다. GeneratedMap 은 unmanaged 라
        // StructureData 참조를 왕복시킬 수 없어, 저작 주체(페인터)가 관리 엔트리를 직접
        // 넘긴다. null = 거점 저작을 건드리지 않는다(기존 호출자 무회귀).
        public static void WriteToDocument(MapDocument doc, in GeneratedMap map,
            StructureEntry[] structures = null)
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

            doc.SetFrom(
                map.gridSize.x, map.gridSize.y,
                tiles, mergeDegree, chokepoint, propLayerId,
                goals,
                spawns,
                map.seed, map.generatorVersion,
                placeMask);
            if (structures != null) doc.SetStructures(structures);
        }
    }
}
