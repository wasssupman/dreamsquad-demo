# 7. 엘리트 2종 라이브 풀 편입 (드래곤 · 슬라임)

## 목적

`elite-enemy-tier` 가 만들어놓고 **의도적으로 미룬** 라이브 덱 등록을 끝낸다. 그쪽 spec 이 "라이브 덱 풀 등록은 웨이브 baseline 을 바꾸므로 별도 커밋" 으로 남겼고, 그 baseline 을 소유한 것이 이 spec 이다.

**이 unit 을 여기서 하는 이유**: 두 엘리트가 어느 컨셉에 들어가는지는 `classFilter × altitude` 가 결정한다. 풀에 넣는 순간 컨셉 편성이 바뀌므로 등록과 컨셉 조정은 같은 커밋이어야 한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` (7개) — `attackUnitPool` 12→14(Endless 11→13) · `waveSeed` 갱신 · `waveGeneratorVersion` 3→4
- `Assets/_Project/Data/WaveConcepts/Concept_Airstrike.asset` — 슬롯 1→2
- `Assets/_Project/Tests/EditMode/WaveConceptAuthoringTests.cs` — Air 로스터 단언 교체
- `Assets/_Project/Tests/EditMode/DragonBreathAuthoringTests.cs` · `SlimeSplitAuthoringTests.cs` — 「아직 풀에 없다」 단언 해소

## 구현

### 두 엘리트가 어느 컨셉에 들어가는가 (필터가 이미 결정한다)

| 유닛 | class | 고도 | 걸리는 컨셉 | 안 걸리는 이유 |
|---|---|---|---|---|
| **Dragon** | Shooter | **Air** | **「공습」**(None×Air) | 「원거리」는 Shooter×**Ground** 라 배제 — 고도 축이 여기서 값을 한다 |
| **Slime** | Bruiser | Ground | **「평소」**(None×Ground) | 「중장」=Tanker · 「벌떼」=Runner · 「원거리」=Shooter 어디에도 안 걸림 |

신규 필터 축(tier 등)을 만들지 않는다. 기존 2축이 이미 의도한 자리로 보낸다.

### 「공습」을 슬롯 2개로 — `maxPerWave: 1` 이 만드는 함정

두 엘리트 모두 `maxPerWave = 1` 이다. **단일 슬롯 컨셉이 엘리트를 뽑으면 웨이브가 1기로 붕괴한다** — `ClampGroupCounts` 는 잘린 몫을 다른 슬롯으로 넘기는데 슬롯이 하나면 넘길 곳이 없다.

- 「공습」은 지금 **슬롯 1개** → Dragon 을 뽑으면 `countMul 0.3` 로 계산된 6기가 **1기**가 된다. → **슬롯 2개(둘 다 None×Air)로 늘린다.** 슬롯 간 유닛 중복 배제가 있으므로 Dragon + Skimmer 가 되고, 잘린 몫이 Skimmer 슬롯으로 흘러 «드래곤 1기 + 스키머 호위» 가 된다.
  - ⚠ **정정 (unit 8, 2026-08-15)**: 이 전제는 **w4~7 창에서 거짓**이었다 — `Skimmer.minWaveNumber 8` 이 컨셉 게이트 4보다 늦어 그 창의 Air 후보가 Dragon 단독이고, 중복 배제가 fail-open 으로 풀려 **Dragon×1+Dragon×1 = 2기**가 됐다. 슬롯을 늘리는 조치는 «그 컨셉의 게이트 웨이브에 서로 다른 후보가 슬롯 수만큼 있을 때만» 유효하다. [8](8_airstrike_gate.md) 이 Skimmer 게이트를 4로 내려 전제를 참으로 만들고 저작 술어 가드를 세웠다.
- 「평소」는 **이미 슬롯 2개**라 Slime 을 뽑아도 «슬라임 1기 + 잡몹 다수» 가 자동으로 나온다. **손대지 않는다.**

### Air 로스터가 2종이 된다 (계약 3 재확인)

Skimmer 2.5 · Dragon 2.0 → 속도 폭 **0.5**. 「벌떼」(Runner 4.5·5.6, 폭 1.1)보다 좁으므로 «성질로 좁혀 뭉친다»(계약 3)를 만족한다. 20셀 기준 도착차 2초.

`EveryLiveDeck_HasExactlyOneAirUnit_ForNow` 는 **바로 이 순간을 위해 걸어둔 알람**이다. 단언을 «Air 1종» 에서 **«Air 로스터의 속도 폭이 1.5 이하»** 로 교체한다 — 개수가 아니라 계약이 지키려던 것을 직접 잰다.

### 풀 삽입과 baseline

- 삽입은 **풀 중간**(파이프라인 맵 경고 — 맨 뒤면 `ResolveWaveEligibleIndex` 전방 순환이 초반 웨이브를 `pool[0]` 로 쏠리게 한다).
- `waveSeed` 20260821~27 → **20260831~37**, `waveGeneratorVersion` 3 → **4**. 풀이 12→14 가 되면 6맵 편성이 전부 재추첨되므로 새 baseline 을 diff 에 드러낸다.
- **슬라임 분열체(`Slime_Mid`·`Slime_Small`)는 풀에 넣지 않는다.** `splitUnit` 으로만 생성되고 `killScore 0`·`awakeningReward 0` 인 파생물이다 — 풀에 넣으면 점수 없는 적이 정규 편성에 섞인다.

## 완료 기준

- **EditMode**
  - 7덱 `attackUnitPool` 에 Dragon·Slime 이 있고 **맨 뒤가 아니다**. `Slime_Mid`·`Slime_Small` 은 **없다**
  - 「공습」 슬롯이 2개이고, 「공습」 블록에 나온 전 유닛이 Air 다
  - 「원거리」·「중장」·「벌떼」·「평소」 블록에 **Dragon 이 한 기도 없다**(고도 게이트)
  - 엘리트가 뽑힌 그룹의 `count` 가 **1을 넘지 않는다**(`maxPerWave`)
  - 엘리트가 뽑힌 웨이브의 **총 수량이 1보다 크다** — 슬롯 붕괴 회귀 가드
  - Air 로스터 속도 폭 ≤ 1.5
  - 웨이브 1~3 은 여전히 「평소」이고 Air 가 없다
  - 결정론 3회 일치 · `WaveKillBudgetPinTests` 7덱 초록
- **Play (사용자 확인)** — 드래곤이 「공습」에서, 슬라임이 「평소」에서 실제로 등장하는가. 슬라임 분열이 라이브 편성에서 감당 가능한가.

## 미결 — 저작 소유권 밖

**`Enemy_Slime.minWaveNumber = 3`** 이라 **블록 0(웨이브 1~3)의 마지막 칸에 엘리트가 들어올 수 있다.** 240HP 가 2×120 → 4×60 으로 갈라지는 적이 온보딩 블록에 나오는 셈이다. Dragon 은 4라 블록 0 밖이다.

이 값은 `elite-enemy-tier` 의 저작이라 **이 unit 에서 바꾸지 않는다.** 온보딩을 깨끗이 두려면 4 이상으로 올려야 하며, 판단은 그쪽 소유자에게 남긴다.
