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

## 실아트·튜닝 (완료 2026-07-18)

- 스프라이트 `Assets/_Project/Data/TileSets/placeable_slab.png`(64px, 흰색 베이스, 알파=림 불투명+베벨 falloff+옅은 내부) → `Tile_PlaceableSlab.asset`. 형태 조정은 이 png 교체로만(코드 무관).
- 4개 라이브 TileSet(`TileSet_AutoTileTest`(씬 활성)/`Desert`/`Placeholder`/`PlaceholderIso`)에 `placeableTile` 할당 + `placeableColor` = **시안 α0.5**(Play 비교로 α0.7 대비 채택 — 배치영역 ambient·사거리 focal 위계). C# 기본값도 동기화.
- **unit 3 다크라이너 = skip**(불필요, 위 검증). 밝은 신규 테마에서 사거리 약하면 그때 스프라이트 교체.
- **사거리 색 변경(교차-스펙)**: 배치영역(쿨 시안)과 공존 위해 `rangeColor` 를 **노랑→주황 `(1, 0.55, 0.12)`** 으로.
  쿨/웜 보색 대비 최대 + "공격=웜" 의미 + invalid-hover 빨강과 구분. 4개 라이브 TileSet 에셋에 반영(스킬 조준
  사거리도 같이 주황). `placement-attack-range-preview` 의 rangeColor 계약을 갱신 — 코드 기본값(TileSetData.cs)은
  아직 노랑(신규 tileset 만 영향, 실사용 무관). 사용자 색 선택 렌더 비교로 확정(주황 vs 보라 vs 코랄).

## z-fight 픽스 + 사거리 아웃라인 두께 (2026-07-18)

- **증상**: 사거리 아웃라인을 두껍게 하니 카메라 이동 중 "자글자글"(z-fighting). **원인**: ground 머티리얼
  `TileShadowReceive`(불투명·depth write)와 투명 오버레이 타일맵들이 전부 **coplanar(local z=0)** → 깊이 정밀도
  다툼. 두꺼운 라인이 coplanar 면적을 키워 악화. (코드 주석의 "타일맵끼리는 z-fight 안 함"은 **불투명 depth-writer
  ground 앞에선 틀림**.)
- **해결**: `TilemapMapView.EnsurePlaceableTilemap`/`EnsureRangeTilemap` 에서 타일맵을 카메라 쪽으로 미세하게 띄움
  (`localPosition.z = -0.04`/`-0.05`; grid 90°X 회전이라 local −Z = world +Y = 카메라 쪽). 깊이 평면 분리로 자글거림
  제거, 셀 정렬 영향 없음(0.04는 셀의 4%). 신규 오버레이 타일맵 추가 시 동일 오프셋 필요.
- **아웃라인 두께**: `tile_grid_outline.png` 2px → 튜닝 반복(6px→) → **3px solid + 1px soft inner** 안착(사용자 "더 크게"
  후 "좀 얇게"). soft edge 로 tilted 평면 aliasing crawl 감소. 형태 조정은 이 png 교체로만(range-preview 계약, 코드 무관).
- **사거리 알파 펄스 제거(사용자 요청)**: `Update()` sin 펄스 삭제 → **정적 알파**(`rangePulseMaxAlpha` 레벨). 화면 알파
  연출(맥동) 없음. `rangePulseMin/Speed` 는 vestigial(제거 안 함 — range-preview 데이터 최소 변경). 이제 전 오버레이 정적.

## Follow-up

- 실드래그(포인터) 중 상승(9998)+적 dim 상태에서의 체감은 미검증(정적 render+로직으로 확인) — 사용자 실드래그 Play 체감 1회 권장.
- 그 뒤 main 머지 판단.
- README 후속 후보: arm 중 스킬조준 진입 시 억제, 연속배치 시 유지, 배치비율 seed·프리셋별 정밀 측정.
