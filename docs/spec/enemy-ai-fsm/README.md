# Enemy AI FSM

> 상태: **units 0~5 자동검증 PASS · Pulse 모드(7~8) 진행 중** (2026-06-30)
> units 0~5(기반 FSM) 구현/커밋 완료, EditMode·PlayMode·리뷰 APPROVE. 후속으로 진동형 교전이동(Pulse) 을 units 7~8 로 추가 중. 라이브 육안 검증(기반 FSM unit 5 완료기준 3 + Pulse unit 8)은 마지막에 사용자 Play 로 일괄 확인. handoff: `6_handoff_summary.md`.

## 목표

적(공격 유닛) 행동을 명시적 유한상태머신(FSM)으로 1급화하여, 현재 컴포넌트 유무 + 숫자/enum 필드에 흩어진 행동 분기를 **단일 상태 + 이동정책**으로 완전 대체한다.

## 검증 질문

> "적이 지금 무엇을 하는지가 단일 `EnemyAiState` 로 명시되고, 이동·공격 시스템이 그 상태를 RO로 읽어 행동하며, `aimMode`/`movePauseOnAttackSec`/`EnemyAttackMovePause`/pause 큐·drain 시스템이 모두 사라졌는가? Vanguard(Halt)는 디펜더 사거리에서 멈춰 계속 공격하고, 디펜더 사망 시 다시 행진하는가?"

## 연결 문서

- 설계 요약: `docs/plans/2026-06-30-enemy-ai-fsm-design.md`
- 참조 spec: `aggro-targeting/`, `aggro-standoff/`, `attack-hit-delay/`, `enemy-behavior-components/`, `enemy-tile-movement-integrity/`

## 작업 단위

| # | 문서 | 작업 | compile-safe |
|---|---|---|---|
| 0 | `0_state_and_data.md` | `EnemyAiState` enum/컴포넌트 + `engageMovement` 필드 plumbing (레거시 공존) | ✅ |
| 1 | `1_transition_system.md` | `EnemyAiStateSystem` 전이 평가 + EditMode 전이 테스트 | ✅ |
| 2 | `2_movement_consume.md` | `MovementSystem` 상태 기반 이동으로 전환 | ✅ |
| 3a | `3a_attack_consume.md` | `AttackSystem` 상태 기반 fire로 전환 | ✅ |
| 3b | `3b_legacy_removal.md` | 레거시 제거(aimMode/movePause/EnemyAttackMovePause/DrainSystem/큐) + 테스트 정리 | ✅ |
| 4 | `4_so_migration.md` | 적 SO 9종 `engageMovement` 마이그레이션 | ✅ |
| 5 | `5_playmode_verify.md` | PlayMode smoke 갱신 + Play 검증 (자동 ✅ / 라이브 육안 ⏳) | — |
| 7 | `7_pulse_engage.md` | `EngageMovement.Pulse`(진동형) + MovementSystem Engaging 분기 + TDD | ✅ |
| 8 | `8_vanguard_pulse_migration.md` | Vanguard Pulse 전환(hitDelaySec=0.3) + Play 검증 | — |

## Feature-wide 계약

