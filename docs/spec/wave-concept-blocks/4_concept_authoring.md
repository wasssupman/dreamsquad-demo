# 4. 컨셉 5종 저작 + Skimmer 라이브 편입

## 목적

컨셉을 콘텐츠로 만든다. 기계는 unit 2 에서 다 돌고 있으므로 이 unit 은 **에셋 저작과 덱 배선**이다. 그리고 랩 전용으로 묶여 있던 비행 적을 「공습」의 몸으로 라이브에 편입한다.

## 변경 대상

- **신규** `Assets/_Project/Data/WaveConcepts/Concept_{Spread,Swarm,Heavy,Ranged,Airstrike}.asset`
- `Assets/_Project/Scripts/Data/Decks/Deck_{Serpent,Coil,Twin,Spiral,Zig,Hook,Endless}.asset` — `waveConceptPool` 5종 배선 · `attackUnitPool` 에 `Enemy_Skimmer` 편입(11→12종) · `waveSeed` 갱신
- `Assets/_Project/Data/EnemyCatalog.asset` — Skimmer 등재 확인(이미 등재됨)

## 구현

### 컨셉 5종 — 성질과 위상이 결합된 완성품

컨셉을 «축의 조합»으로 두지 않는다. 성질 컨셉이 어차피 위상을 하나씩 갖고 태어나므로, 위치 전용 컨셉(순수 협공·일점)은 후속 후보로 둔다.

| 에셋 | displayName | 슬롯 (laneGroup · classFilter · altitude) | countMul | minWave | weight |
|---|---|---|---|---|---|
| `Concept_Spread` | 평소 | `-1`·None·Ground / `-1`·None·Ground | 1.0 | 1 | **낮음** |
| `Concept_Swarm` | 벌떼 | `0`·Runner·Ground / `0`·Runner·Ground | **1.3** | 4 | 보통 |
| `Concept_Heavy` | 중장 | `0`·**Tanker**·Ground (**슬롯 1개**) | **0.4** | 4 | 보통 |
| `Concept_Ranged` | 원거리 | `0`·Shooter·Ground / `1`·Shooter·Ground | 0.7 | **7** | 보통 |
| `Concept_Airstrike` | 공습 | `0`·None·**Air** (**슬롯 1개**) | **0.3** | 4 | 낮음 |

**「평소」의 가중치가 낮은 것이 의도다.** 블록이 3웨이브면 판당 컨셉 전환이 4번뿐이라 **바탕으로 깔 «쉬는 컨셉»이 필요하지 않다** — 전부 특별해도 피곤하지 않다. 그리고 블록 0 은 게이트가 이미 「평소」를 강제하므로(다른 컨셉이 `minWaveNumber` 4 이상) 온보딩은 가중치와 무관하게 보장된다. 이후 블록의 「평소」는 «간헐적 숨 고르기»이고, 그것이 잦으면 판이 지금과 같아진다.

**`countMul` 과 `weight` 값은 어림이다.** `countMul` 은 계약 4 의 예시(Runner 20hp × 19 = 380 vs Tanker 100hp × 19 = 1,900)에서 역산한 초기값이고, `weight` 는 «판당 4번의 전환에 컨셉이 골고루 나오는가»를 실측해 정한다. **조정 지점은 이 표 하나다.**

각 컨셉의 의도와 **속도 폭이 좁은 이유**(계약 3 — 뭉침을 저작으로 만든다):

