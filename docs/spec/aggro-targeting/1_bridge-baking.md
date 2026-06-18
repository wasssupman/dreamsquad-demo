# Unit 1 — BattleBridge 베이킹

## 목적

SO 필드를 ECS 엔티티에 부착한다. 가디언은 `AggroProvider`, 모든 적은 도발 공격 프로필을 런타임에서 쓸 수 있도록 데이터를 싣는다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (디펜더 스폰 경로 ~2800, 적 스폰 경로 ~3390)
- (신규) `Assets/_Project/Scripts/Battle/Combat/AggroAttackProfile.cs`

## 구현

### 가디언 스폰 — AggroProvider 부착

디펜더 스폰 경로(`AttackState` 부착부, ~2801)에서:

```csharp
if (unitData.aggroCapacity > 0)
{
    float aggroRange = unitData.aggroRange > 0f ? unitData.aggroRange : unitData.attackRange;
    _em.AddComponentData(entity, new Wassup.Battle.Effects.AggroProvider
    {
        capacity = unitData.aggroCapacity,
        range = aggroRange,
    });
}
```
- `aggroCapacity == 0` (Fighter/Ranger) 면 부착하지 않음 → 어그로 미발생.

### 적 스폰 — 도발 공격 프로필 부착

적 스폰 경로(~3390)에서, `aggroAttackDamage > 0` 인 적에 부착:

```csharp
public struct AggroAttackProfile : IComponentData   // 신규, Combat
{
    public float damage;
    public float cooldown;
    public float range;
}
```
```csharp
if (entry.unitType.aggroAttackDamage > 0f)
    _em.AddComponentData(entity, new Wassup.Battle.Combat.AggroAttackProfile
    {
        damage = entry.unitType.aggroAttackDamage,
        cooldown = entry.unitType.aggroAttackCooldown,
        range = entry.unitType.aggroAttackRange,
    });
```
- outputs 가 있는 적(Bruiser/Shooter/Tanker)은 기존 `AttackState`+outputs 로 어그로 시 가디언을 때린다(unit 4 에서 타겟만 전환). 도발 프로필은 outputs 없는 Runner/Swift 의 fallback.

### SO 값 채우기

- `Defender_Guardian.asset`, `Defender_Bastion.asset`: `aggroCapacity` placeholder 값 부여(예: 4). aggroRange 0(=attackRange).
- `Enemy_Runner.asset`, `Enemy_Swift.asset`: `aggroAttackDamage/Cooldown/Range` placeholder(예: 5 / 1 / 1).
- 나머지 디펜더는 aggroCapacity 0 유지.

## 완료 기준

- [ ] 컴파일 + reflection 으로 Guardian 엔티티에 `AggroProvider{capacity>0}` 부착 확인.
- [ ] Runner/Swift 엔티티에 `AggroAttackProfile` 부착, Fighter/Ranger 엔티티엔 `AggroProvider` 없음.
- [ ] Play 진입 시 기존 전투 회귀 없음.
