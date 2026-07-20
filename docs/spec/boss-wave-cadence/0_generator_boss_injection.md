# 0 — 생성기 보스 편성 주입

## 목적

`WavePatternGenerator.Generate`(seed 경로)가 매 `bossWaveInterval`번째 웨이브를 **보스×1 + 잡몹×[min,max]**로
치환하도록 한다. 순수 데이터 로직 → EditMode 단위 테스트로 회귀 고정.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackDeck.cs` — 보스 필드 추가
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `Generate` 오버로드에 보스 optional 파라미터 + pool 제외 + 치환 로직
- `Assets/_Project/Tests/EditMode/WavePatternGeneratorBossTests.cs` — 신규 테스트
- (기존 `WavePatternGeneratorTests.cs`는 optional 파라미터 덕에 **무수정 컴파일** — 변경 대상 아님. 회귀만 확인.)

## 구현

**AttackDeck** — `[Header("Boss Waves")]` 아래:
```csharp
public AttackUnitData bossUnit;        // null → 보스 웨이브 없음
public int bossWaveInterval = 5;       // 매 N번째 웨이브가 보스 웨이브
public int bossEscortMin = 3;
public int bossEscortMax = 4;
```
`waveGeneratorVersion` 기본값은 코드에서 건드리지 않는다(덱 asset 에서 1→2, unit 3).

**WavePatternGenerator.Generate**(전체 인자 오버로드)에 보스 파라미터를 **optional(기본값)** 로 추가 —
`AttackUnitData bossUnit = null, int bossWaveInterval = 0, int bossEscortMin = 0, int bossEscortMax = 0`.
→ 기존 9-인자 호출부(특히 `WavePatternGeneratorTests.cs`의 헬퍼)가 **수정 없이 컴파일**되고 보스 미주입(회귀 안전).
덱 오버로드(`Generate(deck, seed)`)는 `deck.bossUnit / deck.bossWaveInterval / deck.bossEscortMin / deck.bossEscortMax`를 전달.

**pool 방어 제외**: `BuildDistinctPool`(또는 그 직후)에서 `bossUnit`을 pool 에서 제외한다. `attackUnitPool`에
`bossUnit`이 실수로 들어있으면 **경고 로그** 후 제외. 없으면 no-op → 비-보스 웨이브 불변. 이 boss-free `pool`을
main loop 와 escort 선택이 공유 → 비-보스 웨이브 보스 오발화·escort 보스 중복이 구조적으로 불가능.

치환 로직 — **기존 랜덤 웨이브 루프가 끝난 뒤** 후처리(비-보스 웨이브 rng 소비를 현행과 동일하게 유지):
```
if (bossUnit != null && bossWaveInterval > 0)
  for i in [0, waveCount):
    if ((i + 1) % bossWaveInterval == 0):
      escortMin' = max(1, min(bossEscortMin, bossEscortMax))
      escortMax' = max(escortMin', max(bossEscortMin, bossEscortMax))
      escortCount = rng.NextInt(escortMin', escortMax' + 1)
      escortType  = pool[rng.NextInt(0, pool.Count)]   // pool 은 boss-free (위 제외)
      groups = { new WaveSpawnGroup(bossUnit, 1), new WaveSpawnGroup(escortType, escortCount) }
      waves[i] = new GeneratedWave(i, i * interval, groups, 0f, WaveExpandMode.RoundRobin)
```
- 보스 그룹을 **맨 앞**에 두어 RoundRobin round 0 에서 보스가 먼저 스폰(`triggerTimeSec ≈ 웨이브 시작`).
- `spawnIntervalSec`는 RoundRobin 에서 무시되므로 `0f`(2-entry 편의 생성자와 일관 — dead 값 혼란 방지).
- 잡몹은 boss-free `pool`에서 rng 로 1타입 선택(다타입 호위는 후속).
- 후처리라 비-보스 웨이브는 현행 생성기와 byte-identical(같은 seed).

## 완료 기준

- 컴파일 통과.
- EditMode `WavePatternGeneratorBossTests` green (저수준 오버로드 직접 호출, in-memory `AttackUnitData`):
  - **편성**: interval=5, waveCount 고정(예 12) → 웨이브 idx 4·9 는 보스 유닛 정확히 1 + 잡몹 count ∈ [min,max];
    그 외 idx 는 보스 없음.
  - **선봉**: 보스 웨이브의 groups[0].unit == bossUnit.
  - **graceful**: `bossUnit == null` → 어떤 웨이브도 보스 없음.
  - **결정론**: 같은 seed 두 번 → 동일 plan(웨이브 수·그룹·count 일치).
  - **불변식(핵심)**: 같은 seed 로 boss-ON / boss-OFF 두 plan 생성 → 보스 웨이브 인덱스를 제외한 **모든 웨이브의
    groups·count 가 동등**(비-보스 웨이브 == version 1). 후처리 rng 를 실수로 앞당기면 이 테스트가 잡는다.
  - **pool 방어**: `bossUnit`을 `attackUnitPool`에 넣어도 (a) 비-보스 웨이브에 보스 없음, (b) escort 에 보스 없음
    (보스 2기 방지) — boss-free pool 강제 확인.
- 기존 `WavePatternGenerator` 테스트 회귀 통과(비-보스 웨이브 불변).
