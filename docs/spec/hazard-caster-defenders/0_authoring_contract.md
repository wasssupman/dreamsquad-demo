# 0. Authoring Contract

## 목적

`DefenderUnitData` 에 hazard caster action 을 authoring 할 수 있는 계약을 추가한다. 신규 방어 유닛 4종은 이 계약으로 기존 hazard asset 을 참조한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- Modify/Create: `Assets/_Project/Data/Defenders/Defender_*Caster.asset`
- Create: `Assets/_Project/Data/Hazards/Hazard_Fire_1x1.asset`
- Create: `Assets/_Project/Data/Hazards/Hazard_Ice_1x1.asset`
- Create: `Assets/_Project/Data/Hazards/Hazard_Poison_1x1.asset`
- Create: `Assets/_Project/Data/Hazards/Hazard_Rock_1x1.asset`
- Reference: existing `*_3x3` assets for effect/visual values only.

## 구현

### HazardCastKind enum

`HazardCastKind` 는 `Assets/_Project/Scripts/Battle/Effects/HazardCastKind.cs` 에 단독 파일로 정의한다. **`Wassup.Battle.Effects` 네임스페이스**로 둔다.

```csharp
namespace Wassup.Battle.Effects
{
    public enum HazardCastKind : byte
    {
        None      = 0,
        Zone      = 1,
        Blocking  = 2,
    }
}
```

`DefenderUnitData.cs` 는 `using Wassup.Battle.Effects;` 를 추가해서 참조한다. Authoring SO 가 runtime enum 을 참조하고, runtime component 는 authoring namespace 에 의존하지 않는다.

### DefenderUnitData 추가 필드

```csharp
[Header("Hazard Cast")]
public bool hazardCastEnabled;
public float hazardCastRange;
public float hazardCastCooldown;
public HazardCastKind hazardCastKind;
public HazardSO zoneHazard;
public BlockingHazardSO blockingHazard;
public int hazardFootprintWidth = 1;
public int hazardFootprintHeight = 1;
```

기본값은 비활성이다. 기존 defender asset 은 동작이 바뀌면 안 된다.

> **추상화 금지 조건**: hazard caster 필드를 별도 nested struct / interface 로 추출하지 않는다. 두 번째 caster 계열 유닛이 생겨서 실제 중복이 발생하는 시점에 추출한다. 이전 추출은 `CLAUDE.md` 추상화 규칙 위반이다.

MVP 에서 `hazardFootprintWidth/Height` 는 `1` 고정이다. authoring 값이 1이 아닌 경우 `HazardCastSystem` 에서 1로 clamp 한다 (BattleBridge drain 쪽에서 clamp 하지 않는다).

### 1x1 Hazard variant asset

`*_1x1` variant 는 기존 `*_3x3` asset 의 effect/visual 값을 그대로 복제하되, `shape` 필드를 `HazardShape.SingleCell` (기존 enum 값, 새로 만들지 않는다)으로 설정한다. `HazardShape.SingleCell = 0` 은 `HazardShapeSampler.Sample` 이 이미 처리한다.

신규 방어 유닛 4종은 다음 계약을 따른다.

| Unit | kind | hazard | footprint | target |
|---|---|---|---|---|
| Fire caster defender | Zone | `Hazard_Fire_1x1` | `1 x 1` | Enemy |
| Ice caster defender | Zone | `Hazard_Ice_1x1` | `1 x 1` | Enemy |
| Poison caster defender | Zone | `Hazard_Poison_1x1` | `1 x 1` | Enemy |
| Blocking caster defender | Blocking | `Hazard_Rock_1x1` | `1 x 1` | Enemy |

## 완료 기준

- `HazardCastKind.cs` 가 `Wassup.Battle.Effects` 네임스페이스에 단독으로 존재한다.
- 기존 defender asset 의 serialized default 로 hazard cast 가 비활성이다.
- 신규 4종 defender asset 이 hazard asset 을 정확히 참조한다.
- 1x1 hazard variants 는 기존 3x3 asset 의 effect/visual 값을 보존하되 `shape = HazardShape.SingleCell` 이다.
- `outputs[]` 없이도 hazard caster 로 동작할 수 있는 authoring 상태가 된다.
- `hazardFootprintWidth/Height = 1` 계약이 문서와 코드에서 일치한다.
