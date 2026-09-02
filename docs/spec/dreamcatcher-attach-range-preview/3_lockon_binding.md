# 3 — 락온 연동 (드래그 슬롯이 arm / clear 한다)

> 선행: unit 1(`ResolveCard`) · unit 2(`SetAttachPreview`). 사용자 결정 2026-09-01 결정 1(락온 순간) ·
> 2026-09-02 Q5(무효 락온 표시 0).

## 목적

카드 드래그 중 **유효한 락온이 성립한 순간** 프리뷰를 켜고, 대상이 바뀌면 옮기고, 락온이 풀리거나
드래그가 끝나면 즉시 지운다. 소유자는 이미 락온을 소유한 `DreamcatcherCardDragSlot` 하나다.

## 변경 대상

- `UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`
  - 필드 `DcRangeSpec _attachRangeSpec` 신설.
  - `BeginFocus(slot)` (`:252`, `AimMode.Defender` 분기) — `_attachRangeSpec = DcRangeCatalog.ResolveCard(slot.card)`
    **드래그 시작 1회**(`_attachable` 스냅샷과 같은 자리 — managed SO 읽기는 per-frame 금지).
  - `UpdateUnitHover` (`:713`) — `_hoverEntity` 대입 직전의 전환 지점(`:760~767`)에서
    `found != _hoverEntity` 일 때만:
    `found != Entity.Null && _attachable.Contains(found) && _attachRangeSpec.shape != None`
    → `bridge.SetAttachPreview(found, _attachRangeSpec, focusCfg.attachRangeColor)`
    그 외 → `bridge.ClearAttachPreview()`.
  - `EndInteraction` (`:484`) — `ClearAttachPreview()` 추가(`ClearAimRange()` 옆). 커밋·취소·`OnDisable`
    이 전부 여기로 모인다.
- `UI/Dreamcatcher/DreamcatcherHandView.cs:1077 ForceClose` — 슬롯 `EndInteraction` 을 거치지 않는
  경로가 있으면 거기에도 `ClearAttachPreview()`(attach-lockon 계약 10 하드 클리어 — 페이즈 전환·항아리
  토글·0장 자동 닫힘).

## 구현

- **전환 시에만 호출한다.** 매 프레임 호출하지 않는다 — 추종은 브리지 LateUpdate 가 한다(unit 2).
  기존 `lockView.PlayPunch()` 가 같은 조건(`found != _hoverEntity`)을 이미 감지하는 자리다.
- **정체 히스테리시스 뒤**에 건다 — `found` 가 히스테리시스로 `_hoverEntity` 로 되돌아온 뒤의 값이라
  밀집 손끝 흔들림에 링이 홱홱 옮겨 다니지 않는다.
- **무효 락온 = 표시 0(Q5).** `_attachable` 은 드래그 중 불변 스냅샷이라 게이트가 안정적이다.
  리티클은 invalid 폼으로 뜨고 링은 없다 — 「왜 안 붙나」는 리티클·콜아웃이 말한다.
- `OnEndDrag` (`:292`) 는 `UpdateUnitHover` 재호출 → 커밋 → `EndInteraction` 순이라 별도 처리 없음.
  카드가 유닛으로 날아가는 동안(`FlyCardToUnitDeferred`) 링은 **이미 사라진 상태**다 — 확정 비트는
  리티클 수렴·펄스가 담당(attach-lockon 계약 E).
- `Squad` 는 `Defender` 모드와 동일 경로(`Classify`)라 추가 작업 없음.
- `EnemyMark`·`TileAim` 모드는 `_attachRangeSpec` 을 만들지 않는다(비목표).

## 완료 기준

- [ ] 궁지폭발 드래그 → 유효 유닛 락온 순간 링 점등 · 다른 유효 유닛으로 이동 시 링 이동 ·
      빈 곳으로 이동 시 소멸 · 손 떼면(커밋/취소) 즉시 소멸.
- [ ] full 3/3 유닛 락온 → 링 없음(리티클 invalid 폼만).
- [ ] 비공간 카드(광란·서리 화살·니들) 드래그 → 어떤 락온에도 링 없음, `_rangeOwner` 무변.
- [ ] 드래그 중 손패 강제 종료(페이즈 전환) → 잔류 링 0.
- [ ] `EndInteraction` 경유 경로 3종(커밋·취소·OnDisable) 전부 링 소멸.
- [ ] sim 파일 변경 0 · 골든 바이트 무변.
