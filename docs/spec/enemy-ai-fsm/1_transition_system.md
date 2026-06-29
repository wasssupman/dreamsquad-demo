# 1 — EnemyAiStateSystem 전이 평가 + 타겟 선정 단일화

## 목적

매 틱 적의 `EnemyAiState` 를 결정론적으로 갱신하는 전이 시스템을 추가한다. 행동은 아직 안 바뀐다(상태만 set, Movement/Attack 은 2·3 에서 소비). **데드락 방지를 위해 타겟 선정 로직을 공유 헬퍼로 추출**하여 전이 판정과 AttackSystem fire 판정이 정의상 일치하게 한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/EnemyTargeting.cs` — `static TrySelectTarget(...)` 헬퍼.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 기존 타겟 선정 로직(`~131–187`)을 헬퍼 호출로 치환(동작 동일, compile-safe 리팩토링).
- 신규 `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs` (ISystem).
- 신규 `Assets/_Project/Tests/EditMode/EnemyAiStateTransitionTests.cs`.

## 구현

### 타겟 선정 헬퍼 (H2 핵심)

```
static bool TrySelectTarget(attacker, in Aggroed?, in FocusTarget?, in EnemyTargetFilter,
                            tileRange, targetLookups..., out Entity target)
```

기존 `AttackSystem` 의 우선순위 체인을 그대로 캡슐화: **Aggroed(가디언) > FocusUntilDead 락(락 타겟이 사거리 내일 때만) > EnemyTargetFilter(classMask+priorityClass) > Nearest**. tile-Chebyshev 사거리 사용. AttackSystem 과 EnemyAiStateSystem 이 **동일 헬퍼**를 호출 → "상태=Engaging ⟺ AttackSystem 이 fire할 타겟 존재" 가 보장되어 H2 소프트 데드락이 구조적으로 불가능.

> FocusUntilDead 주의: 락 타겟이 사거리 밖이면 헬퍼는 `false`(다른 타겟이 근처여도 락 유지로 발사 안 함). 따라서 전이도 `Marching` → 정지 데드락 없음. 락 무효화(타겟 사망)는 기존 AttackSystem 규칙대로.

### 전이 평가

`EnemyAiStateSystem : ISystem`, `UpdateInGroup(SimulationSystemGroup)`, `UpdateAfter(TauntAttackGrantSystem)`, `UpdateBefore(MovementSystem)`. 각 적(`AttackUnitTag`):

1. **`Aggroed` 있음**: 가디언 tile-Chebyshev 거리 vs `RangeToTiles(AttackState.range)`. `≤` → `Standoff`, 아니면 `Chasing`.
2. **`Aggroed` 없음**: `TrySelectTarget` 호출 → 성공 `Engaging`, 실패 `Marching`.

순수 함수로 분리해 테스트: `static AiState Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget)`.

### 엣지

- `AttackState` 없는 적(`attackMethod==None`): `TrySelectTarget` 항상 실패 → `Marching`. aggro 되면 `Chasing`. `AggroAttackProfile` 없으면 가디언 옆에서 `Chasing` 고착(현재 동작과 동일, M5).
- **stun**: 현재 stun 은 이동/공격을 멈추지 않음(미구현). 이 spec 은 그 동작을 보존하며 stun 게이트를 넣지 않는다(H1 후속).
- 1틱 위치 stale: 전이는 Movement 전, AttackSystem 은 Movement 후라 위치가 1틱 다를 수 있음 → 경계에서 1틱 깜빡임 가능(영구 데드락 아님). deadband 는 후속.

## 완료 기준

- compile 통과. AttackSystem 헬퍼 치환 후 기존 `AttackSystemUnifiedLoopTests`·`EnemyTargetPriorityTests`·Focus 테스트 통과(동작 불변).
- EditMode `EnemyAiStateTransitionTests`:
  - aggro 없음 + fire 타겟 존재 → `Engaging`; 없음 → `Marching`.
  - aggro + 가디언 사거리 밖 → `Chasing`; 내 → `Standoff`.
  - aggro 해제 → 재평가로 복귀.
  - **FocusUntilDead 락 타겟 사거리 밖 + 타 타겟 근처 → `Marching`**(H2 회귀 가드).
  - **통합: state=Engaging 이면 `TrySelectTarget` 성공(= AttackSystem 이 fire)** 을 같은 픽스처로 검증.
- 상태 set 만 검증(이동/공격은 2·3 에서).
