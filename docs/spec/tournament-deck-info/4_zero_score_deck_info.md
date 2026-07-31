# 4. 0점 마감도 그 attempt 의 덱을 싣는다

## 목적

0점 마감 두 경로(`AbandonMatch` 나가기 · `ReconcilePending` 하드킬/이전 세션)가 `deckInfo` 를 빈 값으로 보내는 것을 멈춘다. 계약 5 의 "키 생략"은 완화이지 차단이 아니다 — 서버가 엔트리 컬럼을 최고점 가드 없이 대입하면 좋은 판의 덱이 `null` 로 덮인다(README 미확인 #2).

**덱을 실으면 서버 가드 유무와 무관해진다.** 어느 마감이 대입해도 엔트리에는 항상 "그 플레이어가 그 attempt 에 들고 간 덱"이 남는다. 남는 불일치는 프리셋을 바꿔 재플레이한 경우의 점수↔덱 어긋남뿐이고, 그건 서버 가드로만 해소된다.

## 계약: 덱은 attempt 에 귀속된다

지금 메모리의 덱을 아무 마감에나 붙이지 않는다. 경로마다 **그 attempt 의** 덱을 가져온다.

| 경로 | 덱 출처 |
|---|---|
| `ReportResult` (완주 — 패배 포함) | 무변경. 호출자가 `logger.DeckInfoJson()` 전달 |
| `AbandonMatch` (나가기) | **호출자 전달**. 배틀 씬이 살아 있어 `GameManager.Instance.Logger` 가 유효하다 |
| `ReconcilePending` (하드킬/이전 세션) | **`PendingMatchRecord.deckInfo`**. 로비에는 로거도 GameManager 도 없다 — 판 중에 적어둔 것이 유일한 출처 |

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs` — 레코드에 `deckInfo` + `SaveDeckInfo`
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs` — `PersistMatchDeck` 신설, `AbandonMatch(deckInfoJson)`, `ReconcilePending` 이 레코드의 덱을 전송
- `Assets/_Project/Scripts/UI/MenuPopup.cs` — 나가기에서 로거의 덱 전달
- `Assets/_Project/Scripts/Core/GameManager.cs` — 캐리인 직후 스냅샷 저장
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `LogDeck` 직후 갱신
- `Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs`

## 구현

1. `PendingMatchRecord.deckInfo` 추가. `Save` 는 새 attempt 이므로 빈 값으로 시작한다. 구 레코드(필드 없음)는 `JsonUtility` 가 null → 빈 값 취급이라 하위호환된다.
2. `SaveDeckInfo(attemptId, json)` 은 **compare-and-write** — 저장된 레코드가 그 attempt 일 때만 쓴다(`ClearIfMatches` 와 같은 형태). 다음 판이 이미 자기 레코드를 저장한 뒤 늦은 호출이 와도 남의 레코드를 오염시키지 못한다.
3. `TournamentMatchReporter.PersistMatchDeck(json)` — `_attemptId` 로 위 호출. **빈 값은 쓰지 않는다**(한 번 적힌 덱을 빈 값이 지우지 못하게). 게스트/attempt 없음/불일치는 조용한 no-op.
4. 저장 호출 시점 **두 곳**: 캐리인 직후(유닛·스톤 확정 — `StartSquadMatch`·`StartTestModeMatch` 공통 헬퍼)와 `LogDeck` 직후(카드 확정). payload 가 단조 증가하므로 순서 의존이 없고, 배치 진입 전 하드킬도 유닛·스톤은 남는다.
5. `AbandonMatch` 는 파라미터로 받은 덱을, `ReconcilePending` 은 `rec.deckInfo` 를 `TournamentApi.Complete` 에 넘긴다. 진짜로 덱이 없으면 `BuildCompleteBody` 가 키를 빼는 계약 5 는 그대로다.

## 완료 기준

- compile 통과, EditMode 전량 green. `PendingMatchStoreTests` 5건: 덱 왕복 / 다른 attempt 는 no-op / 무레코드·빈 id / 새 `Save` 는 덱을 빈 값으로 초기화 / 덱 없는 구 레코드는 `TryLoad` 가 빈 값으로 정규화. `TournamentMatchReporterTests` 3건: 현재 attempt 에 기록 / **attempt 없으면 레코드 무접촉**(에디터 직접 Play·테스트 모드가 남의 안전망을 오염시키지 않는다) / 빈 스냅샷은 이미 적힌 덱을 지우지 않는다.
- Play 3경로 콘솔 확인 — (a) 완주: 기존대로 덱 실림, (b) 배치 이후 나가기: `complete` body 에 `deckInfo` 존재, (c) 배치 중 강제 종료 후 로비 재진입: reconcile 이 그 attempt 의 덱과 함께 0점 마감.
- 라이브 확인(선택): 좋은 판을 친 뒤 같은 엔트리에서 한 판을 나가기로 끝내고 `GET result` 로 엔트리의 `deckInfo` 가 살아 있는지 본다. 이 작업 이후에는 서버 가드가 없어도 덱이 남아야 한다.

**완료 확인 2026-07-31** — EditMode 1766 중 1764 pass / 0 fail / 2 기존 Ignore, 신규 8건(`PendingMatchStoreTests` 5 · `TournamentMatchReporterTests` 3). Play 3경로(완주 · 배치 후 나가기 · 하드킬 후 로비 reconcile) 사용자 확인 완료. 커밋 `73deadbb`.
