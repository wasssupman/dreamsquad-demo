# 4. Tests And Handoff

## 목적

0~3 단계의 migration 회귀를 한 번 더 묶어 검증하고 handoff 를 작성한다. 본 문서는 구현 cleanup 과 검증 문서화 단위다.

## 변경 대상

- `Assets/_Project/Tests/EditMode/*`
- `docs/spec/modifier-legacy-migration/README.md`
- `docs/spec/modifier-legacy-migration/5_handoff_summary.md`
- 필요 시 `docs/spec/README.md` Promoted 항목

## 구현

1. EditMode 테스트를 보강한다.
   - defender outputs damage dispatch
   - enemy outputs damage dispatch
   - MoveSpeedMul 합성식과 MovementSystem 소비
   - MovementPauseRequest drain/update
2. PlayMode smoke checklist 를 실행한다.
   - 기존 defender damage
   - healer heal
   - basic/swift/tanker enemy damage
   - 신규 적 3종 공격/이동
   - slow hazard 또는 slow projectile
3. `README.md` 상태를 완료로 갱신하고 `5_handoff_summary.md` 를 작성한다.
4. 최상위 `docs/spec/README.md` 의 Follow-up Backlog 에서 본 spec 링크를 Promoted 로 이동한다.

## 완료 기준

- [x] Unity compile error 0.
- [x] 관련 EditMode 테스트 통과.
- [x] PlayMode smoke 결과와 남은 리스크가 handoff 에 기록됨.
- [x] Follow-up Backlog 가 중복 항목 없이 정리됨.

검증:
- 2026-05-01: full EditMode 181 total / 179 passed / 2 ignored, failed 0.
- 2026-05-01: Play Mode enter/exit smoke, console error 0.
