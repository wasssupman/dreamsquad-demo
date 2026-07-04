# 2 — BattleBridge 포워딩 + DragController 배선

## 목적

드래그 중 hover 셀 기준으로 범위 하이라이트를 갱신/정리한다.
BattleBridge 가 `attackRange → tileRange` 변환 후 뷰에 위임(뷰는 int 만 받음).

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`

## 구현

**BattleBridge** (`SetPlacementHover` 인접에 추가):

```csharp
public void SetPlacementRange(Vector2Int center, DefenderUnitData unit)
{
    if (tilemapMapView == null || unit == null) return;
    int tileRange = GridMath.RangeToTiles(unit.attackRange);
    tilemapMapView.SetPlacementRange(center, tileRange);
}

public void ClearPlacementRange()
{
    if (tilemapMapView != null) tilemapMapView.ClearPlacementRange();
}
```

**DragController**:

- `SetHover(cell, valid)` — 셀 변경을 assignment **이전에** 캡처한 뒤 range 갱신(F8):

```csharp
bool changed = !_session.hoverTile.HasValue || _session.hoverTile.Value != cell;
// (기존: 셀 바뀌면 old-hover clear)
_session.hoverTile = cell;
_session.isValidTile = valid;
// (기존: preview 활성화)
bridge?.SetPlacementHover(cell, valid);
if (changed) bridge?.SetPlacementRange(cell, _session.unit);
```

  범위는 `valid` 에 의존하지 않으므로(계약: 항상 표시), 유효성만 바뀔 땐 재그리지 않는다.
  같은 셀 반복 호출 방지로 flicker/불필요 repaint 도 회피.

- `ClearHover()` 와 `CleanupSession()` 에 `bridge?.ClearPlacementRange();` 추가.

## 완료 기준

- compile.
- Play(에디터 **포커스**): 카드 드래그 시작 → 맵 이동 시 중심 초록/빨강 + 주변 노란 펄스 범위가 따라다님.
  범위 크기 = 유닛 `attackRange` 반영(예: range 3 → 중심 둘레 3칸).
  drop / 취소 시 범위 사라짐. 빨강(배치 불가) 타일 위에서도 노란 범위 유지.
- 스크린샷으로 노란 펄스 육안 확인(메모리: 시각 변경 스크린샷 검증).

완료 확인 2026-07-04 (`b3cd345`) — 코드리뷰·compile 통과, 렌더/펄스/격자 경로 검증(unit1). **e2e 드래그 확정**: spec2 Play 에서 `BeginDrag/UpdateDrag` 구동 시 `_rangeCells=44`(엣지 clip) 로 범위가 실제 드래그 hover 를 따라 페인트됨(스크린샷 `drag_billboard.png`).
