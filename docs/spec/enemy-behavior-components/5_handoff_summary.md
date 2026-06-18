# Handoff — enemy-behavior-components

> 상태: 완료 2026-06-18 (Unit 0~4). 적 거동을 SO enum → ECS 컴포넌트로 데이터화. enemyClass 는 라벨.

## Commit (단위별)

- `6647e0c` spec 초안(Critic REVISE 반영)
- `5d92be1` unit 0 — enum + EnemyBehavior/FocusTarget + SO 필드
- `ffca694` unit 1 — 6종 SO 거동 필드 기입
- `e0191a8` unit 2 — BattleBridge bake(attackMethod 분기, SO 필터, 하드코딩 제거)
- `45b0390` unit 3 — AttackSystem FocusUntilDead + aimMode 게이팅
- `3ff2fe7` unit 4 — EditMode 테스트 + handoff

## Implemented

- 거동 4축이 `AttackUnitData` enum 필드: `attackMethod`(None/Melee/Projectile), `targetMode`(None/Nearest/FocusUntilDead), `aimMode`(StopToAttack/MoveAndShoot), `targetPriorityClass`+`targetClassMask`.
- bake: attackMethod 가 AttackState/ProjectileRef 부착 결정(**방어적**: Melee/Projectile + outputs 빈 → walk-only). `EnemyBehavior`/`FocusTarget`(Focus만)/`EnemyTargetFilter` 를 SO 에서 부착. enemyClass→Ranger 하드코딩 제거.
- AttackSystem: 선정 순서 **nearest+filter → FocusUntilDead → Aggroed(최상위)**. Focus 는 타겟 죽을때까지 lock(룩업 유효성), 사거리 밖이면 발사 보류·lock 유지. aimMode 가 movePause 게이팅.
- 6종 매핑: Runner/Swift walk-only, Tanker Melee/Nearest, Needler 이동사격, Rootcaster 정지캐스트+Ranger우선, Basic 근접 focus-fire.

## Key Files

- `Data/EnemyBehaviorEnums.cs`, `Data/AttackUnitData.cs`(Behavior 필드)
- `Battle/Combat/EnemyBehavior.cs`, `FocusTarget.cs`, `AttackSystem.cs`(focus/aimMode), `EnemyTargetFilter.cs`
- `Bridge/BattleBridge.cs`(적 스폰 bake ~3410)
- `Data/Enemies/Enemy_*.asset`(거동 필드)
- `Tests/EditMode/EnemyBehaviorTests.cs`

## Verified

- EditMode: EnemyBehaviorTests 5/5, 전체 342 중 340 pass / 0 fail / 2 기존 ignore.
- Play(실월드 reflection): 6종 bake 컴포넌트 정확, focus lock/유지/재선정, aimMode StopToAttack→pause / MoveAndShoot→무. 콘솔 에러 0.

## Notes (되돌리면 안 되는 의도)

- 거동은 SO enum 이 source of truth. enemyClass 는 라벨(거동 파생 금지).
- bake 방어: Melee/Projectile 라도 outputs 비면 walk-only — 데미지-0 공격자 금지(Critic C1).
- focus 유효성은 룩업만(`HasComponent<Health>`), `em.Exists` 미사용(Critic C2).
- 어그로(aggro-targeting) override 가 focus 보다 우선 — 계약 유지.
- FocusUntilDead lock 은 사거리 밖이어도 유지, 발사만 사거리로 게이팅(fire 경로에 range 검사 없음, Critic M2).

## Follow-up

- 적 신규 클래스/유형 추가(거동 조합 새 적).
- aimMode MoveAndShoot 의 kiting 경로/세부.
- 디펜더 거동 컴포넌트화(필요 시).
- 밸런싱 수치(밸런싱 spec).
