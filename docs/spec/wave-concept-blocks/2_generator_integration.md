# 2. 생성기 통합 — 켜기 (한 커밋)

## 목적

unit 0 의 데이터와 unit 1 의 함수를 생성기에 연결해 컨셉이 실제 편성을 만들게 한다. **이 unit 을 쪼개지 않는다** — 데이터 배선 / 함수 호출 / 펼침 소비를 따로 커밋하면 «컨셉을 저작했는데 편성이 안 바뀌는» 중간 상태가 생기고, 그 상태에서 순수 함수 테스트는 전부 초록이다(`waypoint-flight-enemy` 계약 6, `traversal-layers` unit 5 실패 사례 승계).

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `Generate` 시그니처에 `laneCount` · 웨이브 루프를 블록 구조로 · `ExpandWave` 의 lane 결정 분기
- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs` — `WaveSpawnGroup` 에 `laneIndex`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryInitializeGeneratedWaves`·`BuildBriefingWavePlan` 이 `laneCount` 주입 (`BattleBridge.cs:474`·`1818`) · `PendingSpawnEntry` 가 그룹 `laneIndex` 존중
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `waveGeneratorVersion` 2 → 3

⚠ **`SpawnUnit` 의 스탯 대입부는 건드리지 않는다** (`BattleBridge.cs:8136` `speed = unitType.moveSpeed`). 계약 3.

## 구현

### laneCount 주입

`Generate(deck, seed, laneCount)`. 호출 순서상 이미 확정돼 있다 — `StartBattle` → `BeginPlacement`/`BuildMapForBattle` → `TryInitializeGeneratedWaves`. **브리핑도 같은 값을 넘겨야 한다**(계약 6): `BuildBriefingWavePlan` 이 다른 값을 쓰면 예고와 실스폰이 갈린다. 미확정(≤0)은 **2로 폴백**하고 경고한다.

기존 2-인자 오버로드는 프리뷰·테스트용으로 남기고 `laneCount = 2` 를 넘긴다.

### 웨이브 루프 → 블록 루프

```
for i in 0..waveCount-1:
    block = i / conceptHoldWaves
    if block != prevBlock:                          # 블록 경계에서만 뽑는다
        concept = PickConcept(pool, block*hold+1, laneCount, prevConcept, rng.NextInt())
        if concept == null: concept = FallbackSpread # 슬롯 2개 · laneGroup -1 · countMul 1
        AssignLanes(concept.slots.laneGroup, laneCount, rng.NextInt(), laneByGroup)
        prevBlock, prevConcept = block, concept
    # 블록 안에서 컨셉과 lane 배정은 재사용한다 (계약 1)

    total  = ExponentialWaveTotal(i, minUnits, maxUnits, growth, jitter, rng.NextFloat())
    counts = DistributeSlotCounts(total, concept.countMul, slotCount, maxUnits)
    각 슬롯: 후보 = pool 필터(classFilter × altitude), 앞 슬롯이 고른 유닛 제외
             후보 0 → 제외를 풀고, 그래도 0 이면 전체 pool + 경고 (계약 5 fail-open)
             ResolveWaveEligibleIndex 로 minWaveNumber 게이트 통과 → rng 로 선택
    ClampGroupCounts 를 N슬롯으로 일반화해 maxPerWave 적용
    조립: WaveSpawnGroup(unit, count, laneIndex)
```

**블록 경계에서만 컨셉·lane 을 뽑는 것**이 계약 1 이다. 블록 안에서 다시 뽑으면 3웨이브 유지가 깨진다.

**블록 0(웨이브 1~3)이 「평소」로 고정되는 것은 특수 분기가 아니다.** unit 4 의 저작에서 「평소」만 `minWaveNumber = 1` 이고 나머지는 4 이상이므로, 블록 0 의 후보가 「평소」 하나뿐이 되어 게이트가 자동으로 강제한다. **`i < 3` 같은 분기를 넣지 말 것** — 넣으면 `conceptHoldWaves` 를 바꿨을 때 두 곳이 갈린다.

