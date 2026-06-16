# 6 — 웨이브 상대 타이밍 모델 정정 (rev)

## 목적

웨이브 타이밍 모델 정정. 기존: 각 웨이브가 **전체 타임라인 절대 시각** 하나(triggerTimeSec)를 갖고 그룹은 (unit,count)만. 정정: **각 웨이브 = N초 구간(durationSec)**, 스폰을 **웨이브 상대 시각 0~N** 로 그룹마다 배치. 웨이브는 순차로 이어붙음(웨이브 i 절대 시작 = 앞 웨이브 durationSec 합).

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePlanAsset.cs` — `AuthoredWave{durationSec, intervalSec, groups}`, `AuthoredSpawnGroup{triggerTimeSec(0~N), unit, count}`. plan-level `intraWaveSpacingSec` 제거.
- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs` — `WaveExpandMode{RoundRobin, PerGroupTimeline}`, `WaveSpawnGroup.triggerOffsetSec`, `GeneratedWave.spawnIntervalSec`/`expandMode`.
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `ExpandWave` expandMode 분기, `FromPlanAsset` 누적 시작 + per-group.
- `Assets/_Project/Scripts/Data/WavePlans/WavePlan_Sample.asset` — 신 구조로 재작성.
- `Assets/_Project/Tests/EditMode/WavePatternGeneratorTests.cs` — FromPlanAsset/PerGroupTimeline 테스트.

## 모델

```
AuthoredWave   { float durationSec; float intervalSec; List<AuthoredSpawnGroup> groups }
AuthoredSpawnGroup { float triggerTimeSec; AttackUnitData unit; int count }   // triggerTimeSec ∈ [0, durationSec]
```
- 웨이브 i 절대 시작 `S_i = Σ durationSec[0..i-1]`.
- 그룹 g 의 k번째(0..count-1) 스폰 절대 시각 = `S_i + g.triggerTimeSec + k * wave.intervalSec`. `intervalSec=0` → 동시.

## 결정론 보존

- seed 경로는 `WaveExpandMode.RoundRobin` 그대로(2-entry, intraWaveSpacing 인터리브). `ExpandWave` 는 expandMode 로 분기 — RoundRobin 분기 로직 무변경. 작성 경로만 `PerGroupTimeline`.
- 기존 결정론 회귀 + 인터리브 + summary 테스트 그대로 green 유지.

## 완료 기준

- 컴파일 0, EditMode green(seed 회귀 + 신규 PerGroupTimeline/FromPlanAsset 테스트).
- FromPlanAsset: 누적 웨이브 시작 + 그룹 offset + spawnIntervalSec + expandMode 매핑.
- ExpandWave PerGroupTimeline: 그룹 absolute 시각 = S+offset+k·interval, interval=0 동시.
- Play: 작성 플랜이 웨이브 상대 시각대로 스폰, endless 유지.

---

*완료 확인*: 2026-06-16 — 컴파일 0, EditMode 329개 중 327 pass/0 fail(seed 결정론 유지 + 신규 FromPlanAsset 누적시작/PerGroupTimeline 테스트). 샘플 변환 실측: 웨이브 절대 시작=누적 durationSec(0,12,…,60,74,86), W0 Basic@0~2+Swift@4~6, W5(@60) interval 0.5 적용. Play: usingAuthored/timer=0(endless), wave0 pending 시각 [0..6]. 커밋 `8695cd2`.
