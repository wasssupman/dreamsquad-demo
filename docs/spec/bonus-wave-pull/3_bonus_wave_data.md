# 3 — 보너스 웨이브 데이터와 보너스 적 에셋

## 목적

보너스 당기기의 **모든 수치**를 한 에셋이 소유하게 하고(제약 6), 보너스 적 SO 를 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BonusWaveData.cs` — 신규 SO
- `Assets/_Project/Data/BonusWaveData.asset` — 신규
- `Assets/_Project/Data/Enemies/Enemy_DreamShard.asset` — 신규
- `Assets/_Project/Data/EnemyCatalog.asset` — 등록

## 구현

1. `BonusWaveData : ScriptableObject` 필드:
   - `AttackUnitData enemyUnit`
   - `int enemyCount = 10` (계약 2). **`portalCount` 는 두지 않는다** — 포탈 개수의 소유자는
     맵이다(런타임 분모 = `bonusSpawns.Length`, 저작 계약 = `RequiredPortalCount`). 리뷰 H1.
   - `float portalAppearDelaySec = 1f` · `float firstSpawnDelaySec = 2f`
     (포탈 등장 후 첫 스폰까지) · `float spawnIntervalSec`
   - `float portalLingerSec` (마지막 스폰 후 포탈이 남는 시간)
   - `int killThreshold` (계약 12 의 N)
   - `float maxStressToOffer = 30` (계약 15 — 스트레스 창, unit 9)
   - `OnValidate` — `killThreshold > enemyCount` 불변식(계약 12). 위반 시 LogError.

2. 보너스 적 `AttackUnitData`:
   - `tier = Normal`(계약 6) · `huntsDefenders = true` · 근접 · 체력·공격력 낮게
   - `waypointPathIndex = -1` **필수** — 0 이상이면 없는 경유점을 밟다 경고 폴백
   - `targetFactions` 는 **기본 마스크 그대로**(0 으로 두어 `Resolve` 폴백). `DefenderCore`
     포함이 곧 공성이고 그것이 목표문의 「거점을 패러 이동」이다(계약 7ⓒ)
   - ⚠ 기존 에셋을 복제했다면 `targetFactions` 를 **0 으로 되돌린다**(2026-08-13 사고)
   - `stabilityDamage` 를 저작 결정으로 명시(잡몹 기본 1 · Duel `goalStabilityMax` 1000)
   - `minWaveNumber`/`maxPerWave` 는 의미 없다 — 생성기를 안 타므로 기본값

3. **덱 풀에 넣지 않는다**(계약 4). `Deck_*.asset` 무접촉 → 일반 웨이브 편성 diff 0,
   시드 재기준 불필요, `waveGeneratorVersion` 범프 불필요.

4. `EnemyCatalog` 에 등록한다. 시트 관리 대상이므로(계약 14) `Data/Enemies/` 에 둔다 —
   `UnitStatExporter` 가 그 폴더를 전수 스캔한다.

5. unit 1 의 `BonusSpawnAuthoringRules` 에 **개수 규칙**(0 또는 `portalCount`)을 붙인다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] `BonusWaveData.OnValidate` 가 `killThreshold <= enemyCount` 를 잡는다
- [x] `EnemyCatalogAuthoringTests` green (등록 자동 편입)
- [x] `AuthoredTargetMaskTests` green — 마스크를 좁히지 **않으므로** 통과. 이유를 주석으로
- [x] `WaveKillBudgetPinTests` green — 풀 미삽입이라 무영향
- [x] EditMode + Assets lane green

**확인 2026-08-24** — `BonusEnemyNotInDeckTests`(2) 가 덱 풀 미삽입과 임계 불변식을 고정.
`AuthoredTargetMaskTests`·`EnemyCatalogAuthoringTests`·`WaveKillBudgetPinTests` green.
