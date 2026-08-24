# 4 — 보너스 웨이브 스케줄러

## 목적

버튼이 눌린 뒤 「1초 → 포탈 2개 → 2초 → 10기 순차 스폰」을 **기존 웨이브 생성과 완전히
분리된 경로**로 결정론적으로 굴린다(계약 1·3).

## 변경 대상

- `Assets/_Project/Scripts/Data/BonusWaveSchedule.cs` — 신규(순수 함수)
- `Assets/_Project/Scripts/Battle/Units/BonusWaveTag.cs` — 신규
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/BonusWaveScheduleTests.cs` — 신규

## 구현

1. **순수 함수** `BonusWaveSchedule.Build(portalCount, enemyCount, firstSpawnDelaySec, spawnIntervalSec)`
   → `(portalIndex, timeOffsetSec, ringIndex)[]`.
   - 포탈 배분 = `i % portalCount`
   - 시각 = `firstSpawnDelaySec + i * spawnIntervalSec`
   - RNG 없음. 제약 10 의 (c) sim-critical 에 해당하므로 순수 분리 + 테스트.

2. `BonusWaveTag : IComponentData`(빈 태그)를 **Units 맥락**에. 소비자 둘:
   전멸 판정 제외(계약 10)와 트리거 카운터 분리(계약 12, unit 5).

3. 브리지 상태: `_bonusPending`(전용 엔트리 리스트) · `_bonusWaveActive`.
   **`_pending.Clear()` 가 있는 두 곳**(`BeginPlacement`·`StartBattle`) 양쪽에서 같이 리셋한다.

4. 스폰 펌프는 **`TickBattleFrame` 안**, 기존 `_pending` 루프 옆에 둔다. `Update` 직하에 두면
   sim 하네스(`StepOneTick` → `TickBattleFrame`)와 라이브가 갈린다. 시각 기준은 `Time` 이
   아니라 **`_battleClock`** 이다(정지·슬로우모 반영).

5. 스폰은 `CreateEnemyEntity(so, worldPos, -1, -1)` 재사용 + 직후 `BonusWaveTag` 부착.
   위치는 **분열 레시피 복제** — 셀 중심 + 반경 `tileSize * 0.25` · 각도 `2π·i/count`.
   레인 스폰의 `ComputeSpawnLateralOffset` 은 래퍼 전용이라 이 경로에 없다. 복제하지 않으면
   여러 기가 한 점에 태어나 좁은 복도에서 교착 조건에 들어간다.

6. **전멸 판정 전용 쿼리**(계약 10):
   `_aliveNormalAttackersQuery = CreateEntityQuery(ReadOnly<AttackUnitTag>, None<BonusWaveTag>)`.
   `NoQueuedAttackersRemain()` 만 이걸 쓰고, `_bonusPending` 도 그 판정에 **넣지 않는다**.

   ⚠ **`_aliveAttackersQuery` 자체는 절대 건드리지 않는다.** 11곳이 공유하고 거기엔 슬로우·
   토네이도·메테오 사전집계, `CollectEnemiesInTileRange`(배치 스킬 대상), 전방 투사체,
   밀쳐냄, 골 근접 경보가 들어 있다. 필터를 걸면 보너스 적이 광역기와 배치 스킬에서 사라진다.

7. `CollectMatchConfig` 에 등재(계약 2): `PutAsset("bonusWave", bonusWaveData)` ·
   **`PutAsset("bonusEnemy", enemyUnit)`**(리뷰 M1 — 참조는 이름까지만 접히므로 적 스탯은
   따로 담아야 한다. 이 적은 계약 4 때문에 `[enemies]` 섹션에도 없다) · 맵 섹션에 `bonusSpawn[i]`.

8. 브리지 SerializeField 로 `BonusWaveData` 를 받는다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] `BonusWaveScheduleTests` — 배분 `i % portalCount` · 시각 등차 · **2회 호출 동일**
- [x] 보너스 적 10기가 살아 있어도 `NoQueuedAttackersRemain()` 이 참이 될 수 있다
- [x] `_aliveAttackersQuery` 를 쓰는 11곳은 보너스 적을 **여전히 본다**
- [x] 재시작(`BeginPlacement`/`StartBattle`) 후 `_bonusPending` 이 비어 있다
- [x] `configHash` 가 `BonusWaveData` 변경에 반응한다
- [x] EditMode green

**확인 2026-08-24** — `BonusWaveScheduleTests`(6) 결정론 + PlayMode 순차 스폰·재시작 리셋.
`_aliveAttackersQuery` 11개 소비처 무필터를 리뷰가 전수 확인.
