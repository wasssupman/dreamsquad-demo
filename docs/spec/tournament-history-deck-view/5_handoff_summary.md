# 5 — handoff summary

## Commit

- `125b02a1` — feat: 히스토리 1뎁스 2컬럼 재설계 (unit 0)
- `fa82b1aa` — feat: 덱보기 + DeckInfo 팝업 (units 1~3)
- `42812708` — feat: 씬 배선 + Play e2e (unit 4)
- `aee7ad45` — feat: 내 덱에서는 프리셋 적용 버튼을 숨긴다
- 스펙 문서는 `8e865919` 로 먼저 푸시됨 (구현보다 앞서 올림)

## Implemented

- 로비 히스토리가 **1뎁스 2컬럼**이다. 좌 = 내 토너먼트 목록(최신순, 일시 병기), 우 = 선택된 토너먼트 랭킹. `TournamentDetailPopup` 은퇴.
- 진입 시 가장 최근 토너먼트 자동 포커스. 다른 행을 고르면 **우측만** 갈아끼워진다(목록/랭킹이 각자 epoch·상태를 가짐).
- 랭킹 행마다 **덱보기** 버튼(옵트인). `대기 중...` 슬롯 제외, 덱 정보 유무와 무관하게 노출.
- **DeckInfo 팝업** — 스쿼드(유닛·드림스톤) / 드림캐쳐(카드) 탭, 좌 상세 + 우 가변 그리드. 순수 프레젠테이션(페이로드 + 표시명만 받는다).
- 어떤 데이터가 와도 그린다: null 페이로드 · 빈 배열 · 미해석 id(raw id 로 **남긴다**) · 카탈로그 null · 개수 초과.
- "프리셋 적용" 버튼은 자리만 잡고 비활성(기능은 별도 spec). **내 덱에서는 통째로 숨기고** 목록이 그 자리까지 내려온다 — 내 덱을 내 프로필에 다시 쓰는 건 no-op 이라, 비활성으로 남겨두면 "왜 나만 안 되나"로 읽힌다.
- OutgameScene 에 카탈로그 3종 배선.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs` — 2컬럼, 정렬/일시 파싱, 팝업 생성·주입
- `Assets/_Project/Scripts/UI/Outgame/DeckInfoPopup.cs` — 모달 뷰
- `Assets/_Project/Scripts/UI/Outgame/DeckInfoDisplay.cs` — id → 표시 항목 순수 변환
- `Assets/_Project/Scripts/UI/LeaderboardList.cs` — `Row.DeckInfo`, `Render(..., onDeckView)`
- 테스트: `TournamentHistoryPanelSortTests` / `DeckInfoDisplayTests` / `DeckInfoPopupTests`

## Verified

- EditMode **1659 tests green** (이 spec 신규 25건). 실패 1건(`DirectionalVolleyIntegrationTests.AuthoredDefenderPatterns_*`)은 병행 세션의 샷건너 에셋 WIP — 무관.
- 실서버 Play e2e (계정 `wassup`): 히스토리 22건 최신순, 랭킹 5슬롯, 내 덱보기 → 스쿼드 11셀 / 드림캐쳐 10셀 전부 해석. 콘솔 에러 0.
- 코드 리뷰 1회(별도 레인) — blocking 1건 포함 6건 반영.
- 프리셋 버튼 숨김 Play 확인: 내 덱 → 버튼 없음 + 목록이 그 자리까지 확장, 남의 덱 → 버튼 노출. 같은 화면에서 **`deckInfo` 없는 참가자의 "덱 정보가 없습니다"** 도 실데이터로 확인(계약 10 null-payload 경로).
- **`tournament-deck-info` 의 미확인 1번(왕복 성립) 닫힘.** 2번(0점 덮어쓰기)은 **여전히 미확인**.

## Notes (되돌리지 말 것)

- **`createdTime` 은 epoch 밀리초 문자열이다.** swagger 의 `format: date-time` 은 틀렸다(실측 `"1785419835370"`). ISO 만 파싱하던 시절엔 날짜가 **항상 비어 있었고** 정렬도 무효였다. 파서를 ISO 전용으로 되돌리지 말 것.
- **정렬과 표시가 같은 파서(`TryParseCreated`)를 공유한다.** 갈라지면 "화면엔 날짜가 있는데 정렬은 맨 뒤"가 된다.
- **파싱 실패 행을 버리지 말 것.** 표시 전용 필드 하나 때문에 참가 기록이 목록에서 사라지면 그 토너먼트는 영영 열 수 없다.
- **중첩 캔버스는 자기 rect 를 먼저 펴야 한다**(`StretchFull(transform)`). 안 펴면 dim 이 화면을 못 덮어 암전이 사라지고 뒤 페이지로 클릭이 통과한다.
- **`overrideSorting` 은 활성화 뒤에 다시 박아야 한다.** 비활성 상태에서 세팅하면 `SetActive(true)` 시 풀린다(Play 실측).
- **미해석 id 를 버리지 말 것.** 버리면 7명 스쿼드가 5명으로 보이고, 그게 그 사람 덱인지 내 카탈로그가 뒤처진 건지 화면에서 구분되지 않는다.
- **선택은 (섹션, 항목) 인덱스 기준.** 같은 카드 2장·같은 스톤 4개가 설계상 허용이라 id 로 잡으면 슬롯을 잃는다.
- `DreamcatcherDeckStrip` 을 재사용하지 말 것 — 고정 슬롯이라 남의 덱이 조용히 잘린다.

## Follow-up

- ~~**0점 마감이 덱 기록을 덮어쓰는지**~~ — **성립하지 않는 질문이었다 (2026-07-31).** `entryId` 하나로 플레이하는 판은 한 판이라 0점 마감이 마감하는 것은 자기 엔트리이고, 덮어써서 잃을 다른 기록이 없다. 서버 최고점 가드도 불필요. 대신 0점으로 끝난 판도 덱이 남도록 `tournament-deck-info` unit 4 가 세 마감 경로 모두에 덱을 실었다 — 근거는 그 README 미확인 #2.
- 빈 목록 안내는 라이브로 못 봤다(실계정에 22건). EditMode 로만 고정돼 있다.
- 후속 후보 전체는 `docs/spec/README.md` → Follow-up Backlog → **토너먼트 덱 정보** 그룹으로 이관했다.
