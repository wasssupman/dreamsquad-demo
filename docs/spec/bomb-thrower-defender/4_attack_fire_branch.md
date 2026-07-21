# 4 — AttackSystem 폭탄 발사 분기

## 목적

`AttackSystem` 에 폭탄맨 발사 경로를 넣는다: 쿨다운마다 무조건, 방향×N 착지 셀로,
랜덤 3종 중 하나를 GrenadeToCell×TileAoe 로 발사. 이 단위 후 sim end-to-end 성립.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 방향 분기에 폭탄 경로 추가

## 구현

- **분기 게이트**: `DeployedFacing` 보유 유닛 중 **`BombLauncherState` 있으면 폭탄 경로**(볼리/lane-gate 로 안 감, 계약 9). 없으면 기존 볼리(machine-gunner).
- **발사 조건 = blind bombardment**(계약 1): `AttackState` 쿨다운 준비되면 **적 유무·lane 검사 없이** 발사. 쿨다운은 발사 시 리셋(볼리처럼 버스트 없음 — 폭탄 1발/쿨다운).
- **착지 셀**(그리드 인프라 이미 AttackSystem 에 존재 — 신규 배선 0): `flowField`(AttackSystem:91)·`facingLookup`(:55) 재사용. `casterCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, ffOrigin)`, `cardinalDir = facingLookup[entity].value`(**이미 cardinal int2**), `(cell, valid) = BombLanding.ResolveCell(casterCell, cardinalDir, landingTiles, gridSize)`, `impactWorld = GridMath.CellToWorldCenter(cell, ...)`. **볼리 착탄 셀 계산(:580-581)과 동형 패턴**. **`valid=false`(off-grid) → 발사 스킵 + 쿨다운 리셋**(재스캔 스팸 방지, 엣지 배치 안전망).
- **랜덤 타입**(계약 6, 인라인): `int t = rng.NextInt(0, 3)`(state 의 rng advance, RW). 변종 매핑(3-way switch, 인라인):
  - `0` 데미지탄: `damage=dmgBombDamage, ccKind=0`
  - `1` 수면탄: `damage=0, ccKind=(byte)CcKind.Sleep, ccDuration=sleepSec`
  - `2` 스턴탄: `damage=0, ccKind=(byte)CcKind.Stun, ccDuration=stunSec`
  - `bombType=t` 도 request 에 실어 뷰 변종(색)용(계약 8 — 뷰가 해석).
- **spawn request enqueue**: `ProjectileSpawnRequest{ movement=GrenadeToCell, payload=TileAoe, origin=casterWorld, impact=cellCenterWorld, impactTileRange=aoeTileRange, aoeTargetCap=B, flightTime=travelSec(n), fuseSec(m), arcHeight, damage, ccKind, ccDuration, bombType, dataIndex=폭탄 ProjectileData, targetFaction=Enemy }`. `SpawnProjectile` drain 이 엔티티+뷰 생성(2단계, 기존).
- CC/데미지 값은 발사 시점 확정 스냅샷 — 착지 후 캐스터 사망해도 폭탄 자립(계약 1).

## 완료 기준

- [x] compile 0 에러.
- [x] 전체 EditMode 회귀 없음 — 1189 passed / 0 failed / 2 skipped(기존 known-skip). 분기가 `BombLauncherState` 게이트라 기존 유닛 무영향.
- [ ] (통합 Play, unit 6) 폭탄맨 배치 → 쿨다운마다 방향×N 칸으로 폭탄 발사(적 없어도) → n초 구르기 → m초 → 폭발.
- [ ] 3종이 랜덤하게 섞여 나옴(데미지/수면/스턴). 같은 seed 재현.
- [ ] 엣지에 바깥 방향 배치 시 크래시 없이 발사 스킵.

확인 2026-07-21 · compile 0 + 전체 EditMode 1189/0/2(회귀 없음). Play 는 unit 6 통합.
