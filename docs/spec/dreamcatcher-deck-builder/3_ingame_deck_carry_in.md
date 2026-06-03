# 3 — 인게임 덱 반입

## 목적

인게임 드림캐쳐 3중1이 선택 저장덱에서 뽑히게 한다. 없으면 고정 덱 폴백.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`
- 수정 `Assets/_Project/Scenes/BattleScene.unity` — profileSO/cardCatalog 참조 wiring
- 신규 `Assets/_Project/Tests/PlayMode/DreamcatcherDeckCarryInTest.cs`

## 구현

`DreamcatcherController`:
- 참조 추가: `[SerializeField] PlayerProfileSO profileSO; [SerializeField] DreamcatcherCardCatalog cardCatalog;`
- 런타임 덱 해석(첫 사용 시 1회): 
  ```
  List<DreamcatcherCard> ResolveDeck():
    var save = profileSO?.profile?.SelectedDeck();
    if (save != null && cardCatalog != null && DeckRules.Validate(save.cardIds, cardCatalog, out _)) {
        save.cardIds → cardCatalog.ById → List<DreamcatcherCard> (null 제외)
    } else {
        serialized `deck` 의 cards (C 폴백)
    }
  ```
- `Draw3()` 가 고정 `deck.cards` 대신 `ResolveDeck()` 결과(캐시)에서 추첨. 중복 cardId 는 독립 항목 유지(스택).
- BeginPlacement/매치 시작마다 캐시 무효화(선택 덱이 매치 중 바뀌진 않지만 재진입 대비). 간단히 OnPhaseChanged(Placement) 진입 시 1회 resolve.

BattleScene: DreamcatcherController 에 `PlayerProfile.asset`(profileSO) + `DreamcatcherCardCatalog.asset` 연결. serialized `deck` 은 `DreamcatcherDeck_Default` 유지(폴백).

## 완료 기준

- 저장덱(예: ranger 카드 위주 10장) 선택 후 게임 진입 → 첫 3중1이 그 덱 카드들로 구성(저장덱 카드만 등장).
- 선택 덱 없음 → 기존 고정 덱으로 3중1(C 동작 유지).
- PlayMode `DreamcatcherDeckCarryInTest`: 프로필에 유효 덱 주입 → 컨트롤러 ResolveDeck 이 그 카드 반환; 덱 없음 → serialized 폴백.
- 기존 PlayMode(Dreamcatcher/Squad/Outgame) 통과.
- read_console clean.

> 완료 확인 2026-06-03 — PlayMode DreamcatcherDeckCarryInTest: 선택 저장덱(10×ranger_atk) → ResolveDeck 그 카드만; 선택 없음 → serialized 기본 덱(혼합) 폴백. BattleScene DreamcatcherController 에 profileSO/cardCatalog wiring. PlayMode 7/7.
