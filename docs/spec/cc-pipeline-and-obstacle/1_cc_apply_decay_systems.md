# CC Apply / Decay Systems

**작업 구분**: 1

## 목적

`EnemyCcEventsSingleton` 의 큐를 소비하여 적 entity 의 `DynamicBuffer<CcEffect>` 에 add/merge 하는 시스템과, 매 프레임 buffer 의 `remainingTime` 을 감소시키고 만료된 entry 를 제거하는 시스템을 만든다. 본 단위 commit 시점에는 큐 producer / buffer consumer 가 아직 없으므로 동작 변화 0.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/CcApplySystem.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/CcDecaySystem.cs`
- Modify: Effects bootstrap (또는 BattleBridge bootstrap) — `EnemyCcEventsSingleton.queue` 생성/해제

## CcApplySystem

- Effects 맥락. `[BurstCompile]`, `[UpdateInGroup(typeof(SimulationSystemGroup))]`.
- MovementSystem **이전** 업데이트 (`UpdateBefore(typeof(MovementSystem))`) — 이번 프레임 enqueue 가 다음 프레임이 아니라 현재 프레임 Movement 에 반영되도록.
- 매 OnUpdate:
  - 싱글턴 큐 dequeue loop.
  - target entity 에 `DynamicBuffer<CcEffect>` 가 없으면 ECB 로 추가.
  - 같은 `kind` 의 entry 가 이미 있으면 **merge**:
    - `remainingTime = max(existing.remainingTime, newEffect.remainingTime)`
    - `vector / scalar` 는 새 값으로 덮어씀 (현 `EffectSpawner.ApplySlow` merge 정책 보존).
  - 없으면 buffer.Add(newEffect).
- ECB playback / dispose.

## CcDecaySystem

- Effects 맥락. `[BurstCompile]`, `[UpdateInGroup(typeof(SimulationSystemGroup))]`.
- MovementSystem **이후** 업데이트 (`UpdateAfter(typeof(MovementSystem))`) — 이 프레임의 read 가 끝난 다음 tick.
- 매 OnUpdate:
  - 모든 적 entity 의 buffer 순회. 각 entry `remainingTime -= dt`.
  - `remainingTime <= 0` 인 entry 는 buffer 에서 제거 (역순 순회 + RemoveAt 또는 swap-back).
  - 비어있는 buffer 는 그대로 둠 (다음 add 가 재사용).

## 큐 lifecycle

- `EnemyCcEventsSingleton.queue = new NativeQueue<EnemyCcEvent>(Allocator.Persistent)` — World init 시점 (BattleBridge bootstrap 또는 Effects bootstrap system OnCreate).
- World 정리 시 dispose.
- 기존 `DefenderAttackEventsSingleton` lifecycle 패턴 그대로 따름. 정확한 진입 코드 위치는 `BattleBridge.cs` 검색하여 동일 위치에 추가.

## 단위 테스트 (EditMode)

- `CcApplySystemTests`:
  - 동일 kind 재 enqueue 시 max(remaining) + 새 vector/scalar 채택 확인.
  - 다른 kind 는 별도 entry 로 누적.
  - target entity 에 buffer 미존재 → 추가 후 entry 1 확인.
- `CcDecaySystemTests`:
  - dt 만큼 감소 후 0 이하 entry 제거 확인.
  - 같은 buffer 의 다른 entry 는 보존.

## 완료 기준

- 두 시스템 컴파일 + Burst 활성.
- 단위 테스트 통과.
- 런타임에 호출자 없으므로 게임 동작 변화 0 (Slow 는 아직 기존 `SlowEffect` 경로로 동작 중 — Unit 2 에서 마이그레이션).
- 콘솔 에러/경고 0.

완료: 2026-04-28 — 53c0bd9
