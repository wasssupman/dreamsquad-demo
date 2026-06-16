# 5 — Handoff Summary (wave-authoring-test-mode)

## Commit

- `ea8cb35` 0 WavePlanAsset 저작 모델 + 샘플 플랜
- `5a1e8fb` 1 GeneratedWave N-entry 일반화 (2타입 → N그룹)
- `123a7ee` 2 작성 플랜 변환기 + BattleBridge 작성-플랜 경로 + endless
- `aaf97ed` 3 테스트 모드 진입(TestModeConfig/Context + GameManager 분기)
- `1e01eb2` 4 아웃게임 TEST MODE 버튼 + 플랜 피커 + 저장 스쿼드 반입
- 문서 해시 기재: `c5dbb09`(0) · `ced8fa0`(1) · `2121125`(2) · `786d704`(3) · 본 커밋(4+handoff)

## Implemented

- 에디터에서 `WavePlanAsset`(웨이브당 N개 (적,수량) 그룹 + triggerTimeSec, `timerDurationSec=0`=endless) 직접 작성. 샘플 `WavePlan_Sample.asset`(8웨이브, N>2 포함).
- `GeneratedWave` 2타입 고정 → `WaveSpawnGroup[]` 일반화. seed 경로는 2-entry 편의 생성자로 byte-identical(결정론 회귀 테스트 잠금). 소비처(생성기/스케줄러/브리핑 카드 N줄/로그 entries[]) 전부 전환.
- `WavePatternGenerator.FromPlanAsset` 변환기 + `BattleBridge.SetAuthoredWavePlan` + `TryInitializeGeneratedWaves` 작성 분기(우선, 실패 시 seed fallback).
- endless: 작성 모드 `_timerDuration=plan.timerDurationSec`(0이면 `CheckTimer` 비활성) → `CheckVictory`(전 웨이브 dispatch + 전멸)로만 종료. seed/legacy `deck.timerDurationSec` 무변경.
- 아웃게임 TEST MODE 버튼 → `TestModePanelView`(자체 빌드, 영어 UI) 플랜 피커 → 선택 시 `TestModeContext.Set` + BattleScene 로드.
- `GameManager` 최상위 테스트 분기 `StartTestModeMatch` — 드래프트 스킵, **디펜더는 기존 저장 스쿼드 그대로**(`ResolveSquadDefenders`; 스쿼드 비면 `TestModeConfig.defenderPreset` 폴백), 작성 플랜 + 스킬 roll, placement 진입.

## Key Files

- `Assets/_Project/Scripts/Data/WavePlanAsset.cs` · `WavePatternGenerator.cs`(FromPlanAsset/ExpandWave/FormatSummary) · `GeneratedWavePlan.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`(SetAuthoredWavePlan, TryInitializeGeneratedWaves, `_timerDuration` 분기)
- `Assets/_Project/Scripts/Core/GameManager.cs`(StartTestModeMatch, ResolveSquadDefenders) · `TestModeContext.cs` · `Data/TestModeConfig.cs`
- `Assets/_Project/Scripts/UI/Outgame/TestModePanelView.cs` · `OutgameMenuController.cs`
- 에셋: `Data/WavePlans/WavePlan_Sample.asset` · `Data/Config/TestModeConfig.asset` · `Scenes/OutgameScene.unity`
- 테스트: `Tests/EditMode/WavePatternGeneratorTests.cs`

## Verified

- 컴파일 0. EditMode 328개 중 326 pass/0 fail(2 skip 기존 무관). 결정론 회귀 + N그룹 round-robin + FromPlanAsset 매핑/필터 테스트 통과.
- Play 전체 체인(OutgameScene): TEST MODE → 영어 피커 → "Sample Test Plan(8 waves)" → BattleScene 진입(Context 소비, phase=Placement, 드래프트 스킵), 디펜더=저장 스쿼드 7유닛, StartBattle 시 `_usingAuthoredPlan`/`_timerDuration=0`(endless)/waves=8, wave0 스폰·`_resultShown=False`.

## Notes

- **seed(라이브) 경로 불변**: GeneratedWave 일반화는 2-entry round-robin = 기존 A,B 인터리브. timerDuration 분기는 `_usingAuthoredPlan` 일 때만. 회귀 테스트로 잠금.
- **로그 포맷 변경(의도)**: `WaveRecord.unitA/B/countA/B` → `entries[]`. 디버그 로그라 마이그레이션 부담 없음.
- 디펜더 프리셋(TestModeConfig.defenderPreset, 현재 4종)은 **저장 스쿼드가 비었을 때만** 쓰이는 폴백.
- Play 중 콘솔 "missing script (Unknown)" 1건은 BattleScene 기존 항목 — 본 작업 무관(OutgameScene 미발생 확인).
- endless 타이머 HUD 표시(`TimerRemaining` 0 반환)는 후속(README 후속 후보).

## Follow-up

- endless 모드 타이머 HUD 표시 정리.
- iso/rect 타일 아트, 그룹별 개별 오프셋 초, 엘리트/보스 웨이브 — README 비목표/후속 후보.
- 작성 플랜 밸런싱·난이도 곡선, import/export, 런타임 작성 UI.
