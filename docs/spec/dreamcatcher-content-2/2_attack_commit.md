# 2 — AttackSystem frontmost 선택·잠금 (START/RESOLVE, 엄격 lapse)

## 목적

`끝을 보는 눈`의 타겟 정책을 `AttackSystem`에 배선한다. 기존 후보 스냅샷 루프를 재사용해(두 번째 query 없음) frontmost 후보를 추적하고, 공격 단위로 대상을 **잠그고(START) / 판정하고(RESOLVE) / 엄격 lapse**한다. damageMul 적용(±20%)은 unit 3.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Tests/EditMode/FrontmostAttackLockTests.cs` — **신규** 통합 테스트.

## 구현

### 단일 패스 추적 (계약 1)

- lookups 추가: `FrontmostAttackLock`(RW), `PastGoalTag`(RO).
- 공격자별 `wantFrontmost = Defender && FrontmostAttackLock && 살아있는 FrontmostTarget 슬롯`. 없으면 미참여. `frontmostMul` = 활성 슬롯 damageMul 곱(2장=1.44).
- 기존 nearest 후보 루프 안에서 각 후보의 `flowDist = dist[cell]`(그리드 bounds 체크, 아니면 unreachable)을 계산하고 `FrontmostTargeting.RanksBefore`로 best 추적. **PastGoal·unreachable 제외**.

### 잠금 생명주기 (계약 2/3, 엄격 lapse)

- 선택 결정(enemy focus/aggro 블록 뒤, 디펜더 전용):
  - **midAttack**(`hitDelayRemaining>0 && lock.active`): 잠긴 target을 hold. alive·!PastGoal·사거리 검증 실패 → `bestTarget=Null`(lapse). **재선택 없음**.
  - **비 midAttack**: 현재 frontmost가 있으면 잠금 후보로, 없으면 nearest fallback 유지(`fmChosenIsPriority=false`).
- **START**: `wantFrontmost`면 lock=`{active, target=bestTarget, damageMulSnapshot=frontmostMul, targetIsPriority=fmChosenIsPriority}`.
- **RESOLVE 종료**: `doResolve && wantFrontmost` → lock 초기화(hit·lapse 공통).
- 가디언(계약 5): `AggroTargeting.SelectTargets` 결과가 lock을 덮지 않게, lock.active면 `hitTargets[0]=bestTarget`(잠긴 frontmost)로 강제하고 bestTarget 재대입 skip. secondary는 기존 aggro 선정.

## 완료 기준

- [x] compile green. — 2026-07-14
- [x] `FrontmostAttackLockTests` 8/8 green: flow-dist 우선, windup hold, 사망 엄격 lapse+해제, PastGoal 정적 제외, PastGoal 전이 lapse, FlowField 부재 fallback+non-priority, **가디언 primary 강제 no-double-hit(ecs-review M1)**, 무카드 무회귀. — 2026-07-14
- [x] 기존 EditMode 스위트 무회귀: total 737 / passed 735 / failed 0 / skipped 2. — 2026-07-14
- [x] ecs-review: CRITICAL/HIGH 0, MEDIUM M1(가디언 중복) 반영(swap-to-primary). — 2026-07-14
