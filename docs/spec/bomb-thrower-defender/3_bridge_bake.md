# 3 — BattleBridge: BombLauncherState bake + 캐스터별 RNG 시드

## 목적

배치 시 폭탄맨 SO 필드를 `BombLauncherState` 로 bake 하고, 결정론 RNG 를 캐스터별로 시드한다.
이 단위 후 발사(unit 4)가 참조할 상태가 엔티티에 존재.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateDefenderEntity` 조건부 bake

## 구현

- **조건부 bake**(`CreateDefenderEntity`, `ShieldCastState`/`VolleyFireState` 조건부 bake 선례): `unitData.bombLandingTiles > 0 && unitData.bombTravelSec > 0` 일 때만 `AddComponent<BombLauncherState>`.
- **필드 복사**: `landingTiles/travelSec/fuseSec/aoeTileRange/aoeTargetCap/arcHeight/dmgBombDamage(=bombDamage)/sleepSec/stunSec` ← SO. AoE 범위는 SO `bombAoeTileRange`(→ `BombLauncherState.aoeTileRange`, unit 4 가 request 의 `impactTileRange` 로 전달) — 공격범위(`attackRange`) 재사용 아님, 폭탄은 착지 셀 기준 독립 AoE.
- **RNG 시드**(계약 6): `rng = new Unity.Mathematics.Random(math.max(1u, MatchSeed.DeriveBombSeed(_matchSeed) ^ cellHash))`. `cellHash` = 배치 셀(`DefenderTile`/placement cell) 파생 — **캐스터마다 독립 stream**(order-independent). `_matchSeed` 은 이미 map build 시 확정(meteor barrage 선례). 비0 보장.
- 폭탄맨은 `directionalAttack=1`(조준) + `BombLauncherState`(발사) 조합. `DeployedFacing` bake 는 기존 방향지정 경로 그대로(unit 4 가 읽음).
- **주의**: machine-gunner 도 `directionalAttack=1` 이지만 `BombLauncherState` 없음 → `AttackSystem`(unit 4)이 상태 유무로 분기(폭탄 vs 볼리). bake 게이트가 그 분기의 source.

## 완료 기준

- [x] compile 0 에러.
- [ ] (unit 6 에셋 배치 Play) 폭탄맨 배치 시 `BombLauncherState` 부착 확인(entity inspector / 로그). 기존 유닛(볼리/실드/일반)엔 미부착.
- [ ] 같은 `_matchSeed` + 같은 배치 → 같은 폭탄 타입 시퀀스(결정론, unit 4 통합 시 관측).

확인 2026-07-21 · compile 0. bake/결정론 관측은 unit 6 Play(에셋 배선 후).
