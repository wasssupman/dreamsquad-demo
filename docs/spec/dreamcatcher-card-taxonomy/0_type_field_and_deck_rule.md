# 0 — CardType 필드 + 덱 규칙 이전

## 목적

`CardType { Squad, Unit }` 필드를 SO 에 추가하고, 덱 캡을 고유(category)에서 스쿼드 타입으로 옮긴다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `CardType` enum + `type` 필드 append
- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DeckRules.cs` — MaxUnique → MaxSquad
- 수정: `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — 캡 체크/상태텍스트 Unique → Squad

## 구현

`DreamcatcherCard.cs`:
```csharp
// dreamcatcher-card-taxonomy — 스쿼드(축 스탯 버프) / 유닛(개별 부착 메커니즘).
// binding 과 정합(Squad=Axis, Unit=Unit)하되 type 을 신뢰. append 끝(기본 Squad).
public enum CardType { Squad, Unit }
// DreamcatcherCard 필드 (attackMods 뒤):
public CardType type;
```

`DeckRules.cs`:
- `MaxUnique` → `MaxSquad = 2`.
- `Validate`/`UniqueCount` 의 `card.category == CardCategory.Unique` 판정을 `card.type == CardType.Squad` 로 교체. 메서드명도 `SquadCount` 로. (호출부 갱신)
- 덱 크기 10 불변. 반복 규칙 손대지 않음.

`DreamcatcherDeckBuilderView.cs`:
- `DeckRules.UniqueCount`/`MaxUnique` 참조 → `SquadCount`/`MaxSquad`.
- 캡 차단(add 시 `card.type==Squad && SquadCount>=MaxSquad`), 상태텍스트 `squad {n}/{2}`.
- 프레임 색/라벨은 category 유지(cosmetic, 후속에서 type 전환).

## 완료 기준

- [ ] 컴파일 통과 (신규 enum refresh scope=all)
- [ ] 기존 카드 에셋 로드 무변동 (type append zero-init = Squad, 유닛 카드는 unit 1 에서 지정)
- [ ] DeckRules 가 스쿼드 개수로 캡 검증 (EditMode 있으면 갱신)
