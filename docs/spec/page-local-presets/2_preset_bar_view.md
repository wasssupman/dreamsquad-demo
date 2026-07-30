# 2 — PresetBarView (프레젠테이션 전용)

## 목적

두 페이지가 공유하는 프리셋 조작 바. 목록 팝업 + 이름 필드 + 버튼 4개(선택/저장/되돌리기/삭제) + dirty 배지를 만들고 **이벤트만 raise** 한다. 상태 판단·저장은 페이지 컨트롤러 소유 — 이 뷰는 프리셋이 무엇인지 모른다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Outgame/PresetBarView.cs`

## 구현

```csharp
public class PresetBarView : MonoBehaviour
{
    public event Action<string> PresetPicked;   // 목록에서 프리셋 id 선택 (전환 요청)
    public event Action CreateClicked;          // [+] 셀
    public event Action CommitClicked;          // [선택]
    public event Action SaveClicked;            // [저장]
    public event Action RevertClicked;          // [되돌리기]
    public event Action DeleteClicked;          // [삭제]
    public event Action<string> NameCommitted;  // onEndEdit 에서만 발화

    public struct Entry { public string id; public string name; public Sprite[] thumbs; public bool committed; }

    public void SetEntries(IReadOnlyList<Entry> entries, string viewingId, bool canCreate);
    public void SetName(string name);
    public void SetDirty(bool dirty);
    public void SetButtonEnabled(bool commit, bool save, bool revert, bool delete);
}
```

레이아웃 — 두 페이지 골격이 동일하므로(좌 `detailWidth 0.34` 전체높이 / 우 = 상단 밴드 + 브라우저) **우측 컬럼 최상단 밴드**로 끼운다. 좌측 상세는 Spine 전체높이를 유지하고 좌상단 authored `CloseButton` 과도 겹치지 않는다.

```
┌──────────┬─────────────────────────────────────────────┐
│░░░░░░░░░░│ [스쿼드 2 ▼] [이름__] 선택 〖저장〗 되돌리기 삭제 │ ← PresetBarView
│░ SPINE  ░│ ● 미저장 변경 — 반입은 지금 저장분             │   (dirty 일 때만 2행)
│░░░░░░░░░░├─────────────────────────────────────────────┤
│          │ ▣▣▣▣▢▢▢   💎💎💎💎                            │ ← 기존 상단 밴드
├──────────┼─────────────────────────────────────────────┤
│ 상세 카드 │ 브라우저 그리드                               │
└──────────┴─────────────────────────────────────────────┘
```

- **목록은 `TMP_Dropdown` 이 아니다** (계약 9) — 리치 셀 불가. `SquadRosterBrowser.EnsureGridBuilt` 의 Scroll/Viewport/Mask/ContentSizeFitter 골격을 재사용한 커스텀 팝업. 셀 높이 ~120px, 세로 스크롤, 최대 31셀(30 + `[+]`)
- 셀 내용 = 이름 + `thumbs` 를 작게 나열(스쿼드는 유닛 초상 최대 7, 드캐는 카드 카테고리 색 바). **스프라이트는 카탈로그에 이미 로드된 것을 그대로 받으므로 신규 로드 0** — 뷰는 스프라이트를 찾지 않고 `Entry` 로 받는다
- `SetEntries` 는 셀을 전부 재구성하는 무거운 호출이다. 뷰는 호출된 대로 재구성하되, **컨트롤러가 구조 변경 시에만 부른다**(unit 3). 내용 편집 경로에서는 `SetDirty`/`SetButtonEnabled` 만 온다
- 확정 프리셋 셀에 `확정` 뱃지(`entry.committed`)
- 펼친 팝업은 브라우저 위에 그려져야 한다. **구현 정정(리뷰 CRITICAL, 2026-07-30)**: 팝업은 바의 자식으로 두고, 열 때 **바 자체**를 페이지 루트의 마지막 형제로 올린다(`transform.SetAsLastSibling()`). 초안대로 팝업만 바 안에서 `SetAsLastSibling` 하면 페이지 루트 형제 순서가 바뀌지 않아, 나중에 생성된 불투명 `HeaderStrip`·`BrowserPanel` 이 팝업을 완전히 덮는다 — `[+]` 가 팝업 안에만 있어 프리셋 생성·전환이 **도달 불가**가 된다. 중첩 Canvas 로 우회하지 않는 이유는 `LoadoutGatePopup.cs:43-49` 에 기록돼 있다(렌더만 살고 탭이 샌다). 회귀는 `PresetBarPopupLayerTest` 가 형제 인덱스로 고정한다
- 이름은 `TMP_InputField`, **`onEndEdit` 에서만** `NameCommitted` 발화(`onValueChanged` 미사용 — 계약 4 / unit 1 규약)
- dirty 배지는 2행으로 나타나고, 사라질 때 1행으로 접힌다. 바 자체 높이는 고정(0.10)이고 배지는 그 안에서 토글 — 높이가 변하면 아래 밴드가 흔들린다
- 버튼 dim 은 `SetButtonEnabled` 가 받은 대로만 반영한다. **어떤 조건에서 dim 인지 판단하지 않는다**(컨트롤러 책임)
- 런타임 생성 + `UiLayer.Apply(gameObject)` — 기존 뷰 관례

## 완료 기준

- [ ] 컴파일 그린
- [ ] 어떤 프리셋/프로필 타입도 참조하지 않음 — `PresetBarView` 에 `PlayerProfile|SquadPreset|DreamcatcherPreset|ProfileStore` using/참조 0건 (프레젠테이션 전용 증명)
- [ ] 임시 하네스(또는 unit 3 배선)로 Play 확인: 목록 팝업 개폐, 31셀 스크롤, `[+]` dim 토글, 이름 필드가 `onEndEdit` 에서만 이벤트 발화, dirty 배지 on/off 시 아래 밴드가 움직이지 않음
- [ ] 펼친 팝업이 브라우저 그리드 위에 렌더되고 그리드 셀 클릭을 먹지 않음
- [ ] 한글 IME 로 이름 입력 → 조합 중에는 이벤트 없고 확정/포커스아웃에서 1회 발화

---

**검증 기록 2026-07-30 · `f5f7608f`** — 컴파일 errors=0 · 프로필 타입 참조 0건 확인(프레젠테이션 전용) · **팝업이 브라우저 위에 렌더됨을 `PresetBarPopupLayerTest` 로 자동 검증 + 스크린샷 육안 확인**(리뷰 CRITICAL 수리). `[+]` 라벨 전각 ＋ 두부 렌더를 스크린샷으로 발견해 ASCII 로 교체. **미검증**: 31셀 스크롤 · `[+]` dim 토글 · 한글 IME 입력 · 배지 on/off 시 밴드 흔들림.
