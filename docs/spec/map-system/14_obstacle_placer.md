# ObstaclePlacer (Single Cell)

**작업 구분**: Phase 10B

## 목적

Walk 타일과 Place 타일을 침범하지 않는 셀에 theme obstacle prefab 을 단일 셀 단위로 배치. multi-cell footprint 는 Phase 11+ 이관. 사용자 bullet 5 "이동타일 기반 맵 테마 디자인 오브젝트 배치" 구현.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/ObstaclePlacer.cs`
- Modify: `Assets/_Project/Scripts/Core/MapView.cs` — `InstantiateObstacles(GeneratedMap, MapThemeData)` 추가
- 새 prefab: `Assets/_Project/Map/Theme/forest/{tree,rock,bush,...}.prefab` (3~4개)

## 구현 — 데이터 레이어

```csharp
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Data
{
    public static class ObstaclePlacer
    {
        // tiles 에서 Place 로 표시된 셀 중 일부를 Deco(배경 오브젝트) 로 전환.
        // minPlaceableRatio 이하로는 떨어지지 않음 (defender 배치 공간 확보).
        public static void Place(ref Random rng, NativeArray<MapTileType> tiles, int2 gridSize, MapThemeData theme)
        {
            if (theme == null || theme.obstaclePrefabs == null || theme.obstaclePrefabs.Length == 0) return;

            int n = gridSize.x * gridSize.y;

            // 현재 Place 셀 수집
            int placeCount = 0;
            for (int i = 0; i < n; i++)
                if (tiles[i] == MapTileType.Place) placeCount++;

            // 최소 유지 수
            int minPlace = Mathf.CeilToInt(placeCount * theme.minPlaceableRatio);

            // Deco 로 전환할 대상 수
            int convertCount = placeCount - minPlace;
            if (convertCount <= 0) return;

            // 인덱스 풀에서 랜덤 convertCount 개 선택
            var placeIndices = new NativeList<int>(placeCount, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++)
                    if (tiles[i] == MapTileType.Place) placeIndices.Add(i);

                // Fisher-Yates 부분 셔플
                for (int i = 0; i < convertCount; i++)
                {
                    int j = rng.NextInt(i, placeIndices.Length);
                    int tmp = placeIndices[i]; placeIndices[i] = placeIndices[j]; placeIndices[j] = tmp;
                    tiles[placeIndices[i]] = MapTileType.Deco;
                }
            }
            finally { placeIndices.Dispose(); }
        }
    }
}
```

## 구현 — 시각 레이어 (MapView)

```csharp
// MapView.cs 에 추가
public void InstantiateObstacles(GeneratedMap map, MapThemeData theme)
{
    if (theme == null || theme.obstaclePrefabs == null || theme.obstaclePrefabs.Length == 0) return;

    if (obstaclesRoot != null) Destroy(obstaclesRoot);
    obstaclesRoot = new GameObject("Obstacles");
    obstaclesRoot.transform.SetParent(transform, false);

    // 같은 seed 로 같은 prefab 배정을 위해 rng 재사용 또는 별도 해시 권장
    // v1: map.seed + cellIndex 해시로 prefab 선택
    for (int y = 0; y < map.gridSize.y; y++)
    for (int x = 0; x < map.gridSize.x; x++)
    {
        var cell = new int2(x, y);
        var type = map.TileAt(cell);
        if (type != MapTileType.Deco) continue;

        int hash = unchecked(map.seed * 73856093) ^ (x * 19349663) ^ (y * 83492791);
        int prefabIdx = math.abs(hash) % theme.obstaclePrefabs.Length;
        var prefab = theme.obstaclePrefabs[prefabIdx];

        var pos = new Vector3(x * _tileSize, 0f, y * _tileSize);
        Instantiate(prefab, pos, Quaternion.identity, obstaclesRoot.transform);
    }
}
```

## Env 타일 처리

Phase 10 에서 `MapTileType.Env` 는 시각 구분만. 일단 `ObstaclePlacer.Place` 에서는 Env 로 전환하지 않음 (Place → Deco 만). Env 는 Phase 11+ procedural 확장에서 별도 추가.

단 `MapView.BuildTiles` 는 Env 타일도 색으로 렌더링 (task 7 에서 구현됨).

## BattleBridge 통합

```csharp
// BuildMapForBattle 끝에 추가:
if (mapView != null) mapView.InstantiateObstacles(_generatedMap, mapTheme);
```

`mapTheme` 은 BattleBridge 의 SerializeField `MapThemeData` 참조.

## Forest theme prefab 3~4개

- `Assets/_Project/Map/Theme/forest/tree.prefab` — 나무 (단순 cylinder + sprite)
- `Assets/_Project/Map/Theme/forest/rock.prefab` — 바위
- `Assets/_Project/Map/Theme/forest/bush.prefab` — 덤불
- `Assets/_Project/Map/Theme/forest/flower.prefab` — 꽃 (optional)

각 prefab 은 1×1 타일 크기에 맞춰 scale 조정. Collider 없음.

## 완료 기준

- 컴파일 0 errors.
- EditMode 테스트: ObstaclePlacer 호출 후 Place 셀 수 >= `Mathf.CeilToInt(originalPlace * minPlaceableRatio)`.
- PlayMode smoke: procedural 맵 생성 → Deco 타일 위치에 prefab 실제로 Instantiate 됨.
- Walk/Place 셀 위 prefab 배치 0 확인 (Walk 경로 자유 이동, Place 에 defender 배치 가능).
