# Gift Phase — Handoff Summary

## Commit

- `29d9ffd7` unit 0~1 — `GamePhase.Gift` + 덱 조합 seam
- `8b2119d5` unit 2 — 무의식 카드 2장 + 덱빌더 제외
- `950ac0b4` unit 3 — GiftPhaseView + 라우팅 + HUD 재게이팅 + 씬 배선
- `3ee3047f` unit 4~5 — PrimeTween 연출 시퀀스
- `4d4eee69` 코드리뷰 반영 (M1 재시작 stale덱 + minors)
- (스펙: `ec50107f`, `c5e7ca1b`)

## Implemented

- 배치 직전 `GamePhase.Gift`(Placement 앞) 삽입. 진입 신호(Draft `DraftConfirmed` / Squad·Test `PlacementRequested`)와 재시작(`BattleBridge.OnRestartRequested`→`EnterPlacementOrGift`)이 `GiftPhaseView.BeginGift()` 로 라우팅.
- `SetPhase(Gift)` → `DreamcatcherHandController.BuildGiftDeck` → `GiftDeckReady` → GiftPhaseView 연출 → `ProceedToPlacement` → `PlacementPhaseView.BeginPlacementPhase()`.
- Lucid = 기존 `SkillLoadoutController.Picked`(Active 2) 재사용. Rim = 카탈로그 Subconscious 시드 2장 + 폴백. `GiftDeckComposer.PickKind/PickRim` 순수 결정론(EditMode 9/9).
- 덱: HandController 가 Gift 에서 `DreamcatcherCycleDeck` 1회 생성(단일 셔플)·캐시, 배치에서 **동일 인스턴스 재사용**(per-entry 플래그 `_giftDeckComposed`). 연출 순서 = `GiftFinalOrder()`(=`deck.Hand(전체)`) = 인게임 큐. **이중 셔플 없음, CycleDeck 무변경**.
- 무의식 카드 2장(`sub_deepsleep` 유효체력+12%, `sub_dreamhaste` 공속+10%) 기존 effects 채널. 카탈로그 등록(무의식 총 3장). 덱빌더 소유그리드 제외.
- 연출(PrimeTween): 인트로 텍스트 → 보유10 스태거 드롭인 → 선물2 임팩트 → 촤라락 셔플(entryId 매핑, 확정순서 착지) → 홀드 → 각성버튼 fly-out scale0 → 배치. 타이밍 전부 `GiftConfig`.
- 배치 HUD(DefenderSelector·AwakeningGaugeView) 노출을 진입 이벤트 → `PhaseChanged(Placement)` 로 재게이팅(선물 중 무누수).

## Key Files

- `Assets/_Project/Scripts/Core/Dreamcatcher/GiftDeckComposer.cs` (순수 결정론)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` (BuildGiftDeck/BuildFallbackDeck/ResolveRimGift, GiftFinalOrder, `_giftDeckComposed`)
- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` (라우팅 + PrimeTween 시퀀스)
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` + `GiftConfig_Default.asset`
- `Assets/_Project/Data/Dreamcatcher/Card_SubDeepSleep.asset`·`Card_SubDreamHaste.asset`·`DreamcatcherCardCatalog.asset`
- 라우팅/게이팅: `PlacementPhaseView.cs`·`DefenderSelector.cs`·`AwakeningGaugeView.cs`·`BattleBridge.cs`(`_giftPhaseView`)
- 씬: `BattleScene.unity`(GiftPhaseView GO + 전 참조)

## Verified

- Compile clean(에러 0), EditMode `GiftDeckComposerTests` 9/9.
- Play(비포커스, execute_code): phase=Gift, kind=Rim·Lucid 양 분기, finalOrder=12, `reuseSame=True`(GiftOrder==AfterPlacement, 이중셔플 없음), HandFront5==GiftOrder앞5. 선물 중 tray/awak=False, 배치서 True/True. 런타임 에러 0.

## Notes (되돌리면 안 되는 의도)

- **CycleDeck 무변경**: 순서 일치는 `Hand(전체)` 재사용으로 달성. no-shuffle 오버로드 추가하지 말 것.
- **재시작=동일 결과 재생**: MatchSeed 고정이라 restart 마다 같은 선물(결정론 보존, 사용자 결정). restartIndex 서브시드 도입 금지.
- **덱 재사용 가드는 per-entry 플래그**(`_giftDeckComposed`), `_deck==null` 아님 — 후자는 gift 우회 재시작 시 stale 덱 재사용(리뷰 M1).
- HUD 노출은 `PhaseChanged(Placement)` 기준. 진입 이벤트 재구독 금지(선물 중 튀어나옴).
- `category` 는 더 이상 dormant 아님(Rim 풀 필터 + 덱빌더 제외 소비).

## Follow-up

- **트위닝 시각 최종 검증**: 비포커스 MCP 는 프레임 정지라 애니메이션 육안 확인은 **사용자 포커스 Play** 필요. 카드 아트/셔플/fly-out 느낌·`GiftConfig` 타이밍 튜닝.
- 무의식 카드 수치는 placeholder — 밸런스 튜닝.
- fly 타깃은 고정 근사 좌표(`FlyTarget`) — 각성 버튼 정밀 정렬은 후속.
- `fastForwardInTestMode` 는 TestModeContext 가 배치 전 Clear 되어 현재 사실상 미발동 — 테스트 훅 필요 시 컨텍스트 유지 방식 도입.
- 후속 후보(README): ownedCardIds 보유 인벤토리, 무의식 콘텐츠 확장, 선물 이벤트 종류/리롤.
