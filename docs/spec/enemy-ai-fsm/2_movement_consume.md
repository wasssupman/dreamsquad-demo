# 2 — MovementSystem 상태 기반 이동 전환

## 목적

`MovementSystem` 이 `EnemyAiState` 를 RO로 읽어 이동을 결정하게 한다. 기존의 aggro-standoff 분기와 `EnemyAttackMovePause` 체크를 상태 읽기로 대체한다. (이 단계에선 레거시 컴포넌트/큐는 아직 삭제하지 않고, MovementSystem 만 상태 경로로 전환.)

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`.

## 구현

적 엔티티 이동 분기를 상태 switch 로 재구성:

- `Marching` → 기존 flow field follow(+ portal/tornado/recenter/cell-trim 유지).
- `Engaging`:
  - 이동정책 `Halt` → 이동 스킵(정지).
  - 이동정책 `Advance` → flow field follow(이동하며 공격은 AttackSystem 이 별도 수행).
- `Chasing` → 가디언 방향 self-walk(기존 aggro 분기 로직, cell-trim 유지).
- `Standoff` → 이동 스킵(정지).

제거되는 기존 분기:
- `MovementSystem.cs:63–90` aggro 분기 내 standoff 거리 판정(`tileDist ≤ tileRange` halt) → 이제 `Chasing`/`Standoff` 상태가 대신. Chasing 의 self-walk 는 유지, standoff 정지 판정은 상태로 이동.
- `MovementSystem.cs:94–102` `EnemyAttackMovePause.remaining` 체크 → 삭제(Engaging-Halt / Standoff 가 정지를 표현).

CC impulse(넉백), portal, tornado 는 상태와 직교하게 기존대로 적용(상태 무관 오버레이). **stun**: 현재 stun 은 이동을 멈추지 않으며(미구현), 이 단계도 stun 게이트를 추가하지 않는다 — 현재 동작 보존(H1 후속).

> H3 — 2 와 3a 사이 window: AttackSystem 이 아직 레거시라 fire 시 `MovementPauseRequest` 를 enqueue 하고 DrainSystem 이 `EnemyAttackMovePause` 를 붙이지만, MovementSystem 은 더는 그걸 읽지 않는다(이 단계에서 분기 삭제). 읽히지 않는 orphan 컴포넌트라 무해 — 3b 에서 발행/컴포넌트 모두 제거.

## 완료 기준

- compile 통과.
- Halt 적: Engaging 상태에서 정지. Advance 적: Engaging 에서 flow 이동 지속.
- aggro 적: Chasing 에서 가디언으로 접근, Standoff 에서 정지(기존 standoff 와 동일 위치에서 멈춤).
- `MovementIntegritySmokeTest` 통과 유지(모든 유닛 walk 타일 위, 회귀 없음).
- 레거시 `EnemyAttackMovePause` 는 아직 존재하나 MovementSystem 이 더는 읽지 않음(3b 에서 제거).
