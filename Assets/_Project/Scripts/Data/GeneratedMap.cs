using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    // Phase 10: 판 1회용 맵 데이터. BattleBridge 가 owner.
    // BuildFromFixture / BuildFromManual / ProceduralMapGenerator.Generate / MapGridGenerator.Generate 중 하나로 생성.
    public struct GeneratedMap : IDisposable
    {
        public NativeArray<MapTileType> tiles;   // gridSize.x * gridSize.y
        public NativeArray<byte>        mergeDegree;   // path 셀이 인접 path 와 만나는 수. path 외 = 0.
        public NativeArray<byte>        chokepoint;    // 0/1. degree ≥ 3 인 path 셀만 1.
        public NativeArray<byte>        propLayerId;   // 장식 풀 인덱스. 기본 0 (없음).
        public int2                     gridSize;
        public NativeArray<int2>        spawns;  // 1~N
        public int2                     goal;
        public int                      seed;
        public int                      generatorVersion;

        public bool IsCreated => tiles.IsCreated && spawns.IsCreated;

        public int CellIndex(int2 cell) => cell.y * gridSize.x + cell.x;

        public MapTileType TileAt(int2 cell) => tiles[CellIndex(cell)];

        public void Dispose()
        {
            if (tiles.IsCreated)       tiles.Dispose();
            if (spawns.IsCreated)      spawns.Dispose();
            if (mergeDegree.IsCreated) mergeDegree.Dispose();
            if (chokepoint.IsCreated)  chokepoint.Dispose();
            if (propLayerId.IsCreated) propLayerId.Dispose();
        }
    }
}
