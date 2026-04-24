# 8. Placer → BoardVisualPlan 전환

## 목적

`BackgroundPropPlacer` 를 `BoardVisualPlan` 단일 입력 구조로 전환한다. `GeneratedMap` 을 더 이상 받지 않는다. spawn/goal 은 `plan.goal`, `plan.spawns` 로 읽는다.

본 단계에서 **알고리즘은 바꾸지 않는다**. Poisson/cluster/jitter 는 `13` 범위. 여기서는 입력 경로 교체만.

## 전제

- `7` (Deco resolution) 완료: Deco folding 정책 (B) 로 고정.
- `BoardVisualPlan` 에 `goal: int2`, `spawns: int2[]` 가 출력으로 노출되어야 함. 본 단계에서 필드 추가 + builder 수정 같이 수행.

## 변경 대상

- `Assets/_Project/Scripts/Data/BoardVisualPlan.cs` (`goal`, `spawns` 필드 추가)
- `Assets/_Project/Scripts/Data/BoardVisualPlanBuilder.cs` (`Build` 에서 `map.goal`, `map.spawns` 복사)
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs`
- `Assets/_Project/Scripts/Core/MapView.cs` (호출부)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (호출부)
- `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs`
- `Assets/_Project/Tests/EditMode/BoardVisualPlanBuilderTests.cs` (goal/spawns assert 추가)

## 구현 가이드

1. `BoardVisualPlan` 에 필드 추가:
   - `public readonly int2 goal`
   - `public readonly int2[] spawns` (배열 복사. NativeArray 는 lifecycle 복잡도 추가)
2. `BoardVisualPlanBuilder.Build` 에서:
   ```
   goal = map.goal;
   spawns = new int2[map.spawns.Length];
   for (int i=0; i<spawns.Length; i++) spawns[i] = map.spawns[i];
   ```
3. `BackgroundPropPlacer.Generate` 시그니처:
   ```
   Generate(BoardVisualPlan plan, MapThemeData theme, int seed) -> List<PropPlacement>
   ```
   `GeneratedMap` 매개변수 제거.
4. 내부 `FloodFillRegion` 제거. `plan.Regions` 순회.
5. 판정 치환:
   - `IsBackgroundTile(map.TileAt)` → `plan.CellAt(cell).zoneType == Env`
   - `IsNearSpawnOrGoal(map, candidate, radius)` → `plan.goal`, `plan.spawns` 로 계산
   - 경로 인접 → `plan.CellAt(cell).pathProximity`
   - 외곽 인접 → `plan.CellAt(cell).borderProximity`
6. occupancy 배열은 `plan.gridSize` 기준.
7. `MapView` 호출부:
   ```
   var plan = BoardVisualPlanBuilder.Build(map, visualSeed);
   var placements = BackgroundPropPlacer.Generate(plan, theme, seed);
   mapView.InstantiateBackgroundProps(plan, theme, placements);
   ```
8. 테스트:
   - `CreateMap` → `CreateMapAndPlan` 헬퍼
   - deterministic: 동일 plan × 2 회 → placement 비트 동일
   - "placer 가 `Walk` / `Place` 셀 건드리지 않는다" plan 기반 assert
   - goal/spawns 회피 기존 테스트 유지

## 완료 기준

- `BackgroundPropPlacer` 에서 `map.TileAt`, `MapTileType`, `map.spawns`, `map.goal` grep 0.
- `FloodFillRegion` 제거.
- 동일 seed 에서 기존 vs 새 구현의 placement 결과 **동등** (알고리즘 불변, 입력만 교체).
- `BoardVisualPlan` 에 goal/spawns 노출, builder 테스트로 확인.
- `BackgroundPropPlacerTests` 전원 통과.

## 주의

- Poisson / cluster / jitter 는 `13` 범위. 여기서는 점수식 변경 없음.
- `int2[]` 복사 방식이면 plan lifetime 과 GC 책임 분리. NativeArray 로 가면 plan Dispose 경로 필요 — 쉬운 쪽(배열) 권장.
- Plan 필드 추가로 기존 consumer 가 깨지지 않는지 confirm (특히 `MapView` 외 참조자).

확인 일자: 2026-04-24 / 커밋 해시: a608388
