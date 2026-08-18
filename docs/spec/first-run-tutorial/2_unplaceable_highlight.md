# 2 — 배치 **불가** 영역 하이라이트

## 목적

맵 설명(B1)이 "여기는 놓을 수 있다 / 여기는 못 놓는다" 를 번갈아 보여준다. 지금은
**놓을 수 있는 칸만** 칠하는 경로 하나뿐이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/TileSetData.cs` (`blockedTile` · `blockedColor`)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

**뷰**: `SetPlacementHighlight` / `ClearPlacementHighlight` 의 형제로
`SetBlockedHighlight(IReadOnlyList<Vector2Int>)` / `ClearBlockedHighlight()` 를 만든다.
전용 타일맵을 하나 더 깔고(`_blockedTilemap`) 기존 것과 같은 관용구를 따른다 —
`EnsurePlaceableTilemap` 와 같은 형태의 지연 생성, 첫 프레임 알파 0 에서 페이드인,
균일 tint(per-cell 색 없음). 색·타일은 `TileSetData` 가 소유한다(하드코딩 금지).

sorting 은 배치 가능 하이라이트와 **같은 층**에 둔다. 둘은 정의상 서로 겹치지 않는
집합이라 다툴 일이 없고, 층을 나누면 `SetPlacementHighlightAboveUnits` 의 쌍을
하나 더 관리해야 한다.

**브리지**: `ShowBlockedHighlight(DefenderUnitData unit)` — `RepaintPlacementHighlight`
와 **같은 스캔**을 돌려 `SpatialPlacementCheck != None` 인 칸을 모은다. 여집합을 따로
계산하지 않는다(두 벌의 판정식이 갈리면 어느 날 두 하이라이트가 같은 칸을 동시에
칠한다). 스캔 1회에서 두 리스트를 나눠 담는 형태가 맞다.

칠하는 범위는 **가능 칸의 여집합 전체**다(사용자 결정) — 경로·벽·장애물·점유 칸 전부.

이 하이라이트는 **맵 설명 전용**이다. 배치 중에는 켜지 않는다 — 드래그하는 동안
화면 대부분이 빨개지면 정작 놓을 곳이 안 보인다.

## 완료 기준

- compile 통과.
- 튜토리얼 B1 에서 두 하이라이트가 번갈아 뜨고, 같은 칸이 동시에 두 색으로 칠해지지 않는다.
- `ClearBlockedHighlight` 후 잔상 타일 0.
- 일반 배치(드래그)에서는 불가 하이라이트가 뜨지 않는다.
- 색/타일을 `TileSetData` 에서 바꾸면 화면에 반영된다(코드 상수 없음).
