# 1. 컨셉 데이터 모델

## 목적

컨셉을 저작 가능한 데이터로 만든다. 읽는 코드는 이 unit 에서 **0** 이다 — SO 타입과 덱 필드만 생기고 생성기는 아직 모른다. 소비자 0 이라 안전하게 먼저 들어간다.

수치를 코드에 두지 않는 것이 제약 6 이다. 컨셉의 성질·비중·배율·게이트는 전부 SO 에서 나온다.

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
| `minLaneCount` | int, Min(1) | 요구 스폰 수. 맵이 못 채우면 후보에서 빠진다 |
| `countMul` | float, Min(0.05) | 수량 배율 (계약 4) |
| `cohesion` | bool | 동행 여부. 적용 단위는 `laneGroup`(계약 3) |
| `slots` | `WaveConceptSlot[]` | 스웜 슬롯. 1개 이상 |

**`WaveConceptSlot`**:

| 필드 | 타입 | 의미 |
|---|---|---|
| `laneGroup` | int | **같은 값 = 같은 lane**. `-1` = 무지정(전 lane 분산) |
| `share` | float, Min(0) | 총량 중 비중. 슬롯 전체 합으로 정규화 |
| `classFilter` | `EnemyClass` | `None` = 무필터 |
| `altitude` | `SlotAltitude` | `Any` / `Ground` / `Air` |
| `triggerOffsetSec` | float, Min(0) | 웨이브 시작 기준 시차 |

**`laneGroup` 이 이 모델의 핵심**이다. lane 을 인덱스로 말하지 않고 위상으로만 말한다(계약 2):

```
협공        슬롯 2개 · laneGroup 0 / 1    → 서로 다른 두 lane
전위·후위   슬롯 2개 · laneGroup 0 / 0    → 같은 lane, 시차만 다름
일점 집중   슬롯 1개 · laneGroup 0
평소(현행)  슬롯 2개 · laneGroup -1 / -1  → 무지정
```

**`SlotAltitude` 를 `PlacementLayer` 로 직접 쓰지 않는 이유**: 슬롯이 표현하는 것은 «이 유닛의 통행층이 Air 를 포함하나»라는 **술어**이고, `PlacementLayer` 는 비트마스크다. 마스크를 그대로 두면 저작자가 `Ground|Air` 같은 조합을 넣을 수 있는데 그 의미(둘 다 허용? 둘 다 필요?)가 정의되지 않는다. 3값 enum 이 의도를 못 박는다. 판정은 `AttackUnitData.EffectiveTraversalLayers` 를 읽는다.

**`AttackDeck` 신규 필드** (직렬화 back-compat 을 위해 **맨 뒤에 추가**):

- `waveConceptPool` (`WaveConceptData[]`) — 비우면 unit 3 의 폴백(=「평소」 상당)으로 떨어져 무회귀 경로가 데이터로 확보된다(계약 5 의 침묵 금지와 별개로, 풀 부재는 정상 상태다).
- `conceptHoldWaves` (int, Min(1), 기본 **3**) — 블록 길이. 컨셉별 오버라이드는 후속 후보.

## 완료 기준

- **compile** 통과. `dotnet build` 로 확인 가능하나 신규 파일은 csproj 에 나열되어야 한다(메모리: csproj 는 파일을 명시 나열).
- **EditMode** — 신규 SO 를 코드로 생성해 기본값이 문서와 일치하는지 단언(`conceptHoldWaves = 3`, `countMul` 기본 1, `laneGroup` 기본 -1).
- **기존 테스트 전부 초록** — 읽는 코드가 0 이므로 어떤 웨이브 생성 결과도 바뀌지 않아야 한다. `WaveKillBudgetPinTests` 가 7개 덱을 그대로 통과한다.
- **인스펙터 확인** — `Wassup/WaveConcept` 로 에셋이 생성되고 슬롯 배열이 편집 가능하다.
