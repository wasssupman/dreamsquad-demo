---
name: enemy-wave-integration
description: Use when adding a new enemy AttackUnitData, or when changing an existing enemy's minWaveNumber / maxPerWave / enemyClass / traversalLayers / splitUnit — any of these silently rewrites what every live wave contains. Covers pool insertion position, seed rebaselining, concept assignment, the tutorial roster contract, and the traps that produce collapsed or biased waves.
---

# Enemy → Wave Integration

## Overview

적 SO 를 하나 만드는 것은 **적 하나를 만드는 일이 아니다.** 라이브 덱 풀에 넣는 순간 그 덱의 웨이브 편성이 **전부 재추첨**되고, 그 적의 `enemyClass` × 통행층이 어느 웨이브 컨셉에 걸릴지가 자동으로 정해진다. 저작자가 그것을 모르면 「적만 예쁘게 만들고 판은 조용히 망가진」 상태가 된다.

**핵심 원칙: 적을 만든 세션이 웨이브까지 책임진다.** 다른 세션이 나중에 발견해 고치는 구조면 그 사이의 모든 플레이가 잘못된 판이다.

## The Iron Law

```
적 에셋을 만드는 것과 라이브 풀에 넣는 것은 다른 작업이다.
넣었으면 웨이브 baseline 을 다시 세우고 검증까지가 한 커밋이다.
```

풀에 넣지 않기로 **결정**하는 것도 유효하다(랩 전용·미완성). 다만 그 결정을 spec 에 적어야 하고, 나중에 넣는 커밋이 이 스킬을 다시 태워야 한다.

## When to Use

다음 중 하나라도 하면 즉시:

- 신규 `AttackUnitData` 에셋 생성
- 기존 적의 `minWaveNumber` · `maxPerWave` 변경 (등장 시점·상한이 곧 편성이다)
- 기존 적의 `enemyClass` · `traversalLayers` 변경 (**어느 컨셉에 걸리는지가 바뀐다**)
- `splitUnit` 추가 (파생 유닛이 생긴다)
- `waveConceptPool` · `Concept_*.asset` 의 슬롯·필터 편집

## 정거장 체크표

새 적 하나가 지나야 하는 자리. **해당 없으면 빈 칸이 아니라 `N/A + 이유`로 적는다.**

| # | 정거장 | 확인 |
|---|---|---|
| 1 | `Assets/_Project/Data/EnemyCatalog.asset` | 등재 |
| 2 | 라이브 덱 `attackUnitPool` | Serpent·Coil·Twin·Spiral·Zig·Hook(맵 6종) + Endless + 공성 3종(Duel·Ford·Isle) |
| 3 | **삽입 위치** | 맨 뒤 금지 — 아래 «전방 순환» 참조 |
| 4 | `waveSeed` 갱신 + `waveGeneratorVersion` bump | 풀이 바뀌면 편성 전체가 재추첨된다. 새 baseline 을 diff 에 드러내라 |
| 5 | 컨셉 배정 | `enemyClass` × 통행층이 **자동**으로 정한다. 신규 필터 축을 만들지 마라 |
| 6 | 튜토리얼 플랜 | `WavePlan_Tutorial` 에 그 적을 가르치는 웨이브. EditMode 가 강제한다 |
| 7 | dev 덱 | `WaypointLab`·`SiegeTest`·`WaveA/B` 는 판단. 넣지 않았으면 이유를 적어라 |

## 규칙과 함정

### 전방 순환 — 풀 맨 뒤에 넣지 마라

`WavePatternGenerator.ResolveWaveEligibleIndex` 는 뽑힌 인덱스에서 **앞으로 순환**하며 `minWaveNumber <= waveNumber` 인 첫 유닛을 고른다:

```csharp
int index = (start + step) % count;   // 전방 순환
if (unit.minWaveNumber <= waveNumber) return index;
```

게이트가 걸린 적을 **맨 뒤**에 넣으면, 초반 웨이브에서 그 인덱스가 뽑힐 때마다 순환이 배열 끝을 넘어 **`pool[0]` 으로 쏠린다.** 풀 중간에 넣어라.

### `maxPerWave: 1` + 단일 슬롯 컨셉 = 웨이브 붕괴

`ClampGroupCounts` 는 상한에 잘린 몫을 **다른 슬롯으로 넘긴다.** 슬롯이 하나뿐인 컨셉이 `maxPerWave: 1` 인 적을 뽑으면 넘길 곳이 없어 **웨이브 전체가 1기로 붕괴한다.**

