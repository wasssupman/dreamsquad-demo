# CC Data Model

**작업 구분**: 0

## 목적

CC (Crowd Control) 패밀리의 통일된 데이터 타입을 정의한다. 시스템과 Producer 는 다음 작업 단위에서 만든다. 본 단위는 컴파일만 통과하면 끝난다.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/EnemyCcEvents.cs`

## CcKind enum

```csharp
public enum CcKind : byte
{
    Slow = 0,        // multiplier-form: scalar = speed multiplier (0..1)
    Impulse = 1,     // displacement-form: vector = velocity (units/sec)
}
```

미래 확장 (본 spec 범위 밖): `Stun`, `Root`, `Reverse`, `Pull`, `Push`. enum 값과 switch case 만 추가하면 된다.

## CcEffect (IBufferElementData)

```csharp
public struct CcEffect : IBufferElementData
{
    public CcKind kind;
    public float3 vector;       // displacement-form 이 채움 (단위: world units / sec, velocity)
    public float scalar;        // multiplier-form 이 채움
    public float remainingTime; // 초
}
```

### kind 별 슬롯 사용 컨벤션

| kind | vector 사용 | scalar 사용 |
|---|---|---|
| Slow | (사용 안 함) | speed multiplier (`0` ≤ x ≤ `1`) |
| Impulse | velocity (방향 × 크기, units/sec) | (사용 안 함) |

`vector` 와 `scalar` 는 union 슬롯이지만 kind 별로 어느 쪽을 쓰는지 본 표가 source of truth. 다른 kind 추가 시 본 표를 갱신한다.

## EnemyCcEvents 싱글턴

```csharp
public struct EnemyCcEvent
{
    public Entity target;
    public CcEffect effect;
}

public struct EnemyCcEventsSingleton : IComponentData
{
    public NativeQueue<EnemyCcEvent> queue;
}
```

기존 `DefenderAttackEventsSingleton` / `MeteorBurstEventsSingleton` 패턴과 동일. NativeQueue 의 lifecycle (Allocator.Persistent, World 종료 시 dispose) 은 다음 작업 단위에서 정의한다.

## 완료 기준

- 두 파일 컴파일 성공.
- Burst 에서 IBufferElementData / IComponentData 사용 가능 확인 (해당 어셈블리 burst-compile).
- 콘솔 에러/경고 0.
- `CcKind` 와 `CcEffect.kind / vector / scalar / remainingTime` 식별자가 grep 으로 발견됨.

완료: 2026-04-28 — 커밋 해시 TBD
