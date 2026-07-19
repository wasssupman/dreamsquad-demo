# 10 — 컬렉션 편성-먼저 정렬 + 헤더 슬롯 선택 표시

> 드림캐쳐 덱 페이지의 `dreamcatcher-deck-page/6_pool_sort_and_slot_selected.md` 와 쌍 (두 페이지 공통 UX 결정).

## 목적

(1) 우측 컬렉션 그리드에서 **편성된 유닛이 항상 목록 맨 앞**에 오도록 정렬한다. (2) 상세 패널에 떠 있는 유닛이 편성 중이면 **헤더 스트립의 해당 슬롯에 선택 outline**을 표시한다. unit 9(슬롯 탭=선택)와 합쳐 "편성 현황 ↔ 상세 ↔ 컬렉션"의 대응이 눈에 보이게 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs` — `SortedUnits()` 신설, `EnterUnitMode`/`ToggleUnit` 재-Show, Refresh 양쪽에 헤더 선택 전달
- `Assets/_Project/Scripts/UI/Outgame/SquadHeaderStrip.cs` — `SetSelectedUnit(string)` + 유닛 슬롯 outline 적용 (기존 outline 위젯·`ActiveOutline` 색 재사용)

## 구현

- **정렬**: 컨트롤러가 리스트 소유 — stable 2-pass partition. 1패스=`squad.unitIds` 슬롯 순서(헤더와 동일 순서), 2패스=미편성 유닛 카탈로그 순서 유지. **라이브 재정렬**: `ToggleUnit`([출전]/[편성 해제]) 직후 `browser.ShowUnits(SortedUnits())` 재호출 — 불변식이 항상 유지(셀 이동 자체가 편성 피드백). 그리드 재빌드로 스크롤은 톱으로 리셋(전면 그룹이 보이므로 수용).
- **헤더 선택 표시**: `SlotW`에 유닛 id 보관, `Refresh`가 채우고 `SetSelectedUnit(id)`/`Refresh` 후 outline 토글. 시각은 스톤 활성 슬롯과 동일한 `ActiveOutline`(노랑) — "이 슬롯이 지금 상세 대상"이라는 같은 언어. **유닛 모드에서만** 표시(`RefreshStoneMode`는 null 전달).
- 스톤 모드 그리드 정렬은 범위 밖(장착 스톤은 4개뿐, 후속 후보).

## 완료 기준

- compile 클린.
- Play: 페이지 진입 시 편성 유닛들이 그리드 맨 앞 + 슬롯 순서와 일치. [출전] 시 해당 셀이 앞으로 이동, [편성 해제] 시 미편성 그룹(카탈로그 위치)으로 복귀.
- Play: 편성 유닛을 (그리드 셀이든 헤더 슬롯이든) 선택하면 헤더 해당 슬롯에 노란 outline. 미편성 유닛 선택 시 헤더 outline 없음. 스톤 모드 진입 시 유닛 outline 소등.

2026-07-19 사용자 Play 확인 · 커밋 ebfa923a
