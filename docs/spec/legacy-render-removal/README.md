# legacy-render-removal — Legacy MapView 렌더 경로 제거

상태: 초안 (설계 승인 2026-07-03)

## 문제

보드 렌더가 두 세대로 갈려 있다:

- **현행 Tilemap** (`boardViewMode=TilemapRect`, 실 씬 `BattleScene.unity`): `TilemapMapView` + `tileSet`(TileSetData)로 바닥 렌더.
- **Legacy 3D** (`boardViewMode=Legacy3D`, `BattleScene_Legacy3D.unity`): `MapView` + `TerrainSurfaceSelector`/`TerrainTileRuleResolver`가 `MapThemeData` 텍스처 필드로 절차적 렌더.

Legacy는 **향후 사용 계획 없음**(사용자 확정). 그런데 기존 로직과 얽혀 이전 세션에서 삭제 보류됨. 그 얽힘 때문에 `MapThemeData`에 **LEGACY 필드 43개**(텍스처/surface rule/walk shape/place transition/zone tint)가 남아 forest.asset을 오염시키고 있다(audit 확인, prop-upright-root 후속).

## 목표

Legacy 렌더 서브시스템과 그것만 읽는 43개 LEGACY 테마 필드를 **완전 제거**한다. Tilemap 경로·sim·배치 로직은 무변경.

## 비목표

- Tilemap 렌더/배치 로직 변경 금지.
- sim(ECS)·flowfield·좌표계 변경 금지.
- `boardViewMode` enum 자체는 유지 가능(TilemapRect/Iso 구분에 쓰임). `Legacy3D` 값만 제거 대상.

## 얽힘 (이전 세션 blocker, audit 확인)

1. **`TilemapMapView`(ACTIVE)가 `MapView`의 static 헬퍼 호출** — `ApplyPropSorting`/`ApplyPropGlobalTint`/`DisablePropDebugMarkers`. → MapView.cs 통삭제 전에 추출 필요.
2. **입력/배치가 `MapView` 타입 참조** — `PlacementInput.cs:22`(SerializeField), `DefenderSelector.cs:198`(`bridge.MapView`), `DefenderDragPlacementController`.
3. **`BoardViewMode.Legacy3D` 분기가 presentation ~9파일** — BoardSpace(좌표변환)/QuadUnitView/DamageNumberView/SpineUnitView/SkillBar/BoardCameraPreset/BattleBridge.
4. **`BattleBridge` `!UseTilemapView` 분기 3곳** + `BackdropMounter`(Legacy3D 전용 시즌 백드롭).
5. **테스트** `TerrainSurfaceSelectorTests.cs`(Legacy surface selector 전용).
6. **Legacy 씬** `BattleScene_Legacy3D.unity`.

(참고: movement/sim의 MapView 언급은 전부 **주석** — 실제 코드 의존 아님.)

## 작업 단위

| # | 문서 | 작업 | 완료 기준 |
|---|---|---|---|
| 0 | `0_extract_shared_prop_helpers.md` | `MapView`의 공용 프랍 헬퍼(ApplyPropSorting 등)를 중립 static 클래스(`PropInstanceUtil` 등)로 추출, `TilemapMapView` 참조 전환 | compile, Tilemap Play 프랍 렌더 무변경 |
| 1 | `1_resolve_input_maptype_dep.md` | 입력/배치(`PlacementInput`/`DefenderSelector`/`DefenderDragPlacementController`)의 `MapView` 의존을 Tilemap 경로/중립 인터페이스로 전환 | compile, D&D 배치 Play 검증 |
| 2 | `2_remove_legacy_render.md` | `MapView`/`TerrainSurfaceSelector`/`TerrainTileRuleResolver` + 전용 테스트 삭제, `BattleBridge` mapView 경로 정리, **백드롭 통삭제**(BackdropMounter/AnchorTable/SeasonBackdropData — 사용자 결정 2026-07-03) | compile, Tilemap Play 무회귀 |
| 3 | `3_remove_legacy3d_mode.md` | `BoardViewMode.Legacy3D` 값 + presentation 9파일 분기 제거, `BattleScene_Legacy3D.unity` 삭제 | compile, 전 모드 Play 무회귀 |
| 4 | `4_remove_legacy_theme_fields.md` | LEGACY 43개 필드 삭제(텍스처/surface/walk shape/place transition/zone tint + nested `TerrainSurfaceVariant`), forest/desert.asset 재직렬화 정리 | compile, Tilemap Play 무회귀, 인스펙터 정리 확인 |
| 5 | `5_handoff_summary.md` | 회귀 확인 + handoff | — |

## 검증 질문

> "Legacy 렌더 코드·씬·43개 테마 필드가 전부 사라지고, Tilemap 게임플레이/렌더/배치는 완전히 동일한가?"

각 unit compile-safe + Tilemap Play 스크린샷 무회귀. 배경/프랍 변경은 스크린샷 육안 검증 필수.

## 리스크 / 순서

- unit 0→1이 얽힘 해소(선행). 2·3·4가 실제 삭제. **순서 엄수** — 0 없이 2 하면 TilemapMapView compile 깨짐.
- 각 unit 독립 compile 가능하도록 분할(memory: 대규모 refactor는 compile-safe 서브태스크).
- 롤백: unit별 커밋. 삭제 unit(2~4)은 Play 무회귀 확인 후 커밋.

## 참고

- audit 근거: prop-upright-root 후속 MapThemeData 필드 audit (ACTIVE 24 / LEGACY 43 / DEAD 4). DEAD 4개는 이미 `be4666f`에서 제거.
- ACTIVE 필드(유지): tileSet, propGlobalTint, 프랍풀/링/구조물/이펙트타일/poisson 규칙, mapGridBuildableKeepRatio, minPlaceableRatio, obstaclePrefabs(map-gen).
