# 2 — 폭발 AoE: 가까운 순 B 상한 + CC 부여

## 목적

`TileAoe` 페이로드를 확장해 (1) 착지 셀 범위 내 **가까운 순 최대 B명**만 맞히고,
(2) 수면/스턴탄이 **CC 를 부여**하게 한다. 기존 무제한·데미지전용 경로는 무회귀.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AoeTargetCap.cs` (신규) — 가까운 순 B 선별 순수 함수
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs` — `int aoeTargetCap; byte ccKind; float ccDuration; byte bombType`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs` — 동일 4필드 (`bombType` = 뷰 변종 인덱스, sim 무해 캐리어 — unit 4 세팅, unit 5 뷰가 읽음. 여기서 **선언만** 해 unit 4 컴파일 선행 확보)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — TileAoe arm 확장
- `Assets/_Project/Tests/EditMode/AoeTargetCapTests.cs` (신규)

## 구현

- **`AoeTargetCap.SelectNearest`** (순수 static, `ShieldTargeting.Select` 동형): 입력 = 후보 `NativeArray<float> distanceSq`(범위 내 victim 만), `int cap`, `ref NativeList<int> outIndices`. `cap<=0` → 전 인덱스(무제한). `cap>0` → distanceSq 오름차순 `cap`개, 동률은 인덱스 오름차순(selection sort, 결정론). sim-critical, EditMode 필수(제약 10·4).
- **TileAoe arm 재구성**(cap=0 && ccKind=0 → 기존과 동일 결과 보장):
  1. 범위 내(`TileAoe.IsInTileRange`) victim 인덱스 + `distancesq(impact중심, victimPos)` 수집(Temp).
  2. `AoeTargetCap.SelectNearest(distSq, aoeTargetCap, selected)`.
  3. selected 각 victim: `damage>0` 이면 `IncomingDamage` append(기존 prioMul/heavyMul/ThreatCredit 경로 유지) · `ccKind != 0` 이면 `EnemyCcEventsSingleton` 에 `EnemyCcEvent{ target, effect = CcEffect{ kind=(CcKind)ccKind, remainingTime=ccDuration } }` enqueue.
- **CC 채널**: `ProjectileHitSystem` 에 `TryGetSingletonRW<EnemyCcEventsSingleton>` 추가(기타 채널 선례). Combat→Effects — 수면파이터(`AttackSystem`)가 이미 쓰는 채널, 신규 0(계약 5). 테스트 월드 무해(옵셔널 게이트).
- 데미지탄: `damage=C, ccKind=0`. 수면탄: `damage=0, ccKind=Sleep, ccDuration=sleepSec`. 스턴탄: `damage=0, ccKind=Stun, ccDuration=stunSec`. 데미지·CC 는 **동일 capped victim 집합**에 적용.
- 폭발 크레이터 VFX(`ProjectileHitEvent` TileAoe)는 기존대로 impact 중심 1회 — cap/CC 무관.

## 완료 기준

- [ ] compile 0 에러.
- [ ] `AoeTargetCapTests` green: cap<=0 무제한 · cap<후보수 가까운 순 절단 · 동률 인덱스 tie-break · cap>=후보수 전원.
- [ ] 기존 EditMode(`TileAoeTests`/meteor/skill) green — cap=0·ccKind=0 경로 바이트 무변경.
- [ ] (통합 Play, unit 6) 데미지탄 최대 B명 피해 · 수면/스턴탄 최대 B명 CC(피해0).
