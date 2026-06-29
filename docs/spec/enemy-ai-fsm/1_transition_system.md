# 1 — EnemyAiStateSystem 전이 평가 (focus-aware 미러)

## 목적

매 틱 적의 `EnemyAiState` 를 결정론적으로 갱신한다. H2(FocusUntilDead+Halt 데드락)는 전이의 "fire 타겟 존재" 판정이 **AttackSystem 의 fire 조건(focus 락 포함)을 미러**하여 차단한다. **AttackSystem 은 건드리지 않는다**(완전 추출은 후속 — 11 lookup + focus write 분리로 전투 핵심 대수술이라 위험 대비 이득 낮음).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs` — `Evaluate`(완료) + `OnUpdate`.
- `Assets/_Project/Tests/EditMode/EnemyAiStateTransitionTests.cs` — 순수 `Evaluate`(완료).
- 신규 `Assets/_Project/Tests/EditMode/EnemyAiStateSystemTests.cs` — OnUpdate 통합.

## 구현

`EnemyAiStateSystem : ISystem`, `UpdateInGroup(SimulationSystemGroup)`, `UpdateAfter(TauntAttackGrantSystem)`, `UpdateBefore(MovementSystem)`.

### Evaluate (완료, 순수)
`aggroed ? (guardianInRange ? Standoff : Chasing) : (hasFireTarget ? Engaging : Marching)`.

### HasFireTarget 미러 (비-aggro) — AttackSystem `:131–189` 미러
- `targetMode==FocusUntilDead` && 락(`FocusTarget.current`) 유효(살아있음·!Dead) → **락 타겟이 tile-Chebyshev ≤ tileRange 면 true**(AttackSystem 은 락 타겟만 fire). 사거리 밖이면 false → Marching(데드락 방지).
- 그 외 → 사거리 내(tile-Chebyshev ≤ `RangeToTiles(AttackState.range)`) + 필터(`classMask`) 통과 디펜더가 **1개라도 존재**하면 true.
- priority-class 는 "어느 타겟" 문제라 존재 판정에 불필요(사거리 내 후보의 부분집합).

### OnUpdate
- 디펜더 후보 스냅샷(faction & `targetMask`). `EnemyAiState` 가진 적별로: `Aggroed` → 가디언 사거리 `Standoff`/`Chasing`; 비-aggro → `HasFireTarget` → `Engaging`/`Marching`. `flowField` 없으면 tileSize=1/gridSize 128(AttackSystem 과 동일 fallback).
- **AttackState 없는 적**(attackMethod==None): range 없음 → hasFire=false → `Marching`. aggro 면 `Chasing` 고착(가디언 사거리 판정 불가, M5 — 현재 동작과 동일).
- **stun**: 미구현 보존 — 게이트 없음(H1 후속).
- 1틱 위치 stale(전이=Movement 전, AttackSystem=Movement 후) 허용 — 경계 1틱 깜빡임, 영구 데드락 아님.

### drift 가드
`HasFireTarget` 위에 "⚠ AttackSystem fire 조건 미러 — `AttackSystem.cs` 타겟 선정 변경 시 동기화" 주석. 통합 테스트가 일관성 회귀 가드.

## 완료 기준

- compile 통과. **AttackSystem 무변** → 기존 `AttackSystemUnifiedLoopTests`/`EnemyTargetPriorityTests`/`EnemyBehaviorTests` 그대로 통과.
- 순수 `Evaluate` 5/5(완료).
- 통합 `EnemyAiStateSystemTests`:
  - 비-aggro + 디펜더 사거리 내 → `Engaging`; 밖 → `Marching`.
  - aggro + 가디언 사거리 밖 → `Chasing`; 내 → `Standoff`.
  - **FocusUntilDead 락 타겟 사거리 밖 + 타 디펜더 근처 → `Marching`**(H2 회귀 가드).
- 상태 set 만 검증(이동/공격은 unit 2·3a 에서 소비).

---

✅ **완료 2026-06-30** — EditMode 10/10 PASS(순수 Evaluate 5 + OnUpdate 통합 5, FocusUntilDead 회귀 가드 포함). AttackSystem 무변(B focus-aware 미러). 전체 EditMode 회귀 없음(`ObstaclePlacerTests` 1건 실패는 FSM 변경 전부터 존재한 맵 장애물 도메인 사전 실패 — 무관, 별도 추적).
