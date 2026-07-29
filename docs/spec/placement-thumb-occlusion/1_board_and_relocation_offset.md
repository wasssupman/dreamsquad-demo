# 1 — armed 보드 · 재배치 경로 + 승격 램프

## 목적

오프셋을 나머지 두 **연속 드래그** 경로로 확장하고, 두 경로 공통의 "무이동 탭은 누른 칸 그대로"를
보장한다. 승격 순간 하이라이트가 한 칸 순간이동하지 않게 램프를 붙인다.

선행: unit 0(SO 필드 · `ToPlacementPointer` · 읽기 seam 2개).

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `UpdateBoardGesture` 분기 + 램프 소유
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — 목적지 제스처 승격 게이트
- `Assets/_Project/Tests/PlayMode/RelocationPlacementSessionTest.cs` — 드래그 커밋 스텝의 목표 좌표 보정

## 구현

### 램프 (오프셋 스칼라)

`_offsetRamp01` 을 `DefenderDragPlacementController` 가 소유하고 unscaled 시계로 올린다.
`rampSeconds == 0` 이면 즉시 1.

- 트레이 D&D: `BeginDrag` 에서 `1f` 로 시작(직전 하이라이트가 없어 점프가 없다 — unit 0 결정 유지).
- armed 보드: press 시 `0f`, `_boardDragging` 승격 후 매 프레임 `dt/rampSeconds` 만큼 1 로 상승.
- `ResetBoardGesture` / `CleanupSession` 에서 리셋.

램프 중엔 판정 셀이 손가락이 멈춰 있어도 움직인다. 기존 `placementCommitInterval` throttle(기본 0.5s)이
이 전이를 자연히 흡수하므로 추가 보정은 넣지 않는다.

### armed 보드 제스처 (`UpdateBoardGesture`)

`cur = pointer.position.ReadValue()` 는 **raw 로 유지**한다(임계 비교가 raw↔raw 여야 값이 불변).
가상 포인터는 **셀을 판정하는 세 호출에만** 넘긴다:

| 호출 | 오프셋 | 근거 |
|---|---|---|
| `UpdateBoardScout(cur)` | `_boardDragging` 일 때만 | 승격 전엔 누른 칸을 그대로 비춰야 릴리즈(탭) 결과와 일치 |
| `CommitBoardDrag(cur)` | 적용 | 드래그 릴리즈 = 스카우트가 보여준 칸에 배치 |
| `HandleBoardTap(cur)` | **미적용** | 무이동 탭 = 누른 칸 |

즉 `UpdateBoardScout` 는 `_boardDragging ? ToPlacementPointer(cur) : cur` 를 받는다. 승격 프레임에
스카우트 셀이 한 칸 올라가는데, 이는 손가락이 움직이기 시작한 순간과 겹쳐 "하이라이트가 리드한다"로
읽힌다 — 램프가 이 전이를 부드럽게 한다.

### 재배치 목적지 제스처 (`DefenderRelocationController.TickMoveMode`)

현재 이 경로엔 **탭/드래그 구분이 없다** — `_targetPressActive` 가 press 즉시 서고, `pressed` 동안
`UpdateScout(screen)`, 릴리즈에 `ResolveRelease(screen)`. 계약 3("탭은 원위치")을 지키려면 승격
게이트를 신설한다. armed 보드의 `_boardDragging` 을 그대로 미러:

```
_targetPressDown  : Vector2   press 시작 좌표(raw)
_targetDragging   : bool      이동량이 임계를 넘겨 드래그로 승격
```

- 임계는 `DragController.BoardDragThreshold` 를 읽는다(제스처 일관 — 이미 `DragSlowmoScale` 을 같은
  방식으로 공유하는 선례가 있고, 이 seam 은 원래 재배치를 위해 노출된 것이다).
- 승격 후 `UpdateScout` / `ResolveRelease` 에 `DragController.PlacementPointerOffsetPx` 를 더한 좌표를
  넘긴다. 미승격이면 raw.
