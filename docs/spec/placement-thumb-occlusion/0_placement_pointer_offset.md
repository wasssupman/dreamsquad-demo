# 0 — 가상 포인터 오프셋 (트레이 D&D)

## 목적

배치 판정 포인터를 실제 포인터보다 화면상 살짝 위로 파생시켜, 손가락이 포커스 칸 하이라이트를
덮지 않게 한다. 이 단위는 **튜닝값 + 변환 seam + 트레이 D&D 경로**까지만 다룬다
(armed 보드·재배치는 unit 1).

## 변경 대상

- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — 오프셋 필드 2개(신규 그룹 ⑪)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 변환 seam(`UpdateDrag` 진입부 1곳)
  + `_prevScreenPos` seed + 읽기 seam 2개. **`EndDrag` 는 무변경**(아래 참조)
- `Assets/_Project/Tests/PlayMode/DragPlacementReachTest.cs` — 비교 기준을 가상 포인터로 갱신

## 구현

### SO 필드 (그룹 ⑪, ⑩ 드롭 하마 뒤)

```
placementPointerOffsetHeightRatio  [Range(0, 0.2)]  기본 0.06
placementPointerOffsetRampSeconds  [Range(0, 0.4)]  기본 0.08
```

Tooltip 에 반드시 담을 것: `0 = 현행(손가락 위치 그대로)` · `↑ = 칸이 손가락에서 멀어져 잘 보이나
최하단 행 도달성이 줄어든다` · `상한은 DragPlacementReachTest 가 못박는다 — 깨지는 값 채택 금지`.

px 환산은 SO 가 소유한다(값과 단위 변환을 같은 곳에 둔다. 소비처가 2개라 재사용 근거 충족):

```csharp
public float PlacementPointerOffsetPx => placementPointerOffsetHeightRatio * Screen.height;
```

### 변환 seam

`DefenderDragPlacementController` 에 private 헬퍼 하나:

```csharp
// 배치 판정 포인터 = 실제 포인터 + 화면 up × offset. 원 포인터를 덮어쓰지 않는다(파생값).
private Vector2 ToPlacementPointer(Vector2 rawScreen)
    => rawScreen + Vector2.up * (Cfg.PlacementPointerOffsetPx * _offsetRamp01);
```

`_offsetRamp01` 은 unit 1 에서 램프를 붙일 스칼라다. **이 단위에서는 트레이 D&D 만 다루므로 1f 고정**
(`BeginDrag` 시점엔 직전 하이라이트가 없어 부드럽게 할 대상이 없다 — 램프는 승격 점프가 있는
경로에만 필요하다). 필드로 두고 `BeginDrag` 에서 `1f` 로 세팅한다.

적용 지점은 **`UpdateDrag` 진입부 한 곳**이다:

```csharp
public void UpdateDrag(Vector2 screenPosition)
{
    if (!_session.active) return;
    screenPosition = ToPlacementPointer(screenPosition);   // 이 아래는 전부 가상 포인터
    _lastScreenPos = screenPosition;
    ...
```

**`EndDrag` 는 무변경이다.** 이미 `UpdateDrag(screenPosition)` 에 위임하고 그 뒤로는
`screenPosition` 을 다시 쓰지 않는다(`_onBoard`/`_session.hoverTile` 만 읽는다). 여기서 또 변환하면
오프셋이 두 번 더해져 릴리즈 칸이 하이라이트보다 한 칸 더 위로 튄다 — 계약 4 위반. 변환 지점은
끝까지 한 곳으로 유지한다. `BeginDrag` 의 `UpdateDrag(screenPosition)` 호출도 같은 이유로 무변경.

### 반드시 함께 고칠 것 — `_prevScreenPos` seed

`BeginDrag` 의 `_prevScreenPos = screenPosition`(현재 `:202`)은 **raw 값**이다. `_lastScreenPos` 만
가상이 되면 첫 프레임 스와이프 속도에 `offset/dt` 스파이크가 생겨 배치 컷신 틸트가 튄다 — 그 줄의
기존 주석("stale-prev 속도 스파이크 방지")이 경고하는 것과 같은 버그다. seed 도 가상 좌표로 맞춘다.

### 건드리지 않는 것

- `RunSimulatedDrag` — `_lastScreenPos` 를 월드(`finalRing`)에서 역산한다. 이 seam 을 지나지 않으므로
  자동 제외. 탭 비행이 한 칸 위로 날아가면 안 된다.
- `PointerOverUi()`, press 시작 시 `TryScreenToCell(_boardDownScreen)` 가드, 탭/드래그 임계 비교 —
  전부 raw 유지(계약 2).
- `_armedFromScreen`(트레이 슬롯 원점) — 비행 시작점이지 판정점이 아니다.

### 읽기 seam 2개 (unit 1 의 재배치가 소비)

`DragSlowmoScale` / `BoardDragThreshold` 선례를 그대로 미러:

```csharp
public float PlacementPointerOffsetPx => Cfg.PlacementPointerOffsetPx;
public float PlacementPointerOffsetRampSeconds => Cfg.placementPointerOffsetRampSeconds;
```

### 테스트 갱신

`DragPlacementReachTest.TopPlaceableRow_IsReachable_AndCommitFollowsFinger` 의 두 번째 단언
("확정셀 == 손가락셀 ±1")이 오프셋만큼 어긋난다. 비교 대상을 **가상 포인터의 셀**로 바꾼다:

```csharp
var virtualScreen = screen + Vector2.up * ctrl.PlacementPointerOffsetPx;
if (bridge.TryScreenToCell(cam, virtualScreen, out var aimCell)) { ... }
```

테스트 이름/주석도 갱신한다 — 지키는 계약이 "판정 = 손가락"에서 "판정 = 가상 포인터(= 손가락 +
고정 오프셋)"로 바뀌었고, **여전히 발점(`_unitTargetWorld`)이 아니다**는 점이 이 테스트의 원 취지다.

## 완료 기준

- `dotnet build` 또는 Unity 컴파일 에러 0.
- `placementPointerOffsetHeightRatio = 0` 에서 **현행과 동작이 바이트 동일**(회귀 0 확인 경로).
- `DragPlacementReachTest` 통과 — 최상단 배치가능 행 도달 유지 + 확정셀이 가상 포인터 셀과 ±1 이내.
- 에디터 Play 실드래그: 포커스 칸 하이라이트가 마우스 커서보다 위에 뜨고, 릴리즈 시 **그 칸에** 배치된다
  (하이라이트가 보여준 칸 ≠ 배치 칸이면 실패).
- 배치 컷신 틸트가 드래그 첫 프레임에 튀지 않는다(`_prevScreenPos` seed 확인).
- 탭 배치(트레이 슬롯 arm → 보드 탭)의 비행 착지 칸이 무변경.
