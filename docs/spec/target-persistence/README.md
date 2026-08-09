# target-persistence — 타겟 지속성 (결정한 대상을 계속 때린다)

> ## 목표 3줄
>
> 1. **START 에서 겨눈 대상을 RESOLVE 에서 때린다** — 지금은 wind-up 중 타겟이 바뀌어 A를 겨누고 B를 때린다.
> 2. **결정된 타겟은 정해진 사유로만 바뀐다** — 방어유닛은 사망·범위이탈, 적은 거기에 어그로 끌림이 더해진다.
> 3. **명시적 타겟팅 규칙을 가진 유닛은 제외한다** — 힐러·facing·「끝을 보는 눈」·가디언 어그로 자석은 «매 순간 다시 고른다»가 정체성이다.

상태: **초안 2026-08-09 · 사용자 승인 대기**

## 왜 지금

`traversal-layers` 조사 중 **라이브 버그 2건**이 코드 대조로 확인됐다. 통행 층과 인과가 없어 별도 spec 으로 분리한다(제약 9 · 「버그 픽스 ≠ 기능」).

## 검증 질문

> 방어유닛이 한 적을 겨누기 시작하면, 그 적이 죽거나 사거리를 벗어나기 전까지 **다른 적으로 갈아타지 않는가?** 그리고 겨눈 대상과 실제로 맞는 대상이 **같은가?**

---

## 확인된 결함 2건 (코드 대조 완료)

### B1 — wind-up 중 타겟이 바뀐다 (A를 겨누고 B를 때린다)

START 게이트는 `hitDelayRemaining == 0` 일 때만 걸리는데(`AttackSystem.cs:752`), **타겟 선정 사슬은 매 프레임 무조건 돈다**(`:442~739`). RESOLVE(`:858`)는 **그 프레임의 새 `bestTarget`** 을 쓴다.

방어유닛 26종 중 **24종이 `hitDelaySec: 0.3`** 이라 이 창이 상시 열려 있다. 애니/빔은 A를 향하는데 데미지·투사체·넉백·수면·드림캐쳐 payload 는 B로 간다.

**기존 부품이 이미 정답 형태다.** `FrontmostAttackLock`(`:822~831`)은 START 에서 `{active, target, damageMulSnapshot}` 을 잡고 wind-up 동안 유지하다 RESOLVE 에서 해제한다 — 딱 필요한 shape 인데 **「끝을 보는 눈」 카드에만 게이트**돼 있다. 일반화가 최소 변경이다.

### B2 — Focus 적이 사거리를 벗어나면 영구히 골로 걸어간다

```csharp
else bestTarget = Entity.Null;                                   // :653 out of range → hold fire
focusLookup[attackerEntity] = new FocusTarget { current = cur };  // 락은 재저장
```

락은 **대상이 죽을 때만** 풀린다. 사거리를 벗어나면 발사만 보류하고 락은 유지하는데, `EnemyAiStateSystem` 미러가 같은 규칙이라 fire 타겟 없음 → **`Marching`** → 골로 계속 걸어간다.

**바로 옆에 방어유닛이 있어도 영원히 무시한다.** 재현 경로는 Focus + 전진 이동을 함께 가진 적 — Needler(`Advance`), Rootcaster·Vanguard(`Pulse`). 방어유닛 재배치나 넉백으로도 같은 상태에 빠진다.

사용자 원칙 2가 «범위 이탈»을 해제 사유로 명시했는데 **코드는 정반대**다.

---

## 현황 — 지속성 장치는 어디에 있나

