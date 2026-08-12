# 5. 컨셉 5종 저작 + Skimmer 라이브 편입

## 목적

컨셉을 콘텐츠로 만든다. 기계는 unit 3 에서 다 돌고 있으므로 이 unit 은 **에셋 저작과 덱 배선**이다. 그리고 랩 전용으로 묶여 있던 비행 적을 「공습」의 몸으로 라이브에 편입한다.

## 변경 대상

- **신규** `Assets/_Project/Data/WaveConcepts/Concept_{Spread,Swarm,Heavy,Ranged,Airstrike}.asset`
- `Assets/_Project/Scripts/Data/Decks/Deck_{Serpent,Coil,Twin,Spiral,Zig,Hook,Endless}.asset` — `waveConceptPool` 5종 배선 · `attackUnitPool` 에 `Enemy_Skimmer` 편입(11→12종) · `waveSeed` 갱신
- `Assets/_Project/Data/EnemyCatalog.asset` — Skimmer 등재 확인(이미 등재됨)

## 구현

### 컨셉 5종 — 성질과 위상이 결합된 완성품

컨셉을 «축의 조합»으로 두지 않는다. 성질 컨셉이 어차피 위상을 하나씩 갖고 태어나므로, 위치 전용 컨셉(순수 협공·일점)은 후속 후보로 둔다.

| 에셋 | displayName | 슬롯 (laneGroup · classFilter · altitude · share · offset) | countMul | cohesion | minWave | minLane |
|---|---|---|---|---|---|---|
| `Concept_Spread` | 평소 | `-1`·None·Any·1·0 / `-1`·None·Any·1·0 | 1.0 | **off** | 1 | 1 |
| `Concept_Swarm` | 벌떼 | `0`·Runner·Ground·1·0 / `0`·Runner·Ground·1·0.5 | **1.3** | on | 4 | 1 |
| `Concept_Heavy` | 중장 | `0`·Tanker·Ground·1·0 / `0`·Bruiser·Ground·1·1.0 | **0.4** | on | 4 | 1 |
| `Concept_Ranged` | 원거리 | `0`·Shooter·Ground·1·0 / `1`·Shooter·Ground·1·0 | 0.7 | on | **7** | **2** |
| `Concept_Airstrike` | 공습 | `0`·None·**Air**·1·0 | **0.3** | on | 4 | 1 |

**`countMul` 값은 어림이다.** 계약 4 의 예시(Runner 20hp × 19 = 380 vs Tanker 100hp × 19 = 1,900)에서 역산한 초기값이며 **실측으로 조정될 값**이다. 조정 지점은 이 표 하나다.

각 컨셉의 의도:

- **평소** — 현행 재현. 블록 0(웨이브 1~3) 온보딩. 동행 off 라서 다른 컨셉의 «뭉침»이 대비로 읽힌다.
- **벌떼** — `Runner` class 는 Swift(30hp/4.5)·Runner(20hp/5.6). 한 lane 집중 + 동행이면 작은 것들이 한 곳으로 쏟아진다. DPS 가 아니라 **처리량** 문제.
- **중장** — `Tanker` class 는 Tanker 1종뿐이라 두 번째 슬롯을 `Bruiser`(Basic·Vanguard·Heartseeker·Skimmer)로 받아 2종을 만든다. 같은 lane 시차 1초로 «벽 뒤의 벽».
- **원거리** — `Shooter` 5종(Rootcaster·Needler·Sniper·Debuffer·Kindler)에서 2종, **협공 위상**. 적이 다가오지 않고 사거리에서 멈춰 방어선을 깎는다 — 지금 이 게임에 없는 화면이다. `minLaneCount = 2` 이고 게이트가 웨이브 7 인 이유는 계약 9(압력 밀도 상승을 실측으로 판정).
- **공습** — Air 슬롯 하나. 현재 Air 는 Skimmer 1종이라 단일 종이다. 소수(0.3)·저HP(80)·동행이 계약 10 의 «스킬 한 발 값» 안전장치다.

### Skimmer 편입 — ⚠ 삽입 위치가 중요하다

파이프라인 맵 경고: **풀에 1종을 더하면 그 덱의 웨이브가 전부 재추첨된다.** `rng.NextInt(0, pool.Count)` 가 뽑으므로 `waveSeed` 가 고정이어도 웨이브 1부터 구성이 바뀐다. 그리고 **맨 뒤에 넣으면 `ResolveWaveEligibleIndex` 의 전방 순환이 초반 웨이브를 `pool[0]` 로 쏠리게 한다** — 삽입은 **풀 중간**에 한다(Bruiser 이웃 자리, 예: Vanguard 뒤).

unit 3 의 계약 7 로 이미 baseline 이 깨지므로 재추첨 자체는 새 비용이 아니다. 다만 **`waveSeed` 를 갱신해 새 baseline 을 diff 에 드러낸다**(파이프라인 맵 지침). 20260811~16 → 20260821~26, Endless 20260817 → 20260827.

`Enemy_Skimmer` 는 `enemyClass = Bruiser`·`traversalLayers = Air`·`flightLift 1.4`·`waypointPathIndex 0` 이다. **`waypointPathIndex 0` 이라 경로 0 이 저작된 맵에서만 그 경로를 탄다** — 미저작 맵에서는 골 슬롯으로 폴백한다(`waypoint-flight-enemy` 계약 3). 즉 「공습」은 경로 저작 여부와 무관하게 성립하고, 저작된 맵에서 더 흥미로워진다.

`minWaveNumber` 는 SO 가 아니라 **컨셉 게이트**로 제어한다(Skimmer 자체는 1 유지). 「중장」의 Bruiser 슬롯이 Skimmer 를 뽑을 수 있으므로 `altitude = Ground` 로 막아둔 것을 확인한다 — 막지 않으면 「중장」에 비행이 섞여 컨셉이 흐려진다.

## 완료 기준

- **EditMode**
  - 7덱 전부 `waveConceptPool` 이 5종을 참조하고 `conceptHoldWaves = 3`
  - 6맵 덱의 `attackUnitPool` 이 12종이고 Skimmer 가 **맨 뒤가 아니다**
  - 「공습」 블록의 전 유닛이 `EffectiveTraversalLayers` 에 Air 를 포함한다
  - 「중장」·「벌떼」·「원거리」 블록에 Air 유닛이 **한 기도 없다**
  - 「원거리」가 `laneCount = 1` 맵에서 후보에서 빠진다(`minLaneCount = 2`)
  - 웨이브 1~3 이 항상 「평소」다
- **결정론** — 각 덱 3회 생성 signature 일치
- **`WaveKillBudgetPinTests` 7덱 초록**
- **Play 육안 (사용자 확인)** — 5개 컨셉이 화면에서 서로 구분되는가. 특히 「벌떼」와 「평소」가 다르게 보이는가(구분되지 않으면 성질 컨셉 접근 자체를 재검토한다) · 「공습」이 무력감이 아니라 스킬 지불로 느껴지는가
