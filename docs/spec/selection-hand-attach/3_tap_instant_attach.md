# 3 — 카드 탭 즉발 부착 (Unit/Squad) + 명시 클릭 가드 + 움찔 피드백

> rev 2 (2026-07-29 critic 반영): "드래그가 클릭을 삼킨다" 전제 교정 + 명시 가드(M1) ·
> flinching 슬롯 소유권(M7) · 확정 펄스 캡처 시점(M5) · EnemyMark 배제 근거 명시.

## 목적

선택 유닛이 있을 때 손패 카드 **탭**(드래그 임계 미만)으로 그 유닛에 즉시 부착한다.
즉발 불가면 카드가 **움찔**하고 사유를 보여준다 — 무차감(결정 4). D&D 는 모든 카드 기존 그대로.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `IPointerClickHandler`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 움찔 트윈(뷰가 슬롯 rect 소유)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` — 펄스 중심 사전 캡처(M5)

## 구현

### A. 탭 판정 — `OnPointerClick` + 명시 가드

**전제 교정(critic M1)**: UGUI 는 "드래그로 이어지면 클릭이 죽는다"를 보장하지 않는다 —
`eligibleForClick` 해제는 `pointerPress != pointerDrag` 일 때만이고, 카드 슬롯은 press/drag
핸들러가 같은 GO 라 **드래그 내내 eligible 유지 + 클릭이 `OnEndDrag` 보다 먼저 발화**한다.
"손패로 되돌려 취소" 제스처가 릴리즈 지점에 따라 클릭도 발화시킨다. 명시 가드가 필수다
(레포 선례: `DraftCardView.cs:101` 의 `_dragHappened` 차단. `DefenderDragSlot.cs:67` 주석은
같은 오해를 담고 있으니 선례로 삼지 말 것).

가드(순서대로, 전부 무차감):

0. **`if (_dragging || IsPortalAiming) return;`** — 드래그/조준 릴리즈의 동반 클릭 차단(M1).
   클릭은 `OnEndDrag` 보다 먼저 발화하므로 이 시점 `_dragging` 은 아직 true 다.
1. `_view.SelectionTarget == Entity.Null` → 기존 동작 그대로(press 브리핑만). 움찔도 없음.
2. `CanPeek(_index)` 실패(전환 중/타 인터랙션/재딜 중) → 무시.
3. `Classify(card) != Defender || card.type == Active` → **움찔 + "이 카드는 끌어서 사용하세요"**
   계열(즉발은 Unit/Squad 부착만 — 결정 4). **적 표식 카드는 `Classify` 가 `EnemyMark` 를
   반환하므로 이 조건이 자동 배제한다**(`card.type == Unit` 만 보고 통과시키는 오구현 금지).
4. `!slot.usable`(게이지) → 움찔 + 기존 문안 "각성치가 부족합니다".
5. `!Controller.CanAttachMore(target) || !Bridge.WouldDreamcatcherCardApply(target, card)` →
   움찔 + "이 유닛에는 부착할 수 없습니다" (D&D 판정과 동일 3판정 — 계약 5, 코드 검증 완료).

통과 시 커밋 — D&D 성공 경로와 동일 형태:

```
CommitNow(() => _view.Controller.CommitAttach(entryId, target),
          () => _view.FlyCardToUnit(startUiWorld, ghostSize, face, target));
```

발사점/고스트 캡처는 커밋 전(기존 계약). 유지/자동닫힘/재딜인은 `HandChanged(Used)` 가
처리(계약 4).

### B. 확정 펄스 캡처 시점 (critic M5)

`CommitNow` 는 `commit()` **뒤에** `Confirm()` 을 부른다 — 마지막 사용 가능 카드의 커밋은
동기 `OnCardUsed → Close() → Focus.End()` 를 타서 `Confirm()` 이 중심을 잃고 **펄스가 사라진다**.
프레젠터에 `TryCaptureConfirmCenter(out Vector2)` 를 추가하고 `CommitNow` 가 **커밋 전에**
중심을 캡처, 성공 시 캡처값으로 펄스를 쏜다(오버로드 `Confirm(Vector2)`). D&D 경로도 같은
결함이 있었으므로 함께 고쳐진다. 펄스는 독립 타이머라 `End()` 후에도 완주(기존 계약 유지).

### C. 움찔 피드백 — 뷰 소유 + 슬롯 소유권 (critic M7)

- **API 사실 확인**: `Tween.PunchAnchoredPosition` 은 PrimeTween 에 없다. `PunchLocalPosition`
  / `ShakeLocalPosition` / `PunchScale` 중 선택(구현 시 확정).
- **rect 소유권**: `SpringSlots()` 는 매 프레임 pos/rot/scale 을 전부 덮어써 트윈과 동시 writer
  충돌 — 셰이크가 뭉개진다. `redealing` 선례대로 **`flinching` 소유 플래그**를 슬롯에 추가:
  `SpringSlots`/`ApplyFocusTargets` 가 그 슬롯을 건너뛰고, 트윈 완료 콜백이 플래그 해제 +
  홈 복원. 강제 종료 경로(Refresh/ForceClose)는 `StopDeal` 의 `Complete()` 패턴 준용.
- 드래그/재딜 소유 슬롯(`OwnedByInteraction || redealing`)에는 움찔 no-op.
- 진폭/시간은 `[SerializeField]`(제약 6). 사유 표시는 기존 press 브리핑 채널 재사용
  (`ShowDragBriefing`/`UpdateDragBriefingStatus`) — 신규 텍스트 표면 금지.

## 완료 기준

- [ ] compile 클린
- [ ] Play: 유닛 선택 → Unit/Squad 카드 탭 → 카드가 유닛으로 비행·부착 + **확정 펄스**, 게이지
      차감, 손패 유지+재딜인 · **마지막 사용 가능 카드 탭**(자동 닫힘 확정)에도 펄스가 나온다(M5)
- [ ] Play: **카드를 끌었다가 같은 카드 위로 되돌려 놓기** → 취소만, 차감 0, 즉발 미발화(가드 0)
- [ ] Play: Active/적표식 카드 탭 → 움찔 + 사유, 무차감 · 같은 카드 D&D 는 정상
- [ ] Play: 게이지 부족/부착 캡/적용 불가 각각 탭 → 움찔 + 각 사유, 무차감
- [ ] Play: 움찔 중 스프링/호버와 겹쳐도 셰이크가 뭉개지지 않고 끝나면 홈 정착(M7)
- [ ] Play: 선택 없음(항아리 단독 오픈) 카드 탭 → 현행과 동일(움찔 없음, 브리핑만)
