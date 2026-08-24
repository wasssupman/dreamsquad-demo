# 맵 · 웨이브 밸런싱 레퍼런스

> 맵 로테이션 / 웨이브 난이도 / 몬스터 스탯을 조정하는 실무 가이드. **자주 바꾸는 값들**이라 여기 모아둔다.
> 점수 산식 상세는 `docs/reference/score-formula.md`, 맵 파이프라인은 `object-pipeline-map.md` 참조.

---

## 조정하고 싶은 것 → 어디로 가나

| 바꾸고 싶은 것 | 파일 / 도구 | 핵심 필드 |
|---|---|---|
| **어떤 맵이 등장하나** (맵 추가/제거) | `Assets/_Project/Data/Maps/MapDocumentPool.asset` | `entries` (맵+덱 쌍) |
| **맵 지형** (경로·스폰·골·배치칸) | `Window/Wassup/Map Painter` 또는 execute_code | MapDocument (tiles/spawns/goals) |
| **웨이브 난이도** (몬스터 수·종류·보스) | 맵별 `Deck_{맵}.asset` (AttackDeck) | 아래 §웨이브 knob |
| **웨이브의 «성격»** (편성 컨셉) | `Assets/_Project/Data/WaveConcepts/Concept_*.asset` | 아래 §웨이브 컨셉 블록 |
| **개별 몬스터 강함** (HP·속도·공격) | `Enemy_*.asset` (AttackUnitData) | health/moveSpeed/attackRange/attackCooldown… |
| **마음이 얼마나 버티나** (판 길이) | 맵별 `Deck_{맵}.asset` | `goalStabilityMax` — 아래 §마음 · 스트레스 |
| **처치로 얼마나 되돌리나** (교환비) | 같은 파일 | `killHealPerAwakening` — 아래 §마음 · 스트레스 |
| **특정 몬스터를 초반 웨이브에서 제외** | `Enemy_*.asset` (AttackUnitData) | `minWaveNumber` (기본 1=제한없음, Runner=2) |
| **맵 랜덤 on/off** | `BattleBridge.fixedMapSeed` (BattleScene) | `0`=시드 배정(아래 우선순위), 비0=한 맵 고정 |
| **개발 중 특정 맵으로 진입** | 로비 맵 스테퍼(◀ ▶ OFF, dev/에디터 전용) | `DevMapOverride`(PlayerPrefs), OFF=시드 배정 복귀 |

맵 인덱스 우선순위: **로비 스테퍼(dev override) > `fixedMapSeed`(비0) > 토너먼트 시드(같은 토너먼트 = 같은 맵) > 시드 부재 시 0번 폴백**.

---

## 맵 ↔ 덱 페어링

풀의 각 엔트리 = `(MapDocument, AttackDeck)`. **맵마다 자기 전용 덱**을 가진다(2026-07-23~):

> ⚠ **라이브 `entries` 는 현재 `MapDocument_Duel` 한 장뿐이다**(`duel-live-focus`, 커밋 `fc755760`).
> 아래 표의 나머지는 `devEntries` 로 옮겨져 **로비 맵 스테퍼로만** 들어간다. 토너먼트 판은
> 시드와 무관하게 Duel 로 간다 — 밸런스를 재려면 여기가 첫 번째 맵이다.

| 맵 asset | 덱 | waveSeed |
|---|---|---|
| MapDocument_Serpent | Deck_Serpent | 20260821 | Boss_Nightmare |
| MapDocument_Coil | Deck_Coil | 20260822 | Boss_Nightmare |
| MapDocument_Twin | Deck_Twin | 20260823 | Boss_Jjangssen |
| MapDocument_Spiral | Deck_Spiral | 20260824 | Boss_Jjangssen |
| MapDocument_Zig | Deck_Zig | 20260825 | Boss_Mamemo |
| MapDocument_Hook | Deck_Hook | 20260826 | Boss_Mamemo |

(4번째 열 = 그 맵의 보스. 판당 보스가 1기라 덱마다 **1종을 저작**한다 — 시드 뽑기로는 어차피 맵마다 고정되고 «어느 맵이 어느 보스를 받나»만 시드에 맡겨진다. `wave-concept-blocks` unit 3.)

