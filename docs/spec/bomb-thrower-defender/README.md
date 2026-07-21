# bomb-thrower-defender — 폭탄맨 (구르는 폭탄 투척 defender)

> 상태: 스펙 작성 2026-07-21 · 구현 대기

## 목표

방향지정(머신거너) 메커니즘을 재사용해, **쿨다운마다 방향×N 고정 칸으로 폭탄을 굴려 보내는** defender 를 추가한다.
폭탄은 착지 후 퓨즈를 거쳐 폭발하며, **3종을 랜덤 투척**(데미지 / 수면 / 스턴)한다.

```
발사 ──(travelSec n, 고정)──▶ 착지(방향×N 칸) ──(fuseSec m, 고정)──▶ 폭발(가까운 순 B명 AoE)
```

- **조준 = 머신거너 재사용**: `directionalAttack=1` → `DeployedFacing`(4방향) + `DirectionAimController`. N(착지 거리)은 SO 고정, 방향만 조준.
- **발사 = blind bombardment**: 쿨다운마다 적 유무 무관하게 발사 (머신거너 lane-gate 와 대비). 착지 셀 고정 = 초크 zone-denial.
- **굴리기 = 순수 전달**: 구르는 도중 적 무시. travel n 초는 거리 N 과 무관하게 **고정**(request-carried, SkyFall 관례). 낮은 arc + 뷰 tumbling 회전으로 "데굴데굴" 지면 구르기.
- **폭발 = 가까운 순 최대 B명** AoE (착지 셀 `impactTileRange` 내). 데미지탄=피해 C / 수면탄=피해0+Sleep / 스턴탄=피해0+Stun.
- **3종 랜덤 = 결정론**: 캐스터별 seeded RNG(`MatchSeed` 시드, order-independent) — 비동기 토너먼트 재현성.

검증 질문: **"굴러간 폭탄이 착지→퓨즈→폭발로 초크를 압박하고, 3종(데미지/수면/스턴)이 랜덤하게 체감되는가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터/순수 | `0_data_and_landing.md` | `DefenderUnitData` 폭탄 필드 + `BombLauncherState`(Combat) + `BombLanding.ResolveCell` 순수 + `MatchSeed.DeriveBombSeed` + EditMode |
| 1 | ECS 이동 | `1_grenade_movement.md` | `MovementKind.GrenadeToCell` arm(travel n + fuse m + arrive n+m) + `ProjectileState.fuseSec` + SpawnRequest 필드 + Bridge spawn 셋업 |
| 2 | ECS 폭발 | `2_aoe_cap_and_cc.md` | `AoeTargetCap.SelectNearest` 순수 + EditMode + `ProjectileHitSystem` TileAoe 가까운 순 B cap + CC enqueue(`EnemyCcEvents`) |
| 3 | Bridge 배선 | `3_bridge_bake.md` | `CreateDefenderEntity` 조건부 `BombLauncherState` bake + 캐스터별 RNG seed |
| 4 | ECS 발사 | `4_attack_fire_branch.md` | `AttackSystem` 방향 분기: `BombLauncherState` 유닛 → 쿨다운마다 무조건 발사 + `ResolveCell` + 인라인 랜덤 타입 |
| 5 | View/VFX | `5_bomb_view_and_vfx.md` | 데굴데굴 구르는 폭탄 뷰(tumbling 회전 + 낮은 arc) + 퓨즈 블링크 + 폭발 크레이터(기존 재사용) |
| 6 | 에셋 | `6_unit_asset_and_catalog.md` | `Defender_BombMan.asset` 저작 + 카탈로그 등록 + Play 검증 |

## Feature-wide 계약

