# 1 — BattleLogger JSON 스냅샷

## 목적

결과 팝업 시점에 현재 배틀 로그를 **세션을 닫지 않고** JSON 문자열로 뽑는다. complete API body 의 `debug` 필드 재료. 파일 기록(`EndSession`)은 기존 흐름 그대로 유지한다.

## 변경 대상

- `Assets/_Project/Scripts/Logging/BattleLogger.cs` — `SnapshotJson()` 추가
- `Assets/_Project/Tests/EditMode/` — 스냅샷 테스트

## 구현

- `public string SnapshotJson()`
  - `currentEntry == null` 이면 `null` 반환.
  - `timestamp_end` 와 `result.duration_sec` 를 호출 시각 기준으로 채운 뒤 `JsonUtility.ToJson(currentEntry, prettyPrint: false)` 반환 (compact — 전송용).
  - 필드를 currentEntry 에 직접 채워도 안전: 이후 `EndSession` 이 최종 값으로 덮어쓴다. 이 의도를 주석으로 남긴다.
- 호출 시점 계약: BattleBridge 가 `SetResult`/`SetScore` 를 먼저 호출한 뒤 스냅샷을 뜬다 (unit 3). 따라서 스냅샷에는 최종 outcome/score 가 담긴다.
- BattleLogger 가 유일한 JSON 작성자 원칙 유지 — 리포터는 이 문자열을 가공 없이 사용.

## 완료 기준

- [ ] compile 통과
- [ ] EditMode 테스트: `StartSession` → `SetScore(123)` → `SetResult("victory", 0)` → `SnapshotJson()` 결과에 `"score":123` 과 `"outcome":"victory"` 포함, 이후 `EndSession()` 이 정상 동작 (파일 기록·경고 없음), 세션 없이 호출 시 `null`

확인: 2026-07-08 · `c53ed605` — `BattleLoggerSnapshotTests` 통과.
