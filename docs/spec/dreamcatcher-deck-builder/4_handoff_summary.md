# 4 — Handoff Summary (D 완료)

D(`dreamcatcher-deck-builder`) 10장 빌더 MVP 종료. 최신 계약은 README + 번호 문서 우선.

## Commit

- spec `6213e5d`
- 0 `add08ca` — 카드 카테고리 + 카탈로그 + DeckSave
- 1 `42cd9b5` — DeckRules + 테스트
- 2 `d933754` — 덱 빌더 UI
- 3 `1672591` — 인게임 덱 반입

## Implemented

- `DreamcatcherCard.category`(Normal/Unique) 6종 백필(fortress=Unique). `DreamcatcherCardCatalog`(id→card) + 에셋.
- `DeckSave`(id/name/cardIds) + `PlayerProfile.SelectedDeck()`. 신규 프로필은 덱 0개(ProfileStore 무변경).
- `DeckRules.Validate`(정확히 10·고유≤2) + `UniqueCount` — 빌더 저장 게이트 + 인게임 폴백 판정 공용.
- `DreamcatcherDeckBuilderView`(OutgameScene DreamcatcherPanel): 보유 6 카드 + 10슬롯 + 규칙 라벨 + SAVE(유효 시만). deck_1 upsert + selectedDeckId.
- `DreamcatcherController.ResolveDeck()`: 선택 저장덱(유효) → catalog 해석; 없으면 serialized 고정 덱(C 폴백). `Draw3` 가 resolve 결과에서 추첨, 배치 진입 시 매치별 1회 resolve.

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/{DreamcatcherCard,DreamcatcherCardCatalog,DeckRules}.cs`
- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`(DeckSave/SelectedDeck)
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`(ResolveDeck)
- 에셋: `Data/Dreamcatcher/DreamcatcherCardCatalog.asset`, OutgameScene DreamcatcherPanel, BattleScene DreamcatcherController wiring
- 테스트: `Tests/EditMode/DeckRulesTests.cs`, `Tests/PlayMode/DreamcatcherDeckCarryInTest.cs`

## Verified

- EditMode DeckRulesTests 6/6, PlayMode 7/7(DreamcatcherDeckCarryInTest 포함).
- Play(MCP): 빌더 보유 6 → 10장 저장(disk deck_1/selectedDeckId), 고유 2 상한, ResolveDeck 저장덱/폴백.

## Notes (되돌리지 말 것)

- 신규 프로필 덱 0개 → 인게임은 serialized 고정 덱(`DreamcatcherDeck_Default`) 폴백(C 비파괴). 첫 덱은 빌더에서 생성.
- 카드 6종 전부 보유(MVP, 보유 시스템 없음). fortress만 Unique → 고유≤2 여유.
- UI 라벨 영문(한글 폰트 후속). 빌더 슬롯뷰는 _working(List) 가 source of truth.

## Follow-up

- 카드 보유/언락(ownedCardIds) + 가챠/꿈런 파밍.
- 카드 콘텐츠 확장(기획 일반10+고유3+무의식2, 신규 메커닉 채널).
- 다중 덱 수집/전환, 덱 이름 편집, 무의식 편입.
- 빌더 슬롯뷰 재사용(현재 Destroy/재생성) 최적화 — 대량 카드 시.
