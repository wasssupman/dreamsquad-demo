# Movement Obstacle Trim

**작업 구분**: 8

## 목적

MovementSystem 에 cell trim 을 도입한다. desired 셀이 `blockedCells` 에 속하면 currentCell 경계 epsilon 안쪽으로 변위를 잘라낸다. flow 변위와 impulse 변위 모두 적용 (Q6=B 결정 — 큐브가 진짜 벽).

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- Add: `Assets/_Project/Scripts/Battle/Movement/ObstacleTrim.cs` — `ClampToBoundary` 순수 함수

## ClampToBoundary

```csharp
// epsilon = 1e-3f cells. 셀 경계에 정확히 박지 않고 안쪽으로 끌어옴 (부동소수 오차 방지).
public static float3 ClampToBoundary(
    float3 current,
    float3 desired,
    int2 currentCell,
    float tileSize,
    int2 gridSize)
{
    // currentCell 의 XZ AABB 계산:
    //   minX = currentCell.x * tileSize, maxX = minX + tileSize
    //   minZ = currentCell.y * tileSize, maxZ = minZ + tileSize
    // y 는 무시.
    //
    // desired - current 직선이 currentCell AABB 의 X 또는 Z 경계와 만나는 t 를 구해
    // 그 t 직전 (epsilon 만큼 안쪽) 위치까지만 이동.
    //
    // current 가 이미 셀 안에 있다고 가정 (호출자 보장).
    // current 와 desired 가 같은 셀이면 desired 그대로 반환.
}
```

- Burst 호환 순수 수학 (Mathematics 만 사용).
- y 좌표 보존.
- `desired` 가 `currentCell` 안이면 변경 없이 반환.

## MovementSystem 통합

기존 변위 적용 직전에:

```csharp
float3 desired = current + flowStep + impulseDisplacement;

int2 currentCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize);
int2 targetCell  = GridMath.WorldToCell(desired, field.tileSize, field.gridSize);

if (!currentCell.Equals(targetCell) && obstacleSingleton.blockedCells.Contains(targetCell))
{
    desired = ClampToBoundary(current, desired, currentCell, field.tileSize, field.gridSize);
}

transform.ValueRW.Position = desired;
```

`obstacleSingleton` 은 `SystemAPI.GetSingleton<ObstacleSingleton>()`. `blockedCells` read-only.

## 의도

- flow 와 impulse 합성 *후* 한 번만 trim → 두 변위가 모두 영향 받음.
- 같은 셀 내 미세 이동은 통과 (분기 첫 조건).
- 큐브 셀 안에 적이 이미 들어있는 상태 (디버그 spawn 으로 가능) 는 통과 허용 → 셀 경계 도달 시 정지.

## 단위 테스트 (EditMode)

- `ObstacleTrimTests`:
  - 직선이 X 경계 통과 → epsilon 안쪽 점 반환 (X 좌표만 잘림).
  - 직선이 Z 경계 통과 → epsilon 안쪽 점 반환 (Z 좌표만 잘림).
  - 직선이 코너 통과 (X, Z 둘 다 경계 침범) → 먼저 닿는 축 기준 잘림.
  - 같은 셀 내 이동 (current cell == target cell) → desired 그대로.
  - y 좌표 보존.

## 완료 기준

- 컴파일 + Burst 활성.
- 단위테스트 통과.
- 큐브 spawn 진입점 미존재 (다음 unit) 이므로 런타임 동작 변화 0.
- 콘솔 에러/경고 0.
