# 1 — 덱 규칙 (순수 검증)

## 목적

덱 유효성(정확히 10장 · 고유≤2)을 판정하는 순수 함수. 빌더와 인게임 폴백 판정에 공용.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/Dreamcatcher/DeckRules.cs`
- 신규 `Assets/_Project/Tests/EditMode/DeckRulesTests.cs`

## 구현

```csharp
public static class DeckRules
{
    public const int DeckSize = 10;
    public const int MaxUnique = 2;

    // 반환: 유효 여부 + 사람용 사유(영문). cardIds 의 빈/무효 id 는 무효 카운트.
    public static bool Validate(IReadOnlyList<string> cardIds, DreamcatcherCardCatalog catalog, out string reason);

    // 편집 UI 용: 현재 고유 카드 수.
    public static int UniqueCount(IReadOnlyList<string> cardIds, DreamcatcherCardCatalog catalog);
}
```
규칙:
1. `cardIds.Count == 10` 아니면 reason="need exactly 10 (have N)".
2. 각 id를 catalog로 해석, 무효 id 1개 이상이면 reason="unknown card: X".
3. category==Unique 개수 > 2 이면 reason="too many unique (N/2)".
4. 통과 시 reason="ok".

- 빈 슬롯("")은 카운트에 포함되어 10 미만/초과 판정에 반영(또는 빈칸 제외 후 10 요구 — 빌더 정책과 일치). **정책: cardIds 는 채워진 카드만 담고, 10개 채워야 유효.** 빈 슬롯은 빌더가 리스트에서 제외.

## 완료 기준

- EditMode `DeckRulesTests`:
  - 10 Normal → ok.
  - 9장 → invalid(count).
  - 11장 → invalid(count).
  - Unique 2 + Normal 8 → ok. Unique 3 → invalid(unique).
  - 무효 id 포함 → invalid(unknown).
  - `UniqueCount` 정확.
- compile + read_console clean.
