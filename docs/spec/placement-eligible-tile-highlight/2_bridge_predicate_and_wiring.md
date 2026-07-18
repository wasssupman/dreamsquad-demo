# 2 — 공유 술어 + bridge 포워딩 + 컨트롤러 배선

## 목적

`CanPlaceDefenderAt` 의 공간 게이트를 공유 술어로 추출해 하이라이트·판정이 어긋나지 않게 하고,
bridge 게이트웨이(Show/Hide/Refresh)와 컨트롤러 파생상태 토글을 배선한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- Test: `Assets/_Project/Tests/EditMode/SpatialPlacementCheckTests.cs` (신규)

## 구현

### (a) 공유 술어 추출 (BattleBridge)

`CanPlaceDefenderAt` 의 공간 게이트(IsCreated + bounds + `TileAt==Place` + `!_occupiedTiles`,
현 3474~3495) 를 reason 을 돌려주는 private 헬퍼로 뺀다:

```csharp
// 공간 조건만. 비용/풀/유닛/running 은 CanPlaceDefenderAt 이 별도로 본다.
private PlacementRejectReason SpatialPlacementCheck(int2 cell)
{
    if (!_generatedMap.IsCreated) return PlacementRejectReason.MissingMap;
    if (cell.x < 0 || cell.x >= _generatedMap.gridSize.x ||
        cell.y < 0 || cell.y >= _generatedMap.gridSize.y) return PlacementRejectReason.OutOfBounds;
    if (_generatedMap.TileAt(cell) != MapTileType.Place) return PlacementRejectReason.NotBuildable;
    if (_occupiedTiles.Contains(new Vector2Int(cell.x, cell.y))) return PlacementRejectReason.Occupied;
    return PlacementRejectReason.None;
}
```

`CanPlaceDefenderAt` 은 이 헬퍼를 먼저 호출해 `!= None` 이면 그 reason 으로 early-return, 그 뒤 기존
비용/풀/유닛 체크. **동작·reason 불변**(순수 리팩터).

### (b) 배치 가능 셀 수집 + Show/Hide/Refresh (BattleBridge)

```csharp
private bool _placeableShown;
private readonly List<Vector2Int> _placeableScratch = new(); // 재사용 스크래치

public void ShowPlacementHighlight() { _placeableShown = true;  RepaintPlaceable(); }
public void HidePlacementHighlight() { _placeableShown = false; if (tilemapMapView != null) tilemapMapView.ClearPlacementHighlight(); }
public void RefreshPlacementHighlightIfShown() { if (_placeableShown) RepaintPlaceable(); }

private void RepaintPlaceable()
{
    if (!_placeableShown || tilemapMapView == null || !_generatedMap.IsCreated) return;
    _placeableScratch.Clear();
    int w = _generatedMap.gridSize.x, h = _generatedMap.gridSize.y;
    for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
        if (SpatialPlacementCheck(new int2(x, y)) == PlacementRejectReason.None)
            _placeableScratch.Add(new Vector2Int(x, y)); // 공간상 배치 가능 = 밝힘 대상
    tilemapMapView.SetPlacementHighlight(_placeableScratch);
}
```

- 드래그 상승 배선: `ShowPlacementHighlight` 직후(또는 기존 range 상승과 같은 지점) `tilemapMapView.
  SetPlacementHighlightAboveUnits(true)` 가 이미 호출되면 하이라이트도 함께 상승(unit 1 에서 두 타일맵 동시 처리).
- **변경 구동 리프레시**: `_occupiedTiles` 변이 지점 전부에서 `RefreshPlacementHighlightIfShown()` —
  수비 사망 해제(현 ~2340), pending 점유 Add ×2(현 ~3531/3558), Clear(현 ~1046).

### (c) 파생 상태 토글 (DefenderDragPlacementController)

지점마다 산탄 금지. 원하는 상태를 파생해 idempotent 호출. 이미 매 프레임 도는 `Update()` 말미 또는 전이:

```csharp
bool desired = (_session.active && !_simulatedDrag) || _armedUnit != null;
if (desired != _maskDesiredPrev)   // 필드명은 _highlightDesiredPrev 등 자유
{
    _maskDesiredPrev = desired;
    if (desired) bridge.ShowPlacementHighlight(); else bridge.HidePlacementHighlight();
}
```

- 탭 시뮬 비행 중 `_simulatedDrag==true` → 자동 OFF(range 억제와 일관).
- BeginDrag 의 Disarm→재Show 순서의존·`_sessionGen` 하이재킹 무관(상태는 파생값이 결정).
- `OnDisable`/`OnDestroy` 에서 `HidePlacementHighlight` 안전 호출로 수렴.

## 완료 기준

- 컴파일 0 errors. `CanPlaceDefenderAt` 회귀 없음(기존 배치/리젝 동작·reason 동일).
- EditMode: `SpatialPlacementCheck` — bounds/비-Place/점유 각각 정확한 reason, 빈 Place 셀 None.
  구성한 `GeneratedMap` + 점유 set 으로 검증.
- Play: 드래그 시작 → 배치 가능 칸이 밝아짐(정적, 페이드인). 노란 사거리 공존. 탭 arm → 동일 하이라이트.
  탭 후 비행 중 OFF. 드롭/취소/커밋 시 확실히 소거.
- 슬로우모 중 수비 사망 → 그 칸이 즉시 밝아짐(리프레시).
- 비용 부족 유닛 arm 시: 칸은 밝은데 hover 는 invalid(빨강) — 하이라이트가 비용 안 봄(계약).

- [x] 2026-07-18 · 컴파일 0 err/0 warn · EditMode `SpatialPlacementCheckTests` 5/5 pass (MCP) · (이 커밋).
  `SpatialPlacementCheck` 는 순수 static(값 in→reason out)으로 확정 — CellClassifier 선례. 시각 e2e 는 스프라이트 후.
