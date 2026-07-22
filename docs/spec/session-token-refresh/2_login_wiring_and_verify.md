# Unit 2 — 로그인 wiring + 라이브 검증

## 목적

firebase 로그인 성공 시 `refreshToken` + `firebaseApiKey` 를 세션에 심어, unit 0 의 `TryRefreshBearer` 가 실제로 재발급할 수 있게 한다. 이게 없으면 RefreshToken 이 null 이라 자가치유가 no-op.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs`

## 구현

- `SignInToGameServer` 의 `UserSession.Set(user, tokens.idToken, gameApiBaseUrl)` →
  `UserSession.Set(user, tokens.idToken, gameApiBaseUrl, refreshToken: tokens.refreshToken, firebaseApiKey: firebaseApiKey)`.
- **이 한 곳만** 변경한다. Start()의 silent 재로그인과 OnLoginClicked 수동 로그인 **둘 다** 이 메서드를 거치므로 한 줄로 커버.
- guest(`OnSkipClicked`) / username(`AdoptExistingUser`) 의 `Set` 은 변경 없음(만료 없는 세션 — refresh 소스 null 유지).

## 완료 기준

- 컴파일 오류 0.
- **인증 거부 계약 실측 완료(2026-07-22 curl)**: 무효/누락 Bearer → `HTTP 403` + `HANDLE_ACCESS_DENIED/C006`. 트리거는 이 403 을 문다.
- **라이브 검증** (실서버, firebase 계정 로그인 상태 Play):
  1. 정상: 로그인 → 히스토리 열기 → `unclaimed` 목록 정상 로드(회귀 없음).
  2. 강제 무효화 재현: Play 중 `UserSession.IdToken` 을 reflection 으로 무효값("garbage")으로 덮음(execute_code — 인메모리 배선 검증 기법). RefreshToken/FirebaseApiKey 는 유효 유지.
  3. 히스토리 재오픈 → 첫 요청 **403** → `TryRefreshBearer` 재발급 → **재시도 성공으로 목록 정상 로드**. 콘솔에 재발급 로그 1회 + 최종 성공.
  4. 재발급 실패 재현(옵션): RefreshToken 도 garbage 로 덮음 → 재발급 실패 → 기존 "기록 조회에 실패했습니다." 문구(현행 유지 확인).
- 검증 후 README 상태 라인 "완료 YYYY-MM-DD" + 커밋 해시 기재, `3_handoff_summary.md` 작성.

## 주의

- 검증은 에디터 포커스 필요(비포커스 시 프레임 정지 — `project_unitymcp_play_needs_focus`).
- reflection 으로 세션 상태를 덮는 인메모리 기법은 씬 저장 없이(사용자 WIP 보존) 수행.

완료: 2026-07-22 — 배선 1줄. 라이브 검증 완료: 인메모리 firebase 세션(익명가입→sign/in) → idToken garbage 무효화 → 히스토리 API → **403→refresh→retry→200, idToken 21→834, healed=True**. curl 로 서버측 403/refresh/재수용 체인 별도 실측. (실측 중 서버 `user_name` unique 제약 발견 — 무관.)
