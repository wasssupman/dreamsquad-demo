# 1 — 생성기 고정 간격 지원

## 목적

웨이브 간격을 웨이브수 의존(`duration/waveCount`)이 아니라 **고정값**으로 뽑을 수 있게 한다.
**스케줄러·트리거타임 계약은 불변** — `triggerTimeSec = i*interval` 형식 그대로, `interval` 만 달라짐.

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs`
- `Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs` (또는 신규 테스트 파일)

## 구현

1. `Generate(AttackDeck, seedOverride)` 오버로드가 `deck.fixedWaveIntervalSec` 를 코어로 전달.
2. `Generate(...)` 코어 시그니처에 `float fixedIntervalSec = 0f` 파라미터 추가(끝에, 기본 0).
3. 간격 계산부(현재 `interval = duration/waveCount` at 라인 78):
   ```csharp
   float interval = fixedIntervalSec > 0f ? fixedIntervalSec
                    : (waveCount > 0 ? duration / waveCount : 0f);
   ```
   나머지(`triggerTimeSec = i*interval`, rng 소비 순서)는 **불변**.
4. `GeneratedWavePlan.waveIntervalSec` 도 이 `interval` 로 채워짐(기존과 동일 경로).
5. 웨이브 개수: 엔드리스 덱은 `minWaveCount==maxWaveCount==30` 이라 기존 랜덤 로직이 30 을 고정
   산출 — 개수 로직 손대지 않는다.

## 완료 기준

- **EditMode 테스트 (신규)**: `fixedIntervalSec=10`, waveCount=30 → 모든 `triggerTime[i] == i*10`
  (마지막 웨이브 = 290s, 타이머 밖은 unit 2/6 스케줄러가 자연 컷).
- **회귀**: `fixedIntervalSec=0` 이면 기존 `duration/waveCount` 동작 불변 — 기존 생성기 테스트 전부 green.
- rng 소비 순서 불변 확인(기존 결정론 테스트 green).
