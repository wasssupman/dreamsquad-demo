# Demo Variant Asset + Defender Wiring

**작업 구분**: 7

## 목적

배리에이션 인프라가 실제로 동작하는지 확인할 수 있는 시연용 ProjectileData 변종 자산 1개를 만들고 디펜더 1종에 와이어링한다.

## 변경 대상

- New: `Assets/_Project/Data/Projectiles/Projectile_Sniper_Crimson.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Sniper.asset`

## 시연 자산 사양

`Projectile_Sniper_Crimson.asset`:
- id = `sniper_crimson`
- speed = 22 (sniper 빠른 탄)
- hitThreshold = 0.3
- visualScale = 0.28
- projectilePrefab = `WindBulletProjectile.prefab` (Arrow 와 같은 base)
- hitPrefab = null (이 단계에서는 hit VFX 생략 가능)
- tintColor = (1, 0.25, 0.25, 1) — 진홍
- emissionMultiplier = 2.0
- scaleJitter = 0.1
- hueJitter = 0.03
- rotationJitter = 15
- facing = AlongVelocity
- spinSpeed = 0
- textureVariants = `[wind_var0.png, wind_var1.png, wind_var2.png]` (task 5 의 베이크 결과)
- selectMode = Random

## Defender 변경

`Defender_Sniper.asset`:
- 기존 `projectile` 필드를 `Projectile_Sniper_Crimson.asset` 으로 교체.

## 검증 시나리오

BattleScene Play 에서 Sniper 가 Draft 에 등장하도록 deck 설정 후:
1. Sniper 발사 시 비행체가 진홍 색조로 보임.
2. 연속 N발 발사 시 발사마다 텍스처가 다름 (wind_var0/1/2 순환).
3. 스케일이 미세하게 다름 (가시 확인).
4. 발사 시 roll 이 무작위로 적용됨.

## 완료 기준

- 자산 두 개가 Inspector 에서 정상 노출.
- BattleScene Play smoke 에서 Sniper 의 비행체가 다른 디펜더(예: Archer) 와 시각적으로 명확히 구분됨.
- Archer 의 발사는 회귀 없음 (기본 `Projectile_Arrow` 그대로).
- read_console Error/Warning 0.

확인 2026-04-28 / 커밋: (pending)
- 검증 통과 시 `Assets/Screenshots/audit/{date}/sniper_crimson_*.png` 1~2장 첨부 (이전 spec 들의 audit 폴더 패턴).
