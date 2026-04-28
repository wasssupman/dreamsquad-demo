# DoT CcKind + DotApplySystem

**작업 구분**: 1

## 목적

`CcKind.DoT` 신규 enum 값 + `DotApplySystem` 신설. CC buffer 안 DoT entry 의 `scalar` 를 `damagePerSec` 로 해석하여 매 프레임 `IncomingDamage` 에 누적. 기존 HealthSystem 재사용. Producer 가 아직 없으므로 본 단위 commit 시점 동작 변화 0.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs` — `CcKind` enum 에 `DoT = 2` 추가, kind 별 슬롯 컨벤션 표 갱신
- Add: `Assets/_Project/Scripts/Battle/Effects/DotApplySystem.cs`

## CcKind 확장

```csharp
public enum CcKind : byte
{
    Slow = 0,
    Impulse = 1,
    DoT = 2,         // ← new. scalar = damage / second.
}
```

`CcEffect` 의 컨벤션 표에 DoT row 추가:

| kind | vector | scalar |
|---|---|---|
| Slow | (사용 안 함) | speed multiplier |
| Impulse | velocity | (사용 안 함) |
| DoT | (사용 안 함) | damage / sec |

기존 MovementSystem 의 switch 는 변경 없음 (DoT 는 Movement 무관). Slow/Impulse 케이스만 처리, default 는 무시.

## DotApplySystem

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CcApplySystem))]
[UpdateBefore(typeof(CcDecaySystem))]
public partial struct DotApplySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (ccBuffer, entity) in
                 SystemAPI.Query<DynamicBuffer<CcEffect>>().WithEntityAccess())
        {
            for (int i = 0; i < ccBuffer.Length; i++)
            {
                var cc = ccBuffer[i];
                if (cc.kind != CcKind.DoT) continue;
                ecb.AppendToBuffer<IncomingDamage>(entity, new IncomingDamage { amount = cc.scalar * dt });
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
```

= 매 프레임 buffer 의 모든 DoT entry → `damagePerSec * dt` 만큼 IncomingDamage 추가. AttackSystem 이 IncomingDamage 쓰는 패턴과 동일.

## 시스템 순서

`CcApplySystem (큐→buffer)` → `DotApplySystem (DoT → IncomingDamage)` → `MovementSystem (Slow/Impulse 합성)` → `CcDecaySystem (tick + remove)` → `HealthSystem (IncomingDamage 처리, 기존)`.

## 단위 테스트 (EditMode)

`DotApplySystemTests`:
- DoT entry 가 매 프레임 `scalar * dt` 만큼 IncomingDamage 에 추가 확인
- Slow/Impulse entry 는 IncomingDamage 영향 없음
- 같은 entity buffer 에 DoT 가 여러 개 있을 때 모두 합산
- 빈 buffer → IncomingDamage 영향 0

## 완료 기준

- 컴파일 + Burst 활성.
- 단위 테스트 통과.
- Producer 미존재 → 런타임 동작 변화 0 (DoT entry 가 buffer 에 들어갈 일 없음).
- 콘솔 에러/경고 0.
