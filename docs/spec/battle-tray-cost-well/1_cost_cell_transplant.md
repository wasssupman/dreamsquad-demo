# 1 — 코스트 셀 이식 (통합 박스)

## 목적

떠 있는 코스트 레일을 없애고, 코스트 뷰를 트레이 패널 안 첫 번째 셀로 옮긴다. 이 unit 은 **구조와 배치**만 다룬다. 물통 연출은 unit 2.

## 변경 대상

- `Assets/_Project/Scripts/UI/CostDisplay.cs`
- `Assets/_Project/Scripts/UI/DefenderSelector.cs`

## 함정 — 먼저 읽을 것

### 1. `_slotContainer` 는 `_panel` 과 같은 객체다

`DefenderSelector.cs:178` 이 `_slotContainer = _panel.transform;` 이고, `RebuildSlots`(`:189-190`)는 `_slotContainer` 의 자식을 **전부** Destroy 한다. 호출 경로는 `OnPhaseChanged(Placement)` → `OnDraftConfirmed()` → `RebuildSlots` 이므로 **매 배치 페이즈 진입마다** 돈다.

`AttachToTray` 를 `BuildCanvas`(Awake, 1회)에서만 부르면: Awake 에 셀 생성 → 첫 Placement 진입에 파괴 → **영원히 없음**. 게다가 파괴된 `_panel` 은 Unity fake-null 이라 `RefreshVisible`(`CostDisplay.cs:201`)과 `Update`(`:262`)가 조용히 early-return 한다 — **예외도 경고도 없다.**

→ 트레이 패널 아래에 전용 `SlotContainer` 자식을 두고 `_slotContainer` 를 그것으로 바꾼다.

### 2. `childForceExpandWidth` 가 `flexibleWidth = 0` 을 덮어쓴다

`HorizontalOrVerticalLayoutGroup.cs:237-238`:

```csharp
if (childForceExpand)
    flexible = Mathf.Max(flexible, 1);
```

`DefenderSelector.cs:176` 의 `childForceExpandWidth = true` 를 그대로 두면 셀의 `flexibleWidth = 0` 이 1 로 덮여 잉여 폭이 균등 분배된다(셀 288.75 / 슬롯 134.75 — 의도와 전혀 다름).

## 구현

### 계층

```
DefenderPanel  (HLG: childForceExpandWidth = false, childControlWidth = true)
├─ CostCell        LayoutElement { preferredWidth = costCellWidth, flexibleWidth = 0 }
│  ├─ CanvasGroup  (억제용 — SetActive 대신)
│  ├─ Value        "4<size=52%>/10</size>", cellNumberFontSize
│  ├─ EnergyIcon   ⚡ 1개 (HUD 전체에서 유일한 에너지 기호 — unit 3 이 슬롯 볼트를 지운다)
│  └─ Well
│     ├─ WellBack     Mask 용기 (라운드 사각)
│     ├─ WellLiquid   Image.Type.Filled / Vertical / Bottom
│     └─ WellSurface  액체 표면 하이라이트
└─ SlotContainer   LayoutElement { flexibleWidth = 1 }
   └─ (HLG: childForceExpandWidth = true — 자식이 동질이므로 유지)
      └─ Slot_* ×n
```

바깥 HLG 는 force-expand 를 끄고 flexible 을 명시 부여, 안쪽 슬롯 컨테이너는 기존처럼 force-expand 로 슬롯을 균등 분할한다. `RebuildSlots` 의 파괴 범위가 `SlotContainer` 로 격리된다.

### 부착 seam

```csharp
// CostDisplay
public void AttachToTray(Transform trayPanel);
```

`DefenderSelector.BuildCanvas` 말미(`UiLayer.Apply` 직전)에서 `costDisplay?.AttachToTray(_panel.transform)` 를 호출한다. 두 컴포넌트 모두 `Awake` 에서 캔버스를 짓기 때문에 실행 순서가 보장되지 않는다 — 그래서 `CostDisplay` 는 `Awake` 에서 뷰를 짓지 않고 **`AttachToTray` 가 호출될 때 짓는다**.

### 삭제

