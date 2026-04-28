# Other 7 Defenders — Cast VFX Wiring (rev3)

**작업 구분**: 12 (rev3)
**근거**: rev2 task 11 의 generic 인프라가 검증된 후, 데이터만 채우면 동작하는 follow-up.

## 목적

Archer 외 7 projectile 디펜더(Marksman/Piercer/Sniper/Scout/Ranger/Cannon — Bastion/Bruiser/Guardian 은 melee 라 제외) 가 발사 시 cast prefab 1회 재생되도록 데이터만 채운다. 코드 변경 0.

## 변경 대상 (자산만)

ProjectileData 3종 (Arrow 는 task 11 에서 완료):
- Modify: `Assets/_Project/Data/Projectiles/Projectile_Bolt.asset`
- Modify: `Assets/_Project/Data/Projectiles/Projectile_CannonBall.asset`
- Modify: `Assets/_Project/Data/Projectiles/Projectile_Sniper_Crimson.asset`

Defender 7종 (Archer 는 task 11 에서 완료):
- Modify: `Assets/_Project/Data/Defenders/Defender_Marksman.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Piercer.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Sniper.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Scout.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Ranger.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Cannon.asset`

## ProjectileData → Cast prefab 매핑

| ProjectileData | castPrefab | URP path | guid | root fileID |
|---|---|---|---|---|
| Projectile_Bolt | StonebulletCast | `Assets/PixPlays/ElementalProjectiles/Stonebullet/Version_URP/StonebulletCast/StonebulletCast.prefab` | `8c4a61e68fcbe594bb98a889a5e2f1de` | `1410976176046209586` |
| Projectile_CannonBall | FireballCast | `Assets/PixPlays/ElementalProjectiles/Fireball/Version_URP/FireballCast/FireballCast.prefab` | `819d56d561992c744896fe3e31c3a13c` | `2299589311705926841` |
| Projectile_Sniper_Crimson | WindbulletCast | `Assets/PixPlays/ElementalProjectiles/Windbullet/Version_URP/WindbulletCast/WindbulletCast.prefab` | `20500c467e5bd38409ade86cab21b4de` | `4516154863890805629` |

YAML edit 형식 (예시 — Bolt):
```yaml
  castPrefab: {fileID: 1410976176046209586, guid: 8c4a61e68fcbe594bb98a889a5e2f1de, type: 3}
  castVfxLifetime: 0
```

## Defender → Anchor 데이터

10 디펜더 모두 `Lamb` 스켈레톤 데이터를 공유 (skin 만 다름). Archer 의 `WEAPON` 본이 모든 skin 에 존재한다고 가정.

7 디펜더 모두 동일 값:
```yaml
  castAnchorBone: WEAPON
  castAnchorLocalOffset: {x: 0.5, y: 1, z: 0}
```

만약 특정 skin (Owl: Bruiser/Sniper, Goat: Cannon/Guardian/Ranger/Scout) 에 `WEAPON` 본이 없으면 fallback offset 만 동작 (cast 가 디펜더 root 기준 (0.5, 1, 0) 위치에서 spawn). Play smoke 에서 디펜더별로 시각 확인 후 본 이름을 변경하거나 offset 튜닝.

## 검증 시나리오

BattleScene Play 에서 각 디펜더 1대씩 배치 후 발사 확인:
- Marksman, Piercer, Sniper → Wind cast (Sniper 는 Crimson tint 유지) — projectile data 가 wind 베이스라 Stonebullet cast 와 톤 다름
- Scout, Ranger → Wind cast (둘 다 Arrow projectile 사용)
- Cannon → Fire cast
- 좌/우 facing flip 시 cast 위치도 따라옴 (X-mirror)
- 풀 누수 0

**잠재 미스매치**: Scout/Ranger 는 Arrow projectile + Goat skin 사용. ProjectileData(Arrow) 의 cast 는 wind, defender skin 은 goat. 시각 적합도는 디자이너 검토 후 후속 — 이번 task 는 wiring 만.

## 완료 기준

- 3 ProjectileData 자산이 castPrefab 채움.
- 7 Defender 자산이 castAnchorBone="WEAPON" + offset(0.5,1,0) 채움.
- BattleScene Play smoke: 각 디펜더 발사 시 적절한 element 의 cast prefab 재생, 다른 디펜더와 시각 구분 가능.
- 회귀 없음 (Archer cast 동작 유지, 풀 누수 0).
- read_console Error/Warning 0.
- 매핑 미스매치 (Scout/Ranger Arrow + Goat 등) 발견 시 메모 남기고 후속 후보로 이관.

확인 2026-04-28 / 커밋: 51f285f
