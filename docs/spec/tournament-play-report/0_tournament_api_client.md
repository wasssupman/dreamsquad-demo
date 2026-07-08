# 0 — TournamentApi 클라이언트 + UserSession baseUrl

## 목적

`POST /tournament/play`, `POST /tournament/complete/{attemptId}/{score}`, `GET /tournament/result/tournament/{entryId}` 를 호출하는 정적 클라이언트를 `UserSignApi` 선례 그대로 만든다. 배틀 씬에서 base URL 을 쓸 수 있도록 sign-in 시 `UserSession` 에 보관한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs` (신설)
- `Assets/_Project/Scripts/Core/Api/UserSession.cs` — `GameServerBaseUrl` 추가
- `Assets/_Project/Scripts/UI/Outgame/LoginPanelView.cs` — `UserSession.Set` 호출부에 baseUrl 전달 (게스트 스킵 경로 포함)
- `Assets/_Project/Tests/EditMode/` — 파싱/바디 빌드 테스트 (기존 Api 테스트 파일 옆)

## 구현

- `TournamentApi.Play(string baseUrl, string idToken, Action<PlayState, string> onDone)`
  - `POST {base}/tournament/play` — **body 없음** (uploadHandler 미설정), `Authorization: Bearer`, `X-SERVICE-APP-VERSION`, timeout 10초.
  - 응답 `data` 는 `UserTournamentState` — 소비 필드만 담은 DTO `PlayState { status, tournamentEntryId, tournamentEntryAttemptId }` 로 `ApiEnvelope.Parse<T>` 바인딩.
- `TournamentApi.Complete(string baseUrl, string idToken, string attemptId, int score, string debugJson, Action<bool, string> onDone)`
  - `POST {base}/tournament/complete/{attemptId}/{score}` — body 는 `TournamentResultExtraData` 형식 `{ "debug": debugJson }`. Newtonsoft `JsonConvert.SerializeObject` 로 조립 (로그 JSON 문자열의 이스케이프를 라이브러리에 맡긴다).
  - 응답 data(`TournamentResult`) 는 소비하지 않는다 — envelope `success` 확인만 하고 성공/실패만 콜백. 랭킹은 unit 3/4 에서 별도 GET 으로 조회한다 (사용자 결정).
- `TournamentApi.GetResult(string baseUrl, string idToken, string entryId, Action<ResultData, string> onDone)`
  - `GET {base}/tournament/result/tournament/{entryId}` — body 없음, 헤더 동일.
  - 소비 필드만 담은 DTO: `ResultData { entryCount, entries: List<ResultEntry> }`, `ResultEntry { userId, userName, score, rank }`. `ApiEnvelope.Parse<T>` 바인딩.
- 에러 규약은 `UserSignApi` 와 동일: `onDone` 의 둘 중 하나만 유효, HTTP 전송 실패 시 `(HTTP: ...)` 접미.
- `UserSession.Set` 확장은 **additive 로만** (critic M3): 3번째 인자를 optional (`string baseUrl = null`) 로 붙이거나 오버로드 추가 — 기존 호출 4곳 (`LoginPanelView.cs:89,152`, `AuthE2ETest.cs:60`, `UserAuthApiTests.cs:110`) 이 무수정으로 컴파일되어야 한다. LoginPanelView 의 실 경로 2곳(신규 로그인/게스트 스킵)만 baseUrl 을 전달하도록 수정. 게스트 스킵 경로도 baseUrl 은 채운다 (호출 스킵 판정은 IdToken 기준이므로 무해).
- URL/바디 조립과 파싱은 `internal` 로 분리해 테스트 가능하게 (`BuildCompleteUrl`, `BuildCompleteBody`, `TryParsePlay` 등).

## 완료 기준

- [ ] compile 통과, 기존 테스트 무손상
- [ ] EditMode 테스트: play 응답 envelope → `PlayState` 파싱 (성공 / errorDetail / data 누락), complete URL 조립, complete body 의 `debug` 문자열 이스케이프 (따옴표·개행 포함 JSON 을 넣어 round-trip 확인), 결과 응답 → `ResultData.entries[]` 파싱 (rank/score/userName)
- [ ] 실 호출 검증은 unit 3 (wiring) 에서 수행

확인: 2026-07-08 · `c53ed605` — EditMode 테스트 통과 + 실서버 curl 프로브(sign-in→play→complete→result) 왕복 성공.