- 덱 asset 위치는 `Assets/_Project/Scripts/Data/Decks/`. 무한 모드 전용 `Deck_Endless`(waveSeed 20260827, 보스 로테이션 3종 유지)는 풀 밖 — `BattleBridge.endlessEncounter` 슬롯이 들고 있다.
- 맵과 덱은 **같은 인덱스로 함께 선택**된다(`MapPoolSelect.SelectIndex(seed, count)`), 그래서 "맵마다 고정된 적 패턴".
- 맵 추가 = 풀 `entries` 에 (새 MapDocument, 새 Deck) 한 쌍 추가. **코드 변경 불필요**(GUID 참조).
- `WaveA.asset`/`WaveB.asset` 은 레거시 원본(테스트 참조) — 풀은 안 씀, 삭제 금지.

---

## 웨이브 난이도 knob (AttackDeck)

`WavePatternGenerator.Generate(deck, seed)` 가 이 값들로 웨이브를 짠다:

| 원하는 것 | 필드 | 현재 기본 |
|---|---|---|
| 웨이브당 몬스터 시작값 ↑↓ | `minUnitsPerWave`(= 곡선의 base) | 5 |
| 성장 속도 ↑↓ | `unitGrowthPerWave`(웨이브마다 ×) | 1.12 |
| 수량 상한 | `maxUnitsPerWave`(= cap) | 24 |
| **웨이브마다 수량 평탄** | `unitGrowthPerWave = 1` | (성장 없음) |
| 웨이브 개수 | `minWaveCount` / `maxWaveCount` | 100 / 100 (**명목** — 아래 참조) |
| 웨이브 간 상한 간격 | `maxWaveIntervalSec` | 20 |
| 등장 몬스터 종류 | `attackUnitPool` (AttackUnitData[]) | 값 재도출로 확인(스킬 「값 재도출」 — 숫자를 여기 얼리지 않는다) |
| 보스 | `bossPool`(덱당 1종) · `bossWaveInterval` · `bossEscortMin`/`Max` | 맵별 1종 · **9마다** · 3~4 |
| 스폰 템포 | `intraWaveSpacingSec` | **0.5s** |
| **웨이브 컨셉** | `waveConceptPool` · `conceptHoldWaves` | 5종 · 3웨이브 |
| **두 단계 곡선 + 클라이맥스**(공성 전용) | `waveRampBreakWave` · `waveRampBreakUnits` | 공성 3덱 15 · 12 / 라이브 0(끔) — break 전 평탄, 후 지수 + 변주 상시. **breakWave 값 변경 = 변주 구간 이동 = rng 갈림 → 시드 스캐너 재실행**(wave-ramp-two-phase) |
| **웨이브 시작 → 첫 적 유예** | `waveSpawnLeadInSec` | 2s |
| 골 안정도 최대치(패배 조건) | `goalStabilityMax` | 20 |
| 스트레스 한계(계약 카드 지불 대상, **패배와 무관**) | `defeatGoalReachedCount` | 10 |
| 제한 시간 | `timerDurationSec` | 180 |

**수량 결정 방식**(three-minute-survival unit 2): **완만한 지수 성장**.
`total_i = clamp(round(base × growth^i) + jitter, base, cap)` — 단 `waveRampBreakWave ≥ 2` 인
덱(공성)은 break 전 구간이 `lerp(base, breakUnits, i/(break−1))` 평탄 상승으로 대체된다
(wave-ramp-two-phase unit 0). — base=`minUnitsPerWave`,
growth=`unitGrowthPerWave`, cap=`maxUnitsPerWave`, jitter 폭=`waveCountJitter`(waveSeed 파생).
구 선형 ramp(전체 웨이브 수로 나눈 보간)는 은퇴 — 웨이브 상한이 100(명목)이 되면서 분모가
의미를 잃었다. 일반 웨이브 = **2종류**(countA+countB 분할). 보스 웨이브 = 보스1 + 호위 치환.

