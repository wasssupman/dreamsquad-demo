# Unit 0 — enum + 컴포넌트 + SO 필드

## 목적

거동 데이터 토대. enum 3종, ECS 컴포넌트 2종, `AttackUnitData` SO 필드. 로직 없음, 컴파일만.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Data/EnemyBehaviorEnums.cs` (또는 enum별 파일)
- (신규) `Assets/_Project/Scripts/Battle/Combat/EnemyBehavior.cs`
- (신규) `Assets/_Project/Scripts/Battle/Combat/FocusTarget.cs`
- `Assets/_Project/Scripts/Data/AttackUnitData.cs`

## 구현

### enum (Wassup.Data)

```csharp
public enum EnemyAttackMethod { None, Melee, Projectile }
public enum EnemyTargetMode   { None, Nearest, FocusUntilDead }
public enum EnemyAimMode      { StopToAttack, MoveAndShoot }

// 공격필터 클래스 비트. bit = 1 << (int)DefenderClass (EnemyTargetFilter.classMask 와 정합).
[System.Flags]
public enum DefenderClassFlags
{
    None = 0,
    Ranger   = 1 << 1,
    Guardian = 1 << 2,
    Fighter  = 1 << 3,
    Caster   = 1 << 4,
    Support  = 1 << 5,
    Everything = ~0,
}
```

### ECS 컴포넌트 (Wassup.Battle.Combat)

```csharp
public struct EnemyBehavior : IComponentData
{
    public Wassup.Data.EnemyTargetMode targetMode;
    public Wassup.Data.EnemyAimMode aimMode;
}

// targetMode == FocusUntilDead 일 때만 부착. AttackSystem 이 현재 고정 타겟을 기록.
public struct FocusTarget : IComponentData
{
    public Entity current;
}
```

### AttackUnitData 필드 (`[Header("Behavior")]`)

```csharp
public EnemyAttackMethod attackMethod = EnemyAttackMethod.Melee;
public EnemyTargetMode   targetMode   = EnemyTargetMode.Nearest;
public EnemyAimMode      aimMode      = EnemyAimMode.StopToAttack;
public DefenderClass     targetPriorityClass = DefenderClass.None; // None = 우선순위 없음
public DefenderClassFlags targetClassMask    = DefenderClassFlags.Everything;
```
- 기존 `enemyClass` 주석을 "라벨 전용"으로 갱신. `movePauseOnAttackSec` 는 StopToAttack 지속시간으로 유지(>0 일 때만 실제 정지).

### classMask 계약 (Critic M4)
- `(int)DefenderClassFlags.Everything == -1` → 기존 `EnemyTargetFilter` "-1 = all" 의미와 정합.
- **bit 0 은 의도적으로 미사용**: `DefenderClass.None(0)` 에는 플래그 비트가 없다(플래그는 `1<<1` 부터). 비-Everything 마스크를 쓰면 None 클래스 타겟은 제외된다.
- **`DefenderClassTag` 없는 타겟(BlockingHazard 등)은 classMask 무시하고 항상 후보**(AttackSystem.cs 의 `cclass>=0` 가드와 동일). 마스크는 디펜더 클래스에만 적용.

## 완료 기준

- [ ] 컴파일 에러 없음.
- [ ] enum 3종 + DefenderClassFlags, `EnemyBehavior`/`FocusTarget` 인식.
- [ ] AttackUnitData 신규 필드 인스펙터 노출, 기존 SO 역직렬화 회귀 없음(기본값).
