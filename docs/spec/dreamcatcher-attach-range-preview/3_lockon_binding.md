# 3 — 락온 연동 (드래그 슬롯이 arm / clear 한다)

> 선행: unit 1(`ResolveCard`) · unit 2(`SetAttachPreview`). 결정 1(락온 순간) · Q5(무효 = 0) · D5(손 떼면 즉시).

## 목적

카드 드래그 중 **유효한 락온이 성립한 순간** 프리뷰를 켜고, 대상이 바뀌면 옮기고, 락온이 풀리거나 드래그가
끝나면 즉시 지운다. 소유자는 락온을 이미 소유한 `DreamcatcherCardDragSlot` 하나다.

## 변경 대상

- `UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`
  - 필드 `DcRangeSpec _attachRangeSpec`.
  - `BeginFocus(slot)` (`:252`, `AimMode.Defender` 분기) — `_attachRangeSpec = DcRangeCatalog.ResolveCard(slot.card)`
    **드래그 시작 1회**(`_attachable` 스냅샷과 같은 자리 — managed SO 읽기는 per-frame 금지).
  - `UpdateUnitHover` (`:713`) — `_hoverEntity` 대입 직전 전환 지점(`:760~767`)에서 `found != _hoverEntity` 일 때만:
    `found != Entity.Null && _attachable.Contains(found) && _attachRangeSpec.shape != None`
    → `bridge.SetAttachPreview(found, _attachRangeSpec, focusCfg.attachRangeStyle)` / 그 외 → `bridge.ClearAttachPreview()`.
  - `EndInteraction` (`:484`) — `ClearAttachPreview()` 추가(`ClearAimRange()` 옆). 커밋·취소·`OnDisable` 이 전부 여기.
- `DreamcatcherHandView.ForceClose`(`:1077`) 는 첫 줄 `CancelAllCardInteraction()` → `CancelDrag()` → `EndInteraction()`
  을 탄다 — **추가하지 않는다**. 완료 기준의 잔류 0 확인으로 대신한다(경로가 바뀌었으면 그때 얹는다).

## 구현

- **전환 시에만 호출.** 추종은 브리지 LateUpdate(unit 2). 기존 `lockView.PlayPunch()` 가 같은 조건을 감지하는 자리.
- **정체 히스테리시스 뒤**에 건다 — 밀집 손끝 흔들림에 링이 옮겨 다니지 않는다.
- **무효 락온 = 표시 0.** `_attachable` 은 드래그 중 불변 스냅샷. 「왜 안 붙나」는 리티클 invalid 폼·콜아웃이 말한다.
- `OnEndDrag`(`:292`)는 `UpdateUnitHover` 재호출 → 커밋 → `EndInteraction` 순 — 별도 처리 없음. 카드 비행 중 링은
  이미 없다(D5). 확정 신호는 리티클 수렴·펄스(attach-lockon 계약 E).
- `Squad` 는 `Defender` 와 같은 경로(`Classify`). `EnemyMark`·`TileAim` 은 `_attachRangeSpec` 을 만들지 않는다.

## 완료 기준

> 구현 커밋 `240e2b04`(2026-09-02). `ForceClose` 는 `CancelAllCardInteraction` → `EndInteraction` 경유 확인 — 추가 없음.

- [ ] `cornered_burst` 드래그 → 유효 유닛 락온 순간 점등 · 다른 유효 유닛으로 이동 시 이동 · 빈 곳 이동 시 소멸 ·
      손 떼면(커밋/취소) 즉시 소멸.
- [ ] full 3/3 유닛 락온 → 링 없음(리티클 invalid 폼만).
- [ ] 비공간 카드(`frenzy` · `frostbite` · `poke_needle`) 드래그 → 어떤 락온에도 링 없음, `_rangeOwner` 무변.
- [ ] 드래그 중 항아리 토글·페이즈 전환(`ForceClose` 경로) → 잔류 링 0.
- [ ] `EndInteraction` 경유 3종(커밋·취소·OnDisable) 전부 소멸.
- [ ] sim 파일 변경 0 · 골든 바이트 무변.
