# GeneratedMap Runtime Struct

**작업 구분**: Phase 10A

## 목적

판 중 살아있는 맵 상태 전체를 담는 runtime-only struct. MonoBehaviour (BattleBridge) 가 owner. ECS 에는 주입하지 않음 (Q-B 결정).

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/GeneratedMap.cs`

## 구현

```csharp
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    // Phase 10: 판 1회용 맵 데이터. BattleBridge 가 owner.
    // BuildFromFixture / BuildFromManual / ProceduralMapGenerator.Generate 중 하나로 생성.
    public struct GeneratedMap : IDisposable
    {
        public NativeArray<MapTileType> tiles;   // gridSize.x * gridSize.y
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
            if (tiles.IsCreated)  tiles.Dispose();
            if (spawns.IsCreated) spawns.Dispose();
        }
    }
}
```

## 수명 계약

- `BattleBridge.BuildMapForBattle()` 이 생성자 호출 (BuildFromFixture / Manual / Procedural 분기)
- `Allocator.Persistent` 로 NativeArray 2개 할당
- 판 종료 시 `BattleBridge.TeardownCurrentBattle()` 에서 `map.Dispose()` + struct 재초기화
- 재시작/redraft 시 기존 Dispose 후 재생성 (Phase 9 FlowFieldSingleton 과 동일 패턴)

## 접근 규약

- 읽기 전용 소비자: `MapView`, `PlacementInput`, `FlowFieldBuilder`, 적 spawn 로직
- 쓰기 소유자: `BattleBridge` 만
- ECS 는 `FlowFieldSingleton` 만 읽음 (tiles 는 BattleBridge 가 walkmask 로 1회 변환 후 버림)

## 완료 기준

- `GeneratedMap.cs` 컴파일.
- EditMode 테스트: Dispose 멱등성 (두 번 호출 가능, IsCreated 체크).
- `CellIndex(new int2(x,y))` = `y * gridSize.x + x` 검증.
