# 2. DraftSession 폐기 모델

## 목적

기존 "10장 풀에서 7장 픽" 모델을 "10장 풀에서 3장 폐기, 나머지 7장 자동 픽" 모델로 의미 반전한다. UI 가 변하기 전 단위 테스트로 이 변환을 잠그고, BattleLogger / 기존 테스트 / Reset 시그니처까지 일괄 정리한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/DraftSession.cs`
- `Assets/_Project/Scripts/Core/DraftController.cs`
- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs` (`DraftRecord.picked` 코멘트 갱신)
- `Assets/_Project/Tests/EditMode/DraftSessionTests.cs` (기존 테스트 의미 반전 갱신 또는 폐기)
- 신규: `Assets/_Project/Tests/EditMode/DraftSessionDiscardTests.cs`

## 구현

1. `DraftSession` 모델 변경:
   - 신규 컬렉션 `Discarded` (`HashSet<DefenderUnitData>` + 순서 보존을 위한 `List<DefenderUnitData>` 둘 다 또는 `OrderedSet`. 폐기 카운트가 3 고정이라 단순 `List` 도 OK.)
   - 신규 메서드 `bool ToggleDiscard(DefenderUnitData unit)`:
     - 풀에 없는 unit → `false`
     - 이미 폐기된 unit → 폐기 해제 후 `true`
     - 미폐기이고 `DiscardedCount < maxDiscards` → 폐기 추가 후 `true`
     - 미폐기이고 `DiscardedCount == maxDiscards` → `false`
   - 기존 `Picked` 관련 API 정리:
     - `Picked` 프로퍼티는 의미를 "Pool 에서 Discarded 를 제외한 순서 보존된 리스트" 로 재정의 (이름 유지로 BattleLogger 호환).
     - `IsPicked(unit)` = `Pool.Contains(unit) && !Discarded.Contains(unit)`.
     - `PickedCount` = `Pool.Count - DiscardedCount` (보통 7).
   - `IsFull` 의미 변경: `DiscardedCount >= maxDiscards`.
   - `PickedArray()` 는 새 `Picked` 의미를 그대로 반환 (길이 7).
   - 기존 `TogglePick` 메서드는 제거. (호출자 업데이트 필요. 본 spec 의 새 UI 는 `ToggleDiscard` 만 호출.)
2. `Reset` 시그니처 변경:
   - 기존: `Reset(catalog, poolSize, maxPicks, seed)`.
   - 변경: `Reset(catalog, poolSize, maxDiscards, seed)`. 파라미터 이름만 바뀌고 내부 의미 반전.
   - 기본값: `maxDiscards = 3`.
3. `DraftController` 정리:
   - `[SerializeField] int pickCount = 7;` → `[SerializeField] int discardCount = 3;` 로 이름/기본값 변경.
   - `BeginDraft` 가 `_session.Reset(catalog, poolSize, discardCount, seed)` 호출.
   - 기존 `bool TogglePick(DefenderUnitData)` 메서드 제거. 대신 `bool ToggleDiscard(DefenderUnitData)` forwarding.
   - `PickCount` public 프로퍼티는 `pickCount` 가 사라졌으므로 `PoolSize - discardCount` 또는 `_session.PickedCount` 로 대체. (UI 가 이미 `PickedArray.Length` 를 쓰므로 호출 흔적 grep 후 조치.)
4. `BattleLogger.SetDraft` 호출부 (`DraftController.TryConfirm` 내):
   - `record.picked.Add(...)` 는 새 `_session.Picked` (= 폐기되지 않은 7장, pool 순서) 를 그대로 사용. 코드 변경 없음.
   - `BattleLogSchema.cs` 의 `DraftRecord.picked` 필드 코멘트를 "the 7 they locked in (in pick order)" 에서 "the 7 units the player kept (pool order, non-discarded)" 로 갱신.
5. 기존 `DraftSessionTests.cs` 정리:
   - `TogglePick`, `IsPicked`, `PickedCount`, `IsFull`, `PickedArray` 를 호출하는 6개 테스트 모두 새 의미로 재작성하거나, 본 task 가 추가하는 `DraftSessionDiscardTests` 와 중복되면 제거.
   - 최소: `TogglePick_*` 케이스는 `ToggleDiscard_*` 로 의미 반전 + 이름 변경. `PickedArray_Preserves_Order` 는 "폐기 후 남은 7장이 pool 순서를 유지" 로 변경.
6. 신규 `DraftSessionDiscardTests` 케이스:
   - 빈 시작에서 같은 unit 두 번 `ToggleDiscard` → 폐기 해제 검증.
   - 3장 폐기 후 4번째 폐기 시 `false` + 상태 보존.
   - `IsFull` 이 `DiscardedCount == 3` 일 때만 true.
   - `Picked` / `PickedArray()` 가 정확히 7장 + 폐기되지 않은 unit + pool 순서.
   - `Reset` 호출 시 `Discarded` 가 비워짐.

## 완료 기준

- Unity Test Runner EditMode 에서 `DraftSessionDiscardTests` + 갱신된 기존 테스트 모두 PASS.
- `BattleBridge.SetDefenderPool` 호출 시 인자 길이 7 (pool 크기 10 기준).
- `BattleLogSchema.cs` 의 `DraftRecord.picked` 코멘트 갱신.
- `DraftController.TogglePick` 호출자 0 (grep 결과). 옛 메서드 제거 확인.
- 컴파일 에러 0.