- `UiCanvasSetup.Ensure` 호출 — 이제 트레이 캔버스 안에 산다. 씬 오브젝트에 남은 `Canvas`/`GraphicRaycaster`/`CanvasScaler` 도 정리한다.
- `railMode` 분기 전체, 레거시 부유 배지 경로(`PlateW 363`/`PlateH 112`/`y164`), `RailYFor()`, `OnPhaseChanged` 의 위치 재계산.
- 세그먼트 바 일체(`_bars` · `_barRoots` · `_barCount` · `EnsureBars` · `PopSegment` · `FlashLostSegment` · `_flashing`). `CostRuntime.Max` 는 런타임에 바뀌지 않으므로(`Configure` 호출처는 `GameManager.cs:134` 하나) `EnsureBars` 의 max 대응 재구성도 불필요하다.

### 변경되는 계약

| 항목 | 이전 | 이후 | 이유 |
|---|---|---|---|
| 억제 | `_panel.SetActive(show)` | **`CanvasGroup.alpha` + `blocksRaycasts`** | 레이아웃 자식이라 비활성화 시 슬롯이 1232 폭으로 재확장(154 → 176) — 손패 열 때마다 리플로우 |
| 활성 판정 | `_panel.activeSelf` (`:73`, `:262`) | **`activeInHierarchy`** | 부모(트레이)만 꺼진 상태를 `activeSelf` 는 못 본다 → 안 보이는 채 코루틴이 돈다 |
| pulse 틴트 대상 | 레일 플레이트 | 셀 배경 | — |
| pulse 라벨 부모 | `_panel`(363폭) | **트레이 패널** | `260×34` 라벨을 154 폭 셀에 붙이면 좌우로 53씩 삐져나와 인접 슬롯을 덮는다 |

### pulse 틴트 함정

`ResetPulseVisual`(`CostDisplay.cs:129`)이 `_plateImage.color = Color.white` 로 되돌린다. 현재는 색이 스프라이트에 구워져 있어 white 가 중립이라 안전하다. **셀 배경을 `Image.color = wellBackColor` 로 구현하면 pulse 1회 후 base 틴트가 흰색으로 파괴된다.** 셀 배경색도 스프라이트에 굽고 `Image.color` 는 white 로 유지한다.

### 플립 중 구멍

`DreamcatcherHandView.Open()`(`:508`)은 플립 코루틴 시작 **전에** `SetSuppressed(true)` 를 부른다. 셀이 트레이 첫 자식이 된 뒤 이걸 그대로 두면 트레이가 회전하는 동안 **왼쪽에 빈 구멍이 뚫린 채** 돈다.

→ 트레이 부착 상태에서 가림 소유권은 **플립이 갖는다**. `SetSuppressed` 는 호출측 무변경을 위해 시그니처만 유지하고 내부는 no-op 으로 만든다. 근거를 메서드 주석에 남긴다.

### sortingOrder

코스트 뷰가 자체 캔버스(order 5)에서 트레이 캔버스(order 4)로 내려간다. 손패(5)·DraftView(5) 아래가 되지만 두 경우 모두 트레이가 함께 숨거나 플립되므로 실사용 충돌은 없다. pulse 라벨만 트레이 패널 기준이라 order 4 에서 그려진다는 점을 인지한다.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러/경고 없음
- [ ] **배치 페이즈를 2회 이상 진입(Restart/Redraft 포함)해도 코스트 셀이 살아 있다** — D1 회귀 가드
- [ ] Play — 셀 폭이 `costCellWidth` 와 일치하고 슬롯 폭이 균등하다 (실측값을 기록한다 — D2 회귀 가드)
- [ ] 트레이 위에 떠 있던 레일이 사라졌다 (겹침/이중 표시 없음)
- [ ] 숫자가 `CostRuntime` 을 따라 갱신된다 (물통 연출은 unit 2)
- [ ] 손패를 열고 닫는 동안 **슬롯 폭이 변하지 않는다** (리플로우 없음)
- [ ] 손패 플립 **중간 프레임**을 캡처해 셀 자리에 구멍이 없는지 확인
- [ ] 코스트 부족 상태로 슬롯을 끌면 셀에 pulse 가 뜨고, pulse 후에도 셀 배경색이 원래대로 돌아온다
