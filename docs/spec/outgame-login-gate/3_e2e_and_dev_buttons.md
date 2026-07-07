# 3. E2E 테스트 + 개발용 버튼 그룹 (계정 리셋)

## 목적

실 엔드포인트 인증 체인의 회귀를 PlayMode E2E 로 고정하고, 로비의 개발용 버튼(시트 임포트)을 카테고리로 묶어 **계정 리셋** 버튼을 추가한다. 리셋 시 로그인 화면으로 복귀한다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/AuthE2ETest.cs` (신규) — 실 Firebase+게임서버 체인: signUp → sign-in(userId) → refresh(같은 계정) → 갱신 토큰 재 sign-in(**같은 userId** = 신원 안정성) → 세션 Set/Clear. 부작용: 실행당 익명 계정 1개 생성 (내부 dev — 허용)
- `Assets/_Project/Scripts/UI/DevOnlyGroup.cs` (신규) — dev 게이트 마이크로 컴포넌트 (`!isDebugBuild && !isEditor` → SetActive(false)). 개발용 버튼 컨테이너에 부착
- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs` — `ResetAccount()`: PlayerPrefs(refreshToken/userName) 삭제 + `UserSession.Clear()` + 입력/상태 초기화
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `OnResetAccount()`: 패널 닫기 + `loginPanel.ResetAccount()` + `ApplyAuthGate()` → 로그인 화면 복귀
- OutgameScene — `MenuButtons/DevButtons` 컨테이너 신설(+DevOnlyGroup): StatRefreshButton/StatRefreshResult reparent + `ResetAccountButton` 신규 (persistent onClick → `OnResetAccount`)

## 구현

- 리셋은 확인 다이얼로그 없음 (내부 데모). 리셋 후 재로그인하면 신규 익명 계정 (기존 계정은 고아 — 허용, README 후속 후보 참조).
- StatRefreshButtonView 의 자체 dev 게이트는 유지 (DevButtons 게이트와 중복 — 무해).
- E2E 는 네트워크 의존 — dev 서버 다운 시 실패가 정상. 스위트에서 상시 돌리기보다 인증 경로 변경 시 실행 (PlayMode 스위트 소속).

## 완료 기준

- [x] compile 오류 없음 (2026-07-07)
- [x] PlayMode `AuthE2ETest` **2회 연속 green** (실 엔드포인트 — signUp/sign-in/refresh/신원 유지/세션 리셋)
- [x] 씬 YAML: DevButtons(DevOnlyGroup)/ResetAccountButton + persistent `OnResetAccount` 배선 확인
- [x] 에디터 Play: 자동 로그인 → RESET ACCOUNT → 패널 복귀(`IsSignedIn=false`, prefs 2키 삭제) → 재로그인 정상. 콘솔 에러 0
- 참고: PlayMode 스위트에 **무관 플레이크 존재** — 2회 실행에서 실패 조합이 바뀜 (1회차 DreamcatcherEffect+ProjectileVisual, 2회차 DreamstoneCarryIn+ProjectileVisual). 전부 이 spec 과 무관한 전투/비주얼 테스트, AuthE2E 는 양쪽 다 통과. 별도 조사 후보
