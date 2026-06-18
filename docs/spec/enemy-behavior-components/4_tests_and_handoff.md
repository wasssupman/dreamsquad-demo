# Unit 4 — 테스트 + handoff

## 목적

거동 핵심을 회귀 방지 수준으로 고정하고 feature 종료.

## 변경 대상

- (신규) `Assets/_Project/Tests/EditMode/EnemyBehaviorTests.cs`
- (신규) `docs/spec/enemy-behavior-components/5_handoff_summary.md`

## 구현 (EditMode)

AttackSystem 을 World 에 띄우고 엔티티 수동 구성:

1. **FocusUntilDead 잠금**: focus 적 + 타겟 A(가까움) + 타겟 B. A 잠금 후 더 가까운/우선 B 등장해도 A 유지. A 사망 시 B 로 재선정.
2. **Focus 사거리 밖 발사 보류 (Critic M2)**: 잠근 A 가 사거리 밖이면 발사 안 함(IncomingDamage 0) + lock 유지. 다시 사거리 안이면 발사.
3. **Nearest vs Focus**: Nearest 적은 매 틱 최근접 갱신, Focus 적은 불변.
4. **aimMode 정지**: StopToAttack(movePause>0) 적은 발사 시 **MovementPauseRequest 큐 enqueue** 로 검증(결정적 신호), MoveAndShoot 적은 미발생.
5. **walk-only / 방어적 bake (Critic C1)**: attackMethod None → AttackState 없음. **attackMethod Melee + outputs 빈 적도 AttackState 없음**(데미지-0 공격자 생성 안 함).
6. **filter priority(회귀)**: SO 기반 priorityClass 가 기존 EnemyTargetPriorityTests 와 동일 동작(중복 최소화).

## 완료 기준

- [x] EditMode 신규(EnemyBehaviorTests 5종: lock/hold/reselect, 사거리밖 hold-fire, Nearest 재선정, aimMode 정지/비정지) 통과 + 전체 342 중 340 pass/0 fail.
- [x] Play 검증(Unit 2/3): 6종 컴포넌트 bake + focus lock + aimMode 정지 게이팅.
- [x] `5_handoff_summary.md` 작성.
- [x] README 상태 "완료" + 후속 후보 갱신.

> walk-only/방어적 bake(Melee+outputs빈→no AttackState)는 BattleBridge bake 경로라 Play reflection(Unit 2)으로 검증(EditMode 는 AttackSystem 단위).

완료: 2026-06-18 / 커밋 해시 `3ff2fe7`
