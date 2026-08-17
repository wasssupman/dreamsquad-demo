# wave-ramp-two-phase — 평탄 본편 + 지수 클라이맥스 웨이브 페이싱

> ## 목표 3줄
>
> 1. **w1–15 = 본편**: 수량은 5→12 평탄 상승, 난이도 대신 **컨셉 다양성**(4종+)이 판을 끌고 간다.
> 2. **w15+ = 모든 판의 자연 클라이맥스**: 지수 수량 + 변주 상시(타입 혼합) + 조직된 그룹의 다중 레인 협공 가중. 당기기는 클라이맥스를 더 길게 만드는 수단이 된다.
> 3. **적용은 공성 3덱 먼저** — 라이브 6덱은 기본값(기존 지수)으로 byte-identical 을 유지하고, 검증 후 별건으로 확대한다.

상태: **작성됨 2026-08-17** · 사용자 승인 대기 · **선행: `siege-lane-spawn`** (laneCount 2 확정 후 시드를 떼야 재작업이 없다)

## 사용자 결정 (2026-08-17)

- 지수 구간 = **모든 판의 자연 클라이맥스**. 평탄화로 처치가 빨라져 무당기기 판이 w17~19 에 도달하는 것을 받아들인다. 경계 w15 유지.
- 다중 레인 등장 기준 = **조직된 그룹 협공**. 「평소」의 개체 분산(laneGroup −1)은 다중 레인으로 치지 않는다 — 평소 저작 무변경.
- 적용 범위 = **공성 3덱 먼저**. 라이브는 검증 후.
- 후반 공습 3기 고정 = **`maxPerWave` 상향**으로 해소 (Dragon 1→2, Skimmer 2→4).

## 작업 단위

| # | 작업 | 문서 |
|---|---|---|
| 0 | 두 단계 수량 곡선 — 순수 함수 + 덱 필드 `waveRampBreakWave`/`waveRampBreakUnits`(맨 뒤 append, 0 = off = 기존 지수와 byte-identical). 공성 3덱만 (15, 12) 설정 | 0_two_phase_curve.md |
| 1 | 클라이맥스 변주 격상 — break 이후 변주 **상시** 적용(현행: 블록 가운데만) + `InheritLanes` 가 본 편성에 없는 laneGroup 에 **미사용 레인을 연다**(현행: 본 레인으로 접힘). 둘 다 unit 0 의 break 필드가 게이트 — 라이브(off)는 접힘 유지로 무변경 | 1_climax_variant_escalation.md |
| 2 | 공습 상한 상향 — `Enemy_Dragon.maxPerWave` 1→2 · `Enemy_Skimmer` 2→4. **enemy-wave-integration 스킬 필수 태움** | 2_airstrike_cap_raise.md |
| 3 | 공성 3덱 시드 재선정 + 회귀선 — «w1–15 컨셉 4종+» 술어로 탐색·확정, 그 술어를 EditMode pin 으로. Duel 시드의 Serpent 중복(20260841)도 해소. **후보 탐색은 오프라인 시뮬로, 확정 pin 은 실제 생성기(EditMode)로** — 포트는 정본이 아니다 | 3_seed_reselection.md |
| 4 | handoff | 4_handoff_summary.md |

## Feature-wide 계약

1. **곡선은 rng 를 소비하지 않는다** (실측 확인: 곡선·상한 변경에도 컨셉 시퀀스 불변). `ExponentialWaveTotal` 확장은 plain 입력 → plain 출력 순수 함수 유지(제약 10). 곡선: `center = i < break ? lerp(min, breakUnits, i/break) : breakUnits × growth^(i−break)`, `maxUnits` 클램프 유지.
2. **break 필드 하나가 클라이맥스 전체의 게이트다.** 곡선 전환·변주 상시·변주 신규 레인이 모두 `waveRampBreakWave > 0` 에서만 켜진다 — 목표 문장(«그 이후부터 지수 상승과 타입을 섞는다»)이 knob 하나로 표현된다. 라이브 덱은 off 라 세 가지 모두 현행 그대로.
3. **변주 격상의 그라데이션**: break 전 = 블록 가운데 1/3(현행), break 후 = 3/3. 협공 빈도가 이 비율로 상승한다. 3단계 세분화는 실측 후 후속 후보.
4. `InheritLanes` 의 기존 계약(«본 편성 레인은 블록 안에서 불변»)은 유지된다 — 새 laneGroup 이 **추가 레인을 여는 것**이지 기존 입구를 옮기는 게 아니다. 주석의 근거를 같이 갱신한다.
5. 변주 협공 저작: `Concept_Swarm`·`Concept_Heavy` 의 `variantSlots.laneGroup 0→1`. 공유 에셋이지만 라이브 덱은 게이트 off 로 접힘(=현행 lane) 유지 — 에셋 복제 없이 격리된다.
6. **unit 2 는 라이브에 파급된다(명시된 예외).** `maxPerWave` 는 적 SO 전역이라 라이브 공습 웨이브 수량이 3캡→6캡으로 는다. rng 는 불변(재추첨 없음, `ClampGroupCounts` 는 rng 무소비)이나 **킬 예산 pin 재도출** 필수. 이것이 「후반 공습 = 쉬어가는 웨이브」(wave-concept-blocks unit 8 의 알려진 구멍)의 해소이기도 하다.
7. `waveGeneratorVersion` 은 **전 덱 일괄 bump**. 라이브는 출력 불변이지만 세대 표기가 갈리면 `SiegeDevSlot_IsWiredWithCurrentGenerationDeck` 의 세대 대조가 깨진다.
8. 생성기·`AttackDeck` 필드 변경이므로 **enemy-wave-integration 스킬의 「갱신 트리거」 표를 해당 커밋에서 같이 갱신**한다 (unit 0·1·2).

## 검증 질문

- w1–15: 컨셉 4종+ 이 등장하고 웨이브 총량이 12 를 넘지 않는가 (EditMode 술어)
- w15+: 수량이 지수로 오르고, 변주가 매 웨이브 붙고, 협공(2레인 그룹) 빈도가 break 전보다 높은가
- 라이브 6덱: unit 2 의 공습 수량 외 **byte-identical** 인가 (기존 signature 대조)
- Play: 공성 판이 «다양한 본편 → 급해지는 클라이맥스» 로 읽히는가 (사용자 체감)

## 후속 후보

- **라이브 6덱 확대** — 공성 검증 후 break 필드 설정 + 시드 재선정 (사용자 결정)
- **변주 격상 3단계 세분화** (1/3 → 2/3 → 3/3)
- **Air 적 신규** — 공습 로스터 확장의 정공법 (이번엔 상한 상향으로 대체)
- **무당기기 도달 웨이브 실측** — 평탄화 후 실제 페이스 재측정, break 경계 재조정 여부 판단
