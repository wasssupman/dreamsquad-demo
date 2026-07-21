# Force Wave Reschedule

**작업 구분**: 추가 9 (2026-07-21)

## 목적

`Next Wave` 강제 호출이 **해당 웨이브만** 앞당기고 이후 웨이브는 원래 절대 시각에 그대로
두던 동작을 고친다. 남은 웨이브 전체를 같은 양만큼 앞당겨, 강제 호출 뒤 다음 웨이브가
**"호출 시점 + 그 웨이브의 원래 간격"** 에 나오게 한다.

README 의 기존 계약(`Next Wave 연타는 남은 wave 들을 순서대로 앞당긴다`)은 이미 이 동작을
명시하고 있었다. 구현이 계약을 따르지 않은 불일치 수정이며, 새 기능이 아니다.

### 수정 전 증상

플랜이 0/10/20/30초일 때 3초에 wave 2 를 강제 호출하면 wave 2 는 3초에 나오지만
wave 3 은 여전히 20초에 나온다. 당긴 7초가 그대로 공백으로 남아, 빨리 부를수록
다음 웨이브까지의 체감 대기가 길어지는 역방향 보상이 생긴다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/WaveForceRescheduleTests.cs` (신규)

## 구현

런타임 스케줄 오프셋 `_waveTimeShift` 하나를 도입한다. 플랜의 `triggerTimeSec` 은 건드리지
않는다 — 브리핑 스트립(`WavePatternStripView`)과 로그(`BattleLogger`)가 읽는 source of
truth 이므로 불변이어야 한다.

```csharp
private float _waveTimeShift;   // 강제 호출로 앞당긴 누적 시간(앞당김이므로 음수)

private float ScheduledWaveTime(int waveIndex) =>
    _wavePlan.waves[waveIndex].triggerTimeSec + _waveTimeShift;
```

`ForceNextWave()` 에서 인덱스 증가 **전에** 오프셋을 갱신한다:

```csharp
_waveTimeShift -= ScheduledWaveTime(_nextWaveIndex) - elapsedSec;
```

오프셋이 남은 웨이브 전체에 균일하게 적용되므로 **웨이브 간 간격이 보존**된다. 결과적으로
다음 웨이브는 `호출 시점 + 원래 간격` 에 나오고, 연타하면 매 호출마다 그 시점으로 재기준된다.
간격을 상수(`waveIntervalSec`)로 재계산하지 않고 오프셋을 미는 방식이라, 웨이브마다 간격이
다른 작성 플랜(`WavePlanAsset`, PerGroupTimeline)에서도 각 웨이브 고유 간격이 그대로 유지된다.

스케줄을 읽는 세 지점이 모두 `ScheduledWaveTime()` 단일 창구를 쓴다:

- `QueueDueWaves()` — 자동 큐잉 판정 및 `QueueWave` 의 base 시각
- `TryGetSpawnAlertForecast()` — 스폰 예고선이 밀린 실제 예정 시각을 따라가야 한다
- `ForceNextWave()` — 오프셋 갱신 기준

### 리셋 (계약 9)

`_battleClock` 이 0 이 되는 모든 지점에서 `_waveTimeShift` 도 0 이어야 한다. teardown 없는
`StartBattle` 재호출에서 이전 판 오프셋이 이월되면 첫 웨이브부터 스케줄이 어긋난다.

- `TryInitializeGeneratedWaves()` — `_nextWaveIndex` 와 같은 자리 (StartBattle 경로)
- `StopBattle()` — `_battleClock = 0.0` 과 같은 자리

## 비목표

- 전투 타이머(`_timerDuration`) 는 당기지 않는다. 웨이브를 몰아 부르면 그만큼 일찍
  전멸시켜 빨리 끝나는 것이 의도된 결과다.
- 웨이브 간 간격 자체의 재밸런싱.
- `Next Wave` 버튼의 rate limit (README 대로 계속 없음).
- **조기 클리어의 시간점수 보상 조정.** 이 수정으로 "빨리 불러 빨리 끝내기"가 성립하게 되어
  터질 가능성이 있는 밸런스지만, 사용자 인지된 밸런싱 영역이라 본 unit 밖이다.
  → `docs/spec/README.md` Follow-up Backlog

## 완료 기준

- EditMode `WaveForceRescheduleTests` 5케이스 통과:
  - 강제 호출 없으면 플랜 시각 그대로 (기준선)
  - 3초에 강제 호출 → 다음 웨이브가 13초에 큐잉 (12.9초에는 아직 아님)
  - 그 다음 웨이브도 23초 — 남은 전 구간 간격 보존
  - 연타 시 매 호출 기준 재기준, 추가 웨이브 미생성
  - 플랜의 `triggerTimeSec` 은 강제 호출 후에도 불변
- 전체 EditMode 스위트 무회귀.
- Play 검증: 전투 중 `Next Wave` 를 이른 시점에 눌렀을 때 다음 웨이브가 원래 간격 뒤에
  자연스럽게 이어지고, 스폰 예고선이 그 시각에 맞춰 뜬다.
