# 8. 공습 2기 붕괴 — 게이트 어긋남 창 (rev 3)

## 목적

**「공습」이 열리는 순간부터 Air 로스터가 2종이게 한다.**

unit 7 이 「공습」을 슬롯 2개로 넓힌 근거는 «슬롯 간 중복 배제로 Dragon + Skimmer 가 된다»였다. 그런데 `Concept_Airstrike.minWaveNumber 4` 와 `Enemy_Skimmer.minWaveNumber 8` 이 어긋나, **w4~7 창에서 Air 후보가 Dragon 단독**이었다. 중복 배제가 fail-open ②로 풀려 두 슬롯이 모두 Dragon 을 뽑고, `ClampGroupCounts` 는 슬롯별 적용이라 `maxPerWave 1` 이 슬롯 수만큼 곱해져 **웨이브가 정확히 Dragon×1+Dragon×1 = 2기**가 됐다(Coil w4~6 실측 재현).

기존 가드(「엘리트 웨이브 총량 > 1」)는 그룹별로만 봐서 2기를 초록 통과했다.

## 사용자 결정 (2026-08-15)

**`Enemy_Skimmer.minWaveNumber 8 → 4`** (공습 게이트는 4 유지). 이 값은 `waypoint-flight-enemy` unit 5 저작이라 사용자 결정을 받았다.

기각한 대안 — `Concept_Airstrike.minWaveNumber 4 → 8`: 컨셉 게이트는 **블록 첫 웨이브 번호**(1·4·7·10…)로 판정되므로 8은 실질 10이고, 실제 도달이 10~16웨이브라 대부분의 판에서 공습이 0~1회가 된다. unit 7 이 드래곤을 넣으려고 슬롯까지 늘린 컨셉을 안 보이게 만드는 수정이라 기각.

효과: w4~7 공습 = Dragon 1 + Skimmer 1~2 (2~3기, 엘리트 중복 소멸 — 기존 엘리트 2기보다 순한 편성). Air 속도 폭 2.0·2.5 = 0.5 불변. 다른 4컨셉은 전부 `altitude: Ground` 명시라 Skimmer 유출 없음.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Skimmer.asset` — `minWaveNumber 8 → 4`
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` 11개(v5 전부) — `waveGeneratorVersion 5 → 6`. **`waveSeed` 는 유지** — 풀이 안 바뀌었고, 시드까지 흔들면 pin·문서 표를 이유 없이 다시 쓴다
- `Assets/_Project/Tests/EditMode/WaveConceptAuthoringTests.cs` — 가드 2개(아래)

## 가드 (수정 전 빨강 확인 — 2026-08-15)

1. **저작 술어** `ConceptSlots_HaveEnoughDistinctCandidates_AtTheirGateWave` — 각 컨셉 × 각 라이브 덱, **게이트 웨이브에서** 필터 통과 후보의 distinct ≥ 슬롯 수 (전원 `maxPerWave 0` 이면 면제, 후보 0 은 무조건 실패). 100웨이브 생성 없이 에셋만으로 판정한다. 게이트만 보면 충분 — `minWaveNumber` 는 웨이브가 오를수록 후보를 추가만 한다. 변주(본+삽입)는 게이트+1 에서 같은 술어.
2. **결과 가드 강화** `EliteWaves_DoNotCollapseToASingleUnit` — 그룹별 `count ≤ 1` 을 **웨이브별 유닛 종류합 ≤ maxPerWave** 로 교체. `maxPerWave` 는 종류별 상한인데 클램프는 슬롯별이라, 중복 픽이 나면 그룹별 검사는 초록으로 샌다.

## 알려진 구멍 (이 unit 에서 고치지 않음)

`ClampGroupCounts` 는 **슬롯당** 상한을 적용한다 — 상한의 의미는 **종류당**이다. 위 저작 술어가 지켜지는 한 라이브에서 중복 픽이 없어 소비자 0이고, 지금 고치면 rng 는 안 흔들려도 수량 분배가 바뀌어 baseline 만 또 깨진다. 위 종류합 단언이 알람으로 남는다.

`Concept_Airstrike.countMul 0.3` 은 사실상 죽은 값이다 — Air cap 합이 3(Dragon 1 + Skimmer 2)이라 w8+ 공습 총량은 곡선과 무관하게 3 근처에 붙는다. 후반 공습이 쉬어가는 웨이브가 되는지는 실측 후 판단(후속 후보).

> **해소 (2026-08-17)**: `wave-ramp-two-phase` unit 2 가 Dragon 1→2·Skimmer 2→4 로 상향해
> Air cap 합 6. 클라이맥스(지수 구간)에서 공습이 3 에 고정되던 구멍이 풀렸다. countMul 0.3
> 은 이제 총량이 20 을 넘는 구간에서만 cap 에 먹힌다.

## 완료 기준

- 가드 2개: 수정 전 빨강(Serpent 공습 후보 1<2 · Coil w4 dragon 종류합 2>1) → 수정 후 초록
- `WaveConceptAuthoringTests` 25/25 · EditMode 전량에서 신규 실패 0 (기존 map-rework 폭 계약 4건만 잔존)
- `GeneratorVersion_IsBumped` = 6 · `WaveSeeds_ArePinnedAndUnique` 불변
- **Play 확인 (사용자)** — 공습 블록(w4~6)이 드래곤 1 + 스키머 호위로 오는가. EditMode 초록은 「공습이 2기로 온다」 증상 해소의 증거가 아니다

> **사용자 Play 확인 2026-08-16** — 공습 블록이 드래곤 중복 없이 편성됨. 커밋 111dc4fc.
