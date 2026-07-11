# 4 — Aspect·Safe Area 검증 게이트

## 목적

Height 전환을 코드 완료로 끝내지 않고, 화면 상태·비율·실기 safe area 조합을 고정 QA 세트로 검증한다. 선행: units 0~3.

## 변경 대상

- `Assets/_Project/Tests/EditMode/UiSafeAreaMathTests.cs`
- 필요 시 신규 `Assets/_Project/Tests/PlayMode/UiSafeAreaLayoutSmokeTest.cs`
- 검증 캡처: `Assets/Screenshots/` 또는 handoff에 기록할 최종 증거 경로

## 구현

- Game View 기준 1920×1080(16:9), 2160×1080(18:9), 2340×1080(19.5:9), 2400×1080(20:9)을 확인한다.
- Battle 상태는 Draft/Placement/Battle/Hand-open/Result, Outgame은 로그인/로비/스쿼드 준비를 캡처한다.
- 순수 테스트는 left/right notch와 bottom gesture inset을 모두 포함한다.
- 실기에서는 landscape 양 방향 회전, gesture navigation과 3-button navigation을 확인한다.
- PlayMode smoke는 phase 전환 후 critical panel이 safe root 하위이며 active 상태/raycast가 정상임을 최소 1경로 검증한다.

## 완료 기준

- [ ] EditMode/PlayMode 대상 테스트 통과, 기존 테스트 회귀 없음.
- [ ] 16:9 기준 캡처와 시각 회귀 없음.
- [ ] 19.5:9·2:1·20:9에서 확대/상하 클립/edge 이탈 없음.
- [ ] Android 실기 landscape 양 방향에서 cutout/gesture 침범 없음.
- [ ] 콘솔 에러 0, 테스트 결과와 캡처 경로를 handoff에 기록.
- [ ] 사용자 확인 후 README 완료 처리 및 handoff 작성.
