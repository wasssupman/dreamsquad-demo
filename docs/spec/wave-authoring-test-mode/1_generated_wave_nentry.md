# 1 — 런타임 모델 N-entry 일반화

## 목적

`GeneratedWave` 의 2타입 고정(`unitA/B`)을 **N개 `(unit,count)` 그룹**으로 일반화한다. seed 경로는 정확히 2-entry 로 생성되어 스폰 순서·시간·lane·summary 가 byte-identical 로 유지된다. 작성 플랜(unit 3)이 같은 런타임 모델을 채울 수 있게 토대를 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs` — `WaveSpawnGroup` 추가, `GeneratedWave` 를 `groups` 기반으로.
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `ExpandWave` round-robin 일반화, `FormatSummary` groups 순회. `Generate` 는 2-entry 편의 생성자 사용(동작 무변경).
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` — `AddWaveCard` 2줄 고정 → groups N줄 가변.
- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs` — `WaveRecord.unitA/B/countA/B` → `List<WaveEntryRecord> entries`.
- `Assets/_Project/Scripts/Logging/BattleLogger.cs` — `SetWavePattern` entries 기록.
- `Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs` — groups 기반 어서션 + 결정론 회귀 테스트.

## 구현

```csharp
public readonly struct WaveSpawnGroup { public readonly AttackUnitData unit; public readonly int count; ... }

public readonly struct GeneratedWave
{
    public readonly int waveIndex;
    public readonly float triggerTimeSec;
    public readonly IReadOnlyList<WaveSpawnGroup> groups;
    public readonly int totalCount;            // Σ count
    public GeneratedWave(int idx, float t, IReadOnlyList<WaveSpawnGroup> groups) { ... }
    // 편의: seed 경로/테스트용 2-entry
    public GeneratedWave(int idx, float t, AttackUnitData a, int ca, AttackUnitData b, int cb)
        : this(idx, t, new[]{ new WaveSpawnGroup(a,ca), new WaveSpawnGroup(b,cb) }) {}
}
```

- `ExpandWave`: round-robin — `for round in 0..maxCount: for g in groups: if round<count[g] emit`. 2그룹이면 기존 `A,B,A,B...` 인터리브와 동일. `unit==null` 그룹은 emit 스킵.
- `FormatSummary`: `"Wave {n} - {name} {count}, ..."`. 2그룹이면 기존 포맷 문자열과 byte-identical.
- 브리핑 카드: groups 수에 따라 세로 분할, 폰트 N 따라 축소.

## 완료 기준

- 컴파일 0 에러.
- EditMode 테스트 green. **결정론 회귀**: 같은 seed 2회 생성→전 웨이브 `ExpandWave` 결과(unit ref/triggerTime/spawnIndex) 완전 일치 + 기존 인터리브 테스트(`a2,b4→A,B,A,B,B,B`) + `FormatSummary` 동일 유지.
- seed 웨이브는 정확히 2 그룹(불변 잠금 테스트). N그룹(예: 3) round-robin 펼침 테스트 통과.
- 로그 `WaveRecord` 가 entries[] 로 직렬화(포맷 변경 의도됨).
- 브리핑 UI 가 N줄 표시(시각 검증은 unit 3·6 Play 에서, 여기선 컴파일+테스트).

---

*완료 확인*: 2026-06-16 — 컴파일 0, EditMode 325 pass/0 fail. 결정론 회귀(같은 seed → 펼친 SpawnEntry unit/시각/lane 완전 일치), seed=2그룹 불변, N>2 round-robin, 기존 인터리브·summary 테스트 green. 커밋 `__PENDING__`.