→ 엘리트(보통 `maxPerWave: 1`)를 넣기 전에 그 적이 걸릴 컨셉의 슬롯 수를 확인하라. 슬롯 1개면 컨셉을 2슬롯으로 넓히거나 그 컨셉에 안 걸리게 필터를 조정한다.

### 속도 폭 — 컨셉의 「뭉침」 계약

성질 컨셉(벌떼·중장·공습)은 **속도 폭이 좁아 한 덩어리로 온다**는 전제 위에 있다. 같은 필터에 걸리는 적을 추가하면 그 로스터의 속도 폭이 벌어져 덩어리가 흩어진다.

→ 추가 후 그 컨셉 로스터의 `moveSpeed` 최대−최소를 재라. 기존 pin(예: Air 로스터 폭 ≤ 1.5)이 그 계약을 지킨다.

### 저작 플랜은 게이트를 받지 않는다

`WavePlanAsset`(튜토리얼·테스트 모드)은 `minWaveNumber` 를 무시한다 — 적용 범위가 seed 생성 경로뿐이다. 그래서 게이트 8 인 적도 **튜토리얼 웨이브 3 에 놓을 수 있다.** 교습 순서는 게이트가 아니라 저작이 정한다.

### 분열체·파생 유닛은 풀에 넣지 않는다

`splitUnit` 으로만 생성되는 파생(예: `Slime_Mid`·`Slime_Small`)은 보통 `killScore 0`·`awakeningReward 0` 이다. 풀에 넣으면 **점수 없는 적이 정규 편성에 섞인다.**

## 워크플로

1. **적 SO 저작** — 스탯·메커닉·비주얼
2. **컨셉 귀속 판정** — `enemyClass`(None/Tanker/Runner/Bruiser/Shooter) × 통행층(Path/Air)이 어느 `Concept_*` 에 걸리는지 표로 적는다. 걸리는 컨셉의 **슬롯 수**를 함께 확인(붕괴 함정)
3. **풀 삽입** — 위 덱들에 **중간 위치**로. `.meta` 동반 확인
4. **baseline 재설정** — `waveSeed` 갱신 + `waveGeneratorVersion` bump. 전 덱 동일하게
5. **튜토리얼 갱신** — `WavePlan_Tutorial` 의 적절한 웨이브에 추가. 엘리트는 후반, 신규 축(비행 등)은 그 축을 가르치는 웨이브에
6. **검증** — 아래
7. **커밋** — 적 에셋 + 덱 + 튜토리얼 + 테스트를 **한 커밋**으로. 「적만 만들고 편입은 나중에」로 나눌 거면 그 이유를 spec 에 적는다

## 검증 (건너뛰지 않는다)

- **EditMode 전량.** 특히:
  - `WaveConceptAuthoringTests` — 컨셉별 로스터 계약(속도 폭·필터·슬롯)
  - `WaveKillBudgetPinTests` — 덱별 킬 예산
  - `WaveEligibilityGateTests` — 게이트 동작
  - `MapDocumentPoolDevEntriesTests.TutorialEntry_TeachesEveryLiveEnemyTypeInTenWaves` — **로스터 전종 교습**. 빠진 이름을 메시지에 찍어준다
- **결정론** — 같은 덱 3회 생성 signature 일치
- **엘리트를 넣었다면** — 그 적이 뽑힌 웨이브의 총 수량이 1보다 큰지(붕괴 가드)

## Red Flags — 멈추고 이 스킬로 돌아와라

| 생각 | 실제 |
|---|---|
| "적만 만들고 편입은 다음에" | 유효한 선택이지만 **결정을 spec 에 적어야** 한다. 조용히 미루면 다음 사람이 못 찾는다 |
| "풀 맨 뒤에 붙이면 diff 가 깔끔" | 전방 순환이 초반 웨이브를 `pool[0]` 로 쏠리게 한다 |
| "시드는 안 건드려도 되겠지" | 풀이 바뀌면 편성이 이미 바뀌었다. 시드를 갱신해 **그 사실을 diff 에 드러내라** |
| "컨셉은 나중에 저작하면 됨" | 컨셉 귀속은 저작이 아니라 **`enemyClass` × 통행층에서 자동 파생**된다. 이미 정해져 있다 |
| "튜토리얼은 별개 콘텐츠" | EditMode 가 로스터 전종 교습을 요구한다. 빨간불로 돌아온다 |
| "테스트 초록이니 됐다" | 초록이 **다른 세션이 대신 고쳐서**일 수 있다. 실제로 그런 적이 있다 — 값을 직접 찍어 확인하라 |
