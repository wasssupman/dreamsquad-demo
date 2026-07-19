# 4. PlayMode 스모크 — 실전 어그로 체인 검증 + 스모크 부채 수리

## 목적

실 배틀에서 "히트 → 어그로 → 경로 추격 → 사거리 도달 → 응전" 전 체인과 타일 불변식을 검증한다.

## 발견된 테스트 부채 (수리 대상)

`MovementIntegritySmokeTest` 의 더미 가디언은 `AttackState` 가 없다. **히트 구동 전환(b84b6887, 07-09) 이후 이 가디언은 어떤 적도 때릴 수 없어 AggroHitEvent 가 0 — sawAggro 단언은 그때부터 통과 불가능**했다 (aggro-tile-chase 와 무관한 기존 실패. 타일 불변식 절반은 신형 이동으로도 통과 확인). 수리: 더미 가디언에 광역 `AttackState`(range 8) + Damage 출력 버퍼를 부여해 히트 구동 체인이 실제로 돌게 한다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/MovementIntegritySmokeTest.cs`

## 완료 기준

- PlayMode `MovementIntegritySmokeTest` green: offWalk 0 + sawAggro + 가디언 피해 (전 체인).
- EditMode 전체 무회귀.
- 기록: PlayMode 잔여 실패 3건(Gift 페이즈 진입 2, 덱 캐리인 1)은 본 spec 범위 밖 — 병행 세션 작업 영역의 기존 실패로 보고만 한다.
