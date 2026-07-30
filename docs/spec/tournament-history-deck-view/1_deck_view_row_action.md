# 2 — 랭킹 행 덱보기 진입점

## 목적

랭킹 행에서 그 참가자의 덱을 여는 버튼을 만든다. `LeaderboardList` 는 배틀 결과 화면과 **공유**되므로, 버튼은 히스토리에서만 켜지는 **옵트인**이어야 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/LeaderboardList.cs`
- `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs` (버튼 켜기 + 팝업 열기)
- `Assets/_Project/Tests/EditMode/` — `BuildRows` 테스트가 있는 곳에 필드 적재 케이스 추가

## 구현

**`Row` 확장** — 덱보기가 필요로 하는 두 가지를 행에 싣는다.

- `DeckInfo` (string) — 그 참가자의 원문 페이로드. `ResultEntry.deckInfo` 를 그대로 옮긴다(파싱하지 않는다 — 파싱은 팝업 호출 직전에).
- 표시명은 이미 `Name` 에 있다. 별도 userId 는 **싣지 않는다** — 팝업이 필요로 하는 건 이름과 덱뿐이고, 식별자를 UI 모델에 끌고 들어가면 소비처가 늘어난다.

`BuildRows` 는 `대기 중...` 슬롯에 `DeckInfo = null` 을 넣는다.

**행 액션** — `Render` 에 옵션을 하나 받는다(예: `Render(content, rows, onDeckView)`). `onDeckView` 가 null 이면 버튼을 만들지 않는다(= 결과 화면 현행 유지). null 이 아니면 `IsWaiting` 이 아닌 행에만 버튼을 붙이고, 클릭 시 그 행을 콜백으로 넘긴다.

**버튼 노출 규칙** — 실제 참가자 행에는 **덱 정보 유무와 무관하게** 노출한다. 없으면 팝업이 "덱 정보가 없습니다"를 말한다. 버튼이 행마다 있다 없다 하면 눌러도 되는지 매번 판단해야 한다.

**레이아웃 주의** — 현재 행은 이름이 `offsetMax = -150f` 로 점수 컬럼을 피하고 있다. 버튼이 들어가면 그 여백을 다시 나눠야 한다. 버튼이 없는 경우(결과 화면)의 여백은 **지금 값 그대로 유지**한다 — 공유 컴포넌트라 결과 화면이 조용히 틀어지면 안 된다.

**팝업 호출** — 히스토리 패널이 `TournamentDeckInfo.Deserialize(row.DeckInfo)` 로 파싱해 팝업에 넘긴다. 파싱 실패/빈 값은 `null` 이고, 그대로 넘기면 팝업이 "없음"을 그린다(계약 9).

## 완료 기준

확인: 2026-07-30 EditMode green. 리뷰 반영 — 버튼/이름 컬럼을 점수 컬럼 실폭(`ScoreColW = 162`)에서 파생시켰다. 예전엔 이름 inset(150)을 버튼 기준으로 재사용해 버튼이 점수 rect 를 6px 물었고, 점수가 7자리가 되면 겹쳤다.

- [x] 컴파일 통과
- [ ] 결과 화면(`ResultScreen`)의 랭킹 행 모양이 **변하지 않는다**. (구현 중 확인: `ResultScreen` 은 `Render` 를 아예 쓰지 않고 자체 행 페인팅을 갖는다 — README 계약 2 정정 참조. `Row` 에 필드를 늘린 것이 유일한 접점이므로 선택 인자 기본값으로 흡수했다)
- [x] 히스토리 랭킹 행에는 덱보기 버튼이 있고 `대기 중...` 슬롯에는 없다
- [x] EditMode: `BuildRows` 가 `deckInfo` 를 행에 옮기고, 대기 슬롯은 null
