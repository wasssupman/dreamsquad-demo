# 1 — 배치 **불가** 영역 하이라이트

## 목적

맵 설명(B1)이 "여기는 놓을 수 있다 / 여기는 못 놓는다" 를 번갈아 보여준다. 지금은
**놓을 수 있는 칸만** 칠하는 경로 하나뿐이다.

Duel 기준 캐논의 배치 가능 134칸 / 불가 118칸이라 대비가 화면 절반씩으로 선다.

## 변경 대상

- `Assets/_Project/Scripts/Data/TileSetData.cs` (`blockedTile` · `blockedColor`)
- `Assets/_Project/Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset` (**라이브** — BattleScene 이 쓴다)
- `Assets/_Project/Data/TileSets/TileSet_Desert.asset`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

**뷰**: `SetPlacementHighlight` / `ClearPlacementHighlight` 의 형제로
`SetBlockedHighlight(IReadOnlyList<Vector2Int>)` / `ClearBlockedHighlight()` 를 만든다.
전용 타일맵을 하나 더 깔고(`_blockedTilemap`) 기존 관용구를 그대로 따른다 —
`EnsurePlaceableTilemap` 형태의 지연 생성, 첫 프레임 알파 0 에서 페이드인, 균일 tint.

⚠ **`SetPlacementHighlightAboveUnits` 의 목록에 `_blockedTilemap` 도 넣는다.**
"같은 층에 두면 쌍을 하나 더 관리할 필요가 없다"는 오독이다 — 쌍 관리가 필요한 이유는
층이 달라서가 아니라 **타일맵이 하나 더 존재하기 때문**이다. `TilemapMapView` 의
ultimate-leap unit 4 주석이 이 누락의 결과를 이미 기록해뒀다.

⚠ **`blockedTile` 은 `TileBase` 참조라 코드 기본값을 못 준다.** null 이면
`SetBlockedHighlight` 가 조용히 early-return 한다(기존 `SetPlacementHighlight` 와 같은 형태).
**타일셋 에셋 2개를 손으로 채우는 것이 이 unit 의 일부다.**

**브리지**: `ShowBlockedHighlight(DefenderUnitData unit)` — `RepaintPlacementHighlight`
와 **같은 스캔**을 돌려 `SpatialPlacementCheck != None` 인 칸을 모은다. 여집합을 따로
계산하지 않는다(두 벌의 판정식이 갈리면 어느 날 두 하이라이트가 같은 칸을 동시에
칠한다). 기존 루프가 이미 전 격자를 돌며 `== None` 만 담고 있으므로 `else` 한 줄이다.

두 가지 주의:
- `RepaintPlacementHighlight` 는 `_placeableHlShown` 으로 early-return 한다 →
  **blocked 전용 플래그**가 따로 필요하다.
- `_placeableHlExtraCell`(재배치 소스 칸)은 스캔이 `Occupied` 로 뺀 칸을 사후에 되넣는다 →
  두 하이라이트를 동시에 켜면 그 한 칸이 양쪽에 든다. 브리핑 전용이라 실무상 무해하지만
  동시 표시를 하려면 알고 있어야 한다.

칠하는 범위는 **가능 칸의 여집합 전체**다(사용자 결정) — 벽·장애물·점유 칸 전부.

이 하이라이트는 **맵 설명 전용**이다. 배치 중에는 켜지 않는다 — 드래그하는 동안
화면 절반이 빨개지면 정작 놓을 곳이 안 보인다. (드래그 컨트롤러의 하이라이트 자기치유는
켜는 방향만이라 외부에서 켠 것을 매 프레임 끄지는 않는다 — 확인됨.)

## 완료 기준

- compile 통과.
- 튜토리얼 B1 에서 두 하이라이트가 번갈아 뜨고, 같은 칸이 동시에 두 색으로 칠해지지 않는다.
- `ClearBlockedHighlight` 후 잔상 타일 0.
- 일반 배치(드래그)에서는 불가 하이라이트가 뜨지 않는다.
- 색/타일을 `TileSetData` 에서 바꾸면 화면에 반영된다(코드 상수 없음).
- 타일셋 에셋 2개 모두 `blockedTile` 이 채워져 있다(하나라도 비면 그 테마에서 조용히 안 뜬다).
