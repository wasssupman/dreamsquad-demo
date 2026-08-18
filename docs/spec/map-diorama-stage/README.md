# map-diorama-stage — 맵 저작을 디오라마 스테이지로 전환 (접근 A)

상태: **설계 승인 2026-08-18 · 미착수** (별도 브랜치 작업 예정 — 브랜치 생성은 unit 0 착수 시)

설계 근거·결정 이력·전투 접점 감사: [`docs/plans/2026-08-18-map-diorama-stage-design.md`](../../plans/2026-08-18-map-diorama-stage-design.md)

## 상위 목표

맵 저작을 "MapPainter 로 셀 칠하기(MapDocument)"에서 **"씬에서 Ground + 프랍을 자유 배치하는 디오라마 스테이지 프리팹"** 으로 전면 교체한다. 프랍의 명시 선언(footprint/마커)을 셀로 양자화해 논리 격자를 자동 파생하고, 이동은 **열린 마당**(walkable = 차단되지 않은 모든 셀)이 된다. 비주얼은 스테이지 프리팹 그 자체 — 바닥 타일맵 페인팅은 은퇴한다. **심(`GeneratedMap` 이하 Battle 전체)은 무변경.**

## 작업 단위 목록

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [0_authoring_components.md](0_authoring_components.md) | 저작 레이어 | `MapStage`·`PropFootprint`·마커 컴포넌트 + 기즈모 (선언만, 로직 0) |
| [1_diorama_map_builder.md](1_diorama_map_builder.md) | 파생 코어 | 프랍 스캔 → 셀 양자화 → `GeneratedMap` 조립 (순수 코어 + EditMode 테스트) |
| [2_bridge_build_path.md](2_bridge_build_path.md) | 빌드 경로 | `BattleBridge` 문서 경로 → 스테이지 경로 교체 + `MapStagePool` |
| [3_overlay_view_split.md](3_overlay_view_split.md) | 뷰 은퇴/존치 | 바닥 페인팅 은퇴 · 오버레이 7채널 존치 · `BoardSortOrder` 간격 수정 |
| [4_goal_spawn_marker_views.md](4_goal_spawn_marker_views.md) | 뷰 재귀속 | 골/스폰 앵커·균열·붕괴 연출을 마커 뷰로 이관 |
| [5_pilot_map_playmode.md](5_pilot_map_playmode.md) | 검증 | 파일럿 스테이지 1개 + PlayMode 스모크 + 육안 검증 축 4종 |
| [6_portal_prop.md](6_portal_prop.md) | 선택 | 포탈 프랍 → `PortalLink` 엔티티 배선 (v1 필수 아님) |

## Feature-wide 계약

1. **정본 = `MapStage` 프리팹 1개.** 저작도 비주얼도 그 프리팹이다. `MapDocument`/`MapPainter` 는 이 브랜치에서 은퇴한다.
2. **`GeneratedMap` 구조체 무변경.** `tiles` 는 **합성**한다 — 열린 셀=`Walk`, 차단 셀=`Deco`. 목적은 기존 파생식(`walkMask`·`cellLayers`·픽업 후보 Walk∪Place)을 무수정으로 살리는 것. 합성 규칙 변경/`MapTileType` 은퇴는 접근 C(후속 spec) 몫.
3. **placeMask 는 빌더가 직접 조립한다** — 열린 셀 = `Ground|Path|Air`, 차단 셀·BlockZone = 0. `PlacementLayers.Derive` 폴백에 기대지 않는다 (Place 타일이 없으므로 폴백은 오답).
4. **격자 = playArea rect 만.** Ground 가 그보다 커도 잉여는 셀 없는 순수 배경(서라운드 링의 후계). 카메라 프레이밍(`TryGetPlayfieldWorldBounds`)은 격자 기준이라 무변경.
5. **결정론은 명시 인덱스가 정본.** `SpawnMarker.laneIndex`·루트 인덱스는 저작 필드. 빌더 출력은 씬 계층 순서에 비의존(차단은 OR 라 순서 무관, 목록형은 인덱스 정렬). laneCount(=스폰 수)는 웨이브 결정론 키 — 스테이지 교체 시 웨이브가 바뀌는 것은 정상.
6. **심 코드 변경 0.** `Battle/**`·`GeneratedMap`·`FlowField*`·`NavGrid`·`SpatialPlacementCheck` 를 수정하지 않는다. 유일한 예외는 뷰 유틸 `BoardSortOrder`(간격 버그 수정, unit 3).
7. **`BoardSpace` 계약 유지.** `Grid` 가 좌표 권위, sim origin = `float3.zero`, `ToView` 의 Y-폐기(평면 보드) 유지 — 높이는 논리에 없다(사용자 결정 D4).
8. **순수 코어 분리** (CLAUDE.md 제약 10): 양자화·마스크 조립·마커 수집은 plain 값 입출력 static 함수 — EditMode 테스트 대상. Mono 스캔 레이어는 얇게.
9. **안전망 유지**: `MapConnectivity.AllSpawnsReachGoal` 실패 시 `BuildFallbackLinear` — 기존 계약 그대로.
10. **밸런스 품질은 완료 기준 밖.** 열린 마당의 웨이브/덱 재밸런스(`MapConceptRules` tiles 직독 게이트 포함)는 별도 트랙 — 이 spec 은 파일럿 맵의 기능 검증까지만.