- 램프는 `DragController.PlacementPointerOffsetRampSeconds` 로 같은 값을 쓰되 **스칼라는 재배치가
  자기 것을 따로 굴린다**(두 컨트롤러가 동시에 살아 있을 수 있고, 램프는 제스처 지역 상태다).
- `EnterMoveMode` / `CancelMoveMode` 에서 두 필드 리셋.
- `DragController` 가 null 인 폴백(트레이 미빌드)에서는 오프셋 0 · 임계 기존 폴백값 — 재배치가
  드래그 컨트롤러 부재로 죽지 않게 한다(`DragSlowmoScale` 폴백 `0.2f` 선례와 동형).

`Step(pressStarted, pressed, screen, dt)` **시그니처는 유지한다** — PlayMode 테스트가 reflection 으로
직접 구동하는 원격 검증 경로다.

### 테스트 갱신

`RelocationPlacementSessionTest` 의 두 경로가 성격이 다르다:

- `:179-180` (무효 목적지 거부) — press·release 가 **같은 좌표** = 탭. 승격 게이트 덕에 **무변경으로 통과**한다.
- `:120-122` (드래그 커밋) — `nowScreen` 에서 press 후 `backScreen` 으로 이동해 릴리즈한다. 승격되므로
  릴리즈가 `backScreen + offset` 의 셀로 해석돼 `source` 대신 한 칸 위에 배치된다 → **단언 실패**.

`backScreen` 을 "가상 포인터가 `source` 를 가리키는 실제 좌표"로 보정한다. 헬퍼를 하나 더 둔다:

```csharp
// 승격된 드래그 스텝용 — 가상 포인터가 cell 을 가리키게 하는 실제 포인터 좌표.
private static Vector2 ScreenAimingAt(BattleBridge b, Camera cam, Vector2Int cell)
    => ScreenOf(b, cam, cell) - Vector2.up * EffectiveOffsetPx();

// 재배치 컨트롤러의 폴백을 그대로 미러한다 — 이 테스트는 DisableUiCanvases() 를 거치므로
// DragController 가 해석될지 보장되지 않는다. 미해석이면 컨트롤러도 오프셋 0 이라 양쪽이 함께 0 이 된다.
private static float EffectiveOffsetPx()
{
    var dc = Object.FindObjectOfType<DefenderSelector>()?.DragController;
    return dc != null ? dc.PlacementPointerOffsetPx : 0f;
}
```

`ScreenOf` 의 라운드트립 단언은 그대로 두고(셀 중심 좌표 자체는 여전히 유효), 보정은 그 결과에서 뺀다.
`:120` 의 press 시작 좌표(`nowScreen`)는 보정하지 않아도 된다 — 승격 임계만 넘기면 되는 좌표이고,
판정에 쓰이는 건 릴리즈 좌표다. 다만 press→릴리즈 이동량이 임계(`boardDragThreshold`, 기본 16px)를
확실히 넘는 두 셀이어야 승격된다. `source` 와 `target2` 가 인접해 화면 거리가 임계 미만이면 승격이
안 돼 탭으로 해석된다 — 그 경우 이 스텝은 보정 없이 통과하므로, **단언이 통과하는 이유가 무엇인지**
(승격 후 보정 성공 vs 미승격) 를 헷갈리지 않게 두 셀의 화면 거리를 단언으로 한 줄 못박는다.

## 완료 기준

- 컴파일 에러 0. `RelocationPlacementSessionTest` · `DragPlacementReachTest` 전부 통과.
- 에디터 Play — armed 보드: 슬롯 탭 후 보드를 **누른 채 움직이면** 하이라이트가 커서 위로 올라가고
  릴리즈 시 그 칸에 배치된다. **누르고 바로 떼면** 누른 칸에 배치된다(오프셋 없음).
- 에디터 Play — 재배치: 이동모드 진입 후 목적지를 **끌어서** 놓으면 커서 위 칸으로, **탭하면**
  누른 칸으로 간다.
- `placementPointerOffsetHeightRatio = 0` 에서 세 경로 모두 현행과 동일.
- 승격 순간 하이라이트가 순간이동하지 않는다(램프 육안 확인. `rampSeconds = 0` 과 비교해 차이 확인).
