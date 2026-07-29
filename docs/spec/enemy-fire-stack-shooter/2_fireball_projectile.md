# 2 — 파이어볼 투사체 에셋

## 목적

PixPlays `ElementalProjectiles/Fireball` 을 우리 파이프라인이 쓸 수 있는 뷰 프리팹으로
복제하고 `ProjectileData` 를 만든다.

**사전 조사 결론 — 스트립이 필요 없다.** 벤더가 이미 조립본(`Fireball.prefab`)과 별개로
부품 3종을 따로 배포하고 있고, 그 3종은 전부 view-only 다:

| 프리팹 | 구성 | 무버/RB/Collider |
|---|---|---|
| `FireballProjectile/Fireball.prefab` | TrailRenderer ×1 + Mesh ×2 (셰이더 애니메이션) | **없음** |
| `FireballHit/FireballHit.prefab` | ParticleSystem ×4 | **없음** |
| `FireballCast/FireballCast.prefab` | ParticleSystem ×3 | **없음** |

조립본 루트에만 `ProjectileVfx`(데모 무버, `_FlySpeed`/`_FlyCurve`) 와
`TrailScaleWithHierarchy` 가 붙어 있다 — **조립본은 쓰지 않는다.**
Trail `m_Autodestruct: 0` 이라 `projectile-ga-reskin` 이 밟았던 풀 재사용 트레일 소실도 없다.
따라서 `GaProjectileStripper` 를 태우지 않고 **단순 복제**한다.

## 변경 대상

신규:

- `Assets/_Project/VFX/Projectiles/PixPlays/Fireball_View.prefab`
- `Assets/_Project/VFX/Projectiles/PixPlays/FireballHit_View.prefab`
- `Assets/_Project/VFX/Projectiles/PixPlays/FireballCast_View.prefab`
- `Assets/_Project/Data/Projectiles/Projectile_Enemy_Fireball.asset`

## 구현

복제는 벤더 원본 직접 참조 금지 계약(`projectile-ga-reskin` 공통 원칙) 때문이다 — 벤더
업데이트가 우리 룩을 덮지 않게 하고, 스케일/색 튜닝을 우리 쪽에서 한다.

`ProjectileData` 저작:

| 필드 | 값 | 비고 |
|---|---|---|
| `id` | `enemy_fireball` | |
| `flightMode` | `Homing` | **명시 저작** — 기본값 의존 금지(`Projectile_Shuriken_GA` 선례가 후속 후보로 남아 있다) |
| `speed` | 7 | 회피 불가하되 날아오는 게 보이는 속도 |
| `hitThreshold` | 0.4 | |
| `visualScale` | 0.35 | 원본 루트가 scale 2 로 저작돼 있어 그대로 쓰면 크다 |
| `visualHeightOffset` | 0.3 | 타일에 깔리는 것 방지 |
| `facing` | `AlongVelocity` | |
| `preserveVfxColors` | `true` | 벤더 색 완성본 — recolor 우회 |
| `projectilePrefab` / `hitPrefab` / `castPrefab` | 위 복제본 3종 | |
| `hitVfxLifetime` | 1.0 | |
| `onHitEffect` | `None` | 스플래시 없음(단일 대상) |

⚠ **크기·회전은 코드 추측이 아니라 SO knob 으로 맞춘다.** 벤더 VFX 를 이 게임 보드에 얹을
때 상습 함정이 셋이다 — 비활성 그룹 / 정렬 대역 / **바닥 평면 불일치**(이 게임 바닥 = 월드
XY, 벤더 = XZ). 조립본 루트에 `m_LocalEulerAnglesHint: {x: -90}` 이 박혀 있는 것이 그 신호다.
복제본에 회전을 굽기보다 `ProjectileViewPool` 이 `facing` 으로 세우는 결과를 먼저 보고
판단한다.

## 완료 기준

- [x] 복제본 3종이 `Assets/_Project/VFX/Projectiles/PixPlays/` 아래에 있고, `ProjectileData` 가
      **벤더 경로를 하나도 참조하지 않는다**
- [x] 복제본에 `ProjectileVfx` / `Rigidbody` / `Collider` 가 **없다**
- [x] `flightMode` 가 `Homing` 으로 **명시 저작**돼 있다(기본값 의존 아님)
- [ ] **육안 확인 — 사용자 Play 대기**: ① 몸체가 진행 방향으로 서는가 ② 트레일이 남는가
      ③ 보드에 눕거나 묻히지 않는가 ④ 크기가 유닛 대비 과하지 않은가
      ⑤ 연속 발사 시 트레일 소실·순간이동 streak 없음 ⑥ 착탄 `FireballHit` / 발사 `FireballCast`

## 확인

- **2026-07-30** · 구조 검증은 EditMode 단언으로 고정(testrig).
  세 프리팹 전부에 대해 (a) 참조 경로가 `Assets/_Project/VFX/Projectiles/PixPlays/` 로
  시작하는지 (b) `Rigidbody`/`Collider` 가 0개인지 (c) `MonoBehaviour` 가 **하나도 없는지**
  를 단언한다 — 벤더 프리팹을 나중에 다시 끌어다 쓰면 이 단언이 즉시 깨진다.
- e2e(`KindlerFireStackE2ETest`)가 이 투사체로 실제 히트를 성사시키므로
  **비행·명중 경로 자체는 동작이 증명됐다.** 남은 것은 순수 외형 판정이다.
- ⚠ 벤더 조립본(`Version_URP/Fireball.prefab`) 루트에는 데모 무버(`ProjectileVfx`,
  `_FlySpeed`/`_FlyCurve`)와 `TrailScaleWithHierarchy` 가 붙어 있다. **조립본을 쓰지 말 것** —
  부품 3종만 view-only 다.
