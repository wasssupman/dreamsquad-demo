# Enemy AI FSM — Design (얇은 브레인스토밍 결과물)

> 상세 구현 스펙은 `docs/spec/enemy-ai-fsm/` 참조. 이 문서는 목표·결정·아키텍처 요약만 담는다.

## 목표

적(공격 유닛)의 행동을 명시적 상태머신으로 1급화한다. 현재 "적이 지금 뭘 하는지"는 단일 상태가 없고 컴포넌트 유무(`Aggroed`, `EnemyAttackMovePause`, `FocusTarget`) + 숫자/enum 필드(`aimMode`, `movePauseOnAttackSec`, `cooldownRemaining`)에 흩어져 있어, `AttackSystem`이 상태를 추론하려 8개+ lookup을 한다. 이 파편화를 FSM으로 **완전 대체**한다.

## 핵심 결정

- **FSM (행동트리 아님).** ECS/Burst 호환(enum + switch), 결정론(index 기반), 적 행동 복잡도(상태 4개, 깊은 중첩 없음)에서 FSM이 적합. BT는 DOTS에서 구현/유지 비용이 크고 지금 YAGNI. 미래에 행동이 깊어지면 HFSM으로 점진 진화.
- **완전 대체.** `aimMode` / `movePauseOnAttackSec` / `EnemyAttackMovePause` / `MovementPauseRequest` 큐 / `MovementPauseRequestDrainSystem` 를 제거하고 상태 + 이동정책으로 흡수.
- **Combat 맥락 소유.** `EnemyAiState` 컴포넌트와 `EnemyAiStateSystem`은 Combat. 전이 트리거(타겟·사거리·cooldown·aggro)가 교전 정보라 의미론적 주체가 Combat. Movement·Attack 둘 다 RO.

## 아키텍처 요약

**주 상태** (상호배타, 항상 정확히 하나):

| | 이동 단계 | 교전 단계(사거리 도달) |
|---|---|---|
| 어그로 없음 | `Marching` (flow field) | `Engaging` (Halt=정지+공격 / Advance=이동+공격) |
| 어그로 있음 | `Chasing` (가디언으로) | `Standoff` (정지+계속 공격) |

- **직교 차원**(상태머신 위 오버레이, 기존 유지): CC(slow / impulse) = `CcEffect`, lifecycle = `DeadTag`/`PastGoalTag`. (stun 은 현재 이동/공격을 멈추지 않음 — 미구현. FSM 은 현재 동작 보존, stun 게이트는 후속.)
- **이동정책** = `aimMode`의 대체: `engageMovement { Halt, Advance }` 적 아키타입 데이터. Engaging 상태에서만 의미.
- **전이 평가**: `EnemyAiStateSystem` (`UpdateAfter(TauntAttackGrantSystem)` `UpdateBefore(MovementSystem)`)이 매 틱 `Aggroed` 유무 → 가디언/타겟 tile-Chebyshev 사거리 → 4상태 set. Movement/Attack은 상태를 RO로 읽어 행동.

## 동작 불변식

타겟/가디언이 사거리에 있으면 **정지하고 계속 공격**, 벗어나거나 죽으면 **이동 재개**. 어그로 유무 양쪽 동일 원리. aggro는 "타겟을 가디언으로 고정 + 이동목표를 flow→가디언으로" 오버라이드.

## Spec 포인터

`docs/spec/enemy-ai-fsm/README.md` — 작업 단위 0~5.
