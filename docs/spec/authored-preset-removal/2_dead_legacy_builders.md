# 2 — 사문화된 레거시 빌더 제거

## 목적

씬에 `m_Enabled: 0` 으로 남아 있는 구세대 편성 UI 2개를 제거한다. 후속 spec 이 `SquadSave`/`DeckSave` 타입을 개명하는데, 이 둘은 그 타입에 **쓰기**를 하는 유일한 잔존 코드라 남겨두면 개명 작업이 사문화 코드를 끌고 다녀야 한다.

## 변경 대상

**삭제** (`.cs.meta` 짝 포함):
- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`

**씬** (`OutgameScene.unity`, UnityMCP): 두 컴포넌트 인스턴스 제거
- `SquadBuilderView` — `:327~345` (`m_Enabled: 0`)
- `DreamcatcherDeckBuilderView` — `:906` 부근 (`m_Enabled: 0`)

두 컴포넌트가 붙어 있던 GameObject 와, 그 SerializeField 가 가리키던 하위 컨테이너(`slotsContainer` → `{fileID: 1665361532}`, `statusText` → `{fileID: 1402124747}` 등)가 **다른 컴포넌트에 의해 쓰이는지 먼저 확인**한다. 쓰이지 않으면 함께 제거, 쓰이면 컴포넌트만 제거.

## 구현

1. 두 컴포넌트가 현재 확실히 비활성이며 대체품이 살아 있음을 재확인한다 — `SquadCharacterPage`(`m_Enabled: 1`) 와 `DreamcatcherDeckPage`(`m_Enabled: 1`) 가 실제 페이지다.
2. 씬에서 컴포넌트 인스턴스를 먼저 제거하고(코드를 먼저 지우면 씬에 missing script 가 남는다), 고아가 된 하위 GameObject 를 정리한다.
3. 소스 2파일 삭제.
4. 삭제된 타입을 **길찾기 포인터로** 언급하는 주석 3곳을 현재 소유자로 고쳐 쓴다 — `DreamstoneStyle.cs:7`, `PlayerProfile.cs:35`, `LoadoutGatePopup.cs:49` (`SquadBuilderView.OpenPicker` → `SquadCharacterPageController` 등).
   `MenuPopup.cs:60` 은 `SquadPrepView` 를 언급하는데 그건 BattleScene 에서 **살아 있는 컴포넌트**다 — 이 줄은 **수정 대상이 아니다.**

## 완료 기준

- [ ] 컴파일 그린
- [ ] `SquadBuilderView|DreamcatcherDeckBuilderView` 검색 0건 (주석 포함)
- [ ] OutgameScene 에 missing script 경고 0 — 씬 로드 시 콘솔 클린
- [ ] Play — 스쿼드 페이지·드림캐쳐 페이지 정상 렌더 및 편성 동작(유닛 출전/해제, 스톤 장착, 카드 추가/제거), 콘솔 에러 0
- [ ] EditMode + PlayMode 전체 그린

---

**검증 기록 2026-07-30 · `5592b676`** — 컴파일 errors=0 · `SquadBuilderView|DreamcatcherDeckBuilderView` 검색 0건(주석 4곳은 "retired" 표기로 수정) · 씬 missing script 경고 0 · EditMode 그린. 두 컴포넌트가 **살아있는 패널(SquadPanel/DreamcatcherPanel) 자체에 붙어 있어** GameObject 가 아니라 컴포넌트만 제거했고, 구 빌더 전용 자식 10개를 정리했다. **미검증**: 두 페이지 편성 조작 Play 육안(스쿼드 페이지 렌더는 스크린샷 확인).