**웨이브 진행 = 이벤트 구동**: 이전 웨이브를 전멸시키면 리드인 뒤 즉시 다음 웨이브, 못 잡으면
트리거 후 `maxWaveIntervalSec`(20초)에 자동 진행. 시각 그리드(`triggerTimeSec`)는 **명목값**
(= i × 상한 간격 = 최악 케이스)으로만 남고 런타임은 읽지 않는다. 당기기(`ForceNextWave`)의
플레이어 경로는 제거됐다(테스트 진행 동력으로만 남음).

**⚠ 스폰 창 불변식**: `waveSpawnLeadInSec + (maxUnitsPerWave − 1) × intraWaveSpacingSec`
< `maxWaveIntervalSec`. 위반하면 `_pending` 이 영구히 비지 않아 **전멸 즉시 진행이 구조적으로
죽고** 상한 케이던스만 남는다. 현재 값: 2 + 23 × 0.5 = 13.5초 < 20초 ✓.
생성기가 위반 시 경고하고 `WaveKillBudgetPinTests` 가 7개 덱 전부를 가드한다.
수량 상한을 올릴 때는 `intraWaveSpacingSec` 을 함께 내려라.

**웨이브 100은 명목이다**: 180초 + 20초 상한이면 실제 도달은 **10~16웨이브**(못 잡으면
floor(180/20)+1 = 10, 즉시 밀면 14~16). 곡선은 그 구간에서 성장이 보이도록 저작한다.
브리핑 스트립(`WavePatternStripView`)은 앞 12장만 그린다(100장이면 인트로가 6.4초).

**리드인**(`waveSpawnLeadInSec`, wave-pattern unit 11): 웨이브 트리거와 첫 적 등장 사이의 유예.
트리거 그리드(`i × interval`)·강제 호출 리스케줄·플랜 시각·브리핑 표기는 **불변**이고 스폰만 밀린다.
올릴 때는 **마지막 스폰이 `timerDurationSec` 안에 남는지** 확인할 것(`WaveKillBudgetPinTests` 가 가드).
작성 플랜(`WavePlanAsset`)에는 적용되지 않는다 — 그룹 상대 시각으로 직접 표현한다.

**작성 플랜의 레인 고정**(`AuthoredSpawnGroup.laneIndex`, first-run-tutorial unit 8):
**-1 = 무지정(기본, 기존 동작)** — 펼침 순번 % 레인 수로 라운드로빈해 스폰이 갈린다.
≥0 을 박으면 그 그룹이 **한 스폰 지점으로 몰린다**(Duel 은 `laneCount 2`, `0` = 적 마음의
`y-1`). 「배치 스킬 한 번이 한 덩어리를 덮는 장면」처럼 **몰림이 의도일 때만** 쓴다 —
그냥 박으면 반대쪽 레인이 통째로 빈다. 규칙 상세는 `.claude/skills/enemy-wave-integration`.

**완전 수제 웨이브**: `useGeneratedWaves=false` + `spawns` 리스트에 (시각, 유닛, 수) 직접 authoring → 생성기 안 씀.

---

## 마음 · 스트레스 knob (heart-stress-axis)

판이 끝나는 통로가 둘이라(3분 만료 · 스트레스 100) **마음은 이제 판 길이를 정하는 손잡이**다.

| 원하는 것 | 필드 | 어디 | 현재 |
|---|---|---|---|
| 마음이 더 오래 버티게 | `goalStabilityMax` | `Deck_*.asset` | **1500** |
| 처치 보상을 크게 | `killHealPerAwakening` | 같은 파일 | **10** |
| 적별 회복 서열 | `awakeningReward` | `Enemy_*.asset` · **시트 소유** | 잡몹 2 / 엘리트 3 / 보스 5 |
| 돌격형 한 방 | `stabilityDamage` | `Enemy_Runner`·`Enemy_Swift` | 50 (= 3.3%) |
| 방패 두께 | `health` | `Structure_GuardInstinct` | 1000 ×2 (Duel·Isle·Ford) |

