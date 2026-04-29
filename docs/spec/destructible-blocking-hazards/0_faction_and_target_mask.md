# Faction Enum + FactionTag + AttackState.targetMask

**작업 구분**: 0

## 목적

공격 타겟팅 일반화의 토대 — Faction enum, FactionTag IComponentData, AttackState 의 `targetMask` 필드를 신설한다. 이 단위는 데이터 모델만 추가하고 실제 사용은 Unit 1/2 에서. **compile-only 게이트**.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Units/Faction.cs` (enum [Flags])
- Add: `Assets/_Project/Scripts/Battle/Units/FactionTag.cs` (IComponentData)
- Modify: `Assets/_Project/Scripts/Battle/Combat/AttackState.cs` (`targetMask` 필드 추가)

## 구현

### Faction.cs

```csharp
using System;

namespace Wassup.Battle.Units
{
    [Flags]
    public enum Faction : int
    {
        None           = 0,
        Defender       = 1 << 0,
        Enemy          = 1 << 1,
        BlockingHazard = 1 << 2,
        // 미래 확장: FieldProp = 1 << 3, Goal = 1 << 4, Totem = 1 << 5, ...
    }
}
```

### FactionTag.cs

```csharp
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Identifies an entity's faction for attack-targeting filtering.
    // Owned by Units context (entity identity). Read by Combat (AttackSystem).
    public struct FactionTag : IComponentData
    {
        public Faction value;
    }
}
```

### AttackState.cs (수정)

`AttackState` struct 에 한 필드 추가 (기존 필드 순서 보존, 마지막에 append):
```csharp
public int targetMask; // (int)Faction bitmask of attackable factions
```

## 단위 테스트 (EditMode)

없음 — 데이터 정의만. 실제 동작은 Unit 2 에서 검증.

## 완료 기준

- 컴파일 성공.
- 기존 테스트 (133/133) 회귀 0.
- 기존 동작 변화 0 — `targetMask=0` default 이지만 AttackSystem 이 아직 mask 안 보므로 영향 없음.
- **plays-test 금지** — Unit 1+2 까지 합쳐야 attack 동작 정상. 단독 play 시 회귀 가능.
- 콘솔 에러/경고 0.

검증: 2026-04-29 — Unit 0~2 묶음으로 컴파일 성공, EditMode 142/142 통과, 콘솔 에러/경고 0. 커밋 `3f5ab31`.
