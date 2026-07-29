# unit 1 — Direction-bound emitter

## 목적

기존 공용 `ProjectileEmitterSystem`에서 타겟 후보가 필요 없는 방향 직선탄을 정상 발사한다.
한 trigger가 스냅샷한 원점·기준 방향·최대 거리를 유지하면서 각 shot의 `directionT`를
min/max 각도로 변환해 기존 projectile carrier/drain에 전달한다.

이 unit은 Direction binding 소비 경로만 연다. `DeployedFacing`, 실효 damage,
`attackRange * tileSize`를 읽어 defender trigger를 만드는 작업과 실제 샷건너 데이터는
unit 2에서 연결한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/{PatternSpec,ProjectilePatternData}.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/PatternTargeting.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/ProjectileEmitterSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/{PatternBakeTests,PatternTargetingTests,ProjectileEmitterIntegrationTests}.cs`

## 구현

- `PatternSelectionRule.None`을 append-only 값으로 추가한다. 이는 선택 실패가 아니라
  Direction binding의 정상적인 무타겟 계약이다.
- `TryToSpec`은 Direction+None 또는 target-bound+target selection 조합만 허용한다.
  선택/binding 불일치는 bake에서 loud 거절한다.
- Direction instance는 후보 query와 `PatternTargeting.Select`를 실행하지 않는다.
  후보가 0이어도 `PatternLogic.BuildOrder(..., -1)`로 shot 위상을 전진시키고 carrier를 만든다.
- 발사 요청은 trigger가 `EmitterInstance.template`에 저장한 `origin`, `direction`,
  `maxDistance`를 사용한다. host의 현재 위치나 타겟 상태로 덮어쓰지 않는다.
- 개별 방향은
  `PatternDirection.Resolve(template.direction, minAngleDeg, maxAngleDeg, directionT)`로 결정한다.
  기존 Bridge drain이 최종 단위 벡터 정규화와 `ProjectileState` 복사를 담당한다.
- Entity/Cell binding은 기존 host 현재 위치·타겟 선택·잠금 semantics를 유지한다.
- emitter를 `BossPeriodicTriggerSystem`과 `AttackSystem` 뒤에 배치한다. 다음 unit의 defender
  producer가 buffer에 push한 첫 shot을 같은 sim frame에 소비할 수 있어야 한다.
- `DeadTag` host의 진행 중 instance는 더 발사하지 않는다. 실제 buffer 수명은 host entity
  lifecycle에 따라 종료된다.

## 완료 기준

- Unity 컴파일 오류가 없다.
- `None` 선택 규칙은 후보 배열과 무관하게 `-1`을 반환한다.
- Direction 통합 테스트가 타겟 0 상태에서 trigger frame에 N개 carrier를 생성한다.
- 각 carrier가 스냅샷 origin/maxDistance와 min·center·max 방향을 보존한다.
- 기존 Entity/Cell emitter 통합 테스트가 모두 통과한다.
- ECS 리뷰에서 신규 구조 변경·채널·managed hot-path·맥락 쓰기 위반이 없다.
