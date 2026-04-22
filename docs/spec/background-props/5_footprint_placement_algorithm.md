# Footprint Placement Algorithm

**작업 구분**: 5 / Placement

## 목적

맵 생성 후 배경 타일 영역을 순회하면서, 연결된 가용 영역을 찾고 그 영역 중앙에 들어갈 수 있는 X * Y footprint 프랍을 필터한 뒤 룰에 따라 선택해 배치한다. v1 룰은 seeded random 이다.

## 입력

```csharp
GeneratedMap map;
MapThemeData theme;        // tileProps / decorProps 포함
uint seed;
float tileSize;
```

필요한 theme 필드:

```csharp
public PropData[] tileProps;
public PropData[] decorProps;
public float tilePropDensity;      // 0~1, 후보 셀 중 배치 시도 비율
public int maxTilePropCount;       // 0 이하면 제한 없음
```

`tilePropDensity` 와 `maxTilePropCount` 는 초기값을 보수적으로 둔다. 예: density 0.25, max 0.

## 배경 타일 영역 정의

초기 허용 타일:

- `MapTileType.Deco`
- `MapTileType.Env`

선택 사항:

- `Place` 타일 위에도 배치하려면 `PropData.blocksPlacement` 같은 명시 필드가 필요하다. 초기 구현에서는 Place 소모를 하지 않는다.
- `Walk` 타일은 항상 금지한다.

## Placement Data

runtime 최소 record:

```csharp
public struct PropPlacement
{
    public int propIndex;
    public int x;
    public int y;
    public int width;
    public int height;
    public uint variantSeed;
}
```

`propIndex` 는 `theme.tileProps[propIndex]` 를 가리킨다.

## Occupancy

배치 중 별도 occupancy grid 를 둔다.

```text
occupied[x, y] = 이미 Tile Prop 이 점유한 셀
```

검증 조건:

- footprint 가 맵 bounds 안에 있어야 한다.
- footprint 내부 모든 셀이 허용 배경 타일이어야 한다.
- footprint 내부 모든 셀이 unoccupied 여야 한다.
- `PropData.prefab` 이 null 이면 후보에서 제외한다.

## 순회 순서

v1 은 deterministic scan 으로 가용 영역의 시작점을 찾는다.

```text
for y = 0..height-1
  for x = 0..width-1
    if cell is unvisited background
      FloodFillRegion(cell)
```

각 셀을 직접 배치 후보로 보지 않는다. 먼저 `Deco/Env` 로 연결된 가용 영역을 flood fill 로 찾고, 그 영역의 bounds 중앙에 가장 가깝게 들어가는 lower-left 좌표를 계산한다. 하나를 배치한 뒤 occupied 로 마킹하고, 남은 가용 영역을 다시 flood fill 한다. 이 과정을 더 이상 배치할 수 없거나 `maxTilePropCount` 에 도달할 때까지 반복한다.

랜덤성을 위해 영역 순서를 shuffle 하는 방법도 가능하지만, v1 에서는 재현성과 디버깅을 위해 scan 순서를 고정하고 선택만 seeded random 으로 처리한다.

## 후보 필터

각 가용 영역에서:

1. flood fill 로 연결된 `Deco/Env` 영역 bounds 를 구한다.
2. `rng.NextFloat() > tilePropDensity` 이면 해당 영역의 배치 시도를 건너뛴다.
3. 각 `theme.tileProps` 에 대해 영역 bounds 안에서 `CanFit(prop, x, y)` 를 통과하는 좌표를 찾는다.
4. 같은 프랍의 여러 좌표 중 영역 중심에 가장 가까운 좌표만 후보로 남긴다.
5. 후보 프랍 중 하나를 seeded random 으로 선택한다.
6. 선택된 footprint 영역을 occupied 로 마킹한다.
7. `PropPlacement` 를 추가한다.
8. 전체 scan pass 를 끝낸 뒤, 하나라도 배치했다면 visited 를 초기화하고 남은 가용 영역을 다시 찾는다.
9. 더 이상 배치가 없거나 `maxTilePropCount` 에 도달하면 종료한다.

중앙 lower-left 계산은 다음 개념을 따른다.

```csharp
regionCenter = ((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
propCenter = (x + (width - 1) * 0.5f, y + (height - 1) * 0.5f);
score = distanceSquared(propCenter, regionCenter);
```

score 가 가장 작은 좌표가 해당 프랍의 centered fit 이다.

기존 셀 단위 절차는 더 이상 사용하지 않는다.

```text
Deprecated:
1. 현재 셀이 허용 배경 타일인지 확인한다.
2. `rng.NextFloat() > tilePropDensity` 이면 배치 시도를 건너뛴다.
3. `theme.tileProps` 중 `CanFit(prop, x, y)` 를 통과하는 프랍을 모은다.
4. 후보가 없으면 다음 셀로 이동한다.
5. 후보 중 하나를 seeded random 으로 선택한다.
6. footprint 영역을 occupied 로 마킹한다.
7. `PropPlacement` 를 추가한다.
8. `maxTilePropCount` 에 도달하면 종료한다.
```

## CanFit

```csharp
bool CanFit(PropData prop, int x, int y, GeneratedMap map, NativeArray<bool> occupied)
{
    int width = prop.footprintX;
    int height = prop.footprintY;

    if (x < 0 || y < 0) return false;
    if (x + width > map.gridSize.x) return false;
    if (y + height > map.gridSize.y) return false;
    if (prop.prefab == null) return false;

    for (int dy = 0; dy < height; dy++)
    for (int dx = 0; dx < width; dx++)
    {
        int cx = x + dx;
        int cy = y + dy;
        if (occupied[Index(cx, cy)]) return false;

        var tile = map.TileAt(cx, cy);
        if (tile != MapTileType.Deco && tile != MapTileType.Env)
            return false;
    }

    return true;
}
```

## 선택 룰

v1:

```csharp
var selected = candidates[rng.NextInt(0, candidates.Count)];
```

v1.1 후보:

- `PropData.weight` 기반 weighted random.
- 큰 footprint 우선 배치.
- 셀별 noise score 로 자연스러운 군집 배치.
- 동일 프랍 연속 배치 방지.

## World Position

footprint 좌하단 기준 record 를 prefab root position 으로 변환한다.

```csharp
float centerX = x + (width - 1) * 0.5f;
float centerY = y + (height - 1) * 0.5f;
Vector3 world = new Vector3(centerX * tileSize, 0f, centerY * tileSize);
```

root 는 footprint 중심에 놓인다. `PropData.visualOffset` 으로 시각 위치를 보정한다.

## Determinism

- UnityEngine.Random 금지.
- `Unity.Mathematics.Random` 또는 고정 hash 기반 RNG 사용.
- 동일 seed, map, theme prop 배열 순서이면 동일 결과.
- prop 배열 순서 변경은 결과 변경으로 본다.

## 완료 기준

- 1x1, 2x1, 1x2, 2x2 프랍 후보가 footprint bounds 안에서만 배치된다.
- Walk/Place 타일 위 배치 0.
- occupancy 중복 0.
- 같은 seed 결과 동일.
- 다른 seed 에서 배치 선택이 달라질 수 있음.
- `tilePropDensity=0` 이면 placement 0.
- `maxTilePropCount=1` 이면 최대 1개만 배치.
