# projectile-shot-sequence unit 5 handoff

## Commit

- `5503240a feat(projectile-shot-sequence): retarget and randomize shotgun volley`

## Implemented

- 샷건너의 배치 후 방향 지정 단계를 제거하고 일반 D&D 배치로 전환했다.
- 가장 가까운 사거리 내 적을 START witness로 선택해 spread 기준 방향을 고정한다.
- wind-up 중 witness가 죽거나 사거리 밖으로 이동해도 고정 방향으로 한 번 발사한다.
- 기존 공용 `EmitterInstance` trigger를 유지하면서 10발의 방향과 interval을 매 trigger
  결정론적으로 다시 생성한다.
- pellet 방향은 `-30°~+30°`, interval은 첫 탄 즉시 이후 `0.006~0.018초` 범위다.
- 샷건 pellet은 탄당 6 damage, 4타일 `maxDistance` 계약을 유지한다.
- 머신거너의 방향 지정 배치와 기존 10발 cadence는 그대로 보존한다.
- 공용 projectile drain은 carrier가 아니라 실제 `req.owner`의 launch anchor를 조회한다.
- defender는 Spine `WEAPON` bone, enemy는 renderer body center를 첫 표시 위치로 사용한다.
- spawn 직후 첫 sync만 anchor를 보존하고 이후에는 기존 ECS 투영 궤적을 따른다.
- flying VFX는 정리된 GA `vfx_Projectile_Shard01`과 visual scale `0.7`을 사용한다.
- hit/cast VFX와 ECS 원점·충돌·수명은 변경하지 않았다.

## Key Files

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackState.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/PatternShotRandomizer.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`
- `Assets/_Project/Scripts/Presentation/SpineUnitPool.cs`
- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs`
- `Assets/_Project/Data/Projectiles/Pattern_Defender_Shotgunner.asset`
- `Assets/_Project/Data/Projectiles/Projectile_ShotgunPellet.asset`
- `Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Shard01.prefab`

## Verified

- Unity `6000.4.3f1` 컴파일 오류 0.
- 최종 EditMode 전체: 1,613개 중 1,612 통과.
- 유일한 실패는 unrelated dirty `MapDocument_Zig.asset`의 non-goal cell `(0,7)` 공유를
  검출한 `MultiGoalPoolSeparationTests`다.
- 첫 EditMode 실행은 테스트 초기화 timeout으로 시작되지 않았고 asset refresh 후 재실행했다.
- VFX 교체 전 PlayMode 전체: 74개 중 61 통과, 13개 기존 unrelated 실패.
- 신규 `ProjectileVisualSmokeTest` launch-anchor 2개는 실패 목록 없이 통과했다.
- 최종 Shard wrapper는 EditMode에서 ParticleSystem 1개, 0.0751초 이하 trail 2개,
  Rigidbody·Collider 부재와 projectile asset 참조를 검증했다.
- 2026-07-31 사용자 Play 육안 확인 통과.
- ECS 리뷰에서 Combat 소유 쓰기, BattleBridge 단일 경계, sim/view 분리 위반 없음.

## Notes

- `ResolveCastAnchor()`의 기존 의미는 보존하고 projectile 전용 조회 API만 추가했다.
- launch anchor는 프레젠테이션 첫 프레임 전용이며 sim 위치를 다시 쓰지 않는다.
- 랜덤 결과는 trigger seed에 대해 재현 가능하고 다음 trigger에서는 달라진다.
- 기존 `ShotgunPelletFireball.prefab`은 다른 참조 가능성을 고려해 삭제하지 않았다.
- 새 아키타입이나 정거장 이동이 없어 `object-pipeline-map.md` 갱신은 필요하지 않다.

## Follow-up

- 일반 target-bound 투사체의 wind-up 중 타깃 소실 정책은 README의 후속 후보로 유지한다.
- unit 5 범위에서 남은 필수 구현은 없다.
