# ObstacleLifetimeSystem Multi-Cell Extension

**작업 구분**: 4

## 목적

`ObstacleLifetimeSystem` 이 매 프레임 `ObstacleSingleton.blockedCells` 재구축 시 `BlockingHazardCellsBuffer` 의 모든 cell 을 add 하도록 확장. cc-pipeline 의 단일 cell (`Obstacle.cell`) 처리는 유지.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Effects/ObstacleLifetimeSystem.cs`
- Modify: `Assets/_Project/Scripts/Tests/EditMode/ObstacleLifetimeTests.cs` (멀티셀 테스트 추가)

## 구현

### 현재 (단일 cell, lifetime only)

```csharp
foreach (var (obstacle, entity) in
         SystemAPI.Query<RefRW<Obstacle>>().WithEntityAccess())
{
    obstacle.ValueRW.remainingLife -= dt;
    if (obstacle.ValueRO.remainingLife <= 0f)
        ecb.DestroyEntity(entity);
    else
        blockedCells.Add(obstacle.ValueRO.cell);
}
```

### 변경 후

```csharp
// 단일 cell obstacle (cc-pipeline 디버그 큐브) — 변경 없음
foreach (var (obstacle, entity) in
         SystemAPI.Query<RefRW<Obstacle>>()
                  .WithNone<BlockingHazardCellsBuffer>()      // ← 멀티셀 entity 제외
                  .WithEntityAccess())
{
    if (obstacle.ValueRO.remainingLife <= 0f)
        ecb.DestroyEntity(entity);
    else
    {
        blockedCells.Add(obstacle.ValueRO.cell);
        obstacle.ValueRW.remainingLife -= dt;
    }
}

// 멀티셀 blocking hazard — 본 unit 신설
foreach (var (cellsBuffer, entity) in
         SystemAPI.Query<DynamicBuffer<BlockingHazardCellsBuffer>>()
                  .WithAll<BlockingHazard>()
                  .WithNone<DeadTag>()                        // ← HP 0 hazard 제외
                  .WithEntityAccess())
{
    for (int i = 0; i < cellsBuffer.Length; i++)
        blockedCells.Add(cellsBuffer[i].cell);
}
```

### 핵심 결정

- **단일/멀티 cell entity 분리 query** — `WithNone<BlockingHazardCellsBuffer>` 로 cc-pipeline 큐브 (Obstacle only) 와 hazard (Obstacle + BlockingHazardCellsBuffer) 를 구분. `Obstacle.remainingLife` 는 hazard 에서 미사용이므로 두 loop 분리가 자연.
- **`WithNone<DeadTag>`** — DamageApplicationSystem 이 같은 프레임에 DeadTag 추가 가능. 이번 프레임 lifecycle 이 destroy 하기 전에 ObstacleLifetimeSystem 이 다시 돌면 dead hazard 의 cell 이 blockedCells 에 들어감 → 1 프레임 잔존 (README "1-frame blockedCells 잔존" 의도). DeadTag 필터로 같은 프레임 추가 방지하지만 ObstacleLifetimeSystem 이 DamageApplicationSystem 보다 *먼저* 실행되므로 dead 검출 못 할 수도 있음 — 시스템 순서 점검 필요.

### 시스템 순서 검증

현재 순서 (CC pipeline + path-zone-hazards 기준):
1. `ObstacleLifetimeSystem` (UpdateBefore MovementSystem) — 본 프레임 시작 시 blockedCells 재구축
2. `MovementSystem`
3. `AttackSystem` (UpdateAfter MovementSystem)
4. `DamageApplicationSystem` (UpdateAfter AttackSystem) — DeadTag 추가
5. `UnitLifecycleSystem` (UpdateAfter DamageApplicationSystem) — entity destroy

→ ObstacleLifetimeSystem 은 DamageApplicationSystem 보다 **먼저** 실행. 이번 프레임 추가된 DeadTag 는 **다음** 프레임 ObstacleLifetimeSystem 이 처리. 즉:
- 프레임 N: 적 공격 → HP=0 → DeadTag → entity destroy. blockedCells 는 이번 프레임 시작 시 이미 재구축됨 (dead cell 포함).
- 프레임 N+1: ObstacleLifetimeSystem 이 destroy 된 entity 무시 (entity gone) → blockedCells 에서 cell 제외.

→ **1-frame 잔존** = 프레임 N 의 Movement 가 dead cell 을 blocked 로 인식. 16ms — 의도 (README 명시).

## 단위 테스트 (EditMode)

`ObstacleLifetimeTests` 추가:
- 멀티셀 hazard (3 cell) → blockedCells 가 정확히 3 cell 포함.
- 멀티셀 hazard 2개 같은 cell 중첩 → set 중복 제거 (HashSet).
- 단일 obstacle + 멀티셀 hazard 공존 → 둘 다 blockedCells 합집합.
- DeadTag 부착된 hazard → 같은 프레임 blockedCells 에서 제외 (`WithNone<DeadTag>` 필터 검증).

## 완료 기준

- 컴파일 + Burst 활성.
- EditMode 멀티셀 테스트 통과 + 기존 회귀 0.
- 동작 변화: cc-pipeline 디버그 큐브 동작 동일 (회귀 검증).
- LocalTransform writer 단독 = MovementSystem (불변).
- 콘솔 에러/경고 0.

검증: 2026-04-29 — `ObstacleLifetimeTests` 멀티셀 케이스 추가, 관련 9/9 통과, 전체 EditMode 149/149 통과, 콘솔 에러/경고 0. 커밋 미작성.
