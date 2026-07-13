# 5 — Handoff Summary

각성 손패를 StS/HS 카드감으로 재설계. 순수 프레젠테이션(ECS 변경 0, 채널 변경 0).

## Commit

- `c56d3bd5` docs — StS/HS 재설계 spec (units 0~3)
- `34815ed8` docs — 모바일 피벗(hover → press-to-lift + idle, close 재번호)
- `3f574a9c` feat — units 0~3 (아치 부채 + press-lift + 덱-드로우 딜 + idle)
- `f34ce20e` feat — unit 4 (퇴장 침강 sink)

(구 `c9ee68d8` = 초기 "버튼 딜링" spec — 재설계로 폐기됨. 히스토리로만 참조.)

## Implemented

- **아치 부채**(unit 0): 평면 행 → 포물선 `y = handBaseY + arcHeight·(1−t²)` + 겹침(`step = cardW − cardOverlap`) + 접선 회전 `−t·rotMax`.
- **스프링 모델**(unit 0): 슬롯이 `targetPos/targetRotZ/targetScale` 를 갖고 `SpringSlots` 가 매프레임 실시간 lerp. focus/idle/드래그/딜이 **한 모델** 공유. 드래그·전이 슬롯은 skip.
- **눌러서 들기**(unit 1, 모바일): `DreamcatcherCardDragSlot` 의 `OnPointerDown/Up` → `SetFocus/ClearFocus`. 누른 카드 raise/확대/펴짐/최상단 + 이웃 scatter. **hover 아님**(터치 대응).
- **덱-드로우 딜**(unit 2): 하단 덱에서 곡선 상승 → 아치 OutBack 안착 + 원근 틸트(dealTiltX) + squash flex(PunchScale). 각성 버튼 `Pulse()`.
- **idle**(unit 3): 무입력 index-위상 bob/sway 를 스프링 target 에 합성(focus 카드 제외). `idleBobY=0` 으로 정지 가능.
- **퇴장 침강**(unit 4): `Close` → 카드가 하단 덱으로 역스태거 InBack 침강+축소 → strip 폴드 인.
- critic 리뷰 반영: 드래그/전이/빈슬롯 focus 가드, `OwnedByInteraction` 헬퍼(3중 복제 제거), stale 주석 정리.

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 아치 기하(`EnsureSlots`), `SpringSlots`(+idle), press-focus(`SetFocus/ApplyFocusTargets`), 딜(`StartDeal`), 침강(`StartSink/OnSinkComplete`), `RotateX`(strip fold 공용).
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `IPointerDown/UpHandler` → focus 보고. 드래그 시작 시 `SetFocus(-1)`.
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — `Pulse()`(패널 punch + fill 발광, unscaled).

## Verified

- compile 클린(에러/경고 0), 콘솔 NRE 는 에디터 인스펙터 내부 버그로 무관.
- Play(사용자 포커스): 딜(하단 덱→아치, pulse), 아치 형태, press-lift(누름 raise/scatter, 뗌 복귀), idle bob, 침강 close, 사용 후 자동복귀, 재오픈 재딜 — 전부 통과.

## Notes (되돌리면 안 되는 의도)

- **모든 연출은 realtime**: Unity `Time.timeScale` 은 항상 1(TimeManager 도메인 슬로모). PrimeTween 기본 타이밍·`SpringSlots`의 `Time.deltaTime`·Pulse 의 `unscaledDeltaTime` 이 UI 를 슬로모에 안 눌리게 한다.
- **teardown 에서 `StopDeal`**: `_dealSeq`(딜/침강) 는 `ForceClose`/phase 이탈/`OnDisable` 에서 Stop. `ForceClose` 하드스톱은 `OnSinkComplete` 를 건너뛰고 스스로 panel/strip 정리(이중 방지).
- **focus/idle 는 target 만 조작**, rect 직접 X. 드래그/전이 슬롯은 `OwnedByInteraction` 으로 스프링·focus에서 제외(DragSlot 이 rect 소유).
- 딜 소스는 **하단 덱**(각성 버튼 아님). 초기 "버튼 정확좌표 딜"은 UI스럽다는 사용자 판정으로 폐기.
- 슬로모 lease·페이즈 가드·drag 서비스 계약 불변. 연출만 교체.

## Follow-up

→ README "비목표 / 후속 후보" 참조. 핵심: **진짜 버텍스 커브(②-A) + 꼬깃꼬깃 펴짐(③)** 은 서브디바이드 메시(+셰이더) 토대 공유라 별도 spec. 카드/딜/focus SFX, 사용 카드 소비 강조도 후속.
