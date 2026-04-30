# 0. Data Model

## 목적

modifier framework 의 **데이터 타입만** 먼저 정의한다. struct / enum 만 들어가고 system 로직은 후속 단위에서. 컴파일 통과 + 다른 단위가 import 할 수 있는 상태가 0번의 끝.

scope: enum 4종, struct 5종 (header + 두 slot + cache + dirty). System stub / channel singleton / AttackOutput 사용처는 1/5번 단위에서.

## 변경 대상

신규 파일 (5개 + meta):

| 파일 | namespace | 내용 |
|---|---|---|
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs` | `Wassup.Battle.Effects` | enum `StatKind`, `StackKind`, `CombineOp` + struct `ModifierHeader` |
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/StatModifierSlot.cs` | `Wassup.Battle.Effects` | `IBufferElementData` |
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierSlot.cs` | `Wassup.Battle.Effects` | `IBufferElementData` |
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/BuffStats.cs` | `Wassup.Battle.Effects` | `IComponentData` (`BuffStats`) + `IComponentData, IEnableableComponent` (`BuffStatsDirty`) |
| `Assets/_Project/Scripts/Data/AttackOutput.cs` | `Wassup.Data` | `Serializable` struct `AttackOutput` + enum `AttackOutputKind` |

폴더 신설: `Assets/_Project/Scripts/Battle/Effects/Modifiers/`. 기존 `Effects/` 평탄 구조에서 modifier framework 파일이 8+개 늘어날 예정이라 서브폴더 분리. 프로젝트 단일 asmdef (`Wassup.Runtime.asmdef`) 가 하위 폴더 자동 포함 — asmdef 변경 불필요.

## 구현

**`ModifierTypes.cs`**
```csharp
public enum StatKind : byte { DamageMul, AttackSpeedMul, DmgTakenMul, RegenPerSec }
public enum StackKind : byte { None }                  // 멤버는 4번 단위에서 추가 (Fire, Ice, Bleed, ...)
public enum CombineOp : byte { Multiplicative, Additive, Override }

public struct ModifierHeader {                         // 임베딩 컨벤션, IComponentData/IBufferElementData 아님
    public float remaining;
    public Entity source;
    public ushort stackId;
}
```

**`StatModifierSlot.cs`**
```csharp
public struct StatModifierSlot : IBufferElementData {
    public ModifierHeader header;
    public StatKind stat;
    public CombineOp op;
    public float magnitude;
}
```

**`StackModifierSlot.cs`**
```csharp
public struct StackModifierSlot : IBufferElementData {
    public ModifierHeader header;                      // remaining = perAppDuration 까지 남은 시간 (S1)
    public StackKind kind;
    public byte stackCount;
    public byte maxStack;
    public byte lastTriggeredStack;                    // edge 검출 캐시 (4번 단위에서 사용)
}
```

**`BuffStats.cs`**
```csharp
public struct BuffStats : IComponentData {
    public float damageMul;       // 디폴트 1.0
    public float attackSpeedMul;  // 디폴트 1.0
    public float dmgTakenMul;     // 디폴트 1.0
    public float regenPerSec;     // 디폴트 0.0
}

// IEnableableComponent — Add 시 기본 disabled.
// ApplySystem/TickSystem 이 SetComponentEnabled(true), Aggregate 가 처리 후 SetComponentEnabled(false).
public struct BuffStatsDirty : IComponentData, IEnableableComponent { }
```

**`AttackOutput.cs`** (MonoBehaviour-side data, `Wassup.Data` namespace)
```csharp
[Serializable] public enum AttackOutputKind { Damage, Heal, ApplyStat, ApplyStack }

[Serializable] public struct AttackOutput {
    public AttackOutputKind kind;
    public float magnitude;
    public float duration;
    public StatKind stat;          // ApplyStat 만
    public CombineOp op;           // ApplyStat 만
    public StackKind stackKind;    // ApplyStack 만
}
```

`AttackOutput` 이 `Wassup.Battle.Effects` 의 enum (`StatKind`/`StackKind`/`CombineOp`) 을 참조하지만, 프로젝트 전체가 단일 `Wassup.Runtime.asmdef` 안에 있으므로 (확인됨, `Assets/_Project/Scripts/Wassup.Runtime.asmdef`) namespace 간 자유 참조 가능. asmdef 경계 우려 없음 — `using Wassup.Battle.Effects;` 한 줄이면 끝.

## 완료 기준

- [ ] 5개 파일 신규 작성 (위 변경 대상 표).
- [ ] Unity Editor 컴파일 성공 (`Console` 에 에러 0). 기존 코드(`AttackSystem`/`DamageApplicationSystem` 등) 는 신규 타입 미사용 상태로 무변경 → 회귀 0.
- [ ] `BuffStatsDirty` 가 `IEnableableComponent` 인지 확인 (`SystemAPI.IsComponentEnabled<BuffStatsDirty>` 가 컴파일).
- [ ] (asmdef 검증은 0번 작성 전 완료 — 단일 `Wassup.Runtime.asmdef` 확인. 추가 검증 불요.)
- [ ] EditMode 테스트 추가 불요 (struct 정의만이므로 동작 검증 대상 없음). 11번 단위에서 `BuffStats` 합성식 테스트가 본 타입을 사용하는 형태로 검증.
- [ ] 본 문서 하단에 "확인 일자 + 커밋 해시" 한 줄 추가 후 commit.

## 후속 단위 의존

- 1번: 두 NativeQueue singleton 정의 + BattleBridge lifecycle (이 단위가 본 단위의 타입을 import 안 함 — payload struct 는 1번이 자체 정의)
- 2번: `ModifierApplySystem` 이 `StatModifierSlot` / `StackModifierSlot` / `BuffStatsDirty` 사용
- 3번: `BuffStatsAggregateSystem` 이 `BuffStats` write
- 5번: `AttackOutput[]` 가 `DefenderUnitData` 에 추가됨

---

확인 일자 + 커밋 해시: _(작업 완료 시 기재)_