- **평소** — 현행 재현. 블록 0(웨이브 1~3) 온보딩. 무필터라 속도가 1.5~5.6 으로 넓고 **의도적으로 안 뭉친다** — 다른 컨셉의 «덩어리»가 이 대비 위에서 읽힌다.
- **벌떼** — `Runner` class = Swift(30hp/**4.5**)·Runner(20hp/**5.6**). 속도 폭 1.1 이라 20셀에서 도착차 ~0.8초 — 사실상 한 덩어리다. 한 lane 집중이라 작은 것들이 한 곳으로 쏟아진다. DPS 가 아니라 **처리량** 문제.
- **중장** — `Tanker` class 는 Tanker **1종뿐**이라 속도가 1.5 단일이다. **슬롯을 하나로 두는 것이 이 컨셉의 핵심** — 2종을 만들려고 `Bruiser` 슬롯을 붙이면 2.0~2.5 가 섞여 벽이 흩어진다. 사용자가 말한 "탱커 웨이브"가 정확히 이 모양이고, 「한 웨이브 = 2종」 관례를 여기서 의도적으로 깬다.
- **원거리** — `Shooter` 5종(Rootcaster 1.8·Needler 2.8·Sniper 1.6·Debuffer 2.0·Kindler 2.0)에서 2종, **협공 위상**. 속도 폭이 최대 1.2 라 다소 흩어지지만 애초에 두 lane 으로 갈라지므로 «덩어리»가 컨셉의 본체가 아니다. 적이 다가오지 않고 사거리에서 멈춰 방어선을 깎는 것이 본체다 — 지금 이 게임에 없는 화면이다. 게이트가 웨이브 7 인 이유는 계약 9(압력 밀도 상승을 실측으로 판정).
- **공습** — Air 슬롯 하나. **구성은 pool 의 Air 로스터가 결정한다** — 편입 시점에 Air 가 Skimmer 하나면 속도 2.5 단일로 완전히 뭉치고, Air 적이 늘면 「공습」이 자동으로 그들을 받는다. 속도 폭이 벌어지면(계약 3) 슬롯을 성질로 더 좁히거나 컨셉을 쪼갠다. 소수(0.3)·저HP·좁은 속도 폭이 계약 10 의 «스킬 한 발 값» 안전장치다.

**「평소」의 `altitude` 가 `Ground` 인 것은 필수다.** `Any` 를 허용하면 블록 0(웨이브 1~3)에 비행이 뽑혀 **대공 없는 첫 3웨이브에서 막을 수 없는 적**이 나온다. `SlotAltitude` 를 2값으로 둔 이유가 이것이다(unit 0).

**결과적으로 비행 적은 「공습」에서만 등장한다.** 「중장」은 `Tanker`, 「벌떼」는 `Runner` 필터라 뽑을 수 없고, 「평소」와 「원거리」는 `Ground` 로 막혀 있다.

**`altitude = Ground` 가 「원거리」에서 하는 일이 성질 필터만으로는 안 되는 부분이다.** `Shooter` class 에 비행 적이 있으면(고도와 성질은 직교하므로 가능하다) 성질 필터만으로는 걸러지지 않고, 「원거리」 블록에 대공 없이 못 잡는 적이 섞여 컨셉이 «원거리»가 아니라 «운»이 된다. 고도를 두 값으로 명시하는 설계(unit 0)가 이 사고를 구조적으로 막는다 — **성질 컨셉은 전부 `Ground` 를 명시하고, 비행은 `Air` 컨셉이 전담한다.**

어느 컨셉도 `Bruiser` 를 쓰지 않는 것도 의도다 — Bruiser 는 속도 폭이 2.0~2.5 로 넓어 «성질로 좁혀 뭉친다»는 계약 3 을 만족하지 못한다.

### Skimmer 편입 — ⚠ 삽입 위치가 중요하다

파이프라인 맵 경고: **풀에 1종을 더하면 그 덱의 웨이브가 전부 재추첨된다.** `rng.NextInt(0, pool.Count)` 가 뽑으므로 `waveSeed` 가 고정이어도 웨이브 1부터 구성이 바뀐다. 그리고 **맨 뒤에 넣으면 `ResolveWaveEligibleIndex` 의 전방 순환이 초반 웨이브를 `pool[0]` 로 쏠리게 한다** — 삽입은 **풀 중간**에 한다(예: Vanguard 뒤).

unit 2 의 계약 7 로 이미 baseline 이 깨지므로 재추첨 자체는 새 비용이 아니다. 다만 **`waveSeed` 를 갱신해 새 baseline 을 diff 에 드러낸다**(파이프라인 맵 지침). 20260811~16 → 20260821~26, Endless 20260817 → 20260827.

`Enemy_Skimmer` 는 `enemyClass = Bruiser`·`traversalLayers = Air`·`flightLift 1.4`·`waypointPathIndex 0`·hp 80·speed 2.5 다. **`waypointPathIndex 0` 이라 경로 0 이 저작된 맵에서만 그 경로를 탄다** — 미저작 맵에서는 골 슬롯으로 폴백한다(`waypoint-flight-enemy` 계약 3). 즉 「공습」은 경로 저작 여부와 무관하게 성립하고, 저작된 맵에서 더 흥미로워진다.

Skimmer 자체의 `minWaveNumber` 는 1 로 유지한다 — 등장 시점 제어는 **컨셉 게이트**(`Concept_Airstrike.minWaveNumber = 4`)가 소유한다. 두 곳에 두면 갈린다.

## 완료 기준

- **EditMode**
  - 7덱 전부 `waveConceptPool` 이 5종을 참조하고 `conceptHoldWaves = 3`
  - 6맵 덱의 `attackUnitPool` 이 12종이고 Skimmer 가 **맨 뒤가 아니다**
  - 「공습」 블록의 전 유닛이 `EffectiveTraversalLayers` 에 Air 를 포함한다
  - 「평소」·「중장」·「벌떼」·「원거리」 블록에 Air 유닛이 **한 기도 없다** — 특히 웨이브 1~3
  - 「중장」 블록의 전 유닛이 Tanker 1종이다 (슬롯 1개 확인)
  - 「원거리」가 `laneCount = 1` 맵에서 후보에서 빠진다 (lane 요구량 2)
  - 웨이브 1~3 이 항상 「평소」다
- **결정론** — 각 덱 3회 생성 signature 일치
- **`WaveKillBudgetPinTests` 7덱 초록**
- **Play 육안 (사용자 확인)** — 5개 컨셉이 화면에서 서로 구분되는가. 특히 **「벌떼」와 「평소」가 다르게 보이는가**(구분되지 않으면 성질 컨셉 접근 자체를 재검토한다) · 「공습」이 무력감이 아니라 스킬 지불로 느껴지는가

---

확인: **2026-08-13 사용자 Play 확인 통과** (커밋 `79dacaa8` — units 1~4). EditMode 2307개 실패 0.
