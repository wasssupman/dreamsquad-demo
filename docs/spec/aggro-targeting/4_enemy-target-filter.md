# Unit 4 — 적 공격필터 + 클래스 우선순위

## 목적

적의 기본(비어그로) 타게팅을 **아군 클래스 비트마스크 필터 + 클래스 우선순위**로 결정한다. 이번 범위는 **Shooter 적만 Ranger 우선**, 나머지 적은 `all`(최근접). 어그로는 이 필터를 override(unit 5)하므로, override 대상인 기본 필터를 여기서 세운다.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Battle/Units/DefenderClassTag.cs`
- (신규) `Assets/_Project/Scripts/Battle/Combat/EnemyTargetFilter.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (디펜더/적 스폰 baking)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` (비어그로 타겟 선정)

## 구현

### 디펜더 클래스 ECS 노출

```csharp
public struct DefenderClassTag : IComponentData   // Units
{
    public Wassup.Data.DefenderClass value;
}
```
디펜더 스폰 시 `unitData.role` 로 baking. 적이 클래스로 필터/우선순위 판정에 사용.

### 적 공격필터

```csharp
public struct EnemyTargetFilter : IComponentData  // Combat
{
    public int classMask;       // 허용 DefenderClass 비트: bit = 1 << (int)DefenderClass
    public int priorityClass;   // 동일조건 우선 선정 클래스. -1 = 우선순위 없음(최근접)
}
```
- `ALL_MASK = ~0`. 비트 헬퍼: `1 << (int)DefenderClass.Ranger` 등.
- 적 스폰 baking:
  - `enemyClass == Shooter` → `{ classMask = ALL_MASK, priorityClass = (int)DefenderClass.Ranger }`
  - 그 외 전부 → `{ classMask = ALL_MASK, priorityClass = -1 }`

### AttackSystem 비어그로 선정

비어그로 적 attacker 의 후보 선정에 적용 (어그로 적은 unit 5 에서 분기):
1. 기존 faction mask + 사거리 통과 후보 중,
2. `EnemyTargetFilter` 있으면: `DefenderClassTag` 가진 후보는 `classMask` 비트 검사로 제외 가능. `DefenderClassTag` 없는 후보(예: BlockingHazard)는 필터 통과(클래스 없음 = 제외 안 함).
3. `priorityClass >= 0` 이고 그 클래스 후보가 사거리 내 존재하면 → **그 클래스 후보 중 최근접** 선정. 없으면 전체 후보 중 최근접.
- `EnemyTargetFilter` 없는 attacker(디펜더 등)는 기존 최근접 로직 그대로.

## 완료 기준

- [ ] 컴파일 + Burst 호환.
- [ ] 디펜더 엔티티에 `DefenderClassTag{role}` baking 확인(reflection).
- [ ] Shooter 적 엔티티 `priorityClass == Ranger`, 그 외 `-1`.
- [ ] EditMode/Play: 가디언+레인저 동시 사거리일 때 Shooter 적이 **레인저**를 선정(더 가까운 가디언이 있어도).
- [ ] 비-Shooter 적은 기존대로 최근접 선정(회귀 없음).