| 장치 | 대상 | 해제 | 원칙 대비 |
|---|---|---|---|
| `FocusTarget` + `EnemyTargetMode.FocusUntilDead` | **적 6종**(Basic·Kindler·Needler·Rootcaster·Sniper·Vanguard) | 대상 사망만 | **B2** |
| `Aggroed` sticky override | 적(보스 면제) | 가디언 소멸·필드 무효화 | **일치** |
| `FrontmostAttackLock` | 방어유닛 中 「끝을 보는 눈」 부착분 | **RESOLVE 마다** | 지속성 장치가 아님(wind-up desync 방지) |
| `AttackState.committedDirection` | `DirectionalLinear` 탄 | RESOLVE | 타겟이 아니라 **방향** 보존 |

**방어유닛에는 지속 락이 없다.** 매 프레임 `bestTarget = Entity.Null` 로 시작해 사거리 내 최근접을 재계산한다(`:442~544`). 적은 절반이다 — `Nearest` 모드 4종(**보스 2종 포함**: Tanker·Debuffer·Boss_Nightmare·Boss_Jjangssen)이 매 프레임 재선정한다.

부수 효과로 **동거리 flip-flop** 도 있다 — 타이 브레이크에 히스테리시스가 없어(`d2 < bestSq` 엄격 비교) 나란히 걸어오는 적 2기 사이에서 프레임 단위로 진동한다.

---

## 작업 단위

| # | 작업 구분 | 내용 | 위험 |
|---|---|---|---|
| **0** | **공격 1회 커밋** | `FrontmostAttackLock` 의 카드 게이트를 풀어 **모든 공격자**가 START 에서 타겟을 고정하고 RESOLVE 에서 그것을 때리게 한다. 타겟팅 **정책은 건드리지 않는다** — 「어떻게 고르나」가 아니라 「고른 뒤 안 바뀐다」만 | 낮음. B1 해소 |
| **1** | **순수 술어 추출** | `TargetPersistence.Keeps(alive, inRange, releaseOnExit) → bool` 을 `Combat/` 에 두고 `AttackSystem` 과 `EnemyAiStateSystem` 이 **같은 함수를 호출**. 지금은 미러가 두 벌이고 `EnemyAiStateSystem` 주석이 *"⚠ 동기화 필요"* 를 이미 경고한다 | 낮음. 미러 드리프트를 구조로 차단 |
| **2** | **적 — 범위 이탈 해제** | `FocusUntilDead` 의 사거리 이탈을 **락 해제 + 이미 계산된 nearest 채택**으로. 미러 동시 수정 | 중. **B2 해소, 밸런스 변경** |
| **3** | **적 — `Nearest` 모드에도 락** | `FocusTarget` 을 공격 가능한 전 적에게 부착하고 게이트를 `targetMode != None` 으로 완화 | 중. 원칙 2 완성 |
| **4** | **방어유닛 락** | facing override **직후**에 방어유닛 sticky 블록 삽입. 게이트 = `!facing ∧ !frontmost ∧ !healer ∧ !guardian`. 해제 = 사망 ∨ 사거리 이탈. 골 락 금지 가드 복사 | 높음. 원칙 1 · **미결 D1 선행** |

**순서 근거**: 0 은 타겟팅 정책을 안 건드리는 순수 결함 수정이라 먼저 넣어도 밸런스가 안 움직인다. 1 이 술어를 하나로 만들어야 2·3 이 미러를 깨지 않는다. 4 는 밸런스 영향이 가장 크고 D1 이 걸려 있어 마지막.

**신규 컴포넌트 0 · 신규 시스템 0 · 신규 이벤트 채널 0.** 편집은 `AttackSystem` 블록 2개(+1 삽입) · `EnemyAiStateSystem` 미러 1개 · `BattleBridge` 부착 2줄 · 순수 함수 파일 1개.

---

## 미결 결정

**D1 — 가디언 어그로 자석과 원칙 1이 정면 충돌한다.** `AggroCapacity > 0`(Guardian·Bastion·ShieldShuttle)는 RESOLVE 에서 `AggroTargeting.SelectTargets` 로 **두 번째 독립 선정**을 하고 `bestTarget` 을 덮는다(`:1157~1218`). 그 Pass A 가 *"여유가 있으면 **아직 어그로 안 걸린 적** 우선"* 인데, **이게 신규 팩을 흡수하는 자석의 작동 원리**다. primary 를 고정하면 자석이 죽는다.

