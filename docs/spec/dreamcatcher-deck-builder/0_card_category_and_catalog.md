# 0 — 카드 카테고리 + 카탈로그 + DeckSave

## 목적

덱 규칙(고유≤2)과 저장/해석의 토대. 카드에 category, id→card 카탈로그, DeckSave 확장.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `category`
- 신규 `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCardCatalog.cs`
- 수정 `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — `DeckSave` 확장 + `SelectedDeck()`
- 6 카드 에셋 category 백필 + `DreamcatcherCardCatalog.asset` 생성 (execute_code)

## 구현

`DreamcatcherCard`:
```csharp
public enum CardCategory { Normal, Unique }
// 필드 추가
public CardCategory category = CardCategory.Normal;
```
백필: fortress=Unique, 나머지 5종=Normal.

`DreamcatcherCardCatalog` (DefenderCatalog 패턴):
```csharp
[CreateAssetMenu(menuName="Wassup/DreamcatcherCardCatalog")]
public class DreamcatcherCardCatalog : ScriptableObject {
    public DreamcatcherCard[] cards;
    public DreamcatcherCard ById(string id);     // 선형, null 허용
    public IEnumerable<string> AllIds();
}
```
6 카드 등록.

`DeckSave` 확장 (JsonUtility):
```csharp
[Serializable] public class DeckSave {
    public string id;
    public string name = "Deck 1";
    public List<string> cardIds = new();  // 가변(편집 중), 저장 시 Validate
    public int Count() => cardIds?.Count ?? 0;
}
```
`PlayerProfile.SelectedDeck()` — selectedDeckId 로 조회, 없으면 null. (기본 덱 미생성 — ProfileStore 변경 없음.)

## 완료 기준

- compile + read_console clean.
- 6 카드 category 백필(fortress=Unique 외 Normal), 디스크 직렬화 확인.
- `DreamcatcherCardCatalog.asset` cards=6, `ById("guardian_fortress").category==Unique`.
- `SelectedDeck()` null-safe(덱 없을 때 null).
- 기존 ProfileStore 테스트 유지(덱 기본생성 안 함).
