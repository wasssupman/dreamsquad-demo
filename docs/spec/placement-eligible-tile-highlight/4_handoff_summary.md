# 4 — Handoff Summary

브랜치: `feat/placeable-tile-highlight` (main 미반영, 롤백 가능). 코드 3유닛 완성·검증, 실아트만 대기.

## Commit

- `df75b984` docs — 스펙(전략 B)
- `47ec8117` unit 0 — TileSetData 하이라이트 필드
- `a4b15d53` unit 1 — `_placeableTilemap` 레이어
- `3f45fd83` unit 2 — 공유 술어 + bridge 배선 + 컨트롤러 토글

## Implemented

- 방어 유닛 **드래그/탭 arm 중 배치 가능 셀(Place ∖ 점유)을 밝게 하이라이트**. 전용 `_placeableTilemap`, 정적(펄스 없음), 집을 때 페이드인만.
- `SpatialPlacementCheck`(순수 static, 값 in→reason out)를 `CanPlaceDefenderAt` 에서 추출 — **판정과 하이라이트 셀 수집이 같은 술어 공유**.
- bridge 게이트웨이 `ShowPlacementHighlight`/`HidePlacementHighlight`/`RefreshPlacementHighlightIfShown` + `_occupiedTiles` 변이 4곳(사망 해제·pending Add×2·Clear)에서 변경구동 리프레시.
- 컨트롤러 파생상태 1함수 토글: `desired = (_session.active && !_simulatedDrag) || _armedUnit != null`. 탭 비행 중 자동 OFF, 세션 하이재킹 무관.
- 드래그 중 유닛 위로 상승(9998, range 10000 아래), arm/정적은 바닥(−13). `_rangeTilemap` 과 완전 분리(owner enum 밖).
- 2-state(가능/불가) — 점유칸은 밝히지 않고 그 위 유닛이 마커. 유닛 몸체 틴트 안 건드림.

## Key Files

- `Assets/_Project/Scripts/Data/TileSetData.cs` — `placeableTile`/`placeableColor`/`placeableFadeInDuration`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `_placeableTilemap`, `SetPlacementHighlight`/`ClearPlacementHighlight`, `EnsurePlaceableTilemap`, `Update()` 페이드(사거리 펄스와 독립), `SetPlacementHighlightAboveUnits` 상승 합류, `Clear()` teardown
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpatialPlacementCheck`(public static), Show/Hide/Refresh/`RepaintPlacementHighlight`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `UpdatePlacementHighlightState` + CleanupSession hide
- `Assets/_Project/Tests/EditMode/SpatialPlacementCheckTests.cs`

## Verified

- 컴파일 0 err / 0 warn (MCP 리컴파일).
- EditMode `SpatialPlacementCheckTests` **5/5** (None/NotBuildable/OutOfBounds/Occupied/MissingMap).
- Play 배선(BattleScene, 임시 타일 in-memory): **배치가능 82/200셀 정확 페인트**(경로·Deco 구멍 제외), **노란 사거리와 공존**(placeable −13 / range −12, 사거리 위로 읽힘), Hide→0셀 소거. 에셋 오염 0(SaveAssets 안 함 → Play 종료 자동 원복, git 클린).
- **실측 배치가능 비율 = 41%**(20×10 중 82). "약 절반+Deco 구멍"이 사실. 밝혀도 경로·구멍으로 쪼개져 판을 안 뒤덮음(B 결정 실증).

## Notes (되돌리면 안 되는 의도)

- **다크마스크 초안 → 밝게(B) 반전 확정.** 근거: 배치영역 실측 41%(라이브 테마 `mapGridBuildableKeepRatio=0.6`)라 밝혀도 figure-ground 안 뒤집힘 + 표준 TD 문법. 밝게가 사거리를 죽이던 문제는 **사거리 다크라이너(unit 3)로 해결**, 마스크 반전 아님.
- `_rangeTilemap` 재사용 금지(RangeDisplayOwner=상호배타 시분할). 하이라이트는 항상 직교하는 전용 레이어.
- **라이브 펄스는 사거리 독점** — 하이라이트는 정적(상태지 이벤트 아님).
- **하이라이트=공간 조건만**(bounds/Place/점유). 비용/풀 안 봄 → 코스트 부족이면 "밝은데 hover invalid"가 정상. 비용 끼우지 말 것(보드 전체 깜빡임).
- 점유 3번째 명도 상태 만들지 말 것. `SpatialPlacementCheck` 는 순수 static 유지(EditMode 테스트 앵커).

## Follow-up

- **실아트(핵심 미완)**: `placeableTile` = 슬랩+밝은 림 스프라이트, `placeableColor` = 옅은 시안 계열 RGBA(시안 확정값) → 라이브 TileSet(`TileSet_AutoTileTest`/`TileSet_Desert` 등)에 할당 + SaveAssets. **현재 미할당이라 `SetPlacementHighlight` no-op** — 붙여야 실제로 보임.
- **unit 3 다크라이너**: 실아트(옅은 시안) 붙였을 때 밝은 칸 위 노란 사거리가 약하면 `tile_grid_outline.png` 스프라이트 교체(코드 0). 불투명 마젠타에서도 읽혔으니 optional 가능성.
- 최종 Play 튜닝(실아트, `placeableColor` 알파/림), 그 뒤 main 머지 판단.
- README 후속 후보: arm 중 스킬조준 진입 시 억제, 연속배치 시 유지, 배치비율 seed·프리셋별 정밀 측정.