**슬롯 간 유닛 중복 배제**는 기존 「한 웨이브 = 2종」 관례를 잇는다(`bIndex` 가 `aIndex` 를 피하던 자리). 필터가 좁아 후보가 1종뿐인 경우(예: `Tanker` class)는 슬롯이 하나여야 하고, 그렇지 않으면 제외를 풀어 같은 종이 두 슬롯에 들어간다 — 저작 실수의 결과가 «빈 슬롯»이 아니라 «중복»이어야 웨이브가 비지 않는다.

**`FallbackSpread` 는 저작 데이터가 아니라 구조적 폴백이다.** 슬롯 2개·`laneGroup -1`·`countMul` 1 로 **현행 동작의 모양을 재현하는 것**이므로 코드 상수로 두는 것이 제약 6(하드코딩된 수치 금지)에 걸리지 않는다 — 밸런스 값이 아니라 «컨셉이 없을 때의 모양»이다.

**`ClampGroupCounts` 일반화**: 현재 2슬롯 `ref` 시그니처를 배열 버전으로 확장한다. 잘린 몫을 여유 있는 슬롯으로 넘겨 총량을 보존하는 규칙은 그대로 유지하고, **rng 를 소비하지 않는다는 계약도 유지**한다.

### 펼침 — lane 결정 분기만 추가한다

`ExpandWave` 는 계속 `RoundRobin` 을 쓴다(계약 8). 슬롯이 라운드마다 1기씩 교차 emit 되고 시각은 `base + localIndex × spacing` 이므로 **마지막 스폰이 `(total−1) × spacing` 으로 현행과 동일**하다 — 스폰 창 불변식과 `WaveKillBudgetPinTests` 는 손대지 않는다.

바뀌는 것은 lane 결정 한 곳이다:

```
laneIndex ≥ 0 → 그 lane 을 그대로 쓴다
laneIndex < 0 → 기존 EffectiveSpawnIndex(authoredSpawnIndex, deckIndex, laneCount)
```

이 분기가 **반드시 필요하다**: `EffectiveSpawnIndex` 는 `laneCount ≥ 3` 에서 authored 값을 무시하고 `deckIndex % laneCount` 로 돌린다. 우회하지 않으면 컨셉이 지정한 lane 이 3레인+ 맵에서 조용히 지워진다.

`_spawnGuideForecast`(스폰 예고 트레일)는 같은 펼침 결과에서 만들어지므로 **자동으로 컨셉의 lane 을 따른다** — 예고 코드는 손대지 않는다.

### baseline 변경 (계약 7)

웨이브당 rng 소비가 늘어(컨셉 1 + lane 1 + 슬롯당 1) **6맵 전부의 편성이 바뀐다.** `waveSeed` 는 그대로이므로 «같은 맵 = 같은 웨이브»는 유지된다. `waveGeneratorVersion` 2 → 3 으로 표시한다.

## 완료 기준

- **EditMode**
  - 블록 유지 — `conceptHoldWaves=3` 일 때 웨이브 1·2·3 의 컨셉 id 와 슬롯 lane 이 동일하고, 웨이브 4 에서 바뀐다
  - 수량은 블록 안에서 오른다 — 웨이브 3 총량 > 웨이브 1 총량
  - lane 불변식 — 같은 laneGroup 슬롯이 같은 lane, 다른 laneGroup 이 다른 lane
  - 컨셉 풀이 비었을 때 폴백이 슬롯 2개·무지정이고 **웨이브 수·총량이 현행과 동일**
  - 마지막 스폰 시각이 `base + (total−1) × spacing` 이다 (계약 8 — RoundRobin 유지 확인)
  - 결정론 — 같은 `(deck, seed, laneCount)` 3회 생성 시 signature(유닛 id + count + lane) 완전 일치
  - ⚠ **`laneCount` 는 결정론 키의 일부다.** lane 요구량 게이트가 후보 집합을 바꾸므로 스폰 수가 다른 맵은 컨셉·유닛·수량까지 달라진다 — «laneCount 무관» 을 단언하지 말 것. 계약 6 이 필요한 이유가 이것이다
- **PlayMode** — 컨셉 풀을 주입한 덱으로 판을 돌려 ① 지정 lane 에서만 스폰되는지 ② 블록 경계에서 스폰 지점이 바뀌는지 좌표로 단언
- **`WaveKillBudgetPinTests` 7덱 초록** (식 변경 없이)
- **콘솔** — fail-open 경고가 의도한 경우에만 뜬다(정상 저작에서는 0건)