**두 손잡이는 하는 일이 다르다 — 같은 패스에서 함께 돌리지 말 것.**

- `goalStabilityMax` 는 **시계**다. 키우면 판 전체가 같은 비율로 느려진다. 「한 대 맞기 :
  한 마리 잡기」의 교환비는 하나도 안 바뀐다 — 분모가 양쪽에서 약분되기 때문이다.
- `killHealPerAwakening` 은 **저울**이다. 여기만이 공격과 방어의 환율을 움직인다.

빠른 산수(마음 1500 기준):

| 상황 | 스트레스 |
|---|---|
| `Enemy_Basic` 1타(20) | 1.33 |
| 잡몹 1킬(reward 2 × 10 = 20) | −1.33 (**정확히 상쇄**) |
| `Enemy_Basic` 1기가 마음에 붙어 있음 | 초당 2.67 → **37.5초**에 100 |
| 같은 상황 3기 | **12.5초** |
| `Enemy_Skimmer` 1기(DPS 100) | 초당 6.67 → **15초** |
| 돌격형 1기 통과 | 3.33 (30기 = 판 종료) |

**방패를 빼먹지 말 것.** Duel·Isle·Ford 는 방어 본능 2기(각 1000)가 살아 있는 동안 마음이
아예 표적이 되지 않는다(`CoreShielded`). 그 세 맵의 실효 체력은 1500 이 아니라 **3500** 이고,
위 표의 초 단위는 **본능이 다 무너진 뒤부터** 흐른다. 나머지 맵은 첫 웨이브부터 맨몸이다.

---

## 웨이브 컨셉 블록 (wave-concept-blocks)

**3웨이브마다 편성의 «성격»이 바뀐다.** 컨셉은 웨이브가 아니라 **블록**의 속성이고, 블록 안에서 컨셉과 lane 배정이 고정되고 수량만 곡선을 따라 오른다(배우고 → 대응하고 → 겨우 버티고).

| 컨셉 | 성질 · 위상 | countMul | 게이트 | weight |
|---|---|---|---|---|
| 평소 | 무필터 2종 · 전 lane 분산 | 1.0 | 1 | 0.6 |
| 벌떼 | Runner · 한 lane 집중 | 1.3 | 4 | 1.0 |
| 중장 | Tanker **단일 슬롯** · 한 lane | 0.4 | 4 | 1.0 |
| 원거리 | Shooter · **협공(두 lane)** | 0.7 | 7 | 1.0 |
| 공습 | Air · 한 lane | 0.3 | 4 | 0.6 |

**바꾸고 싶을 때 만지는 곳은 이 표 하나다** — `Assets/_Project/Data/WaveConcepts/Concept_*.asset`. `countMul`·`weight` 는 실측으로 조정할 초기값이다.

규칙 몇 개는 코드가 아니라 **데이터가 소유**한다:

