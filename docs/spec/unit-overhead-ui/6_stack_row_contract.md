# 6. 스택 행 계약 — 스타일/레이아웃/레지스트리/DTO [pure/data]

## 목적

스택 이상효과 아이콘 행의 토대: 뷰가 받는 plain DTO, kind→아이콘 레지스트리, 스타일 파라미터, 순수 레이아웃 오프셋. 코드가 아이콘 부재에도 무크래시(레지스트리 폴백)라 unit 7~8 이 아이콘(Codex, unit 9) 전에 진행 가능.

## 변경 대상

- **신규** `Assets/_Project/Scripts/Data/OverheadStackKind.cs` — `OverheadStackKind` enum(Fatigue/Heat, append-only) + `OverheadStackEntry`{kind,count} DTO.
- **신규** `Assets/_Project/Scripts/Data/StackIconRegistry.cs` — SO, `OverheadStackKind→Sprite`. `IconFor` 미매핑 시 null(뷰 생략).
- `Assets/_Project/Scripts/Data/UnitOverheadUiStyle.cs` — 스택행 파라미터(gap·아이콘높이·spacing·max·타일폭비·배지 색/plate/높이비).
- `Assets/_Project/Scripts/Presentation/UnitOverheadLayout.cs` — `StackRowBottom`(순수, DC행 위).
- `Assets/_Project/Tests/EditMode/UnitOverheadLayoutTests.cs` — `StackRowBottom` 케이스.

## 구현

- **DTO**: `OverheadStackEntry { OverheadStackKind kind; int count; }` — Presentation 이 Battle.StackKind 미참조(overhead-ui 계약). gather(unit 8)가 Battle.StackKind/HeatAccrual → OverheadStackKind 번역.
- **레지스트리**: `StackIconRegistry.IconFor(kind)` — Entry[] 선형 조회, 없으면 null. 아이콘↔코드 디커플링의 폴백 지점.
- **스타일**: `StackGap`·`StackIconHeight`·`StackSpacing`·`StackRowMax`·`StackRowTileWidthFraction`·`StackBadgeColor`·`StackBadgePlate`·`StackBadgeHeightFraction` (전부 clamp getter).
- **레이아웃**: `StackRowBottom(cardRowBottom, cardRowHeight, stackGap)` = 세 값의 NonNegative 합. cardRowHeight 0(카드 없음/적) → 스택행이 카드행 자리+gap 로 내려옴.

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- `UnitOverheadLayoutTests.StackRowBottom_*` green (카드 있음/없음/음수·NaN 방어).
- `Wassup/Stack Icon Registry` 메뉴로 SO 생성 가능(내용 채움은 unit 10).
- 아직 화면 변화 없음(뷰 소비는 unit 7, gather 는 unit 8).
