# 1 — 호출부 배선 (직접 LoadScene → SceneTransition.Go)

## 목적

프로덕션의 `SceneManager.LoadScene` 직접 호출 3곳을 유일 진입점 `SceneTransition.Go` 로 리다이렉트한다. 이후 모든 씬 전환은 연출 파이프라인을 경유한다(계약 #1).

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs:76` — `OnStartGame()`
- `Assets/_Project/Scripts/UI/Outgame/TestModePanelView.cs` (LoadScene 호출 라인)
- `Assets/_Project/Scripts/UI/MenuPopup.cs:86` — `OnExit()`

## 구현

- 각 호출부: `SceneManager.LoadScene(SceneNames.X)` → static `SceneTransition.Go(SceneNames.X)`. (null-guard/degrade 는 단위 0 의 static `Go` 내부에 이미 있음 — 호출부는 신경 안 씀.)
- `using UnityEngine.SceneManagement;` 가 씬 전환 목적으로만 남았다면 제거(다른 용도 있으면 유지). `SceneNames` 는 그대로.
- `MenuPopup.OnExit()`: 기존 `_pauseLease.Dispose()` 등 teardown 순서 유지 — pause 해제 후 `Go` 호출. `Go` 가 async 라 씬 파괴 타이밍이 바뀌므로, dispose 는 반드시 `Go` **이전**에.

## 완료 기준

- compile clean.
- 프로덕션 코드에 `SceneManager.LoadScene(` 직접 호출 0건(grep). 테스트(`Tests/PlayMode/`)의 `LoadSceneAsync` 는 스코프 밖.
- Play 검증: START → 페이드 후 Battle 진입, 일시정지 메뉴 나가기 → 페이드 후 Outgame 복귀, 테스트모드 웨이브 선택 → 페이드 후 Battle. 3 경로 모두 연출 경유.
- 나가기 시 pause 가 정상 해제되고 씬이 교체됨(전투 잔류 상태 없음).

확인: 2026-07-10 — 3 호출부 Go 경유 배선, 프로덕션 직접 LoadScene 0건, PlayMode 2/2 passed(SceneTransition + Outgame 라운드트립 회귀 없음), 사용자 Play 에서 START 페이드 확인.