1. **상태 집합**: 컴포넌트 `EnemyAiState`(Combat 소유)가 `AiState : byte { Marching, Engaging, Chasing, Standoff }` 값 보유. 한 적은 항상 정확히 하나. lifecycle(Dead/PastGoal)은 enum 밖. **CC(stun/slow/impulse)는 현재 이동/공격을 멈추지 않음(미구현) — FSM은 이 동작을 보존하고 stun 통합은 후속(H1).**
2. **소유 맥락**: `EnemyAiState` 컴포넌트와 `EnemyAiStateSystem` 은 **Combat**. 쓰기는 `EnemyAiStateSystem` 단독. `MovementSystem`·`AttackSystem`·뷰 계층은 RO.
3. **시스템 순서**: `EnemyAiStateSystem` 은 `UpdateAfter(TauntAttackGrantSystem)` `UpdateBefore(MovementSystem)`. 전이 평가 후 Movement/Attack이 상태를 읽는다.
4. **전이 규칙**: `Aggroed` 있음 → 가디언 tile-Chebyshev 사거리 내 `Standoff`, 밖 `Chasing`. `Aggroed` 없음 → **`EnemyAiStateSystem.HasFireTarget`(AttackSystem fire 조건을 미러 — FocusUntilDead 락·필터·사거리)가 타겟을 찾으면** `Engaging`, 없음 `Marching`. 전이 판정이 AttackSystem fire 판정을 **미러**하므로 상태=Engaging ⟺ fire 타겟 존재(데드락 방지). 동기화 책임은 미러 주석(`EnemyAiStateSystem.cs`)이 지고, 공유 헬퍼 추출은 후속(아래 "타겟 스캔 1패스 공유").
5. **이동정책**: `engageMovement { Halt, Advance, Pulse }` (적 아키타입 데이터, `aimMode` 대체). **`EnemyBehavior`(Combat 소유) 필드**로 두고(aimMode 제거 후에도 targetMode 잔존) Movement 가 RO로 읽는다. `Engaging` 에서 Halt=정지+공격, Advance=flow 이동+공격, **Pulse=타격 진행중(`AttackState.hitDelayRemaining>0`) 정지·아니면 전진(진동, unit 7)**. `Chasing`=가디언 이동, `Standoff`=정지(engageMovement 무관 → Pulse 적도 어그로 시 캠프). `Marching`=flow.
6. **공격 게이트**: fire 는 `Engaging | Standoff` 에서만. `AttackState`(cooldown/range/hitDelay/outputs)·`AggroAttackProfile`·타겟 우선순위 체인은 유지.
7. **제거 대상**: `EnemyBehavior.aimMode`, `AttackState.movePauseOnAttackSec`, `EnemyAttackMovePause`, `MovementPauseRequest` + `MovementPauseRequestEventsSingleton` 큐, `MovementPauseRequestDrainSystem`. → CLAUDE.md NativeQueue 채널 16→15.
8. **유지(건드리지 않음)**: `AggroAssignmentSystem`(Aggroed 부여/해제), `TauntAttackGrantSystem`(AttackState 부여/strip), `CcApplySystem`/`CcDecaySystem`, lifecycle 시스템.
9. **결정론**: 분산/지터 없음. 전이는 위치·사거리·aggro의 결정론적 함수.

## 후속 후보 (현 범위 밖)

- **Pulse 멈춤시간 분리** [S] · 현재 Pulse(unit 7) 는 멈춤구간 = `hitDelaySec`(타격지연)에 연동. 멈춤시간과 텔레그래프를 따로 튜닝하려면 `attackMotionSec` 별도 필드. 필요 시.
- **Pulse 적용 확대** [S] · 현재 Vanguard 만 Pulse. Basic/Tanker 등 다른 근접에도 진동을 줄지는 콘텐츠 결정. 원거리 캠퍼(Sniper/Rootcaster)는 Halt 유지 권장.
- **경계 깜빡임 deadband** [S] · Engaging↔Marching 사거리 경계 hysteresis. tile-Chebyshev 이산이라 우선순위 낮음. 실측 후 필요 시.
- **stun 직교 게이트** [S] · 현재 stun(`CcKind.Stun`)은 이동/공격을 멈추지 않음(미구현). 전이/Movement/Attack 에 stun 게이트 추가 — CC 파이프라인 spec 과 함께. (H1)
- **타겟 스캔 1패스 공유** [S] · 전이와 AttackSystem 이 각각 사거리 스캔(2패스). 로직은 `TrySelectTarget` 공유로 일치하나 스캔 자체는 중복. 엔티티 수 증가 시 1패스 캐시 검토. (M2)
- **stun 명시 상태화** [S] · stun 게이트 구현 후 `Stunned` 를 enum 상태로 승격(복귀 재평가) 검토. CC 콘텐츠 확장 시.
- **HFSM 승격** [L] · 행동이 깊어지면 상태 내 하위상태. 현 4상태로 충분.
- **Standoff/Chasing 스킬캐리어 면역 테스트/의도확정** [S] · `MovementSystem` 은 `Standoff`/`Chasing` 에서 early-return 하여 portal 텔레포트·tornado pull 에 **면역**(Engaging-Halt 와 비대칭). 이 비대칭은 aggro-standoff 시점부터의 기존 동작이고 enemy-ai-fsm 범위 밖이라 미테스트. portal/tornado 블록을 early-return 위로 옮기는 리팩터가 조용히 동작을 바꿀 수 있음. 음성 테스트(`Standoff_OnPortal_DoesNotTeleport`) 추가 또는 spec 에 의도 명시 검토. (ecs-review unit5 M1)
- **Engaging-Advance + 스킬캐리어 경로 테스트** [S] · 신규 테스트는 Engaging-Halt 직교성만 잠금. Advance(스킬캐리어 후 flow 진행) 조합은 미커버. 리스크 낮음(portal/tornado 가 Engaging 분기보다 선행). (ecs-review unit5)
