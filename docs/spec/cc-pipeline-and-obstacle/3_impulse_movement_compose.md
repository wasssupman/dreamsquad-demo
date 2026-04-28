# Impulse Movement Composition

**작업 구분**: 3

## 목적

MovementSystem 의 buffer 순회 switch 에 `CcKind.Impulse` 케이스를 추가한다. Producer 가 아직 없으므로 런타임 동작 변화 0. 단위 테스트로 합성 수학만 검증.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## 합성 모델

```csharp
float speedMul = 1f;
float3 impulseDisplacement = float3.zero;

if (ccBufferLookup.HasBuffer(entity))
{
    var ccBuffer = ccBufferLookup[entity];
    for (int i = 0; i < ccBuffer.Length; i++)
    {
        var cc = ccBuffer[i];
        switch (cc.kind)
        {
            case CcKind.Slow:    speedMul *= cc.scalar; break;
            case CcKind.Impulse: impulseDisplacement += cc.vector * dt; break;
        }
    }
}

float3 flowStep = new float3(stepDir.x, 0, stepDir.y) * follow.ValueRO.speed * speedMul * dt;
float3 desired = current + flowStep + impulseDisplacement;

transform.ValueRW.Position = desired;
```

## 합성 의도

- Slow 와 Impulse 는 수학적으로 독립: `speedMul` 은 `flowStep` 에만 곱, `impulseDisplacement` 는 별도 가산.
- 임펄스 도중에도 flow 는 계속 흐른다 → 임펄스 시간이 끝나면 자연스럽게 적이 다시 골을 향함.
- Slow + Impulse 가 동시에 걸리면 적은 천천히 이동 + 추가 변위가 더해진다.

## 기존 분기 보존

- Tornado pull 분기 (현재 73-83 행) 와 Portal 분기 (45-56 행) 는 그대로 유지.
- Tornado 안에 들어간 적은 임펄스 무시 (`continue` 로 빠져나감) — 기존 동작 보존. Tornado pull 이 우선.

## 단위 테스트 (EditMode)

- `MovementCompositionTests`:
  - Slow 만 적용 시 `desired = current + flowStep × multiplier`.
  - Impulse 만 적용 시 `desired = current + flowStep + vector × dt`.
  - Slow + Impulse 동시 시 `desired = current + flowStep × multiplier + vector × dt`.
  - 둘 다 없을 시 `desired = current + flowStep`.

## 완료 기준

- 컴파일 + Burst 활성 (`[BurstCompile]` 유지).
- 단위테스트 통과.
- 런타임 동작 변화 0 (Producer 미존재; CcEffect.Impulse 가 buffer 에 들어갈 일이 없음).
- 콘솔 에러/경고 0.

완료: 2026-04-28 — 커밋 TBD
