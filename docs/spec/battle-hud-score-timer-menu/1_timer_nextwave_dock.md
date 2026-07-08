# 1 — NextWaveDock: 타이머 통합 + BattleBridge 밖 추출

## 목적

우하단 NextWave 버튼을 `BattleBridge`(ECS 게이트웨이) 밖의 새 MonoBehaviour View `NextWaveDock` 으로 추출하고, 그 위에 **남은 시간 행**을 통합한다. `TimerDisplay` 의 중앙 표시는 은퇴한다. BattleBridge 는 웨이브 상태를 읽기 전용으로만 노출한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/NextWaveDock.cs` (`Wassup.UI`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — NextWave UI 빌드 로직 제거 + 읽기 전용 getter 추가
- `Assets/_Project/Scripts/UI/TimerDisplay.cs` — 중앙 표시 은퇴
- `BattleScene.unity` — `NextWaveDock` 호스트 GameObject 배선(bridge 주입), 기존 `TimerDisplay` 처리

## 구현

### BattleBridge — UI 제거, 상태 노출
- 제거: `EnsureNextWaveButton()`, `SetNextWaveButtonVisible()`, `RefreshNextWaveButton()`, 필드 `_nextWaveButton`, `_nextWaveLabel`. 이들을 호출하던 지점(약 375/850/891/1065/1209/1217/1228/2620/2641/2660)에서 UI 호출 삭제.
- 유지: `_nextWaveIndex`, `ForceNextWave()`(public), `QueueWave` 로직, `TimerRemaining`(public).
- 추가 public 읽기 전용 getter (View 가 폴링):
  - `bool NextWaveAvailable` → `_running && _usingGeneratedWaves && _wavePlan.waves != null`
  - `bool NextWaveHasNext` → `NextWaveAvailable && _nextWaveIndex < _wavePlan.waves.Count`
  - `int NextWaveNumber` → `_nextWaveIndex + 1`
- **ECS 경계**: getter 는 순수 읽기, 구조 변경/EntityManager 접근 없음 — 게이트웨이 규칙 준수.

### NextWaveDock (신규)
- 자체 ScreenSpaceOverlay Canvas(order 7), CanvasScaler 1920x1080, `UiLayer.Apply`.
- 우하단 컨테이너(기존 위치 계승: anchor 1/0, pivot 1/0, pos -40/40). 세로 2단:
  - **상단 행 — 타이머**: `bridge.TimerRemaining` 을 `{min}:{sec:D2}` 로 매 프레임 갱신, <30초 빨강. (TimerDisplay 의 포맷/색 로직 이관.)
  - **하단 행 — NEXT WAVE 버튼**: `bridge.NextWaveHasNext` 면 `NEXT WAVE {bridge.NextWaveNumber}` 활성, 아니면 `NO WAVES` 비활성. onClick → `bridge.ForceNextWave()`.
- **항상 표시**(배틀 중): 컨테이너는 전투 진입~종료 동안 표시. 타이머 행은 웨이브 상태와 무관하게 유지, 하단 버튼만 `NextWaveAvailable`/`HasNext` 로 활성/라벨 전환. 전투 밖(draft/result)에서는 숨김 — `GameManager.PhaseChanged`(Battle) 또는 `PlacementRequested` 구독으로 on/off (ScoreHudView/TimerDisplay 의 기존 표시 트리거와 일치시킬 것).
- SerializeField: `bridge`(BattleBridge). Timer 색상/폰트 등은 인스펙터 노출 최소.

### TimerDisplay 은퇴
- 중앙 타이머 표시를 제거. 두 방법 중 택1(구현자 판단, 씬 배선 최소 파손 우선):
  - (a) `TimerDisplay.cs` 삭제 + 씬의 `TimerDisplay` GameObject 제거, 또는
  - (b) 표시 로직만 비활성(패널 미생성)하고 GameObject 는 남김.
- 어느 쪽이든 **중앙 상단에 남은 시간이 더 이상 그려지지 않아야** 함. draftController/PlacementRequested 구독이 다른 곳에서 의존되지 않는지 확인 후 제거.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러 없음(BattleBridge UI 제거 후 미참조 심볼 없음).
- [ ] 중앙 상단에 남은 시간 미표시. 우하단 Dock 에 `2:57` + `NEXT WAVE {n}` 2단 표시.
- [ ] 남은 시간이 전투 내내 표시(웨이브 소진해 `NO WAVES` 여도 타이머 유지), <30초 빨강.
- [ ] NEXT WAVE 클릭 시 조기 소환(`ForceNextWave`) 동작, 소진 시 `NO WAVES` 비활성.
- [ ] 메뉴 팝업(작업 2) 정지 시 타이머 동결 확인은 작업 2에서.

> 확인: 2026-07-08 사용자 Play 확인 통과 (작업 0 과 묶어 검증). 우하단 dock 타이머 카운트다운 + NEXT WAVE 버튼 동작, 중앙 타이머 제거, BattleBridge UI 추출·getter 노출.
