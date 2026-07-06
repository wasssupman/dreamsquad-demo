# 6 — 드래그 중 배치 하이라이트를 적 위로

**작업 구분**: wiring (소팅 토글)

## 목적

드래그 배치 중 타일 하이라이트(사거리 그리드 + hover 중심셀)를 적 유닛 위로 올려, 적에 가려지지 않고
어느 타일인지 확실히 보이게 한다. **드래그 중에만** 적용하고 종료 시 원복한다.

## 배경 (현재 소팅)

- `overlayTilemap`(hover/reject/goal/spawn) = **-10**, `_rangeTilemap`(사거리 그리드) = **-12** (바닥 데칼, 음수).
- 적/디펜더 = 빌보드 양수 sortingOrder(`BoardSortOrder.Compute` 수백). → 평상시 하이라이트는 유닛 아래.
- "보드 레이어(음수) < 유닛 레이어(양수)" 는 TilemapMapView 의 기본 1규칙 — **드래그 밖에선 불변**.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `SetPlacementHighlightAboveUnits(bool)` 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 포워딩 메서드
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — BeginDrag(on)/CleanupSession(off)

## 구현

- `TilemapMapView.SetPlacementHighlightAboveUnits(bool above)`:
  `above` 면 overlay=**10002**, range=**10000** (유닛·투사체 위, 힛바 16000·프리뷰 20000 아래) — hover 가 range 위 유지.
  아니면 overlay=**-10**, range=**-12** 기본값 복원. (이 파일 관례대로 리터럴 소팅값 사용 — Core 는 Presentation.BoardSortOrder 미참조.)
- **sticky 상태**: `_rangeTilemap` 은 첫 `SetPlacementRange`(드래그 중) 때 lazy 생성되므로, 상승 상태를
  `_highlightAbove` 필드로 보관하고 `EnsureRangeTilemap` 생성 시 반영 → 첫 드래그도 range 그리드가 상승.
- BattleBridge 는 `tilemapMapView?.SetPlacementHighlightAboveUnits(...)` 포워딩(게이트웨이 규칙).
- DragController 는 dim 토글과 **같은 지점**에서 on/off — CleanupSession 단일 funnel 로 모든 종료 원복.

## 완료 기준 (Play)

- 드래그 중 사거리 그리드·hover 셀이 (반투명해진) 적 **위로** 선명히 보인다.
- 드롭·거부·취소 후 하이라이트 소팅이 기본값(-10/-12)으로 복원 — 평상시 적이 다시 하이라이트 위.
- 주의: 바닥 그리드가 적 몸통 위에 겹쳐 그려질 수 있음(적이 0.15 라 자연스러움). 어색하면 이 unit 만 원복.
