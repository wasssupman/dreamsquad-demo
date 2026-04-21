# Multi-Spawn Connectivity + Fallback

**작업 구분**: Phase 10A

## 목적

모든 spawn 타일에서 goal 타일까지 flow field 로 도달 가능한지 BFS 로 검증. 실패 시 하드코딩 직선 맵으로 fallback (freeze 방지).

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/MapConnectivity.cs` (static helper)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`BuildMapForBattle` 에서 호출)
- 새 static: `BattleMapBuilder.BuildFallbackLinear(gridSize)`

## 구현

### MapConnectivity

```csharp
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;
using Wassup.Battle.Effects;  // FlowFieldBuilder 재사용

namespace Wassup.Data
{
    public static class MapConnectivity
    {
        // goal 기준 BFS 로 dist 계산 후 모든 spawn 이 도달 가능한지 검증.
        // H-4 fix: spawn / goal 이 gridSize 내부인지 먼저 검증 (out-of-bounds 시 false).
        // M-1 fix: NativeArray 3개 default 선언 후 try 안에서 할당 → 중간 실패 시 leak 없음.
        public static bool AllSpawnsReachGoal(GeneratedMap map)
        {
            // Bounds pre-check (H-4)
            var gs = map.gridSize;
            if (gs.x <= 0 || gs.y <= 0) return false;
            if (!InBounds(map.goal, gs)) return false;
            for (int s = 0; s < map.spawns.Length; s++)
                if (!InBounds(map.spawns[s], gs)) return false;

            int n = gs.x * gs.y;
            NativeArray<byte>   walk = default;
            NativeArray<float2> flow = default;
            NativeArray<int>    dist = default;
            try
            {
                walk = new NativeArray<byte>(n, Allocator.Temp);
                flow = new NativeArray<float2>(n, Allocator.Temp);
                dist = new NativeArray<int>(n, Allocator.Temp);

                for (int i = 0; i < n; i++)
                    walk[i] = (byte)(map.tiles[i] == MapTileType.Walk ? 1 : 0);

                FlowFieldBuilder.Build(walk, gs, map.goal, flow, dist);

                for (int s = 0; s < map.spawns.Length; s++)
                {
                    int idx = map.CellIndex(map.spawns[s]);
                    if (dist[idx] == int.MaxValue) return false;
                }
                return true;
            }
            finally
            {
                if (walk.IsCreated) walk.Dispose();
                if (flow.IsCreated) flow.Dispose();
                if (dist.IsCreated) dist.Dispose();
            }
        }

        private static bool InBounds(int2 cell, int2 gridSize)
            => cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;
    }
}
```

### BuildFallbackLinear

```csharp
public static GeneratedMap BuildFallbackLinear(int2 gridSize, int seed = 0, int generatorVersion = 0)
{
    // goal = (width-1, height/2), spawn = (0, height/2)
    // Walk = y == height/2 인 row 전체, 나머지는 Place
    int n = gridSize.x * gridSize.y;
    var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
    int midY = gridSize.y / 2;
    for (int y = 0; y < gridSize.y; y++)
    for (int x = 0; x < gridSize.x; x++)
        tiles[y * gridSize.x + x] = (y == midY) ? MapTileType.Walk : MapTileType.Place;

    var spawns = new NativeArray<int2>(1, Allocator.Persistent);
    spawns[0] = new int2(0, midY);

    return new GeneratedMap
    {
        tiles = tiles,
        gridSize = gridSize,
        spawns = spawns,
        goal = new int2(gridSize.x - 1, midY),
        seed = seed,
        generatorVersion = generatorVersion,
    };
}
```

### BattleBridge.BuildMapForBattle 에서 검증

```csharp
private void BuildMapForBattle()
{
    TeardownGeneratedMap();

    int seed = mapSettings != null ? mapSettings.EffectiveSeed : 0;
    int ver  = mapSettings != null ? mapSettings.generatorVersion : 0;
    _generatedMap = BattleMapBuilder.BuildFromFixture(map, seed, ver);

    if (!MapConnectivity.AllSpawnsReachGoal(_generatedMap))
    {
        Debug.LogError("[BattleBridge] Map fails spawn→goal connectivity. Falling back to linear map.");
        _generatedMap.Dispose();
        // H-5 fix: mapSettings null-safe fallback gridSize
        var fallbackSize = mapSettings != null
            ? new int2(mapSettings.gridWidth, mapSettings.gridHeight)
            : new int2(20, 20);
        _generatedMap = BattleMapBuilder.BuildFallbackLinear(fallbackSize, seed, ver);
    }

    // 주입 + FlowField build (task 4)
    ...
}
```

## Phase 10B 확장

Procedural 생성 시 Generate 실패 (max 3회 재시도 → 전부 실패) 시 동일 fallback 으로 분기. task 11/12 참조.

## 완료 기준

- 컴파일 0 errors.
- EditMode 테스트:
  - `AllSpawnsReachGoal(PrototypeMap fixture)` == true
  - 강제로 Walk 타일 모두 Deco 로 바꾼 map → false
  - `BuildFallbackLinear(20, 20)` 의 tiles[y*20+x] 가 y==10 일 때 Walk, 아니면 Place
  - Fallback map 은 `AllSpawnsReachGoal` == true
- PlayMode smoke: PrototypeMap 정상 진행 (fallback 경로 미진입).
- H-4: out-of-bounds spawn/goal 입력 → exception 없이 false 반환.
- H-5: mapSettings null → fallback gridSize=(20,20) 로 진행 (null deref 없음).
- M-1: `new NativeArray(...)` 중간 throw 시 이미 할당된 array 만 dispose (leak 없음).

## Subtask 분할 (OVERRUN 대응, 35분 예상)

- **9A** — `MapConnectivity.AllSpawnsReachGoal` + InBounds helper + NativeArray leak-safe
- **9B** — `BattleMapBuilder.BuildFallbackLinear` 직선 맵 생성
- **9C** — `BattleBridge.BuildMapForBattle` 내 연결성 검증 + null-safe fallback + EditMode 테스트
