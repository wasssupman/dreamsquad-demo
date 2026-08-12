# Map/View 사장 코드 정리 (map-view-deadcode-removal)

**작성일**: 2026-08-12
**상태**: **완료 2026-08-12** — unit 0~3 구현·검증·커밋 완료. 인계 요약은 `4_handoff_summary.md`.
**목표**: 맵/보드 뷰 계층에서 **참조가 0이거나 도달 불가로 확정된** 심볼·에셋을 걷어낸다. 런타임 거동은 한 톨도 바꾸지 않는다.

## 왜 지금인가

`BoardViewMode.TilemapIso` 를 폐기하기로 결정(2026-08-12)하면서 iso 경로가 확정 사망했고, 그 조사 과정에서 **iso 와 무관하게 이미 죽어 있던 것들**이 함께 드러났다. 맵 구현 방식 개편(원화 기반 / 격자 축소)이 `MapDocument` 스키마와 `TilemapMapView` 를 건드릴 예정이라, 죽은 코드를 지고 들어가지 않도록 **개편 착수 전에** 정리한다.

## 검증 질문

1. **거동 불변**: 정리 전후로 Play 결과·화면이 동일한가? (같은 맵·같은 seed)
2. **참조 0 주장이 옳은가**: 제거한 심볼이 정말 아무도 안 읽었는가? (컴파일이 1차 증인)
3. **테스트가 지키던 성질이 유실되지 않았는가**: 삭제한 테스트의 계약이 다른 형태로 남았는가?

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 삭제 | `0_camera_preset_retirement.md` | 은퇴한 `ApplyTilemapCameraPreset` 계통 통째 제거 (메서드 + SerializeField 2 + SO 1 + 에셋 2) |
| 1 | 삭제 | `1_board_view_mode_collapse.md` | `BoardViewMode` enum 접기 — iso 분기·`isoCellSize`·`BoardSpace.Mode`·`PH_Iso_*` 에셋 제거, 시그니처 3개 정리 |
| 2 | 삭제 | `2_map_metadata_arrays_removal.md` | 소비자 없는 `mergeDegree`/`chokepoint`/`propLayerId` 3배열 제거 |
| 3 | 삭제 + 감사 | `3_residual_and_reaudit.md` | `MapDocument.goal`(단수) 폴백 → loud 검증, `PH_Rect_Env.asset`, **삭제 연쇄 재감사** |
| 4 | Handoff | `4_handoff_summary.md` | 인계 요약 + 커밋 표 + stale 어셈블리 함정 |

의존 순서: `0 → 1 → 2 → 3`. 0 과 1 은 둘 다 `BattleBridge` SerializeField 를 지우므로 연속 처리한다.

## Feature-wide 계약

- **동작 변경 0 이 최상위 계약.** 어떤 단위도 런타임 거동·화면을 바꾸지 않는다. "이왕 지우는 김에" 리팩터 금지.
- **SerializeField 제거는 씬을 편집하지 않는다.** 필드가 사라지면 `BattleScene.unity` 의 해당 YAML 키는 orphan 으로 남고 무해하다(기존 판례: `goalMaxStability`). **이 spec 은 `BattleScene.unity` 를 커밋하지 않는다.** 씬이 dirty 해 보이면 다른 세션의 작업이므로 손대지 않는다.
- **제거 근거를 문서에 남긴다.** 각 단위는 "제거 대상 / 참조 수 / 남기는 것"을 명시한다. 근거는 `Assets/_Project` 전수 grep 기준이며 `il2cppOutput` 백업은 제외한다. reflection 접근은 grep 에 안 잡히므로, 컴파일 통과 + EditMode green 을 2차 증인으로 삼는다.
- **테스트는 지우기 전에 "그 테스트가 지키던 성질"을 먼저 적는다.** 성질이 아직 유효하면 다른 방식으로 다시 못 박는다. 특히 iso 정합 테스트는 *"`BoardSpace` 가 iso 수식을 하드코딩하지 않고 `GridLayout` 에 위임한다"* 는 계약을 지키고 있었다 — 이건 iso 폐기 후에도 유효하다 (unit 1 참조).
- **안전망은 건드리지 않는다.** `BattleMapBuilder.BuildFallbackLinear` 와 `MapConnectivity` 는 정상 콘텐츠에서 미도달이지만 connectivity 실패 시 freeze 를 막는 안전망이다. 제거 대상 아님.
- **범위 밖**: `MapTileType.Env`(9장 전부 0개), `ObstaclePlacer.DesignateDeco`(게이트가 9장 전부 차단), `TilemapPropScatter`(프랍 시스템 2벌 중복). 셋 다 **맵 개편 방향 결정에 종속**이라 여기서 다루지 않는다 — 아래 후속 후보 참조.

## 파이프라인 커버리지

**N/A** — 본 spec 은 플레이 오브젝트를 신설하지 않고, 생성→렌더 경로를 변경하지도 않는다(순수 삭제). `TilemapMapView.Initialize` 시그니처가 바뀌지만 정거장 구성·순서는 불변이므로 `docs/reference/object-pipeline-map.md` 갱신 대상 아님.

## 후속 후보

- **`MapTileType.Env` 제거** — 9장 전부 0개인 죽은 데이터 값. `envTile` 슬롯 · `BoardVisualCell` 의 Env 후보 등록 · `PlacementLayers.Derive` 의 Env 분기가 전부 빈 경로다 (placeholder 에셋 `PH_Rect_Env`/`PH_Iso_Env` 는 참조 0 이라 unit 1·3 에서 먼저 지운다). 단 EditMode 테스트 여럿(`BoardVisualPlanBuilderTests`, `BackgroundPropPlacerTests`)이 Env 를 **픽스처로** 쓰므로 함께 손봐야 한다. 맵 개편에서 Deco/Env 개념이 재정의될 수 있어 대기.
- **`ObstaclePlacer.DesignateDeco` + `RederivePlaceMask` 제거** — 게이트(`theme.keepRatio<1 && !hasAuthoredDeco && !hasAuthoredMaskIntent`)를 9장 전부 통과 못 한다. 두 테마의 `mapGridBuildableKeepRatio: 0.6` 도 무의미한 저작값. 절차적 Deco 커빙을 되살릴지가 맵 개편 결정에 달렸다.
- **프랍 시스템 2벌 중 하나 정리** — `TilemapPropScatter`(BattleScene 배선·활성, `groundTilemap.GetTile` 으로 Deco 셀 역판정)와 `BoardVisualPlan`+`BackgroundPropPlacer`(`GeneratedMap` 기반)가 독립적으로 돈다. 의도적 레이어링(타일=잔풀/꽃, 프리팹=나무/바위)인지 확인 후 판단.
- **`PaintMarkers` → `InstantiateStructureProps` paint-then-erase** — 구조물 프랍이 있는 테마(forest)는 goal/spawn 타일을 칠한 직후 같은 셀을 `null` 로 지운다. 무해하지만 낭비.
- **`BoardSortOrder.Compute` 깊이 결함** [별건] — `(gridSize.y - cellY) * 10 + cellX` 에서 행 간격 10 < 맵 폭(13~30)이라 먼 행 유닛이 가까운 행 유닛 위에 그려진다. **사장 코드가 아니라 버그**라 본 spec 밖. 별도 처리 필요.
