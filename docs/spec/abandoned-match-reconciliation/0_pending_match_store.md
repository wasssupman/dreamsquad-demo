# 0 — PendingMatchStore + PendingMatchPolicy

## 목적

미결 attempt 1건을 앱 재실행 너머로 영속하는 저장소와, 경과 시간으로 마감 방식을 결정하는 순수 함수를 만든다. 리포터(unit 1)와 배선(unit 2)이 소비할 토대.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Api/PendingMatchStore.cs`
- 신규 `Assets/_Project/Scripts/Core/Api/PendingMatchPolicy.cs`
- 신규 `Assets/_Project/Tests/EditMode/Api/PendingMatchStoreTests.cs`
- 신규 `Assets/_Project/Tests/EditMode/Api/PendingMatchPolicyTests.cs`

## 구현

### PendingMatchStore (static, PlayerPrefs)

- PlayerPrefs 단일 키 `Wassup.PendingMatch` 에 JSON 1개.
- 레코드 struct/class `PendingMatchRecord { string attemptId; string userId; long startedAtUnix; }` — `JsonUtility` 직렬화 대상이라 public 필드 + `[Serializable]`.
- API:
  - `void Save(string attemptId, string userId, long startedAtUnix)` → 레코드 JSON 저장 후 `PlayerPrefs.Save()`.
  - `bool TryLoad(out PendingMatchRecord record)` → 키 없음/빈 문자열/파싱 실패면 false. attemptId 빈 레코드도 false 취급(손상 방어).
  - `void Clear()` → `PlayerPrefs.DeleteKey` 후 `PlayerPrefs.Save()`.
- **flush 불변식**: Save/Clear 는 반드시 `PlayerPrefs.Save()` 로 끝난다. 이게 kill 생존과 좀비 방지의 핵심(README 계약).
- 시각 취득은 store 밖에서 주입(호출부가 `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` 전달). store 는 `Time`/시계에 의존하지 않는다 → 테스트 결정론.

### PendingMatchPolicy (순수 static)

- `enum PendingMatchAction { Complete0, DiscardOnly }`
- `const long DefaultTtlSeconds = 600;` — grace window 상수의 **유일한 소유처**(README 계약).
- `PendingMatchAction Decide(long elapsedSeconds, long ttlSeconds)` → `elapsedSeconds <= ttlSeconds ? Complete0 : DiscardOnly`.
- 경계·음수(시계 되감김) 방어: `elapsedSeconds < 0` 은 Complete0(막 시작한 판으로 간주). plain 값 in/out, 아키텍처 타입 무의존 → CLAUDE.md 규칙 10 대상.

## 완료 기준

- 컴파일 통과 (`dotnet build` 또는 Unity 콘솔 무에러).
- EditMode 테스트 통과:
  - Store: Save→TryLoad 라운드트립(필드 3개 일치), Clear 후 TryLoad=false, 미저장 상태 TryLoad=false, Save 덮어쓰기(단일 슬롯) 후 최신 레코드만.
  - Policy: `elapsed<ttl`=Complete0, `elapsed==ttl`=Complete0, `elapsed>ttl`=DiscardOnly, `elapsed<0`=Complete0.
- 테스트는 시계에 의존하지 않는다(경과초를 직접 주입).

완료: 2026-07-22 `b4ef4434` — 컴파일 0에러, EditMode 9/9 통과(Store 5 + Policy 4).
