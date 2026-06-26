# 6 — Handoff Summary

## Commit
- `a7e794b` `feat(view): Tilemap 광역 터레인 + 배경/원경 프랍 (tilemap-world-surround 0~5)` · **main 머지됨** (fast-forward, main tip `0f07a8c`, 2026-06-26).

## Implemented (단위 0~5, 전부 Presentation 계층 · sim/ECS 무영향)
- **내부 Deco 생성**: MapGrid 맵에 `ObstaclePlacer.DesignateDeco`(Place→Deco, Walk 불변, 시드 결정적). 노브 `MapThemeData.mapGridBuildableKeepRatio`(forest=0.6).
- **보드 내부 프랍**: `BattleBridge` gate 해제 + `TilemapMapView.InstantiateBackgroundProps`(좌표 grid 권위=BoardSpace 수식). 근경 그림자 CAST.
- **같은-category 인접 회피 룰**: `PropData.category/sameCategoryMinDistanceCells` + `BackgroundPropPlacer.ViolatesSameCategory`. 꽃 연속 배치 방지(꽃=2).
- **프랍 에셋 7종**: Test 스프라이트(flower×3/rock×3/tree) → prefab+PropData+머티리얼. 그림자용 `URP/Unlit+_ALPHATEST_ON`.
- **외곽 터레인 링**: `PaintSurroundRing` — 보드 밖에 **플레이 영역과 동일 풀 타일**(decoTile) + `Color.Lerp(흰색, surroundFarColor, t)` 그라데이션(안쪽=원색 매끄러운 연속, 바깥=어두움) + Perlin 노이즈로 banding 제거.
- **원경 프랍(침엽수림)**: `InstantiateRingProps` — 링 scatter + falloff, 그림자 OFF, 꽃 제외. `distantRingWeight`(tree=14·rock=1) + density 0.55 로 빽빽한 침엽수림 backdrop(~230).
- **모바일 예산**: `mobilePropBudgetScale`(0.5) — 모바일에서 근경/원경 프랍 솎음. 그림자: 근경 cast/원경 off/모바일 전부 off.

## Key Files
- `Scripts/Bridge/BattleBridge.cs` (Deco designate · prop gate · ring 호출 · 모바일 예산 · 그림자 정책)
- `Scripts/Core/TilemapMapView.cs` (VisualPlan · InstantiateBackgroundProps · PaintSurroundRing · InstantiateRingProps · CellCenterToWorld)
- `Scripts/Data/BackgroundPropPlacer.cs` (theme threading · ViolatesSameCategory)
- `Scripts/Data/{PropData,TileSetData,MapThemeData,ObstaclePlacer}.cs`, `Scripts/Presentation/PropBillboard.cs`
- 에셋(정식화 후): `Prefabs/Props/forest/`(prefab+mat), `Data/Theme/forest/prop_{flower_*,rock_*,tree}.asset`, `Art/Theme/forest/`(스프라이트 7), `Map/Theme/forest/forest.asset`(tileProps 교체·튜닝), `Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset`(ring 값), `Scenes/BattleScene.unity`(PropsTilemap 비활성)

## Verified
- compile 0 에러. Editor Play(BattleScene, MapGrid+Tilemap): Deco grass + 근경 프랍(그림자 CAST 확인) + 외곽 링(유기적 페이드) + 원경 트리/돌. 가독성 유지.
- 객관: 꽃 인접 0쌍(Chebyshev≥2). 페인트 32×22. 데스크톱 풀카운트 / 모바일 배율 로직.
- console: 기존 missing-script 경고 4건(무관), URP Forward+ 경고(무관). CS 에러 0.

## Notes (되돌리면 안 되는 의도)
- 프랍 좌표는 반드시 `CellCenterToWorld`(grid). raw `(x,y)*tileSize` 금지.
- 링 셀은 sim 무관 — `GeneratedMap.gridSize`/`BoardSpace.Configure` 는 N×M 유지.
- Legacy3D 경로 불변: ObstaclePlacer.Place 동일 keepTarget, MapView 헬퍼는 visibility만 internal 확대.
- forest 테마 tileProps 가 기존 21종 → keeper 7종으로 교체됨. 7종은 `forest` 정식 위치로 승격(264097d, R100 GUID 보존). 옛 21종 `prop_style_*` 에셋은 아직 보존(고아 — 3번에서 정리).
- 씬 저장 시 PropsTilemap 비활성 포함. 프리뷰 타일 유입 주의(edit 모드 저장).

