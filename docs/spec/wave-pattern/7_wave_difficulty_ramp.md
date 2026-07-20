# 7. 웨이브 수량 램프 — 진행할수록 몬스터 증가

> rev 2026-07-20. wave-pattern 1차(0~5) + 고정 시드(6) 이후 추가 작업 단위.

## 목적

seed 생성 경로에서 웨이브 총 마릿수(`total`)를 **웨이브 진행에 따라 선형 증가**시킨다.
기존에는 매 웨이브 `[minUnits,maxUnits]` 균등 랜덤이라 후반 난이도 상승이 없었다
(README 비목표 "난이도 곡선"의 최소 버전 = 수량 램프만 도입, HP/스탯은 그대로).

**min/max 재해석**: `minUnitsPerWave` = 첫 웨이브 목표, `maxUnitsPerWave` = 마지막 웨이브
목표. 선형 보간값에 `±waveCountJitter` 정수 지터를 더한 뒤 `[min,max]` 클램프.
현재 `WaveA.asset` 값(6→10)이 그대로 램프 양끝이 된다(평균 8 불변, 초반↓·후반↑ 재분배).

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `waveCountJitter`(int, 기본 1) 필드 추가
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 순수 함수 `RampedWaveTotal` 추가 + 생성 루프에서 `rng.NextInt(min,max+1)` → `RampedWaveTotal(i, waveCount, ..., rng.NextFloat())` 로 교체 + `Generate` 오버로드에 `waveCountJitter` 스레딩
- `Assets/_Project/Scripts/Data/Decks/WaveA.asset` — `waveCountJitter: 1` 명시
- `Assets/_Project/Tests/EditMode/WaveCountRampTests.cs` — 신규(순수 함수 검증)

## 구현

순수 함수 (아키텍처 무관, jitter 는 plain 값 입력 — 제약 10):

```csharp
public static int RampedWaveTotal(int waveIndex, int waveCount,
    int minUnits, int maxUnits, int jitterBand, float jitter01)
// t = waveCount>1 ? waveIndex/(waveCount-1) : 1
// center = lerp(min, max, t);  jitter = (jitter01*2-1)*jitterBand
// return clamp(round(center + jitter), min, max)
```

생성 루프(`Generate`) 교체 — **rng 소비 수 불변**(`NextInt`→`NextFloat` 둘 다 1스텝):

```csharp
float jitter01 = rng.NextFloat();
int total = RampedWaveTotal(i, waveCount, minUnits, maxUnits, waveCountJitter, jitter01);
int countA = rng.NextInt(1, total);   // 이후 A/B 분배·보스 후처리 무변경
```

`waveCountJitter` 는 big `Generate(...)` 오버로드 **마지막 파라미터(기본 1)** 로 추가 —
기존 테스트/호출부 positional 인자를 깨지 않는다. deck 오버로드가 `deck.waveCountJitter` 전달.

## 완료 기준

- (EditMode) `RampedWaveTotal`: jitter=0 일 때 wave 0=min, 마지막=max, i 증가에 **비감소(monotonic non-decreasing)**.
- (EditMode) 임의 jitter01∈[0,1)·jitterBand 에서 결과가 항상 `[min,max]`.
- (EditMode) `waveCount==1` graceful(=max), `min>max` 방어 스왑.
- 기존 `WavePatternGeneratorTests`·`WavePatternGeneratorBossTests`·`WaveSpawnForecastTests` 그린 유지(범위/결정론 불변식이라 램프 후에도 통과).
- (Play) 고정 시드에서 후반 웨이브가 초반보다 마릿수가 많다(요약 로그 육안).
- 예고선(spawn-point-alert)은 `ExpandWave` 공유라 램프된 수량에도 자동 정합.

**확인 2026-07-20** — 사용자 Play 확인(후반 웨이브 마릿수 증가) + EditMode 1022 통과
(실패 0, 스킵 2 = 기존 known-ignore). `WaveCountRampTests` 6건 신규 그린.
