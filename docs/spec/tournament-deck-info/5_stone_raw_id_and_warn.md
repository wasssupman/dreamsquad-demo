# 5. 스톤 미해석 id 보존 + 덱 없는 제출 가시화

## 목적

`deckInfo` 가 **조용히** 부실해지는 두 경로를 막는다.

1. **드림스톤 미해석 id 가 사라진다.** `LogDreamstoneCarryIn` 은 `stoneCatalog.ById(id) == null` 이면 그 슬롯을 기록에서 지운다 — 유닛은 카탈로그를 보지 않고 raw id 를 남기는데 스톤만 비대칭이다. 결정적으로 `LoadoutGate` 는 유닛 7 + 덱 검증만 하고 **스톤은 검사하지 않으므로**, 게이트를 통과한 실제 토너먼트 판에서도 발생한다. 시트가 정본인 특성상 "시트엔 있는 새 스톤 + stale 로컬 SO" 빌드에서 4개 장착한 판이 서버 기록엔 2개로 남는다. 계약 3(미해석 id 는 그 슬롯만 폴백)과 정렬한다.
2. **덱 없이 점수만 제출되는 것을 아무도 모른다.** 로거 미배선/세션 부재면 `DeckInfoJson()` 이 null → 키 생략 → 점수만 기록되고 로그가 한 줄도 남지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Core/GameManager.cs` — `LogDreamstoneCarryIn` 의 미해석 분기
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — `ReportResult` 경고

## 구현

1. 카탈로그 miss 슬롯을 버리지 않고 `id = raw, name = id, grade/kind = "", percent = 0, slotIndex = i` 로 기록하고 warning 한 줄. `DeckInfoJson` 은 `.id` 만 읽으므로 payload 에 raw id 가 실린다. 수신 측은 계약 3 이 이미 그 경우를 허용한다(카탈로그 miss → 그 슬롯만 폴백).
2. 경고는 **`ReportResult` 안, 게스트·attempt 부재 가드를 통과한 뒤**에 둔다. 호출부(`BattleBridge.ReportMatchResult`)에 두면 아무것도 제출되지 않는 진입(게스트·에디터 직접 Play·테스트 모드)에서도 "점수만 제출된다"고 매 판 거짓 경고가 뜬다. 0점 마감 두 경로에는 경고를 두지 않는다 — 배치 전 이탈의 빈 덱은 정상이다. 로거 미배선 자체는 `GameManager.Awake` 가 이미 따로 경고한다.

`DreamstoneRecord` 소비처는 로컬 로그 파일과 `DeckInfoJson` 둘뿐이라(다른 참조 없음) 폴백 레코드가 다른 화면을 깨뜨리지 않는다.

## 완료 기준

- compile 통과, EditMode 전량 green(기존 `BattleLoggerDeckInfoTests` 가 스톤→payload 매핑을 이미 고정한다).
- Play 확인: 프리셋 스톤 슬롯에 카탈로그에 없는 id 를 넣은 뒤 한 판 진행 → 콘솔에 미해석 warning, 로컬 로그 `dreamstones[]` 에 그 id 가 `slotIndex` 와 함께 남고, `complete` 의 `squad.stones` 에도 실린다.
- 스톤 `slotIndex` 를 payload 에 넣는 것(포맷 v2)은 이 작업 범위 밖 — README 후속 후보에 남긴다.
