# unit 0 — 공격 1회 타겟 커밋 (겨눈 대상을 때린다)

## 목적

**START 에서 겨눈 대상을 RESOLVE 에서 때린다.**

지금은 START 게이트가 `hitDelayRemaining == 0` 일 때만 걸리는데(`AttackSystem.cs:752`) **타겟 선정 사슬은 매 프레임 무조건 돈다**(`:442~739`). RESOLVE(`:858`)는 **그 프레임의 새 `bestTarget`** 을 쓴다. 방어유닛 26종 중 24종이 `hitDelaySec: 0.3` 이라 창이 상시 열려 있고, 애니/빔은 A를 향하는데 데미지·투사체·넉백·수면·드림캐쳐 payload 는 B로 간다.

**타겟팅 정책은 건드리지 않는다** — 「어떻게 고르나」가 아니라 「고른 뒤 한 공격 안에서 안 바뀐다」만 다룬다. 그래서 이 unit 은 **밸런스를 움직이지 않는다**(같은 대상을 한 번 더 때릴 뿐, 누구를 고르는지는 그대로).

## 새로 발명하지 않는다 — 부품 둘이 이미 그 형태다

| 기존 부품 | 무엇을 하나 | 이 unit 과의 관계 |
|---|---|---|
| `AttackState.committedDirection` / `hasCommittedDirection` | START 에서 **방향**을 스냅샷 → RESOLVE 에서 해제 (`:774~779`, `:1639~1643`) | **수명·필드 형태를 그대로 복제**한다. 방향 옆에 대상이 붙는 것뿐 |
| `FrontmostAttackLock` wind-up 블록 (`:691~724`) | 락 대상을 **생존 + 사거리** 검사 후 유지, 실패면 strict lapse | **판정 로직을 그대로 복제**한다. 카드 게이트만 없앤 형태 |

즉 신규 개념이 없다. **필드 2개 + 블록 1개**다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackState.cs` — `committedTarget` / `hasCommittedTarget` 신설
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — START 커밋 · wind-up 유지 블록 · RESOLVE 해제
- 신규: `Assets/_Project/Tests/EditMode/AttackCommitTests.cs`

## 구현

### 1. 상태 (`AttackState`)

`committedDirection` 바로 옆에 같은 모양으로:

```csharp
public Entity committedTarget;
public byte   hasCommittedTarget;
```

Combat 맥락 소유. 수명은 **START → RESOLVE 1회**이며 그 밖에서는 항상 비어 있다.

### 2. wind-up 유지 블록 — 배치가 핵심이다

frontmost 블록 **직후**, facing override **직전**에 넣는다. 이 자리여야 하는 이유가 각각 있다:

| 앞에 있는 것 | 왜 이 unit 이 덮지 않나 |
|---|---|
| **어그로 sticky**(`:672~687`) | 사용자 원칙 2가 «어그로 끌림»을 **변경 사유로 명시**했다. 게이트 `!aggroLookup.HasComponent` 로 비켜준다 |
| **frontmost**(`:691~724`) | 같은 일을 이미 하고 있다(strict lapse 포함). 게이트 `!wantFrontmost` 로 중복을 피한다 |

| 뒤에 있는 것 | 왜 그쪽이 이겨야 하나 |
|---|---|
| **facing override**(`:731~739`) | 레인 witness 는 «타겟»이 아니라 **발사 게이트**다. 이 unit 은 그 축을 모른다 — 뒤에 있으므로 자동으로 facing 이 이긴다(별도 게이트 불필요) |

판정은 frontmost 와 **같은 규칙**을 쓴다 — 생존(`Health > 0` ∧ `!DeadTag`) ∧ 사거리(체비셰프 ≤ `tileRange`). 실패면 `bestTarget = Entity.Null`(**strict lapse** — 재선정하지 않는다). `PastGoalTag` 는 해제 사유가 **아니다**(goal-tower-siege unit 1 선례 — 골에 붙은 적은 살아 있는 유효 대상이다).

### 3. 커밋과 해제

- **START**: `bestTarget` 을 `committedTarget` 에 저장. `hitDelaySec == 0`(즉시 RESOLVE)이어도 저장했다가 같은 프레임에 해제한다 — 분기를 늘리지 않는다.
- **RESOLVE**: `committedDirection` 해제 바로 옆에서 함께 비운다(`:1639~1643`).

## 이 unit 이 고치지 않는 것 (정직하게)

**가디언(`AggroCapacity > 0` — Guardian·Bastion·ShieldShuttle)은 RESOLVE 에서 `bestTarget` 이 한 번 더 덮인다**(`:1157~1218`, `AggroTargeting.SelectTargets` → primary 대입 `:1210~1213`). 이 unit 은 **그 블록을 건드리지 않는다** — 거기서 *"여유가 있으면 아직 어그로 안 걸린 적 우선"* 이 신규 팩을 흡수하는 자석의 작동 원리라, 손대면 어그로 설계가 바뀐다.

따라서 **가디언 3종에 한해 B1 이 부분적으로 남는다.** 해소는 README 의 D1(가디언 sticky 예외) 확정 후 unit 4 소관이며, 그때 기존 `keepFrontmostPrimary`(`:1193~1207`)를 일반화하면 된다.

## 완료 기준

- [x] compile 에러 0 · EditMode **1993 중 1990 통과 · 실패 0**(나머지 3은 기존 `[Ignore]`)
- [x] 신규 테스트 7건 (`AttackCommitTests`): ① wind-up 중 더 가까운 적이 나타나도 커밋 대상이 유지된다 ② 커밋 대상이 죽으면 strict lapse(재선정 없음) ③ 사거리 이탈도 lapse ④ `PastGoalTag` 는 lapse 사유가 아니다 ⑤ RESOLVE 후 커밋이 비워진다 ⑥ `hitDelaySec == 0` 이면 거동 불변 ⑦ 다음 공격은 새로 고른다(커밋은 공격 1회만 산다)
- [x] **기존 타겟팅 테스트 6종이 기대값 갱신 0 으로 그대로 그린** — `AttackSystemMaskTests` · `AttackSystemUnifiedLoopTests` · `EnemyTargetPriorityTests` · `GoalTargetingPriorityTests` · `LowestHealthTargetingTests` · `FrontmostAttackLockTests`. **이것이 «타겟팅 정책을 안 건드렸다»의 증거다** — 하나라도 기대값을 고쳐야 했다면 선정 규칙이 움직인 것이다
- [ ] **Play 육안**: 빔 레인저가 겨눈 적과 실제로 맞는 적이 일치한다(지금은 어긋난다)

---

**완료 기준 확인**: (미확인)
