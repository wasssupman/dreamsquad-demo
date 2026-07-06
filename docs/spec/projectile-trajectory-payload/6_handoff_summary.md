# 6 — Handoff Summary

> 엔진 리팩터(units 0~5) 완료 2026-07-06. Play e2e 검증은 후속 `artillery-defender` 로 이관.

## Commit

- `e5836bc` unit 0-1 — 궤적×페이로드 축 분해 seam + 홈잉 이관
- `23723d2` unit 2 — TileAoe 반경 멤버십 순수함수
- `1ad21bf` unit 3 — BallisticArc 궤적 arm
- `9aec824` unit 4 — TileAoe payload arm (곡사 AOE seam 완성)
- `27a452a` unit 5 — 곡사 발사 배선 (hunk 격리 커밋)

## Implemented

- 투사체 = **궤적(MovementKind) × 페이로드(PayloadKind)** 직교 2축. 단일 라이프사이클(spawn→Move→Hit→파괴), 궤적/페이로드별 별도 시스템·태그 0.
- MoveSystem `switch(movement)`: Homing(추적, 도착=거리) / BallisticArc(arc, 도착=elapsed≥flightTime). 각 arm 이 `impactReached` 세팅.
- HitSystem `switch(payload)`: SingleSplash(기존 outputs+splash+HitFlash) / TileAoe(착탄 셀 반경 flat, HitFlash 없음).
- `BallisticArc.ArcPosition`/`FlightTime`, `TileAoe.IsInTileRange` static 순수함수 + EditMode.
- `ProjectileData.flightMode`(authored) → convert 가 (movement,payload) 매핑 → AttackSystem 이 셀 고정 후 ballistic SpawnRequest → drain 이 flightTime 산출.
- 곡사 경로 런타임 활성: `flightMode=BallisticToCell` ProjectileData 를 가진 디펜더가 발사시점 셀에 포물선 AOE 셸 발사.

## Key Files

- `Battle/Combat/Projectile/`: `MovementKind.cs`·`PayloadKind.cs`(축), `ProjectileState.cs`(+impactReached/축필드), `ProjectileSpawnRequest.cs`, `ProjectileMoveSystem.cs`(switch), `ProjectileHitSystem.cs`(switch), `BallisticArc.cs`, `ProjectileRef.cs`
- `Battle/Combat/TileAoe.cs` (Meteor 와 공유 예정 primitive)
- `Data/ProjectileData.cs` (ProjectileFlightMode + ballistic 필드)
- `Bridge/BattleBridge.cs` (`ResolveProjectileAxes` + convert 2곳 + `SpawnProjectile` drain)
- `Battle/Combat/AttackSystem.cs` (RESOLVE 곡사/홈잉 분기)
- Tests: `ProjectileSystemTests`, `BallisticArcTests`, `TileAoeTests`, `AttackSystemUnifiedLoopTests`(곡사 분기)

## Verified

- EditMode 498/499 (유일 실패 `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio` ≥36 기대 31 = **HEAD 기존 결함, 무관**).
- 홈잉 무회귀(6 프로젝타일 테스트), 곡사 arc/AOE/분기(신규 테스트), 양트랙 리뷰 게이트 2회(unit 1, unit 3+4, unit 5) 전부 통과.
- Play e2e 미실행 — 후속 이관.

## Notes (되돌리면 안 되는 의도)

- **홈잉은 unit 1 에서 한 번만 수정하고 봉인.** arrival 을 MoveSystem 이 `impactReached` 로 신호, HitSystem 이 소비. units 3/4/5 는 홈잉 코드 무수정.
- **TileAoe 는 AOE 중심을 `ProjectileState.impact`(고정)에서 읽는다.** payload=TileAoe 스폰은 궤적 무관 `impact` 락 필요 — 현재 `ResolveProjectileAxes` 가 Ballistic 과만 페어링해 자연 충족. Homing+TileAoe 는 impact=타겟 도착위치 락 필요(후속).
- drain 이 origin·impact 를 **동일 spawnHeight 평면**에 두고 arc bump 만 Y. AttackSystem 의 impact.y 는 placeholder(drain 이 덮음).
- `minFlightTime` authored(ProjectileData, 0.3) — point-blank 즉시착탄 방지. 하드코딩 아님.
- 두 switch 모두 `default` arm(Move=destroy 누수방지 / Hit=parity).
- **BattleBridge dirty**: dreamstone `CardBuffKind.CostRate` 주석 + `UiLayer.Apply` UI WIP + Unity 재직렬화 노이즈는 **내 것 아님**. unit 5 는 hunk 격리로 이들 미포함. 여전히 uncommitted.

## Follow-up

→ `docs/spec/README.md` Follow-up Backlog 및 `docs/spec/artillery-defender/`:
- **artillery-defender** (authored 곡사포 유닛 + **Play e2e 검증 = 이 리팩터의 첫 실증**)
- Meteor 를 `TileAoe.IsInTileRange` 로 수렴(dedup 완성)
- non-Damage payload(slow-곡사포) · 임팩트 CC · arcHeight 거리비례 · Bezier 궤적 · Homing+TileAoe
