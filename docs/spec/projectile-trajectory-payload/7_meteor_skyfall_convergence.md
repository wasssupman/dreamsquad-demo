# 7 — Meteor 수렴: SkyFall arm + ApplyMeteor 스폰 재배선 (동작 보존) — rev2 (critic 반영)

## 목적

Meteor 스킬을 레거시 경로(`MeteorPending` → `MeteorResolutionSystem` → `MeteorBurstEvents` 큐)에서 **단일 투사체 라이프사이클**(SkyFall × TileAoe)로 **동작 보존** 이관. 시각 무변경(링+`MeteorFall` 유지). 레거시 삭제는 unit 8, 비주얼은 unit 9.

매핑: `warningSec → flightTime` · `centerWorld → impact`(셀 스냅 락) · `tileRange → impactTileRange` · 일괄 burst = TileAoe 도착 해결. (레거시도 sim 이동 없는 텔레그래프 — 시간 도메인 동일: 양쪽 다 `BattleSimGroup` scaled dt.)

## 변경 대상

- `MovementKind` enum + `ProjectileMoveSystem` — SkyFall arm (`t += dt/flightTime`, sim 위치 `impact` 고정, 도착 `t≥1`)
- `Battle/Combat/Projectile/ProjectileSpawnRequest.cs` — **`flightTime` 필드 추가** (기존엔 drain 에서 distance/speed 파생이라 부재 — SkyFall 은 이동거리 0 이라 파생 불가)
- `Battle/Combat/Projectile/ProjectileHitEvent.cs` — **`radiusWorld`(+payload 판별) 필드 추가** (현재 `{position, dataIndex}` 뿐)
- `Bridge/BattleBridge.cs` — `ApplyMeteor`(~1650) 재배선 · `SpawnProjectile` SkyFall 분기(flightTime 복사, prefab null 시 뷰 스킵) · hit drain 버스트 라우팅
- `Data/SkillData.cs` — `ProjectileData` 참조 필드 + `Data/Projectiles/Projectile_Meteor.asset` 신규(prefab **null**, SkyFall 파라미터만 — 하드코딩 금지 준수)
- `EffectSpawner.SpawnMeteor` — 미사용화(삭제는 unit 8)

## 구현

1. **스폰 seam = BattleBridge.ApplyMeteor 직접** (critic CRITICAL 반영): 유일 호출자인 `ApplyMeteor` 가 request 를 구성해 `SpawnProjectile(req, Entity.Null)` 호출(`shooter` 는 `HasBuffer` 가드(~2090)라 Null 안전). **Effects 에서 Combat 컴포넌트 쓰기 금지** — `EffectSpawner` 무접촉, ECS 캐리어 엔티티 0(레거시 드레인의 `RemoveComponent` 잔존 엔티티 누수 원천 회피). `dataIndex = GetOrCreateProjectileDataIndex(skill.projectile)` — 레지스트리는 bridge private 이라 이 seam 에서만 유효한 인덱스 확보 가능.
2. **request 값**: `movement=SkyFall, payload=TileAoe, impact=셀스냅(centerWorld), impactTileRange=RangeToTiles(skill.range), damage=skill magnitude(기존 `request.damage` 필드), flightTime=warningSec`. drain 의 SkyFall 분기가 flightTime 을 복사(BallisticArc 분기는 기존 파생 유지).
3. **뷰(viewless 합법화)**: `Projectile_Meteor.asset`(prefab null) 로 dataIndex 는 유효 — `SpawnProjectile` 이 `projData.projectilePrefab == null` 이면 `_projectileViewPool.Spawn` 스킵(현재는 null 가드 없이 NRE — `ProjectileViewPool.cs:68`). 시각은 기존 링(`SpawnMeteorWarningVisual`, 캐스트 시·MeteorPending 무관)+스트릭(`SpawnMeteorFall`)이 그대로 담당.
4. **버스트**: meteor 반경은 per-cast(`RangeToTiles(skill.range)`)라 **ProjectileData 상수로 복원 불가** → `ProjectileHitEvent.radiusWorld` 에 스냅샷. drain 에서 TileAoe 판별 → `SpawnMeteorBurst(pos, radiusWorld)` (시각 동일). artillery 임팩트 VFX 와의 분기 규칙을 drain 에 명시(판별 필드 기준).
5. HitFlash 미적용 = TileAoe 계약 유지. 순수함수(진행/도착)는 static Burst + EditMode.
6. 레거시는 컴파일 유지·미사용 (compile-safe 분할).

## 완료 기준

- compile PASS + EditMode: SkyFall 순수함수 + 기존 전체 GREEN.
- Play: meteor 캐스트 → 링+스트릭 → `warningSec` 후 동일 반경 데미지+버스트, 무회귀. `MeteorPending` 엔티티 미생성(쿼리 확인).
- 캐스트 반복 후 잔존 엔티티 증가 없음(leak 확인).

확인 2026-07-06 — 리그 EditMode 10/10+506/509 · MCP Play 검증: MeteorPending=0 유지, Projectile 1→0(소멸), HP 120→80(=magnitude 40 정확), 콘솔 클린 · 투트랙 리뷰 양측 APPROVE(주석 5건 반영) · 사용자 진행 승인. 링/스트릭 육안은 코드 무변경이라 1x 플레이에서 언제든 확인 가능.