후보: (a) 가디언은 sticky 제외 — 자석 보존, 원칙 1의 명시적 예외 (b) "어그로 미보유 적 등장"을 해제 축으로 추가 (c) 원칙 1을 가디언에도 강제하고 자석을 포기.
**권고 (a).** unit 4 착수 전 확정 필요.

**D2 — `FocusUntilDead` 라는 이름과 원칙 2의 충돌.** unit 2 를 적용하면 «죽을 때까지»가 «죽거나 사거리를 벗어날 때까지»가 된다. SO 저작 6종의 의도가 후자였는지 확인 필요. 대안: 이탈 후 **N초 유예** 뒤 해제(추격 중 일시 이탈을 살림).

---

## Feature-wide 계약

1. **«어떻게 고르나»와 «고른 뒤 안 바뀐다»를 분리한다.** 이 spec 은 후자만 다룬다. 우선순위 사슬(nearest·priority·filter·힐러·facing·frontmost·골 최후순위)의 **선정 규칙은 한 줄도 바꾸지 않는다**.
2. **명시적 타겟팅 규칙을 가진 유닛은 제외한다** — 힐러(lowest-health 재랭킹이 정체성) · facing 유닛(레인 witness 는 타겟이 아니라 발사 게이트) · 「끝을 보는 눈」(카드 계약이 *"매 공격마다 지금의 최전방"*) · 가디언(D1). 제외는 **누락이 아니라 계약**이므로 각각 이유를 코드 주석에 남긴다.
3. **골은 잠그지 않는다.** 골을 락하면 «방어유닛이 배치되면 골을 놓는다»는 goal-stability 계약이 깨진다. 기존 가드(`:659~665`)를 신규 락에도 복사한다.
4. **술어는 한 벌이다.** `AttackSystem` 과 `EnemyAiStateSystem` 이 같은 순수 함수를 호출한다. 두 벌이면 «락은 있는데 FSM 은 Marching» 데드락이 재발한다.
5. **어그로 override 는 최상위를 유지한다.** sticky 블록이 aggro 위로 올라가면 즉시 깨진다.
6. **도발 해제 시 락을 비운다.** `TauntAttackGranted` strip 경로가 `AttackState` 를 통째로 제거하는데 `FocusTarget` 은 남아 stale 락이 된다.

## 예상 파급 (착수 시 확인)

sticky 는 CC/스택을 **분산 → 집중**으로 바꾼다: 넉백·수면 on-hit 이 같은 적에 반복되고, 드림캐쳐 payload(`frost_arrow`·`ember_bite`·니들)도 한 대상에 몰린다. HeavyStrike 의 HP 게이트는 같은 대상을 반복 평가해 **발동이 안정화**된다(개선). 빔 레인저의 «뷰는 START target, 데미지는 RESOLVE pick» 불일치는 unit 0 이 **없앤다**(개선).

기대값 갱신 대상 테스트: `AttackSystemMaskTests` · `AttackSystemUnifiedLoopTests` · `EnemyTargetPriorityTests` · `GoalTargetingPriorityTests` · `LowestHealthTargetingTests` · `FrontmostAttackLockTests`. **일괄 갱신 금지** — 2프레임 이상 돌리는 케이스만 기대값이 바뀐다.

## 파이프라인 커버리지

**N/A** — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음. 타겟 **선택의 지속성**만 바뀐다.

## 후속 후보

- **동거리 히스테리시스** — unit 4 가 들어가면 flip-flop 은 자동 소멸한다. 별도 장치 불필요.
- **`Nearest` 보스 2종의 타겟 정책** — Boss_Nightmare·Boss_Jjangssen 이 매 프레임 재선정하는 것이 의도인지 저작 확인.
