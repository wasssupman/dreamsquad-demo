# 4 — 드림캐쳐 페이지 배선

## 목적

드림캐쳐 페이지에 프리셋 바를 붙인다. unit 3 과 동형이지만 두 가지가 다르다 — 작업본이 **가변 길이 카드열**이고, 이 컨트롤러는 이미 `_working` 리스트를 갖고 있어 작업본 도입 자체는 절반이 되어 있다. 남은 일은 **자동 저장을 떼는 것**이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPage.cs` — 레이아웃 + 바 생성/주입

## 구현

**레이아웃** (`DreamcatcherDeckPage`): `presetBarHeight = 0.10f` 신설, `headerHeight` 0.16 유지, 브라우저 `1 - 0.10 - 0.16 = 0.74`.
- presetBar: `(detailWidth, 0.90) ~ (1, 1)`
- deckStrip: `(detailWidth, 0.74) ~ (1, 0.90)`
- browser: `(detailWidth, 0) ~ (1, 0.74)`

`ConfirmPopup` 도 생성해 주입.

**자동 저장 제거** — 현 컨트롤러의 핵심 변경:
- `AddCard`/`RemoveOccurrence` 의 `PersistWorking()` 호출을 제거하고 **`RefreshBarState()`**(dirty 재계산, 목록 재구성 없음)로 교체 — unit 3 과 동일한 분리
- `PersistWorking()` 은 [저장] 전용 경로로 축소한다. 현재 이 메서드는 **하드코딩 `DeckId = "deck_1"` 을 찾아 없으면 만들고 `selectedDeckId` 를 강제 대입**한다 — 프리셋 30개 세계에서 이 세 동작 모두 틀렸다. `_viewingPresetId` 가 가리키는 엔트리에만 쓰고, **확정 포인터는 건드리지 않는다**([선택] 전용)
- `const string DeckId = "deck_1"` 삭제
- `LoadWorking()` 이 `CommittedDeck()` 대신 `_viewingPresetId` 의 엔트리를 읽도록 변경
- `_workingName` 추가 (unit 1 의 `IsDeckDirty` 가 이름을 본다)
- `ProfileSaver` 테스트 훅(`[NonSerialized] internal Action<PlayerProfile>`)은 유지 — 테스트가 쓰는 심이고, 자동 저장이 사라져도 [저장] 경로 검증(저장 호출 **횟수** 세기)에 그대로 쓸모가 있다. 해당 테스트는 `DreamcatcherDeckAutosaveTests` → **`DreamcatcherDeckSaveTests`** 로 개명됐다(전제가 반전됐으므로 이름도 함께)

**4조작**: unit 3 의 표와 동일하되 대상이 `dreamcatcherDecks` / `selectedDeckId` / `_workingCards`. 기본 이름 `"덱 N"`.

[되돌리기]는 `LoadWorking()` 재호출로 **저장본 기준 복원**이다(rev 2026-07-30). 고정 칸이 없어 신규 프리셋에서는 빈 리스트가 되고, 그게 곧 완전 비움이다.

**덱 규칙과의 관계**: `DeckRules.Validate` 는 여전히 START 게이트(`LoadoutGate`)의 책임이고, 이 페이지는 유효하지 않은 중간 상태를 자유롭게 저장할 수 있다(기존 계약 유지). `CanAdd` 의 캡·타입 제한 검사도 그대로 `_workingCards` 기준으로 동작한다.

## 완료 기준

- [ ] 컴파일 그린
- [ ] `DeckId` 하드코딩 검색 0건. `selectedDeckId` 대입이 [선택] 경로에만 존재
- [ ] 카드 추가/제거가 디스크에 닿지 않음 — [저장] 없이 재진입하면 이전 저장 상태
- [ ] [저장] 후 재진입 시 저장 내용
- [ ] 미저장 변경 + [선택] → START 시 **저장분이 반입**된다
- [ ] `[+]` → 빈 덱 프리셋 생성 → 카드 채우고 저장 → 두 프리셋 간 전환이 각자 내용을 보존
- [ ] [되돌리기] → 저장본 카드열로 복원 + dirty 꺼짐. 신규 빈 프리셋에서는 빈 덱
- [ ] 삭제: 확정분/마지막 1개는 사유 팝업 · 삭제 가능분은 확인 팝업 · 미주입 시 fail-closed. 30 상한
- [ ] `DreamcatcherDeckAutosaveTests` → **`DreamcatcherDeckSaveTests`** 개명 + 전제 반전(편집 시 저장 **0회**, [저장] 시 1회)하고 그린
- [ ] Play 콘솔 에러 0. 덱 스트립·카드 그리드·상세가 작업본을 반영

---

**검증 기록 2026-07-30 · `5de0f258`** — 컴파일 errors=0 · `DeckId` 하드코딩 검색 0건, `selectedDeckId` 대입이 [선택] 경로에만 존재 · `DreamcatcherDeckSaveTests` 8건 그린(편집 시 저장 0회 · [저장] 1회 · 확정 미이동 · 미로드 가드) · 팝업 레이어 자동 검증. **미검증**: 드캐 페이지 Play 육안(스쿼드와 동일 골격이라 레이어 테스트로 대체 검증).
