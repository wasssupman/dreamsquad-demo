# 3. 맵별 공격 덱 (적 웨이브 패턴) — 예산은 동일, 구성만 다르게

## 목적

맵마다 다른 적 패턴을 위해 각 맵에 물릴 `AttackDeck` 을 준비한다. **ArkFunnel = 기존 `WaveA` 재사용**(3 스폰 적합), **신규 맵용 덱 1종(WaveB) 신설**. 단, **점수 예산은 전 맵 동일**(README 계약) — 맵마다 다른 건 *적 구성*뿐이고 *채점 기준·판 길이·패배 조건·점수 상한*은 불변이어야 한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Decks/WaveB.asset` (신규 — 신규 맵용, 이름 placeholder)
- `WaveA.asset` 무변경(deck A, 그 signature 유지).

## 구현

`WaveA.asset` 구조 참조. WaveB 는 **예산 결정 필드를 WaveA와 동일 고정**하고 **적 구성 필드만** 달리한다.

### 반드시 WaveA와 동일 (예산 동결 — 변경 금지)

- `defeatGoalReachedCount = 10` — 스트레스 예산 9,000 상한 동일. (score-formula.md: 한계×점당 = 예산)
- `timerDurationSec = 180` — 시간 예산 18,000 상한 동일.
- **적 volume 범위 동일**(킬 상한 동급 유지): `minWaveCount`/`maxWaveCount`, `minUnitsPerWave`/`maxUnitsPerWave`, `waveCountJitter`, `bossWaveInterval`, `bossEscortMin`/`bossEscortMax` 를 WaveA와 같게. 킬 값은 잡몹 일괄 100·보스 2,000 이고 유닛 *종류*와 무관하므로, 총 잡몹/보스 개수 범위만 같으면 킬 상한이 동급이다.
  - 부수효과: WaveA 의 `minUnitsPerWave=6` 은 신규 맵 2 스폰에도 자동으로 `≥ 스폰수`(6≥2, 3배 커버) — 스폰 수 결합 규칙 자동 충족. 별도 축소 불필요.

### 맵마다 달리 (flavor 차별화 — 예산 불변)

- `attackUnitPool` — WaveA와 다른 유닛 조합(빠른 소수형 vs 탱키형 등). 종류가 달라도 킬 총합은 불변.
- `bossUnit` — 맵 성격에 맞는 다른 보스 가능(단 보스도 킬 2,000 동일).
- `intraWaveSpacingSec` — 웨이브 내 분사 간격(pacing 체감).
- 레인 분배는 코드가 스폰 수(2)로 자동 — 데이터 손댈 것 없음.
- `waveSeed = 0` — matchSeed 파생(매판 변주, 재현 가능). (WaveA 는 현재 고정값 유지.)

### 계약 준수

- `useGeneratedWaves = 1`. 레거시 `spawns` 리스트 미사용(비워도 됨).
- `bossUnit` 은 `attackUnitPool` 에 넣지 않는다(계약상 분리, 생성기도 방어적 제외).

## 완료 기준

- [ ] WaveB 의 `defeatGoalReachedCount=10`·`timerDurationSec=180` + volume 범위 필드가 WaveA와 **동일**
- [ ] `attackUnitPool`(≥2 distinct)·pacing 등 구성 필드는 WaveA와 **구별**됨, `bossUnit` pool 미포함
- [ ] `WavePatternGenerator.Generate(WaveB, seed)` 예외 없이 플랜 생성
- [ ] 신규 맵(2 스폰) 단독 Play — 매 웨이브가 2 레인 모두에 분배, 보스 웨이브 정상, 체감 패턴이 WaveA와 구별
- [ ] 승리 시 스트레스 최소 900 보장·시간 예산 상한 동일 확인(간이: 같은 클리어 시점 → 같은 시간점수)
