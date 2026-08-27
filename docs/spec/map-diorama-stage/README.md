# map-diorama-stage — 맵 저작을 디오라마 스테이지로 전환 (접근 A)

상태: **units 0~5·7 구현·검증 완료 2026-08-19 · unit 9 구현 2026-08-25 · units 10~12(본능 마커·Duel 재저작·레거시 은퇴) 구현 2026-08-26, Play 확인 대기** (브랜치 feature/map-diorama-stage · unit 6 포탈 = v1 제외) · 잔여: 육안 검증 축 5종(사용자)·OutgameScene dev 패널 수동 배선 1건·US-007 후속 조사(병합 게이트)

설계 근거·결정 이력·전투 접점 감사: [`docs/plans/2026-08-18-map-diorama-stage-design.md`](../../plans/2026-08-18-map-diorama-stage-design.md)

## 상위 목표

맵 저작을 "MapPainter 로 셀 칠하기(MapDocument)"에서 **"씬에서 Ground + 프랍을 자유 배치하는 디오라마 스테이지 프리팹"** 으로 전면 교체한다. 프랍의 명시 선언(footprint/마커)을 셀로 양자화해 논리 격자를 자동 파생하고, 이동은 **열린 마당**(walkable = 차단되지 않은 모든 셀)이 된다. 비주얼은 스테이지 프리팹 그 자체 — 바닥 타일맵 페인팅은 은퇴한다. **심(`GeneratedMap` 이하 Battle 전체)은 무변경.**

**지향점 (2026-08-18 사용자 확인)**: «Ground(터레인/메쉬, 예 100×100)를 만들고 스크립트를 붙이면 의도한 논리 타일맵이 구성되고 그 자체로 게임 진행이 가능하다.» 단 세 가지 정정이 이 지향의 계약이다:
- ① 논리는 터레인 표면이 아니라 **선언**에서 나온다 — 시스템은 터레인의 높이/텍스처/콜라이더를 읽지 않는다(D6: 선언 정본). **최소 플레이 가능 스테이지 = Ground + `MapStage` + `SpawnMarker`×2 + `GoalMarker`×1** — 차단 프랍 0개여도 판이 성립한다.
- ② 100×100 전체가 플레이 필드가 아니다 — 격자는 playArea rect(모바일 가독 스케일)에만 깔리고 잉여는 배경이다(계약 4).
- ③ 플레이 영역의 시각 바닥은 평평해야 한다(D4: 논리 Y=0) — 굴곡 터레인은 유닛/오버레이와 시각 충돌한다. 굴곡은 playArea 밖 배경에만. 터레인 저작은 «평평한 메쉬와 동일 취급»이며 높이/텍스처/콜라이더를 논리가 읽는 별도 검증은 없다.
- ④ **이동은 연속 월드 좌표, 판정은 셀** — 이것은 현행 심이 이미 그렇게 동작한다(부드러운 이동·평활화·충돌은 연속 좌표, 벽/배치/사거리 소스는 셀). 이 spec 은 셀 데이터의 **출처**만 바꾼다. 단 지형 메쉬에 그려 넣은 시각적 길은 논리가 모른다 — 적이 그 길을 따르게 하려면 `RouteMarker` 로 동선을 저작해야 한다(안 하면 골 직행).

**검증 무대 = KayKit 더미맵** (2026-08-18 사용자 결정): unit 2 픽스처·unit 5 파일럿 모두 `Assets/KayKit` Platformer Pack 조립로 만들고, 이 spec 의 모든 Play/육안 검증은 그 더미맵에서 진행한다.

