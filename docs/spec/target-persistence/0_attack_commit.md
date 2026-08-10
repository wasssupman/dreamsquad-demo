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
- [x] **e2e 실측** (`Crowd_OvertakingStream_HitAlwaysMatchesTheCommittedTarget`) — Artillery 형(사거리 7) 하나 vs 서로 다른 속도로 흘러오는 적 12기, 4000프레임(≈64초). 속도는 index 기반 결정론(난수 없음):

  ```
  START 125 · RESOLVE 125 · 불발 0 | 겨눈≠맞은 0 | 예전이라면 어긋났을 39 (31%)
  ```

  · **겨눈 대상 ≠ 맞은 대상 = 0.** 125회 전부 일치.
  · **31% (125회 중 39회)** 는 RESOLVE 시점의 최근접이 커밋 대상과 달랐다 — **예전 코드였다면 그만큼 엉뚱한 적을 때렸다**. 이 수가 0 이면 시나리오가 결함을 자극조차 못 한 것이라 위의 «0» 이 공허해지므로, 테스트가 `wouldHaveMismatched > 0` 도 함께 단언한다.
  · **불발 0** — strict lapse 로 인한 빈 스윙이 이 시나리오에선 한 번도 없었다(적이 죽지 않고 사거리를 유지하는 조건). 실전에서 대상이 죽는 빈도는 별개이므로 Play 체감으로 확인한다.
- [x] ① 겨눈 대상 = 맞은 대상 — 위 e2e 가 125회 전수 확인(`겨눈≠맞은 = 0`). 별도 육안 불요
- [x] **빈 스윙 발생률 실측** (2026-08-10, 라이브 배틀 · 방어유닛 12기 밀집):

  ```
  스윙 203회 · lapse 28회 = 14%
     내역: 대상 사망 25 · 사거리 이탈 3
  ```

  거의 전부가 «**남이 먼저 죽였다**»다. 여러 유닛이 같은 적을 쏘면 내 wind-up 중에 적이 죽어 스윙이 빈다. 밀집 배치라 **상한에 가까운 수치**이고, 성기게 놓으면 낮아진다.

  이것이 B1 수정의 **대가**다:

  | | 겨눈≠맞은 | 빈 스윙 |
  |---|---|---|
  | strict lapse (현재) | 0 | 14% |
  | 재조준 (예전) | 31% | 0 |

- [x] **Play 육안(체감)**: 적이 몰린 구간의 헛스윙 — **거슬리지 않음**(2026-08-10 사용자 확인). 14% 를 수용값으로 확정한다. 나중에 이 수치가 문제로 올라오면 해법은 «재조준으로 되돌리기»(= B1 부활)가 **아니라** 연출로 감추는 쪽(빗나감 모션·쿨다운 단축)이고, 그건 별도 spec 이다

### ⚠ 계측 설계에서 두 번 틀렸다 (방법 기록)

1. 첫 계측은 «**때려서 죽인 것**»과 lapse 를 구분하지 못했다 — RESOLVE 시점의 대상 무효를 그냥 셌다. `hasCommittedTarget` 이 1인 **wind-up 창 안에서만** 세도록 고쳤다.
2. 두 번째는 에피소드 키가 `공격자>대상` 이라 **반복 공격이 1회로 접혔다**. 스윙 시작(`hasCommittedTarget` 0→1)을 키로 바꿔 세 번째에 스윙당 비율이 나왔다.

«비율»을 잴 때는 **분모의 정의**를 먼저 못박을 것.

---

**완료 기준 확인**: 2026-08-10 · 사용자 확인 완료 (① 겨눈=맞은 전수 · ② 빈 스윙 14% 체감 문제없음)