- **블록 0(웨이브 1~3)이 「평소」인 것은 게이트로 성립한다** — 「평소」만 `minWaveNumber 1`, 나머지는 4 이상. `i < 3` 같은 분기는 코드에 없다.
- **같은 컨셉이 두 블록 연속으로 안 나온다**(직전 배제). 풀에 컨셉이 1개뿐이면 배제를 풀어 fail-open.
- **`countMul` 이 필요한 이유**: 곡선은 **개체 수**를 내므로 성질을 통일하면 난이도가 성질에 끌려간다(Runner 20hp × 19 = 380 vs Tanker 100hp × 19 = 1,900 — 5배).
- **비행은 「공습」에서만 나온다.** 성질 컨셉은 전부 `altitude = Ground` 를 명시한다 — 고도와 성질은 직교하므로(Shooter 이면서 비행인 적이 있다) 명시하지 않으면 지상 컨셉에 대공 없이 못 잡는 적이 섞인다.
- **뭉침은 저작으로 만든다.** 스폰 시 속도를 덮어쓰지 않고(코어 로직 불침범), 슬롯의 `classFilter` 가 속도 폭이 좁은 후보만 남기게 저작한다(Tanker 1종 = 1.5 단일, Air 2종 = 2.0·2.5 폭 0.5, Runner 2종 = 4.5·5.6).
- **컨셉 게이트와 로스터 게이트를 맞춰라.** 컨셉이 열리는 웨이브에 필터 통과 후보가 슬롯 수보다 적으면 중복 픽이 `maxPerWave` 를 슬롯 수만큼 곱한다(공습 w4~7 Dragon×2 사고 — wave-concept-blocks unit 8). `ConceptSlots_HaveEnoughDistinctCandidates_AtTheirGateWave` 가 에셋만으로 가드한다. Skimmer 게이트는 8→**4**(공습 게이트와 정렬, 2026-08-15).
- **컨셉 풀을 비우면 현행 동작(랜덤 2종·전 lane 분산)으로 폴백**한다 — rng 소비 순서까지 동일한 무회귀 경로다.
- ⚠ **엘리트를 `Tanker` 로 저작하면 안 된다**(elite-whirlpot unit 2, 2026-08-14). 「중장」이 **유일한 1슬롯 컨셉**이고 Tanker 만 필터하는데, 엘리트는 `maxPerWave 1` 이 강제되므로(`EliteWaves_DoNotCollapseToASingleUnit`) 그 슬롯에 뽑히는 순간 **웨이브 전체가 1기로 붕괴**한다. 슬라임(Bruiser)·드래곤(Shooter)이 무사한 것은 구조 덕이다 — 어떤 컨셉도 `Bruiser` 를 필터하지 않고 Shooter 를 쓰는 「원거리」는 슬롯이 2개다. 신규 엘리트는 **`Bruiser` 또는 `Shooter`** 로 저작하고, 1슬롯 컨셉을 새로 만들 땐 그 필터에 엘리트가 들어올 수 있는지 먼저 볼 것.

lane 은 절대 인덱스가 아니라 `laneGroup` **위상**으로 저작한다(같은 값 = 같은 lane, `-1` = 무지정). 실제 인덱스는 `waveSeed` 가 고르므로 한 컨셉 풀이 스폰 2~4개인 6맵에 그대로 쓰이고, 같은 「원거리」가 맵마다 다른 복도 쌍이 된다.

⚠ **`laneCount`(맵 스폰 수)는 결정론 키의 일부다.** lane 요구량 게이트가 후보 집합을 바꾸므로 스폰 수가 다른 맵은 컨셉·유닛·수량까지 달라진다. 브리핑과 런타임이 같은 값을 넘겨야 예고와 실스폰이 일치한다.

설계 이력·기각 목록은 `docs/spec/wave-concept-blocks/`.

---

## ⚠️ 결정론 규칙 (절대 지킬 것)

**같은 맵 = 매번 같은 웨이브** 는 `waveSeed` 로 보장된다:

- `BattleBridge`: `waveSeed = deck.waveSeed != 0 ? deck.waveSeed : DeriveWaveSeed(matchSeed)`.
- 덱 `waveSeed` **비0 고정** → `matchSeed`(매판 랜덤) **무시** → 시드 고정 → 웨이브(수·종류·순서·수량) **매판 동일**.
- **`waveSeed` 를 0 으로 만들면 매판 달라진다 — 절대 금지.** (실증: 각 덱 3회 생성 시 유닛·수량까지 완전 일치.)
- 매판 랜덤인 건 "**어느 맵이 나오냐**"(`fixedMapSeed=0`)뿐. 특정 맵이 나오면 그 맵 웨이브는 항상 같음.
- 새 맵/덱 추가 시에도 **덱 waveSeed 를 비0 유니크 값**으로.
- **편성이 바뀌는 조정을 했으면 `waveGeneratorVersion` 을 +1 한다** (전 컨셉 덱 동일 값, 현재 7). 수량 knob·게이트·풀·컨셉 어느 쪽이든 결과 편성이 달라지면 대상이다. `waveSeed` 는 그대로 — 시드는 「같은 맵 같은 웨이브」의 키고, 버전은 「baseline 이 언제 바뀌었나」의 표식이다. pin 테스트(`GeneratorVersion_IsBumped_SoTheNewBaselineIsVisible`)가 숫자를 하드코딩하므로 **같은 커밋에서** 갱신한다.

