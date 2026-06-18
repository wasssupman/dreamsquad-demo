# Unit 5 — sticky 타게팅 + 도발 공격

## 목적

어그로된 적이 가디언만 공격하도록 타게팅을 고정(override)하고, 공격 능력 없는 Runner/Swift 도 어그로 시 가디언을 때리게 한다(계약 4, 7).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/AggroAssignmentSystem.cs` (도발 공격 활성/비활성 — 구조 변경)

## 구현

### sticky 타게팅 (AttackSystem)

attacker 루프에서 attacker 가 `Aggroed` 면 최근접 탐색을 건너뛰고 타겟을 링크 가디언으로 고정:

```csharp
if (aggroLookup.HasComponent(attackerEntity))
{
    var g = aggroLookup[attackerEntity].guardian;
    // 가디언이 살아있고 사거리 내면 그 가디언만 타겟. 아니면 미발사(이동 중).
    bestTarget = (in-range 판정 통과) ? g : Entity.Null;
}
else { /* 기존 최근접 탐색 */ }
```
- 어그로 적은 mask·`EnemyTargetFilter`(unit 4)·우선순위와 무관하게 **가디언 전체 override**(계약 4, 10). 사거리 판정은 기존 `GridMath` 방식 재사용.

### 도발 공격 활성 (AggroAssignmentSystem)

outputs 없는 적(Runner/Swift)은 `AttackState`/outputs 가 없어 AttackSystem 루프에 안 든다. 어그로 획득/해제 시 구조 토글:

- **획득 시**, 적이 `AttackState` 없음 && `AggroAttackProfile` 있음 →
  - `AttackState{ range=profile.range, cooldownDuration=profile.cooldown, targetMask=(int)Faction.Defender, attackTargetCount=1 }` 추가
  - `AttackOutputElement` 버퍼 추가 + `Damage` output(magnitude=profile.damage) 1개
  - 표식 `TauntAttackGranted`(empty tag) 추가 — 해제 시 제거 대상 식별용
- **해제 시**, `TauntAttackGranted` 있는 적 → `AttackState`/`AttackOutputElement`/`TauntAttackGranted` 제거 (출구행 적이 디펜더를 때리지 않도록, 계약 7).

> outputs 가 이미 있는 적(Bruiser/Shooter/Tanker)은 토글 불필요 — sticky 타게팅만으로 가디언을 때린다.

> 신규 tag: `Assets/_Project/Scripts/Battle/Effects/TauntAttackGranted.cs`.

## 완료 기준

- [ ] 컴파일 + Burst 호환.
- [ ] EditMode/PlayMode: 어그로된 Bruiser 적이 근처 다른 디펜더가 아닌 **가디언**에게 IncomingDamage 적용.
- [ ] 어그로된 Runner 가 가디언에 도발 공격 데미지 적용.
- [ ] 해제된 Runner 는 `AttackState` 제거되어 디펜더를 공격하지 않고 출구로 이동.
- [ ] 가디언이 어그로 적을 정상 공격(기존 경로).
