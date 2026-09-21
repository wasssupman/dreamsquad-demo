# 1 · `DefenderAiStatus`·`DefenderAiPolicy` 컴포넌트 + `DefenderAiStateSystem`

## 변경 대상
- 신규 `Battle/Combat/DefenderAiState.cs` — `DefenderAiStatus { value }`(Combat 소유 — enum 과 이름을 가른다) · `DefenderAiPolicy { value }`(스폰 bake)
- 신규 `Battle/Combat/DefenderAiStateSystem.cs` — 밴드 C, `[UpdateAfter(TauntAttackGrantSystem)] [UpdateBefore(AttackSystem)]`
- `Bridge/BattleBridge.cs` `CreateDefenderEntity` — 두 컴포넌트 부착. 정책: `SummonPatrolAbility` → Summon · `BombThrowAbility` → Bomb · 그 외 Target
- 신규 `Tests/EditMode/DefenderAiStateSystemTests.cs`

## 구현
시스템은 `WithAll<DefenderUnitTag, DefenderAiState>` 를 돌며 스냅샷을 만들고 `DefenderAi.Resolve` 결과를 쓴다(유일 writer):
- `deploying` = `HasComponent<PendingDeployment>` (쿼리로 빼지 않는다 — 상태값에 Deploying 을 남겨 뷰·트레이스가 읽는다)
- `actionLocked` = `CcActionLock.IsLocked(CcEffect)` ‖ `LeapFlight` — AttackSystem 과 **같은 식**(이후 AttackSystem 은 이 상태를 읽는다)
- `swinging` = `AttackState.hitDelayRemaining > 0`
- `summonAlive` = `SummonerState.current` 3중 생존 술어(Exists · !DeadTag · Health > 0) — 오늘 AttackSystem·브리지에 둘 있는 사본을 **여기 하나로**
- `DeadTag` 유닛은 건너뛴다(값 stale 유지 — 파괴 대기).

## 완료 기준
- EditMode: 각 입력 조합에서 컴포넌트 값이 `DefenderAi.Resolve` 와 같다 · 순찰병 사망 프레임(DeadTag 만 붙은 상태)에 Sustaining → Ready.
- 시스템 순서 덤프에서 `TauntAttackGrant < DefenderAiState < AttackSystem`.
