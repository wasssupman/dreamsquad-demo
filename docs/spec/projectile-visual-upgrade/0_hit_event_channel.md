# Hit Event Channel

**작업 구분**: 0

## 목적

`ProjectileHitSystem` 이 임팩트 시점에 Presentation 계층으로 일회성 신호를 흘릴 수 있는 NativeQueue 채널을 신설한다. 데미지 적용은 그대로 ECS 안에서 끝나고, 본 채널은 hit VFX prefab 재생만 트리거한다.

## 변경 대상

- New: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitEvent.cs`
- New: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitEventsSingleton.cs`
- Modify: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (싱글턴 lifecycle 만 — 큐 생성/Drain stub)

## 채널 계약

```csharp
public struct ProjectileHitEvent
{
    public float3 position;   // 임팩트 좌표 (target snapshot)
    public int dataIndex;     // ProjectileData lookup index (BattleBridge 캐시와 동일)
}

public struct ProjectileHitEventsSingleton : IComponentData
{
    public NativeQueue<ProjectileHitEvent>.ParallelWriter writer;
}
```

큐 본체는 `BattleBridge` 가 소유 (`NativeQueue<ProjectileHitEvent> _projectileHitEventQueue`). 기존 `_goalEventQueue`, `_defenderAttackEventQueue` 패턴을 그대로 따름.

## 구현

- `ProjectileHitEventsSingleton` 은 `BattleBridge.InitializeBattleSingletons()` 시점에 큐 생성 후 등록. `OnDestroy` 에서 Dispose.
- `ProjectileHitSystem.OnUpdate` 에서 hit 판정 직후 (데미지 enqueue 와 같은 자리에서) `singleton.writer.Enqueue(new ProjectileHitEvent { position = targetPos, dataIndex = state.dataIndex })`.
  - dataIndex 는 후속 task(2) 에서 `ProjectileState`/`ProjectileSpawnRequest` 의 `assetIndex → dataIndex` 리네이밍과 함께 들어온다. 이번 task 에서는 큐 enqueue 만 stub 으로 두고, 실제 dataIndex 인입은 task 2 이후 활성. (또는 `ProjectileState` 에 `int dataIndex` 필드를 task 0 에서 미리 추가해도 됨 — compile-safe.)
- BattleBridge 에는 `DrainProjectileHitEvents()` 빈 구현만 추가 (`while (_projectileHitEventQueue.TryDequeue(out _)) {}`). 실제 prefab 재생은 task 3 에서 채움.

## 완료 기준

- compile: 새 파일 + ProjectileHitSystem + BattleBridge 변경이 에러 없이 통과.
- 큐 lifecycle: `InitializeBattleSingletons` 에서 생성, `OnDestroy` 에서 Dispose 확인 (Editor reload 시 leak 경고 없음).
- ProjectileHitSystem 이 데미지 적용 경로마다 enqueue 호출 (Splash 직접 타겟에 1회 — splash 보조 타겟은 enqueue 하지 않음, 시각은 직접 타겟 1점).
- BattleScene Play smoke: 기존과 동일하게 동작 (큐는 enqueue/dequeue 만 하고 prefab 재생 없음).
- read_console Error/Warning 0.

확인 일자: 2026-04-28 — Unity 컴파일 에러/경고 0, BattleScene Play smoke 회귀 없음 (큐 enqueue/dequeue 만 동작, 게임 흐름 변화 없음). 커밋: 191fbbb
