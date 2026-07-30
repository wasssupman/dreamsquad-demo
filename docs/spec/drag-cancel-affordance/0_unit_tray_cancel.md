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
- 문자 예고는 포인터 추종 라벨(`UpdateRejectLabel`)이 담당한다. **⚠ rev3 에서 트레이를 덮던 배너를
  삭제했다** — 이 §C 를 배너 사양으로 읽지 말 것. 아래 rev3 절이 최신이다.

취소 존을 **나가면** 다음 프레임의 `ResolveFocusAndTarget` 이 hover 를 즉시 복구하고, 알파는
`UpdateCancelVisual` 이 원복한다.

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
cancelPreviewAlpha       = 0.4   // 취소 예고 중 프리뷰 실루엣 알파
cancelTint               = coral // 취소 라벨 색
cancelHintDwellSeconds   = 0.18  // 예고 게이트(rev2) — 오버슛·시작 구간 깜빡임 차단
```

하드코딩 금지(제약 6). 라벨 문구는 게임플레이 수치가 아니라 구조 문자열이므로 코드 상수로 둔다
(거부 라벨의 `"X 코스트 부족"` 과 같은 취급).

## rev2 — UX 리뷰 반영 (2026-07-30)

디자이너/빠른템포 게이머 관점 리뷰에서 나온 세 건. 판정 로직은 유지하고 **표면만** 고친다.

### H1. 배너를 트리거 위치에 그린다 (WYSIWYG 회복)

판정은 가상 포인터인데 배너를 **트레이 rect 그대로** 그려서, 보이는 밴드와 실제 히트박스가
오프셋(1080에서 65px)만큼 어긋났다:

| | 화면 y |
|---|---|
| 보이는 트레이 = 구 배너 | 32 ~ 190 |
| 실제 트리거(손가락 기준) | −33 ~ **125** |

125~190 은 **배너가 켜져 보이는데 취소가 아닌** 구간이었다 — "되돌린 줄 알고 뗐는데 꽂힌다".
`LayoutCancelBanner` 가 트레이 rect 를 `PlacementPointerOffsetPx` 만큼 **내려** 그리고, 화면
밖으로 나간 아래쪽은 잘라낸다(라벨이 보이는 밴드 중앙에 오게). 오프셋 값은 여전히 한 곳에서만
나오므로 계약 2 는 유지된다.

### H2. 예고 게이트에 dwell 문을 추가

`_cancelZoneLeft`(존 이탈 1회) 단독이면 **가장 빠른 취소가 침묵한다**: 트레이 드래그는 취소 존
**안에서** 시작하므로 "집었다가 그 자리에서 놓기" 는 이미 취소로 동작하는데, 그 순간엔 배너가
안 떠서 아무도 그 수단을 배우지 못한다. 게이트를 `존 이탈 1회 OR 존 안 dwell ≥
cancelHintDwellSeconds(0.18)` 로 바꾼다. 위로 튕기는 드래그는 조용하고, 망설이는 손가락에는 뜬다.

dwell 은 `UpdateCancelVisual` 에서 `Time.unscaledDeltaTime` 으로 누적한다 — `OnDrag` 는 포인터가
움직일 때만 오지만 `_cancelHover` 는 마지막 값을 유지하므로 "멈춘 채 머무는" 경우가 정확히 잡힌다.

### M2. 무차감을 문자로 못박는다

빠른 템포에서 취소의 유일한 불안은 "코스트 날아갔나" 다. 라벨을
`✕  놓으면 취소 · 코스트 유지` 로. 상수는 `CancelLabelText` 하나(포인터 라벨과 배너가 공유).
드림캐쳐 힌트도 대칭으로 `· 각성치 유지`.

### 미반영 (후속 unit 후보)

- **M3** 최초 발견 경로가 없다 → 첫 배치 드래그 1회 힌트(`UserDragStarted` 훅 기존).

## rev3 — 취소 배너 삭제 (사용자 결정 2026-07-30)

**M1 을 "얇게 만들기" 대신 "지우기" 로 해결했다.** 트레이를 덮던 배너를 통째로 삭제한다.

근거: 취소 예고에는 이미 두 신호가 있고 **둘 다 플레이어 시선 위치에 있다** —
(a) 손에 든 프리뷰가 고스트 알파, (b) 보드 하이라이트·사거리 소거. 배너는 그 위에 얹은
스크림이었고, 대가로 **코스트 물통과 출발 슬롯을 가려 "어디로 되돌아가는지" 를 오히려 지웠다.**
1초짜리 인터랙션에 전면 오버레이는 과하다.

문자 채널은 **포인터 추종 라벨 하나**로 합친다(`UpdateRejectLabel`). 취소 사유가 둘(트레이 존 복귀 /
격자 밖 관용 초과 — unit 3)이지만 표면은 하나다. 라벨은 작고 손가락을 따라와 아무것도 가리지 않고,
문구는 동일하므로 사유를 구분해 보여줄 이유도 없다.

지워진 것: `EnsureCancelBanner` · `LayoutCancelBanner` · 배너 GO/필드 · rev2 의 H1(배너 위치 보정)
자체. **H1 이 풀려고 했던 "보이는 밴드 ≠ 히트박스" 문제는 배너가 없어져 소멸한다** — 보이는
밴드가 없으므로 어긋날 대상이 없다. `_cancelZoneLeft` + dwell 게이트(H2)는 라벨에 그대로 적용된다.

남는 것: 판정 rect(트레이 패널), 고스트 알파(`cancelPreviewAlpha`), 라벨 색(`cancelTint`),
dwell 노브. `UnityEngine.UI` / `Wassup.UI.Layout` using 도 함께 정리(배너 전용 의존).

### 리뷰 반영 (code review 2026-07-30)

- **M1/M2 — 예고 술어를 하나로 통합.** `CancelStateNow`(= `_cancelHover || _noCell`) 위에 게이트를
  얹어 `CancelArmed` 를 만든다. 게이트는 `(존 이탈 후 재진입) || dwell`. 전에는 dwell 게이트가
  트레이 존에만 걸려 **`_noCell` 이 판정 프레임에 즉시 예고를 켰다** — 가장자리 열을 좌우로 흔들면
  관용 링을 넘나들며 알파/라벨이 껌뻑였고(맵 무관), 배치가능 타일 하이라이트도 사유별로 달랐다
  (`UpdatePlacementHighlightState` 가 `_noCell` 을 안 봤다 → 계약 4·6 위반). 통합으로 둘 다 해소.
- **M3 — 테스트 공허 통과 차단.** `DragCancelZoneTest` 가 릴리즈 직전에 `_noCell == false` 를 단언한다.
  없으면 취소 존 분기를 지워도 `_noCell` 경로가 대신 취소해 무차감 단언이 통과할 수 있었다(강도가
  로드된 맵에 의존).
- **MINOR** — 인스펙터 툴팁/주석의 '배너' 잔재 정리, `_noCell` 소거를 진입 1회로, 오프보드 전이에서
  `_noCell` 관리, 비활성 트레이는 취소 판정 제외(`activeInHierarchy`), `SoundManager` 풀네임 호출 정리,
  관용 **경계값**(frac −1.5 / −1.51, 10.49 / 10.5) 테스트 추가.

## 완료 기준

- [x] 컴파일 통과, CS 에러 0
- [x] EditMode 전량 통과(신규 실패 0 — 유일 실패는 dirty `MapDocument_Zig.asset` 사전 실패)
- [x] **신규** `DragCancelZoneTest` 통과 — (1) 취소 존 릴리즈 = 무차감 종료,
      (2) 손가락이 트레이 안이어도 조준점이 트레이 밖이면 취소가 아니다
- [x] `DragPlacementReachTest` 통과 + 단언 추가 — 최하단 행을 노리는 **가상 포인터**가 트레이 rect 밖
- [ ] Play — 유닛을 집어 트레이 위로 되돌리면 취소 예고가 뜨고, 놓으면 **코스트가 줄지 않는다**
- [ ] Play (rev3) — 트레이 쪽으로 끌면 **덮는 UI 없이** 프리뷰 고스트 + 포인터 라벨만 나오는가
- [ ] Play (rev3) — 코스트 물통과 슬롯 아이콘이 **끝까지 가려지지 않는가**
- [ ] Play (rev2) — 집었다가 **그 자리에서 잠깐 머물면** 라벨이 뜨는가 (H2 — 가장 빠른 취소의 발견성)
- [ ] Play (rev2) — 위로 빠르게 튕기는 드래그에서 라벨이 **깜빡이지 않는가** (H2 의 다른 쪽)
- [ ] Play — 취소 존 안에서 보드 하이라이트·사거리·거부 라벨이 사라진다
- [ ] Play — 취소 존을 나가면 하이라이트가 즉시 복구된다
- [ ] Play — 큰 맵(Serpent/Twin/Spiral) **최하단 행에 여전히 배치된다** (도달성 회귀 없음)
- [ ] Play — 탭 배치 비행이 트레이 위를 지나가도 취소되지 않는다(계약 5)
