# Tilemap 광역 터레인 + 배경/원경 프랍

> 상태: 구현 완료 (단위 0~5 + 원경 그라데이션·침엽수림 rev) · 커밋 `a7e794b`/`750e7c9` · **main 머지됨** (ff `0f07a8c`, 2026-06-26)
> 전제: `tilted-billboard`(퍼스펙티브+XZ 바닥), `tilemap-real-shadows`(바닥 receive + 빌보드 cast). 커밋 `47e7925`, `a9c0b00`, `e62ea35`.
> 대상: `Assets/_Project/Scenes/BattleScene.unity` (Tilemap, URP). Legacy3D 불변.

## 목표 / 검증 질문

> **N×M 플레이 보드를 더 넓은 터레인으로 감싸고 보드 안팎을 프랍으로 채워 "플레이 영역이 더 큰 자연 환경에 자연스럽게 이어져 보이는가?" — 단, 플레이 영역(Walk+Place) 가독성은 유지.**

씬 `_TilemapBoard` 아래 미사용 `PropsTilemap` GO 를 비활성화하고, 프랍은 타일이 아니라 프리팹
(`PropData`/`PropBillboard`)으로 배치한다. 보드 내부의 비-플레이 셀(Deco)에 근경 프랍을,
보드 밖 외곽 링(순수 시각)에 원경 프랍을 채운다.

## 핵심 제약 발견 (탐색 결과 — 계획 재구성 근거)

- **현재 씬 = `mapSource: MapGrid` + Tilemap 뷰.** MapGrid 파이프라인(`MapGridGenerator`→`CellClassifier`)은
  내부 셀을 **전부 `Walk`(경로)/`Place`(배치)** 로만 분류 — `Env`/`Deco` 없음. 즉 **보드 내부에 '빈 타일'이 없다.**
- 따라서 "빈 타일에 프랍 배치"를 위해선 **내부에 `Deco` 셀을 논리적으로 만들어야 한다**(사용자 선택: option 3).
  옛 `ProceduralMapGenerator` 의 `ObstaclePlacer` 가 `Place→Deco`(buildable dirt 블롭 + 나머지 decorative)로
  하던 자연 분포를 MapGrid 에 도입한다.
- `Deco` 는 게임플레이 안전: `CanPlaceDefenderAt` 은 `Place` 만 배치 허용, 적은 `Walk` 만 이동 → `Deco`=비배치 장식.
- `theme` 은 **`SeasonRuntime.Active.mapTheme`** 런타임 주입(직렬화 필드 아님). deco/prop 파라미터는 시즌 `MapThemeData` 에 둔다.
- 씬 TileSet(`TileSet_AutoTileTest`) 의 `decoTile == envTile` → Deco 셀은 빈칸이 아니라 env/grass 비주얼로 렌더.

## 확정 설계 결정 (사용자 승인)

1. **광역 터레인 = 타일맵 외곽 링 확장.** `groundTilemap` 에 sim 그리드 밖 셀을 터레인 타일로 더 칠한다. sim 그리드는 N×M 그대로(순수 시각).
2. **플레이 영역 구분 = 주변 톤 다운.** 외곽 링/원경을 채도↓ + fog tint 로 어둡게 해 보드만 또렷하게.
3. **모바일 비용 = 근경만 그림자·원경 경량.** 근경 프랍만 real-shadow CAST + tilt. 원경은 그림자 OFF·단순 빌보드·저밀도. 모바일은 전부 그림자 OFF(기존 폴백 재사용).

## feature-wide 계약

