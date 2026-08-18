# 3 — 바닥 페인팅 은퇴 · 오버레이 존치 · BoardSortOrder 수정

## 목적

`TilemapMapView` 에서 바닥/프랍 생성 경로를 걷어내고 **오버레이 7채널만 남긴다**. 비주얼 바닥은 unit 2 부터 스테이지 프리팹이 담당하고 있으므로, 이 unit 이후 타일맵은 순수 UI/연출 캔버스다. 겸사 `BoardSortOrder` 행 간격 버그(간격 10 < 맵 폭)를 수정한다 — 새 맵이 더 넓어질 수 있어 라이브 결함이 된다.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 슬림화. **개명 주의 (critic M-5)**: reflection 문자열 결합 3개소가 있어 grep-by-type 으로 안전 판정 불가 — `TilemapMapViewTests.SetField`(L38-58 `grid`/`groundTilemap`/`overlayTilemap`) · `ActiveAllyZoneTest.cs:373-383`(`tilemapMapView`/`_zoneCellRefs` 문자열 접근). 개명하면 같은 커밋에서 3개소 갱신
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` + 소비처 시그니처 확인
- 은퇴 (critic M-7 정정 — 이 unit 은 **바닥 페인팅·산포 계열만**): `PaintGround`/`PaintSurroundRing` · `BackgroundPropPlacer.cs` · `TilemapPropScatter.cs`. **`BoardVisualPlan` 계열 7파일은 unit 4 로 이동** (튜토리얼 `VisualPlan` 의존 L161-190 이 unit 4 소관 — critic M-6). `BoardDecorAnchorType` 은 `PropData.preferredAnchorTypes` 로 forest 36 + desert 14 = 50개 에셋에 직렬화 — **삭제하지 않는다**(orphan YAML 키 정리 불가)
- `Assets/_Project/Scenes/BattleScene.unity` — `_TilemapBoard` 하위 정리 (propsTilemap 등)
- `TileSetData.cs` — 바닥 타일 필드(walk/place/env/deco/terrainTile/ring 계열) **사용 중단** (필드 삭제·SO 분리는 후속 후보 — orphan 키 정리 불가 이슈도 있어 서두르지 않는다)
- `Assets/_Project/Tests/EditMode/TilemapMapViewTests.cs` — **`PaintPositions_MatchBoardSpace_*` 는 삭제가 아니라 스테이지 기준 재작성** (critic M-5): 스테이지 프랍 월드 위치 ↔ `BoardSpace.ToView(셀)` 일치 단언. 계약 7(BoardSpace)·C-1(정렬)의 유일한 자동 회귀망이다

## 구현

**존치 (오버레이 7채널 + 좌표 권위)**: `Grid`(BoardSpace 권위) · overlayTilemap(마커) · EffectTiles · PlacementRange · PlacementHighlight · AllyZone · LandingTelegraph + 비타일맵 자식(PlacementCommitPop · PlacementLiquidTile · AimArrow). `Initialize` 는 격자 구성·오버레이 준비만 남긴다.

**은퇴**: `PaintGround` · `PaintSurroundRing` · `BackgroundPropPlacer` · `TilemapPropScatter`(유일한 `GetTile` 역참조 — 함께 소멸). 프랍 인스턴스화 3종 호출은 unit 2 가 이미 끊었다(M-8). `BoardVisualPlan` 계열과 골/스폰 구조물 프랍 경로(`InstantiateStructureProps`·`_goalPropsByCell`·앵커)는 **unit 4 로 이관 후 삭제** — 이 unit 에서 먼저 지우면 골 연출·튜토리얼이 공백/컴파일 실패가 된다.

**`BoardSortOrder.Compute` 수정**: 행 간격을 상수 10 에서 `max(10, gridSize.x + 여유)` 로 — 시그니처는 이미 gridSize 를 받으므로 소비처 무변경. **대역 충돌 주의 (critic M-9)**: 같은 파일의 sorting band(`ProjectileOffset 1000` / `PlacementLiquid 11000` / …)가 «Compute 최대 ≈ 수백대»를 전제한다 — 스트라이드 확대 시 `gridSize.y × (gridSize.x+여유)` 가 1000 을 넘으면 투사체가 유닛 뒤로 깔린다(폭 32 부터 발생). Compute 최대치가 상한을 넘으면 대역 재배치도 이 unit 범위다.

**순서 주의**: 이 unit 은 unit 2 뒤에만 안전하다(문서 경로가 살아있는 동안 바닥 페인팅을 지우면 기존 맵이 빈 화면).

## 완료 기준

- [ ] compile + EditMode 두 lane 무회귀 + 오버레이 채널 전부 에디터 Play 동작: 배치 하이라이트/사거리 프리뷰/호버 스냅/아군 장판 페인트/착지 텔레그래프/효과 타일 마커
- [ ] 바닥 타일이 더 이상 페인트되지 않고 스테이지 프리팹 바닥만 보인다
- [ ] `PaintPositions_MatchBoardSpace_*` 스테이지 기준 재작성 그린 (C-1 회귀망)
- [ ] `BoardSortOrder` EditMode 테스트 그린 — **폭 48 격자** near/far 정렬 + **`Compute` 최대치 < `ProjectileOffset`(1000)** 단언 (critic M-9: 폭 30 은 경계를 비껴가는 값)
- [ ] `Tilemap.GetTile` 로 게임 상태를 읽는 코드 0건 (grep — 단 reflection 문자열 접근은 grep 이 못 잡으므로 위 3개소 수동 확인)
