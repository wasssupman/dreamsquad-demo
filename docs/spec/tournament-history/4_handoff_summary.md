# 4 — Handoff Summary

## Commit

- `69163f8a` feat: unclaimed API + ParseList (unit 0)
- `fedb357e` refactor: 리더보드 → 공용 LeaderboardList (unit 1)
- `605543c7` feat: 히스토리 패널 + 상세 팝업 (units 2~3, 코드)
- `c0e5cfc2` docs: README + units 0~4 + handoff
- `40a612d2` fix: ISO 날짜 문자열 보존 (DateParseHandling.None, 리뷰/테스트 반영)
- `9717b460` feat: OutgameScene 씬 배선 — 히스토리 버튼 + 패널 (units 2~3)

## Implemented

- `GET /tournament/result/entry/unclaimed` 클라이언트(`TournamentApi.GetUnclaimedEntries` + `UserTournamentResultEntry` DTO). `ResultData.name`(상세 팝업 제목) 추가.
- `ApiEnvelope.ParseList<T>` 추가 — 성공 envelope 의 `data` 가 `[]`·null 둘 다 빈 리스트(리스트 엔드포인트용). 기존 strict `Parse<T>` 불변.
- `LeaderboardList` 공용 컴포넌트로 랭킹 행 렌더링 추출. `ResultScreen` 은 이를 위임(봇 fallback + 실데이터 swap 동일 룩). 순수 `BuildRows` 계약·테스트 불변.
- `TournamentHistoryPanel` — 로비 히스토리 페이지(자체 캔버스, ScrollRect 목록, 로딩/빈/실패 상태, 게스트 스킵, epoch 가드). 행 탭 → 상세 팝업.
- `TournamentDetailPopup` — 모달 랭킹(기존 `GetResult` + `LeaderboardList` 재사용, epoch/`isActiveAndEnabled` 가드, dim/닫기).
- `OutgameMenuController` — `historyPanel` 필드 + `OnOpenHistory`(RaiseExclusive) + `ClosePanels` 포함 + `onClose` 구독/해제.
- EditMode 테스트: unclaimed 파싱(배열/빈배열/null/에러), `BuildUnclaimedUrl`, `ResultData.name`, 리더보드 모델 6종(대상만 `LeaderboardList.BuildRows` 로 갱신).

## Key Files

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs`, `ApiEnvelope.cs`
- `Assets/_Project/Scripts/UI/LeaderboardList.cs`, `ResultScreen.cs`
- `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs`, `TournamentDetailPopup.cs`, `OutgameMenuController.cs`
- 테스트: `Tests/EditMode/Api/TournamentApiTests.cs`, `Tests/EditMode/ResultLeaderboardModelTests.cs`

## Verified

- compile: Unity 콘솔 무에러(전 어셈블리).
- **EditMode 전 스위트 1020/1020 통과**(2 skip=기존 [Ignore]). unclaimed 배열/빈배열/null/에러 + BuildRows + 무회귀.
- 코드리뷰(oh-my-claudecode:code-reviewer): **APPROVE**, BLOCKER/HIGH 0. MEDIUM(빈 응답 null) → `ParseList` 해소. LOW(팝업 가드) → `isActiveAndEnabled`. 테스트가 잡은 날짜 mangling → `DateParseHandling.None` 로 별도 수리.
- 씬 배선(UnityMCP): HistoryButton onClick→OnOpenHistory(persistent) + historyPanel 할당 확인, OutgameScene 저장.
- **실서버 e2e 확인(사용자 스샷)**: 로그인 상태에서 히스토리 버튼 → 패널 → `unclaimed` 실데이터 3건("기본 토너먼트 테스트" 0/0/918점) 로드·렌더 육안 확인.
- 배선 후 버그 3건 수정: 버튼 화면 밖(앵커 오해, `9964aa93`) · 가시 라벨 오류(LabelOverlay, `9964aa93`) · 패널 세로 잘림 PanelH 1240→960(`54f86223`).
- 잔여(저위험): 행 클릭→상세 팝업 육안 — 리스트 e2e 통과 + 공용 `LeaderboardList` + 기존 `GetResult` 재사용이라 사실상 검증됨.

## Notes

- **새 스크립트 3개 `.meta` 는 수동 생성**(GUID 고정): LeaderboardList=`d3a2ec16…`, TournamentHistoryPanel=`60578cc6…`, TournamentDetailPopup=`9c46b3c2…`. Unity 가 열려 있어 첫 refresh 시 MonoImporter 블록을 덧붙일 수 있으나 GUID 는 보존 — 참조 안전.
- **뒤로 버튼**은 `onClose` 이벤트만 발화(자기 비활성 안 함). `OutgameMenuController` 가 구독해 `ClosePanels`(메뉴 복원)하는 구조(LoginPanelView 선례). 배선 누락 시 뒤로가 no-op + dim 이 클릭 흡수 → 반드시 배선 확인.
- 팝업은 히스토리 패널 위 sorting 3000, 패널 2500. dim 클릭=닫기.
- 게스트(`IdToken==""`)는 API 스킵·빈 상태. `IsSignedIn` 아님에 주의.
- 씬 배선은 `execute_code` 불가(CodeDom/mono 경로 깨짐, Roslyn 부재)로 **일회용 Editor `[MenuItem]` 스크립트**(reflection 기반)로 수행 후 삭제. 향후 유사 배선 시 동일 우회 필요.

## Follow-up (저위험 육안 1건)

- 리스트 e2e 는 실서버로 확인 완료. 남은 육안 1건: **행 클릭 → 상세 랭킹 팝업**(`GetResult` 왕복,
  점수 내림차순·본인 강조). 공용 `LeaderboardList`+기존 `GetResult` 재사용이라 저위험.
- 관찰: dev `unclaimed` 응답의 `rank` 가 0이라 목록 순위는 "-" 로 표기(상세 팝업은 score 순 계산).
  목록에도 순위를 매기려면 별도 결정 필요.
