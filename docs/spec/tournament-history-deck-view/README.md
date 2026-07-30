# tournament-history-deck-view

> 상태: 완료 2026-07-30 (units 0~4)
> EditMode 1658 tests green · 씬 배선 완료 · 실서버 Play e2e 확인(히스토리 22건 최신순, 랭킹 5슬롯, 덱보기 → 내 덱 왕복). 커밋: 125b02a1(unit 0) · fa82b1aa(units 1~3) · unit 4 는 이 커밋.

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

2. **룩은 결과 화면을 따른다.** `ResultScreen` 의 2컬럼 랭킹 화면이 기준이다.

    ~~랭킹 행은 `LeaderboardList` 를 공유한다~~ — **초안의 사실 오류를 정정한다(구현 중 확인).** `ResultScreen` 은 `result-screen-ranking-ui` 재설계 때 **자체 행 페인팅**(`RenderRows`/`CreateRow`)을 갖게 되어, `LeaderboardList` 와는 **행 모델(`Row`)과 `BuildRows` 만** 공유하고 `Render` 는 쓰지 않는다. 히스토리만 `LeaderboardList.Render` 의 소비처다. 따라서 두 화면의 행 모양은 이미 미세하게 다르며, 통일은 이 spec 범위 밖(후속 후보).

3. **진입 = 최근 토너먼트 자동 포커스.** 목록을 받으면 `createdTime` **내림차순**으로 정렬해 첫 항목을 선택하고 그 랭킹을 바로 부른다. 서버 정렬 순서에 기대지 않는다. 목록이 비면 좌우 모두 "참가한 토너먼트가 없습니다" 안내.

4. **`createdTime` 은 epoch 밀리초 문자열이다.** swagger 의 `format: date-time` 을 믿지 않는다 — dev 서버 실측값은 `"1785419835370"` 이다. 파서는 epoch(ms/s)와 ISO-8601 을 **둘 다** 받고, 정렬과 표시가 그 하나를 공유한다. 갈라지면 "화면엔 날짜가 있는데 정렬은 맨 뒤" 가 된다.

5. **목록 행에 일시를 병기한다.** 현행 `FormatDate`(`yyyy.MM.dd`)를 **재설계에서 유실시키지 않는다.** 다만 목록이 선택 UI 가 되고 최신순 정렬이 붙으므로 같은 날 참가가 구분되어야 한다 — `yyyy.MM.dd HH:mm` (로컬 시각)으로 확장한다. 파싱 실패는 빈 문자열(표시 전용 필드라 행을 죽이지 않는다).

6. **랭킹은 선택 시점에 부른다.** 캐시하지 않는다(같은 항목 재선택도 재조회). 모든 응답은 **epoch 가드** — 이전 선택의 응답이 늦게 와서 지금 우측을 덮어쓰면 안 된다(기존 팝업이 쓰던 규칙 그대로 승계).

7. **슬롯은 `maxEntryCount` 만큼 전부 그린다.** 현 서버 최대 5명이고 미참가 슬롯은 `대기 중...`. 기존 `BuildRows` 동작 그대로 — 하드코딩 5 를 새로 심지 않는다.

8. **덱보기는 옵트인 행 액션.** `Render(content, rows, onDeckView = null)` — 콜백을 넘긴 호출자에게만 버튼이 생긴다. 계약 2 정정에 따라 지금 `Render` 의 소비처는 히스토리 하나뿐이라 결과 화면이 영향받을 여지는 **현재는 없지만**, 공유 컴포넌트에 무조건 켜지는 UI 를 심지 않는다는 원칙은 유지한다(다음 소비처가 생길 때 비용이 0). `대기 중...` 슬롯에는 버튼이 없다. 실제 참가자 행에는 **덱 정보 유무와 무관하게** 버튼을 노출하고, 없으면 팝업이 "덱 정보 없음"을 말한다 — 버튼이 상황에 따라 사라지면 그게 더 혼란스럽다.

