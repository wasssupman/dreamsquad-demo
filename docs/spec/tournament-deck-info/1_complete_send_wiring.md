# 1 — complete body 를 deckInfo 단독으로 교체

## 목적

`POST /tournament/complete` 의 body 를 `{"deckInfo": "..."}` 하나로 만든다. 덱 데이터는 새로 수집하지 않는다 — `BattleLogger` 가 그 판의 스쿼드/드림스톤/드림캐쳐 덱을 **이미 기록 중**이므로 그 서브트리를 unit 0 의 직렬화기에 넘긴다. 프로필에서 직접 읽지 않는 이유: 로거는 **실제로 그 판에 들고 간 것**을 담고 있고(캐리인 시점 스냅샷), complete 시점의 프로필은 그 사이 바뀌었을 수 있다.

동시에 `debug`(배틀 로그 전문) 전송 경로를 제거한다. 파라미터를 빈 문자열로 넘기는 게 아니라 **경로 자체를 걷어낸다** — 안 쓰는 인자를 달고 다니면 다음 사람이 "왜 항상 빈 값인가"를 다시 조사하게 된다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs`
- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs`
- `Assets/_Project/Scripts/Logging/BattleLogger.cs`
- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`ReportMatchResult`)
- `Assets/_Project/Tests/EditMode/Api/TournamentApiTests.cs`

## 구현

**TournamentApi**

- `ExtraDataBody` 의 `debug` 를 **`deckInfo` 로 교체**한다 (필드 추가가 아니라 교체 — body 에 `debug` 키가 나가지 않아야 한다).
- `BuildCompleteBody(string deckInfoJson)` — 값이 null/빈이면 **키를 뺀다**(`NullValueHandling.Ignore` → `{}`). 계약 5 참조.
- `Complete(baseUrl, credential, attemptId, score, deckInfoJson, onDone)` — `debugJson` 자리를 그대로 대체. 나머지 전송 로직 무변경.

**BattleLogger**

`SnapshotJson()` **옆에** `DeckInfoJson()` 을 추가한다. 같은 "세션을 닫지 않고 뽑는 스냅샷" seam 이고, `currentEntry` 를 private 으로 유지한 채 나갈 수 있다. `SnapshotJson` 은 호출처를 잃지만 **삭제하지 않는다** (계약 6 — 사용자 결정). `EndSession`·전용 테스트 무변경.

```csharp
public string DeckInfoJson()   // currentEntry == null → null
    => TournamentDeckInfo.Serialize(
           currentEntry.squad.unitIds,
           <dreamstones[].id>,
           currentEntry.dreamcatcher.baseDeckCardIds);
```

**선물 카드 분리** — 로그의 `dreamcatcher.deckCardIds` 는 그 판에 실제로 돌린 **조합 덱**(저장 덱 + 이번 판 선물)이라 `deckInfo` 에 그대로 쓸 수 없다. 계약 1 은 고른 덱만 요구한다. 그래서 `DreamcatcherRecord` 에 `baseDeckCardIds` 를 **추가**하고(기존 필드는 의미까지 무변경), `SetDreamcatcherDeck` 에 파라미터를 하나 늘려 `DreamcatcherHandController.LogDeck` 이 `_giftBaseCards`(= `ResolveAttachDeck()` 결과 = 저장 덱)를 같이 넘긴다. 재시작 경로(`CopyCarryInContext`)도 새 리스트를 승계해야 한다 — 빠뜨리면 재시작한 판만 덱이 빈 채로 기록된다.

**TournamentMatchReporter**

- `ReportResult(int score, string deckInfoJson, Action<ResultData> onRanking = null, Action<string> onError = null)` — `battleLogJson` 파라미터 제거.
- `AbandonMatch` / `ReconcilePending` 의 `Complete(...)` 호출은 빈 문자열을 넘긴다 (README 계약 4). 왜 그런지 각 호출부에 한 줄 주석.

**BattleBridge**

```csharp
TournamentMatchReporter.ReportResult(playerScore, logger?.DeckInfoJson(), ...)
```

## 완료 기준

확인: 2026-07-30 EditMode green. **Play 검증 2건은 미실행** — 라이브 확인 일체를 후속 spec(히스토리 덱 보기)으로 이관한 사용자 결정에 포함된다.

- [x] 컴파일 통과
- [x] EditMode 테스트 통과:
  - 기존 `BuildCompleteBody_*` 2건을 `deckInfo` 기준으로 교체 — 임베드된 JSON 문자열이 이스케이프를 넘어 왕복한다
  - body 에 **`debug` 키가 없다**, null/빈 입력이면 **`deckInfo` 키도 없다** (계약 5 회귀 방지)
  - `DeckInfoJson` 배선 3건 — 세션 없음 → null, 빈 세션 → `""`, 세 서브트리가 각자의 페이로드 슬롯으로 가고 **선물 카드는 빠진다**
- [x] 기존 `BattleLoggerSnapshotTests` 2건이 그대로 통과한다 (계약 6 — `SnapshotJson` 무변경)
- [~] Play 검증: 매치를 완주하고 콘솔에 `[TournamentReporter] complete ok` 가 뜬다 — **미실행, 후속 spec 이관**
- [~] Play 검증: 매치 종료 후 `GameLogs/` 에 로그 파일이 여전히 정상적으로 쓰인다 (계약 6) — **미실행**. 단 `EndSession` 경로는 무변경이고 `BattleLoggerSnapshotTests` 가 파일 출력을 계속 단언한다
- [x] 나가기(AbandonMatch)로 끝낸 판은 덱을 싣지 않는다 — 코드 리뷰로 확인 (라이브 확인은 후속 spec)
