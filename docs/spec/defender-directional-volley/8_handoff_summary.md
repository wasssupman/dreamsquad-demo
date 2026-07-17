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
- `e027fe00` unit 7 — 머신건 유닛 + 통합 테스트 7건 · `537bfce3` handoff/맵/backlog
- `e36bd51e` 최종 리뷰 반영(조준 중 UI press 차단 · teardown ECS 미접촉 · 버스트×스프레드 테스트)
- `c659b61b` handoff · `e1b82b96` CC 중 버스트 완주=의도 명문화
- `c0d0f29c` unit 9 스펙(보드 조준 가이드 — 레인 점등 + 화살표 탭)
- `c6437876` **unit 6·9 배선** — 공격방향 페이즈 + 보드 가이드(hash-object 로 overhead-ui hunk 격리). DirectionAimLogicTests 7/7 green
- `1bb12070` 조준 화살표 z-acne 픽스(바닥 코플레이너 → world +Y 리프트)

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
- **조준 페이즈는 모달 — 보드 탭 소비자 3곳을 전부 막아야 한다**: (1) UI press 는 조준이 아니다(`RaycastAll` 즉석 판정 — `IsPointerOverGameObject()` 는 터치 press 프레임에 UI 를 놓쳐 실기기에서만 터진다), (2) 조준 중 트레이 드래그 잠금(`BeginDrag` 가드 — tap-to-place 시뮬 경로도 여기 걸린다), (3) `DcInspectController.Blocked()` 에 `IsAiming`. 하나라도 빠지면 한 제스처가 두 곳에서 소비된다 — (1)(2)는 엉뚱한 방향 고정(리뷰 HIGH-1), (3)은 확정 후에도 slomo/줌이 남아 클릭이 한 번 더 필요(사용자 Play 실측).
- **`Cancel(activatePending: false)` on teardown**: World 파괴 순서가 비결정적이라 ECS 접근이 던진다.

## Follow-up

- ~~unit 6 배선 커밋 미완~~ → **완료(`c6437876`)**. 병행 tap-to-place/액체타일(`dedde0f6`) 커밋 후 볼리 hunk 만 격리 스테이징(BattleBridge 는 overhead-ui hunk 제외하고 hash-object 로 볼리 hunk 만 인덱스 적용, 워크트리 overhead diff 보존). 커밋 트리 overhead 심볼 0 검증됨.
- ~~씬 배선 미완(HIGH-2)~~ → **해소(`044b639a`)**. 튜닝값이 `slowmoScale` 하나뿐이라 전용 SO(DirectionAimSettings)를 폐기하고 이미 씬 배선된 `DragSwaySettings.directionAimSlowmoScale` 로 합침. 별도 에셋·배선 불필요. 라이브 에셋 참조라 Play 중 편집 반영 유지.
- ~~설계 질문: CC 중 버스트 완주 여부~~ → **결정됨(2026-07-17 사용자)**: 완주가 맞다. 볼리 = 한 번의 공격, combat-action-lock 의 "시작된 스윙은 RESOLVE 완료"와 같은 결. 버스트 틱이 `actionLocked` 게이트 위인 것은 의도 — 계약 8·코드 주석에 명문화. 되돌리지 말 것.
- **`shotIndex` 산식 추출 후보**: 버스트 발 인덱스(`shotCount − 남은수`)가 AttackSystem 인라인. 샷건 spec 착수 시 `VolleyMath` 로 추출 + EditMode 고정 검토(현재는 통합 테스트가 커버).
- **코루틴 중단 시 PendingDeployment 잔류**: `Confirm` 후 배치 연출 대기 중 컨트롤러가 파괴되면 유닛이 굳는다. 단 드래그 컨트롤러 `RunDeployment` 도 동일 노출 — 신규 회귀 아닌 기존 패턴 parity.
- **Play e2e + 실기기 스모크(사용자)**: 드래그→드롭→방향 지정→활성화→10연발→피해. 시뮬 계약은 통합 테스트가 덮으므로 남은 건 Mono 배선과 시각/조작감.
- 아트 플레이스홀더(Marksman Spine + Sniper 파츠) — guid 유지 교체 전제.
- **bounce(통통구슬)×방향 유닛**(사용자 결정 2026-07-18, 후속): 지금은 통통구슬이 방향 유닛에 붙어도 inert. 목표 = 트리거당 N발이 각각 bounce 를 받아 튕기게. 상세·설계 과제는 README 후속 후보 참조.
- 후속 후보는 README 참조(배치 취소, 방향 재지정, 레인 폭 파라미터화, 샷건 유닛, 머신건 연사음, bounce×방향 등).
