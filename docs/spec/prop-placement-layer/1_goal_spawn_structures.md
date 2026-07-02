# 1 — goal/spawn 구조물 프랍

## 목적

goal/spawn 셀을 **KayKit 3D 메쉬 구조물 프랍**으로 세우고, placeholder 마커 타일(`PH_Goal`/`PH_Spawn`)을 억제한다. 데이터 구동(`MapThemeData`), 순수 View, Tilemap-only, sim 무변경.

## 핵심 원리

메쉬는 빌보드가 아니다 — `PropBillboard`/`billboardMode` 를 쓰지 않는다. 프랍은 부모(grid, XZ 바닥 `Euler(90,0,0)`) 하위에 생기면 90° 를 상속해 눕는다. 그래서 구조물 전용 `_structurePropsRoot` 를 `localRotation = Euler(-90,0,0)` 로 두어 부모를 상쇄 → root 는 월드 업라이트, 그 아래 메쉬 child 는 identity 로 **똑바로 선다**. (기존 `None`-mode sprite 는 `_backgroundPropsRoot` 라 무영향.)

## 변경 대상

- `Assets/_Project/Scripts/Data/MapThemeData.cs` (구조물 프랍 필드)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (`_structurePropsRoot` + `InstantiateStructureProps` + Clear)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (배선, prop 블록 근처)
- authoring: KayKit 프리팹 → `PropData` 2종(goal/spawn) + forest 테마 지정

## 구현

- **`MapThemeData`**: `[Header("Structures")]` + `public PropData goalStructureProp;` `public PropData spawnStructureProp;`
- **`TilemapMapView`**:
  - `private Transform _structurePropsRoot;` + `Clear()` 에 dispose.
  - `public void InstantiateStructureProps(in GeneratedMap map, MapThemeData theme, BoardVisualPlan plan)` — 역회전 root 생성 → goal/spawn 셀마다 해당 `PropData`(prefab 있음) 를 인라인 `PropPlacement`(scale=`prop.visualScale`) 로 `InstantiateProp(...)` → 놓은 셀의 `overlayTilemap.SetTile(cell, null)` 로 마커 억제.
- **`BattleBridge`**: prop 블록 뒤에 `UseTilemapView && tilemapMapView != null && theme != null` 가드로 `tilemapMapView.InstantiateStructureProps(_generatedMap, theme, tilemapMapView.VisualPlan)` — `theme.tileProps` 가드와 독립.
- **authoring**: `Assets/KayKit/Packs/KayKit - Platformer Pack` 에서 goal(성/깃발류)·spawn(문/게이트류) 메쉬 프리팹 → `PropData`(prefab, footprint, visualScale; `billboardMode` 무의미) 작성, forest 테마에 지정.

## 완료 기준

- compile 통과 (`read_console` 클린).
- Play → 게임뷰: goal/spawn 에 3D 구조물 **똑바로 서고 접지**, `PH_Goal`/`PH_Spawn` 타일 사라짐. 배경 프랍(기존 None sprite 포함)·유닛 무영향. 스크린샷 육안.
- sim 불변: 적 goal 도달(FlowField/GoalReached 무변경), console 클린.

> 확인: 2026-07-02 Play 검증 PASS — StructureProps 3개(goal 1+spawn 2) worldRot=(0,0,0) 업라이트·정확한 셀·goal/spawn overlay 타일 null(억제)·콘솔 클린. 메쉬 = `platform_arrow_2x2x1`(red=spawn/yellow=goal), `visualScale=0.5`(2×2 메쉬 → 1 타일). authoring = `struct_spawn_arch`/`struct_goal_structure` PropData + forest 테마 배선.
