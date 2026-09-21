# 3 · `UnitActionPhase` — lock 식 추출(동작 무변) + 뷰 원샷 순서

## 목적

「지금 이 유닛이 행동을 시작할 수 있나」의 자리를 순수 함수 하나로 못 박는다. **오늘은 추출만**(critic M-9): 새 게이트를 열지 않는다.
이 함수가 이후 방어유닛 AI 상태(적 `EnemyAiState` 의 대응물)가 앉을 자리다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/UnitActionPhase.cs` (순수 static · Burst 호환 · 아키 타입 0. Combat 에 두는 이유: Movement·Effects 가 Combat 을 참조하는 방향이 이미 있다)
- `Battle/Combat/AttackSystem.cs:309` — `actionLocked` 계산 → `UnitActionPhase.Resolve(...)` 소비
- `Battle/Movement/MovementSystem.cs:169` — `locked` 동일
- `Presentation/UnitView.cs` · `SpineUnitView.cs` · `SpriteUnitView.cs` — 원샷 순서 + `PlayDeploy` 명시 슬롯만
- 신규 `Tests/EditMode/UnitActionPhaseTests.cs`

## 구현

```csharp
public enum ActionPhase : byte { Free = 0, Swinging = 1, Locked = 2 }   // 큰 값이 우선. Deploying·Dead 는 쿼리 랭크(README 계약 6)
public static class UnitActionPhase
{
    public static ActionPhase Resolve(bool actionLocked, bool swinging) => actionLocked ? Locked : swinging ? Swinging : Free;
    public static bool CanStartAction(ActionPhase p) => p == Free;      // 공격 START · 자기주도 이동
    public static bool CanResolveSwing(ActionPhase p) => true;          // 진행 중 스윙 RESOLVE 는 CC 중에도 완료(combat-action-lock 규약) — 랭크가 늘 때 좁힌다
}
```
- `actionLocked` 의 출처(CC Sleep/Stun ‖ `LeapFlight`)는 호출부가 지금처럼 계산해 넘긴다. `swinging = hitDelayRemaining > 0`.
- `HazardCastSystem` 은 손대지 않는다(CC 락이 없다 — 여는 건 별도 결정, README 후속 후보).

뷰:
- `PlayDeploy` — 명시 슬롯만(Spine `SpineDeployAnimation` 단일 · 스프라이트 `_set.Deploy`). 없으면 false(폴백 원샷 은퇴 — README 8-1).
- `SpineUnitView.PlayAttack` — 트랙 0 에 배치 원샷이 진행 중이면 `AddAnimation(0, attack, false, 0f)`(큐) — `ClearLoopOverride` 관용구.
- `SpriteUnitView.PlayAttack` — `_oneShot == deploy` 진행 중이면 `_queuedOneShot = attack`(1칸). 완료 폴링에서 큐를 먼저 비운다.
- `Kill` 은 둘 다 즉시.

## 완료 기준

- EditMode: `Resolve` 전 조합 · `CanStartAction` 경계. `AttackSystemUnifiedLoopTests` · `CcActionLockTests` · Movement 테스트 무수정 초록(동작 무변).
- 뷰: 배치 원샷 중 `PlayAttack` 호출 시 배치 원샷이 잘리지 않고 공격이 뒤이어 재생(Play 계측). `deployAnimation` 빈 유닛(순찰병)은 `PlayDeploy` false.
