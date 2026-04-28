# Combat Knockback Hook

**작업 구분**: 5

## 목적

CombatSystem 에서 적이 데미지를 받는 시점에, 디펜더 SO `knockbackDistance > 0` 이면 `EnemyCcEvent` 를 enqueue 한다.

## 변경 대상

- Modify: 데미지 적용을 담당하는 시스템 (`Assets/_Project/Scripts/Battle/Combat/` 내. 실제 파일명/라인은 구현 시 식별. `IncomingDamage` buffer 처리 또는 attack hit 처리 코드)

## 트리거 위치

- 데미지 적용 직후 (`Health -= damage` 가 일어나는 지점).
- 이미 적이 어떤 디펜더의 어떤 공격에 맞았는지 알 수 있는 지점이어야 한다.
- 현재 코드에서 attacker entity 정보를 들고 있는 buffer/event (`IncomingDamage` 또는 `DefenderAttackEvents`) 를 그대로 활용.
- attacker 의 SO 데이터는 attacker entity 의 ECS 미러 컴포넌트 (`DefenderRuntimeData`) 에서 읽는다 (Unit 4 에서 미러된 필드).

## Enqueue 로직

```csharp
if (defenderData.knockbackDistance > 0f && defenderData.knockbackDuration > 0f)
{
    float3 direction = math.normalizesafe(enemyPos - defenderPos);
    direction.y = 0f;
    float speed = defenderData.knockbackDistance / defenderData.knockbackDuration;
    float3 velocity = direction * speed;

    queue.Enqueue(new EnemyCcEvent
    {
        target = enemyEntity,
        effect = new CcEffect
        {
            kind = CcKind.Impulse,
            vector = velocity,
            scalar = 0f,
            remainingTime = defenderData.knockbackDuration,
        },
    });
}
```

`queue` 는 `EnemyCcEventsSingleton.queue`. `NativeQueue.AsParallelWriter` 가 필요하면 시스템 구조에 맞게 변환.

## 정책

- 같은 적이 같은 프레임에 여러 디펜더에게 맞으면 multiple events enqueue → CcApplySystem merge 정책 (max remaining + 새 vector) 적용. 결과적으로 마지막 처리된 hit 의 방향 + 가장 긴 duration 채택.
- 적 사망 (`Health <= 0`) 직전에 hit 가 들어와도 enqueue 정상. 다음 프레임 사망 처리에서 buffer 통째 제거.
- 거리 0 (defenderPos == enemyPos) 케이스: `normalizesafe` 가 0 벡터 반환 → 임펄스 사실상 무효. enqueue 는 발생하나 시각 변화 없음.

## 검증 (PlayMode)

- 샘플 디펜더 SO 1개에 `knockbackDistance = 2`, `knockbackDuration = 0.2` 설정.
- 적 1마리 spawn, 디펜더 사거리 안에 위치.
- 디펜더가 공격 → 적이 디펜더 반대 방향으로 0.2초간 변위 → 그 후 flow 따라 다시 전진.
- 다른 디펜더 (knockbackDistance = 0) 는 종전과 동일 동작.

## 완료 기준

- 컴파일 + Burst 활성.
- PlayMode 검증 사용자 확인 통과.
- 다른 디펜더 SO 회귀 없음.
- Slow / Tornado / Portal 동시 적용 시에도 정상 (수학 합성).
- 콘솔 에러/경고 0.

완료: 2026-04-28 — 커밋 TBD (PlayMode 확인 대기)
