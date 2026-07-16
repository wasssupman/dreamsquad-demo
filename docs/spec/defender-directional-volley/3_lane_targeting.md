# 3. ECS 타겟팅 — facing 유닛 레인 게이트 + 방향 단발 발사

## 목적

`DeployedFacing` 을 가진 방어 유닛의 공격 사이클을 "최근접 타겟 선택" 대신 "방향 레인 내 적 존재 게이트 + 타겟 없는 방향 발사"로 분기한다. 이 단계는 단발까지 — 다연발은 unit 4.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Presentation/` 공격 시 FaceToward 경로 (visual event 소비부 — 최소 수정)

## 구현

**AttackSystem 분기**:
- 공격자에게 `DeployedFacing` 이 있으면(lookup): 후보 스냅샷 순회에서 최근접/우선순위/aggro 오버라이드 대신 `LaneMath.IsInLane(attackerTile, facing, rangeTiles, candidateTile)` 존재 검사만 수행. 하나라도 있으면 발사 준비 완료.
- 기존 START/RESOLVE 2단(쿨다운 리셋·hitDelay·애니 트리거)은 그대로 타되, `bestTarget` 없이 진행하는 경로를 연다 — RESOLVE 에서 `ProjectileSpawnRequest` 에 `direction = facing`, `maxDistance = range(월드 환산)` 를 실어 캐리어 1개 스폰(unit 2 의 Directional 투사체).
- 게이트 실패(레인에 적 없음) 시 발사하지 않고 쿨다운도 태우지 않는다(기존 "타겟 없음" 동작과 동형).
- non-facing 유닛 경로는 바이트 단위로 무변경 — 분기는 facing lookup 유무로만.

**프레젠테이션 facing**: 방향 유닛의 공격 visual event 는 타겟 위치 대신 facing 방향 지점을 향하도록 — `SpineUnitView.FaceToward` 가 받는 지점을 이벤트에 실린 좌표로 유지하되, 발사 시 이벤트에 facing 지점을 기입(Combat 쪽 기입 값 변경이라 View 는 무수정이 이상적).

## 완료 기준

- [ ] compile + 기존 테스트 회귀 없음 (특히 aggro/frontmost/prio 타겟팅 EditMode)
- [ ] execute_code 스모크: DeployedFacing 을 수동 부여한 유닛이 (a) 레인 안 적 존재 시에만 발사 (b) 레인 밖(수직 오프셋 1타일·사거리+1) 적은 무시 (c) 발사 방향이 facing 과 일치
- [ ] 레인 판정 자체는 unit 0 LaneMathTests 가 커버
