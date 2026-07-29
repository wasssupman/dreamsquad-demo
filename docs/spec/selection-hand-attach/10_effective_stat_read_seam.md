# 10 — 실효 스탯 read seam (BattleBridge)

> 추가 2026-07-30 (사용자 결정). units 11~13 의 토대 — 패널이 읽을 값이 먼저 있어야 한다.

## 목적

선택 유닛의 **버프·디버프가 반영된 실효 스탯** 3종(체력·공격력·공격속도)을 뷰가 읽을 창구를
만든다. 지금 `BattleBridge` 는 내부 순회로 오버헤드 UI 에 push 만 하고, **단일 엔티티를 pull 하는
API 가 없다**. 델타 표기(unit 11)를 위해 기본값도 함께 낸다.

## 전제 — 최종 스탯은 아무도 저장하지 않는다 (실측 확인)

ECS 는 **재료**(base + 배율)를 맥락별로 나눠 들고, 최종값은 **소비 시점에 계산**된다.
Mono 에는 최종 스탯이 전혀 없다.

| 스탯 | 재료 | 최종값 계산 지점 | 저장 |
|---|---|---|---|
| 공격력 | `AttackOutputElement.magnitude`(Combat) + `damageMul`(Effects) | `AttackSystem.cs:814` RESOLVE | 안 함 |
| 공격속도 | `AttackState.cooldownDuration`(Combat) + `attackSpeedMul`(Effects) | `AttackSystem.cs:660-663` START | 안 함 |
| 체력 최대 | `maxHealthMul`(Effects) | Units `MaxHealthScaleSystem` | **`Health.max` 에 구워짐** |

**그래서 이 unit 은 "읽기"가 아니라 "재료를 읽어 표시값을 결정하기"다.** 단 그 결정은
**뷰 쪽에서만** 일어난다 — 아래 D 참조.

## 변경 대상

- `Assets/_Project/Scripts/Data/UnitStatReadout.cs` (신규) — plain 값 구조체 + 순수 계산
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryGetUnitStatReadout`
- `Assets/_Project/Tests/EditMode/UnitStatReadoutTests.cs` (신규)

## 구현

### A. `UnitStatReadout` — plain 값 + 순수 함수

```
struct UnitStatReadout {
    float hp, hpMax, hpMaxBase;      // 체력: 현재 / 실효 최대 / 기본 최대
    float damage, damageBase;        // 공격력
    float attackRate, attackRateBase;// 공격속도(초당 발사 횟수)
}
```

순수 static 2개(제약 10 — 아키텍처를 모르는 값 계산, EditMode 대상):

- `CooldownToRate(cooldownDuration, speedMul)` → 초당 발사 횟수. `cooldown <= 0` 이면 0 반환
  (0 나눗셈 가드). **쿨다운 초가 아니라 rate 로 내는 것이 계약** — 큰 숫자 = 빠름이 직관적이다.
- `ResolveDelta(baseV, effV, epsilon)` → `(sign, magnitude)`. `|차이| < epsilon` 이면 sign 0
  = "변화 없음"(unit 11 이 칩을 숨기는 근거). 부동소수 잔차가 ▲0 으로 새는 것을 막는다.

### B. `TryGetUnitStatReadout(Entity, out UnitStatReadout)`

읽기 전용이므로 맥락 경계를 넘지 않는다(쓰기는 각 소유 맥락만 — 제약 2).

| 값 | 실효 | 기본(델타 기준) |
|---|---|---|
| 체력 | `Health.value` / `Health.max` (Units) | SO `maxHealth` |
| 공격력 | `AttackOutputElement` 버퍼의 `Damage` × `ModifierStats.damageMul` | SO `outputs` 의 `Damage` |
| 공격속도 | `CooldownToRate(AttackState.cooldownDuration, ModifierStats.attackSpeedMul)` | `CooldownToRate(SO attackCooldown, 1)` |

- `Health.max` 는 **이미 `maxHealthMul` 이 반영된 값**이다(Units 의 `MaxHealthScaleSystem` 이 씀).
  다시 곱하지 말 것 — 이중 적용된다.
- 런타임 버퍼가 SO 와 다를 수 있다(예: `TauntAttackGrantSystem` 이 공격을 부여). **실효는 버퍼,
  기본은 SO** 로 두면 그 차이도 델타에 정직하게 나타난다.
- SO 해석은 기존 `FindDefenderData(Entity)`(`BattleBridge.cs:3174`) 재사용 — 사본 금지.
- 방어 유닛이 아니거나(적) 컴포넌트/버퍼가 없으면 `false`. 뷰는 그 프레임 표시를 생략한다.
- `AttackOutputStats.TryGetUniqueMagnitude`(`Data/AttackOutputStats.cs`)로 SO 쪽 base 를 뽑는다.
  스칼라 `attackDamage` 는 unit-stat-projection unit 4 에서 **은퇴**했으므로 읽지 않는다.

### C. 표시값은 "조건 없는 타격당 피해" 다

실제 데미지 체인은 곱이 더 길다(`AttackSystem` 실측):

```
magnitude × damageMul × [대상 CC면 attackerVsCc] × [frontmostMul] × [dcBounceMul]
              ^^^^^^^^^^ 여기까지만 표시 가능