9. **DeckInfo 팝업은 순수 프레젠테이션이다.** 입력은 `TournamentDeckInfo.Payload`(+ 표시용 참가자 이름)뿐이고, 네트워크/세션/프로필을 모른다. 파싱은 호출자가 한다. 같은 팝업이 나중에 다른 출처(내 덱 미리보기 등)에도 그대로 쓰인다.

10. **어떤 데이터가 와도 그린다.** 아래 전부 정상 입력이고 예외/빈 화면/크래시가 없어야 한다. EditMode 로 고정한다.
   - `payload == null` → 전 탭 "덱 정보가 없습니다"
   - 빈 배열 → 그 섹션만 "없음"
   - 카탈로그가 모르는 id → **슬롯을 유지**하고 raw id + 플레이스홀더로 표시 (버리지 않는다)
   - 카탈로그 자체가 null(미배선) → id 만으로 렌더
   - 개수가 예상과 다름(유닛 9개, 카드 12장 등) → **가변 개수로 전부** 그린다

11. **고정 슬롯 컴포넌트를 재사용하지 않는다.** `DreamcatcherDeckStrip` 은 슬롯을 `DeckRules.EffectiveDeckSize` 개만 만들고 초과분을 잘라내며 `12/10` 같은 유효성 문구를 붙인다 — 남의 덱을 그리는 데 쓰면 조용히 잘린다. 계약 10 을 만족하는 **가변 그리드**를 새로 만든다.

12. **프리셋 저장은 스쿼드/드림캐쳐 버튼을 분리한다.** 이 spec 에서는 하단 영역의 자리와 모양을 만들었고, 후속 `deck-info-preset-apply` 에서 동일 폭의 두 버튼과 대상별 활성 조건·적용 동작을 연결했다. 각 버튼은 활성 탭과 무관하게 자기 종류만 요청하며, 해당 종류에 적용할 항목이 없을 때만 비활성이다.

    **내 덱에서는 버튼을 아예 숨긴다**(2026-07-30 사용자 결정). 내가 그때 쓴 덱을 내 프로필에 다시 쓰는 건 no-op 이라, 비활성으로 남겨두면 "왜 나만 안 되나"로 읽힌다. 판정은 호출자 몫이다 — 팝업은 그게 누구 덱인지 모르므로(계약 9) `Show(payload, title, allowPresetApply)` 로 받고, 히스토리 패널이 `BuildRows` 가 이미 계산해 둔 `Row.IsPlayer` 를 뒤집어 넘긴다. 숨길 때는 목록이 그 자리까지 내려온다(끄기만 하면 죽은 공간이 남는다).

    적용은 `deck-info-preset-apply` 에서 구현됐다. 삭제된 옛 `PresetApply.WriteToProfile` 처럼 확정 편성을 즉시 덮어쓰지 않는다. 누른 버튼 종류의 새 빈 프리셋만 만들고 랭커의 유닛·드림스톤 또는 카드를 **미저장 작업본**에 채운 뒤 해당 페이지로 이동한다. 사용자가 `[저장]` 해야 기록되고 `[되돌리기]` 하면 빈 프리셋으로 돌아간다.

13. **ECS 접점 없음.** 전부 MonoBehaviour 계층(UI + Core/Api). 배틀 시뮬레이션 무관.

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

- 결과 화면(`ResultScreen`)에도 덱보기 노출 — 계약 8 의 옵트인 스위치를 켜기만 하면 된다. 판 직후 상대 덱을 보는 흐름이 자연스러운지는 별도 판단.
- 랭킹 캐시 — 같은 토너먼트를 오가며 볼 때 재조회를 줄인다. 지금은 단순함 우선.
- 내 덱 미리보기에 DeckInfo 팝업 재사용 — 계약 9 가 이미 허용한다.
- **카드 문안 정합** — 이 팝업은 `card.description` **원문**을 쓴다. 게임 내 다른 카드 표면은 `DreamcatcherCardText` 를 거쳐 축/타입 헤더와 "○○ 전용" 부착 제한 줄을 붙인다. 남의 덱을 볼 때 부착 제한이 안 보이는 것은 **의식적 선택**(unit 3: SO 를 그대로 읽는다)이지, 누락이 아니다. 필요해지면 그 포매터를 태운다. Scope: S
