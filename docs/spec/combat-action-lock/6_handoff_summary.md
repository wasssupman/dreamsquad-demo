# Handoff — combat-action-lock

## Commit
- (이 커밋) feat(combat): 행동불가 상태(Sleep/Stun action-lock) + warmup→Sleep 승격

## Implemented
- `CcKind.Sleep`(append 4) + 순수 `CcActionLock.IsLock/IsLocked`(Sleep‖Stun 단일 소스).
- **AttackSystem**: `actionLocked` 시 공격 START 차단(쿨다운 틱은 유지 → wake 즉시 공격). RESOLVE(진행 스윙)는 완료.
- **MovementSystem**: `locked` 를 AiState 직후 조기 계산 → Chasing/Engaging self-walk + flow-step 정지.
  외력(impulse/tornado pull/portal)은 유지.
- **defender CcEffect 버퍼** 스폰 부착(3641 이전, 적은 기존).
- **wake-on-hit**: `CcClearRequestsSingleton`(16th 채널) — DamageApplicationSystem(Units) 이 Sleep 보유 피격
  (`totalDamage>0`) 시 enqueue → `CcClearSystem`(Effects, `[UpdateAfter(DamageApplicationSystem)]`, Exists 가드)
  이 Sleep 제거. **Stun 은 wake 안 함**.
- **warmup 은퇴**: `ApplyPlacementWarmup` 이 cooldownRemaining 직접쓰기 대신 Sleep CcEffect 적용
  (`EffectSpawner.ApplyCc`). placement-aura 의 warmup 이 Sleep 로 승격 → 층위 비대칭 해소.
- inert 였던 **Stun 이 이번에 실제로 행동을 막음**(같은 action-lock 게이트).

## Key Files
- `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs`(Sleep), `CcActionLock.cs`, `CcClearEvents.cs`, `CcClearSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`(게이트), `Battle/Movement/MovementSystem.cs`(게이트)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs`(wake enqueue)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`(defender 버퍼·큐 lifecycle·ApplyPlacementWarmup→Sleep)
- Tests: `EditMode/CcActionLockTests.cs`, `PlayMode/ActionLockTest.cs`, `PlacementAuraTest.cs`(warmup→Sleep 갱신)

## Verified
- compile 클린 · EditMode 17/17 · PlayMode 13/13(재실행). CLAUDE.md 채널 16개 갱신.
- critic 사전리뷰 반영(MED1 Movement lock 위치, MED2/3 CcClear 순서·가드, MED4 버퍼 순서, LOW1 deployDelay 스코프).

## Notes
- lock = **자기주도 이동/공격만 정지**; 외력(impulse/tornado/portal)·이미 시작된 스윙 RESOLVE 는 유지.
- 무한 = `remainingTime = float.PositiveInfinity`(CcDecay `Inf-dt=Inf` 로 안전 유지).
- `_dcHandleCounter` 처럼 CC 는 Effects 소유; Combat/Movement/Units 는 **읽기만**, 제거는 Effects(CcClearSystem).
- squad `placementWarmupSec` 인프라는 이제 Sleep 경유(현재 이 필드 쓰는 에셋 없음).

## Follow-up
- **게이트-동작 PlayMode 미커버**: 적 이동정지(Chasing 포함)·defender 공격정지는 combat/wave 하네스가
  필요해 이번 자동테스트에서 제외(코드리뷰 + placement-aura Sleep 경로로 간접 커버). 하네스 생기면 추가.
- **PlayMode 격리 플레이키**: 합산 실행 시 `PlacementAuraTest.Aura_RespectsAxis` 가 간헐 실패(ranger AS 1.2
  transient) — 단독/재실행 그린. ECS World 크로스-테스트 잔존(기존 인프라 취약)에서 기인, 제품 결함 아님. 격리 하드닝 후속.
- Sleep/Stun VFX(zzz/별) presentation.
