# 0 — PlayerProfile 데이터 모델 + 유닛 id + 카탈로그

## 목적

씬 간 캐리어와 영속 저장의 데이터 토대를 만든다. 저장은 에셋 참조가 아니라 **안정 string id** 로 한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- 신규 `Assets/_Project/Scripts/Core/Profile/PlayerProfileSO.cs`
- 신규 `Assets/_Project/Scripts/Data/DefenderCatalog.cs`
- 수정 `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `id` 필드 추가
- 신규 에셋 `Assets/_Project/Data/DefenderCatalog.asset`
- 15개 `Assets/_Project/Data/Defenders/Defender_*.asset` — `id` 백필

## 구현

`DefenderUnitData` 최상단에 추가:
```csharp
[Tooltip("저장/로드용 안정 ID. 에셋 이름과 무관하게 고정 유지.")]
public string id;   // 예: "scout","ranger","guardian"
```
id 는 displayName 소문자 슬러그 기준으로 15개 에셋에 백필(UnityMCP `manage_asset` 또는 직접 .asset 편집). 한 번 정하면 변경 금지(저장 키).

`PlayerProfile` — plain `[Serializable]` 클래스 (`JsonUtility` 직렬화 대상):
```csharp
[Serializable]
public class PlayerProfile
{
    public int schemaVersion = 1;
    public List<string> ownedUnitIds = new();   // 기본 = 카탈로그 전체
    public List<SquadSave> squads = new();        // B 가 채움 (지금은 빈 타입 stub)
    public List<DeckSave> dreamcatcherDecks = new(); // C/D 가 채움
    public string selectedSquadId;   // null = 미선택 → A 폴백
    public string selectedDeckId;
}
```
`SquadSave`/`DeckSave` 는 **빈 stub** (`[Serializable] public class SquadSave { public string id; }` 수준). 필드 확장은 B/C 몫. 지금 과설계 금지.

`PlayerProfileSO` — 메모리내 홀더. `[CreateAssetMenu]`, 필드 `public PlayerProfile profile;` + 편의 접근자. 싱글톤 아님.

`DefenderCatalog` — `[CreateAssetMenu]`, `public DefenderUnitData[] units;` + `DefenderUnitData ById(string id)`(선형 탐색, null 허용) + `IEnumerable<string> AllIds()`. 15개 에셋을 카탈로그에 등록.

## 완료 기준

- compile 무에러 (read_console clean).
- `DefenderCatalog.asset` 에 15유닛 등록, 각 `id` 비어있지 않고 유일.
- `DefenderCatalog.ById("scout")` 가 해당 에셋 반환 (Unit 1 테스트에서 간접 검증).
- 기존 드래프트/전투 회귀 없음 (id 는 추가 필드일 뿐).
