# 6 — 카드 그리드 덱-먼저 정렬 + 덱 슬롯 선택 표시

> 스쿼드 페이지의 `squad-character-page/10_roster_sort_and_slot_selected.md` 와 쌍 (두 페이지 공통 UX 결정).

## 목적

(1) 카드 그리드에서 **덱에 편성된 카드가 항상 목록 맨 앞**에 오도록 정렬한다. (2) 상세 패널에 떠 있는 카드가 덱에 있으면 **덱 스트립의 해당 슬롯에 선택 outline**을 표시한다(현재 스트립에는 선택 표시 위젯 자체가 없음 — 신설).

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs` — `SortedPool()` 신설, `OnEnable`/`AddCard`/`RemoveOccurrence` 재-Show, `RefreshAll`에서 스트립 선택 전달
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckStrip.cs` — 슬롯 outline 위젯 신설 + `SetSelected(string cardId)` (SquadHeaderStrip outline 패턴·노랑색 동일)

## 구현

- **정렬**: stable 2-pass partition. 1패스=`_working` 덱 순서(스트립과 동일 순서, 풀에 있는 카드만 — Subconscious는 덱에 있어도 그리드 비노출 계약 유지), 2패스=미편성 카드 카탈로그 순서 유지. **라이브 재정렬**: [덱에 추가]/[제거] 직후 `browser.ShowCards(SortedPool())` 재호출. legacy 저장 덱의 중복 id는 seen-set으로 1셀만.
- **스트립 선택 표시**: `SlotW`에 카드 id + outline 보관, `Refresh`가 채우고 `SetSelected(id)` 토글. 편집(in-memory `_working`) 기준 — Save 여부와 무관.
- 카테고리/타입 정렬·필터 툴바는 기존 후속 후보 유지(범위 밖).

## 완료 기준

- compile 클린.
- Play: 드림캐쳐 패널 진입 시 덱 카드들이 그리드 맨 앞 + 스트립 순서와 일치. [덱에 추가] 시 해당 셀이 앞으로 이동, [제거] 시 미편성 그룹(카탈로그 위치)으로 복귀.
- Play: 덱 카드를 (그리드 셀이든 슬롯이든) 선택하면 스트립 해당 슬롯에 노란 outline. 미편성 카드 선택 시 outline 없음.

2026-07-19 사용자 Play 확인 · 커밋 b86545ea
