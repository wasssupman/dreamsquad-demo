# 3 — 바닥 페인팅 은퇴 · 오버레이 존치 · BoardSortOrder 수정

## 목적

`TilemapMapView` 에서 바닥/프랍 생성 경로를 걷어내고 **오버레이 7채널만 남긴다**. 비주얼 바닥은 unit 2 부터 스테이지 프리팹이 담당하고 있으므로, 이 unit 이후 타일맵은 순수 UI/연출 캔버스다. 겸사 `BoardSortOrder` 행 간격 버그(간격 10 < 맵 폭)를 수정한다 — 새 맵이 더 넓어질 수 있어 라이브 결함이 된다.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 슬림화 (필요 시 `OverlayView` 로 개명은 참조 폭을 보고 결정 — 개명 자체가 목적이 아니다)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` + 소비처 시그니처 확인
- 은퇴: `BoardVisualPlan*.cs`(4파일) · `BackgroundPropPlacer.cs` · `TilemapPropScatter.cs` · `PropInstanceUtil.cs`(구조물 경로가 unit 4 에서 이관된 뒤)
- `Assets/_Project/Scenes/BattleScene.unity` — `_TilemapBoard` 하위 정리 (propsTilemap 등)
- `TileSetData.cs` — 바닥 타일 필드(walk/place/env/deco/terrainTile/ring 계열) **사용 중단** (필드 삭제·SO 분리는 후속 후보 — orphan 키 정리 불가 이슈도 있어 서두르지 않는다)

## 구현

**존치 (오버레이 7채널 + 좌표 권위)**: `Grid`(BoardSpace 권위) · overlayTilemap(마커) · EffectTiles · PlacementRange · PlacementHighlight · AllyZone · LandingTelegraph + 비타일맵 자식(PlacementCommitPop · PlacementLiquidTile · AimArrow). `Initialize` 는 격자 구성·오버레이 준비만 남긴다.

**은퇴**: `PaintGround` · `PaintSurroundRing` · `BoardVisualPlanBuilder` 계열 · `BackgroundPropPlacer`+`InstantiateBackgroundProps`/`InstantiateRingProps` · `TilemapPropScatter`(유일한 `GetTile` 역참조 — 함께 소멸). 골/스폰 구조물 프랍 경로(`InstantiateStructureProps`·`_goalPropsByCell`·앵커)는 **unit 4 로 이관 후 삭제** — 이 unit 에서 먼저 지우면 골 연출이 공백이 된다.

**`BoardSortOrder.Compute` 수정**: 행 간격을 상수 10 에서 `max(10, gridSize.x + 여유)` 로 — 시그니처는 이미 gridSize 를 받으므로 소비처 무변경. EditMode 테스트로 "far-row 유닛이 near-row 유닛보다 항상 낮은 order" 를 폭 30 격자에서 고정.

**순서 주의**: 이 unit 은 unit 2 뒤에만 안전하다(문서 경로가 살아있는 동안 바닥 페인팅을 지우면 기존 맵이 빈 화면).

## 완료 기준

- [ ] compile + 오버레이 채널 전부 에디터 Play 동작: 배치 하이라이트/사거리 프리뷰/호버 스냅/아군 장판 페인트/착지 텔레그래프/효과 타일 마커
- [ ] 바닥 타일이 더 이상 페인트되지 않고 스테이지 프리팹 바닥만 보인다
- [ ] `BoardSortOrder` EditMode 테스트 그린 (폭 30 격자 near/far 정렬)
- [ ] `Tilemap.GetTile` 로 게임 상태를 읽는 코드 0건 (grep 확인 — tilemap-view-backend 계약 재확인)
