# 3 — 카드 탭 즉발 부착 (Unit/Squad) + 불가 움찔 피드백

## 목적

선택 유닛이 있을 때 손패 카드 **탭**(드래그 임계 미만)으로 그 유닛에 즉시 부착한다.
즉발 불가(카드 종류/게이지/캡/적용 불가)면 카드가 **움찔**하고 사유를 보여준다 — 무차감
(사용자 결정 4). D&D 는 모든 카드 기존 그대로.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `IPointerClickHandler` 추가
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 움찔 트윈(뷰가 슬롯 rect 소유)

## 구현

### A. 탭 판정 — `OnPointerClick`

UGUI 클릭 = press→release 가 드래그 임계(DPI 보정, `GameManager.CalibrateDragThreshold`) 미만.
드래그로 이어지면 `OnBeginDrag` 가 클릭을 삼킨다(`DefenderDragSlot.OnPointerClick` 탭-arm 선례).

가드(순서대로, 전부 무차감):

1. `_view.SelectionTarget == Entity.Null` → **기존 동작 그대로**(press 브리핑만) — 선택 없으면
   즉발 개념 자체가 없다. 움찔도 없음.
2. `CanPeek(_index)` 실패(전환 중/타 인터랙션/재딜 중) → 무시.
3. `Classify(card)` 가 `Defender` 가 아니거나 `card.type == Active` → **움찔 + 사유**
   ("이 카드는 끌어서 사용하세요" 계열 — 즉발은 Unit/Squad 부착만, 결정 4).
4. `!slot.usable`(게이지) → 움찔 + 기존 문안 "각성치가 부족합니다".
5. `!Controller.CanAttachMore(target) || !Bridge.WouldDreamcatcherCardApply(target, card)` →
   움찔 + 기존 문안 "이 유닛에는 부착할 수 없습니다" (D&D `_attachable` 판정과 동일 3판정 — 계약 5).

통과 시 커밋 — D&D 성공 경로와 **동일 형태**:

```
CommitNow(() => _view.Controller.CommitAttach(entryId, target),
          () => _view.FlyCardToUnit(startUiWorld, ghostSize, face, target));
```

발사점/고스트 캡처는 커밋 전(기존 계약). 성공 시 `Focus.Confirm()` 펄스는 `CommitNow` 가
이미 처리 — 펄스 중심은 락온 렉트가 없으므로 `_locked`(선택 리티클의 대상) 폴백 경로를 탄다.
유지/자동닫힘/재딜인은 `HandChanged(Used)` 가 처리(계약 4 — 신규 소비 경로 없음).

### B. 움찔 피드백 — 뷰 소유

- `DreamcatcherHandView.FlinchSlot(int index)` — 슬롯 rect 에 짧은 수평 셰이크
  (PrimeTween `Tween.PunchAnchoredPosition` 계열, 재딜/드래그 소유 슬롯은 no-op).
  진폭/시간은 `[SerializeField]` (제약 6). 끝나면 `RestoreSlotHome` 스냅 없이 스프링 복귀
  (`targetPos` 불변이므로 SpringSlots 가 알아서 끌어온다).
- 사유 표시는 기존 press 브리핑 채널 재사용(`ShowDragBriefing`/`UpdateDragBriefingStatus`) —
  신규 텍스트 표면 금지(use-flow 계약 5 "조준 중 텍스트를 늘리지 않는다" 의 정신 — 기존 표면만).

## 완료 기준

- [ ] compile 클린
- [ ] Play: 유닛 선택 → Unit/Squad 카드 탭 → 카드가 유닛으로 비행·부착, 게이지 차감,
      손패 유지+재딜인(use-flow 규칙)
- [ ] Play: Active/적표식 카드 탭 → 움찔 + 사유, 무차감 · 같은 카드 D&D 는 정상
- [ ] Play: 게이지 부족 카드 탭 → 움찔 + "각성치 부족", 무차감
- [ ] Play: 부착 캡 찬 유닛 선택 후 카드 탭 → 움찔 + "부착 불가", 무차감
- [ ] Play: 선택 없음(항아리 단독 오픈) 카드 탭 → 현행과 동일(움찔 없음, 브리핑만)
- [ ] Play: 탭↔드래그 판별 — 카드를 끌면 즉발이 발화하지 않는다(클릭 삼킴)