```

뒤 셋은 **대상·시점 의존**이다 — `attackerVsCc`(:816, 대상이 CC 상태일 때만) ·
`frontmostMul`(:375, START 스냅샷) · `dcBounceMul`(:804, 바운스 감쇠). 유닛 하나만 보고는
값이 정해지지 않으므로 readout 에 **넣을 수 없다**(넣으면 표시가 거짓이 된다).

같은 이유로 `dmgTakenMul`·`moveSpeedMul` 도 제외한다. 필요하면 부착 카드 설명이 그 역할을
맡는다(신규 표면 만들지 않음). 이 한계는 결함이 아니라 **사양**이므로 unit 11 의 라벨도
이 전제로 쓴다.

### D. ECS 시스템은 건드리지 않는다 (사용자 결정 2026-07-30)

**표시용 산식 때문에 `AttackSystem` 을 리팩터하지 않는다.** 초안은 산식을 순수 static 으로
빼서 sim 과 뷰가 공유하게 하려 했으나, 제약 10 의 자체 판정에서 탈락한다:

- **(a) 비자명 — 실패.** `o.magnitude * damageMul` 은 곱셈 하나다.
- **(b) 재사용 2+ — 형식만 통과.** 두 번째 호출처가 사전에 있던 게 아니라 이 unit 이 만든다.
- **(c) sim-critical 회귀 가치 — 실패.** sim 은 이미 동작 중이고 테스트가 있다. 추출은 sim 의
  안전성을 높이지 않고 **건드려서 낮춘다**.

그리고 제약 10 이 요구하는 것은 **"값이 아키텍처를 모른다"**이지 "모든 소비자가 한 함수를
공유한다"가 아니다. 그 분리는 **이미 달성돼 있다** — `ModifierMath.CombineMul` 이 순수하게
결합해 `ModifierStats`(plain float 묶음)를 만들고, ECS 시뮬과 Mono 프레젠테이션이 각자
해석·소비한다. 여기서 필요한 것은 새 분리가 아니라 **그 값을 읽어 표시로 해석하는 일**이다.

위험 교환도 손해다: 표시 산식이 어긋나면 **숫자가 틀리고**(cosmetic), `AttackSystem` 회귀는
**판정·밸런스가 틀린다**. 한 단계 심각한 쪽을 열어 가벼운 쪽을 막을 이유가 없다.

따라서:

- 이 unit 의 순수 함수는 **뷰 소유의 표시 로직**이다. `AttackSystem` 은 자기 인라인 산식을
  그대로 둔다. **이 unit 은 ECS 변경이 아니다** → `ecs-reviewer` 가 아니라 일반 code-review 대상
  (`BattleBridge` 단순 read 는 ECS 리뷰 대상이 아니라는 기존 판정과 같다).
- 대신 **테스트가 산식을 문서화**한다. 순수 함수 주석과 EditMode 테스트에 참조 지점
  (`AttackSystem.cs` RESOLVE / START)을 남겨, 훗날 sim 산식이 바뀌면 찾을 수 있게 한다.
  데미지 모델 자체가 바뀌는 규모라면 뷰도 당연히 재검토 대상이 된다.
- **이후 스탯 표시를 늘릴 때도 같은 규칙이다** — 표시 요구가 sim 리팩터를 끌어오면 정지하고
  질문한다.

## 완료 기준

- [x] compile 클린 (2026-07-30 — Unity 콘솔 error 0)
- [x] **`Assets/_Project/Scripts/Battle/` diff 0** — ECS 시스템 무변경(D 의 기계적 확인)
- [x] EditMode: `CooldownToRate` 0/음수 쿨다운 가드 · 배율 결합 · `ResolveDelta` 부호 3종
      (상승/하락/epsilon 내 flat) — `UnitStatReadoutTests` **9/9 통과**
- [ ] Play: 공격력 버프 카드 부착 전후로 `damage` 가 바뀌고 `damageBase` 는 그대로다
- [ ] Play: 표시된 공격력이 실제 데미지 숫자 팝업과 일치한다(조건부 배율이 없는 평범한 타격)
- [ ] Play: 적 엔티티/미배치 엔티티로 호출 시 `false`, 예외 없음

**Play 3항목은 소비자(unit 11 패널)가 생긴 뒤 함께 확인한다** — 지금은 값을 볼 표면이 없다.

전체 EditMode 1589건 중 실패 1건은 `MultiGoalPoolSeparationTests`(`MapDocument_Zig.asset` 복도
병합). 세션 시작 시점부터 dirty 였던 **타 세션의 맵 편집** 때문이며 이 unit 과 무관하다
(이 unit 은 맵 데이터를 건드리지 않는다).
