# 0 — 슬로모 재배치: 열림이 아니라 "잡은 동안"

## 목적

손패를 열어두는 것의 공짜 보험(상시 슬로모)을 없애고, 슬로모를 실제 결정 순간
(카드를 잡고 조준하는 동안)에만 건다. 열림 상태에서는 게임이 실속으로 돈다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`

## 구현

### A. 리스 수명 이동

현재 `Open()` 이 `TimeManager.Request(TimeDomain.Battle, slomoTimeScale 0.3, priority 50)` 로
리스를 잡고 `Close()`/`ForceClose()` 가 `Dispose` 한다. 이 획득/해제를 **press~release 수명**으로
옮긴다:

- 판정 신호는 손패 하강(`TickHandClearance`)이 쓰는 것과 **동일한 식** —
  `_focusIndex >= 0 || AnyInteractionActive()`. 신호가 true 로 바뀌는 프레임에 리스 획득,
  false 로 바뀌는 프레임에 Dispose. 매 프레임 재획득하지 않는다(에지 트리거).
- 포탈 2탭 대기는 `IsPortalAiming` 이 신호에 포함돼 있어 **자동으로 슬로모 유지** —
  손패 하강 유지와 같은 예외, 같은 이유(손을 뗐어도 조준이 안 끝났다).
- `Open()` 의 리스 획득 제거. `Close()`/`ForceClose()` 의 `Dispose` 는 **유지**(신호가
  끊기기 전에 강제 종료되는 경로의 최후 안전망 — 이중 Dispose 는 무해).

### A′. A/B 토글 (사용자 요구 2026-07-29)

구/신 동작을 Play 중 넣다 뺐다 비교할 수 있어야 한다:

```
[SerializeField] private bool slomoOnOpen = false;  // true = 구동작(열림 전체 슬로모)
```

리스 판정을 `slomoOnOpen || held` 로 두고 **매 프레임 폴링 + 에지 트리거**로 획득/해제하면,
인스펙터에서 토글하는 즉시(손패가 열려 있어도) 반영된다 — Open 시점 1회 획득 방식으로는
라이브 토글이 안 된다. 기본값 false(신동작). 씬에 키가 없으므로 코드 기본값이 곧 라이브 값.

### B. press~release 3중 결합

이 unit 이후 press/release 는 세 가지를 한 신호로 움직인다:
**슬로모 ON/OFF + 손패 하강/복귀 + (기존) press-lift focus.**
감각적으로 "카드를 잡는다 = 세상이 느려지고 시야가 열린다"가 된다.

### C. 주의 — 되돌리면 안 되는 것

- 딜/침강 등 손패 자체 연출은 원래 realtime(UI 도메인)이라 슬로모 재배치와 무관 — 건드리지 않는다.
- 슬로모 배율(0.3)·priority(50) 값 불변 (계약 7).
- `TryFastForwardDeal` 경로: 딜 중 press 도 신호를 세우므로 슬로모가 함께 걸린다 — 의도된 동작.

## 완료 기준

- [x] 컴파일 통과, EditMode 신규 실패 0 (1538/1541 — 유일 실패는 MobileBuild 사전 실패)
- [x] Play — 손패만 열어둔 상태에서 적 이동/전투가 **실속**인가
- [x] Play — 카드를 누르는 순간 슬로모, 떼는 순간 해제되는가 (하강/복귀와 동기)
- [x] Play — 포탈 입구 확정 후 출구 탭 대기 중에도 슬로모가 유지되는가
- [x] Play — 조준 중 페이즈 강제 이탈/손패 강제 닫힘에서 슬로모가 남지 않는가
- [x] Play — 손패 열린 채 인스펙터에서 `slomoOnOpen` 토글 시 **즉시** 구/신 동작이 전환되는가
- [x] 체감 — press 마다의 1.0↔0.3 스냅이 거슬리는지 확인 (사용자 "이상없음" — 보간 승격 안 함)

확인: 2026-07-29 사용자 Play 확인("이상없음") — clearance 와 한 제스처로 통합 검증
(press = 슬로모+하강, release = 해제+복귀, slomoOnOpen 토글 즉시 전환 포함). 커밋 `b6415456`.
