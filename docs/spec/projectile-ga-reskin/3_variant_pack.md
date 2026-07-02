# GA 변종 팩 + 스왑 메뉴

**작업 구분**: 3

## 목적

파일럿 Arrow 외에 시각적으로 구별되는 GA 투사체 변종을 몇 개 더 만들어, 플레이하며 빠르게 바꿔보고 고를 수 있게 한다. (사용자 요청: "몇 개 만들어서 바꿔가보자".)

## 변경 대상

- New(스트립본): `Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_{ExplosiveBullet01,Shard01,Shuriken01,Rock01}.prefab`
- New(에셋): `Assets/_Project/Data/Projectiles/Projectile_{ExplosiveBullet,Shard,Shuriken,Rock}_GA.asset`
- New(에디터): `Assets/_Project/Editor/GaProjectileSwapper.cs`

## 구현

- 유닛 0 스트립 툴로 4종 배치 스트립(mover/rb/col 제거, autodestruct=false, emitterVelocityMode=Transform).
- 각 ProjectileData: `preserveVfxColors=true`, 매칭 `vfx_Hit_*`/`vfx_Muzzle_*`, jitter 0.
  - facing: ExplosiveBullet·Shard = `AlongVelocity` / Shuriken·Rock = `SpinAroundUp`(spin 720·180).
  - visualScale: 1차값 1.0 (Arrow 는 1.5). **파티클 bounds 측정이 불안정해 러프값 — 스왑하며 per-변종 튜닝 대상.**
- `GaProjectileSwapper` 에디터 메뉴:
  - `Wassup/VFX/Cycle Archer GA Projectile` — Defender_Archer.projectile 를 `Projectile_*_GA` 사이로 순환(알파벳순, wrap).
  - `Wassup/VFX/Reset Archer to PixPlays Arrow` — 기존 Projectile_Arrow 로 원복.

## 완료 기준

- 4종 스트립본: mover/rb/col 0, 전 TrailRenderer autodestruct=false. ✓
- 5개 GA 변종(Arrow 포함) 에셋 preserveVfxColors=true + hit/cast 연결. ✓
- 스왑 메뉴 순환/원복 동작(ExecuteMenuItem ran=True, 5종 순환 확인). ✓
- read_console Error 0. ✓
- **사용자 플레이로 변종별 룩 + scale 확정 대기** — 마음에 드는 변종/스케일 선택 후 나머지 정리.

확인 2026-07-03 — 4종 스트립·에셋·스왑메뉴 구현, 컴파일·순환 검증 PASS. Archer 시작점 = Arrow_GA. 실게임 육안(변종 비교 + scale 튜닝) 대기.
