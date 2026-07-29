# 0 — 유닛 D&D: 트레이 복귀 취소 존 + 취소 예고

## 목적

트레이에서 집어 든 유닛을 **트레이로 되돌리면 취소**되게 하고, 손을 떼기 전에 그 사실이 화면에 보이게 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 트레이 패널 rect 를 컨트롤러에 주입
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 취소 존 판정 · 예고 · 릴리즈 분기
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — 취소 룩 노브(⑫ 그룹)

## 구현

### A. 취소 존 주입

`DefenderSelector.BuildCanvas` 가 `_panel` 을 만든 직후 `SetCancelZone((RectTransform)_panel.transform)`
로 넘긴다. 컨트롤러는 런타임 `AddComponent` 라 씬 배선이 없고, 트레이 패널은 `Awake`(BuildCanvas)에서
한 번만 생성돼 리빌드로 파괴되지 않는다(파괴 대상은 `SlotContainer` 자식뿐). `EnsureDragController` 에도
같은 주입을 넣어 순서 의존을 없앤다(`_panel` null 이면 no-op).

미주입(null)이면 취소 존은 **비활성**이고 기존 동작 그대로다 — 테스트 하네스가 컨트롤러만 띄우는 경로가 있다.

### B. 판정 — 가상 포인터가 트레이 rect 안

```
_cancelHover = _cancelZone != null && !_simulatedDrag && _session.active
               && RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, _lastAimScreenPos, null);
```

- 카메라 인자 `null` — 트레이 캔버스는 `ScreenSpaceOverlay`(`UiCanvasSetup.Ensure`).
- **가상 포인터(`_lastAimScreenPos`)를 쓰는 이유는 README 의 "도달성 무손실" 절이 소유한다.** 여기에
  다시 쓰지 않는다. 요약만: raw 로 바꾸면 큰 맵 최하단 행이 배치 불가가 된다.
- `_simulatedDrag` 제외 = 계약 5.

갱신 지점은 `UpdateDrag` 하나다(가상 포인터가 확정되는 유일한 곳).

### C. 예고 — 취소 존 안에서는 보드 판정을 멈춘다

`Update` 의 추종 스텝과 `UpdateDrag` 의 위치 계산은 그대로 돈다(고스트가 손가락을 따라 트레이로 내려온다).
바뀌는 것은 **판정과 페인트**다:

- `ResolveFocusAndTarget` 을 호출하지 않고 `ClearHover()` 를 부른다 → hover·사거리·액체 하이라이트·
  거부 라벨이 전부 소거된다(취소 존에 있는 동안 보드는 "여기 아무 일도 없다").
- 프리뷰 실루엣 알파를 `cancelPreviewAlpha`(기본 0.4)로 낮춘다. 세션이 Spine 핸들을 들고 있어야 하므로
  `DragSession.skeleton` 필드를 추가한다(폴백 capsule 은 알파 변경 없음 — 계약 아님, 단순 미지원).
- 트레이 rect 를 덮는 **취소 배너**(`✕ 놓으면 취소`)를 띄운다. 기존 거부 라벨 캔버스(order 20001)에
  형제로 만들고, 위치·크기는 매 프레임 `_cancelZone.GetWorldCorners` 에서 가져온다(오버레이 캔버스라
  world corner == screen px). 별도 상수 오프셋 없음 = 계약 2.

취소 존을 **나가면** 다음 프레임의 `ResolveFocusAndTarget` 이 hover 를 즉시 복구한다. 알파와 배너는
`SetCancelVisual(false)` 가 원복한다.

### D. 릴리즈 분기

`EndDrag` 에서 `UpdateDrag` 직후 `_cancelHover` 를 먼저 본다:

```
if (_cancelHover) { CleanupSession(); SoundManager.Instance?.PlayCardReturn(); return; }
```

`FlashPlacementReject` 를 부르지 않는다 — 취소는 **거부가 아니다**(사용자가 의도한 정상 종료).
SFX 는 카드 복귀음을 재사용한다. 전용 클립이 없고 의미("집었던 걸 되돌림")가 같아서다.

### E. 정리

`CleanupSession` 에서 `_cancelHover = false` + `SetCancelVisual(false)`. 배너 GO 는 거부 라벨과 같은
캔버스에 살고 컨트롤러 파괴 시 함께 사라진다.

### F. 노브 (DragSwaySettings ⑫)

```
cancelPreviewAlpha = 0.4    // 취소 존 안 프리뷰 실루엣 알파
cancelTint         = coral  // 배너 테두리/글자색
```

하드코딩 금지(제약 6). 배너 문구는 게임플레이 수치가 아니라 구조 문자열이므로 코드 상수로 둔다
(거부 라벨의 `"X 코스트 부족"` 과 같은 취급).

## 완료 기준

- [x] 컴파일 통과, CS 에러 0
- [x] EditMode 전량 통과(신규 실패 0 — 유일 실패는 dirty `MapDocument_Zig.asset` 사전 실패)
- [x] **신규** `DragCancelZoneTest` 통과 — (1) 취소 존 릴리즈 = 무차감 종료,
      (2) 손가락이 트레이 안이어도 조준점이 트레이 밖이면 취소가 아니다
- [x] `DragPlacementReachTest` 통과 + 단언 추가 — 최하단 행을 노리는 **가상 포인터**가 트레이 rect 밖
- [ ] Play — 유닛을 집어 트레이 위로 되돌리면 배너가 뜨고, 놓으면 **코스트가 줄지 않는다**
- [ ] Play — 취소 존 안에서 보드 하이라이트·사거리·거부 라벨이 사라진다
- [ ] Play — 취소 존을 나가면 하이라이트가 즉시 복구된다
- [ ] Play — 큰 맵(Serpent/Twin/Spiral) **최하단 행에 여전히 배치된다** (도달성 회귀 없음)
- [ ] Play — 탭 배치 비행이 트레이 위를 지나가도 취소되지 않는다(계약 5)
