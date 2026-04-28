# On-place Push Hook

**작업 구분**: 6

## 목적

디펜더 배치 (drop 성공 + activation 시퀀스) 시점에, SO `onPlacePushDistance > 0` 이면 디펜더 중심 `onPlacePushRadius` 안 적들에 방사형 push impulse 를 enqueue 한다.

## 변경 대상

- Modify: 디펜더 배치 파이프라인 — `Assets/_Project/Scripts/Bridge/BattleBridge.cs` 의 `ActivateDeployedDefender` 또는 `defender-on-place-skills` spec 이 구축한 on-place skill 디스패치 진입점.
- (선택) Add: `Assets/_Project/Scripts/Battle/Effects/OnPlacePushHelper.cs` — 한 곳 응집을 위한 helper.

## 트리거 위치

- 기존 on-place skill 발동 분기와 *동일한 시점* (Drop 성공 + presentation 후 activation).
- 기존 on-place skill (SlowPulse, BoostNearbyDefenders 등) 과 *별개로* SO 5필드만 채워져 있으면 발동.
- 1회 발동 후 재발동 없음 (`PendingDeployment` 제거 직전 1회만).

## Enqueue 로직

```csharp
if (defenderData.onPlacePushDistance > 0f && defenderData.onPlacePushDuration > 0f)
{
    float radiusSq = defenderData.onPlacePushRadius * defenderData.onPlacePushRadius;
    float speed = defenderData.onPlacePushDistance / defenderData.onPlacePushDuration;

    // attackers 쿼리 (적 entity).
    foreach (var (enemyXform, enemyEntity) in
             SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PathFollowState>().WithEntityAccess())
    {
        float3 toEnemy = enemyXform.ValueRO.Position - defenderPos;
        toEnemy.y = 0f;
        if (math.lengthsq(toEnemy) > radiusSq) continue;

        float3 dir = math.normalizesafe(toEnemy);
        queue.Enqueue(new EnemyCcEvent
        {
            target = enemyEntity,
            effect = new CcEffect
            {
                kind = CcKind.Impulse,
                vector = dir * speed,
                scalar = 0f,
                remainingTime = defenderData.onPlacePushDuration,
            },
        });
    }
}
```

EntityManager 직접 접근이 가능한 MonoBehaviour 측 호출자 (BattleBridge) 라면 `EffectSpawner.ApplyCc` 즉시 적용 경로도 가능. 둘 중 일관된 한 가지 선택.

## 정책

- 적 0마리여도 정상.
- 같은 적이 knockback (Unit 5) 을 동시에 받으면 CcApplySystem merge → 마지막 enqueue 방향 우선.
- `onPlacePushRadius == 0` 이면 작용 0 (조건 분기 통과 못함).
- 기존 on-place skill (SlowPulse 등) 과 동시 동작 가능 (서로 독립).

## 검증 (PlayMode)

- 샘플 디펜더 SO 1개에 `onPlacePushDistance = 2`, `onPlacePushDuration = 0.2`, `onPlacePushRadius = 3`.
- 적 3마리를 디펜더 예정 셀 주변에 spawn.
- 디펜더 배치 → 3마리 모두 디펜더 반대 방향으로 변위.
- 다른 디펜더 (onPlacePushDistance = 0) 는 종전과 동일 동작.

## 완료 기준

- 컴파일.
- PlayMode 검증 사용자 확인 통과.
- 기존 on-place skill (SlowPulse, BoostNearbyDefenders 등) 회귀 없음.
- knockback (Unit 5) 와 동시 적용 시 merge 정책 정상.
- 콘솔 에러/경고 0.
