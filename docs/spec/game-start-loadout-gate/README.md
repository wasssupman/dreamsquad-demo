# game-start-loadout-gate — 게임 시작 전 로드아웃 충족 게이트

> 상태: **units 0~1 완료 2026-07-16 · unit 2 미구현** (커밋: 0 `986efa09`)
> 선행: `squad-loadout`(완료) · `dreamcatcher-deck-builder`(완료) · `outgame-login-gate`(units 0~6)
> 브레인스토밍 결정 2026-07-16: 스쿼드 기준 = 정확히 7명 · 팝업 = 미충족 나열 + 해당 패널 이동 버튼 · 테스트 모드는 게이트 없음
> 설계 critic 반영 2026-07-16: C1(카탈로그 null → 해결 불가 팝업) · M1(스쿼드 판정을 `SquadDraw.Resolve` 에 위임) · M3(신규 유저 차단 → 기본 덱 시딩으로 해소, unit 1 신설)

## 검증 질문

로비에서 START 를 눌렀을 때, **스쿼드 7명과 드림캐쳐 덱 8장이 모두 충족되면 게임이 시작되고**, 하나라도 모자라면 **씬 전환 없이 팝업이 떠서 무엇이 몇 개 모자란지 알려주고 해당 편성 화면으로 보내주는가?**

## 상위 목표

`OutgameMenuController.OnStartGame()` 은 현재 검증이 0줄이고 무조건 BattleScene 을 로드한다. 조건 미충족은 씬 로드 **이후** 조용한 열화로 나타난다:

- 덱이 무효/미선택 → `DreamcatcherHandController.ResolveAttachDeck()` 이 **빈 목록** 반환 (fallback 덱은 2026-07-15 의도적으로 제거됨). 부착 카드 0장으로 매치 진행.
- 스쿼드가 3명 → 3명으로 그냥 시작. 비어 있으면 레거시 draft 로 조용히 폴백.

