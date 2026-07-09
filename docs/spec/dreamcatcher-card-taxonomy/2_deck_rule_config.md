# 2 — 덱 규칙 config (타입별 숫자 조정 가능)

## 목적

덱 규칙 숫자(덱 크기, 타입별 최대 장수)를 하드코딩 상수에서 **ScriptableObject config** 로 뽑아 조정 가능하게 하고, 그 값이 **실제 제약(Validate + deck-builder 차단)** 을 구동한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/Dreamcatcher/DeckRuleConfig.cs`
- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCardCatalog.cs` — `ruleConfig` 참조
- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DeckRules.cs` — config 기반 검증
- 수정: `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — UI 숫자/차단 config 기반
- 수정: `Assets/_Project/Tests/EditMode/DeckRulesTests.cs` — config 주입 테스트
- 신규 에셋: `DeckRuleConfig_Default.asset` + 카탈로그가 참조

## 구현

`DeckRuleConfig` (SO):
```csharp
[CreateAssetMenu(...)]
public class DeckRuleConfig : ScriptableObject
{
    public int deckSize = 10;
    public int maxSquad = 2;   // CardType.Squad 최대
    public int maxUnit = -1;   // CardType.Unit 최대 (-1 = 무제한)
    public int MaxFor(CardType t) => t == CardType.Squad ? maxSquad : maxUnit; // <0 = 무제한
}
```

`DreamcatcherCardCatalog`: `public DeckRuleConfig ruleConfig;` (null 이면 DeckRules 기본값 폴백).

`DeckRules`:
- 상수는 **폴백 기본값**으로만 유지(`DefaultDeckSize=10`/`DefaultMaxSquad=2`/`DefaultMaxUnit=-1`).
- `Validate(cardIds, catalog, out reason)`: `catalog.ruleConfig` (없으면 기본값)로 deckSize + **타입별 카운트 ≤ MaxFor(type)** 검증(<0 은 skip).
- `TypeCount(cardIds, catalog, type)` 추가. `SquadCount` 는 `TypeCount(..., Squad)` 로 유지(호출부 호환).
- `EffectiveDeckSize(catalog)` / `EffectiveMax(catalog, type)` 헬퍼 — deck-builder UI 용.

`DreamcatcherDeckBuilderView`: `DeckSize`/`MaxSquad` 상수 참조를 `EffectiveDeckSize`/`EffectiveMax(catalog, Squad)` 로 교체.

## 완료 기준

- [x] 컴파일 + 무회귀 (config 없으면 기본값 폴백으로 기존 동작 유지)
- [x] `DeckRuleConfig_Default`(10/2/-1) 를 카탈로그가 참조 → EffectiveDeckSize=10/EffMaxSquad=2 확인. config 값 변경이 실제 제약 변화 (EditMode `Config_OverridesDeckSizeAndCaps`: deckSize4·maxSquad1 주입 시 2장 거절, `NegativeCap_MeansUnlimited`)
- [x] DeckRulesTests config 주입 케이스 추가 8/8 통과

완료 확인: 2026-07-09 — DeckRuleConfig SO(타입별 숫자) + 카탈로그 참조 + DeckRules/deck-builder config 구동 + 테스트. 값이 실제 제약으로 이어짐 실증. 이 문서와 동일 커밋.
