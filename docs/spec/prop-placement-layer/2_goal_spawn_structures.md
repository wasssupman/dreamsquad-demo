# 2 — goal/spawn 구조물 프랍

## 목적

goal/spawn 셀을 **KayKit 3D 메쉬 구조물 프랍**으로 렌더하고, placeholder 마커 타일(`PH_Goal`/`PH_Spawn`)을 억제한다. 데이터 구동(`MapThemeData`), 순수 View, Tilemap-only, sim 무변경. unit 1 의 메쉬 업라이트 지원 위에 얹는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs` (구조물 프랍 필드)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (sibling 메서드 + `PaintMarkers` 억제 + root/Clear)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (배선, 약 735–765)
- authoring: KayKit 프리팹 → `PropData`(goal/spawn 2종) + 테마 에셋 지정

## 구현

- **`MapThemeData`**: `[Header("Structures")]` + `public PropData goalStructureProp;` `public PropData spawnStructureProp;` (Background Props 근처).
- **`TilemapMapView`**:
  - `public void InstantiateStructureProps(in GeneratedMap map, MapThemeData theme, BoardVisualPlan plan)` — goal/spawn 셀마다 해당 `PropData` 가 null 아니면 인라인 `PropPlacement`(셀 x/y + `prop.Footprint`, propIndex 미사용) 생성 → unit 0 의 `InstantiateProp(prop, placement, plan, theme, _structurePropsRoot)` 호출.
  - 별도 `_structurePropsRoot` 신설, `Clear()` 에 dispose(배경/링 root 동일 패턴).
  - **`PaintMarkers`**: `theme.goalStructureProp != null` → goalTile skip, `spawnStructureProp != null` → spawnTile skip. theme 참조 전달 필요(`Initialize`→`PaintMarkers`).
- **`BattleBridge`**: prop 블록(약 746, `InstantiateBackgroundProps` 호출부)에 `tilemapMapView.InstantiateStructureProps(...)` **별도 호출** — `UseTilemapView` 가드, `theme.tileProps != null` 가드와 독립.
- **authoring**: `Assets/KayKit/Packs/KayKit - Platformer Pack` 에서 goal(성/깃발/포탈류)·spawn(문/게이트류) 메쉬 프리팹 선택 → `PropData`(`billboardMode = None`, footprint, visualScale) 작성. 테마 에셋에 지정.

## 완료 기준

- compile 통과 (`read_console` 클린).
- Play → 게임뷰: goal/spawn 위치에 3D 구조물 출현·**똑바로 서고 접지**, `PH_Goal`/`PH_Spawn` 납작 타일 사라짐. 배경 프랍·유닛 무영향. 스크린샷 육안.
- sim 불변: 적이 여전히 goal 도달(FlowField/GoalReached 무변경), console 클린.
