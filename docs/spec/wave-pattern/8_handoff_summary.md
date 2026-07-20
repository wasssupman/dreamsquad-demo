# 8. Handoff Summary — wave-pattern units 6~7 (고정 시드 + 수량 램프)

> 2026-07-20 작성. 최신 계약은 README 와 번호 문서가 우선한다. 이 문서는 지도다.
> 1차 구현(0~5)의 인계는 `5_handoff_summary.md`. 이 문서는 그 이후 추가분(6~7)과 밸런스 튜닝.

## Commit

- `2d8c843e` feat(wave-pattern): 고정 웨이브 시드 (unit 6) — `deck.waveSeed` 비0 = 라이브 고정
- `529ebf3b` balance(wave-pattern): 수량 −50% + 코스트 생산속도 −30%
- `2c2ecacd` balance(wave-pattern): 수량 6~10 · 스폰 spacing 1.0 · 진행 수량 램프 (unit 7)
- (연관, 타 스펙) `85cf82db` tune(spawn-point-alert): 예고선 draw 0.55→1.0s

## Implemented

- **unit 6 고정 시드**: `deck.waveSeed` 비0 = 매판 동일 패턴(테스트 버전), 0 = `MatchSeed.DeriveWaveSeed` 파생. `WaveA.asset` 은 `20260720` 고정. 시작 로그에 출처(`deck-fixed|derived`) 표기. 부수 효과로 아웃게임 브리핑 스트립과 런타임이 같은 플랜 공유.
- **unit 7 수량 램프**: 웨이브 총 마릿수(`total`)가 웨이브 진행에 따라 `minUnitsPerWave`(첫)→`maxUnitsPerWave`(마지막) 선형 증가 + `±waveCountJitter` 지터, `[min,max]` 클램프. 순수 함수 `RampedWaveTotal` 로 분리(EditMode 검증).
- **밸런스 값**(`WaveA.asset`): 수량 10~15 → −50% → +20% → 램프 양끝 6~10. `intraWaveSpacingSec` 0.35→1.0(스폰지점 순차 출현). 코스트 `regenPerSec` 0.5→0.35.
- min/max 의 **의미가 바뀜**: "웨이브별 균등 랜덤 범위" → "램프 양끝". 이게 unit 7 의 핵심 계약 변화.

## Key Files

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `RampedWaveTotal`(순수), 생성 루프
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `waveSeed` / `waveCountJitter` 필드
- `Assets/_Project/Scripts/Data/Decks/WaveA.asset` — 라이브 밸런스 값(source of truth)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryInitializeGeneratedWaves` 시드 resolve 분기
- `Assets/_Project/Tests/EditMode/WaveCountRampTests.cs` — 램프 순수 함수 회귀
- `Assets/_Project/Data/Config/DefaultCostConfig.asset` — 코스트 생산속도

## Verified

- EditMode 1022 통과(실패 0, 스킵 2 = 기존 known-ignore). `WaveCountRampTests` 6건 신규 그린.
- `WavePatternGeneratorTests`·`WavePatternGeneratorBossTests`·`WaveSpawnForecastTests` 회귀 없음(범위/결정론 불변식).
- 사용자 Play 확인 2026-07-20 — 후반 웨이브 마릿수 증가 체감 통과.

## Notes (되돌리면 안 되는 판단)

- **`NextInt(min,max)`→`NextFloat()` 는 rng state 를 동일하게 1스텝 소비.** 그래서 램프 도입 후에도 보스 후처리 rng 정렬(`NonBossWavesMatchBossOffPlanAtSameSeed`)이 유지된다. 다른 draw 로 바꾸면 이 정합이 깨진다.
- **보스 웨이브는 램프 예외**: 매 `bossWaveInterval`(5) 웨이브는 램프값이 계산된 뒤 보스+호위로 치환된다. 즉 그 인덱스의 램프 total 은 버려진다(escort 는 `bossEscortMin/Max` 독립).
- **min/max = 램프 양끝**(균등 랜덤 아님). 후반을 더 조이려면 `maxUnitsPerWave` 를 올린다.
- **`waveSeed` 고정은 테스트 버전 한정.** 테스트 기간 종료 후 0 으로 되돌리면 매판 랜덤(derived) 복귀. `ResolveWaveSeed()` 의 0→1 폴백은 프리뷰/테스트 오버로드 전용 — 라이브 0 판별은 필드 직접 확인.
- 예고선(spawn-point-alert)은 `ExpandWave` 공유라 램프된 수량·spacing 에 자동 정합(추가 작업 불요).

## Follow-up

- 난이도 램프 **옵션 2(HP/스탯 램프)** — 수량은 그대로 두고 후반 적 체력 상향(README 비목표였으나 후속 후보로 승격 가능).
- `maxUnitsPerWave` 상향으로 후반 피크 강화(밸런스 감각 확인 후).
- 테스트 기간 종료 시 `WaveA.waveSeed` 0 복귀.
