# Claude Handoff Summary

**작업 구분**: Phase 10 completion handoff
**상태**: 완료
**작성일**: 2026-04-21

## 한 줄 요약

Phase 10 map-system 은 완료 상태다. Phase 9 flow field 위에 `GeneratedMap` runtime 모델, seed procedural map generation, map settings UI, multi-spawn branch/trunk/root path, forest obstacle theme, goal marker 가 통합됐다.

## 완료된 기능

- `MapTileType` 4종: `Walk`, `Place`, `Env`, `Deco`
- `GeneratedMap` runtime struct + `Dispose`
- `BattleMapBuilder` fixture/manual/fallback 생성 경로
- `ProceduralMapGenerator.Generate(...)`
- `PathCarver` branch/trunk/root 알고리즘
- `MapConnectivity.AllSpawnsReachGoal`
- `ObstaclePlacer` 단일 셀 obstacle 배치
- `MapThemeData` + forest theme asset/prefab/material
- `AttackDeck.SpawnEntry.spawnIndex` migration
- `BattleBridge` map orchestration owner
- `MapView.Initialize(GeneratedMap, tileSize)` + 4타입 tile material + goal marker + obstacles
- `PlacementInput.Initialize(GeneratedMap, tileSize)` + Place-only placement
- Flow field walk mask = Walk-only
- Timeline briefing map settings panel:
  - path type: Straight / Free
  - map size: width / height
  - obstacle density: Low / Mid / High
  - spawn lane count
- Spawn lane count 가 timeline preview 와 runtime spawn 에 반영

## 최신 알고리즘 결정

Spawn lane 은 단순 병렬 path 가 아니라 branch graph 로 본다.

- 각 lane 은 분리된 branch node 를 가진다.
- branch node 간 y 간격은 최소 2 이상이다.
- 따라서 lane 사이에는 최소 1칸 이상의 빈 행이 있다.
- 각 branch 는 shared trunk 에 merge 된다.
- shared trunk 는 root 인 goal 로 연결된다.
- 기본 `20x10` 에서 가능한 최대 lane 은 5개다.
- 입력 lane 수가 높이상 불가능하면 `MapGenerationOptions.Normalized()` 가 clamp 한다.

## 검증 결과

- Unity compile: 0 errors
- EditMode tests: 69/69 passed
- Play smoke:
  - `pathShape=Straight`
  - `gridSize=20x10`
  - `spawnLaneCount=5`
  - `spawns=5` 로그 확인
  - console error/warning 0
- `git diff --check` 관련 파일 통과

## 주요 파일

- `Assets/_Project/Scripts/Data/MapTileType.cs`
- `Assets/_Project/Scripts/Data/MapGenerationSettings.cs`
- `Assets/_Project/Scripts/Data/MapGenerationOptions.cs`
- `Assets/_Project/Scripts/Data/GeneratedMap.cs`
- `Assets/_Project/Scripts/Data/BattleMapBuilder.cs`
- `Assets/_Project/Scripts/Data/ProceduralMapGenerator.cs`
- `Assets/_Project/Scripts/Data/PathCarver.cs`
- `Assets/_Project/Scripts/Data/MapConnectivity.cs`
- `Assets/_Project/Scripts/Data/ObstaclePlacer.cs`
- `Assets/_Project/Scripts/Data/MapThemeData.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Core/MapView.cs`
- `Assets/_Project/Scripts/Core/PlacementInput.cs`
- `Assets/_Project/Scripts/UI/TimelineBriefingView.cs`
- `Assets/_Project/Scripts/Core/DraftController.cs`

## 테스트 파일

- `Assets/_Project/Tests/EditMode/MapTileTypeTests.cs`
- `Assets/_Project/Tests/EditMode/GeneratedMapTests.cs`
- `Assets/_Project/Tests/EditMode/BattleMapBuilderTests.cs`
- `Assets/_Project/Tests/EditMode/MapConnectivityTests.cs`
- `Assets/_Project/Tests/EditMode/ManualMapInputTests.cs`
- `Assets/_Project/Tests/EditMode/ProceduralMapGeneratorTests.cs`
- `Assets/_Project/Tests/EditMode/PathCarverTests.cs`
- `Assets/_Project/Tests/EditMode/ObstaclePlacerTests.cs`
- `Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs`

## 주의점

- 작업 트리는 다른 Phase 작업과 함께 많이 dirty 하다. unrelated changes 를 revert 하지 말 것.
- `Assets/PixPlays/`, Spine, VFX, defender drag/drop, on-place skill 변경이 같은 worktree 에 섞여 있다.
- map-system 관련 수정만 이어받을 때도 `BattleBridge`, `TimelineBriefingView`, `DraftController` 는 다른 기능 변경과 겹친다.
- `ManualMapInput` 은 data shape 만 완료. 실제 맵툴 UI/직렬화는 Phase 11+ 범위다.
- `Env` 타일은 타입만 존재한다. 환경 효과 동작은 Phase 11+ 범위다.
- multi-cell obstacle, multi-goal, theme 확장 자동화도 Phase 11+ 범위다.

## 다음 후보

- Phase 11 범위 결정
- Env tile 효과 설계
- 맵툴 실제 authoring UI
- theme obstacle footprint 확장
- generated map seed/version 을 플레이 로그에서 QA 재현 플로우로 연결
