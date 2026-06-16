# 2 — 작성→런타임 변환기 + Bridge 경로 + endless

## 목적

`WavePlanAsset` 을 런타임 `GeneratedWavePlan`(N-entry)으로 변환하고, `BattleBridge` 가 작성 플랜이 주입되면 seed 생성 대신 그것을 소비하게 한다. 작성 플랜의 `timerDurationSec=0` 은 endless(타임아웃 승리 비활성, 전멸 시 승리)로 연결한다. seed(라이브) 경로는 무변경.

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `FromPlanAsset(WavePlanAsset)` 추가.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_authoredPlan` 필드 + `SetAuthoredWavePlan` + `TryInitializeGeneratedWaves` 작성 분기 + `_timerDuration` 작성 모드 분기 + 로그.
- `Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs` — `FromPlanAsset` 변환 테스트.

## 구현

### 변환기 (`WavePatternGenerator.FromPlanAsset`)
- 각 `AuthoredWave` → `GeneratedWave(i, triggerTimeSec, groups)`. `unit==null || count<=0` 그룹은 필터.
- `GeneratedWavePlan(seed=0, version=0, timerDurationSec=plan.timerDurationSec, waveIntervalSec=0, intraWaveSpacingSec=plan.intraWaveSpacingSec, waves)`. seed=0/version=0 = 비-seed(작성) 마커. waveIntervalSec 는 작성 모드 비적용.

### Bridge
- `private WavePlanAsset _authoredPlan;` + `private bool _usingAuthoredPlan;`
- `public void SetAuthoredWavePlan(WavePlanAsset plan)` — null 주면 seed 복귀. GameManager 테스트 분기(unit 3)가 StartBattle 전에 호출.
- `TryInitializeGeneratedWaves`: `_authoredPlan != null` 이면 `FromPlanAsset` 으로 `_wavePlan` 구성 + `SetWavePattern` + `_usingAuthoredPlan=true`. 변환 실패 시 seed 경로로 fall-through. 그 외 기존 seed 경로 그대로.
- `_timerDuration` (StartBattle): `_usingAuthoredPlan ? _wavePlan.timerDurationSec : deck.timerDurationSec`. → 작성 endless(0)면 `CheckTimer` early-return, `CheckVictory`(전 웨이브 dispatch + 전멸)로만 종료. **seed 경로는 deck.timerDurationSec 그대로(무위험).**
- 리셋(BeginPlacement)에서 `_usingAuthoredPlan=false`. `_authoredPlan` 은 유지(매치 단위, GameManager 가 set).

## 완료 기준

- 컴파일 0 에러, EditMode green(기존 + 변환 테스트).
- `FromPlanAsset`: groups/totalCount/triggerTime/timerDuration 매핑 + null·0 그룹 필터 테스트 통과.
- seed 경로 회귀 0(기존 wave 테스트 유지).
- Play 통합(작성 플랜 endless 스폰/종료)은 unit 3·4 진입 후 검증 — 여기선 변환+배선 컴파일/단위까지.

---

*완료 확인*: 2026-06-16 — 컴파일 0, EditMode 326 pass/0 fail. FromPlanAsset 매핑·null/0 필터 테스트 통과, seed 회귀 0. Play 통합은 unit 4. 커밋 `__PENDING__`.