## Follow-up

### 다음 작업 (이월 — 사용자 지정 2026-06-26)
다음 세션이 이어받을 우선 작업. 본 spec 본체는 완료, 아래는 "정식 자산화 + 잔여 토대" 정리.

- ~~**머지 결정**: `feat/tilemap-world-surround` → main 머지/PR 여부.~~ — **완료** (ff 머지, main `0f07a8c`, remote 없어 PR 생략).
- ~~**프랍 에셋 정식화**: `Data/Theme/test/`·`Prefabs/Props/test/`·`Generated/Tiles/Test/` → 정식 위치 승격.~~ — **완료** (264097d. 28 에셋 R100 rename → `Data/Theme/forest/`·`Prefabs/Props/forest/`·`Art/Theme/forest/`. forest.asset 참조·PropBillboard 295 런타임 검증. 파일명 유지, 폴더만 승격).
- **고아 에셋 정리**: forest.tileProps 가 기존 21종 → test 7종으로 교체되며 옛 21종 프랍 고아 → 삭제/보존 결정.
- ~~**4b 프랍 틸트**: `PropData.tiltAngle` + `Tilted` 미사용 → 적용 or 휴면 결정.~~ — **완료** (316d45a, `7_prop_tilt_apply.md`. 7종 Tilted 전환, flower 38°/rock 45°/tree 50°. 코드 무변경, 원근감 회복).
- **Legacy 환경 프랍 노출 확인**: `tilemapHiddenEnvironment: []`(빈 배열). 스크린샷상 누출 없어 사실상 해소로 보이나 확정 확인.

#### 프랍 비주얼 가림 회피 (occlusion-aware placement) — 방향성만 기록, 구현 보류 (2026-06-26)

**문제**: 틸트(7) 적용 후, footprint 1×1 나무라도 `Euler(φ,0,0)`로 **+Z(셀 +y, 보드 안쪽) 방향으로 누워** 화면상
여러 셀을 가린다. 현재 `BackgroundPropPlacer.CanFit`은 footprint만 Env 검사 → 비주얼 투영 무지. `InstantiateRingProps`는
Env/footprint 검사 자체가 없어 보드 앞쪽(작은 y) 링의 큰 나무가 플레이 영역(Walk/Place)을 덮음.

**기하 근거**: 틸트 빌보드 up `(0,1,0)→(0,cosφ,sinφ)`. 누운 수평 성분 = `H·sinφ`. prop_tree 추정 H≈2.86
(visualOffset.y 1.428=half-height) → `+y`로 ≈2.2셀 투영(footprint 1×1, 비주얼 ~1×3). 방향 `+y` 고정(카메라 고정).

**합의된 접근 (정밀 occlusion 모델)**: per-prop `L=⌈H·sinφ/cell⌉` 자동 산출(하드코딩 회피) → occlusion 셀
`{(cx, cy+1..cy+L)}`(폭=footprintX). 근경=`CanFit`에 occlusion 셀까지 Env 요구. 원경=링 배치 시 occlusion이
플레이 영역(0..w,0..h) 침범하면 큰 프랍 skip(작은 것 대체). 별도 작업 단위(`8_occlusion_aware_placement.md`)로
설계 예정. **착수 전 Play 실측으로 누운 방향 `+y`·tree 투영 길이 확정해 L 산출식 못박기.** 대안(근사 휴리스틱:
앞쪽 링 큰프랍 가중치 0 + tree footprintY 확대)은 단순하나 경계 누락 → 정밀 모델 권장.

### 폐기 (사용자 지정 2026-06-26)
- ~~멀티 시즌(lava/lunar/cosmic) 테마에 동일 ring/deco 처리~~ — **드롭**. forest(S1)만 유지.

### 장기 후보 (당장 불필요)
- 실기기 프로파일링 후 프랍 캡/밀도/`mobilePropBudgetScale` 재튜닝.
- tilemap 모드 시즌 스카이박스(현재 다크 페이드 대체).
- 프랍 그림자 strength/soft 튜닝(tilemap-real-shadows 종속).
- (선택) 원경 나무가 안쪽 링 가장자리 약간 침범 → 안쪽 1~2 링 나무 비우기.
