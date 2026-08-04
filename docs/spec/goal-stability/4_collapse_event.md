# 4. 붕괴 이벤트 — GoalCollapsedEventsSingleton + 유출 전환

## 목적

안정도 0 도달(붕괴)을 이벤트로 브리지에 알리고, 붕괴한 골이 현행 유출 지점으로 자연 전환됨을 검증 가능하게 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/GoalCollapsedEvent.cs` / `GoalCollapsedEventsSingleton.cs` (신설)
- `Assets/_Project/Scripts/Battle/Units/UnitLifecycleSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/.../UnitLifecycleSystemTests.cs` (기존 위치 확장)

## 구현

1. `GoalCollapsedEvent { Entity entity; int2 cell; int goalIndex; float3 worldPosition; }` + NativeQueue 싱글턴(Units→Bridge, 28번째 채널). 큐 lifecycle 은 기존 패턴 3종 그대로: BattleBridge 생성·싱글턴 엔티티 파괴·Dispose.
2. `UnitLifecycleSystem` 에 goal-dead 루프 추가: `DeadTag + GoalPoint` 쿼리 → 이벤트 enqueue → `DestroyEntity`. hazard-dead 루프 동형. **general-dead 루프 쿼리에 `WithNone<GoalPoint>` 를 추가**한다(hazard 의 `WithNone<BlockingHazard>` 동형) — 순서 배치만으로는 같은 ECB 에 이중 DestroyEntity 가 들어가거나(선행 배치) 이벤트가 유실된다(후행 배치). enqueue 는 반드시 destroy 앞(DefenderDeathEvent 주석과 같은 이유).
3. `BattleBridge.DrainGoalCollapsedEvents()`: 매 프레임 드레인. v1 소비 = 로그 + unit 5 연출 훅 자리. 별도 상태 갱신은 없다 — 유출 전환은 엔티티 부재로 이미 성립(공성 게이트가 다음 프레임부터 열림).
4. `UnitLifecycleSystemTests` 확장: 골 엔티티 DeadTag → 이벤트 1건 발행 + 엔티티 파괴, 이벤트에 cell/goalIndex 보존.

## 완료 기준

- [x] compile + EditMode green — `UnitLifecycleSystemTests` 2건 신규(이벤트 1건·cell bake·파괴 / sink 부재 fail-open) 포함 관련 스위트 57/57. general-dead 루프 `WithNone<GoalPoint>`(리뷰 M1).
- [x] Play: 공성 → 안정도 0 → 붕괴 로그 → 진입 유출로 스트레스 상승 → 유출 한계 패배 정상 — 사용자 Play 확인.
- [x] 붕괴 후 신규 웨이브 정상 유출 — Play 확인.

2026-08-04 사용자 확인 완료.
