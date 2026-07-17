# 3. ECS 타겟팅 — facing 유닛 레인 게이트 + 방향 단발 발사

## 목적

`DeployedFacing` 을 가진 방어 유닛의 공격 사이클을 "최근접 타겟 선택" 대신 "방향 레인 내 적 존재 게이트 + 타겟 없는 방향 발사"로 분기한다. 이 단계는 단발까지 — 다연발은 unit 4.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Presentation/` 공격 시 FaceToward 경로 (visual event 소비부 — 최소 수정)

## 구현

**AttackSystem 분기** (rev1 — 구현 시 확정):
- **레인 witness 모델**: `bestTarget` 을 Null 로 두고 게이트를 바꾸는 대신, 후보 루프에서 **레인 내 최근접 1기를 witness 로 선정**해 `bestTarget` 에 넣는다. 이유 — (a) START/RESOLVE·hitDelay·로그·CC 게이트가 모두 `bestTarget != Null` 을 공유하므로 Null 허용은 공유 게이트를 광범위하게 건드린다, (b) 레인은 facing 축 직선이라 witness 를 바라보는 것이 곧 facing 을 바라보는 것 → **프레젠테이션 facing 이 자동으로 맞는다**(View 무수정, 이벤트 기입 변경도 불필요). witness 는 발사 근거일 뿐 데미지 대상이 아니다 — 데미지는 경로 스윕(unit 2)이 결정.
- 레인 판정은 **기존 후보 루프에 한 줄 합류**(frontmost 선례의 단일 패스). Chebyshev 사거리 필터를 이미 통과한 뒤라 레인은 그 부분집합.
- **facing 은 최종 오버라이드**: 최근접/우선순위/frontmost/aggro 가 무엇을 골랐든 마지막에 witness 로 덮는다(레인이 타겟팅 규칙 전부). 레인이 비면 Null → 기존 hold-fire 경로 그대로(쿨다운 미소모).
- RESOLVE 에 `projRef.movement == DirectionalLinear` arm 추가: `direction = facing`, `maxDistance = tileRange × tileSize`(레인 게이트와 같은 타일 환산 — 탄이 인정된 마지막 칸까지 정확히 닿게). facing 없는 유닛이 Directional SO 를 쓰면 조준 대상 방향으로 발사(퇴화 벡터는 drain 이 폐기).
- non-facing 유닛 경로는 바이트 단위로 무변경 — 분기는 facing lookup 유무로만.

## 완료 기준

- [ ] compile + 기존 테스트 회귀 없음 (특히 aggro/frontmost/prio 타겟팅 EditMode)
- [ ] execute_code 스모크: DeployedFacing 을 수동 부여한 유닛이 (a) 레인 안 적 존재 시에만 발사 (b) 레인 밖(수직 오프셋 1타일·사거리+1) 적은 무시 (c) 발사 방향이 facing 과 일치
- [ ] 레인 판정 자체는 unit 0 LaneMathTests 가 커버
