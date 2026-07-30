# tournament-history-deck-view

> 상태: 초안 2026-07-30 (승인 대기)

## 한 줄

로비 히스토리를 **1뎁스 2컬럼**(좌 토너먼트 목록 / 우 랭킹)으로 재설계하고, 랭킹 행의 **덱보기** 버튼으로 그 참가자의 덱을 여는 **DeckInfo 팝업**을 만든다. 덱 데이터는 `tournament-deck-info` 가 서버에 쌓아둔 `entries[].deckInfo` 다.

## 배경 (왜)

현재 히스토리는 **패널 → 행 클릭 → 모달 팝업**의 2뎁스다(`TournamentHistoryPanel` → `TournamentDetailPopup`). 랭킹을 보려면 매번 모달을 열고 닫아야 하고, 토너먼트를 갈아타며 비교할 수 없다. 목록과 랭킹을 한 화면에 놓으면 선택이 곧 비교가 된다.

그리고 `tournament-deck-info` 로 `deckInfo` 가 서버에 쌓이기 시작했지만 **아직 아무 화면도 그리지 않는다.** 이 spec 이 그 첫 소비처이고, 동시에 그 spec 이 못 돌린 라이브 검증의 확인 수단이다.

## 검증 질문

1. 히스토리 버튼을 누르면 가장 최근 토너먼트의 랭킹이 **곧바로** 보이는가? 다른 토너먼트를 고르면 우측만 갈아끼워지는가?
2. 랭킹의 아무 참가자나 덱보기를 눌렀을 때, 그 사람이 그 판에 들고 간 유닛·드림스톤·카드가 보이는가?
3. **어떤 데이터가 와도** DeckInfo 팝업이 깨지지 않는가 — 덱 정보 없음, 빈 목록, 카탈로그가 모르는 id, 예상보다 많은/적은 개수.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 히스토리 재설계 | `0_two_column_history.md` | 1뎁스 2컬럼 전환. 좌 목록 + 우 랭킹, 최근 자동 포커스, 선택 시 랭킹 fetch. `TournamentDetailPopup` 은퇴 |
| 1 | 덱보기 진입점 | `1_deck_view_row_action.md` | `LeaderboardList` 행에 **옵트인** 액션 버튼 + `Row` 에 덱 문자열 적재 |
| 2 | DeckInfo 팝업 셸 | `2_deck_info_popup_shell.md` | 탭 프레임(스쿼드/드림캐쳐) + 좌 상세 / 우 목록 레이아웃 + **견고성 계약** + 스쿼드 탭(유닛·드림스톤) |
| 3 | 드림캐쳐 탭 | `3_dreamcatcher_tab.md` | 카드 목록 + 카드 상세(art/설명) |
| 4 | 배선 + 검증 | `4_wiring_and_verify.md` | 씬 배선(카탈로그 3종), **라이브 왕복 검증**, Play e2e, handoff |

> **라이브 검증은 unit 4 에 있다** (2026-07-30 사용자 결정 — "히스토리에서 검증하자"). `tournament-deck-info` 가 남긴 미확인 2건(왕복 성립·0점 덮어쓰기)은 히스토리 화면 자체가 확인 수단이므로 선행 유닛으로 분리하지 않는다.

## Feature-wide 계약

1. **1뎁스.** 목록과 랭킹은 같은 패널 안 두 컬럼이다. `TournamentDetailPopup` 은 **은퇴**한다(모달 랭킹 없음). DeckInfo 팝업만 그 위에 뜨는 모달이다.

2. **룩은 결과 팝업을 따른다.** `ResultScreen` 의 2컬럼 랭킹 화면이 기준이고, 랭킹 행은 지금처럼 `LeaderboardList` 를 공유한다 — 결과 화면과 히스토리가 같은 룩을 유지한다(중복 0).

3. **진입 = 최근 토너먼트 자동 포커스.** 목록을 받으면 `createdTime` **내림차순**으로 정렬해 첫 항목을 선택하고 그 랭킹을 바로 부른다. 서버 정렬 순서에 기대지 않는다. 목록이 비면 좌우 모두 "참가한 토너먼트가 없습니다" 안내.

4. **목록 행에 일시를 병기한다.** 현행 `FormatDate`(`yyyy.MM.dd`)를 **재설계에서 유실시키지 않는다.** 다만 목록이 선택 UI 가 되고 최신순 정렬이 붙으므로 같은 날 참가가 구분되어야 한다 — `yyyy.MM.dd HH:mm` (로컬 시각)으로 확장한다. 파싱 실패는 빈 문자열(표시 전용 필드라 행을 죽이지 않는다).

5. **랭킹은 선택 시점에 부른다.** 캐시하지 않는다(같은 항목 재선택도 재조회). 모든 응답은 **epoch 가드** — 이전 선택의 응답이 늦게 와서 지금 우측을 덮어쓰면 안 된다(기존 팝업이 쓰던 규칙 그대로 승계).

6. **슬롯은 `maxEntryCount` 만큼 전부 그린다.** 현 서버 최대 5명이고 미참가 슬롯은 `대기 중...`. 기존 `BuildRows` 동작 그대로 — 하드코딩 5 를 새로 심지 않는다.

