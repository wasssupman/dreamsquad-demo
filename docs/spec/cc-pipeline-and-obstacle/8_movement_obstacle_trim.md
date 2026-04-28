# Movement Obstacle Trim

**작업 구분**: 8

## 목적

Unit 5b 에서 만든 cell trim 시스템 (`MovementCellTrim.cs` 의 `IsWallCell` + `ClampToBoundary`, MovementSystem trim 분기) 을 확장하여, 큐브 (`ObstacleSingleton.blockedCells`) 도 wall 로 취급하게 만든다. trim 알고리즘 자체는 변경 없음.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` (Unit 5b 에서 생성됨)
- Modify: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## IsWallCell 확장 — 옵션 택 1

### 옵션 A: `IsWallCell` 시그니처에 obstacle 추가

```csharp
public static bool IsWallCell(
    int2 cell,
    in FlowFieldSingleton field,
    in NativeHashSet<int2> blockedCells)
{
    if (OOB(cell, field)) return true;
    if (cell.Equals(field.goalCell)) return false;
    if (blockedCells.Contains(cell)) return true;     // ← 본 unit 추가
    return math.lengthsq(field.flow[idx]) < 1e-6f;
}
```

- 장점: trim 진입점 1곳 (호출자는 함수 하나만 보면 됨).
- 단점: FlowField 와 ObstacleSingleton 양쪽 의존이 함수에 들어감.

### 옵션 B: trim 분기 조건에 OR 추가

```csharp
bool wall = IsWallCell(targetCell, field) || obstacleSingleton.blockedCells.Contains(targetCell);
if (!currentCell.Equals(targetCell) && wall)
{
    desired = ClampToBoundary(current, desired, currentCell, field.tileSize);
}
```

- 장점: `IsWallCell` 시그니처 보존. obstacle 의존성 호출자에 격리.
- 단점: 다른 호출자가 생기면 같은 OR 를 또 추가해야 함.

본 unit 구현 시점에 선택. **추천: 옵션 B** (관심사 분리, 5b 시그니처 보존).

## MovementSystem 통합

옵션 B 채택 시: 5b 의 trim 분기 위에 한 줄 (`bool wall = ...`) 추가.

옵션 A 채택 시: `IsWallCell` 호출에 `obstacleSingleton.blockedCells` 인자 추가.

`obstacleSingleton` 은 `SystemAPI.GetSingleton<ObstacleSingleton>()` 으로 read-only 획득.

## 의도 + 엣지 케이스

- 5b 의 trim 의도 그대로 + 큐브 wall 추가.
- **goal 셀 == obstacle 셀 디버그 spawn**: goal 우선. `IsWallCell` 의 goal 분기가 먼저 false 반환 (옵션 A) 또는 trim 분기가 통과 허용 (옵션 B 에서도 동일하게 처리) — 구현 시 goal 분기를 obstacle 체크보다 먼저 두어 명시적 우선순위 확보.
- **큐브 셀 안에 적이 이미 들어있는 상태**: `currentCell == targetCell` 분기에서 통과 허용 → 셀 경계 도달 시 정지 (5b 와 동일 동작).
- **큐브 사라지는 프레임**: ObstacleLifetimeSystem (Unit 7) 이 MovementSystem 이전에 `blockedCells.Clear()` + 재구축 → 같은 프레임에 큐브 제거 반영. trim 분기 자동 해제.

## 단위 테스트 (EditMode)

- 5b 의 `MovementCellTrimTests` 에 케이스 추가:
  - `blockedCells` 에 cell 포함 → wall (trim 발동).
  - `blockedCells` 미포함 + 경로 셀 → 통과.
  - obstacle cell == goal cell → 통과 허용 (goal 우선).
  - obstacle cell == 영벡터 cell → wall (의도, 어쨌든 둘 다 wall).

## 완료 기준

- 컴파일 + Burst 활성.
- 단위테스트 통과.
- 큐브 spawn 진입점 미존재 (Unit 9) 이므로 런타임 동작 변화 0.
- 5b 의 path-wall 동작 회귀 없음.
- 콘솔 에러/경고 0.

완료: 2026-04-28 — 커밋 TBD
