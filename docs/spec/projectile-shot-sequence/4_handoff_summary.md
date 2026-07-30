# projectile-shot-sequence — handoff

## Commit

- `37764dd8` feat(projectile-shot-sequence): add per-shot schedule contract
- `5e7c3778` feat(projectile-shot-sequence): enable direction-bound emitter
- `62260b82` feat(projectile-shot-sequence): migrate defender volleys to emitter
- `2651c577` fix(projectile-shot-sequence): project launch height in camera plane
- 각 구현 확인 기록: `746b4780`, `8d6d8519`, `87457fbe`

## Implemented

- 공용 emitter가 한 trigger에서 shot별 방향과 interval을 가진 N발을 순차 발사한다.
- 샷건너는 불규칙 10발 spread와 4타일 최대 이동 거리를 사용한다.
- 머신거너와 기존 방향 패턴도 같은 emitter 경로로 수렴했다.
- 공격 시작 뒤 witness가 죽거나 빠르게 이탈해도 고정 facing trigger가 완주된다.
- 투사체 발사·이동·히트의 ECS 좌표와 거리 수명은 기존 Combat 소유권을 유지한다.
- 몸체 높이, ballistic/bezier arc, SkyFall drop, grenade bounce는 카메라 평면 up으로 표시한다.
- 발사체와 hit VFX가 같은 카메라 평면 높이 규칙을 사용한다.
- Presentation의 `EntityManager` 직접 접근을 제거하고 `BattleBridge` plain snapshot으로 교체했다.

## Key Files

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileEmitterComponents.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileEmitterSystems.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs`
- `Assets/_Project/Scripts/Data/ProjectileData.cs`
- `Assets/_Project/Tests/EditMode/ProjectileShotSequenceTests.cs`
- `Assets/_Project/Tests/EditMode/HeadAnchorTests.cs`

## Verified

- Unity script compile 성공.
- EditMode 전체 1,607건 실행: 1,606 통과.
- 유일한 실패는 작업 범위 밖의 dirty `MapDocument_Zig.asset`을 읽는
  `MultiGoalPoolSeparationTests` 중복 셀 검증이다.
- `HeadAnchorTests`가 보드 좌·중·우에서 camera depth와 screen X 보존 및 null fallback을 검증한다.
- PlayMode의 `ProjectileVisualSmokeTest` 통과를 확인했다.
- PlayMode 전체에는 인증 서버 중복 계정, PrimeTween callback, drag cancel 등
  이 spec 밖의 기존 실패가 남아 있다.
- ECS 리뷰 결과 새 시스템·채널·구조 변경과 Component 소유권 위반은 없다.
- 사용자 종료 확인: 2026-07-30.

## Notes

- `ProjectileData`의 height/arc/drop 수치는 재튜닝하지 않았다.
- 카메라가 없으면 `HeadAnchor`의 기존 월드 offset fallback을 사용한다.
- `AlongVelocity`는 투영 arc/drop을 따르고, `RollAlongPath`는 ground delta만 사용한다.
- 씬·프리팹 배선과 시뮬 `LocalTransform`/hit sweep/maxDistance는 변경하지 않았다.
- 다른 세션의 dirty 에셋과 씬은 이 spec 커밋에 포함하지 않았다.

## Follow-up

- 현재 spec의 잔여 작업은 없다.
- 15발 초과 패턴이 필요해질 때 README의 fixed-list 폭 후속 후보를 별도 spec으로 검토한다.
- 일반 target-bound 투사체의 wind-up 중 타깃 소실 정책은 별도 spec 후보로 남겼다.
