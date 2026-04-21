# Path Carve Algorithm v1

**작업 구분**: Phase 10B

## 목적

Q-6 결정 반영: **각 spawn lane 을 branch node 로 보고, branch 가 shared trunk 를 통해 root(goal) 로 merge 되는 경로**를 만든다. Multi-line × single-goal 에서 lane 간 간격을 보장하고, 모든 spawn 이 하나의 goal 로 도달한다.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/PathCarver.cs`

## 알고리즘

1. `goal.y` 행에 merge root 를 둔다.
2. grid 우측 2/3 지점에 shared trunk x 좌표를 잡는다.
3. 각 spawn lane 마다 branch node `(trunkX, spawn.y)` 를 만든다.
4. branch node 의 y 간격은 최소 2 이상이어야 한다. 즉 lane 사이에 최소 1칸 이상의 빈 행이 존재한다.
5. `Straight` 경로는 `spawn -> branch node -> shared trunk -> root(goal)` 로 carving 한다.
6. `Free` 경로는 `spawn -> branch node` 구간에 randomized Manhattan walk 를 쓰고, 이후 shared trunk 로 merge 한다.
7. 모든 path cell 을 `MapTileType.Walk` 로 마킹한다.
8. 모든 spawn 완료 후 BFS 로 goal 에서 역방향 도달성 재검증 (9_multispawn_connectivity 재사용).

## 구현

```text
CarveAllSpawnsToGoal(rng, tiles, gridSize, spawns, goal, shape)
  if branch nodes cannot maintain y gap >= 2:
    return false

  trunkX = clamp(gridSize.x * 2 / 3, 1, gridSize.x - 2)
  mergeRoot = (trunkX, goal.y)

  carve line from mergeRoot to goal

  for each spawn:
    branchNode = (trunkX, spawn.y)

    if shape == Straight:
      carve horizontal/vertical straight segment from spawn to branchNode
    else:
      carve randomized Manhattan segment from spawn to branchNode

    carve vertical trunk segment from branchNode to mergeRoot

  return true
```

## 불변조건 (Codex H-4 대응)

1. ✅ 모든 path cell 이 그리드 경계 안 — `next.x >= 0 && < gridSize.x` 체크
2. ✅ path 가 4-neighbor 연속 — Manhattan walk 는 항상 dx=±1 또는 dy=±1
3. ✅ branch node 간격 최소 2 — spawn lane 사이 최소 1칸 이상 separation
4. ✅ 모든 branch 가 shared trunk 를 통해 root(goal) 로 merge
5. ✅ Walk 셀을 덮어쓰지 않음 — ObstaclePlacer 는 `Place` 셀만 수정 (task 14)
6. ✅ BFS 재검증 — ProceduralMapGenerator 가 `MapConnectivity.AllSpawnsReachGoal` 호출

## 완료 기준

- 컴파일 0 errors.
- EditMode 테스트:
  - 동일 seed → 동일 Walk 셀 집합
  - 간단한 20×20 맵에서 2 spawn + 1 goal carve 후 `AllSpawnsReachGoal` == true
  - Straight shape 에서 branch node 간격이 최소 2 이상
  - spawn lane count 가 grid height 에 맞게 clamp
- Free shape 은 seed 별 결정성과 seed 간 variation 유지.
- 최종 검증: EditMode 69/69 passed, Play smoke console error/warning 0.

## Subtask 분할 (OVERRUN 대응, 35분 예상)

- **12A** — `PathCarver.CarveAllSpawnsToGoal` + `CarveSingleSpawn` core loop
- **12B** — `DecideStepDir` (detour + Manhattan bias + random tie-break)
- **12C** — EditMode 테스트 (직선/장애물/연결성 유지/maxSteps 경계)
