# 6 — 기본 로드아웃 버튼 (스쿼드·덱 디폴트 세팅)

## 목적

dev 트레이에 버튼 하나를 추가해 **선택 스쿼드와 드림캐쳐 덱을 기본값으로 되돌린다.** 완전 삭제가 아니라 "새 프로필과 같은 상태"로 세팅한다. 덱 규칙이 바뀌어 저장 덱이 무효화됐을 때(예: `deckSize` 10→8) 한 번에 정상 상태로 복귀하는 QA 수단.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs` — `CreateDefault` 를 public 으로 (기본 프로필 정의의 유일한 소유자를 유지)
- `Assets/_Project/Scripts/UI/Outgame/DefaultLoadoutButton.cs` (신규)
- `Assets/_Project/Tests/EditMode/DefaultLoadoutButtonTests.cs` (신규)
- `Assets/_Project/Scenes/OutgameScene.unity` (버튼 1개 + 결과 라벨 위치)

## 구현

`DefaultLoadoutButton : MonoBehaviour` — `DevTrayContent` 의 버튼에 붙어 프로필을 기본값으로 재작성한다.

1. `[SerializeField]` — `Button button` · `PlayerProfileSO profileSO` · `DefenderCatalog defenderCatalog` · `DreamcatcherCardCatalog cardCatalog` · `DreamcatcherDeck defaultDeck`.
2. 클릭 시:
   - `var p = ProfileStore.CreateDefault(defenderCatalog)` — **스쿼드 기본값은 기존 경로 재사용**(`EnsureDefaultSquad` = 카탈로그 앞 7개 시드, 스톤 슬롯은 빈 값). 여기 기본값을 새로 정의하지 않는다.
   - `p.dreamcatcherDecks = { BuildDefaultDeck(defaultDeck, DeckRules.EffectiveDeckSize(cardCatalog)) }` + `p.selectedDeckId` 설정.
   - `profileSO.profile = p` **먼저**, 그다음 `ProfileStore.Save(p)`.
3. `internal static DeckSave BuildDefaultDeck(DreamcatcherDeck deck, int deckSize)` — 순수 함수. `deck.cards` 를 순서대로 훑어 null 을 건너뛰고 `deckSize` 장까지만 담는다. EditMode 테스트 대상.

### 기본 덱의 출처

`DreamcatcherDeck_Default.asset`(10장)을 **기본 덱 저작처로 되살린다**. 이 에셋은 fallback 덱 폐기(2026-07-15, `DreamcatcherHandController.cs:192`) 이후 아무도 안 읽는 죽은 데이터였다. 현재 `deckSize=8` 이므로 앞 8장만 담기고 `farewell`/`guardian_fortress` 는 잘린다(사용자 결정 2026-07-15). 규칙이 바뀌면 자동으로 따라간다 — 에셋을 8장으로 편집하지 않는 이유다.

### 주의

- **`ProfileStore.Save` 는 반드시 새로 만든 프로필로 부른다.** 빈/오래된 in-memory SO 로 부르면 스쿼드까지 날아간다(기록된 함정). 그래서 `profileSO.profile` 교체를 저장보다 먼저 한다.
- `CreateDefault` 에 덱 시드를 넣지 않는다. 그건 신규 설치 경로(`LoadOrCreate`)도 함께 바꾸는 동작 변경이고 이 unit 의 범위 밖이다. 덱 시드는 이 버튼만의 책임이다.
- 확인 다이얼로그 없음 — `RESET ACCOUNT` 선례(내부 데모)를 따른다.
- 빌드 게이트·트레이 접힘은 `DevButtons`(`DevOnlyGroup`) / unit 5 가 이미 덮는다. 자체 게이트를 두지 않는다.
- `OutgameMenuController` / `LoginPanelView` 는 수정하지 않는다. 스쿼드·덱 빌더는 열 때 `profileSO` 를 다시 읽으므로 갱신 통지가 필요 없다(트레이는 로비 레이어에서만 보이고, 그때 패널은 닫혀 있다).

라벨은 ASCII `DEFAULT LOADOUT` (로비 폰트 제약, unit 5 참조). 위치는 `RESET ACCOUNT` 아래 y=-576, `StatRefreshResult` 는 -664 로 이동.

## 완료 기준

- [x] 클릭 시 `squads[0].unitIds` 가 카탈로그 앞 7개로, `dreamcatcherDecks[0].cardIds` 가 기본 덱 앞 8장으로 세팅된다. — Play 실측 `archer,bastion,blocking_caster,bruiser,cannon,fire_caster,guardian` / `ranger_atk…guardian_hp`.
- [x] `selectedSquadId`/`selectedDeckId` 가 그 둘을 가리킨다. — `squad_1` / `deck_1`.
- [x] 결과가 `profile.json` 디스크에 실제로 써진다. — 디스크 재확인 완료.
- [x] 세팅된 덱이 `DeckRules.Validate` 를 통과한다 (`deckSize=8` 기준). — `Validate = True (ok)`.
- [x] 스톤 슬롯은 빈 값 4개로 정규화된다. — `stones = ['', '', '', '']`.
- [x] EditMode 테스트 green: `BuildDefaultDeck` 이 (a) deckSize 로 자르고, (b) null 카드를 건너뛰고, (c) 카드가 deckSize 보다 적으면 있는 만큼만 담는다. — 전체 827 passed / 0 failed (null source 케이스 추가).
- [x] `OutgameMenuController` / `LoginPanelView` diff 0.
- [x] compile clean, 에디터 Play 검증(클릭 → 프로필 파일 확인 → 덱빌더 정상 노출). — 덱빌더 육안 확인은 사용자 몫으로 남음.

확인 2026-07-15 — 기본 로드아웃 버튼. **기본 스쿼드는 카탈로그 순서 앞 7개**라 큐레이션된 스타터가 아니다(`EnsureDefaultSquad` 의 기존 동작 그대로 — 이 unit 이 바꾸지 않았다). **드림스톤은 함께 비워진다** — 스톤이 `SquadSave` 소유라 `CreateDefault` 가 새 스쿼드를 만들면 빈 슬롯 4개가 된다. "유닛과 덱만" 이 아니라 "스쿼드 전체가 기본값" 이라는 뜻이므로, 스톤을 보존해야 하면 별도 결정이 필요하다.

`ResetAccountButton` 복제로 버튼을 만들었기에 **persistent onClick(`OnResetAccount`)이 딸려와 제거**했다 — 안 지웠으면 로드아웃 버튼이 계정까지 날렸다.
