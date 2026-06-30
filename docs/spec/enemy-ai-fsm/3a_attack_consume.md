# 3a — AttackSystem 상태 기반 fire 전환

## 목적

`AttackSystem` 이 `EnemyAiState` 를 RO로 읽어 **`Engaging | Standoff` 에서만 fire** 하게 한다. aimMode 분기와 `MovementPauseRequest` enqueue 를 제거한다. (레거시 컴포넌트/큐/시스템의 실제 삭제는 3b.)

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` (특히 START 분기 ~221–260).

## 구현

- 적(`AttackUnitTag`, non-defender) fire 게이트에 상태 조건 추가: `EnemyAiState.value ∈ { Engaging, Standoff }` 일 때만 START 진입. `Marching`/`Chasing` 이면 fire 안 함.
- **cooldown tick 보존 (M1)**: cooldown(`hitDelayRemaining`/`cooldownRemaining`) tick 은 상태와 무관하게 **계속** 돈다(현행 `AttackSystem.cs:107` 무조건 tick 유지). 도착 시 즉발(fire-ready on arrival) 동작 보존. 상태는 **fire 게이트만** 조건화한다.
- 전이(unit 1 `EnemyAiStateSystem.HasFireTarget`)가 AttackSystem 의 fire 조건을 **미러**하므로, 상태=Engaging 인데 bestTarget 못 찾는 불일치가 없다(동기화는 미러 주석 책임 — 공유 헬퍼 추출은 README 후속).
- **제거**: `AttackSystem.cs:242–255` 의 aimMode 판정 + `movementPauseSingleton.queue.Enqueue(MovementPauseRequest{...})` 블록 전체. 정지는 이제 Movement 가 상태로 처리하므로 AttackSystem 은 pause 를 발행하지 않는다.
- hit-delay START/RESOLVE 분리, cooldown, 타겟 우선순위 체인(Aggroed > FocusUntilDead > filter > nearest), outputs/투사체 경로는 **그대로 유지**.
- 디펜더 경로(`isDefenderStart`)는 상태머신 대상이 아니므로 영향 없음 — 디펜더는 `EnemyAiState` 컴포넌트가 없고 기존 로직대로 fire.

> 주의: 전이 시스템(1)이 이미 "사거리 내 타겟 존재"로 `Engaging` 을 set 했으므로, AttackSystem 의 사거리/타겟 재판정과 일관되어야 한다(동일 메트릭). 불일치 시 상태=Engaging 인데 fire 안 하는 데드락 가능 → 같은 tile-Chebyshev + 동일 필터 사용으로 보장.

## 완료 기준

- compile 통과.
- Halt 적: Engaging 에서 정지 + 정상 공격(데미지/투사체 발생). Standoff 에서 taunt 공격.
- Advance 적: 이동하며 공격(Engaging, 이동은 Movement 가 지속).
- `Marching`/`Chasing` 중 fire 안 함.
- 기존 `AttackSystemUnifiedLoopTests` 통과(디펜더 경로 무변). 적 fire 경로 회귀 없음.
- `MovementPauseRequest` enqueue 호출 0(grep 확인).

---

✅ **완료 2026-06-30** — 컴파일 PASS(0 errors). AttackSystemStateGateTests RED→GREEN(Marching/Chasing 미발사 ↔ Engaging/Standoff 발사 + Marching→Engaging 전이 즉발). 전체 EditMode 회귀 없음(ObstaclePlacer 1건만 사전 무관). 투트랙 리뷰 APPROVE — H2 데드락 부재(미러 등가)·맥락경계(Combat RO)·Burst·cooldown tick 보존 PASS.
> 주의(ecs M1): RESOLVE 는 START 게이트 밖이라, Advance 적이 hit-delay 만료 프레임에 사거리로 진입하면 상태=Marching 인데 타격이 날 수 있다(1틱 경계 의미 불일치, 데미지 이중/누락 없음 — attack-hit-delay 의 "commit 된 swing 은 착탄" 설계대로 정상).
