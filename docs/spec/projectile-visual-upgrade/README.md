# Projectile Visual Upgrade Spec

**작성일**: 2026-04-28
**상태**: rev2 진행 중 (본체 0..8 + rev1 task 10 완료 / rev2 task 11 추가)
**연결 문서**: `docs/plans/2026-04-28-projectile-visual-upgrade-design.md`
**목표**: ECS RenderMesh 기반의 단순 mesh+material 투사체를 PixPlays prefab 기반 시각으로 교체하고, 데이터-드리븐 + per-shot 랜덤 두 층의 배리에이션 인프라를 도입한다.

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| 0 | `0_hit_event_channel.md` | Combat→Presentation 임팩트 이벤트 채널 신설 |
| 1 | `1_view_pool_skeleton.md` | ProjectileViewPool 기본 골격 (spawn/follow/despawn) |
| 2 | `2_data_schema_swap.md` | ProjectileData 필드 교체 + BattleBridge RenderMesh 경로 제거 |
| 3 | `3_facing_and_lifecycle.md` | 회전 정책(`AlongVelocity / FixedUp / SpinAroundUp`) + hit prefab lifetime |
| 4 | `4_variation_runtime.md` | tint / hueJitter / scaleJitter / rotationJitter / emissionMul (MaterialPropertyBlock) |
| 5 | `5_texture_baker.md` | 에디터 메뉴 + 첫 변종 텍스처 베이크 (4 키트 × 3 변종) |
| 6 | `6_texture_variants_runtime.md` | `Texture2D[] textureVariants` + selectMode 를 view pool 이 MPB 로 swap |
| 7 | `7_demo_variant_asset.md` | 시연용 변종 ProjectileData 1개 + 디펜더 1종 와이어링 + Play smoke |
| 8 | `8_tests.md` | EditMode (variation 결정성) + PlayMode smoke |
| 9 | `9_handoff_summary.md` | 본체 종료 시 인계 (커밋 hash, verified, follow-up) |
| 10 | `10_critical_fixes.md` | rev1: 첫 프레임 회전 / roll 누적 / RNG 시드 / 핫패스 GC / hit lifetime |
| 11 | `11_cast_and_anchor.md` | rev2: cast 머즐 플래시 + Spine bone anchor (Archer 1대 검증, 인프라는 8대 generic) |

## 공통 원칙

- ECS 시뮬레이션 (`ProjectileMoveSystem` / `ProjectileHitSystem` 의 데미지 로직) 은 변경 없음.
- 임팩트 시 Combat→Presentation 채널 `ProjectileHitEventsSingleton` 을 통해 hit prefab 재생을 알린다. 직접 Component 수정 금지.
- 모든 투사체 시각은 prefab 기반. spec 종료 시점에 `RenderMeshArray` 기반 코드/캐시는 코드베이스에서 제거된다.
- ProjectileData 가 시각의 source-of-truth. 디펜더는 ProjectileData 만 참조하고 prefab 직접 참조 금지.
- 배리에이션은 두 층:
  - 결정적(data): `tintColor`, `emissionMultiplier`, `facing`, `spinSpeed`, `textureVariants`, `selectMode`.
  - per-shot 랜덤: `scaleJitter`, `hueJitter`, `rotationJitter`. 시각 전용 RNG 로 시뮬레이션 결정성과 분리.
- Hit prefab 재생은 일회성. 자체 lifetime 후 자동 despawn(풀 반환). 데미지/HitFlash 는 ECS 가 그대로 처리.
- 텍스처 변종은 에디터 타임 베이크 자산. 런타임 절차 생성 금지.

## 자산 매핑 (이번 spec 범위)

| ProjectileData | 새 prefab | hit prefab |
|---|---|---|
| `Projectile_Arrow` | `Windbullet/.../WindBulletProjectile.prefab` | (kit 의 hit prefab) |
| `Projectile_Bolt` | `Stonebullet/.../StonebulletProjectile.prefab` | (kit 의 hit prefab) |
| `Projectile_CannonBall` | `Fireball/.../FireballProjectile.prefab` | `Fireball/.../FireballHit.prefab` |

## 후속 후보 (이번 spec 밖)

- **Cast prefab (머즐 플래시)**: PixPlays 키트의 `*Cast.prefab` 활용. 디펜더별 무기 anchor 결정 필요.
- **Waterball 매핑**: 새 디펜더 추가 또는 Slow effect 도입 시 자연스러운 후보.
- **디펜더별 무기 anchor 추출**: Spine bone or transform child 위치를 spawn origin 으로 사용.
- **포물선 / 호밍 궤적**: 현재는 직선 추적 유지.
- **Hit prefab 의 ECS-driven 위치 보정**: 현재는 target 위치 1회 snapshot 으로 충분.
