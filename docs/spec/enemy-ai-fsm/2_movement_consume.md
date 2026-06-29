# 2 — MovementSystem 상태 기반 이동 전환

## 목적

`MovementSystem` 이 `EnemyAiState` 를 RO로 읽어 이동을 결정하게 한다. 기존의 aggro-standoff 분기와 `EnemyAttackMovePause` 체크를 상태 읽기로 대체한다. (이 단계에선 레거시 컴포넌트/큐는 아직 삭제하지 않고, MovementSystem 만 상태 경로로 전환.)

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`.
- `Assets/_Project/Tests/EditMode/MovementSystemTests.cs` (레거시 pause 테스트 → 상태 기반 대체).

## 구현

적 엔티티 이동 분기를 상태 switch 로 재구성:

- `Marching` → 기존 flow field follow(+ portal/tornado/recenter/cell-trim 유지).
- `Engaging`:
  - 이동정책 `Halt` → 이동 스킵(정지).
  - 이동정책 `Advance` → flow field follow(이동하며 공격은 AttackSystem 이 별도 수행).
- `Chasing` → 가디언 방향 self-walk(기존 aggro 분기 로직, cell-trim 유지). guardian 을 못 찾으면(소멸 후 `Aggroed` 미해제 1프레임) 정지(옛 flow fall-through 대비 미세 개선 — 다음 틱 재평가로 복귀).
- `Standoff` → 이동 스킵(정지).

제거되는 기존 분기:
- `MovementSystem.cs:63–90` aggro 분기 내 standoff 거리 판정(`tileDist ≤ tileRange` halt) → 이제 `Chasing`/`Standoff` 상태가 대신. Chasing 의 self-walk 는 유지, standoff 정지 판정은 상태로 이동.
- `MovementSystem.cs:94–102` `EnemyAttackMovePause.remaining` 체크 → 삭제(Engaging-Halt / Standoff 가 정지를 표현).

`portal`/`tornado` 는 상태 직교(정지 상태도 반응 — Halt 적은 portal/tornado 처리 **뒤**, flow step **앞** 에서 정지하므로 portal/tornado 엔 반응하고 flow 만 스킵). **CC impulse(넉백)**: flow 경로(Marching/Engaging-Advance)에서만 적용 — `Standoff`/`Chasing`/`Engaging-Halt` 는 정지(flow 스킵)라 impulse 미적용. Standoff/Chasing 면역은 기존 aggro 동작 보존, Halt 면역은 신규(정지 일관). **넉백 CC 는 향후 제거 예정** 이라 상태×impulse 통합은 다루지 않는다(리뷰 M1 = 옵션1). **stun**: 현재 미구현 — 게이트 없음(H1 후속).

> H3 — 2 와 3a 사이 window: AttackSystem 이 아직 레거시라 fire 시 `MovementPauseRequest` 를 enqueue 하고 DrainSystem 이 `EnemyAttackMovePause` 를 붙이지만, MovementSystem 은 더는 그걸 읽지 않는다(이 단계에서 분기 삭제). 읽히지 않는 orphan 컴포넌트라 무해 — 3b 에서 발행/컴포넌트 모두 제거.

## 완료 기준

- compile 통과.
- Halt 적: Engaging 상태에서 정지. Advance 적: Engaging 에서 flow 이동 지속.
- aggro 적: Chasing 에서 가디언으로 접근, Standoff 에서 정지(기존 standoff 와 동일 위치에서 멈춤).
- `MovementIntegritySmokeTest` 통과 유지(모든 유닛 walk 타일 위, 회귀 없음).
- 레거시 `EnemyAttackMovePause` 는 아직 존재하나 MovementSystem 이 더는 읽지 않음(3b 에서 제거).

---

✅ **완료 2026-06-30** — 컴파일 PASS. MovementSystemTests 8/8 GREEN(Marching/Engaging-Advance 이동 2.0 ↔ Engaging-Halt/Standoff 정지 0.0). 투트랙 리뷰 APPROVE — standoff 메트릭 등가성(정확 일치)·맥락경계(Combat RO)·시스템순서(1틱 stale 없음)·Burst·lifecycle 모두 PASS. M1(impulse 직교)=옵션1 로 spec 정정. Chasing self-walk + Halt portal/tornado 직교 EditMode 는 무거운 셋업이라 작업 5 PlayMode 검증으로 이관.
