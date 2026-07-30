# 3 — handoff summary

## Commit

- `4f2fafac` — feat(tournament-deck-info): complete body 를 deckInfo 단독으로 (units 0~2)

## Implemented

- `complete` 요청 body 가 `{"deckInfo":"..."}` 단독이 됐다. `debug`(배틀 로그 전문) 전송 경로 종료 — 필드를 빈 값으로 채우는 게 아니라 키가 나가지 않는다.
- `TournamentDeckInfo` v1 포맷 확정: `{"v":1,"squad":{"units":[],"stones":[]},"dc":{"cards":[]}}`, **id 만**. 표시명·아트·등급은 카탈로그가 해석한다.
- `dc.cards` 는 **고른 덱만**. 선물 카드(Lucid 롤 Active / Rim 무의식 2장)는 제외한다 — 이를 위해 `DreamcatcherRecord.baseDeckCardIds` 를 신설하고 `DreamcatcherHandController.LogDeck` 이 `_giftBaseCards` 를 같이 기록한다.
- 덱이 없으면 `deckInfo` 키도 뺀다(`{}`) — 0점 마감이 최고점 판의 기록을 덮어쓸 위험을 줄인다.
- 역직렬화는 관대하다: 빈/깨진 입력·미래 버전 → `null`, **과거 버전은 수용**, 누락 노드와 리스트 원소까지 정규화.
- `GET result` 응답의 `entries[].deckInfo` 바인딩.
- 실점수 경로만 덱을 싣는다. `AbandonMatch`/`ReconcilePending` 은 빈 값.

## Key Files

- `Assets/_Project/Scripts/Core/Api/TournamentDeckInfo.cs` — 포맷 계약 + 순수 시리얼/디시리얼
- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs` — `ExtraDataBody` / `BuildCompleteBody` / `Complete` / `ResultEntry.deckInfo`
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — `ReportResult(score, deckInfoJson, ...)`
- `Assets/_Project/Scripts/Logging/BattleLogger.cs` — `DeckInfoJson()`, `SetDreamcatcherDeck(+baseCardIds)`, `CopyCarryInContext`
- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs` — `DreamcatcherRecord.baseDeckCardIds`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `LogDeck` 이 고른 덱을 같이 넘김
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ReportMatchResult` (호출 1줄)
- 테스트: `Tests/EditMode/Api/TournamentDeckInfoTests.cs`, `Tests/EditMode/BattleLoggerDeckInfoTests.cs`, `Tests/EditMode/Api/TournamentApiTests.cs`

## Verified

- testrig 배치 EditMode **1633 tests / 1630 passed / 1 failed / 2 skipped**. 이 spec 관련 16건 전부 green.
- 실패 1건 `DirectionalVolleyIntegrationTests.AuthoredDefenderPatterns_...` 는 **무관** — 병행 세션의 tap-to-place WIP. 그 테스트가 읽는 `Defender_Shotgunner.asset` 등 에셋이 미커밋인데 리그에는 `.cs` 만 복사돼 생긴 불일치다.
- 코드 리뷰 1회(별도 레인). 지적 6건 중 5건 반영, 1건은 스코프 밖으로 이관(README 후속 후보).
- 라이브 서버 왕복은 이 spec 시점엔 못 했고, **후속 spec(`tournament-history-deck-view` unit 4)에서 닫았다** — 왕복 성립 확인, 선물 카드 분리도 실증(로컬 로그 12장 vs 화면 10장). 0점 덮어쓰기는 여전히 미확인.

## Notes (되돌리지 말 것)

- **`dc.cards` 에 선물 카드를 다시 넣지 말 것.** 실측으로 확인된 사실: 로컬 로그의 `deckCardIds` 는 12장(저장 10 + `active_meteor`/`active_rapid_fire`)이다. 이걸 그대로 보내면 후속 페이지가 재사용할 `DreamcatcherDeckStrip` 이 슬롯을 덱 크기만큼만 만들어 뒤 2장을 잘라먹고, 상태줄에 `12/10` 유효성 실패 문구가 남의 덱에 붙는다.
- **버전 게이트의 하한을 막지 말 것.** `v < 1 || v > Version` 이다. 상한만 위험이고, 하한까지 막으면 `Version` 을 올리는 순간 백카탈로그 전체가 "덱 정보 없음"이 된다.
- **표시명을 페이로드에 넣지 말 것.** `displayName` 은 시트 구동이라(`UnitStatImportDto` / `DcSheetImportDto` 리플렉션 매핑) 스냅샷하면 이름 변경 시 옛 엔트리만 옛 이름으로 남아 한 화면에 두 이름이 공존한다.
- **0점 마감 경로(`AbandonMatch` / `ReconcilePending`)에 지금 메모리의 덱을 붙이지 말 것.** 특히 reconcile 은 이전 세션/하드킬의 attempt 를 뒤늦게 닫는 경로라 남의 판에 엉뚱한 덱이 박힌다.
- `SnapshotJson()` 은 호출처를 잃었지만 **의도적으로 남겼다**(2026-07-30 사용자 결정). 죽은 코드로 보고 지우지 말 것.
- `TournamentDeckInfo.Deserialize` 의 소비처는 `DeckInfoPopup`(히스토리 덱보기) 하나다 — `tournament-history-deck-view` 에서 붙었다.

## Follow-up

- **히스토리 덱 보기 페이지** → `docs/spec/tournament-history-deck-view/` (완료 2026-07-30). 이 spec 의 `Deserialize` 가 그 팝업의 입력이다.
- 후속 후보 전체는 `docs/spec/README.md` → Follow-up Backlog → **토너먼트 덱 정보** 그룹으로 이관했다.
