# 5. Handoff Summary — demo-username-recovery

재설치 후 같은 닉네임으로 로그인하면 서버에 중복 계정이 생기던 문제를, "발급 직전
`GET /user` 로 존재 조회 → 있으면 `X-AUTH-USERNAME` 로 채택" 하도록 고친 feature.
데모 전용 우회로. 2026-07-22 완료 · 라이브 e2e 검증.

## Commit

- `6e1590fc` unit 0 — UserSession 인증 모드 + AuthCredential
- `c14a660e` unit 1 — UserLookupApi (GET /user 닉네임 조회)
- `46ac504a` unit 2 — LoginPanelView 발급 직전 조회 + adopt
- `23b77872` unit 3 — API 호출부 헤더 seam 분기
- `2439e2fe` unit 4 — 게이트 술어 IdToken → HasAccount

## Implemented

- 두 인증 모드: firebase(`Bearer idToken`) / demo-username(`X-AUTH-USERNAME`). `UserSession`
  이 어느 모드인지 보유(`AuthUserName`), 요청 인증은 `AuthCredential` 값으로 캡슐화.
- `HasAccount` = `IdToken` OR `AuthUserName` — 실계정 술어. 게스트는 `IsSignedIn=true`이나
  `HasAccount=false`.
- `GET /user` 3분기(`Classify`): success→Found / body 동반 실패(403)→NotFound / body 없음
  →NetworkError. **네트워크 실패로는 절대 발급 안 함**(중복 재발 차단).
- `LoginPanelView`: 발급 직전 `MintOrAdopt` — Found→adopt, NotFound→firebase 발급,
  Network→정지. `Start()` 는 토큰/UsernameMode 표식으로 firebase·username silent 재진입.
- 헤더 부착은 `TournamentApi.Send` 단일 seam(`credential.Apply`). 호출부는 `Credential`
  전달 + 게이트 `HasAccount`.
- ResultScreen pending-slot · OutgameMenu 히스토리 버튼 게이트를 `HasAccount` 로 이관.

## Key Files

- `Assets/_Project/Scripts/Core/Api/AuthCredential.cs` — 인증 크리덴셜 값 + Apply
- `Assets/_Project/Scripts/Core/Api/UserSession.cs` — 모드 상태 + HasAccount + Credential
- `Assets/_Project/Scripts/Core/Api/UserLookupApi.cs` — GET /user 조회 + Classify
- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs` — MintOrAdopt / AdoptExistingUser / Start
- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs` — Send 헤더 seam

## Verified

- compile clean. EditMode 1175 passed / 0 failed (신규 10 테스트: UserSession 모드·
  AuthCredential.Apply·UserLookupApi.Classify). 스킵 2는 기존 무관.
- 라이브 e2e(Play): "wassup" 로그인 → adopt, `Current.userId` 가 기존 서버 계정
  `019f8019-…` 와 동일(중복 없음). username 모드에서 `/tournament/play·complete·result`
  3개 전부 `X-AUTH-USERNAME` 로 성공.

## Notes (되돌리면 안 되는 것)

- **네트워크 실패 시 발급 금지**: `Classify` 의 "body 없음→NetworkError→정지" 를 유지.
  이걸 무너뜨리면 블립마다 중복 계정 생성 = 이 feature 가 고친 그 버그.
- **헤더는 단일 seam**: `TournamentApi.Send` 밖에서 Bearer/헤더 분기 금지. 호출부는
  `UserSession.Credential` 만 넘긴다(한 곳 누락 = 조용히 다른 계정에 기록).
- **모드 배타**: firebase 성공·ResetAccount·Adopt 에서 `UsernameMode` 표식/refresh 토큰을
  서로 지운다. 둘 다 세팅되는 상태를 만들지 말 것.
- **`UserSignApi.SignIn` 은 firebase Bearer 전용** — username 모드에서 호출 안 됨. 손대지 말 것.
- 검증 중 dev 서버 wassup 계정에 테스트 점수 4321 제출됨(리더보드 잔존).

## Follow-up

- **근본 해결(username 모드 은퇴)**: 익명 → 구글/애플 IDP 연동(`/user/sign/link`), 또는
  서버측 custom token. 재설치를 넘어 살아남는 정식 계정 복구.
- 기존 중복 "wassup" 계정(이미 서버에 2개)은 코드로 못 푼다 — `X-AUTH-USERNAME` 가 어느
  쪽을 가리킬지 서버 구현 의존. 서버 정리 필요.
- `GET /user` 는 없는유저 vs body-동반 서버오류(500/MAINTENANCE)를 errorCode 없이 구분
  못 함 → 후자도 NotFound. 필요 시 errorCode 화이트리스트로 좁히기.
- firebase 모드 idToken 만료(1h) 시 refresh 후 재시도 — 본 feature 와 독립된 갭.
