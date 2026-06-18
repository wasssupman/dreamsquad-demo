# Unit 2 — BattleBridge 베이킹

## 목적

SO 거동 필드를 ECS 컴포넌트로 bake. attackMethod 가 attack 컴포넌트 부착을 결정하고, 필터를 SO 에서 가져온다(enemyClass 하드코딩 제거). Unit 1(마이그레이션) 이후에 적용.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (적 스폰 경로 ~3390)

## 구현

### attackMethod 분기 (방어적 — Critic C1)
- `None` → attack 컴포넌트 미부착 (walk-only).
- `Melee` → outputs 가 있으면 `AttackState` + `AttackOutputElement`. **outputs 가 비면 부착 안 함(walk-only) + 경고 로그.**
- `Projectile` → 위 + `ProjectileRef`. outputs 비면 동일하게 walk-only + 경고.

> 즉 "Melee/Projectile + outputs 빈" 적은 데미지-0 공격자로 만들지 말고 walk-only 로 둔다. (러너/스위프트가 기본값 Melee 로 역직렬화돼도 안전)

### EnemyBehavior + FocusTarget
```csharp
_em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyBehavior
{ targetMode = unitType.targetMode, aimMode = unitType.aimMode });
if (unitType.targetMode == EnemyTargetMode.FocusUntilDead)
    _em.AddComponentData(entity, new Wassup.Battle.Combat.FocusTarget { current = Entity.Null });
```
- `FocusTarget` 는 여기서 **사전 부착**(AttackSystem 은 값만 갱신, mid-loop 구조변경 없음).

### EnemyTargetFilter — SO 에서
```csharp
int prio = unitType.targetPriorityClass == DefenderClass.None ? -1 : (int)unitType.targetPriorityClass;
_em.AddComponentData(entity, new Wassup.Battle.Combat.EnemyTargetFilter
{ classMask = (int)unitType.targetClassMask, priorityClass = prio });
```
- `(int)Everything == -1` → 기존 "-1 = all" 의미 보존.
- **기존 `enemyClass == Shooter ? Ranger : -1` 하드코딩 삭제** (aggro-targeting unit 4 분기).

### 변경 안 함
- **`AggroAttackProfile`(도발) 부착은 attackMethod 분기와 무관하게 그대로** (Critic M5). 어그로 도발 경로 유지.
- movePauseOnAttackSec 는 AttackState 에 그대로 전달(aimMode 가 AttackSystem 에서 게이팅).

## 완료 기준

- [ ] Play reflection: Melee(outputs有) 적 AttackState 있음 / None 적 없음 / **Melee+outputs빈 적도 AttackState 없음** / Projectile 적 ProjectileRef 있음.
- [ ] EnemyBehavior/EnemyTargetFilter SO 값대로, FocusUntilDead 적만 FocusTarget.
- [ ] Runner/Swift `AggroAttackProfile` 유지(어그로 회귀 없음).
- [ ] enemyClass 하드코딩 분기 grep 제거 확인.
