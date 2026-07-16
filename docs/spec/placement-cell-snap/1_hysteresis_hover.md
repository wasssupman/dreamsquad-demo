# 1 — 히스테리시스 hover 배선 (A)

**작업 구분**: feature (A) · 의존: unit 0

## 목적

unit 0 의 `PlacementCellSnap.Resolve` 를 실제 hover 결정에 배선해 경계 플리커를 제거한다.
논리 셀에만 적용하고, 뷰/카메라 스무딩은 건드리지 않는다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (frac-cell read 헬퍼)
- Modify: `Assets/_Project/Scripts/Data/DragSwaySettings.cs` (`placementStickMargin`)

## 구현

- **bridge frac 헬퍼**(read only, `DebugWorldToCell` 근처 co-locate):
  `public Vector2 DebugWorldToCellFractional(Vector3 sim)` →
  `((sim.x - _boardOrigin.x)/tileSize, (sim.z - _boardOrigin.z)/tileSize)`.
  `DebugWorldToCell = round(frac)` 과 동일 공간(드리프트 방지). tileSize/gridSize 는 기존 접근 재사용.
- **컨트롤러**:
  - 필드 `private Vector2Int? _focusedCell;` 추가.
  - `UpdateHoverAtTarget()` 에서: `sim = ToSim(_unitTargetWorld)` → `frac = bridge.DebugWorldToCellFractional(sim)`
    → `cell = PlacementCellSnap.Resolve(_focusedCell, frac, Cfg.placementStickMargin, gridSize)` → `_focusedCell = cell`.
    이후 기존대로 `CanPlaceDefenderAt(cell)` → `SetHover`. (bridge null 폴백은 기존 `FloorToInt(sim+0.5)` 유지.)
  - 리셋: `ClearHover()` / 오프보드(`_onBoard=false`) / 세션 시작(`BeginSession` 계열)에서 `_focusedCell = null`.
    off-board 후 재진입 시 첫 셀은 round(frac) 이어야 함.
- **SO**: `DragSwaySettings.placementStickMargin` (기본 0.18f, 툴팁="타일 경계 sticky 여유(타일 분수). ↑=덜 민감/더 끈끈"). `Cfg` 접근자로 노출.
- gridSize 는 bridge 에서 얻는다(기존 hover 경로가 이미 map 을 참조). 접근자 없으면 최소 read 헬퍼 추가.

## 완료 기준

- 컴파일 통과(`read_console` clean), EditMode 회귀 없음.
- Play(에디터 마우스 + 가능하면 실기기): 경계 근처에서 포인터를 미세하게 떨어도 하이라이트 타일이 튀지 않음. 확실히 이동하면 이웃으로 넘어감(지연 체감 과하지 않음 — margin 튜닝).
- 오프보드 후 재진입 시 하이라이트가 즉시 올바른 셀로 잡힘(stale sticky 없음).
- 커밋(`EndDrag`)은 sticky `hoverTile` 그대로 배치 — 하이라이트와 실제 배치 셀 일치.
- 사용자 Play 확인 일자 + 커밋 해시 추가 후 커밋.
