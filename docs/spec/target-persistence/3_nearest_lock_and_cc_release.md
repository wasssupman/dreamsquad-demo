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

- [ ] compile 에러 0 · EditMode 실패 0
- [ ] 신규: ① `Nearest` 적이 더 가까운 대상이 나타나도 락을 유지 ② 보스도 같다 ③ CC 중 락이 비워진다 ④ 깨어나면 새로 고른다 ⑤ CC 중에도 `committedTarget` 은 유지된다(스윙 완주) ⑥ 미러가 같은 게이트를 쓴다(락 있는데 `Marching` 인 조합이 없다)
- [ ] 기존 타겟팅 테스트 — **2프레임 이상 돌리는 케이스만** 기대값이 바뀐다. 일괄 갱신 금지
- [ ] **라이브 카운터**(units 1·2 와 같은 방식): «사유 없는 타겟 전환 = 0» · 락 유지 중 평균 연속 타격 수를 before/after 로 기록
- [ ] Play 체감: 집중이 세져 한 마리가 계속 묶이는 것이 의도대로인지