이 spec 은 **조용한 열화를 클릭 시점의 명시적 안내로 바꾼다.** 스코프는 **게이트 판정 + 팝업 안내**뿐이다. 덱 규칙 변경·스쿼드 저장 게이트·폴백 제거는 전부 범위 밖.

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_loadout_gate_rules.md` | 순수 `LoadoutGate.Check` + `LoadoutShortfall` + EditMode 테스트 (씬 무관) |
| 1 | 구현 | `1_default_deck_seed.md` | 기본 덱 소유권을 `ProfileStore` 로 이관 + 신규/덱없는 프로필에 기본 덱 시딩 |
| 2 | 구현+wiring | `2_gate_popup_and_wiring.md` | `LoadoutGatePopup` 뷰 + `OnStartGame` 게이트 + 씬 배선 + Play 검증 |
| 3 | 인계 | `3_handoff_summary.md` | handoff (구현 종료 시) |

## Feature-wide 계약

- **게이트 지점은 `OnStartGame()` 하나**. 미충족이면 팝업을 띄우고 **`SceneTransition.Go` 를 부르지 않는다**. 충족이면 기존 동작 그대로.
- **게이트는 규칙을 재정의하지 않는다. 기존 소유자에게 위임하고 그 위에 판정만 얹는다.**
  - 덱: `DeckRules.Validate(cardIds, catalog, out reason)`. 실사용 규칙은 `DeckRuleConfig_Default.asset` 기준 **정확히 8장 · 타입 캡 없음**.
  - 스쿼드: **배치 대상은 `SquadDraw.Resolve`**(빈칸 제거 → dedup → `FieldCount` 컷)가 소유한다 — `GameManager` 가 매치 시작 시 부르는 바로 그 함수다. 게이트는 Resolve 결과를 `DefenderCatalog.ById` 로 해석해 개수만 센다. 로직을 복제하면 게이트와 실제 배치가 어긋난다 (critic M1: 복제본은 이미 "슬라이스 후 dedup" 으로 드리프트해 있었다).
  - 개수 기준은 `min(SquadSave.SlotCount, SquadDraw.FieldCount)`. 둘 다 독립 하드코딩된 7이라, 한쪽만 바뀌어도 요구치가 도달 불가능해지지 않게 낮은 쪽을 쓴다.
- **`FilledCount()` 를 쓰지 않는다** — 빈 문자열이 아닌 슬롯을 셀 뿐이라 **stale 유닛 id(리네임 후)나 중복도 "충족"으로 통과**시키고, 그러면 `GameManager` 가 약속보다 적게 배치하거나 유닛 0개로 resolve 해서 draft 로 조용히 폴백한다. 게이트가 잡아야 할 바로 그 실패다.
- **별도 `SquadRules` 계층을 만들지 않는다** (호출처 1개 — 제약 8). 스쿼드 저장 게이트가 나중에 필요해지면 그때 `LoadoutGate` 에서 가져다 쓴다.
- **카탈로그 null 은 shortfall 이 아니라 배선 오류다.** `DefenderCatalog`/`DreamcatcherCardCatalog` 가 미배선이면 플레이어가 절대 못 고치는 요구치가 뜬다 — 특히 카드 카탈로그 null 은 `DeckRules.EffectiveDeckSize` 가 폴백 상수 **10**을 쓰게 만들어 "덱 8/10" 을 요구하는데, 덱 빌더는 8장에서 추가를 막는다(**영구 잠금**). 호출자가 세 참조(`gatePopup`/`catalog`/`cardCatalog`) 전부를 사전 차단하고 LogError 한다. `LoadoutGate.Check` 는 이 사전조건을 문서화하고 자체 분기를 두지 않는다.
- **shortfall 은 `reason` 을 함께 싣는다.** 카운트만 노출하면 "8장인데 카드 id 가 무효" 인 덱이 `8/8` 로 보여 사용자가 무한 루프에 빠진다. 카운트가 어긋나면 `{have}/{need}`, 아니면 `reason` 을 그대로 노출한다.
- **팝업은 네비게이션을 모른다.** `Show(shortfalls, onGoSquad, onGoDeck)` 로 콜백만 받는다. 패널 가시성의 유일한 소유자가 `OutgameMenuController` 라는 기존 계약(`OutgameMenuController.cs:20-22`)을 유지한다.
- **참조 미배선 = LogError 후 차단** (fail-loud). 조용히 시작되는 깨진 매치를 없애는 게 목적이므로, 배선이 깨졌을 때 게이트를 통과시키지 않는다. 단 배선 오류를 **플레이어가 고칠 수 있는 shortfall 로 위장시키지 않는다** (위 카탈로그 계약).
- **신규 프로필에도 기본 덱을 시딩한다** (사용자 결정 2026-07-16, unit 1). `ProfileStore` 는 스쿼드만 시딩하고 덱은 만들지 않았다(`dreamcatcher-deck-builder` 의 "신규 프로필 = 덱 0개"). 게이트를 그대로 달면 **모든 신규 유저의 첫 START 가 차단**돼 `ProfileStore` 가 명문화한 "out of the box 플레이 가능" 전제가 덱 쪽에서 깨진다. 스쿼드와 대칭으로 맞춘다.
- **기본 덱의 소유자는 `ProfileStore`** (unit 1 이관). 현재는 dev 전용 `DefaultLoadoutButton` 이 유일한 저작처인데, 그 경로는 비-dev 빌드에 존재하지 않는다. 신규 설치가 의존할 정의를 dev 버튼에 둘 수 없다.
- **시딩은 플레이어 데이터를 덮어쓰지 않는다.** 선택된 덱이 있으면 그대로 둔다 — 규칙 변경(예: `deckSize` 8→10)으로 기존 덱이 무효가 돼도 조용히 갈아엎지 않고 게이트가 "8/10" 으로 알려 빌더에서 고치게 한다. `EnsureDefaultSquad` 의 "채워진 스쿼드는 덮어쓰지 않음" 과 같은 규율.
- **팝업은 새 캔버스를 만들지 않는다.** `MenuCanvas` 자식으로 코드 빌드 + `SetAsLastSibling()` — 로비의 기존 팝업 idiom(`SquadBuilderView.OpenPicker`, `DreamcatcherDeckBuilderView.ShowCardPopup`)과 동일. 프로젝트의 sorting-order 레지스트리는 이미 불일치가 있어 값을 하나 더 늘리지 않는다.
- **문구는 한글.** `battle-ui-korean`(2026-07-09)이 TMP 전역 폴백에 Jua SDF 를 넣어 로비에서도 한글이 렌더된다. `outgame-login-gate` 의 "UI 텍스트는 영문" 제약은 그보다 이틀 전 문서라 이 spec 에 적용되지 않는다.
- **테스트 모드는 게이트 없음** (사용자 결정 2026-07-16). `TestModePanelView.StartPlan` 은 동일한 `SceneTransition.Go(Battle)` 를 부르는 두 번째 문이지만 dev 트레이의 QA 수단이다. 디펜더는 자체 프리셋 경로(`StartTestModeMatch`)로 우회하지만 **덱은 우회하지 않는다** — `ResolveAttachDeck` 은 테스트 모드 여부와 무관하게 `SelectedDeck()` 을 읽으므로 덱 없는 테스트 모드는 빈 부착 덱으로 돈다. 그건 아래 "폴백 유지" 계약에 맡긴다.
- **기존 폴백을 제거하지 않는다.** `GameManager` 의 draft 폴백과 `ResolveAttachDeck` 의 빈 목록은 그대로 둔다 — 테스트 모드와 BattleScene 직접 Play 가 게이트를 우회하므로 방어선을 없애면 안 된다.

## 후속 후보 (본 spec 범위 밖)

- **스쿼드 저장 게이트**: `SquadBuilderView.OnSave()` 는 규칙 검사 없이 무조건 저장한다(덱 빌더는 유효할 때만 저장 버튼 활성 — 정반대 정책). 애초에 무효 스쿼드가 저장되지 않게 막는 안.
- **낡은 주석 정정**: `PlayerProfile.cs:112` 가 "exactly 10, unique<=2" 라고 적어놨지만 라이브 에셋은 8장/타입캡 없음. **`DeckRules.cs:5-10` 은 건드리지 말 것** — 그 주석은 정확하고("숫자는 카탈로그의 `DeckRuleConfig` 에서 오고 const 는 미배선 시 폴백"), 위 카탈로그-null 함정을 경고하는 유일한 문서다.
- **`DreamcatcherDeckBuilderView.cs:45 DeckColumns = 10`** 하드코딩이 `deckSize=8` 과 어긋난다. `Refresh()` 는 `_working.Count` 개만 생성하므로 빈 슬롯이 남는 건 아니고, 셀 폭이 10열 기준으로 계산돼 8장이 중앙 정렬되며 프레임이 행보다 넓게 남는다. 기능 버그는 아니지만 규칙 변경이 UI 에 자동 반영되지 않는 지점.
- **7 이 두 군데 독립 하드코딩**: `SquadSave.SlotCount` 와 `SquadDraw.FieldCount`. 공유 상수가 아니라 한쪽만 바꾸면 조용히 어긋난다. (게이트는 `min()` 으로 방어하지만 근본 해소는 아니다.)
- **`UiOverlay.Dim` 톤**: alpha 0.92 는 전체화면 takeover 용이라 "유닛 2명 부족" 안내 팝업엔 과할 수 있다. 공유 상수라 이 spec 에서 바꾸지 않고 Play 육안 확인에 맡긴다 — 무겁게 읽히면 별도 결정.
- 게이트 미충족 시 START 버튼 자체를 딤 처리 (현재는 눌러야 사유를 안다)
