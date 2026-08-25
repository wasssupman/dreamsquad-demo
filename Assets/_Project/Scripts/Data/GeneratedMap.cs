using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    // Phase 10: 판 1회용 맵 데이터. BattleBridge 가 owner.
    // map-diorama-stage — 생산자는 DioramaMapBuilder.Assemble(스테이지 스캔). 연결성 실패는 하드 실패
    // (BuildFallbackLinear 폴백 은퇴 — 테스트 픽스처 빌더로만 잔존).
    public struct GeneratedMap : IDisposable
    {
        public NativeArray<MapTileType> tiles;   // gridSize.x * gridSize.y
        public NativeArray<byte>        placeMask;     // 셀이 여는 PlacementLayer 비트(unit 0 의 0/1 → unit 4 층 비트필드). 배치 가능성의 정본 — tiles 종류와 직교.
        public int2                     gridSize;
        public NativeArray<int2>        spawns;  // 1~N
        public int2                     goal;    // primary = goals[0] (단일-점 소비자·폴백)
        public NativeArray<int2>        goals;   // multi-goal 목록. 소비 시 미생성/빈이면 [goal] 폴백(유닛 1·3).
        // waypoint-routing unit 0 — 가변 길이 경로를 flatten 한 런타임 투영.
        // waypointRanges[path] = (waypointCells start, count). 미생성/빈 = 경로 없음.
        public NativeArray<int2>        waypointCells;
        public NativeArray<int2>        waypointRanges;
        // waypoint-routing unit 8 — 스폰(레인)별 기본 경로 인덱스. spawns 와 같은 길이로
        // 정규화돼 들어온다(MapDocumentBuilder). 미생성/빈 = 전 레인 최단거리(-1).
        public NativeArray<int>         spawnRoutes;
        // battle-structures unit 3 — 거점 저작의 런타임 투영(셀 + 교차 비트). 스탯은 SO 에
        // 남고 브리지가 문서에서 읽는다(unit 4). 미생성/빈 = 거점 없는 맵.
        public NativeArray<StructurePlacement> structures;
        // bonus-wave-pull unit 1 — 보너스 당기기 포탈 칸의 런타임 투영. 미생성/빈 = 그 맵엔
        // 보너스 당기기가 없다. **spawns 와 섞지 않는다** — spawns.Length 는 레인 수이고
        // ExpandWave 의 라운드로빈 분모다(MapDocument.bonusSpawns 주석 참조).
        // goals 와 같은 이유로 IsCreated 불변식에는 넣지 않는다.
        public NativeArray<int2>        bonusSpawns;
        public int                      seed;
        public int                      generatorVersion;

        // 주의: goals 는 IsCreated 불변식에 넣지 않는다 — 안 채우는 생산자(폴백/legacy)와
        // 테스트 픽스처가 IsCreated=false 로 뒤집히는 걸 막기 위함(multi-goal-map 유닛 0).
        public bool IsCreated => tiles.IsCreated && spawns.IsCreated;

        public int CellIndex(int2 cell) => cell.y * gridSize.x + cell.x;

        public MapTileType TileAt(int2 cell) => tiles[CellIndex(cell)];

        public int WaypointPathCount => waypointRanges.IsCreated ? waypointRanges.Length : 0;

        // waypoint-routing unit 8 — 미생성·범위 밖·음수 인덱스는 전부 -1(최단거리 폴백).
        // 예외를 던지지 않는다 — 호출부(WavePatternGenerator 등)가 매 스폰마다 부르는 조회다.
        public int RouteForSpawn(int laneIndex)
        {
            if (!spawnRoutes.IsCreated || laneIndex < 0 || laneIndex >= spawnRoutes.Length)
                return -1;
            return spawnRoutes[laneIndex];
        }

        public int2 WaypointCellAt(int pathIndex, int cellIndex)
        {
            if (!waypointRanges.IsCreated || pathIndex < 0 || pathIndex >= waypointRanges.Length)
                throw new ArgumentOutOfRangeException(nameof(pathIndex));

            int2 range = waypointRanges[pathIndex];
            if (cellIndex < 0 || cellIndex >= range.y)
                throw new ArgumentOutOfRangeException(nameof(cellIndex));

            return waypointCells[range.x + cellIndex];
        }

        // placement-mask unit 0/4 — 이 셀이 여는 배치 층 비트. 마스크 미생성(직접 구성 픽스처/
        // legacy 생산자)이면 타일 종류에서 파생 폴백 (goals 를 IsCreated 불변식에서 뺀 것과 같은 보호 전략).
        public byte LayersAt(int2 cell)
            => placeMask.IsCreated ? PlacementLayers.Sanitize(placeMask[CellIndex(cell)])
                                   : PlacementLayers.Derive(tiles[CellIndex(cell)]);

        // 배치 가능 판정 = 셀 층 ∩ 유닛 층. 코드는 유닛 클래스를 보지 않는다 — 비트만 본다(unit 4).
        public bool PlaceableAt(int2 cell, PlacementLayer layers)
            => (LayersAt(cell) & (byte)layers) != 0;

        public void Dispose()
        {
            if (tiles.IsCreated)            tiles.Dispose();
            if (spawns.IsCreated)           spawns.Dispose();
            if (goals.IsCreated)            goals.Dispose();
            if (waypointCells.IsCreated)    waypointCells.Dispose();
            if (waypointRanges.IsCreated)   waypointRanges.Dispose();
            if (spawnRoutes.IsCreated)      spawnRoutes.Dispose();
            if (structures.IsCreated)       structures.Dispose();
            if (placeMask.IsCreated)        placeMask.Dispose();
            if (bonusSpawns.IsCreated)    bonusSpawns.Dispose();
        }
    }
}