## 작업 단위 목록

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [0_authoring_components.md](0_authoring_components.md) | 저작 레이어 | `MapStage`·`PropFootprint`·마커 컴포넌트 + 기즈모 (선언만, 로직 0) |
| [1_diorama_map_builder.md](1_diorama_map_builder.md) | 파생 코어 | 프랍 스캔 → 셀 양자화 → `GeneratedMap` 조립 (순수 코어 + EditMode 테스트) |
| [2_bridge_build_path.md](2_bridge_build_path.md) | 빌드 경로 | `BattleBridge` 문서 경로 → 스테이지 경로 교체 + `MapStagePool` |
| [3_overlay_view_split.md](3_overlay_view_split.md) | 뷰 은퇴/존치 | 바닥 페인팅 은퇴 · 오버레이 7채널 존치 · `BoardSortOrder` 간격 수정 |
| [4_goal_spawn_marker_views.md](4_goal_spawn_marker_views.md) | 뷰 재귀속 | 골/스폰 앵커·균열·붕괴 연출을 마커 뷰로 이관 |
| [5_pilot_map_playmode.md](5_pilot_map_playmode.md) | 검증 | 파일럿 스테이지 1개 + 육안 검증 축 5종 (PlayMode 스모크는 unit 2 로 이동 — critic M-12) |
| [6_portal_prop.md](6_portal_prop.md) | 선택 | 포탈 프랍 → `PortalLink` 엔티티 배선 (v1 필수 아님) |
| [7_legacy_retirement.md](7_legacy_retirement.md) | 은퇴 | `MapDocument` 계열·`MapPainterWindow`·구 Assets lane 테스트 3파일 처분 (critic M-3·M-4) |
| [9_bonus_spawn_marker.md](9_bonus_spawn_marker.md) | 병합 격차 | main `bonus-wave-pull` 의 포탈 칸 저작 축(`GeneratedMap.bonusSpawns`)을 스테이지 마커로 이식 — 미구현 시 스테이지 맵에 보너스 웨이브 버튼이 뜨지 않는다 |
| [10_structure_marker.md](10_structure_marker.md) | 병합 격차 | 본능 거점의 스테이지 저작 — 브리지 스폰 경로는 살아 있고 입력만 null 이었다. 마음(Core)은 계약 11 유지로 거부 |
| [11_duel_stage_street_style.md](11_duel_stage_street_style.md) | 재저작 | main 현행 Duel(23×10 열린 마당)을 Street 제작방식(바닥 Plane+스프라이트 프랍+마커)으로 `Art/Theme/duel/` 에 생성기로 조립, live 0번 |
| [12_legacy_stage_retirement.md](12_legacy_stage_retirement.md) | 은퇴 | `Prefabs/Maps` KayKit 조립 스테이지 11종 삭제(Fixture 포함) · 이름 pin PlayMode 테스트 재지정/Ignore |

## Feature-wide 계약

