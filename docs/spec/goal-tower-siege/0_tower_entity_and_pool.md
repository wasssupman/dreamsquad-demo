# 0 — 타워 엔티티 + 공유 체력 풀

## 목적

골 셀마다 **때릴 수 있는 대상**을 세우고, 안정도의 정본을 브리지에서 ECS 싱글턴으로 옮긴다.
이 단위만으로는 아무도 타워를 때리지 않는다(적 공성은 unit 1) — 여기서는 **맞을 준비**만 한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/GoalTowerTag.cs` · `GoalTowerHealth.cs` ·
  `GoalTowerDamageSystem.cs`
- `Assets/_Project/Scripts/Battle/Units/Faction.cs` — `GoalTower = 1 << 3`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 타워 생성/티어다운, 안정도 폴링
- `Assets/_Project/Tests/EditMode/` — 풀 감산 회귀 테스트 신설

## 구현

**1. 아키타입** — `EffectSpawner.SpawnBlockingHazard` 와 동형이다. 판 시작 시(맵 빌드 직후)
골 셀(`_generatedMap.goals`, 폴백 `goal`)마다 엔티티 1기:

```
GoalTowerTag + Health{value=max=goalStabilityMax} + IncomingDamage 버퍼
             + FactionTag{Faction.GoalTower} + LocalTransform(셀 중심, sim 좌표)
```

`AttackSystem`·`EnemyAiStateSystem` 의 타겟 후보 쿼리가 `FactionTag + Health + LocalTransform`
이라 **타겟 후보로는 이미 보인다**. 실제로 노려지는 것은 mask 를 받는 unit 1 부터다.

**금지 목록(계약)**: `ModifierStats`·`StatModifierSlot`·`ShieldSlot`·`IncomingHeal` 을 붙이지
않는다. `MaxHealthScaleSystem` 이 `Health.max` 의 유일한 런타임 writer 이고 `ModifierStats`
보유 엔티티만 건드리는데, 타워가 그걸 얻으면 미러가 깨진다.

**2. 공유 체력** — `GoalTowerHealth`(싱글턴 컴포넌트, `value`/`max`)가 정본이고 각 타워의
`Health` 는 표시·타겟팅용 미러다. `GoalTowerDamageSystem`(Units,
**`[UpdateBefore(DamageApplicationSystem)]`**):

```
for each tower:
    taken += Σ(IncomingDamage);  buffer.Clear()
pool.value = max(0, pool.value − taken)
for each tower: Health = { value = pool.value, max = pool.max }
```

**`UpdateBefore` 가 핵심이다.** 버퍼를 먼저 비우므로 `DamageApplicationSystem` 은 타워
`Health` 를 건드리지 않는다 → (a) "누적 결손을 매 프레임 재차감" 하는 델타 계산이 원천적으로
불가능하고(초안의 치명적 오류), (b) 개별 타워가 `DeadTag` 를 받아 `UnitLifecycleSystem` 에
파괴되는 경로도 생기지 않는다. 타워 피격은 데미지 폰트를 만들지 않는다(그 발화도
`DamageApplicationSystem` 에 있다) — 타워는 유닛이 아니므로 의도된 결과다.

**3. authority 는 아직 옮기지 않는다** — 이 단위에서 브리지가 싱글턴을 폴링하기 시작하면
`DrainGoalEvents` 의 즉발 차감(three-minute-survival unit 0)이 매 프레임 덮여 **유출이 무해해진
채로 커밋된다.** 그래서 unit 0 은 풀을 덱 최대치로 세우고 미러만 돌리는 **inert 상태**로 두고,
authority 이관(폴링 + 즉발 차감 제거)은 실제로 피해가 도착하기 시작하는 unit 1 에서 한 번에
한다. 공개 API(`GoalStabilityCurrent`/`GoalStabilityMax`)는 그때도 **시그니처도 의미도 불변**
— 체력바와 tie-break 는 정본이 옮겨간 것을 모른다.

**4. 티어다운** — `BeginPlacement` 에서 `GoalTowerTag` 쿼리로 타워 엔티티를 파괴하고 싱글턴
엔티티도 정리한다(`_pending.Clear()` 부근, 기존 매치 경계 리셋과 co-locate).
`GoalTowerHealth` 는 NativeQueue 를 들지 않으므로 Dispose 대상이 없다.

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] EditMode: **타워 2기 + 복수 프레임** 풀 감산 — 피해를 준 프레임에만 정확히 그만큼 줄고,
      **무피해 프레임에는 풀이 불변**이어야 한다(초안 버그를 잡는 유일한 케이스)
- [ ] EditMode: 오버킬(풀보다 큰 피해)이 음수를 만들지 않는다
- [ ] EditMode: 두 타워에 나눠 들어온 피해의 합이 풀에서 한 번만 빠진다
- [ ] Play: 판 시작 시 골 셀마다 타워 엔티티가 서고 `BeginPlacement` 후 남지 않는다
- [ ] Play: **무회귀** — 유출 시 안정도가 지금까지처럼 즉발로 깎이고 0 이면 패배한다
      (이 단위는 아무 동작도 바꾸지 않는다. 타워는 아직 아무에게도 안 맞는다)
