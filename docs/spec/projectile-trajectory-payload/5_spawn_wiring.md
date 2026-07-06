# 5 — Spawn Wiring (곡사 발사 경로 활성화)

## 목적

seam(unit 0~4)을 실제 발사 경로에 연결한다. `flightMode=BallisticToCell` 로 authored 된 `ProjectileData` 를 가진 디펜더가 공격하면 곡사 투사체(BallisticArc + TileAoe)가 스폰되게 한다. 이 unit 이후 곡사가 런타임에 동작한다(authored 유닛은 후속 `artillery-defender`).

## 변경 대상

- `Assets/_Project/Scripts/Data/ProjectileData.cs` — `ProjectileFlightMode` enum + `flightMode`/`arcHeight`/`impactTileRange`/`minFlightTime` 필드 (authoring)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileRef.cs` — `movement`/`payload`/`arcHeight`/`impactTileRange` 미러 필드
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — convert 2곳(디펜더 3082 / 적 3679) flightMode→(movement,payload) 매핑 + drain(`SpawnProjectile`) flightTime 산출 + ballistic ProjectileState. **⚠️ dirty 파일 → 내 hunk만 격리 커밋**
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE 곡사 분기(셀 고정 → ballistic SpawnRequest)

## 구현

- **ProjectileData**(Data): `enum ProjectileFlightMode { Homing, BallisticToCell }` + `flightMode`(default Homing)·`arcHeight`(2)·`impactTileRange`(1)·`minFlightTime`(0.3). Data→Battle 의존 회피 위해 Data 쪽 enum.
- **ProjectileRef**(Battle): `movement`/`payload`/`arcHeight`/`impactTileRange` 추가. convert 가 채운다.
- **BattleBridge convert**: `ResolveProjectileAxes(flightMode)` 헬퍼 → `BallisticToCell`→(BallisticArcToPoint, TileAoe), else (HomingToEntity, SingleSplash). 양 convert 사이트 동일 적용.
- **AttackSystem RESOLVE**: `projRef.movement == BallisticArcToPoint` 이면 `bestTargetPos` 의 셀을 `WorldToCell`→`CellToWorldCenter(y=atkPos.y)` 로 고정 → ballistic `ProjectileSpawnRequest`(movement/payload/impact/arcHeight/impactTileRange, damage=Damage output 합산) 스테이징, **homing SpawnRequest 스킵**(else 분기). 셀 고정은 RESOLVE 시점(재타겟된 bestTargetPos).
- **BattleBridge drain(SpawnProjectile)**: ballistic 이면 `origin=spawnPos`, `impact=(req.impact.x, spawnHeight, req.impact.z)`(origin 과 동일 Y 평면), `flightTime=BallisticArc.FlightTime(origin, impact, speed, data.minFlightTime)` 산출 후 ProjectileState 에 세팅. homing 은 기존대로.

## 완료 기준

- [x] refresh scope=all → 컴파일 0 에러.
- [x] 홈잉 회귀 유지(EditMode 498/499, 1 실패=무관 ObstaclePlacer). ballistic projRef → ballistic SpawnRequest 분기 테스트 green(movement=Ballistic·payload=TileAoe·target Null·impact 셀락·damage 합산).
- [x] **두 트랙 리뷰 게이트(wiring)** 통과 (ecs PASS · general 수정후통과, 반영: speed→Shared / U1 movement assert / atkPos.y→0f placeholder).
- [x] 내 hunk만 격리 커밋(BattleBridge dreamstone CostRate WIP 무손상).
- [ ] Play 통합(곡사 실발사→arc→AOE)은 unit 6.

완료 확인: 2026-07-06 — 양트랙 리뷰 통과, EditMode 498/499, hunk 격리 커밋.
