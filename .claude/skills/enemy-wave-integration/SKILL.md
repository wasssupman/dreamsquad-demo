---
name: enemy-wave-integration
description: Use when (a) adding a new enemy AttackUnitData or changing an existing one's minWaveNumber / maxPerWave / enemyClass / traversalLayers / splitUnit, or (b) editing wave generation itself — WavePatternGenerator, AttackDeck fields, WaveConceptData / Concept_* assets, WavePlanAsset. Case (a) silently rewrites what every live wave contains; case (b) invalidates the rules written here, so this skill must be updated in the same commit. Covers pool insertion position, seed rebaselining, concept assignment, the tutorial roster contract, the traps that produce collapsed or biased waves, and how to re-derive every volatile number instead of trusting a frozen one.
---

# Enemy → Wave Integration

## Overview

적 SO 를 하나 만드는 것은 **적 하나를 만드는 일이 아니다.** 라이브 덱 풀에 넣는 순간 그 덱의 웨이브 편성이 **전부 재추첨**되고, 그 적의 `enemyClass` × 통행층이 어느 웨이브 컨셉에 걸릴지가 자동으로 정해진다. 저작자가 그것을 모르면 「적만 예쁘게 만들고 판은 조용히 망가진」 상태가 된다.

**핵심 원칙: 적을 만든 세션이 웨이브까지 책임진다.** 다른 세션이 나중에 발견해 고치는 구조면 그 사이의 모든 플레이가 잘못된 판이다.

## The Iron Law

```
적 에셋을 만드는 것과 라이브 풀에 넣는 것은 다른 작업이다.
넣었으면 웨이브 baseline 을 다시 세우고 검증까지가 한 커밋이다.

그리고 — 웨이브 생성 로직을 바꿨으면 이 스킬을 같은 커밋에서 갱신한다.
```

풀에 넣지 않기로 **결정**하는 것도 유효하다(랩 전용·미완성). 다만 그 결정을 spec 에 적어야 하고, 나중에 넣는 커밋이 이 스킬을 다시 태워야 한다.

## ⚠ 이 스킬은 밸런스 작업으로 자주 낡는다

웨이브 생성은 밸런스에 맞춰 계속 바뀐다. **여기 적힌 규칙은 특정 코드에 매여 있고, 그 코드가 바뀌면 규칙이 거짓이 된다.** 그래서 두 가지를 지킨다:

1. **고정 수치를 믿지 마라.** 덱 개수·컨셉 개수·로스터 크기 같은 값은 **아래 「값 재도출」로 그 자리에서 뽑는다.** 이 문서에 숫자를 다시 박지 마라.
2. **아래 표의 파일을 건드렸으면 이 스킬을 같은 커밋에서 고친다.**

### 갱신 트리거 — 이 파일이 바뀌면 이 주장이 죽는다

| 바뀐 것 | 죽는 주장 | 확인할 절 |
|---|---|---|
| `WavePatternGenerator.ResolveWaveEligibleIndex` | 「풀 맨 뒤 금지」의 근거(전방 순환) | 전방 순환 |
| `WavePatternGenerator.ClampGroupCounts` | 「단일 슬롯 + `maxPerWave 1` = 붕괴」 | 웨이브 붕괴 |
| `WavePatternGenerator.PickConcept` · `AssignLanes` | 컨셉 후보 게이트(레인 수·`minWaveNumber`) | 컨셉 배정 |
| `AttackDeck` 필드 추가/삭제 | 정거장 체크표의 「덱에서 손볼 것」 | 정거장 체크표 |
| `WaveConceptData` · `Concept_*.asset` 슬롯·필터 | 「컨셉 귀속은 자동 파생」·속도 폭 계약 | 컨셉 배정 / 속도 폭 |
| `WavePlanAsset` · `FromPlanAsset` | 「저작 플랜은 게이트를 안 받는다」 | 저작 플랜 |
| `AttackUnitData` 의 등장 관련 필드 | When to Use 의 트리거 목록 | frontmatter + When to Use |
| 라이브 덱·맵 풀 구성 변경 | 정거장 2·7 의 덱 목록 | 값 재도출로 대체됨 |

**갱신은 「문장을 고친다」가 아니라 「코드를 다시 읽고 주장을 재확인한다」다.** 근거를 못 찾으면 그 주장을 지워라 — 틀린 규칙이 없는 규칙보다 나쁘다.

