# Logging Tests Validation

**작업 구분**: Phase 4

## 목적

seed 기반 wave pattern 이 재현 가능하고, UI/런타임/로그가 같은 wave plan 을 가리키는지 검증한다.

## 로그

`BattleLogSchema` 에 wave pattern 정보를 추가한다.

```csharp
public WavePatternRecord wavePattern;

public class WavePatternRecord
{
    public int seed;
    public int generatorVersion;
    public float waveIntervalSec;
    public int waveCount;
    public List<WaveRecord> waves;
}
```

권장 event:

```text
wave_forced { waveIndex, elapsedSec, forced=true }
wave_started { waveIndex, elapsedSec, forced }
```

강제 호출 시 `wave_forced` 를 먼저 기록하고, 같은 wave 에 대해 `wave_started.forced=true` 를 이어서 기록한다. 자동 호출은 `wave_started.forced=false` 만 기록한다.

## EditMode 테스트

- same seed + same pool -> same wave summary
- different seed -> different wave summary 가능
- waveCount 는 10~15
- 각 wave totalCount 는 10~15
- 각 wave 는 서로 다른 2종 unit
- `waveIntervalSec = timerDurationSec / waveCount` 규칙을 따른다
- 모든 `triggerTimeSec` 는 0 이상, timerDurationSec 미만 범위에 들어온다
- wave expansion 은 A/B deterministic interleave 를 따른다

## Play 검증

1. 공격 패턴 확인 UI 에 10~15개 wave row 가 표시.
2. Draft start.
3. Battle 시작.
4. Wave 1 즉시 스폰.
5. 예정된 wave interval 후 Wave 2 자동 스폰.
6. `Next Wave` 클릭 시 다음 wave 즉시 스폰.
7. 버튼 연타 시 wave 가 순서대로 앞당겨지고 중복 없음.
8. 마지막 wave 이후 버튼 disabled.

## 완료 기준

- Unity compile 0 errors.
- EditMode wave generator tests pass.
- Play smoke console error/warning 0.
- briefing 에 표시된 wave summary 와 runtime spawn 구성이 일치.
- 로그에 seed, generatorVersion, wave summary 가 기록된다.