1. **정본 = `MapStage` 프리팹 1개.** 저작도 비주얼도 그 프리팹이다. `MapDocument`/`MapPainter` 는 이 브랜치에서 은퇴한다.
2. **`GeneratedMap` 구조체 무변경.** `tiles` 는 **합성**한다 — 열린 셀=`Walk`, 차단 셀=`Deco`. 목적은 기존 파생식(`walkMask`·`cellLayers`·픽업 후보 Walk∪Place)을 무수정으로 살리는 것. 합성 규칙 변경/`MapTileType` 은퇴는 접근 C(후속 spec) 몫.
3. **placeMask 는 빌더가 직접 조립한다** — 열린 셀 **기본** = `Ground|Path|Air`, 차단 셀 = 0. `PlacementLayers.Derive` 폴백에 기대지 않는다 (Place 타일이 없으므로 폴백은 오답). **`PlacementBlockZone` 은 옛 마스크 브러시의 후계이며 «전선»(여기 너머 배치 금지) 저작의 필수 수단이다** — 블랭킷 개방만 있으면 가디언(`placementLayers=Path` 단독 유닛)이 어디에나 놓이는, 이미 겪은 구멍이 맵 전체로 확장된다(critic C-2 · `MapDocumentPoolDevEntriesTests` ④ 가드의 의미를 스테이지가 승계). 주의: 배치 Air(차단 셀=0)와 통행 Air(`Derive(Deco)`=Air — 공중 적은 프랍 위를 넘는다)는 **다른 배열**이다.
4. **격자 = playArea rect 만.** Ground 가 그보다 커도 잉여는 셀 없는 순수 배경(서라운드 링의 후계). 카메라 프레이밍(`TryGetPlayfieldWorldBounds`)은 격자 기준이라 무변경.
5. **결정론은 명시 인덱스가 정본.** `SpawnMarker.laneIndex`·루트 인덱스는 저작 필드. 빌더 출력은 씬 계층 순서에 비의존(차단은 OR 라 순서 무관, 목록형은 인덱스 정렬). laneCount(=스폰 수)는 웨이브 결정론 키 — 스테이지 교체 시 웨이브가 바뀌는 것은 정상.
6. **심 코드 변경 0.** `Battle/**`·`GeneratedMap`·`FlowField*`·`NavGrid`·`SpatialPlacementCheck` 를 수정하지 않는다. 유일한 예외는 뷰 유틸 `BoardSortOrder`(간격 버그 수정, unit 3).
7. **`BoardSpace` 계약 유지.** `Grid` 가 좌표 권위, sim origin = `float3.zero`, `ToView` 의 Y-폐기(평면 보드) 유지 — 높이는 논리에 없다(사용자 결정 D4). **`grid.transform` 의 writer 는 스테이지 정렬 하나뿐이다** — `CenterBoardAtWorldOrigin`(현행 유일 writer)은 unit 2 에서 즉시 제거한다. writer 가 둘이면 프랍과 논리 셀이 조용히 어긋나고, 어긋난 채로도 격자 기준 완료 기준은 전부 통과한다(critic C-1).
8. **순수 코어 분리** (CLAUDE.md 제약 10): 양자화·마스크 조립·마커 수집은 plain 값 입출력 static 함수 — EditMode 테스트 대상. Mono 스캔 레이어는 얇게.
9. **안전망 재정의 — 연결성 실패 = 하드 실패** (critic M-1 로 개정): `MapConnectivity.AllSpawnsReachGoal` 실패 시 `MapGenerationFailedException` 동형으로 즉시 실패한다. 디오라마에서 연결성 실패는 저작 오류이고, 조용한 `BuildFallbackLinear` 교체는 절차 생성기의 유물이다 — unit 3 이후 폴백 맵은 렌더러가 없어 검은 판이 된다.
10. **밸런스 품질은 완료 기준 밖.** 열린 마당의 웨이브/덱 재밸런스는 별도 트랙 — 이 spec 은 파일럿 맵의 기능 검증까지만. (정정 2026-08-18: `WavePatternGenerator` 는 tiles 를 읽지 않는다 — 컨셉 게이트는 laneCount 축(계약 5)이 전부. `MapConceptRules` 는 페인터 저작 경고 전용이라 페인터와 함께 은퇴한다.)
11. **공성·적 마음·강(Env)은 비가용, 본능은 가용(unit 10).** `structures` 는 `StructureMarker`(kind=Instinct 만)에서 채운다 — Core 마커는 빌더가 «계약 11» 문구로 거부한다(적 마음 = 공성 모드·시드 스폰 파생·유출 판정을 끌고 온다). `_resolvedMapDoc` 은 영구 null(문서 은퇴) — 브리지의 거점 입력은 `_stageStructures`(스캔 결과, (y,x) 사전순, 맵 수명). `MapStagePool` 은 `WarnOnSiegeCoreHpMismatch` 를 승계하지 않는다.

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

## 결정 (2026-08-18 — critic C-2 해소)

