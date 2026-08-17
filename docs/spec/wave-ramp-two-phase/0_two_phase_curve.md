# 0. 두 단계 수량 곡선 (옵트인)

## 목적

`center = i < break ? lerp(min, breakUnits, i/break) : breakUnits × growth^(i−break)`.
본편(w1~break)은 평탄 상승, 그 뒤는 기존 지수 — «난이도 낮은 다양한 본편 + 클라이맥스».
break 미저작(0) = 기존 지수와 완전 동일 경로 → 라이브 덱 무회귀가 데이터로 성립.

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `ExponentialWaveTotal` 트레일링
  파라미터 2개 + `Generate` 스레딩(맨 뒤 append — positional 호출자 보호, 기존 관례)
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `waveRampBreakWave`/`waveRampBreakUnits`
- 공성 3덱 에셋 — (15, 12) 저작 (키 부재 → 삽입)
- `WaveCountRampTests`(곡선 순수) · `WaveConceptGenerationTests`(rng 중립 pin)
- `.claude/skills/enemy-wave-integration/SKILL.md` — 덱 필드 추가 반영 (같은 커밋 규칙)

## 구현

1. 곡선은 **rng 무소비 계약 유지** — jitter01 은 plain 입력. 실측(오프라인)으로 곡선 변경이
   컨셉 시퀀스를 안 흔드는 것을 확인했고, 같은 술어를 EditMode pin 으로 박는다
   (`RampCurve_DoesNotDisturbConceptSequenceOrPicks`).
2. break 필드는 unit 1 의 변주 격상 게이트도 겸한다(README 계약 2) — knob 하나.
3. `waveGeneratorVersion` bump 는 unit 2(라이브 출력이 실제로 바뀌는 커밋)에서 일괄.

## 완료 기준

- EditMode: 곡선 순수 4건(off=레거시 동일·평탄 단조·클라이맥스 지수·클램프) + rng 중립 pin
  초록, 전량 무회귀
- 공성 3덱 w13~15 총량이 breakUnits(12) 근처로 내려앉는 것을 생성 결과로 확인
