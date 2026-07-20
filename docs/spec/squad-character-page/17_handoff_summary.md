# 17 — handoff (units 11~16 가독성 패스)

> units 0~10 의 handoff 는 `8_handoff_summary.md`. 이 문서는 2026-07-20 가독성 패스만 다룬다.

## Commit

| 해시 | 제목 |
|---|---|
| `4069aa19` | docs — units 11~13 스펙 |
| `4af6bd81` | unit 11 — 설명문 24→34 확대 + 스탯 2열 |
| `4df58f38` | unit 12 — 그리드 셀 라벨 밴드 분리 |
| `a4d16c61` | unit 13 — 스톤 그리드 편성-먼저 정렬 |
| `5c183228` | unit 14 — 폴백 아이콘이 카드에 잠기는 문제 (픽스) |
| `f92ce093` | unit 15 — 스탯 라벨/수치 타이포 위계 |
| `bb929581` | unit 16 — 스탯 컬럼 폭 고정으로 수치 우측 정렬 (픽스) |
| `cf28ff64` | docs(lessons) — 도메인 리로드 고아 UI 함정 |

## Implemented

- 상세 카드 설명문 `76px/24` → `148px/34`. 스탯 5행(190px)을 2열 3행(114px)으로 압축해 76px 회수.
- 스탯 행 간격 44, 라벨 `24/Normal/#99A3B8` · 수치 `30/Bold/#FFFFFF`, 칸 폭 균등 고정(수치 우측 정렬).
- 그리드 셀 `150×178` → `150×200`. 아이콘/라벨을 겹치지 않는 2밴드 컬럼으로 분리, 라벨에 `rgba(0,0,0,0.72)` 밴드.
- 스톤 그리드가 편성 중인 스톤을 슬롯 순서로 선두 배치(`SortedStones`), 장착/해제 시 라이브 재정렬.
- 스톤 상세 아이콘(`PortraitFallback`)을 카드 위 자유 영역으로 이동 — 앵커를 `cardHeight` 에서 파생.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/SquadUnitDetailView.cs` — units 11·15·16 (카드 내부 전부)
- `Assets/_Project/Scripts/UI/Outgame/SquadRosterBrowser.cs` — unit 12 (셀 구성)
- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs` — unit 13 (`SortedStones`)
- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPage.cs` — unit 14 (폴백 앵커)

## Verified

- compile 클린(에러 0). 씬 저장 없음 — 전 UI 가 런타임 생성이라 씬 변경분 없다.
- Play 실측: 카드 자식 합계 442 + 패딩/간격 130 = **572 / 572.4**. 최장 desc(가디언 62자) 3줄 123px ≤ 148px.
- Play 실측: 셀 inner 138×188, 초상화 `6→132` / 라벨 밴드 `142→188` — 겹침 0. 7열 유지.
- Play 실측: 스톤 정렬 `[stone_004, stone_001]` → 선두 2칸 일치. 중복 주입 시 64셀 0중복. 빈 슬롯 시 카탈로그 순서.
- Play 실측: 폴백 아이콘 `692→992` vs 카드 상단 `605` — 겹침 0.
- Play 실측: 스탯 셀 폭 전부 282, 좌측 값 끝 304 / 우측 631 (유닛 전환에도 불변).
- 2026-07-20 사용자 Play 확인.

## Notes (되돌리지 말 것)

- **폰트 34 는 "슬롯을 채우는" 값이다.** 30/32 는 2줄(71~76px)이라 148px 슬롯에 77px 가 비어 설명과 [출전] 사이가 벌어진다. 이 폰트의 한글 글립은 전각이 아니라 **약 0.65em** — 폭 계산 시 전각으로 가정하면 줄 수를 과대추정한다(초기 스펙이 이 오판으로 "지금도 잘린다"고 썼다가 정정).
- **스탯 압축의 근거는 "잘려서"가 아니라 "키울 자리가 없어서"다.** 5행 유지 시 설명란 몫은 48px 뿐이라 font 30 의 2줄조차 못 담는다.
- **`MakeHalfWidth`(preferredWidth 0 + flexibleWidth 1)를 `StatCell` 과 `Spacer` 양쪽에서 빼지 말 것.** `HorizontalLayoutGroup` 은 `LayoutGroup` 이라 `ILayoutElement` 이기도 해서 preferred 를 자식 텍스트 폭의 합으로 보고한다 — 안 덮으면 컬럼 경계가 글자 수에 따라 행마다 움직인다. 스페이서를 빼면 홀수 행이 다시 어긋난다.
- **`_statRowGos` 는 행 단위(3), `_statValues` 는 셀 단위(5).** `SetUnitPartsActive` 가 스톤 모드에서 스탯을 숨기는 경로다.
- **`SortedStones` 는 `_stones` 를 in-place 정렬하지 않는다.** `EnterStoneMode` 의 `_stones[0]` 폴백이 카탈로그 순서에 의존한다. 중복 가드는 `SetStoneSlot` 이 슬롯 중복을 허용하기 때문(유일성은 UI 만 강제).
- **폴백 앵커에 리터럴 `0.78` 을 박지 말 것.** `(cardHeight + 1) / 2` 파생이라 카드를 키워도 따라 올라간다 — 이번 회귀가 정확히 "카드만 키우고 폴백은 그대로 둔" 데서 나왔다.
- **`cellSize` 는 SerializeField 지만 씬 직렬화 값이 없다.** `SquadCharacterPage.cs` 가 런타임 `AddComponent` 로 만들므로 코드 기본값이 곧 실제 값이다.

## Follow-up

- **드림스톤 아이콘 PNG 에 알파 채널이 없다** (`colortype 2`). 상세 패널에서 아이콘 뒤 검은 사각형으로 보인다. 아트 재출력 또는 임포터 알파 생성 필요 — `14_portrait_fallback_above_card.md` 후속 후보 참조. **미해결.**
- 실기기(Android) 가독성 확인 — 라벨 `#99A3B8`/24 가 작은 화면에서 충분한지.
- 검증 함정: 이 페이지는 전부 런타임 생성이라 Play 중 재컴파일 시 **도메인 리로드가 고아 자식을 남긴다**(`_built`/`_grid` 는 살고 `_cells` 는 비워짐). `docs/reference/lessons/01-unity-mcp-operation.md` 참조.
