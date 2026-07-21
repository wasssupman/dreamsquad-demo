# 1 — GrenadeToCell 이동 arm (travel + fuse + arrive)

## 목적

폭탄의 2단계 고정 타이머 이동을 신규 `MovementKind` arm 으로 추가한다.
travel(n) 동안 셀로 구르고, fuse(m) 동안 착지 정지, `n+m` 에서 도착(폭발은 unit 2 가 해결).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/MovementKind.cs` — `GrenadeToCell` enum case 추가
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs` — `float fuseSec` 필드
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs` — `float fuseSec` 필드
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileMoveSystem.cs` — GrenadeToCell arm
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnProjectile` GrenadeToCell 셋업

## 구현

- **arm 계약**(계약 3): `elapsed += dt`.
  - `elapsed < flightTime` → travel: `t = saturate(elapsed/flightTime)`, 위치 = **`BallisticArc.ArcPosition(origin, impact, arcHeight, t)` 재사용**(BallisticArcToPoint arm 이 쓰는 바로 그 순수 헬퍼 — 신규 순수 0). arcHeight≈0 이면 사실상 지면 직선 이동(구르기).
  - `elapsed >= flightTime` → 착지: 위치 = `impact` 고정(퓨즈 동안 정지).
  - `elapsed >= flightTime + fuseSec` → `impactReached = true`(도착 = 폭발 신호).
  - **`flightTime` = travel n(request-carried 고정)**, `fuseSec` = m. `impactReached` 이전엔 절대 resolve 안 됨(HitSystem 무변경, 계약 3).
- **`SpawnProjectile` GrenadeToCell 분기**(SkyFall 분기 미러): `state.origin = spawnPos; state.impact = req.impact; state.impactTileRange = req.impactTileRange; state.flightTime = math.max(req.flightTime, 0f); state.fuseSec = math.max(req.fuseSec, 0f); state.arcHeight = req.arcHeight;`. **`BallisticArc.FlightTime` 호출 안 함**(속도 유도 금지 — 계약 2).
- `elapsed`/`flightTime`/`fuseSec` 는 뷰가 읽어 구르기/착지/블링크 상태 해석(unit 5) — plain 값(계약 8).
- ProjectileHitEvent 등 기존 arm 무변경. GrenadeToCell 은 PathHit 처럼 in-flight resolve 하지 않음 — 순수 도착형(계약 3).

## 완료 기준

- [ ] compile 0 에러.
- [ ] (unit 4 발사 배선 후 통합 Play) 폭탄이 n초 구른 뒤 착지, m초 후 도착 플래그 → 폭발. 거리 N 달라도 travel 은 n초 고정.
- [ ] 기존 투사체(Homing/Ballistic/SkyFall/Directional) 회귀 없음 — `fuseSec` 기본 0, GrenadeToCell 외 경로 무영향.
