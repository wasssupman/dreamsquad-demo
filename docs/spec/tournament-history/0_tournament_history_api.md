# 0 — Tournament History API 클라이언트

## 목적

히스토리 목록에 필요한 유일한 신규 엔드포인트 `GET /tournament/result/entry/unclaimed` 를 `TournamentApi` 에 추가하고, 상세 팝업 제목용으로 `ResultData.name` 을 확장한다. 상세 랭킹 조회(`GetResult`)는 기존 구현을 그대로 쓴다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs`
- `Assets/_Project/Tests/EditMode/Api/TournamentApiTests.cs`

## 구현

### DTO — 소비 필드만 (선례 준수)

```csharp
[Serializable]
public class UserTournamentResultEntry
{
    public string tournamentEntryId;  // 상세 조회 경로 파라미터 (필수)
    public string tournamentName;
    public int score;
    public int rank;
    public string createdTime;        // ISO-8601 문자열 (표시용, 파싱은 뷰에서)
    public bool claimed;
}
```

- `userId`/`tournamentTypeId`/`rewardData` 는 파싱하지 않는다.
- `ResultData` 에 `public string name;` 추가 (상세 팝업 제목). 기존 필드 순서/이름 불변.

### 메서드

```csharp
public static void GetUnclaimedEntries(string baseUrl, string idToken,
    Action<List<UserTournamentResultEntry>, string> onDone)
```

- `Send` 재사용(Bearer + `X-SERVICE-APP-VERSION` + timeout 동일).
- `internal static string BuildUnclaimedUrl(string baseUrl)` → `{base}/tournament/result/entry/unclaimed`.
- `internal static List<UserTournamentResultEntry> TryParseUnclaimed(string body, out string error)` → `ApiEnvelope.ParseList<UserTournamentResultEntry>` (envelope `data` 가 bare 배열).
- `ApiEnvelope.ParseList<T>`(신규, 추가형): 성공 envelope 의 `data` 가 `[]` **또는 null/누락**이면 빈 리스트로 바인딩(일부 서버가 빈 목록을 null 로 보냄). 기존 strict `Parse<T>`/`TryGetData`(단건·시트) 는 불변 — `TryGetData` 에 `allowNullData` 옵션(기본 false) 추가로만 구현.
- 성공 시 `onDone(list, null)`, 실패 시 `onDone(null, error)`. transportError 병합은 기존 패턴 동일.

### 빈 목록

- `data: []` 와 `data: null` **둘 다** 빈 리스트로 파싱된다(`ParseList` 가 두 형태 모두 수용, 코드리뷰 MEDIUM 반영). 실제 서버 빈 응답 형태는 Play 왕복에서 로그로 확인.

## 완료 기준

- [ ] compile: `dotnet build` (Unity 다운 시 asm csproj) 또는 Unity 콘솔 무에러.
- [ ] EditMode 테스트 추가/통과:
  - `TryParseUnclaimed_Success_BindsList` — 2건 배열 → 필드 바인딩(tournamentEntryId/tournamentName/score/rank/claimed).
  - `TryParseUnclaimed_EmptyArray_ReturnsEmpty` — `data: []` → count 0, error null.
  - `TryParseUnclaimed_NullData_ReturnsEmpty` — `data: null` → count 0, error null.
  - `TryParseUnclaimed_ErrorDetail_ReportsCode` — success=false → null + 코드 포함.
  - `BuildUnclaimedUrl` — trailing slash trim + 경로 합성.
  - `TryParseResult` 기존 테스트에 `name` 바인딩 assertion 추가.
- [ ] 실서버 왕복 확인은 unit 3 Play 검증에서 목록 로그로 겸함(빈/비어있지 않은 응답 형태 실측).
