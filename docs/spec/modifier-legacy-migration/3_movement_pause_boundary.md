# 3. Movement Pause Boundary

## 목적

`EnemyAttackMovePause` 의 write ownership 을 Movement 맥락으로 이동한다. AttackSystem 은 공격 직후 이동 정지 요청만 발행하고, 실제 pause component 갱신은 MovementSystem 이 수행한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/EnemyAttackMovePause.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- 신규 파일 후보: `Assets/_Project/Scripts/Battle/Movement/MovementPauseRequestEvents.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` queue lifecycle

## 구현

1. `EnemyAttackMovePause` 를 `Wassup.Battle.Movement` namespace / 폴더 소유로 이동한다.
2. `MovementPauseRequest { Entity target, float duration }` + `MovementPauseRequestEventsSingleton` 을 추가한다.
3. `BattleBridge` 에 NativeQueue lifecycle 을 추가한다: create, singleton entity, drain/dispose/teardown.
4. `AttackSystem` 은 `EnemyAttackMovePause` lookup write 대신 pause request 를 enqueue 한다.
5. `MovementSystem` 이 queue 를 drain 하며 pause component 를 add/update 하고, 기존 pause countdown 처리를 계속 담당한다.

## 완료 기준

- [x] Unity compile error 0.
- [x] AttackSystem 이 Movement 소유 컴포넌트를 직접 write 하지 않고 `MovementPauseRequest` 를 enqueue 한다.
- [x] 장거리 공격형 적은 `AttackState.movePauseOnAttackSec` 값으로 공격 직후 pause request 를 발행한다.
- [x] Battle reset / scene unload 시 pause request queue dispose 경로가 있다.

검증:
- 2026-05-01: `MovementPauseRequest_Adds_Pause_And_Blocks_Movement_Until_Expired`.
- 2026-05-01: Play Mode enter/exit smoke, console error 0.
