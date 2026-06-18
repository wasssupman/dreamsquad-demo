# Unit 6 — 테스트 + handoff

## 목적

어그로 핵심 계산을 회귀 방지 수준으로 고정하고 feature 를 종료한다.

## 변경 대상

- (신규) `Assets/_Project/Tests/EditMode/AggroAssignmentTests.cs` (또는 기존 EditMode 테스트 폴더 규칙 따름)
- (신규) `docs/spec/aggro-targeting/7_handoff_summary.md`

## 구현 (EditMode 단위 테스트)

`AggroAssignmentSystem` 을 World 에 띄우고 엔티티를 수동 구성해 검증:

1. **capacity 상한**: 가디언 capacity=2, 사거리 내 적 4 → 정확히 2 마리 `Aggroed`.
2. **근접 우선**: 사거리 내 적이 capacity 초과면 가까운 적부터 배정.
3. **선점 고정**: 적 1을 가디언 A 가 선점 후, 가디언 B 사거리에 들어와도 A 유지.
4. **해제(가디언 사망)**: 가디언 Health=0/DeadTag → 링크 적 전부 `Aggroed` 제거.
5. **해제 후 count 복구**: 해제된 슬롯에 새 적이 다시 배정됨.
6. **도발 공격 토글**: outputs 없는 적이 어그로 시 `AttackState` 획득, 해제 시 제거.
7. **공격필터 우선순위**: 가디언+레인저 동시 사거리에서 Shooter 적이 레인저 선정(더 가까운 가디언이 있어도). 비-Shooter 적은 최근접.

이동/sticky 는 순수 계산이 적어 PlayMode smoke 1개로 갈음:
- 가디언+적 배치 → 적이 가디언으로 수렴·겹침, 가디언 사망 후 적이 출구로 이동.

## 완료 기준

- [x] EditMode 테스트 통과. (`AggroAssignmentTests` 5/5: capacity/선점/해제/재배정/도발 grant·strip)
- [x] EditMode 전체 회귀 없음. (334 중 332 pass / 0 fail / 2 기존 ignore)
- [x] Play smoke 통과(콘솔 에러 0). (Unit 1~5 각 Play reflection 검증)
- [x] `7_handoff_summary.md` 작성.
- [x] README 상태 "완료" + 후속 후보 갱신.

> 단위 테스트 작성 중 죽은 가디언(Health 0, 미파괴)이 획득 패스에서 재어그로하는 엣지 발견 → AggroAssignmentSystem 획득 패스에 생존 가드 추가(보강).

완료: 2026-06-18 / 커밋 해시 `<unit6-commit>`
