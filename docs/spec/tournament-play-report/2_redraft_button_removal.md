# 2 — REDRAFT 버튼 제거

## 목적

결과 팝업의 재시작 경로를 RESTART 하나로 단순화한다 (사용자 결정 2026-07-08). 판 시작 = play API 1회 규칙을 흐리는 분기(REDRAFT)를 UI 와 핸들러에서 함께 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/ResultScreen.cs` — `redraftButton` 필드/생성/리스너, `RedraftRequested` 이벤트, `OnRedraftClicked` 제거
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `RedraftRequested` 구독/해제 (Start / OnDestroy), `OnRedraftRequested` 핸들러 제거

## 구현

- ResultScreen: 버튼 행(`Buttons`)에는 RESTART 만 남는다. 레이아웃은 HorizontalLayoutGroup 이 자동 처리 — 추가 조정 불필요.
- BattleBridge: `OnRedraftRequested` 가 이 이벤트의 유일한 소비자임을 삭제 전에 재확인 (2026-07-08 grep 기준 ResultScreen 이벤트 외 호출자 없음). 핸들러 내부에서만 쓰이던 redraft 전용 로직(드래프트 재오픈, `StartReplacementSession("redraft", ...)`)이 다른 경로에서 참조되지 않으면 함께 제거.
- `RestartBattle()` (BattleBridge:334) 는 `OnRedraftRequested` 의 fallback(:308) 이 유일한 호출자 (2026-07-08 grep) — 핸들러 제거로 호출자가 0 이 되면 **같은 커밋에서 함께 제거**. 이후 살아있는 재시작 경로는 `OnRestartRequested` 하나가 된다 (unit 3 의 배선 전제).
- 드래프트 자체(첫 진입 시 드래프트 모드)는 이 작업과 무관 — 건드리지 않는다.

## 완료 기준

- [ ] compile 통과, `RedraftRequested`/`OnRedraftRequested`/`RestartBattle` 참조 0건
- [ ] 에디터 Play: 결과 팝업에 RESTART 버튼만 표시, RESTART 정상 동작 (새 판 시작)

확인: 2026-07-08 · `c53ed605` — 컴파일 클린, `RedraftRequested`/`OnRedraftRequested`/`RestartBattle` 참조 0건.
