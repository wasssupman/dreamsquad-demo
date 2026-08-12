# 1. 순수 함수 3개 — 수량 분배 · lane 배정 · 컨셉 룰렛

## 목적

컨셉 해석의 계산 부분을 **plain 값 in → plain 값 out** 순수 static 함수로 먼저 확정하고 EditMode 로 고정한다(제약 10). 세 함수 모두 (a) 비자명한 분기·다단계이고 (b) sim-critical(편성이 곧 난이도)이라 추출 기준을 만족한다.

소비자는 이 unit 에서 **0** 이다. unit 2 가 켠다.

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — static 함수 3개 추가 (기존 `ExponentialWaveTotal`·`ClampGroupCounts`·`ResolveWaveEligibleIndex` 와 같은 자리)
- **신규** `Assets/Tests/EditMode/WaveConceptMathTests.cs`

## 구현

### `DistributeSlotCounts(total, countMul, slotCount, maxUnits, outCounts)`

`total`(곡선값) → 슬롯별 개체 수. 계약 4 를 구현한다.

```
scaled = clamp(round(total × countMul), slotCount, maxUnits)
기본 몫  = scaled / slotCount   (정수 나눗셈)
잔여     = scaled % slotCount   → 앞 슬롯부터 순서대로 +1
```

**균등 분배다.** 슬롯 비중(`share`)을 두지 않았으므로(unit 0) 나눗셈 하나로 끝난다. 잔여를 «앞 슬롯부터»로 정한 것은 결정론을 위한 규칙이고, rng 를 받지 않는다 — 잔여 배분을 랜덤으로 하면 같은 시드에서 결과가 갈린다.

**하한이 `slotCount` 인 것이 중요하다.** `minUnitsPerWave`(5)를 하한으로 쓰면 「중장」(`countMul` 0.4)이 초반 웨이브에서 5기가 되어 배율이 무의미해진다. 상한은 `maxUnitsPerWave`(24)를 그대로 존중한다.

### `AssignLanes(laneGroups, laneCount, rollValue, outLaneIndex)`

`laneGroup` 값들 → 실제 lane 인덱스. 계약 2 를 구현한다.

```
distinct laneGroup (≥0) 수 = k
k > laneCount  → false 반환 (호출측이 이 컨셉을 후보에서 버린다)
k ≤ laneCount  → rollValue 로 시작 오프셋을 정하고 laneCount 를 순환하며 k 개를 뽑아
                 laneGroup 을 등장 순서대로 매핑
laneGroup == -1 → outLaneIndex = -1 (무지정 = 기존 EffectiveSpawnIndex 경로)
```

`rollValue` 는 호출측이 뽑아 넘기는 plain int 다 — rng 를 함수에 넣지 않아야 EditMode 로 결정론 검증이 된다(`ExponentialWaveTotal` 이 `jitter01` 을 받는 것과 같은 형태).

**같은 laneGroup 은 반드시 같은 lane, 다른 laneGroup 은 반드시 다른 lane.** 이 두 불변식이 「원거리」의 협공을 성립시킨다.

### `PickConcept(pool, blockFirstWaveNumber, laneCount, previousConcept, rollValue)`

가중치 룰렛 + 게이트 + 직전 배제.

```
후보 = pool 중  weight > 0
              AND minWaveNumber ≤ blockFirstWaveNumber
              AND slots 의 distinct laneGroup 수 ≤ laneCount
              AND 참조 ≠ previousConcept
후보 0 → null (호출측 폴백)
후보 1+ → weight 누적 룰렛에서 rollValue 로 선택
```

**lane 요구량 검사는 `slots` 에서 파생한다.** 별도 `minLaneCount` 필드를 두지 않는 이유가 이것이다 — 두면 저작값과 파생값이 갈릴 수 있고, 갈리면 «저작은 2를 요구하는데 슬롯은 3 lane 을 쓰는» 컨셉이 통과한다.

**직전 컨셉 배제가 리듬 규칙이다.** 같은 컨셉이 두 블록 연속이면 그것이 기본값이 되어 인상이 죽는다. 배제 때문에 후보가 0이 되면(풀에 컨셉이 1개뿐인 경우) 배제를 풀고 다시 고른다 — fail-open.

## 완료 기준

- **EditMode `WaveConceptMathTests`**:
  - `DistributeSlotCounts` — 합 = scaled · 각 슬롯 ≥ 1 · 상한 24 준수 · `countMul` 0.4 가 실제로 줄인다 · 잔여가 앞 슬롯부터 붙는다 · 같은 입력 3회 결과 동일
  - `AssignLanes` — 같은 laneGroup → 같은 lane · 다른 laneGroup → 다른 lane · `k > laneCount` 는 false · `-1` 은 `-1` 로 통과 · 같은 `rollValue` → 같은 배정
  - `PickConcept` — 게이트 3종(weight·minWaveNumber·lane 요구량)이 각각 후보를 걸러낸다 · 직전 배제가 작동한다 · 후보 1개일 때 배제 fail-open · 후보 0 이면 null · 가중치 0 은 뽑히지 않는다
- **기존 테스트 전부 초록** — 소비자 0 이므로 웨이브 생성 결과 무변경. `WaveKillBudgetPinTests` 그대로 통과.
- **compile** — 신규 테스트 파일이 EditMode asmdef 와 csproj 에 반영되었는지 확인.
