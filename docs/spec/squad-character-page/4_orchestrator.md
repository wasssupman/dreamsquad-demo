# 4 — 오케스트레이터 (SquadCharacterPageController)

## 목적

상세뷰·브라우저·헤더를 소유하고 편성 상태(profile 선택 스쿼드)를 구동하는 컨트롤러. 브라우즈→상세, 출전 토글/dedup/append, 헤더 슬롯 탭 해제, 스톤 모드 전환/장착, 저장을 잇는다. 기존 모달 피커 흐름을 대체(unit 5에서 옛 SquadBuilderView 비활성).

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs` (`Wassup.UI`)

## 구현

`SquadCharacterPageController : MonoBehaviour`
- SerializeField: `DefenderCatalog catalog`, `DreamstoneCatalog stoneCatalog`, `PlayerProfileSO profileSO`, `SquadUnitDetailView detailView`, `SquadRosterBrowser browser`, `SquadHeaderStrip header`.
- `OnEnable`: 이벤트 1회 배선 + 리스트 빌드 + `NormalizeSlots` + 유닛 모드 진입(첫 편성 유닛 or 첫 유닛 선택).
- **유닛 모드**: `EntrySelected(id)`→선택 갱신+상세; `DeployClicked`→`ToggleUnit`(있으면 제거/없으면 첫 빈 슬롯 append, dedup=IndexOf, 만석 무시); `UnitSlotTapped(i)`→(스톤 모드면 유닛 복귀만) 찬 슬롯 quick 제거. 배지=편성중.
- **스톤 모드**: `StoneSlotTapped(i)`→진입/활성 슬롯 전환(`ShowStones`+`SetActiveStoneSlot`); `EntrySelected(id)`→선택 스톤 상세; `DeployClicked`→`ToggleStone`(활성 슬롯 토글, "one item one slot"=다른 슬롯 동일 id 이동). 배지=장착중.
- 편집마다 `ProfileStore.Save(profile)` 자동 저장. 스쿼드는 `profile.SelectedSquad()` 참조 직접 변경(옛 뷰와 동일).

## 완료 기준

- [x] 컴파일 클린(신규 .cs → scope=all refresh, 에러 0).
- [x] (unit 5 배선 후) 브라우즈→상세 갱신 실화면 확인. 출전/해제·스톤 장착·저장 경로 구현(사용자 조작 확인 남음).
- [x] `SetStoneSlot` 중복 정책 존중(강제 dedup 아님, 이동으로 처리). 하드코딩 수치 0.

> 구현 2026-07-18 · 커밋 대기 (unit 5와 함께). 컴파일 클린 + Play e2e.
