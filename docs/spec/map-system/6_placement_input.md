# PlacementInput Place-Only

**작업 구분**: Phase 10A

## 목적

`PlacementInput` 이 defender 배치 판정을 `MapTileType.Place` 로 전환. Phase 9 의 `BattleBridge.CanPlaceDefenderAt` → `TileType.Buildable` 참조를 GeneratedMap 기반으로 교체.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Core/PlacementInput.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`CanPlaceDefenderAt` 및 placement 체크 위치)

## 구현

### PlacementInput.Initialize 교체

기존:
```csharp
public void Initialize(float tileSize)
{
    _tileSize = tileSize;
}
```

신규:
```csharp
private GeneratedMap _map;

public void Initialize(GeneratedMap map, float tileSize)
{
    _map = map;
    _tileSize = tileSize;
}
```

### 배치 판정 로직 (BattleBridge 측)

기존:
```csharp
public bool CanPlaceDefenderAt(Vector2Int cell)
{
    if (map == null) return false;
    if (cell.x < 0 || cell.x >= MapData.Width) return false;
    if (cell.y < 0 || cell.y >= MapData.Height) return false;
    return map.GetTile(cell.x, cell.y) == TileType.Buildable
        && !_placedCells.Contains(cell);
}
```

신규:
```csharp
public bool CanPlaceDefenderAt(Vector2Int cell)
{
    if (!_generatedMap.IsCreated) return false;
    var gs = _generatedMap.gridSize;
    if (cell.x < 0 || cell.x >= gs.x) return false;
    if (cell.y < 0 || cell.y >= gs.y) return false;
    var tile = _generatedMap.TileAt(new int2(cell.x, cell.y));
    return tile == MapTileType.Place
        && !_placedCells.Contains(cell);
}
```

### PlacementInput 내 raycast 처리

PlacementInput 이 mouse/touch raycast 결과를 cell 좌표로 변환 → BattleBridge 로 요청 위임. 이 경로는 기존 흐름 유지. 단 `MapData.Width/Height` 참조가 있으면 `_map.gridSize` 로 교체.

## Flash 타일 미리보기 (기존 기능 유지)

PlacementInput 이 hover cell 의 valid 여부에 따라 MapView.FlashTile* 호출하는 흐름은 변경 없음. `MapView` 가 4-tile Material 기반으로 재작성되더라도 flash API 시그니처 유지.

## 완료 기준

- 컴파일 0 errors.
- PlayMode smoke: Place 타일 위에 defender 배치 성공 / Walk/Env/Deco 타일 위 배치 실패 + flash reject 표시.
- EditMode 테스트: `CanPlaceDefenderAt` 가 Place 만 true, 나머지 3 타입 false 반환 (PrototypeMap fixture 기반).
- 기존 배치 중복 방지 (`_placedCells.Contains`) 회귀 없음.