## 파이프라인 커버리지

가장 가까운 아키타입 = `docs/reference/object-pipeline-map.md` **프랍/타일 (맵 데코)**. 이 spec 이 그 경로를 교체한다:

| 정거장 | 현행 앵커 | 이 spec 이후 |
|---|---|---|
| 데이터 SO | `MapThemeData`+`PropData`+`TileSetData` | 스테이지 프리팹이 프랍을 직접 보유 — 테마 SO 경유 폐지. `TileSetData` 는 오버레이 필드만 사용(unit 3) |
| ECS | N/A — 맵 빌드 1회 | 동일 N/A — 프랍은 배틀 런타임 무관, footprint 는 빌드 시 `GeneratedMap` 으로만 반영 |
| 배치 계산 | `BackgroundPropPlacer` (절차 산포) | **N/A + 이유: 수작업 배치가 정본** — 절차 산포 은퇴 |
| 인스턴스화 | `BattleBridge`→`TilemapMapView.InstantiateProp` | 프리팹 인스턴스화 1회(`MapStagePool` 경유, unit 2) — 개별 프랍 인스턴스화 없음 |
| View | `PropBillboard`/`TilemapPropScatter` | 프리팹 authored(메쉬/빌보드 자유). `TilemapPropScatter` 은퇴(유일한 타일맵 역참조) |
| 씬 wiring | 씬 theme SO + tilemap GameObject | `MapStagePool` SerializeField + `OverlayView`(unit 3). Play 검증 = unit 5 |

골/스폰 구조물 프랍(거점 아키타입의 View 정거장 일부)은 unit 4 가 마커 뷰로 승계한다. 맵 구조 변경이 확정되면 `object-pipeline-map.md` 의 프랍/타일 표를 같은 커밋에서 갱신한다(워크플로우 5).

## 후속 후보

- **접근 C**: `MapTileType` 은퇴 — 마스크 묶음(blockMask+placeMask+cellLayers) 정본화, tiles 합성 제거. footprint 별 차단 층 선언("공중도 못 넘는 프랍")도 여기서.
- **shape mask footprint** — L자/비사각 대형 구조물.
- **공격/투사체 지형 LOS** — 3D 프랍 관통 사격, v1 수용. 눈에 거슬리면 착수.
- **웨이브 열린 마당 재밸런스** — `MapConceptRules` tiles 직독 게이트 재검토 + `enemy-wave-integration` 스킬 갱신 + 기존 덱/플랜 재저작.
- **기존 라이브 맵 9종 재저작 계획** — 파일럿 검증 통과 후 별도 결정 (사용자 D5: 전면 교체 방향).
- **`TileSetData` SO 분리** — 오버레이 절반만 남기는 정리(unit 3 은 필드 사용 중단까지만).
