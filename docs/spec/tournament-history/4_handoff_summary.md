# 4 — Handoff Summary

## Commit

- (커밋 후 해시 기입) `feat/refactor(tournament-history): units 0~3` — API + LeaderboardList 추출 + 히스토리 패널 + 상세 랭킹 팝업.

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

- compile: `dotnet build Wassup.Tests.EditMode.csproj` 오류 0 (경고 13, 기존).
- 코드리뷰(oh-my-claudecode:code-reviewer): **APPROVE**, BLOCKER/HIGH 0. MEDIUM(빈 응답 null 형태) → `ParseList` 로 해소. LOW(팝업 가드 parity) → `isActiveAndEnabled` 로 수정. 나머지 LOW 2건은 accepted 패턴(아래 Notes).
- Play/Test Runner 실행: **미실행**(이 세션 UnityMCP 부재 + 에디터 락으로 배치모드 불가).

## Notes

- **새 스크립트 3개 `.meta` 는 수동 생성**(GUID 고정): LeaderboardList=`d3a2ec16…`, TournamentHistoryPanel=`60578cc6…`, TournamentDetailPopup=`9c46b3c2…`. Unity 가 열려 있어 첫 refresh 시 MonoImporter 블록을 덧붙일 수 있으나 GUID 는 보존 — 참조 안전.
- **뒤로 버튼**은 `onClose` 이벤트만 발화(자기 비활성 안 함). `OutgameMenuController` 가 구독해 `ClosePanels`(메뉴 복원)하는 구조(LoginPanelView 선례). 배선 누락 시 뒤로가 no-op + dim 이 클릭 흡수 → 반드시 배선 확인.
- 팝업은 히스토리 패널 위 sorting 3000, 패널 2500. dim 클릭=닫기.
- 게스트(`IdToken==""`)는 API 스킵·빈 상태. `IsSignedIn` 아님에 주의.
- `Wassup.Runtime.csproj` 에 새 3파일 `<Compile Include>` 를 검증용으로 임시 추가(gitignore 대상, 커밋 안 함). Unity refresh 시 재생성됨.

## Follow-up (씬 배선 + Play — 다음 세션 필수)

1. `OutgameScene` 로비 메뉴에 "히스토리" Button 추가 → `OnClick` = `OutgameMenuController.OnOpenHistory`.
2. HistoryPanel GameObject 생성(+`TournamentHistoryPanel` 컴포넌트) → `OutgameMenuController.historyPanel` 필드 할당, 초기 비활성.
3. Play: 로그인 상태에서 히스토리 열기 → 목록 로드 로그 확인(실서버 `unclaimed` 왕복, 빈 응답 형태 `[]`/null 실측) → 행 클릭 → 상세 랭킹(`GetResult` 왕복) → 닫기/뒤로 복원. 결과창 리더보드 시각 무회귀도 확인.
4. Unity Test Runner EditMode: `TournamentApiTests`, `ResultLeaderboardModelTests` 그린 확인.
