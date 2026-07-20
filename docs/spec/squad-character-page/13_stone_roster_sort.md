# 13 — 스톤 그리드 편성-먼저 정렬

> unit 10(`10_roster_sort_and_slot_selected.md`)이 *"스톤 모드 그리드 정렬은 범위 밖 — 후속 후보"* 로 남겨둔 항목을 소진한다.

## 목적

스톤 모드 그리드가 카탈로그 순서(`stone_001`~`stone_064`) 그대로라 **편성 중인 스톤 4개가 64종 사이에 흩어져** 있다. 유닛 모드와 동일하게 편성 중인 스톤을 목록 맨 앞으로 끌어올린다.

## 현재 상태

`SquadCharacterPageController.cs:154` — `browser.ShowStones(_stones)` 무정렬 raw 리스트.
`_stones` 는 `BuildLists()`(65-67행)가 `DreamstoneCatalog.AllIds()` 배열 순서 그대로 채운 것.

유닛 모드에는 이미 `SortedUnits()`(87-100행)가 있다 — 이 작업은 그 **정확한 미러링**이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs` — `SortedStones()` 신설, `EnterStoneMode`/`ToggleStone` 재-Show

## 구현

- **`SortedStones()`**: `SortedUnits()` 와 동형 stable 2-pass partition.
  - 1패스 = `squad.stoneIds` 슬롯 순서(헤더 스트립과 동일 순서)
  - 2패스 = 미편성 스톤을 카탈로그 순서 그대로
  - `squad` 가 null 이면 `_stones` 를 그대로 반환(`SortedUnits` 의 90행 가드와 동형)
- **호출 지점 2곳**:
  - `EnterStoneMode` **154행** — `ShowStones(_stones)` → `ShowStones(SortedStones())`
  - `ToggleStone` **201행** `Save()` 뒤 — `browser.ShowStones(SortedStones())` 추가 후 `RefreshStoneMode()`. 유닛 모드 144행과 동형으로 장착/해제 즉시 라이브 재정렬(셀 이동 자체가 편성 피드백).
- **`_stones` in-place 정렬 금지**: 157행의 `_stones[0].id` 기본 선택 폴백이 카탈로그 순서에 의존한다. 반드시 새 List 를 반환한다.
- **뱃지 기준은 불변**: "편성중" 뱃지는 `squad.stoneIds` 4슬롯 **전체** 멤버십(172행), 상세의 [장착]/[해제]는 **활성 슬롯만**(178-181행). 정렬은 이 두 계약을 건드리지 않는다.
- 그리드 재빌드로 스크롤은 톱으로 리셋 — unit 10 이 이미 수용한 동작(전면 그룹이 보이므로 문제 없음).

## 완료 기준

- compile 클린.
- Play: 스톤 슬롯 탭으로 스톤 모드 진입 시 편성 중인 스톤들이 그리드 맨 앞 + 헤더 슬롯 순서와 일치.
- Play: [장착] 시 해당 셀이 앞으로 이동, [해제] 시 미편성 그룹(카탈로그 위치)으로 복귀.
- 4슬롯이 모두 비어 있을 때도 정상 표시(카탈로그 순서 그대로, 예외 없음).
- 다른 슬롯에 있던 스톤을 현재 슬롯에 장착하는 "이동"(197-199행) 후에도 정렬이 슬롯 순서와 일치.
