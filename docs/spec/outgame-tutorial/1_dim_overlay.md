# 1 — 통과구멍 레이어 (사각형 차집합 dim)

## 목적

지정한 사각형만 남기고 화면을 덮는다. dim 조각이 입력을 먹고, 구멍은 아래 로비 캔버스로 통과시킨다.
셰이더·마스크 없이 순수 사각형 차집합으로 만든다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialDimLayout.cs` (신규)
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialOverlay.cs` (신규)
- `Assets/_Project/Tests/EditMode/OutgameTutorialDimLayoutTests.cs` (신규)

네임스페이스는 `Wassup.UI` — `Scripts/UI/Outgame/` 전체 규약을 따른다.

## 구현

### 순수 함수 — `OutgameTutorialDimLayout`

```csharp
public static void Subtract(Rect area, IReadOnlyList<Rect> holes, float padding, List<Rect> results)
```

**축 전제 없는 일반 사각형 차집합**(y 경계 스캔라인). 초기 초안의 "홀들이 같은 수평 밴드에 있다"는
전제는 폐기했다 — 실제 로비 버튼은 **왼쪽 세로 1열**(`SquadButton` y −300, `DreamcatcherButton` y −552,
둘 다 x 48, 180×228)이라 전제가 정반대였다. 스캔라인은 배치 축과 무관하게 정확하다.

1. `results.Clear()`.
2. 각 홀을 `padding` 만큼 확장 → `area` 로 클램프 → 폭·높이가 0 이하면 버린다.
3. 남은 홀이 없으면 `area` 한 장만 담고 끝낸다.
4. y 경계 수집: `area.yMin`, `area.yMax`, 각 홀의 `yMin`/`yMax`(클램프됨). 오름차순 distinct.
5. 인접 경계쌍 `(y0, y1)` 마다 — 높이가 epsilon 이하면 건너뛴다:
   - 그 밴드를 **완전히 가로지르는** 홀(`yMin <= y0 && yMax >= y1`)만 모은다.
   - 없으면 `area` 폭 전체 조각 하나를 담는다.
   - 있으면 x 오름차순 정렬 후 겹치는 x 구간을 병합하고, `area.xMin`→첫 구간, 구간 사이들,
     마지막 구간→`area.xMax` 순으로 조각을 담는다. 폭이 0 이하인 조각은 담지 않는다.

세로로 쌓인 두 버튼이면 4단계가 `[yMin, −780, −552, −528, −300, yMax]` 를 만들고, `−552~−528`
밴드가 폭 전체 조각으로 나와 **두 버튼 사이 24px 간격이 정확히 어둡게 남는다.**

**좌표계 계약**: `area` 와 `holes` 는 오버레이 `FullBleedRoot` 로컬 Rect 다 — **중심 원점, y-up**
(`UiCanvasSetup.cs:68-74` 가 FullBleedRoot 를 부모 stretch 로 만든다). 즉 1920×1080 에서
`area = (-960, -540, 1920, 1080)`. 테스트도 이 규약으로 작성한다.

제약 10 판정: 분기·다단계이고 회귀 테스트 가치가 있으므로 순수 함수로 분리한다.

### 뷰 — `OutgameTutorialOverlay`

`UiCanvasSetup.Ensure(gameObject, 9)` 로 자체 Canvas 를 만들고 `FullBleedRoot` 아래에 dim 조각을 둔다.

> **`TutorialGuidanceView` 와 반드시 다른 GameObject 에 둔다.** 그쪽도 `BuildCanvas()` 에서
> `UiCanvasSetup.Ensure(gameObject, 10)` 을 자기 GameObject 에 호출하므로(`TutorialGuidanceView.cs:355`),
> 한 GameObject 에 얹으면 Canvas 한 개를 공유하며 sortingOrder 를 서로 덮어쓴다. 계층은 unit 4 참조.

- dim 조각은 `Image`, 색은 `UiOverlay.Dim`, **`raycastTarget = true`**. 조각마다
  `IPointerDownHandler` 를 구현한 작은 컴포넌트를 붙여 `Tapped` 를 올린다.
  **`Button` 을 쓰지 않는다** — `IPointerClickHandler` 의미론이라 드래그 임계를 넘긴 포인터에서
  클릭이 취소되고, 로비는 키링 스와이프를 유도하는 화면이라 탭 유실이 실제로 발생한다.
