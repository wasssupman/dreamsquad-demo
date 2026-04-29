# Hazard Data Model

**작업 구분**: 0

## 목적

Hazard 의 통일 데이터 타입 정의. 시스템과 Producer 는 다음 작업 단위에서. 본 단위는 컴파일만 통과.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/HazardShape.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/HazardEffect.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/Hazard.cs`
- Add: `Assets/_Project/Scripts/Data/HazardSO.cs`

## HazardShape enum

```csharp
public enum HazardShape : byte
{
    SingleCell = 0,   // 1×1
    Square3x3 = 1,    // 3×3 정사각 (radius 무시)
    RadiusSquare = 2, // origin 중심 Chebyshev radius 정사각. radius=2 → 5×5
}
```

**naming note**: `RadiusSquare` 는 Chebyshev distance ≤ radius 인 셀 집합 (정사각). 진짜 원형 (Manhattan/Euclidean) 은 후속 후보.

## HazardEffect struct

```csharp
[System.Serializable]
public struct HazardEffect
{
    public CcKind kind;        // 본 spec CC 채널 재사용 (Slow / DoT / 향후 확장)
    public float param1;       // kind 별 컨벤션 (아래 표)
    public float param2;       // **현재 미사용**. 미래 Impulse 방향/축 정책 또는 다파라미터 CC 도입 시 활용 예약
    public float restDuration; // 매 프레임 enqueue 시 잔존시간 (예: 0.2s)
}
```

### kind 별 슬롯 컨벤션

| kind | param1 | param2 |
|---|---|---|
| Slow | speed multiplier | (미사용) |
| DoT | damage / sec | (미사용) |
| Impulse | speed | (미래: 방향 정책 결정 시 활용) |

ZoneApplySystem 이 매 프레임 `HazardEffect` → `CcEffect` 변환:
- Slow → `CcEffect{kind=Slow, scalar=param1, remainingTime=restDuration}`
- DoT → `CcEffect{kind=DoT, scalar=param1, remainingTime=restDuration}`

## ECS 컴포넌트 (b-1 layout — entity 당 multi-cell)

```csharp
public struct Hazard : IComponentData
{
    public float remainingLife;   // 초
}

[InternalBufferCapacity(2)]
public struct HazardEffectsBuffer : IBufferElementData
{
    public HazardEffect effect;
}

[InternalBufferCapacity(9)]  // 3×3 = 9 default
public struct HazardCellsBuffer : IBufferElementData
{
    public int2 cell;
}
```

= 한 hazard spawn = entity 1개 + 두 buffer (effects, cells). lifetime 1번 관리. effect 개수는 `EffectsBuffer.Length` 직접 사용 (별도 mirror 필드 없음).

## HazardSO ScriptableObject

```csharp
[CreateAssetMenu(menuName = "Wassup/Hazard", fileName = "Hazard_New")]
public class HazardSO : ScriptableObject
{
    [Header("Shape")]
    public HazardShape shape = HazardShape.SingleCell;
    public int radius = 1;          // RadiusCircle 시 사용

    [Header("Lifetime")]
    public float lifetime = 5f;

    [Header("Visual (decoupled)")]
    public GameObject visualPrefab;

    [Header("Effects (composition)")]
    public HazardEffect[] effects;
}
```

## 완료 기준

- 4 파일 컴파일 성공.
- HazardSO Inspector 에서 4 헤더 + 필드 노출.
- IBufferElementData / IComponentData / Serializable struct 모두 blittable (Burst-호환 검증).
- 콘솔 에러/경고 0.
- `HazardShape` / `HazardEffect` / `HazardSO` 식별자 grep 으로 발견됨.
