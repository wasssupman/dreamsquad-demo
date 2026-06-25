# 6 — Handoff Summary

## Commit
- `a7e794b` `feat(view): Tilemap 광역 터레인 + 배경/원경 프랍 (tilemap-world-surround 0~5)` · 브랜치 `feat/tilemap-world-surround` (main 미머지).

## Implemented (단위 0~5, 전부 Presentation 계층 · sim/ECS 무영향)
- **내부 Deco 생성**: MapGrid 맵에 `ObstaclePlacer.DesignateDeco`(Place→Deco, Walk 불변, 시드 결정적). 노브 `MapThemeData.mapGridBuildableKeepRatio`(forest=0.6).
- **보드 내부 프랍**: `BattleBridge` gate 해제 + `TilemapMapView.InstantiateBackgroundProps`(좌표 grid 권위=BoardSpace 수식). 근경 그림자 CAST.
- **같은-category 인접 회피 룰**: `PropData.category/sameCategoryMinDistanceCells` + `BackgroundPropPlacer.ViolatesSameCategory`. 꽃 연속 배치 방지(꽃=2).
- **프랍 에셋 7종**: Test 스프라이트(flower×3/rock×3/tree) → prefab+PropData+머티리얼. 그림자용 `URP/Unlit+_ALPHATEST_ON`.
- **외곽 터레인 링**: `PaintSurroundRing` — 보드 밖 grass 링 + 톤다운(바깥 페이드 + Perlin 노이즈로 banding 제거).
- **원경 프랍**: `InstantiateRingProps` — 링 저밀도 scatter + falloff, 그림자 OFF, 꽃 제외(`excludeFromDistantRing`).
- **모바일 예산**: `mobilePropBudgetScale`(0.5) — 모바일에서 근경/원경 프랍 솎음. 그림자: 근경 cast/원경 off/모바일 전부 off.

## Key Files
- `Scripts/Bridge/BattleBridge.cs` (Deco designate · prop gate · ring 호출 · 모바일 예산 · 그림자 정책)
- `Scripts/Core/TilemapMapView.cs` (VisualPlan · InstantiateBackgroundProps · PaintSurroundRing · InstantiateRingProps · CellCenterToWorld)
- `Scripts/Data/BackgroundPropPlacer.cs` (theme threading · ViolatesSameCategory)
- `Scripts/Data/{PropData,TileSetData,MapThemeData,ObstaclePlacer}.cs`, `Scripts/Presentation/PropBillboard.cs`
- 에셋: `Prefabs/Props/test/`, `Data/Theme/test/`, `Map/Theme/forest/forest.asset`(tileProps 교체·튜닝), `Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset`(ring 값), `Scenes/BattleScene.unity`(PropsTilemap 비활성)

## Verified
- compile 0 에러. Editor Play(BattleScene, MapGrid+Tilemap): Deco grass + 근경 프랍(그림자 CAST 확인) + 외곽 링(유기적 페이드) + 원경 트리/돌. 가독성 유지.
- 객관: 꽃 인접 0쌍(Chebyshev≥2). 페인트 32×22. 데스크톱 풀카운트 / 모바일 배율 로직.
- console: 기존 missing-script 경고 4건(무관), URP Forward+ 경고(무관). CS 에러 0.

## Notes (되돌리면 안 되는 의도)
- 프랍 좌표는 반드시 `CellCenterToWorld`(grid). raw `(x,y)*tileSize` 금지.
- 링 셀은 sim 무관 — `GeneratedMap.gridSize`/`BoardSpace.Configure` 는 N×M 유지.
- Legacy3D 경로 불변: ObstaclePlacer.Place 동일 keepTarget, MapView 헬퍼는 visibility만 internal 확대.
- forest 테마 tileProps 가 기존 21종 → test 7종으로 교체됨. 옛 21종 에셋은 보존(고아).
- 씬 저장 시 PropsTilemap 비활성 포함. 프리뷰 타일 유입 주의(edit 모드 저장).

## Follow-up
- 실기기 프로파일링 후 프랍 캡/밀도 재튜닝.
- tilemap 모드 시즌 스카이박스(현재 다크 페이드 대체).
- 프랍 그림자 strength/soft 튜닝(tilemap-real-shadows 종속).
- handoff 커밋 해시 반영(커밋 후).