⚠ **여기서 값만 바꾸면 안 되는 변경**: 적 SO 신설, `minWaveNumber`/`maxPerWave`/`enemyClass`/`traversalLayers` 변경, `WavePatternGenerator`·컨셉 슬롯/필터·`WavePlanAsset` 로직 변경 — 이 경우 **`.claude/skills/enemy-wave-integration` 스킬이 필수**다(풀 삽입 위치·게이트 정합·튜토리얼 로스터 계약·가드 테스트까지 그쪽이 강제). 이 문서는 «어느 값이 어디 있나»의 정본이고, «바꿀 때 뭘 같이 해야 하나»의 정본은 그 스킬이다.

---

## 점수 예산과의 관계

점수원은 **처치 하나**다(`docs/reference/score-formula.md`). 시간·스트레스 축은 은퇴했다. 그래서:

- **몬스터 종류를 바꾸면 예산이 바뀐다** — `killScore` 가 티어별로 다르다(일반 1 / 엘리트 3 / 보스 10).
- **수·성장률·상한 간격을 바꾸면 예산이 변동한다**(3분 안에 몇 웨이브를 미느냐가 곧 점수).
- **제한시간·안정도는 예산에 직접 관여하지 않는다** — 안정도는 동점 판정 tie-break 값이다.
- **맵 간 점수 소폭 차등은 허용**(2026-07-23 사용자 결정) — 예산을 맵마다 똑같이 맞출 필요 없음. **유일 불변식은 "같은 맵=같은 웨이브"**. 맵별 난이도는 그 `Deck_*` 만 자유롭게 조정.

---

## 맵 지형 규칙 (Map Painter / 신규 맵)

- **골 1~2개**(목표지점). 스폰 **2~4개**(1스폰 금지 — 런타임 `MapConnectivity` 가 `<2` 거부).
- **복도는 골 셀에서만 만난다**: 분리 맵=각 스폰 자기 골(완전 분리), 수렴 맵=여러 스폰이 골에서 합류(non-goal 병합 금지).
- 이동로(Walk) 스폰→골 **≥20**, Walk 1링=Place(배치칸), 나머지 Deco. **2×2 walk 블록 금지**. 그리드 **≤20×12**.
- 수동 맵 관례: `authoringSeed=-1`, `generatorVersion=0`. 덮어쓰기는 **GUID 유지**(풀/덱 배선 불변).
- 골 여러 개면 flow field 가 **최근접 골** 라우팅(`FlowFieldBuilder.BuildFromSources`). 복도 분리면 각 스폰이 자기 골로.

---

## 검증

- **회귀 가드**: `Tests/EditMode/MultiGoalPoolSeparationTests` — 풀 맵 골 ≤2·각 스폰 도달·복도 non-goal 병합 금지. `MapConnectivityTests`·`FlowFieldSingletonTests`.
- **런타임 검증**: `MapConnectivity.AllSpawnsReachGoal`(각 스폰 아무 골이든 도달) — adapter/브리지 가드.
- **덱 결정론 확인**: execute_code 로 `WavePatternGenerator.Generate(deck, deck.waveSeed)` 를 N회 생성해 signature(유닛 id+count) 비교.
- **시트 검증**: 값을 curl 로 읽어 SO 대조(읽기 전용). 상세 `docs/reference/lessons/` + 메모리.

---

## 편집 경로 요약

- **인스펙터 직접**: Deck_*.asset / Enemy_*.asset / MapDocumentPool.asset.
- **Map Painter**: `Window/Wassup/Map Painter` (지형 그리기·검증·Bake).
- **execute_code**: 프로그래매틱 대량 편집(맵 bake·덱 생성). CodeDom C#6 — `in` 파라미터는 `ref`, delegate 파라미터명 외부 지역변수와 충돌 금지.
- **Google Sheet 동기화**: 덱/유닛 값 시트 편집→import (프로젝트에 sheet-sync). import 전엔 디스크 SO 가 옛값.
