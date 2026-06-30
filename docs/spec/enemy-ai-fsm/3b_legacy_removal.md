# 3b — 레거시 제거 + 테스트 정리

## 목적

상태머신이 모든 행동을 소유하게 됐으므로, 더는 읽히지 않는 레거시 정지 메커니즘을 **삭제**한다. 이 단계는 4+ 파일을 건드리고 기존 테스트를 깨므로 3a 와 분리한다.

## 변경 대상 (제거)

- `Assets/_Project/Scripts/Battle/Combat/EnemyAttackMovePause.cs` — 컴포넌트 삭제.
- `Assets/_Project/Scripts/Battle/Movement/MovementPauseRequestDrainSystem.cs` — 시스템 삭제.
- `Assets/_Project/Scripts/Battle/Movement/MovementPauseRequestEvents.cs` — `MovementPauseRequest` struct + `MovementPauseRequestEventsSingleton` 삭제.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_movementPauseRequestQueue` 생성/배선/Dispose(`~202, ~1008, ~1010, ~424`) 제거.
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `aimMode`, `movePauseOnAttackSec` 필드 제거.
- `Assets/_Project/Scripts/Battle/Combat/AttackState.cs` — `movePauseOnAttackSec` 필드 제거.
- `Assets/_Project/Scripts/Battle/Combat/EnemyBehavior.cs` — `aimMode` 제거(필드가 `aimMode` 만 남으면 컴포넌트 자체 제거 검토; `targetMode` 가 남으면 유지).
- `Assets/_Project/Scripts/Data/EnemyBehaviorEnums.cs` — `EnemyAimMode` enum 제거.

## 변경 대상 (테스트)

- `Assets/_Project/Tests/EditMode/EnemyBehaviorTests.cs` — `AimMode_StopToAttack_EnqueuesPause`, `AimMode_MoveAndShoot_NoPause` 2개 삭제(상태 기반 테스트는 1·5 가 커버). targetMode/Focus 테스트는 유지.
- **`MakeEnemy` 헬퍼 수정 (M4)**: 같은 파일의 `MakeEnemy` 가 `AttackState.movePauseOnAttackSec = movePause` 를 세팅한다(`~:62`). 필드 제거 시 잔존 테스트(Focus/Nearest)까지 compile break → `movePause` 파라미터와 할당을 헬퍼에서 제거. `aimMode` 세팅도 동일 처리.
- **추가 테스트 정리(컴파일 blast radius)**: `MovementSystemTests.cs`(pause 스캐폴딩·`MovementPauseRequestDrainSystem` 등록 제거), `AttackSystemStateGateTests.cs`·`EnemyAiStateSystemTests.cs`(EnemyBehavior `aimMode` 인자 제거). EnemyBehaviorTests 미사용 using 제거.

## 구현 주의

- `MovementPauseRequestEventsSingleton` 제거로 CLAUDE.md NativeQueue 채널 **16 → 15**. CLAUDE.md 의 채널 목록 한 줄 갱신(별도 docs 커밋 또는 동일 커밋).
- 큐 Dispose 누락 시 leak — BattleBridge teardown 경로에서 해당 라인 완전 제거 확인.
- enum/필드 제거로 `Enemy_*.asset` 직렬화에 stale 키가 남아도 무해(Unity 가 무시). 4 에서 재직렬화.

## 완료 기준

- compile 통과, 콘솔 에러 0.
- `grep` 으로 `aimMode`, `movePauseOnAttackSec`, `EnemyAttackMovePause`, `MovementPauseRequest` 잔존 참조 0(주석/문서 제외).
- EditMode 전체 통과(삭제된 2개 제외). PlayMode smoke 통과.
- BattleBridge 시작/종료 반복 시 신규 leak 경고 없음.

---

✅ **완료 2026-06-30** — 컴파일 PASS(삭제 인식 후 CS2001/CS0103 누락참조 2건까지 해소). grep 잔존 코드참조 0(주석만). 전체 EditMode 회귀 없음(ObstaclePlacer 1건 사전 무관). CLAUDE.md 채널 16→15. 투트랙 리뷰 APPROVE — NativeQueue lifecycle 대칭 제거·참조 완전성·맥락경계·채널 동기화 PASS.
> ⚠ **M1 caveat**: bake 가 SO `engageMovement` 를 직접 사용 → 미마이그레이션 SO(Debuffer/Needler, 원래 MoveAndShoot)는 unit 4 전까지 임시 Halt(정지사격). **unit 4 를 같은 세션에서 즉시 후행**하고, 3b 단독 시점을 "이동사격 동작" Play/배포 체크포인트로 쓰지 말 것.
