# 6 — Handoff Summary

> enemy-ai-fsm 구현 종료 인계 지도. 최신 계약은 README/번호 문서가 우선. 구현 상세는 코드+커밋.

## Commit

체인 (모두 main):
- `d8e3795` spec(enemy-ai-fsm) 분산 스펙
- `81cfda1` u0 EnemyAiState enum/컴포넌트 + engageMovement plumbing
- `7bc7b5d` u1 EnemyAiStateSystem 전이 + EditMode 전이 테스트
- `db438e7` u2 MovementSystem 상태 기반 이동
- `d294b09` u3a AttackSystem 상태 기반 fire
- `0b0db41` u3b 레거시 pause/aimMode 일괄 제거
- `ea01fa7` u4 적 SO 9종 engageMovement 마이그레이션
- `412c320` u5 FSM 이동 회귀 테스트 3종 + smoke FSM 커버리지

(`ed23c1b` desert-theme 은 u0~u1 사이 interleave 된 무관 커밋)

## Implemented

- `EnemyAiState { Marching, Engaging, Chasing, Standoff }`(Combat 소유) 단일 상태로 행동 1급화.
- `EnemyAiStateSystem`(UpdateAfter TauntAttackGrant, UpdateBefore Movement)가 상태 전이 단독 소유. aggro면 가디언 tile-Chebyshev 사거리로 Standoff/Chasing, 아니면 `HasFireTarget`(AttackSystem fire 조건 미러) 결과로 Engaging/Marching.
- MovementSystem: Standoff/Engaging-Halt 정지, Chasing 가디언 self-walk, Marching/Engaging-Advance flow. EnemyAiState+EnemyBehavior.engageMovement RO 소비.
- AttackSystem: fire 게이트 = `Engaging | Standoff`(+ defender start). aimMode/pause enqueue 제거.
- 레거시 완전 제거: aimMode, movePauseOnAttackSec, EnemyAttackMovePause, MovementPauseRequest(+큐), MovementPauseRequestDrainSystem. NativeQueue 채널 16→15.
- 적 SO 9종 engageMovement 확정 — Advance: Debuffer/Needler/Runner · Halt: Vanguard/Basic/Tanker/Rootcaster/Sniper/Swift.

## Key Files

- `Assets/_Project/Scripts/Battle/Combat/EnemyAiState.cs` · `EnemyAiStateSystem.cs`(HasFireTarget 미러)
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`(분기 순서: Standoff→Chasing→portal→goal→tornado→Engaging-Halt→flow)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`(aiStateLookup fire 게이트) · `EnemyBehavior.cs`(targetMode+engageMovement)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`(enemy bake engageMovement 직접)
- 테스트: `Tests/EditMode/{EnemyAiStateTransitionTests, EnemyAiStateSystemTests, AttackSystemStateGateTests, MovementSystemTests, EnemyBehaviorTests}.cs` · `Tests/PlayMode/MovementIntegritySmokeTest.cs`

## Verified

- EditMode: MovementSystemTests 11/11, FSM 전이/게이트 스위트 PASS. 풀 스위트는 6건 "Destroy may not be called from edit mode"가 직전 PlayMode(Battle 씬) 잔류 거짓실패 → 도메인 리로드(RequestScriptReload) 후 해당 7건 PASS 재확인. ObstaclePlacer 1건은 기존 flaky(맵 도메인, FSM 무관).
- PlayMode: MovementIntegritySmokeTest 1/1 — FSM 스택 라이브 구동, aggro Chasing→Standoff→데미지 검증.
- 리뷰: u2 양트랙·u3b 양트랙·u4 데이터·u5 ecs 모두 APPROVE. u5 L1(Chasing 정확값 단언) 반영.

## Notes

- **되돌리면 안 됨**: 전이 판정이 AttackSystem fire 조건을 미러하는 구조(상태=Engaging ⟺ fire 타겟 존재). FocusUntilDead 데드락 방지의 핵심. 미러 동기화 책임은 `EnemyAiStateSystem.cs` 주석이 짐.
- portal/tornado 는 Engaging-Halt 게이트 **이전**이라 Halt 적도 스킬캐리어에 반응(직교성). 단 Standoff/Chasing 은 early-return 으로 **면역**(비대칭, 기존 동작).
- M1 caveat 해소: u3b 단독 시점엔 미마이그레이션 SO(Debuffer/Needler)가 임시 Halt 였으나 u4 에서 즉시 후행 마이그레이션 완료.
- CC(stun/slow/impulse)는 현재 이동/공격을 멈추지 않음(미구현). FSM 이 이 동작 보존, stun 통합은 후속(H1).

## Follow-up

- ⏳ **라이브 육안 검증(필수, 사용자)**: 에디터 **포커스** Play 로 — Vanguard(Halt) 디펜더 사거리 진입 시 정지+공격·처치 후 행진, Advance 적(Debuffer/Needler/Runner) 이동하며 공격, aggro 적 Chasing→Standoff→가디언 사망 시 행진 복귀, 콘솔 에러/leak 0. 확인되면 README 상태를 "완료"로 전환.
- Standoff/Chasing 스킬캐리어 면역 음성 테스트 또는 의도 확정 (ecs-review u5 M1, README 후속 후보).
- Engaging-Advance + 스킬캐리어 경로 테스트 (README 후속 후보).
- README "후속 후보" 의 진동형 Halt / deadband / stun 직교게이트(H1) / 타겟 스캔 1패스 공유(M2) 등은 별도 spec 대기.
