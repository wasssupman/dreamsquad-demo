# 4 — TileAoe Payload (반경 AOE 페이로드 arm)

## 목적

페이로드 축의 **두 번째 arm** 을 `ProjectileHitSystem` switch 에 추가한다: `TileAoe` — 착탄 셀 기준 반경 내 적 전원에 flat 데미지. 이로써 페이로드 축도 2 구현체(SingleSplash + TileAoe)가 되어 seam 이 완성된다. **SingleSplash arm 무수정.** unit 3 의 BallisticArc 궤적과 조합되면 곡사 AOE 가 성립(발사 경로는 unit 5).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` (switch 에 arm 추가 + FlowFieldSingleton 그리드 파라미터 hoist + `using Wassup.Battle.Movement`)
- `Assets/_Project/Tests/EditMode/ProjectileSystemTests.cs` (TileAoe 통합 테스트 1개)

## 구현

- `PayloadKind.TileAoe` arm:
  - AOE 중심 = `projectile.impact`(셀 고정 착탄점) → `GridMath.WorldToCell`.
  - candidate = 기존 `aoeEntities`/`aoeTransforms` 스냅샷(AttackUnitTag) 재사용. 각 후보 셀 계산 후 `TileAoe.IsInTileRange(cell, centerCell, impactTileRange)` 통과 시 `IncomingDamage{ projectile.damage }` append.
  - **데미지 = `projectile.damage`**(spawn 시 Damage output 합산분). 새 필드 없음(계약 #6). non-Damage output(stat/stack) 미적용 = v1 Damage-only.
  - impact VFX = `ProjectileHitEventsSingleton` 에 `position=impact`, `dataIndex` enqueue(크레이터 위치).
  - **HitFlash 미적용**(AOE 는 per-target flash 안 함 = Meteor 선례).
- 그리드 파라미터(tileSize/gridSize/origin)는 `FlowFieldSingleton` 에서(Meteor 와 동일), loop 밖으로 hoist. 없으면 defaults(tileSize 1 / 128² / 0) — 이른 프레임/테스트 안전.

## 완료 기준

- [x] refresh scope=all → 컴파일 0 에러.
- [x] `ProjectileSystemTests` TileAoe 통합 green: 착탄 반경 내 적 전원 flat 데미지, 반경 밖 무피해. 홈잉/SingleSplash 회귀 유지. (EditMode 497/498)
- [x] **두 트랙 리뷰 게이트(unit 3+4)** 통과 (ecs PASS · general 수정후통과, Low 3건 반영: HitSystem default arm + 테스트 내부셀 견고화 + impact 락 계약 노트).

## unit 5 인계 (리뷰 L2)

`TileAoe` payload 는 AOE 중심을 `ProjectileState.impact` 에서 읽는다. 따라서 **unit 5 spawn 은 payload=TileAoe 일 때 궤적과 무관하게 `impact` 를 락**해야 한다(v1 은 BallisticArc 페어링뿐이라 자연 충족, 하지만 계약으로 고정). Homing arm 은 `impact` 를 갱신하지 않으므로 Homing+TileAoe 조합은 후속(impact=타겟 도착위치 락 필요).

완료 확인: 2026-07-06 — 양트랙 리뷰 통과, EditMode 497/498.