- **전부 Presentation(MonoBehaviour) 계층.** ECS 맥락/`BattleBridge` 경계·sim 그리드 무수정. `EntityManager`/`SystemAPI` 직접 호출 금지.
- **좌표 권위 = grid.** 프랍 cell→world 는 반드시 `BoardSpace`/grid transform 경유. Legacy 의 raw `(x,y)*tileSize` 복붙 금지(90° 회전·센터 어긋남).
- **링 셀은 sim 무관.** `GeneratedMap.gridSize`/`BoardSpace.Configure` 는 N×M 유지. 링은 페인트 전용, sim 질의 대상 절대 아님. 센터링은 플레이 보드 기준.
- **내부 Deco 는 생성 후 데이터로 designate.** MapGrid 빌드 후 `theme.mapGridBuildableKeepRatio<1` 일 때만 `Place→Deco` 변환(시드 결정적, `Walk` 불변, 솔리드 buildable 블롭 보존). 기본 1=off. 옛 `ObstaclePlacer` 알고리즘 재사용(`DesignateDeco` 추출).
- **Deco=배경 호스트 재사용.** `BoardVisualPlanBuilder.ToZone` 가 `Deco`→`BoardZoneType.Env` 매핑 → `IsBackgroundCell` 가 이미 Deco 포함. 존 로직 신규 금지.
- **프랍 시스템 재사용.** `BackgroundPropPlacer`/`VisualPlan`/`PropData`/`PropBillboard` 를 그대로 쓴다. 새 배치 엔진 금지.
- **그림자 정책은 인스턴스화 시점 결정.** 근경=CAST(TwoSided)+tilt, 원경=OFF. `PropData` 에 cast 플래그 저장 안 함(같은 에셋이 근/원경 양쪽). 모바일 강제 OFF 는 `useRealShadows` 분기 재사용.
- **같은 category 인접 회피.** `PropData.category` + `sameCategoryMinDistanceCells` → 같은 그룹(예: flower 변종 3종)이 Chebyshev 거리 안에 붙지 않게(`BackgroundPropPlacer.ViolatesSameCategory`). 그 외엔 랜덤. 꽃=2.
- **프랍 에셋 = Test 스프라이트 기반 flower/rock/tree.** `Generated/Tiles/Test/` 의 Flower_Y/P/W·Rock_S/M/L·Tree 로 prefab+PropData 7종 생성(`Prefabs/Props/test/`, `Data/Theme/test/`). 그림자 CAST 위해 머티리얼=`URP/Unlit + _ALPHATEST_ON`(실루엣). 자동 그라운딩(visualOffset.y = 스프라이트 extents·scale).
- **틸트는 레이어 독립.** `PropData.tiltAngle`(기본 0)은 캐릭터 `CharacterBillboardTilt` 와 별개.
- **하드코딩 금지.** 링 반경/톤다운 색/밀도/falloff/캡은 전부 `MapThemeData`·`TileSetData`·serialized 필드.
- **씬 저장 1회.** UnityMCP Play 복귀 주의 — edit 전 isPlaying 확인/Stop. 씬 의존 배선은 마지막에 저장/커밋(프리뷰 타일 유입 방지).

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_prop_tilt_foundation.md` | PropData/PropBillboard | per-data 틸트(4b 토대). `tiltAngle` + `Tilted` 모드 |
| 1 | `1_mapgrid_deco_designation.md` | 내부 Deco 생성 | MapGrid `Place→Deco` 데이터 designate(시드 결정적, buildable 보존) |
| 2 | `2_tilemap_onboard_props.md` | gate 해제 + 좌표 | Tilemap 모드 Deco 셀에 근경 프랍 + CAST 그림자 (grid 좌표) |
| 3 | `3_terrain_ring.md` | 링 페인트 + 톤다운 | 외곽 터레인 링 + 채도↓ 로 보드 구분 |
| 4 | `4_distant_props.md` | 원경 프랍 | 링 위 저밀도 프랍 + falloff, 그림자 OFF |
| 5 | `5_budget_and_camera.md` | 모바일/카메라 | 그림자 정책·프랍 캡·링+스카이박스 합성 검증 |
| 6 | `6_handoff_summary.md` | 인계 | 구현 종료 요약 |

## 후속 후보 (범위 밖)

- 시즌별 차별화 터레인 타일아트 / per-season fog·라이팅 매칭 (seasonal-map-backdrop 후속).
- 작가 수동 프랍 배치 (`MapThemeData.decorProps` 활용).
- 터레인 링 높이 단차/메시 기복 (v1=평면+tint).
- 카메라 프리셋 자동 재적용 부활 (v1=수동 카메라 고정).
- `PropBillboard`↔`Billboard` 완전 통합 (v1=틸트만 이식, 컴포넌트 분리 유지).
