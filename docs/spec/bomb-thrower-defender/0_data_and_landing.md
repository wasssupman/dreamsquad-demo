# 0 — 데이터 계약 + 착지 셀 순수 함수 + 시드

## 목적

폭탄맨의 SO 필드, 캐스터 상태 컴포넌트, 착지 셀 결정 순수 함수, 결정론 시드를 놓는다.
이 단위만으로 컴파일 통과(behavior 없음, 이후 단위의 토대).

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — 폭탄 필드 추가
- `Assets/_Project/Scripts/Battle/Combat/BombLauncherState.cs` (신규) — Combat 소유 캐스터 상태
- `Assets/_Project/Scripts/Battle/Combat/BombLanding.cs` (신규) — 착지 셀 순수 함수
- `Assets/_Project/Scripts/Core/MatchSeed.cs` — `DeriveBombSeed`(salt 패턴)
- `Assets/_Project/Tests/EditMode/BombLandingTests.cs` (신규)

## 구현

- **`DefenderUnitData` 폭탄 필드** (`[Header("Bomb Thrower")]`):
  `int bombLandingTiles`(N) · `float bombTravelSec`(n) · `float bombFuseSec`(m) · `int bombAoeTileRange` · `int bombAoeTargetCap`(B) · `float bombArcHeight`(≈0 구르기) · 3변종 `float bombDamage`(데미지탄 C) · `float bombSleepSec` · `float bombStunSec`. 능력 활성 게이트 = `bombLandingTiles>0 && bombTravelSec>0`(machine-gunner `directionalAttack` 과 공존, 배타 아님).
- **`BombLauncherState`**(`IComponentData`, unmanaged): `int landingTiles; float travelSec; float fuseSec; int aoeTileRange; int aoeTargetCap; float arcHeight; float dmgBombDamage; float sleepSec; float stunSec; Random rng;`. 3변종 인라인(정확히 3종 — FixedList/배열 불필요, 제약 8). 쓰기 소유 = Combat(`AttackSystem` 이 `rng` advance). bake = unit 3.
- **`BombLanding.ResolveCell`** (순수 static): `(int2 cell, bool valid) ResolveCell(int2 casterCell, int2 cardinalDir, int tilesN, int2 gridSize)` — `cell = casterCell + cardinalDir * tilesN; valid = 0<=cell.x<gridSize.x && 0<=cell.y<gridSize.y`. cardinalDir = `DeployedFacing.value`(**이미 cardinal 단위 int2** — 파생/snap 불필요). off-grid 처리 정책은 unit 4(valid=false → 발사 스킵).
  - **추출 판단(제약 10)**: 산식 자체는 얇지만(offset+경계) grid-edge off-by-one 은 sim-critical 이동/타겟팅의 전형적 회귀 지점이라 (c)로 순수+EditMode 유지. (RNG 드로우와 달리 실제 테스트 가치가 있어 `BombTypeSelector` 드롭 기준과 구분됨.)
- **`MatchSeed.DeriveBombSeed`**: `BombSalt` 추가(Pickup/Gimmick/Meteor salt 미러), 순수 해시. 비0 보장은 호출처(bake)에서 `math.max(1u, ...)`.

## 완료 기준

- [x] compile 0 에러(Unity force refresh — 신규 .cs 임포트).
- [x] `BombLandingTests` green: 정방향 오프셋(동/서/남/북 × N) · 경계 안/밖(valid true/false) · 원점/최대 셀 edge · 음수 방향.
- [x] `DefenderUnitData` 신규 필드가 인스펙터에 노출(기존 에셋 무영향, 기본 0).

확인 2026-07-21 · compile 0 + `BombLandingTests` 8/8 green (EditMode).
