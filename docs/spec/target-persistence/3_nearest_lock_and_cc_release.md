# unit 3 — 적 `Nearest` 모드에도 락 + CC 해제 재선정

## 목적

**원칙 2의 나머지 절반을 채운다.** 지금 락은 `FocusUntilDead` 적 6종에만 있고, `Nearest` 4종(Tanker·Debuffer·**Boss_Nightmare·Boss_Jjangssen**)은 매 프레임 재선정한다.

동시에 **해제 사유 하나를 추가**한다 — «자기 CC 해제»(D5). 기절/수면에서 깨어난 유닛이 그 사이 세상이 바뀌었는데도 옛 타겟을 고집하지 않게 한다.

두 변경을 한 unit 에 묶는 이유: 둘 다 **같은 블록**(`AttackSystem` 의 focus 락 블록)을 편집하고, CC 규칙은 락이 넓어질수록 의미가 커진다. 따로 넣으면 같은 자리를 두 번 연다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `FocusTarget` 부착 조건 완화 (`FocusUntilDead` → `!= None`)
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 락 블록 게이트 완화 + CC 비움
- 수정: `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs` — 미러 게이트 동시 완화
- 수정: `Assets/_Project/Scripts/Data/EnemyBehaviorEnums.cs` — `Nearest` 의미 주석 갱신
- 신규: `Assets/_Project/Tests/EditMode/NearestLockTests.cs`

## 구현

### 게이트 완화 — 두 곳을 **같이** 고친다

```
targetMode == FocusUntilDead   →   targetMode != None
```

`AttackSystem` 과 `EnemyAiStateSystem` 미러 **양쪽**이다. 한쪽만 고치면 «락은 있는데 FSM 은 Marching» 데드락이 재발한다 — 그것이 B2 의 절반이었고 계약 4가 못박은 지점이다. 유지 판정은 이미 `TargetPersistence.KeepsLock` 하나를 공유하므로 게이트만 맞추면 된다.

**보스를 제외하지 않는다**(D4). `BossTag` 분기를 넣지 않는 것이 곧 구현이다.

### CC 비움 — 전이 감지기를 만들지 않는다

```
행동정지 CC 중  →  락을 비운다  →  깨어나면 자연히 새로 고른다
```

`actionLocked`(`AttackSystem:251`)는 **`continue` 하지 않는다** — CC 는 START 만 막고 타겟 선정 사슬은 계속 돈다. 그래서 락 블록(`:645`)에서 `actionLocked` 를 그대로 읽을 수 있고, «직전 프레임에 CC 였나»를 기록할 **상태 필드가 필요 없다**.

CC 중엔 어차피 공격을 시작할 수 없으므로 비워도 잃는 것이 없다. 미러는 focus 를 **읽기만** 하고(쓰기는 `AttackSystem` 단독) 비어 있으면 nearest 경로로 흐르며, CC 중 AI 상태는 `MovementSystem` 이 자체 `locked` 로 정지시켜 무의미하다.

### `committedTarget` 은 건드리지 않는다

층이 다르다. 기존 계약이 *"이미 시작된 스윙의 RESOLVE 는 완료"* 이므로 **스윙 도중 CC 를 맞아도 그 한 방은 겨눈 대상에 꽂힌다.** 그다음 공격부터 새로 고른다.

## 예상 파급 — 이건 버그 수정이 아니라 **게임 변경**이다

`Nearest` 적이 한 대상에 붙으면 CC·스택·드림캐쳐 payload 가 **분산 → 집중**된다. 보스 2종은 CC 면역이라 해제 사유가 사망·이탈뿐이라 특히 집요해진다.

**동거리 flip-flop 은 자동 소멸**한다(타이 브레이크 히스테리시스 불요).

## 완료 기준

- [x] compile 에러 0 · **EditMode 2051 중 2048 통과 · 실패 0**
- [x] 신규 7건 — ① `Nearest` 락 유지 ② 이탈 해제(**음성 대조군** — 없으면 ①이 "그냥 안 바뀐다"와 구분 안 됨) ③ 보스도 같다 ④ CC 중 비움 ⑤ 깨어나면 새로 고름 ⑥ CC 중 `committedTarget` 유지 ⑦ 미러가 `Marching` 으로 안 떨어짐
- [x] **기존 기대값 갱신 0건** — 선정 규칙을 안 건드렸다는 증거
- [x] **라이브 카운터** (보스 플랜, 방어유닛 16기) — 아래 §실측
- [ ] Play 체감: 집중이 세져 한 마리가 계속 묶이는 것이 의도대로인지

### ⚠ 초판 결함을 신규 테스트가 잡았다

락을 비우기만 하고 아래로 흘려서, 해제 분기가 그 프레임의 최근접으로 **즉시 다시 잠갔다.** `else` 로 감싸는 것이 핵심이었고 `Cc_ClearsTheLock_WhileActionLocked` 가 빨갛게 났다. **증상 단언을 먼저 쓴 것이 값을 한 첫 사례**다(`CLAUDE.md` 버그 수정 절차).

## 실측 — 락은 맞는데 **체감 변화는 작다**

```
[Nearest · unit 3 신규]  관측 10,787 · 전환 1 · 사유없는전환 0 · 예전이라면 갈아탔을 0
[Focus  · 기존]          관측 28,506 · 전환 2 · 사유없는전환 0 · 예전이라면 갈아탔을 0
```

**「예전이라면 갈아탔을 = 0」** — 라이브에서 unit 3 이 결과를 바꾼 순간이 한 번도 없었다. 이유는 `Nearest` 4종의 이동 정책이다:

| | engageMovement |
|---|---|
| Boss_Nightmare · Boss_Jjangssen · Tanker | **Halt** |
| Debuffer | **Advance** |

`Halt` 는 교전 시작 시 **멈춘다.** 안 움직이니 최근접이 안 바뀌고, 락이 있든 없든 같은 대상을 계속 팬다. 즉 **보스는 이미 사실상 한 놈만 패고 있었고 unit 3 은 그걸 «보장»으로 바꿨다.**

락이 실제로 결과를 바꾸는 경우는 둘뿐이다:
1. `Advance`/`Pulse` 적 — `Nearest` 중엔 **Debuffer 하나**
2. **교전 중 더 가까운 방어유닛이 새로 배치/재배치될 때** — 라이브 러너에선 안 일어났고, EditMode `NearestEnemy_KeepsLock_WhenACloserDefenderAppears` 가 그 케이스를 결정론으로 고정한다

### 계측 방법에서 배운 것

**모드별로 가르기 전엔 남의 성과를 제 것으로 읽을 뻔했다.** 첫 러너는 «갈아탔을 523» 을 보여줬는데 **전부 기존 Focus 적** 것이었다. `Nearest` 만 따로 세고서야 0 이 드러났다.

> 지표를 만들 때는 **«이 수가 내 변경 때문에 움직이는가»** 를 먼저 물을 것. 합계는 무관한 원인으로도 커진다.

첫 웨이브 플랜(`WavePlan_Sample`)엔 `Nearest` 적이 **0기**였다 — 그 러너의 «위반 0» 은 공허했다. 보스 플랜으로 바꿔서야 관측이 생겼다.

## unit 4 에 대한 함의

**방어유닛 락이 체감 변화의 본체일 가능성이 높다.** 방어유닛은 안 움직이는데 **적이 계속 흘러가므로 최근접이 매 순간 바뀐다** — 락이 진짜 일하는 자리다. unit 3 의 «갈아탔을 0» 을 unit 4 의 기대치로 옮기지 말 것.
