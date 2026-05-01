# Path Wall Trim (Patch)

**작업 구분**: 5b (Unit 5 PlayMode 결함 후속 patch)

## 배경

Unit 5 PlayMode 검증에서 임펄스가 적을 *경로 바깥* 셀로 밀어내는 시각 결함 발견. 원본 spec 의 Unit 8 (cell trim) 은 큐브 셀만 차단 대상으로 잡고 있어 이 결함을 막지 못한다. 본 unit 은 *경로 외 셀 = 벽* 이라는 상위 개념을 trim 시스템에 도입하고, Unit 8 (큐브 trim) 이 본 unit 의 인프라를 확장 사용하도록 한다.

## 목적

MovementSystem 의 변위 적용 직전에 cell trim 을 도입하여 경로 바깥 셀 진입을 막는다. FlowField walkability (영벡터 = 도달 불가 = 벽) 를 source of truth 로 사용한다. flow 변위와 impulse 변위 모두 trim 통과.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — `IsWallCell` + `ClampToBoundary` 순수 함수
- Modify: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## IsWallCell

```csharp
public static bool IsWallCell(int2 cell, in FlowFieldSingleton field)
{
    // OOB = wall
    if (cell.x < 0 || cell.y < 0 || cell.x >= field.gridSize.x || cell.y >= field.gridSize.y)
        return true;
    // goal 셀은 통과 허용 (영벡터지만 wall 아님)
    if (cell.x == field.goalCell.x && cell.y == field.goalCell.y) return false;
    int idx = GridMath.CellIndex(cell, field.gridSize);
    return math.lengthsq(field.flow[idx]) < 1e-6f;
}
```

본 unit 의 시그니처는 Unit 8 가 obstacle 분기를 추가할 수 있도록 일반화 가능 형태로 남긴다 (Unit 8 spec 의 옵션 A/B 참조).

## ClampToBoundary

```csharp
// epsilon = 1e-3f cells. 셀 경계에 정확히 박지 않고 안쪽으로 끌어옴 (부동소수 오차 방지).
public static float3 ClampToBoundary(
    float3 current,
    float3 desired,
    int2 currentCell,
    float tileSize)
{
    // currentCell 의 XZ AABB:
    //   minX = currentCell.x * tileSize, maxX = minX + tileSize
    //   minZ = currentCell.y * tileSize, maxZ = minZ + tileSize
    // desired - current 직선이 X 또는 Z 경계와 만나는 t 중 작은 값을 구해
    // (1 - epsilon/dist) 위치까지만 이동.
    // current 가 이미 셀 안에 있다고 가정 (호출자 보장). y 좌표 보존.
    // current 와 desired 가 같은 셀이면 desired 그대로 반환.
}
```

- Burst 호환 순수 수학 (Mathematics 만 사용).
- y 좌표 보존.

## MovementSystem 통합

기존 변위 적용 직전:

```csharp
float3 desired = current + flowStep + impulseDisplacement;

int2 currentCell = GridMath.WorldToCell(current, field.tileSize, field.gridSize);
int2 targetCell  = GridMath.WorldToCell(desired, field.tileSize, field.gridSize);

if (!currentCell.Equals(targetCell) && IsWallCell(targetCell, field))
{
    desired = ClampToBoundary(current, desired, currentCell, field.tileSize);
}

transform.ValueRW.Position = desired;
```

기존 Tornado pull (73-83 행) / Portal (45-56 행) 분기는 변경 없음. trim 은 마지막 변위 적용 직전에 한 번만 적용.

## 의도

- flow + impulse 합성 *후* 한 번만 trim → 둘 다 영향 받음.
- 같은 셀 내 미세 이동은 통과.
- goal 셀 진입 통과 (PastGoalTag 부여 로직과 충돌 없음).
- OOB / 영벡터 셀 모두 trim → 적이 그리드 또는 경로 밖으로 절대 못 나감.

## 단위 테스트 (EditMode)

- `MovementCellTrimTests`:
  - `IsWallCell`: OOB cell, goal cell, 영벡터 cell, 비영벡터 cell 4 케이스.
  - `ClampToBoundary`:
    - X 경계 통과 → epsilon 안쪽 점 (X 좌표만 잘림).
    - Z 경계 통과 → epsilon 안쪽 점 (Z 좌표만 잘림).
    - 코너 (X+Z) 통과 → 먼저 닿는 축 기준 잘림.
    - 같은 셀 내 이동 (current cell == target cell) → desired 그대로.
    - y 좌표 보존.

## 검증 (PlayMode)

- 디펜더 (`knockbackDistance > 0`) 를 경로 가장자리에 배치, 적 1마리 → 공격 → 적이 경로 *바깥으로 튀어나가지 않음*. 경로 가장자리 셀에서 멈추고 다음 프레임 flow 따라 다시 진행.
- 일반 flow 이동 회귀 없음 (적 도달 시간 변화 < 5%).
- Slow / Tornado / Portal 동시 동작 회귀 없음.
- Unit 6 의 on-place push 도 같은 trim 인프라를 통과 → 배치 push 가 적을 경로 밖으로 밀어내지 않음 (회귀 검증).

## Unit 8 와의 관계

- 본 unit 에서 만든 `MovementCellTrim.cs`, `IsWallCell`, `ClampToBoundary`, MovementSystem trim 분기는 Unit 8 가 그대로 재사용.
- Unit 8 는 obstacle 분기를 추가만 (트림 인프라 자체는 본 unit 에서 완성).

## 완료 기준

- 컴파일 + Burst 활성.
- EditMode 테스트 통과.
- PlayMode 시각 검증: 경로 안에 갇힘 + 일반 흐름 회귀 없음 (사용자 manual 확인).
- Unit 6 의 on-place push 에도 trim 적용되어 회귀 없음.
- 콘솔 에러/경고 0.
- `MovementCellTrim.cs` 의 함수 시그니처가 Unit 8 에서 깨지지 않는 형태로 남음.