- 조각은 풀에서 꺼내 쓰고 비활성으로 반환한다.
- **홀 영역에는 어떤 그래픽도 두지 않는다.** 레이캐스트가 아래 `MenuCanvas`(order 0)로 떨어져
  실제 버튼이 눌리는 것이 이 설계의 핵심이다.

Public API:

```csharp
public event Action Tapped;                                  // dim 조각 탭
public void Show();
public void Hide();
public void SetHoles(IReadOnlyList<RectTransform> targets);  // null/빈 목록 = 구멍 없는 풀 dim
```

- **lazy build**: `_built` 가드 + 모든 public 메서드 선두에서 필요 시 빌드. **`Awake` 에서 `Hide()` 를
  호출하지 않는다** — 초기 비가시 상태는 빌드 안에서 1회만 설정한다. (`TutorialGuidanceView.Awake` 의
  무조건 `Hide()` 가 "컨트롤러가 먼저 표시하면 뷰 Awake 가 나중에 꺼버리는" 함정의 원인이다.)
- `SetHoles` 는 대상의 `GetWorldCorners` 를 오버레이 로컬로 변환한다. **`activeInHierarchy == false`
  인 대상은 건너뛴다** — 포커스 대상 3개는 모두 `menuRoot` 하위이고 `menuRoot` 는 `ApplyAuthGate`/
  `RaiseExclusive` 에서 토글된다. 첫 홀 계산은 `Canvas.ForceUpdateCanvases()` 이후에 수행한다.
- `LateUpdate` 는 대상들의 **4개 world corner 캐시**와 비교해 달라졌을 때만 재계산한다(화면 회전·safe area 변동 추종).
- `Hide()` 는 진행 중 페이드 트윈 중단 + 홀 목록 비우기 + 모든 조각 풀 반환까지 수행한다.
- dim 등장은 `UiOverlay.Dim` 까지 페이드 인한다(PrimeTween). `Hide` 는 즉시.
- `padding` 은 `[SerializeField] float holePadding = 6f` 로 노출한다(제약 6 — 하드코딩 금지).
  **두 포커스 버튼 사이 최소 간격의 절반 미만이어야 한다** — 로비 세로 열은 Squad/Dreamcatcher
  사이가 24px 이므로 12 가 상한이고, 12 를 쓰면 두 홀이 정확히 맞닿아 사이 dim 조각이 사라진다.

## 완료 기준

- [ ] 컴파일 통과
- [ ] EditMode `OutgameTutorialDimLayoutTests` 통과 (좌표계는 중심 원점 y-up)
  - 홀 0개 → `area` 1조각
  - 홀 1개 중앙 → 4조각, 합집합 넓이 = `area` − 홀
  - **세로로 쌓인 홀 2개(x 동일, y 간격 24) → 사이 간격이 폭 전체 조각으로 남는다** ← 회귀 가드
  - 가로로 나란한 홀 2개 → 사이 간격 조각이 남는다
  - 홀이 화면 밖으로 나감 → 클램프, 음수 크기 조각 없음
  - 홀이 `area` 를 덮음 → 0조각
  - 홀이 경계에 붙어 좌/우 조각 폭 0 → 그 조각은 생략
  - `padding = 0` / degenerate 홀(w 또는 h ≤ 0) 입력 → 무시하고 정상 동작
  - `results` 에 기존 항목이 있는 상태로 재호출 → `Clear()` 후 채운다
  - 홀 3개 이상
- [ ] Play 에서 `Show()` + 홀 0개일 때 로비 버튼·키링 드래그·캐릭터 클릭이 전부 무반응
- [ ] Play 에서 홀을 지정하면 **그 버튼만 실제로 눌린다** (다른 버튼은 여전히 무반응)
- [ ] 홀 위치가 포커스 대상 버튼과 육안 일치 (스크린샷 확인)
- [ ] dim 톤 확인 — `UiOverlay.Dim` 알파 0.92 가 로비 배경·캐릭터 위에서 과한지 Play 스크린샷으로 판단하고, 과하면 온보딩 전용 알파를 별도 상수로 분리
