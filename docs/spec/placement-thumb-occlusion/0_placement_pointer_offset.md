# 0 — 가상 포인터 오프셋 (트레이 D&D)

## 목적

배치 판정 포인터를 실제 포인터보다 화면상 살짝 위로 파생시켜, 손가락이 포커스 칸 하이라이트를
덮지 않게 한다. 이 단위는 **튜닝값 + 변환 seam + 트레이 D&D 경로**까지만 다룬다
(armed 보드·재배치는 unit 1).

## 변경 대상

- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — 오프셋 필드 2개(신규 그룹 ⑪)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 변환 seam(`UpdateDrag` 진입부 1곳)
  + `_prevScreenPos` seed + raw 보관 필드 + 라벨 클램프 + 읽기 seam 2개. **`EndDrag` 는 무변경**(아래 참조)
- `Assets/_Project/Tests/PlayMode/DragPlacementReachTest.cs` — 비교 기준을 가상 포인터로 갱신
- `Assets/_Project/Tests/PlayMode/DropDismountTest.cs` — **구동 좌표 보정**(아래 "깨지는 테스트")

> **이 단위에서 오프셋이 켜진다.** Unity 는 관리 인스턴스를 먼저 만들고(필드 이니셜라이저 실행) YAML 을
> 덮으므로, 파일에 키가 없는 신설 필드는 **클래스 기본값을 유지**한다. 즉 기본값 `0.06` 이 unit 0 커밋
> 시점부터 라이브다 — 에셋을 편집해야 켜지는 게 아니다. 그래서 아래 두 테스트가 **이 단위에서** 깨진다.

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
가상이 되면 첫 프레임 스와이프 속도(`:237` `rawVel = (_lastScreenPos - _prevScreenPos)/dt`)에
`offset/dt` 스파이크가 생겨 배치 컷신 틸트가 튄다 — 그 줄의 기존 주석("stale-prev 속도 스파이크 방지")이
경고하는 것과 같은 버그다. seed 도 가상 좌표로 맞춘다.

**순서 의존**: `_offsetRamp01 = 1f` 세팅이 seed(`:202`)보다 **앞**에 와야 seed 가 가상이 된다.
`UpdateDrag` 호출은 `:207` 이라 그 뒤다(critic L2).

### 깨지는 테스트 2개 (둘 다 이 단위에서 고친다)

**`DragPlacementReachTest`** — 두 번째 단언("확정셀 == 손가락셀 ±1")이 오프셋만큼 어긋난다.

**`DropDismountTest`**(critic C1 — 초판이 "전수 확인"을 주장하며 놓쳤다) — `:56` 의 `screenA` 는
`cellA` **셀 중심의 화면좌표**이고, `:59` 가 `UpdateDrag(screenA)` ×12, `:65` 가 `EndDrag(screenA)`,
`:67` 이 `TryGetDefenderAt(cellA)` 를 단언한다(`:111-115` 착지 앵커도 `cellA` 기준). 오프셋이 붙으면
커밋 셀이 위 행이 되어 실패한다. 그 위 셀이 배치 불가면 reject 플래시만 나고 아무것도 안 놓여 역시 실패.

고침은 두 테스트 동형 — **셀 탐색/roundtrip 검증은 그대로 두고 구동 좌표만 보정**한다:

```csharp
// 가상 포인터가 목표 셀을 가리키게 하는 실제 포인터 좌표. 직접 호출이라 화면 밖 값도 무해.
var drive = screenA - Vector2.up * ctrl.PlacementPointerOffsetPx;
```

`FindValidCellWithScreen` 안의 `s.y >= 0` 화면 내 판정은 보정 **전** 값에 그대로 둔다.
`DragPlacementReachTest` 쪽은 반대로 스윕 좌표는 raw 로 두고 **비교 대상**을 가상으로 올린다:

```csharp
var virtualScreen = screen + Vector2.up * ctrl.PlacementPointerOffsetPx;
if (bridge.TryScreenToCell(cam, virtualScreen, out var aimCell)) { ... }
```

나머지 placement 계열(`PlacementAuraTest`, `OnPlace*`, `RelocationMoveModeTest`, `RelocationSmokeTest`,
`FirstSessionTutorialSmokeTest`, `TutorialDragGuidanceTests`)은 `PlaceDefenderAs`/`BeginMoveModeFor`
직접 호출이거나 `ToggleArm(.., Vector2.zero)` 뿐이라 무관하다(critic 이 전 테스트 grep 으로 확인).

### `_lastScreenPos` 소비처 — 가상이 되면 안 되는 곳 2개

가상 포인터를 상속하는 게 **전부 옳지는 않다**(critic M2·M3). raw 를 별도 보관한다:

