# battle-hud-score-timer-menu

> 상태: 초안 (작업 대기) — 2026-07-08 작성

## 상위 목표

인게임 배틀 HUD 의 가독성을 높이고, 메뉴 버튼을 팝업화한다. 검증 질문 두 개:

1. **"점수가 연출될 때 맵 가독성을 해치지 않고, 점수가 화면의 주인공으로 읽히는가?"**
   — 남은 시간을 점수 위에서 걷어내고 우하단 NextWave 위젯으로 이관, 점수는 중앙 상단을 크게 차지.
2. **"메뉴 버튼이 즉시 이탈이 아니라, 공격 패턴을 확인하고 재개/이탈을 고를 수 있는 정지형 팝업인가?"**
   — 메뉴 → 일시정지 팝업(공격 패턴 + 나가기/재개). 배틀 중 "!" 온디맨드 토글은 이 팝업으로 일원화.

## 배경 (현행 구조)

- `TimerDisplay.cs` — 상단 중앙 자체 Canvas(order 6), `bridge.TimerRemaining` 매 프레임 표시, <30초 빨강.
- `ScoreHudView.cs` — 상단 중앙 자체 Canvas(order 6), `topOffset -76`(타이머 바로 아래). 점수 연출(PunchScale/골드 플래시/버스트/마일스톤).
- NextWave 버튼 — **`BattleBridge.cs` 안에서 직접 생성**(order 7, 우하단, "NEXT WAVE {n}"/"NO WAVES", 클릭 시 `ForceNextWave()`). 표시 조건 `_running && _usingGeneratedWaves && _wavePlan.waves != null`.
- `ReturnToMenuButton.cs` — 좌상단 버튼(order 1000), 클릭 시 즉시 `SceneManager.LoadScene(SceneNames.Outgame)`.
- `WavePatternStripView.cs` — incoming waves 스트립. `FadeIn()/Roll()/SnapHidden()/SetToggleEnabled()` API + 우측 상단 "!" `WaveToggle` 버튼. `SquadPrepView` 가 배틀 준비 시 `SnapHidden()+SetToggleEnabled(true)` 로 온디맨드 열람 세팅.
- 일시정지 선례 — `DreamcatcherController`: `TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100)` → lease.Dispose() 로 해제. Battle 도메인 0 이면 `BattleBridge._battleClock` 도 멈춰 남은 시간 동결.

## feature-wide 계약

- **타이머 소유권 이전**: 남은 시간 표시는 새 `NextWaveDock` (MonoBehaviour View) 이 소유한다. `TimerDisplay` 의 중앙 표시는 은퇴. 소스는 계속 `bridge.TimerRemaining`.
- **BattleBridge 는 UI 를 만들지 않는다**: NextWave 버튼 UI 빌드 로직을 `BattleBridge` 밖으로 추출한다. BattleBridge 는 웨이브 상태를 **읽기 전용 getter** 로만 노출하고(`ForceNextWave()` 는 유지), UI 는 `NextWaveDock` 이 조립한다. (ECS 게이트웨이가 View 를 빌드하던 기존 냄새 제거.)
- **점수는 주인공**: `ScoreHudView` 는 중앙 상단을 크게 차지(`topOffset`/폰트 상향). 연출은 그대로 유지하되 위치만 상향.
- **메뉴 팝업 = 정지형**: 열림 시 `TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100)`, 재개 시 lease.Dispose(). 나가기 시 씬 전환(lease 는 씬 teardown + `TimeManager.ResetAll` 경계에서 정리).
- **공격패턴 열람 일원화**: 배틀 중 "!" `WaveToggle` 제거. 팝업이 `WavePatternStripView.FadeIn()`(열기)/`Roll()`(재개 시) 을 구동. `SquadPrepView` 의 `SetToggleEnabled(true)` 호출은 정리.
- **Time.timeScale 금지 유지**: 모든 정지는 TimeManager 경유. `Time.timeScale` 은 1 고정.
- **연출은 unscaledTime 유지**: 점수 롤/스파크는 이미 `Time.unscaledDeltaTime` 기반 — 팝업 정지 중에도 마지막 점수 롤이 안착하는 현행 동작 보존.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 리팩터/UI | `0_score_center_prominence.md` | ScoreHudView 중앙 상단 강조(타이머 제거 전제 위치/크기) |
| 1 | 리팩터/추출 | `1_timer_nextwave_dock.md` | NextWave 버튼 BattleBridge 밖 추출 + 타이머 행 통합(`NextWaveDock`), TimerDisplay 중앙 은퇴 |
| 2 | 신규 UI | `2_menu_popup.md` | 메뉴 팝업 컨트롤러(오픈/pause/나가기/재개) + ReturnToMenuButton 배선 변경 |
| 3 | 통합/제거 | `3_wavestrip_in_popup.md` | 팝업에서 WavePatternStripView 구동 + "!" 토글 제거 + SquadPrepView 정리 |
| 4 | handoff | `4_handoff_summary.md` | 종료/인계 요약 (구현 후 작성) |

## 파이프라인 커버리지

N/A — 이 spec 은 MonoBehaviour View(HUD/UI) 계층만 변경한다. 새 플레이 오브젝트(유닛/적/투사체/해저드/VFX)나 생성→렌더 경로 신설/변경 없음. `docs/reference/object-pipeline-map.md` 대조 대상 아님.

## 후속 후보 (현 spec 범위 밖)

- 상단 통합 바(edge-to-edge 띠) 형태로의 재디자인 — 이번엔 "점수 중앙 크게 + 타이머 우하단" 으로 결정, 띠 형태는 보류.
- NextWave 위젯의 다음 웨이브 카운트다운(초) 표기.
- 메뉴 팝업에 설정/사운드 토글 등 추가 항목.
