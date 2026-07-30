# 3 — 선택 중 액티브 허용 (선택만 해제 + 손패 유지)

## 목적

"유닛 선택 중에는 액티브를 쓸 수 없습니다" 규칙을 없앤다. 액티브를 끌면 선택이 풀리고 평시 필드
조준 상태로 나온다 — 해제하고 다시 집는 왕복을 없앤다. **이 unit 은 독립**(먼저 나가도 된다).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` (차단 제거 + 신호 발화)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (신호 이벤트 추가)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` (해제 경로 신설)

## 구현

1. **차단 제거**: `OnBeginDrag` 의 `selection-active-block` 분기와 `OnPointerClick` 의
   `ShowSelectionBlocked()` 경로를 삭제한다.

2. **해제 트리거는 드래그 시작만.** press 에서 풀면 선택 중 **탭 즉발 부착**이 깨진다 —
   `OnPointerClick` 은 `OnEndDrag` 보다 **먼저** 발화하고 `_dragging || IsPortalAiming` 만으로
   가드되므로, press 에서 선택을 놓으면 탭 경로가 매번 죽는다.

3. **신호 배선**: `DreamcatcherHandView` 에 이벤트 하나 추가(예: `SelectionReleasedForAim`).
   슬롯 `OnBeginDrag` 에서 (Active && `SelectionTarget != Entity.Null`) 이면 발화하고
   `DcInspectController` 가 구독한다.
   **`AimingNow()` 폴링 금지** — 그 판정은 `GameManager.IsAiming || drag.IsAiming` 이고 후자는
   **배치 방향지정** 조준이다. 폴링하면 아무도 요청하지 않은 "방향지정 중 선택 해제" 까지 새로 생긴다.

4. **`DcInspectController.ReleaseSelectionKeepHand()` 신설.**
   걷는 것: `_selected`/`_anchorMissFrames` 리셋 · `panel.Hide()` · `_reticleShown` 가드를 지난
   `focus.End()`(카드 조준 세션 보호) · **`_slomoLease.Dispose()`** · `handView.ClearSelectionTarget()`.
   - **`_slomoLease` 를 반드시 놓는다.** 이건 인스펙트 **자기** lease(`AcquireSlomo`, priority 50,
     scale 0.3)이고 지금은 `Close()` 에서만 해제된다. 안 걷으면 손패가 열려 있는 동안엔 조준 lease 와
     값이 같아 보이지 않다가, **손패가 닫힌 뒤 `TimeDomain.Battle` 을 0.3× 로 고착**시킨다
     (`TimeManager` 는 lease 를 스택으로 들고 (priority desc, scale asc)로 승자를 뽑는다).
     조준 슬로모는 `DreamcatcherHandView` 가 소유하는 **다른** lease 라 영향 없다.
   - **부르지 않는 것**: `handView.CloseFromSelection()` — 그 안의 `CancelAllCardInteraction()` 이
     방금 시작한 드래그를 취소한다.
   - `ClearSelectionTarget()` 은 **반드시** 부른다 — 안 부르면 탭 즉발 부착이 해제된 선택을 계속
     타겟으로 본다(`_view.SelectionTarget` 이 그 경로의 게이트).

5. **줌은 자동 복귀한다 — 신규 API 를 만들지 말 것.** `CameraDirector` 에 인스펙트 해제 창구는
   **없다**(피드 staleness 자동 해제가 설계). `TickSelectionAnchor` 는 `!AimingNow()` 일 때만
   `SetInspectFocus` 를 피드하고, `OnBeginDrag` 이 `IsAiming = true` 를 세우므로 다음 프레임부터
   페이드아웃으로 풀린다. 이 unit 은 **확인만** 한다.

6. **인스펙트 재진입 방지는 기존 게이트로 충분**(회귀 확인 항목, 작업 아님): 손패 dismiss 캐처의
   press-프레임 스냅샷에 `IsAiming` 이 포함되고, `DcInspectController.TapGated()` 는 손패 상태와
   `AimingNow()` 양쪽에서 true 다. 신규 가드 금지.

## 완료 기준

- [ ] 선택 상태에서 액티브 카드를 끌면: 선택·패널·리티클·줌이 풀리고 **손패는 유지**되며 조준이
      이어진다. 커밋/취소 후 상태는 "선택 없는 평시".
- [ ] **슬로모 누수 없음**: 위 흐름 뒤 손패를 닫았을 때 전투 속도가 1.0 으로 돌아온다.
- [ ] 선택 중 **부착 카드 탭 즉발**은 그대로 동작(press 에서 해제하지 않았음의 증거).
- [ ] 액티브 조준 중 보드 탭이 인스펙트를 열지 않는다(포탈 2단계 포함) — 회귀 확인.
- [ ] 조준 중 phase 이탈 → lease·`IsAiming`·점등 누수 없음.
- [ ] 콘솔 에러/워닝 0.

> 확인 2026-07-30 — 커밋 `2b8b3efd` · 사용자 Play 육안 확인 완료.
