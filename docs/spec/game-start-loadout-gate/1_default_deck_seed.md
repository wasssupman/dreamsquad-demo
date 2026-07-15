# 1 — 신규 프로필 기본 덱 시딩

## 목적

게이트를 달기 전에 **신규 유저가 첫 START 에서 막히지 않게** 한다. `ProfileStore` 는 스쿼드는 시딩하면서 덱은 만들지 않아(`dreamcatcher-deck-builder`: "신규 프로필 = 덱 0개"), 게이트를 그대로 붙이면 모든 신규 설치가 "드림캐쳐 덱 0/8" 로 차단된다. 스쿼드와 대칭으로 맞춘다 (사용자 결정 2026-07-16, critic M3).

곁들여 **기본 덱의 소유권을 dev 버튼에서 `ProfileStore` 로 옮긴다.** 현재 기본 덱을 아는 유일한 코드는 `DefaultLoadoutButton`(`DevOnlyGroup` 아래 — 비-dev 빌드엔 GO 자체가 없다)이다. 신규 설치가 의존할 정의를 출시 빌드에 존재하지 않는 버튼에 둘 수 없다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs` — `BuildDefaultDeck` 이관 + `EnsureDefaultDeck` + 시그니처 확장
- `Assets/_Project/Scripts/UI/Outgame/DefaultLoadoutButton.cs` — 자체 `BuildDefaultDeck` 제거, `ProfileStore` 위임
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `defaultDeck` SerializeField + `LoadOrCreate` 인자 전달
- `Assets/_Project/Tests/EditMode/ProfileStoreTests.cs` — 시딩 케이스 추가
- `Assets/_Project/Tests/EditMode/DefaultLoadoutButtonTests.cs` — `ProfileStore.BuildDefaultDeck` 로 대상 변경
- `Assets/_Project/Scenes/OutgameScene.unity` — `OutgameMenuController.defaultDeck` ← `DreamcatcherDeck_Default.asset`

## 구현

### ProfileStore

`BuildDefaultDeck(DreamcatcherDeck source, int deckSize)` 를 `DefaultLoadoutButton` 에서 **그대로 옮긴다** (동작 변경 없음 — 이미 테스트가 있다). 그 위에:

```csharp
static void EnsureDefaultDeck(PlayerProfile p, DreamcatcherDeck defaultDeck, DreamcatcherCardCatalog cards)
```

1. `defaultDeck == null || cards == null` → **return** (호출자가 안 넘겼으면 시딩하지 않는다 — 기존 테스트 경로 보존)
2. `p.SelectedDeck() != null` → **return** — 플레이어가 고른 덱은 절대 덮지 않는다
3. 덱은 있는데 선택이 깨졌으면 `selectedDeckId = dreamcatcherDecks[0].id` 로 복구만
4. 그 외 → `BuildDefaultDeck(defaultDeck, DeckRules.EffectiveDeckSize(cards))` 를 추가하고 선택. **단 만들어진 덱이 0장이면 추가하지 않는다** (빈 덱을 심는 건 무의미)

`EnsureNonNull` 에서 `EnsureDefaultSquad` 옆에 호출한다 — 신규 생성뿐 아니라 **덱 없는 기존 프로필도 구제**된다 (스쿼드 시딩이 이미 그렇게 동작한다).

시그니처는 **선택 인자로 확장**한다. 기존 호출처(테스트 다수)를 깨지 않으면서 프로덕션 경로만 덱을 넘긴다:

```csharp
public static PlayerProfile LoadOrCreate(DefenderCatalog catalog,
    DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null)
public static PlayerProfile CreateDefault(DefenderCatalog catalog,
    DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null)