### 값 재도출 (숫자를 외우지 말 것)

```bash
# 라이브 덱 목록과 각 풀 크기
for f in Assets/_Project/Scripts/Data/Decks/Deck_*.asset; do
  echo "$(basename "$f" .asset): $(sed -n '/attackUnitPool:/,/minWaveCount/p' "$f" | grep -c guid)종"
done

# 어느 덱이 맵 풀에 배선돼 있나 (본편 entries / dev 슬롯)
grep -A2 -E "^  (entries|devEntries):" Assets/_Project/Data/Maps/MapDocumentPool.asset

# 컨셉과 그 슬롯 필터
for f in Assets/_Project/Data/WaveConcepts/Concept_*.asset; do
  echo "== $(basename "$f" .asset)"; grep -E "displayName|laneGroup|classFilter|altitude|countMul|minWaveNumber" "$f"
done

# 특정 적이 어느 덱에 들어 있나
grep -l "<enemy-guid>" Assets/_Project/Scripts/Data/Decks/*.asset
```

## When to Use

**(a) 적을 만들거나 등장 조건을 바꿀 때** — 편성이 조용히 바뀐다:

- 신규 `AttackUnitData` 에셋 생성
- 기존 적의 `minWaveNumber` · `maxPerWave` 변경 (등장 시점·상한이 곧 편성이다)
- 기존 적의 `enemyClass` · `traversalLayers` 변경 (**어느 컨셉에 걸리는지가 바뀐다**)
- `splitUnit` 추가 (파생 유닛이 생긴다)

**(b) 웨이브 생성 로직 자체를 밸런스로 손볼 때** — 이 스킬의 규칙이 죽는다:

- `WavePatternGenerator` 의 순수 함수 (`ResolveWaveEligibleIndex`·`ClampGroupCounts`·`PickConcept`·`AssignLanes`·`ExponentialWaveTotal` 등)
- `AttackDeck` 필드 추가/삭제/의미 변경
- `WaveConceptData` 또는 `Concept_*.asset` 의 슬롯·필터·가중치
- `WavePlanAsset` / `FromPlanAsset` 의 변환 규약

(b) 는 코드를 고치고 **끝내지 말고** 위 「갱신 트리거」 표를 따라 이 문서를 같은 커밋에서 재확인한다.

## 정거장 체크표

새 적 하나가 지나야 하는 자리. **해당 없으면 빈 칸이 아니라 `N/A + 이유`로 적는다.**

| # | 정거장 | 확인 |
|---|---|---|
| 1 | `Assets/_Project/Data/EnemyCatalog.asset` | 등재 |
| 2 | 라이브 덱 `attackUnitPool` | **목록은 「값 재도출」로 뽑아라.** 맵 풀에 배선된 덱 전부 + Endless. 한 덱만 빠지면 그 맵에서만 안 나온다 |
| 3 | **삽입 위치** | 맨 뒤 금지 — 아래 «전방 순환» 참조 |
| 4 | `waveSeed` 갱신 + `waveGeneratorVersion` bump | 풀이 바뀌면 편성 전체가 재추첨된다. 새 baseline 을 diff 에 드러내라 |
| 5 | 컨셉 배정 | `enemyClass` × 통행층이 **자동**으로 정한다. 신규 필터 축을 만들지 마라 |
| 6 | 튜토리얼 플랜 | `WavePlan_Tutorial` 에 그 적을 가르치는 웨이브. EditMode 가 강제한다 |
| 7 | dev 전용 덱 | 랩·테스트 덱은 판단. 넣지 않았으면 이유를 적어라 |

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
| "스킬에 이렇게 적혀 있으니 맞겠지" | 웨이브 생성은 밸런스로 자주 바뀐다. **주장의 근거 코드를 열어 확인**하고, 어긋나면 스킬을 고쳐라 |
| "생성 로직만 고쳤으니 스킬은 상관없다" | 이 문서의 규칙 대부분이 그 코드에 매여 있다. 갱신 트리거 표를 보고 같은 커밋에서 재확인한다 |
| "덱이 N개니까 N개만 넣으면 됨" | 덱 목록은 계속 는다(공성·튜토리얼이 그렇게 늘었다). **매번 재도출**하라 |
