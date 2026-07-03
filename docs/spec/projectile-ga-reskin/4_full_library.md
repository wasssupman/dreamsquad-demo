# GA 투사체 전체 라이브러리 (50종)

**작업 구분**: 4

## 목적

GA UniqueProjectiles Vol4 의 **50종 투사체 전부**를 view-only 스트립본 + ProjectileData SO 로 만들어, 어느 디펜더든 원하는 투사체를 골라 쓸 수 있는 라이브러리를 구축한다. (사용자 요청: "ga에 있던것들 다 만들어놔".)

## 변경 대상

- New(스트립본): `Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_*.prefab` — 원본 50종 전부 (유닛 0 툴로 배치 스트립)
- New(에셋): `Assets/_Project/Data/Projectiles/Projectile_*_GA.asset` — 50종

## 구현

- 유닛 0 스트립 툴로 원본 50 프리팹 배치 스트립(mover/rb/col 제거, autodestruct=false, emitterVelocityMode=Transform).
- 각 SO: `preserveVfxColors=true`, `hitPrefab=null`(임팩트 VFX 미발동 정책), 매칭 `vfx_Muzzle_*` cast.
- **facing 패밀리 규칙**: Axe / Rock / RotatingSpheres / Shuriken = `SpinAroundUp`(spin Rock 180 / Spheres 360 / 기타 720) · 나머지 = `AlongVelocity`.
- 명명: prefab suffix 그대로 `Projectile_{Name}_GA`. 단, 최초 파일럿 5종은 무번호 이름 유지(Arrow_GA=Arrow01 등) — 중복 생성 스킵.
- 스케일/높이는 유닛 5에서 일괄 튜닝.

## 완료 기준

- 50종 스트립본(전부 autodestruct=false) + 50종 SO(preserveVfxColors=true, hit=null, cast 연결) 생성. ✓
- `GaProjectileSwapper`(유닛 3) 가 50종 순환. ✓
- read_console Error 0. ✓
- **사용자 플레이로 변종별 룩/스케일 확정 후 미사용 정리** — 후속.

확인 2026-07-03 — 50종 생성·컴파일·스팟체크 PASS. 최종 변종 선택은 사용자 결정 대기.