```
`LoadOrCreateAt` / `EnsureNonNull` 도 같이 통과시킨다.

### 호출처

- `OutgameMenuController.Awake:42` → `ProfileStore.LoadOrCreate(catalog, defaultDeck, cardCatalog)`. `cardCatalog` 는 unit 2 의 게이트가 어차피 필요로 하는 필드다.
- `DefaultLoadoutButton.OnClick` → `ProfileStore.CreateDefault(defenderCatalog, defaultDeck, cardCatalog)` 로 바꾸고, 뒤이어 손으로 덱을 조립하던 3줄(`BuildDefaultDeck` + `dreamcatcherDecks =` + `selectedDeckId =`)을 **삭제**한다. 이 버튼은 이제 "기본 프로필 = `CreateDefault` 결과" 만 알면 된다 — 그게 원래 의도였다(`6_default_loadout_button.md`: "기본값 정의의 유일한 소유자를 유지").

### 주의

- **`ProfileStore.Save` 는 반드시 새 프로필로 부른다** — 빈/오래된 in-memory SO 로 부르면 스쿼드까지 날아간다(기록된 함정). `DefaultLoadoutButton` 의 `profileSO.profile` 선교체 순서를 그대로 둔다.
- `Wassup.Core` 가 `Wassup.Data`(`DreamcatcherDeck`/`DreamcatcherCardCatalog`)를 참조하는 건 기존과 동일 — `ProfileStore` 는 이미 `using Wassup.Data` 로 `DefenderCatalog` 를 쓴다. 새 어셈블리 의존 없음.

## 완료 기준

- [x] compile clean, 콘솔 에러 0.
- [x] EditMode green (`ProfileStoreDefaultDeckTests` 10/10):
  - `LoadOrCreateAt(path, cat, deck, cards)` on missing file → `SelectedDeck()` non-null + `Validate` 통과 — `FreshInstall_SeedsSelectableValidDeck`
  - `LoadOrCreateAt(path, cat)` (덱 인자 없음) → 덱 0개 — `WithoutDeckArgs_SeedsNothing`
  - 이미 선택 덱이 있으면 **변경 없음** (무효 덱이어도) — `ExistingSelectedDeck_IsNeverOverwritten`
  - `selectedDeckId` 깨진 프로필 → 첫 덱으로 복구, 새 덱 추가 안 함 — `BrokenSelection_RepairsToFirstDeckWithoutAddingOne`
  - `defaultDeck` 0장 → 빈 덱 추가 안 함 — `AuthoredDeckEmpty_SeedsNothing`
  - 덱 없는 **기존** 프로필도 로드 시 구제 — `ExistingDecklessProfile_GetsSeededOnLoad`
  - `BuildDefaultDeck` 4케이스가 `ProfileStore` 대상으로 이동 후 green
- [x] 기존 EditMode 전량 green — 854 중 852 passed / **0 failed** / 2 skipped(기존 Ignore). `ProfileStoreTests` 는 덱 인자 없이 호출하는 기존 코드 그대로 통과(선택 인자 = 무회귀 증거).
- [x] 실제 에셋으로 신규 설치 재현 (임시 경로, 라이브 `profile.json` 무접촉):
  `deckSize=8` · squad = `archer,bastion,blocking_caster,bruiser,cannon,fire_caster,guardian` · deck = `ranger_atk,poke_needle,ranger_as,bouncy_bead,cost1_as,thornmail,cost1_hp,guardian_hp` · `selectedDeckId=deck_1` · **`LoadoutGate.Check = True`**. 이 커밋 전이면 `deck=NULL, GATE=False`.
- [x] 라이브 `profile.json` 무손상 확인 — squad 7 / decks 1 / `deck_1`.
- [x] 씬 diff 가 `cardCatalog`/`defaultDeck` 2줄뿐 — 무관한 WIP 미포함.
- [ ] `DEFAULT LOADOUT` 클릭 → 종전과 동일 결과 (스쿼드 7 + 덱 8). **사용자 Play 확인 대기** — 이 버튼은 프로필을 통째로 덮으므로 자동 검증하지 않았다.

확인 2026-07-16 — 기본 덱 시딩. 기본 덱 소유권을 `DefaultLoadoutButton`(dev 전용) → `ProfileStore` 로 이관해 신규 설치와 dev 버튼이 같은 정의를 쓴다. 시딩은 **덮어쓰기가 아니다** — 선택된 덱이 있으면 무효여도 보존하고 게이트가 알리게 둔다(리셋은 `DEFAULT LOADOUT` 의 역할). 테스트 파일은 `git mv` 로 이름만 바꿔 meta guid 를 보존했다.
