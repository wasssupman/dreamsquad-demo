# 0. 컨셉 데이터 모델

## 목적

컨셉을 저작 가능한 데이터로 만든다. 읽는 코드는 이 unit 에서 **0** 이다 — SO 타입과 덱 필드만 생기고 생성기는 아직 모른다. 소비자 0 이라 안전하게 먼저 들어간다.

수치를 코드에 두지 않는 것이 제약 6 이다. 컨셉의 성질·배율·게이트는 전부 SO 에서 나온다.

## 변경 대상

- **신규** `Assets/_Project/Scripts/Data/WaveConceptData.cs` — `WaveConceptData`(SO) + `WaveConceptSlot`(Serializable) + `SlotAltitude`(enum)
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `waveConceptPool` · `conceptHoldWaves`

## 구현

**`WaveConceptData`** (`CreateAssetMenu` = `Wassup/WaveConcept`):

| 필드 | 타입 | 의미 |
|---|---|---|
| `id` | string | 로그·테스트 식별자 |
| `displayName` | string | 브리핑/도크 라벨 (「벌떼」) |
| `weight` | float | 룰렛 가중치. 0 이하면 후보에서 제외 |
| `minWaveNumber` | int, Min(1) | 등장 게이트. **블록의 첫 웨이브 번호** 기준으로 판정 |
| `countMul` | float, Min(0.05) | 수량 배율 (계약 4) |
| `slots` | `WaveConceptSlot[]` | 스웜 슬롯. 1개 이상 |

**`WaveConceptSlot`** — 필드 3개다:

| 필드 | 타입 | 의미 |
|---|---|---|
| `laneGroup` | int (기본 `-1`) | **같은 값 = 같은 lane**. `-1` = 무지정(전 lane 분산) |
| `classFilter` | `EnemyClass` | `None` = 무필터 |
| `altitude` | `SlotAltitude` (기본 `Ground`) | `Ground` / `Air` |

**`laneGroup` 이 이 모델의 핵심**이다. lane 을 인덱스로 말하지 않고 위상으로만 말한다(계약 2):

```
협공        슬롯 2개 · laneGroup 0 / 1    → 서로 다른 두 lane
일점 집중   슬롯 1개 · laneGroup 0
평소(현행)  슬롯 2개 · laneGroup -1 / -1  → 무지정
```

**`SlotAltitude` 는 2값이고 기본이 `Ground` 다.** `Any` 를 두지 않는 이유: 저작 5종 중 어디에도 «지상과 공중을 섞은 슬롯»이 없고, `Any` 를 기본값으로 두면 **「평소」가 비행을 뽑아 웨이브 1~3 에 막을 수 없는 적이 나온다**(대공사수는 스쿼드 보유가 보장되지 않는다). 기본을 `Ground` 로 못 박으면 저작자가 명시적으로 `Air` 를 골라야만 비행이 등장한다.

`PlacementLayer` 비트마스크를 그대로 쓰지 않는 이유도 같다 — 마스크는 `Ground|Air` 같은 조합을 허용하는데 그 의미(둘 다 허용? 둘 다 필요?)가 정의되지 않는다. 2값 enum 이 의도를 못 박는다. 판정은 `AttackUnitData.EffectiveTraversalLayers` 를 읽는다.

**의도적으로 두지 않는 필드 4개** — 필요해지면 그때 연다(제약 8):

| 안 두는 것 | 이유 |
|---|---|
| `share`(슬롯 비중) | 저작 5종 전부 균등 또는 단일 슬롯. 균등 분배 + 잔여로 충분 |
| `triggerOffsetSec`(시차) | 뭉침을 저작으로 만들므로(계약 3) 시차는 속도차에 씻겨 내려가 의미가 없다 |
| `minLaneCount` | `slots` 의 distinct `laneGroup` 수로 완전히 파생된다 |
| `cohesion`(동행) | 런타임 속도 정렬은 기각(README 후속 후보) |

**`AttackDeck` 신규 필드** (직렬화 back-compat 을 위해 **맨 뒤에 추가**):

- `waveConceptPool` (`WaveConceptData[]`) — 비우면 unit 2 의 구조적 폴백으로 떨어져 무회귀 경로가 데이터로 확보된다.
- `conceptHoldWaves` (int, Min(1), 기본 **3**) — 블록 길이. 컨셉별 오버라이드는 후속 후보.

## 완료 기준

- **compile** 통과. 신규 파일은 csproj 에 명시 나열되어야 한다(메모리: csproj 는 파일을 명시 나열 — 빠지면 `dotnet build` 가 조용히 통과한다).
- **EditMode** — 신규 SO 를 코드로 생성해 기본값이 문서와 일치하는지 단언: `conceptHoldWaves = 3` · `countMul = 1` · `laneGroup = -1` · **`altitude = Ground`**.
- **기존 테스트 전부 초록** — 읽는 코드가 0 이므로 어떤 웨이브 생성 결과도 바뀌지 않아야 한다. `WaveKillBudgetPinTests` 가 7개 덱을 그대로 통과한다.
- **인스펙터 확인** — `Wassup/WaveConcept` 로 에셋이 생성되고 슬롯 배열이 편집 가능하다.
