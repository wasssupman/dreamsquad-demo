# Obstacle Data and Lifetime System

**작업 구분**: 7

## 목적

큐브 obstacle 의 데이터 모델과 lifetime 시스템을 만든다. spawn API 와 Movement 통합은 다음 단위들 (8, 9) 에서.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/Obstacle.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/ObstacleSingleton.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/ObstacleLifetimeSystem.cs`
- Modify: Effects bootstrap (또는 BattleBridge bootstrap) — `ObstacleSingleton.blockedCells` 생성/해제

## Obstacle 컴포넌트

```csharp
public struct Obstacle : IComponentData
{
    public int2 cell;             // 점유 셀 (1×1)
    public float3 worldPosition;  // 시각/프레젠테이션용
    public float remainingLife;   // 초
}
```

`worldPosition` 은 시각 표시 용도. ECS 차단 판정은 `cell` 만 본다.

## ObstacleSingleton

```csharp
public struct ObstacleSingleton : IComponentData
{
    public NativeHashSet<int2> blockedCells;  // Allocator.Persistent
}
```

매 프레임 ObstacleLifetimeSystem 이 `Clear` 후 살아있는 obstacle 의 cell 을 다시 채운다.

## ObstacleLifetimeSystem

- Effects 맥락. `[BurstCompile]`, `[UpdateInGroup(typeof(SimulationSystemGroup))]`.
- MovementSystem **이전** 업데이트 (`UpdateBefore(typeof(MovementSystem))`) — Movement 가 현재 프레임의 갱신된 `blockedCells` 를 읽도록.
- 매 OnUpdate:
  1. `blockedCells.Clear()`.
  2. 모든 `Obstacle` entity 순회: `remainingLife -= dt`.
  3. `remainingLife <= 0` 이면 ECB destroy. 아니면 `blockedCells.Add(obstacle.cell)`.
  4. ECB playback / dispose.

= **매 프레임 재구축**. 큐브 수 N 작음 가정 (≤ 16 정도).

## 싱글턴 lifecycle

- `ObstacleSingleton.blockedCells = new NativeHashSet<int2>(64, Allocator.Persistent)` — World init.
- World 종료 시 dispose. 기존 패턴 따름.

## 단위 테스트 (EditMode)

- `ObstacleLifetimeTests`:
  - dt 후 `remainingLife` 감소 확인.
  - `remainingLife <= 0` entity destroy 확인.
  - `blockedCells` 가 살아남은 큐브의 cell 만 정확히 포함하는지 확인.
  - 큐브 0개 → `blockedCells` 빈 set.
  - 같은 cell 에 큐브 2개 (디버그) → set 에 1개만 (HashSet 중복 제거 정상 동작).

## 완료 기준

- 컴파일 + Burst 활성.
- 단위테스트 통과.
- 런타임 동작 변화 0 (큐브 spawn 진입점 미존재 — Unit 9 에서).
- `blockedCells` 정상 생성/해제 (Editor 종료 시 NativeHashSet 누수 경고 없음).
- 콘솔 에러/경고 0.

완료: 2026-04-28 — 커밋 TBD
