# Unit 0 — 데이터 필드 + ECS 컴포넌트 토대

## 목적

어그로에 필요한 authoring 필드와 ECS 컴포넌트를 정의한다. 로직 없음, 컴파일만.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` (aggroCapacity)
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` (도발 공격 프로필)
- (신규) `Assets/_Project/Scripts/Battle/Effects/AggroProvider.cs`
- (신규) `Assets/_Project/Scripts/Battle/Effects/Aggroed.cs`

## 구현

### DefenderUnitData — aggroCapacity

```csharp
[Header("Aggro")]
// 동시에 끌어둘 수 있는 적 수 상한. 0 = 어그로 없음(Fighter/Ranger).
// 가디언만 >0. 구체값은 밸런싱 위임.
public int aggroCapacity = 0;
// 어그로 획득 범위(타일). 0 이면 attackRange 를 사용. 가디언 근접(1) 기본.
public float aggroRange = 0f;
```

### AttackUnitData — 도발 공격 프로필 (Runner/Swift용)

공격 outputs 가 없는 적도 어그로 시 가디언을 때려야 한다(계약 7). 평상시엔 사용 안 함.

```csharp
[Header("Aggro (Taunt) Attack")]
// outputs 가 비어있는 적이 어그로 상태에서 사용할 근접 공격.
// 평상시(어그로 아님)에는 적용되지 않는다.
public float aggroAttackDamage = 0f;
public float aggroAttackCooldown = 1f;
public float aggroAttackRange = 1f;
```

### AggroProvider (Effects 소유, 가디언에 부착)

```csharp
public struct AggroProvider : IComponentData
{
    public int capacity;   // 동시 어그로 상한 (SO aggroCapacity)
    public float range;    // 어그로 획득 범위 (world units)
}
```
> count 는 컴포넌트에 저장하지 않는다. AggroAssignmentSystem 이 매 틱 `Aggroed` 적을 세어 파생 (drift 방지).

### Aggroed (Effects 소유, 적에 부착)

```csharp
public struct Aggroed : IComponentData
{
    public Entity guardian;  // 선점한 가디언. sticky 링크.
}
```

## 완료 기준

- [x] 컴파일 에러 없음.
- [x] `AggroProvider`, `Aggroed` 가 Effects 네임스페이스(`Wassup.Battle.Effects`)에 존재.
- [x] `DefenderUnitData.aggroCapacity`, `AttackUnitData.aggroAttack*` 인스펙터 노출.
- [x] 기존 SO 역직렬화 회귀 없음 (신규 필드 기본값 0).

완료: 2026-06-18 / 커밋 해시 `<unit0-commit>`
