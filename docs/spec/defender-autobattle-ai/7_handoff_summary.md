# 7 · Handoff Summary — defender-autobattle-ai

## Commit
- (이 문서와 같은 커밋) `feat(defender-autobattle-ai): 방어유닛 AI 를 로직 레이어로 — DefenderAi.Resolve · DefenderAiStatus · 적 Evaluate 편입` — 해시는 사용자 확인 뒤 README 에 기록.
- 선행: `d28be037`/`f5b37a37`(`Wassup.UnitAi` asmdef, `UnitActionPhase`·`DeployPhaseClock`).

## Implemented
- 로직(`Wassup.UnitAi`): `DefenderAiState{Ready,Sustaining,Engaging,Locked,Deploying}` · `DefenderAttackPolicy{Target,Bomb,Summon}` · `DefenderAiInput` · `DefenderAi.Resolve/CanStartAttack` · `EnemyAi.Evaluate` + `AiState` enum(적 편입, 이름·값 무변).
- 적용: `DefenderAiStatus`·`DefenderAiPolicy` 컴포넌트(Combat) · `DefenderAiStateSystem`(유일 writer, `After(TauntAttackGrant) Before(AttackSystem)`) · 스폰 bake(정책 = 능력 존재).
- 소비: AttackSystem 3경로 START 가 `CanStartAttack(state, ready)` · 소환사 alivePatrol 사본 삭제(→ `Sustaining`) · 브리지 `SyncSummonerAnimationState` 가 `IsPatrolAlive` 폴링 대신 상태 → `UnitView.SetAiState` · 상태 전이 트레이스(채널 22, 변할 때만).
- 뷰: `UnitView.SetAiState(state, sustainLoop, sustainLost)` — Spine 은 Sustaining → 기존 `SetLoopOverride`, 그 외 Clear. 원샷은 사건 그대로.

## Key Files
- `Scripts/UnitAi/DefenderAi.cs` · `EnemyAi.cs` · `Wassup.UnitAi.asmdef`(⚠ `Unity.Burst` 참조 필수 — README 참조)
- `Battle/Combat/DefenderAiStatus.cs` · `DefenderAiStateSystem.cs` · `AttackSystem.cs` · `EnemyAiState.cs`
- `Bridge/BattleBridge.cs`(`SyncSummonerAnimationState`·`TraceDefenderAiTransition`·bake) · `Presentation/UnitView.cs`·`SpineUnitView.cs`
- Tests: `DefenderAiTests`(진리표) · `DefenderAiStateSystemTests` · `EnemyAiStateTransitionTests` · `PatrolSystemIntegrationTests`(픽스처에 상태 시스템)

## Verified
- 컴파일 0 · EditMode 두 lane 2896 — 기지 2건(bomb_man·boomerang 문안) 외 초록.
- MCP Play(전투 중 배치): 소환사 `Deploying(attack1) → Ready → Sustaining(drop 원샷 → attack2 루프)` · 스나이퍼 `Deploying(drop 1.67s) → Ready → Engaging(1.0s) → Ready …`. 콘솔 에러 0("Bone not found: Gear" 기존).
- 미실행: 골든 코퍼스(값 무변 확인 + 채널 22 재베이크) · PlayMode lane(`PatrolDefenderPlayTest`·`ActionLockTest`).

## Notes
- **`Wassup.UnitAi` 는 `Unity.Burst` 를 참조해야 한다.** 없으면 Burst 메타데이터 해시가 이 어셈블리 타입을 못 찾아 AttackSystem 이 통째로 예외(관련 EditMode 25건 빨강). 엔진 참조가 아니라 해시용.
- 컴포넌트 이름 `DefenderAiStatus`(값 = `UnitAi.DefenderAiState`) — enum 과 같은 이름이면 두 네임스페이스를 함께 여는 파일에서 모호.
- AttackSystem 을 단독으로 도는 테스트 월드에 소환사가 있으면 `DefenderAiStateSystem` 도 넣어야 한다(`PatrolSystemIntegrationTests.AddAttackWithAi`). 없으면 상태가 Ready 로 고정돼 소환물 생존을 못 본다.
- `CanStartAttack` 은 Sustaining 에서도 true — 소환사 쿨 리셋 규칙(재소환 대기 = 남은 쿨) 보존용. 바꾸면 동작이 바뀐다.
- 타겟 선택은 아직 AttackSystem — 후속 `unit-ai-targeting`. HazardCast 는 상태를 읽지 않는다(CC 락 없음 — 별도 결정).

## Follow-up
- 사용자 Play 육안(소환사 능력 루프·상실 원샷 · 폭탄맨) → 커밋 해시 기록.
- 골든 재베이크(채널 22) · PlayMode lane.
- `unit-ai-targeting` spec · `SetLoopOverride` private 화 검토(소비처 = SetAiState 뿐).
