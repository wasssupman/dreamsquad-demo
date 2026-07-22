# 4. 게이트 술어 마이그레이션 (IdToken → HasAccount)

## 목적

"실계정이냐"를 `IdToken` 비어있지 않음으로 보던 남은 두 게이트를 `HasAccount` 로 옮긴다.
이걸 안 하면 username-복구 계정이 idToken 이 없어 **게스트로 오인**돼 히스토리 버튼이
사라지고, 결과화면 pending-slot 이 terminal(5)로 잘못 잡힌다.

## 변경 대상

- `Assets/_Project/Scripts/UI/ResultScreen.cs`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`

## 구현

### ResultScreen.PendingSlotCount

`string.IsNullOrEmpty(UserSession.IdToken) ? TerminalPendingSlots : AwaitingPendingSlots`
→ `!UserSession.HasAccount ? TerminalPendingSlots : AwaitingPendingSlots`.

load-bearing 주석(`:251`) 갱신: 이제 TournamentMatchReporter 가 `HasAccount` 로 게이트
(unit 3). `IsSignedIn` 금지 경고는 **유지** — 게스트는 여전히 `IsSignedIn=true`이나
`HasAccount=false`. 경고의 의도(게스트 배제)는 그대로, 대상 술어만 HasAccount 로.

### OutgameMenuController — 히스토리 버튼

`historyBtn.SetActive(signedIn && !string.IsNullOrEmpty(UserSession.IdToken))`
→ `historyBtn.SetActive(UserSession.HasAccount)`.

`HasAccount` 는 `Current != null` 을 함의(토큰/이름은 Set 시 user 와 함께만 채워짐)하므로
`signedIn &&` 는 흡수된다. 게스트 carve-out 의도 유지 — 게스트는 HasAccount=false 라
버튼 숨김.

## 완료 기준

- [ ] compile 성공 (`read_console` clean)
- [ ] 전체 EditMode 회귀 없음
- [ ] 사용자 Play 확인(feature 종료 시 묶어서): username-복구 로그인 후 히스토리 버튼
  노출 + 결과화면 pending-slot 10칸. 게스트는 버튼 숨김·5칸 유지(회귀 없음).

## 주의

- 이 unit 이후 남는 `IdToken` 직접 참조는 (a) `UserSession` 내부 정의, (b) 헤더
  seam(`AuthCredential`), (c) LoginPanelView 의 firebase 경로뿐 — 실계정 게이트 용도의
  `IdToken` 참조는 전부 `HasAccount` 로 이관 완료.

---

완료 2026-07-22 — compile clean, EditMode 1175 passed / 0 failed. 라이브 e2e:
username 모드 로그인 후 히스토리/토너먼트 게이트 통과 확인(README 라이브 검증).
