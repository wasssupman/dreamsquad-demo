# 2 — result 응답 파싱 + 라이브 왕복 검증

## 목적

`GET /tournament/result/tournament/{entryId}` 응답의 `entries[].deckInfo` 를 바인딩하고, **보낸 덱이 실제로 그대로 돌아오는지** 라이브로 확인한다. 이 unit 이 spec 의 검증 질문 1·2 에 답한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs` (`ResultEntry`)
- `Assets/_Project/Tests/EditMode/Api/TournamentApiTests.cs`

## 구현

`ResultEntry` 에 `public string deckInfo;` 한 줄. `ApiEnvelope` 가 Newtonsoft 바인딩이라 그걸로 끝이고, 필드가 없는 응답(구 서버/과거 참가)은 null 로 남는다.

`TournamentDeckInfo.Deserialize` 의 소비처는 **이번 spec 에 없다.** 표시 UI 가 유일한 소비처이고 그건 후속 spec 이다 — unit 0 에서 짝으로 만들어 테스트로 계약을 고정해 두는 것까지가 이번 범위다.

## 라이브 검증 — 후속 spec 으로 이관 (2026-07-30 사용자 결정)

**이 unit 에서 실행하지 않았다.** `deckInfo` 를 그리는 화면이 아직 없어 확인 대상이 눈에 보이지 않고, 히스토리 덱 보기 페이지가 곧 그 확인 수단이 된다. 아래 절차는 **그 spec 의 첫 작업**으로 그대로 넘긴다. README "미확인" 섹션이 이 이관의 source of truth.

### 이관된 절차

1. 로그인 상태로 매치 1판 완주. 콘솔에서 `entryId` 확보 (`[TournamentReporter] play ok — ... entryId=...`).
2. `GET /tournament/result/tournament/{entryId}` 를 curl 로 조회 (읽기 전용 — 게임 흐름을 건드리지 않는다).
3. 내 엔트리의 `deckInfo` 가 방금 판에 들고 간 유닛/드림스톤/카드 id 와 일치하는지 대조.
4. 이어서 **나가기로 끝낸 판**을 한 번 더 만들고, 그 attempt 가 최고점을 갱신하지 않는 한 엔트리의 `deckInfo` 가 1의 값 그대로인지 확인 (계약 4 의 라이브 확인).

> 진단용 `POST /tournament/play` 를 직접 쏘지 않는다 — orphan 락으로 세션이 오염된다 (tournament-flow-guards unit 4 교훈). 검증은 실제 게임 플레이 + 읽기 전용 조회로만 한다.

## 완료 기준

- [x] 컴파일 통과
- [x] EditMode 테스트 통과: `deckInfo` 가 있는 엔트리는 문자열로 바인딩되고, 없는 엔트리는 null (기존 `TryParseResult_Success_BindsEntries` 픽스처 확장)
- [~] 라이브: 완주한 판의 덱이 `deckInfo` 로 돌아온다 — **후속 spec 으로 이관**
- [~] 라이브: 나가기로 끝낸 판이 앞선 덱 기록을 덮어쓰지 않는다 — **후속 spec 으로 이관**
- [x] `docs/spec/tournament-deck-info/3_handoff_summary.md` 작성

확인: 2026-07-30 EditMode 1633 tests green (관련 16건 포함). 라이브 2건은 사용자 결정으로 히스토리 덱 보기 spec 에 이관.
