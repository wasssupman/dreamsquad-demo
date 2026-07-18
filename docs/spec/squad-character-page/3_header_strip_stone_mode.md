# 3 — 헤더 편성/스톤 스트립 + 스톤 모드

## 목적

우측 상단 헤더 = 편성 7 슬롯 + 스톤 4 슬롯(항상 노출, "편성 현황"). 스톤 슬롯 탭 → 브라우저가 **스톤 모드**로 전환(같은 그리드에 64 스톤), 상세 패널은 선택 스톤 정보. 모달 없이 단일 면 유지.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/SquadHeaderStrip.cs` (`Wassup.UI`)
- 신규 `Assets/_Project/Scripts/UI/Outgame/DreamstoneStyle.cs` (`Wassup.UI`) — 그레이드 프레임색 + effect 요약("ATK +7.5%"). SquadBuilderView 의 GradeColor/StoneSummary 로직 이관(unit 4에서 구 뷰 폐기 예정).
- 수정 `SquadRosterBrowser.cs` — `ShowStones(IReadOnlyList<DreamstoneData>)` 추가(같은 셀 기계, 엔트리=icon/grade프레임/effect라벨).
- 수정 `SquadUnitDetailView.cs` — `ShowStone(DreamstoneData, bool equipped)` 추가(spine off → 아이콘 크게, 배지행·스탯행 hide, grade/effect 표기, 버튼 "장착"/"해제"). `SetDeployState` 라벨 오버로드.

## 구현

`SquadHeaderStrip : MonoBehaviour`
- SerializeField: `DefenderCatalog catalog`, `DreamstoneCatalog stoneCatalog`, `TMP_FontAsset font`.
- `event Action<int> UnitSlotTapped;` `event Action<int> StoneSlotTapped;`
- `void Refresh(SquadSave squad)` — 7 유닛 슬롯(포트레이트/빈"+") + 4 스톤 슬롯(아이콘+grade색/빈"+") 재도장. `SquadSave.SlotCount`/`StoneSlotCount` 기준.
- `void SetActiveStoneSlot(int index)` — 스톤 모드 중 편집 대상 슬롯 하이라이트(-1 = 없음).
- 슬롯 절차적 생성(HorizontalLayoutGroup 2행 or 1행 그룹). 탭 → 각 이벤트.

`SquadRosterBrowser.ShowStones(stones)` — 엔트리 매핑(id, icon, `DreamstoneStyle.Frame(grade)`, `DreamstoneStyle.Summary`). 유닛/스톤은 상호배타 모드(직전 셀 clear).

`SquadUnitDetailView.ShowStone(stone, equipped)` — spine off, portraitFallback=아이콘, 배지행/스탯행 SetActive(false), 이름=displayName, summary=grade+effect, 버튼 라벨 "장착"/"해제". `ShowUnit`(기존 Show) 복귀 시 배지행/스탯행 재활성. 모드 해석은 orchestrator(unit 4)가 `ActionClicked`로.

## 완료 기준

- [x] 컴파일 클린(신규 .cs 2개 + 수정 2개 → scope=all refresh, 에러 0).
- [x] 헤더 7+4 슬롯 squad 도장(편성 4/7·스톤 2/4 등급색) + 탭 이벤트. 스톤 모드 = 브라우저 64 스톤 그리드 + 상세 스톤 정보(유닛 배지/스탯행 숨김) + 활성 스톤 슬롯 노란 아웃라인. 유닛 모드 복귀 시 원상.
- [x] `DreamstoneStyle`(Frame/Summary/GradeLabel) 공용화. 하드코딩 수치 0.
- [x] Play 오버레이 프리뷰로 유닛 모드 + 스톤 모드 2컷 시각 검증(2026-07-18).

> 구현 2026-07-18 · 커밋 대기 (컴파일 클린 + Play 프리뷰 유닛/스톤 2모드 시각 검증 통과).