1. **타이밍 = 2단계 고정 타이머**. travel `n`(SO, 거리 무관 고정) → 착지 → fuse `m`(SO) → 폭발. 총 n+m. 발사 후 캐스터 상태와 무관하게 타임라인 보장(투사체 자립).
2. **travel n 은 request-carried 고정** — 속도/거리 유도 아님(BallisticArc 의 `FlightTime` 대신 SkyFall 처럼 `req.flightTime` 직접 사용). 먼 칸일수록 구르는 시각 속도만 빨라짐(허용).
3. **퓨즈는 movement 타이밍의 몫** — `MovementKind.GrenadeToCell` arm 이 travel+fuse+arrive 전부 소유. `elapsed≥n+m` 에서만 `impactReached=true`. **`ProjectileHitSystem` 은 fuse 를 모름**(기존 `impactReached` 게이트로 폭발만 해결). Movement=타이밍 / Combat=해결 분리 유지.
4. **폭발 AoE = 가까운 순 최대 B명** — 착지 셀 `impactTileRange`(Chebyshev) 내 후보 중 impact 중심 거리 오름차순 B명, 인덱스 tie-break(결정론). cap=0 이면 기존 무제한(메테오/스킬/보스 경로 무회귀). 산식은 순수 `AoeTargetCap.SelectNearest` — sim-critical, EditMode 필수(제약 10).
5. **CC 는 기존 채널 재사용** — 수면/스턴탄은 피해0 + `EnemyCcEventsSingleton`(Combat→Effects, 수면파이터 선례)로 Sleep/Stun enqueue. **신규 NativeQueue 채널 0.** TileAoe cap/CC 는 전부 조건부(default off = 레거시).
6. **3종 랜덤 = 캐스터별 seeded RNG** — `BombLauncherState.rng`(`Unity.Mathematics.Random`), bake 시 `MatchSeed.DeriveBombSeed(matchSeed) ^ cellHash`(비0 보장)로 seed. `AttackSystem` 이 발사마다 `NextInt(0,3)` advance — order-independent(캐스터마다 독립 stream).
7. **로직/아키텍처 분리(제약 10)**: 순수 함수는 `BombLanding.ResolveCell`(facing→cardinal→셀+경계) + `AoeTargetCap.SelectNearest`(가까운 순 B) 2개. movement 위치는 기존 `BallisticArc` lerp+arc 재사용(신규 순수 0). 타입 선택(`NextInt`)·변종 매핑(3-way switch)·arrive 임계·CC enqueue 는 자명/아키텍처-bound 이라 인라인.
8. **값의 아키텍처-blind 흐름**: `bombType`(변종 인덱스)·`fuseSec`·`elapsed` 는 plain unmanaged 필드. **Combat**(HitSystem)은 데미지/CC 로, **Presentation**(뷰)은 색·구르기·블링크로 독립 해석 — 생산자도 값도 상대 소비자를 모른다.
9. **폭탄이 유일한 공격** — 기본공격 없음(머신거너 동형). `attackCooldown` = 폭탄 투척 간격.
10. 하드코딩 금지 — N/n/m/B/AoE범위/arc/3변종 수치 전부 SO.

## 파이프라인 커버리지 (Defender + Projectile 아키타입 대조)

`docs/reference/object-pipeline-map.md` 의 Defender + Projectile 표 대조. 기존 정거장 전부 재사용, arm/필드만 확장.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_BombMan.asset` 신규 + `DefenderUnitData` 폭탄 필드 + **DefenderCatalog 등록**(unit 6) + `ProjectileData`(폭탄 프리팹, 기존 SO 필드) |
| 스폰 진입점 | 기존 `PlaceDefenderAs`→`CreateDefenderEntity`(`BombLauncherState` 조건부 bake, unit 3) · 폭탄 발사 = `AttackSystem` request stage → `DrainProjectileSpawnRequests`→`SpawnProjectile`(2단계 유지, unit 1·4) |
| ECS 컴포넌트 | **신규 1**: `BombLauncherState`(Combat). 재사용: `DeployedFacing`(Units)·`AttackState`. `ProjectileState` 에 `fuseSec` 필드 1개 추가 |
| 시뮬 시스템 | 신규 시스템 **0**. `ProjectileMoveSystem` GrenadeToCell arm 추가(unit 1) · `ProjectileHitSystem` TileAoe cap/CC arm 확장(unit 2) · `AttackSystem` 폭탄 발사 분기(unit 4) |
| 이벤트 큐 | **신규 채널 0** — `ProjectileSpawnRequest`·`ProjectileHitEventsSingleton`·`EnemyCcEventsSingleton` 전부 재사용 |
| View/Pool | 기존 `ProjectileViewPool`(폭탄 프리팹) + `SpineUnitPool`(유닛). 데굴데굴 회전·퓨즈 블링크 = 뷰 확장(unit 5). 폭발 = 기존 TileAoe hit 크레이터 |
| 체력 표시 | N/A — 실드 같은 신규 표기 없음 |
| 씬 wiring | N/A — 신규 SerializeField 없음. 카탈로그 등록 + 폭탄 프리팹은 ProjectileData SO 경유 |

## 후속 후보

- **가중치 랜덤 3종** [S] · 현재 균등 1/3(인라인 `NextInt`). 데미지탄 확률↑ 등 가중치 도입 시 `BombTypeSelector.Pick(weights, roll)` 순수 함수 추출(그때 제약 8 통과).
- **폭탄 종류 확장** [S] · 슬로우/넉백/독 폭탄 등. `BombLauncherState` 3변종 → N변종(FixedList) + 변종 매핑 확장.
- **착지 예고 오버레이** [S] · 착지 셀 + AoE 범위 텔레그래프(스폰 예고 계열 폴링). 적 회피 판단 정보.
- **거리 조준 변종** [S] · N 을 플레이어가 조준(드래그 거리). 머신거너 aim 확장 필요 — 별도 UX.
- **en-route 구르기 피해 변종** [M] · "굴러가며 스치는 적도 타격"(PathHit + TileAoe 합성). 현재 순수 전달과 다른 유닛으로 분리.
- **전용 폭탄/폭발 아트** [S] · v1 은 기존 투사체 프리팹 recolor + TileAoe 크레이터. 종류별 색(데미지/수면/스턴) + 전용 폭발 VFX.
