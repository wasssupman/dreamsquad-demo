# 2 — handoff summary (prop-placement-layer)

## Commit
- unit 0 `e42e2c7` — InstantiateProp 추출 + spec.
- unit 1 (이 커밋) — goal/spawn 3D 구조물 프랍 + authoring.

## Implemented
- `TilemapMapView.InstantiateProp(PropData, PropPlacement, plan, theme, root)` — resolved PropData 받는 단일 프랍 인스턴스화(배경/구조물 공용). propIndex 로 tileProps 재조회 안 함.
- `TilemapMapView.InstantiateStructureProps` — goal/spawn 셀에 구조물 프랍 배치. `_structurePropsRoot` 를 `localRotation=Euler(-90,0,0)` 로 부모(grid 90°X) 상쇄 → 메쉬가 빌보드 없이 똑바로 섬.
- 놓은 셀의 overlay 마커 타일 `SetTile(cell, null)` 억제.
- `MapThemeData.goalStructureProp/spawnStructureProp : PropData` (테마별 swap).
- `BattleBridge` 에서 `theme.tileProps` 가드와 독립한 별도 호출.
- authoring: `struct_spawn_arch`(platform_arrow_red)·`struct_goal_structure`(platform_arrow_yellow) PropData, `visualScale=0.5`, forest 테마 배선.

## Key Files
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (InstantiateProp / InstantiateStructureProps / PlaceStructure / _structurePropsRoot / Clear)
- `Assets/_Project/Scripts/Data/MapThemeData.cs` (Structures 필드)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (배선 ~766)
- `Assets/_Project/Data/Theme/forest/struct_*.asset` · `Assets/_Project/Map/Theme/forest/forest.asset`

## Verified
- compile 클린. Play(포커스 세션) 검증 PASS: StructureProps 3개 upright(worldRot 0)·정확한 셀·마커 억제·콘솔 에러 0.
- sim 무변경 — 구조물 = 순수 View(Obstacle 미부착), goal 판정은 GeneratedMap/FlowField 사용.

## Notes (되돌리면 안 됨)
- **`billboardMode=None` 은 이미 "바닥에 눕는 sprite" 의미**(prop_concept_*/prop_edge_forest_* 8개). 구조물은 그걸 hijack 하지 않고 **역회전 root** 로 세운다. None 분기를 InstantiateProp 에 넣지 말 것.
- Tilemap-only. Legacy `MapView.BuildGoalMarker` 미변경.
- 비포커스 MCP Play 는 frame 0 으로 얼어 맵빌드 안 됨 — 라이브 검증은 에디터 포커스 필요.

## Follow-up (README 후속 후보 참조)
- 데코 authored 배치(`decorProps`) · 런타임 blocker 프랍(destructible-blocking-hazards 재사용, Obstacle=Effects, 새 큐 금지) · 적유닛 발원 blocker(NativeQueue) · footprint 다중셀 정합.
