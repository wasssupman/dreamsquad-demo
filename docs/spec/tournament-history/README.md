# Tournament History — 로비 토너먼트 히스토리 + 상세 랭킹

상태: **완료 2026-07-20** — units 0~3 구현 + 컴파일(오류 0) + EditMode 1020 통과 + 코드리뷰 APPROVE + OutgameScene 씬 배선 + **실서버 e2e 확인**(로그인 상태에서 히스토리 버튼 → 패널 → `unclaimed` 실데이터 3건 로드/렌더 육안 확인, 사용자 스샷). 배선 후 버그 2건 수정(버튼 화면밖·라벨) + 패널 세로 잘림 수정. 잔여(저위험): 행 클릭 → 상세 팝업 e2e 육안(리스트 e2e·공용 LeaderboardList·기존 GetResult API 재사용으로 사실상 검증됨). 인계는 `4_handoff_summary.md`. tournament-play-report 후속 후보 "미수령 목록 조회" 승격.

## 목표

로비에서 내가 참여한 토너먼트 기록을 열람하고, 각 기록의 참가자 랭킹을 확인한다.

플로우: **로비 "히스토리" 버튼 → 히스토리 패널(내 토너먼트 목록) → 목록 행 클릭 → 해당 토너먼트 상세 랭킹 팝업**.

1. **목록** — `GET /tournament/result/entry/unclaimed` 로 내 (진행 중) 토너먼트 참가 목록을 받아 행으로 그린다. 각 행에서 `tournamentEntryId` 를 상세 조회 키로 보관.
2. **상세 랭킹** — 행 클릭 시 `GET /tournament/result/tournament/{tournamentEntryId}` 로 참가자 랭킹을 조회해 모달 팝업으로 표시. 이 API/DTO 는 tournament-play-report 에서 이미 구현됨(`TournamentApi.GetResult` + `ResultData`) — 그대로 재사용.

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_tournament_history_api.md` | `TournamentApi.GetUnclaimedEntries` + `UserTournamentResultEntry` DTO + `ResultData.name` 확장 + EditMode 파싱 테스트 |
| 1 | 리팩터 | `1_leaderboard_list_extract.md` | `ResultScreen` 의 `Row`/`BuildRows`/`CreateRow`/스프라이트 베이킹을 공용 `LeaderboardList` 로 추출, `ResultScreen` 위임 + 테스트 이관 |
| 2 | 구현+wiring | `2_history_panel_and_button.md` | `TournamentHistoryPanel`(내 토너먼트 목록 + 로딩/빈/실패 상태) + 로비 "히스토리" 버튼/패널 씬 배선 |
| 3 | 구현+wiring | `3_detail_ranking_popup.md` | `TournamentDetailPopup`(`GetResult` → `LeaderboardList` 랭킹) + 목록행 클릭 연결 + Play 검증 |

## Feature-wide 계약

- **엔드포인트**: `GET {base}/tournament/result/entry/unclaimed` (무파라미터) · `GET {base}/tournament/result/tournament/{tournamentEntryId}`(기존). base = `UserSession.GameServerBaseUrl`.
- **인증 헤더**: `Authorization: Bearer {UserSession.IdToken}` + `X-SERVICE-APP-VERSION` — 기존 `TournamentApi.Send` 패턴 그대로. 응답은 공통 envelope → `ApiEnvelope` 재사용.
- **unclaimed 응답**: `data` 는 `UserTournamentResultEntry` 의 **bare 배열**(객체 래핑 아님). 소비 필드: `tournamentEntryId`(상세 키·필수), `tournamentName`, `score`, `rank`, `createdTime`, `claimed`. `rewardData`/`userId`/`tournamentTypeId` 는 파싱하지 않는다("소비 필드만" 선례).
- **상세 응답**: 기존 `ResultData`/`ResultEntry` 재사용. 팝업 제목용으로 `ResultData.name`(토너먼트 이름) 만 추가 파싱. 랭킹 행 산식(score 내림차순, 서버 `rank>0` 우선)은 기존 `BuildRows` 그대로.
- **랭킹 렌더링 공유**: `ResultScreen` 에 private 로 묻힌 리더보드 렌더링을 공용 `LeaderboardList`(plain 클래스, `Row` 모델 + `BuildRows` 순수 + 스프라이트 베이킹 + `Render(content, rows)`)로 추출한다. 결과창과 상세 팝업이 **동일 룩**을 공유하고 중복 0 (2 호출처 = 제약 8/10 부합).
- **게스트/미로그인**: `UserSession.IdToken` 이 비면 API 호출 없이 목록 패널에 "기록 없음(로그인 필요)" 빈 상태를 표시. `IsSignedIn` 이 아니라 `IdToken` 공백으로 판정(게스트 = `idToken=""`).
- **실패/로딩**: 목록/상세 조회 실패는 게임을 막지 않는다 — 패널/팝업에 실패 문구 + 재시도 여지만. 로딩 중에는 스피너/문구, 완료 시 교체.
- **진입점**: 로비 정식 메뉴 버튼(Squad/Dreamcatcher 와 동급, dev 트레이 아님). `OutgameMenuController.RaiseExclusive(historyPanel)` 패턴에 얹는다. 상세 팝업은 히스토리 패널 위 모달.
- **스코프**: `unclaimed` 목록을 **히스토리 전체 목록**으로 사용한다. 현재 "완료된 토너먼트" 개념이 없어 `claimed` 엔드포인트(완료/cursor 페이징)는 **미사용**(사용자 결정 2026-07-20). 보상 수령(`claim`/`claimAll`)도 범위 밖.
- **ECS 경계**: 전부 MonoBehaviour 계층(Core/Api, UI). ECS 접점 없음.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/렌더 경로 변경 없음 (네트워크 클라이언트 + 로비 UI).

## 후속 후보

- **보상 수령**: `POST /tournament/claim/{tournamentEntryId}` · `POST /tournament/claimAll` + 미수령 뱃지/자산 변동 연출(`UserAssetChangeSpec`). 목록의 `claimed`/`rewardData` 필드가 토대. (완료 토너먼트 개념이 생기면 `claimed` 목록도 함께 검토.)
- **프로필 이미지**: 상세 랭킹에 `TournamentResultEntry.profileImage` 노출(현재 이름만).
- **토너먼트 상태 배지**: `GET /tournament` (`UserTournamentState.status`) 로 진행 중 매치 표시.
