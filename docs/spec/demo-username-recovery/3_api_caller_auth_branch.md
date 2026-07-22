# 3. API 호출부 헤더 seam 분기

## 목적

인증 호출이 세션 모드에 따라 `Bearer {idToken}`(firebase) 또는 `X-AUTH-USERNAME`
(username) 을 붙이게 한다. 분기는 **단일 seam**(`TournamentApi.Send`)에만 두고, 호출부는
`UserSession.Credential` 을 넘기고 게이트만 `HasAccount` 로 바꾼다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs`
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs`
- `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs`
- `Assets/_Project/Scripts/UI/Outgame/TournamentDetailPopup.cs`

## 구현

### TournamentApi

- `Send(request, string idToken, ...)` → `Send(request, AuthCredential credential, ...)`.
  내부의 하드코딩 `Authorization: Bearer` 를 `credential.Apply(request)` 로 교체
  (`X-SERVICE-APP-VERSION` 은 유지).
- 4개 public 메서드 시그니처 `string idToken` → `AuthCredential credential`:
  `Play`, `Complete`, `GetResult`, `GetUnclaimedEntries`. 파서/URL 빌더는 무변경
  (기존 EditMode 테스트가 그쪽만 커버 → 영향 없음).

### 호출부 (게이트 `IdToken` → `HasAccount`, 인자 `IdToken` → `Credential`)

- **TournamentMatchReporter**: BeginMatch·ReportResult 두 게이트를
  `if (!UserSession.HasAccount) return;` 로. async 스냅샷은 `var credential =
  UserSession.Credential;` 로 캡처(readonly struct, epoch 패턴과 동일한 값 스냅샷).
  Play/Complete/GetResult 에 credential 전달.
- **TournamentHistoryPanel.LoadEntries**: `IsNullOrEmpty(idToken)` 게이트 →
  `!HasAccount`, GetUnclaimedEntries 에 Credential.
- **TournamentDetailPopup.Show**: 게이트의 idToken 항 → `!HasAccount`
  (baseUrl/entryId 항은 유지), GetResult 에 Credential.

## 완료 기준

- [ ] compile 성공 (`read_console` clean)
- [ ] 전체 EditMode 회귀 없음 (파서/빌더 테스트 그대로 통과)
- [ ] (라이브, 선택) username 모드에서 `/tournament/*` 가 `X-AUTH-USERNAME` 를
  존중하는지는 미검증 — 서버 데모 특성상 실패해도 firebase 경로 무영향. README "미검증" 참조.

## 주의

- 이 unit 이 완성돼야 username-복구 계정이 실제로 토너먼트 리포트/조회를 한다. 단
  게이트 술어(`HasAccount`)를 봐도, ResultScreen 의 pending-slot 술어와
  OutgameMenu 히스토리 버튼은 **unit 4** 에서 별도로 옮긴다.

---

완료 2026-07-22 — compile clean, EditMode 1175 passed / 0 failed (회귀 없음).
