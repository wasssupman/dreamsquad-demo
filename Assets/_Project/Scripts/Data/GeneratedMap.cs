using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    // Phase 10: 판 1회용 맵 데이터. BattleBridge 가 owner.
    // MapDocumentBuilder.ToGeneratedMap(authored 풀) / BuildFallbackLinear(connectivity 안전망)로 생성.
    public struct GeneratedMap : IDisposable
    {
        public NativeArray<MapTileType> tiles;   // gridSize.x * gridSize.y
        public NativeArray<byte>        mergeDegree;   // path 셀이 인접 path 와 만나는 수. path 외 = 0.
        public NativeArray<byte>        chokepoint;    // 0/1. degree ≥ 3 인 path 셀만 1.
        public NativeArray<byte>        propLayerId;   // 장식 풀 인덱스. 기본 0 (없음).
        public NativeArray<byte>        placeMask;     // 셀이 여는 PlacementLayer 비트(unit 0 의 0/1 → unit 4 층 비트필드). 배치 가능성의 정본 — tiles 종류와 직교.
        public int2                     gridSize;
        public NativeArray<int2>        spawns;  // 1~N
        public int2                     goal;    // primary = goals[0] (단일-점 소비자·폴백)
        public NativeArray<int2>        goals;   // multi-goal 목록. 소비 시 미생성/빈이면 [goal] 폴백(유닛 1·3).
        public NativeArray<float>       goalMaxStability;   // goals 와 index 정렬(같은 길이). 소비 시 미생성/길이 불일치면 전 골 0 폴백(goal-stability 유닛 0).
        public int                      seed;
        public int                      generatorVersion;

        // 주의: goals/goalMaxStability 는 IsCreated 불변식에 넣지 않는다 — 안 채우는 생산자(폴백/legacy)와
        // 테스트 픽스처가 IsCreated=false 로 뒤집히는 걸 막기 위함(multi-goal-map 유닛 0).
        public bool IsCreated => tiles.IsCreated && spawns.IsCreated;

        public int CellIndex(int2 cell) => cell.y * gridSize.x + cell.x;

        public MapTileType TileAt(int2 cell) => tiles[CellIndex(cell)];

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
            if (goalMaxStability.IsCreated) goalMaxStability.Dispose();
            if (mergeDegree.IsCreated) mergeDegree.Dispose();
            if (chokepoint.IsCreated)  chokepoint.Dispose();
            if (propLayerId.IsCreated) propLayerId.Dispose();
            if (placeMask.IsCreated)   placeMask.Dispose();
        }
    }
}
