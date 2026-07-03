# Handoff Summary — projectile-ga-reskin

**상태**: 구현 완료 2026-07-03. 실게임 Play 검증 PASS. 최종 변종/스케일 선택은 사용자 취향 대기(후속).

## Commit

- `0542198` 스트립 툴(unit 0) · `d941656` autodestruct 해제(unit 0 rev) · `3c449eb` ViewPool as-is 가드(unit 1)
- `785fc4b` 파일럿 Arrow+Archer(unit 2) · `c6a2663` visualScale 튜닝 · `47ab476` 변종4+스왑메뉴(unit 3)
- `a335640` hit off · `06cc8fd` 높이/sorting(unit 5) · `2c8f722` 라이브러리 50종(unit 4) · `60c79ce` 디펜더 배선
- `fc5129f` PlayHit height/scale 전달
- ⚠️ hit VFX 일부(ProjectileData.hitVfxScale, ViewPool.PlayHit 시그니처, 50 SO hitPrefab=muzzle)는 **병렬 legacy-render-removal 커밋(`be4666f`~`942078e`)에 딸려 들어감**(사용자 `add .` 실수). 유실 아님, 라벨만 섞임 — 되돌리지 않기로 함.

## Implemented

- GA UniqueProjectiles Vol4 **50종 전부**를 view-only 스트립본 + `Projectile_*_GA` SO 라이브러리로 구축.
- 스트립 툴(`GaProjectileStripper`): mover/Rigidbody/Collider 제거 + `TrailRenderer.autodestruct=false` + 전 PS `emitterVelocityMode=Transform`.
- ViewPool as-is 가드: 풀 재사용 streak 리셋(`ResetVfx`) + `preserveVfxColors`(GA HDR 색 보존).
- 렌더링: `visualHeightOffset`(타일 위 부양) + `BoardSortOrder.ProjectileOffset=1000`(유닛 위 sorting).
- hit VFX = **매칭 muzzle 버스트**(잔류/데칼/메쉬 없음) + `hitVfxScale` + PlayHit height/scale.
- 스왑 메뉴(`GaProjectileSwapper`): Archer projectile 을 GA 변종 순환 / PixPlays 원복.
- 튜닝 기본값: visualScale ×0.6, visualHeightOffset 0.7, hitVfxScale 2, hit=muzzle.

## Key Files

- `Assets/_Project/Editor/GaProjectileStripper.cs` · `GaProjectileSwapper.cs`
- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (streak/color/height/sorting/hit-scale)
- `Assets/_Project/Scripts/Data/ProjectileData.cs` (preserveVfxColors, visualHeightOffset, hitVfxScale)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` (ProjectileOffset)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (PlayHit 호출)
- `Assets/_Project/VFX/Projectiles/GA/*.prefab` (50 스트립본) · `Data/Projectiles/Projectile_*_GA.asset` (50 SO)

## Verified

- compile 클린(반복 확인). read_console Error 0.
- Play: 투사체 비행 시 트레일/native 색 정상, hit muzzle 버스트가 유닛 위·바닥 위로 보임. 사용자 "좋아" 확인.
- 스왑 메뉴 순환/원복 동작(ExecuteMenuItem PASS).

## Notes (되돌리지 말 것)

- **preserveVfxColors=true**: 안 하면 ViewPool MPB 흰색이 GA `_Color`(HDR)·`_EmissionColor` 를 씻음.
- **autodestruct=false**: GA 트레일이 풀링에서 자가파괴돼 트레일 잃음(런타임에만 드러남).
- **emitterVelocityMode=Transform**: RB 없이 velocity 시각 유지.
- **ProjectileOffset=1000**: 유닛 동적 order(수백) 위, 데미지숫자(32000)·UI 아래.
- **hit = muzzle**(hit 프리팹 아님): GA hit 프리팹은 데칼/잔류가 있어 "터지고 마는" 요구와 안 맞음. muzzle 은 burst+playOnAwake 라 잔류 0.
- visualHeightOffset 은 렌더 Y 에만(ECS/velocity/sorting 불변 — sorting 은 X/Z 기반).

## Follow-up

- 디펜더별 **최종 변종 선택**(사용자, 50종 중) + 스케일/높이 취향 미세조정.
- **미매칭 hit 2개**: Arrow20 / CardsThrow01 (패키지에 매칭 muzzle 없음 → 대체 muzzle 지정 필요).
- 안 쓰는 변종 SO/프리팹 정리(50은 많음).
- 모바일 최적화(라이트/트레일 감축, soft particle) — 미착수.
- tint 플러밍(데이터-드리븐 recolor 원할 시).