```csharp
private Vector2 _lastRawScreenPos;   // 카메라 포커스 전용 — 절대 NDC 변환 소비처
```

- **카메라 포커스는 raw** — `CameraDirector.SetDragFocus`(`CameraDirector.cs:237-244`)는 스크린좌표를
  **NDC 로 절대 변환**한다. 가상을 먹이면 `_focusNdcTarget.y` 에 상수 바이어스(`2 × ratio` ≈ 0.12)가
  실려 카메라가 프레임을 당기고, 보드 콘텐츠가 화면상 내려가 손가락↔칸 간격을 **일부 되돌린다**.
  "델타 불변이라 무해"가 아니다. 카메라 포커스는 "플레이어가 어디를 보나" 채널이라 raw 가 맞다(계약 2).
- **거부 라벨은 가상 기준 + 클램프** — 하이라이트 위에 떠야 하므로 가상이 맞지만, `:480` 은
  `_lastScreenPos.y + 96f` 를 ScreenSpaceOverlay 에 **클램프 없이** 절대 배치한다. 오프셋이 붙으면
  손가락 기준 161px 위가 되어 화면 상단 드래그에서 이탈한다. 코스트 부족의 **유일한 문자 채널**이라
  이탈 비용이 크다 → `Mathf.Min(y, Screen.height - margin)` 클램프를 같은 단위에서 추가한다.

### 건드리지 않는 것

- `RunSimulatedDrag` — `_lastScreenPos` 를 월드(`finalRing`)에서 역산한다. 이 seam 을 지나지 않으므로
  자동 제외. 탭 비행이 한 칸 위로 날아가면 안 된다.
- `PointerOverUi()`, press 시작 시 `TryScreenToCell(_boardDownScreen)` 가드, 탭/드래그 임계 비교 —
  전부 raw 유지(계약 2).
- `_armedFromScreen`(트레이 슬롯 원점) — 비행 시작점이지 판정점이 아니다.
- `PlacementInput`(`Scripts/Core/PlacementInput.cs`) — 은퇴한 클릭 배치. press 즉시 커밋이라 피드백
  루프가 0 → 탭과 같은 이유로 raw 가 맞다.
- `DirectionAimController`(`:103-105`) — `Pointer.current` 를 자기가 읽으므로 자동으로 raw. 조준
  스와이프는 배치 판정이 아니다.

두 항목을 명시하는 이유: 지금 언급이 없으면 나중에 "일관성"을 근거로 오프셋을 먹일 여지가 있다(critic L5).

### 읽기 seam 2개 (unit 1 의 재배치가 소비)

`DragSlowmoScale` / `BoardDragThreshold` 선례를 그대로 미러:

```csharp
public float PlacementPointerOffsetPx => Cfg.PlacementPointerOffsetPx;
public float PlacementPointerOffsetRampSeconds => Cfg.placementPointerOffsetRampSeconds;
```

### 테스트 주석 갱신

두 테스트의 이름/주석에서 지키는 계약을 갱신한다 — "판정 = 손가락"에서 "판정 = **가상 포인터**
(= 손가락 + 고정 오프셋)"로 바뀌었고, **여전히 발점(`_unitTargetWorld`)이 아니다**는 점이
`DragPlacementReachTest` 의 원 취지다. 그 취지가 사라지면 테스트가 무엇을 지키는지 알 수 없게 된다.

## 완료 기준

- `dotnet build` 또는 Unity 컴파일 에러 0.
- `placementPointerOffsetHeightRatio = 0` 에서 **현행과 동작이 바이트 동일**(회귀 0 확인 경로).
- `DragPlacementReachTest` 통과 — 최상단 배치가능 행 도달 유지 + 확정셀이 가상 포인터 셀과 ±1 이내.
  (단언 **형태** 교정과 하단 계측은 unit 2 — 이 단위는 기준만 옮긴다.)
- **`DropDismountTest` 통과** — 핸드오프 팝 0 단언까지 그대로.
- 에디터 Play 실드래그: 포커스 칸 하이라이트가 마우스 커서보다 위에 뜨고, 릴리즈 시 **그 칸에** 배치된다
  (하이라이트가 보여준 칸 ≠ 배치 칸이면 실패).
- 배치 컷신 틸트가 드래그 첫 프레임에 튀지 않는다(`_prevScreenPos` seed 확인).
- **카메라가 드래그 중 위로 스르륵 밀리지 않는다**(포커스 raw 확인 — 가상을 먹이면 상수 바이어스).
- **거부 라벨이 화면 상단 드래그에서 잘리지 않는다**(클램프 확인).
- 탭 배치(트레이 슬롯 arm → 보드 탭)의 비행 착지 칸이 무변경.