**가디언 = (a) 전 마당 배치 허용.** 열린 마당에서 «Path 칸에만 선다»는 정체성은 완화되고, 가디언도 다른 유닛과 같이 모든 열린 셀에 배치된다. 배치 깊이 제한은 맵별 `PlacementBlockZone` 저작 몫(전 유닛 공통 — 계약 3). 가디언 SO 의 `placementLayers=Path` 저작값은 **그대로 둔다** — 블랭킷 마스크에서 결과가 동일하고, 물 영역(후속 후보) 도입 시 Water 층으로 갈아끼울 자리다. unit 1 마스크 조립식은 현행 spec 그대로, unit 5 육안 축 ⑤ 는 «전 마당 배치가 제품 의도대로 보이는가» 확인으로 수행.

## 후속 후보

- **접근 C**: `MapTileType` 은퇴 — 마스크 묶음(blockMask+placeMask+cellLayers) 정본화, tiles 합성 제거. footprint 별 차단 층 선언("공중도 못 넘는 프랍")도 여기서.
- **shape mask footprint** — L자/비사각 대형 구조물.
- **물 영역 (배치층 Water)** [S~M] — «물엔 특정 유닛만 배치» 룰은 기존 비트 교집합 기계가 그대로 받는다 (2026-08-18 분석): `WaterZone` 마커 + 빌더 규칙 1개(셀 tiles=Deco·placeMask=Water — placeMask 직접 조립 계약 3 덕에 «통행상 벽 + 배치 가능» 표현 가능) + `PlacementLayer.Water` 비트 append. 물 **적**(수영)까지 오면 tiles 합성 3값(Walk/Deco/Water) 개정 필요 — 기계 견적은 traversal-layers §6 (3줄 + 슬롯 0).
- **공격/투사체 지형 LOS** — 3D 프랍 관통 사격, v1 수용. 눈에 거슬리면 착수.
- **웨이브 열린 마당 재밸런스** — `MapConceptRules` tiles 직독 게이트 재검토 + `enemy-wave-integration` 스킬 갱신 + 기존 덱/플랜 재저작.
- **적 마음(Core) 스테이지 저작** — 공성 모드 재활성화. `StructureMarker` 의 Core 거부 해제 + 시드 스폰 파생(`SiegeSpawnOffsets`) + 유출/붕괴 판정 대조 + `StructureLivePlayTest` 재활성화(Test/SiegeTest 스테이지 저작).
- **Duel 재저작에서 main 과 갈린 두 지점** (unit 11) — ⓐ 차단 셀 위 공중 waypoint(옛 (11,4)) 허용, ⓑ 층별 `PlacementBlockZone`(적 진영 «Air 만 허용» — 지금은 전 층 0).
- **Ignore 4건 재활성화용 dev 스테이지 저작** — unit 12 가 은퇴시킨 routed-lane(`spawnRoutes`)·`RouteMarker` 순서·저작 플랜(`WavePlanAsset`) 라이브 커버리지가 0 이다(`SpawnGuideMatchesWalkTest.Coil_*`·`WaypointRoutingLiveTest` 3건). 이 spec 이 출하한 `RouteMarker` 의 유일한 라이브 테스트였다 — 경유점 2개 + 레인 기본 경로 + plan 짝을 가진 dev 스테이지 하나로 셋을 되살린다.
- **레거시 덱 정리·개명** — `Deck_Serpent/Zig/Coil` 은 Street/Subway/StreetDay 의 라이브 덱으로 재배정됐지만 이름이 은퇴한 맵을 가리킨다(`Deck_Street` 등으로 개명 시 `WaveConceptAuthoringTests`·`LiveDeckBossAuthoringTests` 의 이름 목록과 `map-wave-balancing.md` 동반 갱신). `Ford/Isle/Tutorial/WaypointLab/SiegeTest/Spiral/Twin/Hook/WaveA` 는 풀 밖 잔존(EditMode 덱 테스트가 이름으로 순회). 정리는 별도 결정.
- **`TileSetData` SO 분리** — 오버레이 절반만 남기는 정리(unit 3 은 필드 사용 중단까지만).