7. **덱보기는 옵트인 행 액션.** `LeaderboardList` 는 결과 화면과 공유하므로 버튼을 무조건 켜면 배틀 결과 화면에도 뜬다. **히스토리에서만 켠다.** `대기 중...` 슬롯에는 버튼이 없다. 실제 참가자 행에는 **덱 정보 유무와 무관하게** 버튼을 노출하고, 없으면 팝업이 "덱 정보 없음"을 말한다 — 버튼이 상황에 따라 사라지면 그게 더 혼란스럽다.

8. **DeckInfo 팝업은 순수 프레젠테이션이다.** 입력은 `TournamentDeckInfo.Payload`(+ 표시용 참가자 이름)뿐이고, 네트워크/세션/프로필을 모른다. 파싱은 호출자가 한다. 같은 팝업이 나중에 다른 출처(내 덱 미리보기 등)에도 그대로 쓰인다.

9. **어떤 데이터가 와도 그린다.** 아래 전부 정상 입력이고 예외/빈 화면/크래시가 없어야 한다. EditMode 로 고정한다.
   - `payload == null` → 전 탭 "덱 정보가 없습니다"
   - 빈 배열 → 그 섹션만 "없음"
   - 카탈로그가 모르는 id → **슬롯을 유지**하고 raw id + 플레이스홀더로 표시 (버리지 않는다)
   - 카탈로그 자체가 null(미배선) → id 만으로 렌더
   - 개수가 예상과 다름(유닛 9개, 카드 12장 등) → **가변 개수로 전부** 그린다

10. **고정 슬롯 컴포넌트를 재사용하지 않는다.** `DreamcatcherDeckStrip` 은 슬롯을 `DeckRules.EffectiveDeckSize` 개만 만들고 초과분을 잘라내며 `12/10` 같은 유효성 문구를 붙인다 — 남의 덱을 그리는 데 쓰면 조용히 잘린다. 계약 9 를 만족하는 **가변 그리드**를 새로 만든다.

11. **각 탭에 "프리셋 적용" 버튼 영역을 잡는다 — 이번 spec 은 레이아웃까지다.** 스쿼드/드림캐쳐 탭 각각에 버튼을 배치하되 **비활성**(`interactable = false`)으로 둔다. 눌러도 아무 일도 안 하는 활성 버튼은 결함으로 신고된다. 자리를 지금 잡는 이유는 나중에 끼워 넣으면 두 탭의 레이아웃을 다시 짜야 하기 때문이다.

    기능은 별도 spec 이다. 그때 붙일 자리는 `Wassup.Core.PresetApply.WriteToProfile(profile, unitIds, cardIds)` — **단, 그 헬퍼는 드림스톤을 건드리지 않는다**(현재 유닛 7슬롯 + 카드만 쓴다). 이 팝업의 페이로드에는 스톤이 들어 있으므로, 후속 spec 은 "스톤도 적용할 것인가"를 먼저 판단해야 한다.

12. **ECS 접점 없음.** 전부 MonoBehaviour 계층(UI + Core/Api). 배틀 시뮬레이션 무관.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설이나 생성→렌더 경로 변경이 없다. 로비 UI + 기존 네트워크 클라이언트 소비.

## 재사용 지도

| 필요한 것 | 기존 자산 |
|---|---|
| 랭킹 행 렌더 | `Assets/_Project/Scripts/UI/LeaderboardList.cs` (`Row` / `BuildRows` / `Render`) |
| 목록·상태(로딩/빈/실패) | `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs` |
| 2컬럼 랭킹 룩 | `Assets/_Project/Scripts/UI/ResultScreen.cs`, spec `docs/spec/result-screen-ranking-ui/` |
| 좌 상세 + 우 목록 패턴 | `SquadCharacterPageController` / `DreamcatcherDeckPageController` (구조 참고, 컴포넌트 직접 재사용 아님) |
| id → 이름·아트 해석 | `DefenderCatalog` / `DreamstoneCatalog` / `DreamcatcherCardCatalog` 의 `ById` |
| 페이로드 해석 | `TournamentDeckInfo.Deserialize` (`tournament-deck-info` unit 0) |
| 자기-빌드 캔버스 패턴 | `TournamentDetailPopup`(은퇴 예정이나 빌드 패턴은 참고), `UiCanvasSetup` / `UiRoundedSprite` / `UiLayer` |

## 후속 후보

- 결과 화면(`ResultScreen`)에도 덱보기 노출 — 계약 7 의 옵트인 스위치를 켜기만 하면 된다. 판 직후 상대 덱을 보는 흐름이 자연스러운지는 별도 판단.
- 랭킹 캐시 — 같은 토너먼트를 오가며 볼 때 재조회를 줄인다. 지금은 단순함 우선.
- 내 덱 미리보기에 DeckInfo 팝업 재사용 — 계약 8 이 이미 허용한다.
- **프리셋 적용 기능** (별도 spec) — 계약 11 이 잡아둔 버튼을 실제로 연결한다. 본 프로필의 선택 스쿼드·덱에 쓰는 것은 `PresetApply.WriteToProfile` 이 이미 한다. 판단이 필요한 지점: (a) 드림스톤 — 헬퍼가 안 건드린다, (b) 내 카탈로그에 없는 id 가 섞인 덱을 적용하면 어떻게 되는가, (c) 적용 후 저장 시점(`ProfileStore.Save`)과 확인 절차. Scope: S~M
