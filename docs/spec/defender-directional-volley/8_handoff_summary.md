# 8 — Handoff Summary

## Commit

- `0c3e731f` spec 신설 · `cae3eb58` merge 후 상태 갱신
- `78f5c38a` unit 0 — VolleyMath · LaneMath · SweepHitMath (순수)
- `80b26662` unit 5 — DirectionAimLogic (순수) · `f85a3ca8` 에서 rev1(축 투영)
- `98cf377b` unit 1 — 데이터 계약(SO 필드 · DeployedFacing · Bridge API)
- `8bd50350` unit 2 — DirectionalLinear 궤적 + PathHit 페이로드 arm
- `980b3d43` unit 3·4 — 레인 게이트 방향 발사 + 다연발(VolleyFireState)
- `f85a3ca8` unit 6 — DirectionAimController + 설정 SO
- `af8965d5` ecs-review 반영(불멸 투사체 · frontmost 오적용 · 파라미터명)
- `e027fe00` unit 7 — 머신건 유닛 + 통합 테스트 7건

## Implemented

- 방향 고정 방어 유닛: 배치 드롭 후 **공격방향 페이즈**(슬로우모션 유지 + 줌 + 4방향 가이드 + 스와이프) → 확정 방향 영구 고정(명일방주식).
- `DeployedFacing`(Units 소유) 1회 기록 후 불변. Combat 은 읽기 전용.
- **레인 게이트 발사**: facing 유닛은 방향 레인(폭 1타일 × 사거리)에 적이 있을 때만 발사. 레인 최근접 1기를 witness 로 잡아 기존 `bestTarget` 게이트를 재사용 — 조준 시각도 자동으로 맞는다(레인이 facing 축이라).
- **다연발 일반화**: `VolleyFireState`(shotCount/interval/spread). 확산형 = 동프레임 캐리어 N개, 버스트형 = 시간차 틱. 쿨다운은 버스트 종료 후 기산.
- **방향 투사체**: `DirectionalLinear`(직선 비행 + 사거리 클램프) × `PathHit`(경로 스윕, 대상당 1회, pierce 예산). 신규 System/큐 0 — arm 추가만.
- 머신건 유닛(10발 0.1s, 발당 8뎀, 사이클 2.5s) + 카탈로그 등록.

## Key Files

- 순수 로직: `Battle/Combat/{VolleyMath,LaneMath}.cs` · `Battle/Combat/Projectile/SweepHitMath.cs` · `UI/DirectionAimLogic.cs`
- ECS: `Battle/Combat/AttackSystem.cs`(레인 witness·버스트 틱·볼리 RESOLVE) · `Battle/Combat/Projectile/{ProjectileMoveSystem,ProjectileHitSystem}.cs` · `Battle/Combat/VolleyFireState.cs` · `Battle/Units/DeployedFacing.cs`
- Bridge: `Bridge/BattleBridge.cs` — `SpawnProjectile`(방향 분기·정규화·pierce 번역·퇴화 폐기) · `ActivateDeployedDefender(cell, entity, facing)` · `ResolveProjectileAxes` · 스폰 시 VolleyFireState 사전 부착
- Mono: `UI/DirectionAimController.cs` · `Data/DirectionAimSettings.cs`(+ `Data/Config/DirectionAimSettings.asset`)
- 테스트: `Tests/EditMode/{VolleyMathTests,LaneMathTests,SweepHitMathTests,DirectionAimLogicTests,DirectionalVolleyIntegrationTests}.cs`

## Verified

- **EditMode 905 green**(실패 0, skip 2 = 기존 Ignored). 컴파일 클린.
- 통합 테스트가 실제 시스템 world 에서 검증: 레인 게이트(안/밖/한 칸 옆) · 버스트 완주(레인이 비어도) · 사이클 2.5s(버스트와 미겹침) · 부채꼴 3발 ±15° · **facing 없는 유닛의 기존 호밍 타겟팅 무회귀**.
- 축 매핑 실측: Directional→(DirectionalLinear, PathHit), Homing/Ballistic 무변화.
- ecs-review 통과(CRITICAL/HIGH 0). MED 1 · LOW 2 반영 완료.

## Notes (되돌리면 안 되는 의도)

- **`impactReached` 는 PathHit 에서 "비행 종료" 뜻**(unit 2 rev1). MoveSystem 이 직접 파괴하면 마지막 프레임 스윕이 소실돼 사거리 끝 적이 그냥 통과한다. 소멸 소유권은 HitSystem 단독.
- **`VolleyFireState.template` 통째 스냅샷**: 버스트 2~N 발이 1발과 바이트 동일해야 한다(카드가 버스트 중 만료돼도 7번 발이 1번 발과 달라지지 않게). AttackState 로 옮기지 말 것 — 적까지 공유하는 컴포넌트다.
- **버스트 틱은 START 앞**: 뒤에 두면 트리거 프레임에 dt 를 한 번 먹어 1번 발이 한 프레임 일찍 나간다.
- **DirectionAimLogic 은 축 투영 모델**(unit 5 rev1). 화면 cardinal 스냅으로 되돌리면 iso 보드에서 "화면 위"가 +Y/−X 동률이라 레인 판정이 불가능해진다.
- **방향 정규화·퇴화 폐기는 drain 담당**: dir=0 또는 speed=0 이면 traveled 가 영영 0 → 불멸 투사체(ecs-review M1).
- **facing 유닛은 frontmost 보너스 포기**(`fmChosenIsPriority = false`): witness 는 최근접이지 최전방이 아니라, 카드가 약속한 대상과 다른 적에게 +20% 가 실린다.

## Follow-up

- **unit 6 배선 커밋 미완**: `DefenderDragPlacementController`(핸드오프 3 hunk) + `DefenderSelector`(aimSettings 주입)가 **워크트리에 구현돼 있으나 미커밋** — 병행 세션의 `defender-tap-to-place` WIP 와 같은 파일이라 격리 불가. 그쪽이 커밋되면 즉시 이어서 커밋할 것. 그 전까지 방향 페이즈는 Play 에서 동작하지만 커밋 트리에는 없다.
- **Play e2e + 실기기 스모크(사용자)**: 드래그→드롭→방향 지정→활성화→10연발→피해. 시뮬 계약은 통합 테스트가 덮으므로 남은 건 Mono 배선과 시각/조작감.
- 아트 플레이스홀더(Marksman Spine + Sniper 파츠) — guid 유지 교체 전제.
- 후속 후보는 README 참조(배치 취소, 방향 재지정, 레인 폭 파라미터화, 샷건 유닛, 머신건 연사음 등).
