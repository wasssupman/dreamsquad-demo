# 6 — Handoff Summary (units 0~4 구현 완료 · Play 검증 대기)

## Commit

- `9d719953` refactor(selection-hand-attach): unit 0 — `Blocked()` 를 close-trigger / tap-gate 로 분리
- `1c51e312` feat(selection-hand-attach): unit 1 — 선택 시 손패 자동 오픈 + 모드 분기 + 선택 수명
- `4ea13544` feat(selection-hand-attach): units 2~4 — 보드 탭 라우팅 + 탭 즉발 부착 + 리티클 재주장
- (선행) `6c152b74` unit-dreamcatcher-inspect unit 6 — 선택 리티클(이 spec 의 시각 전제)
- (설계) `af500701` spec 초안 → `9fdaba6a` critic REVISE 반영 rev 2

## Implemented

- 유닛 선택과 손패가 **공존**한다 — `MustClose()`(배치 드래그·arm / 이동모드)만 선택을 닫고,
  손패 오픈·조준은 `TapGated()`(새 탭만 차단)로 내려갔다.
- 선택 시 손패가 **항상** 열린다. 침강(`Transitioning` ~0.4초) 중 선택은 `_pendingSelectionOpen`
  래치로 예약돼 전이 종료 첫 프레임에 열린다. 선택 기인 오픈은 항아리 `Pulse()` 를 쏘지 않는다.
- 손패 오픈 중 보드 탭: **유닛 = 선택 전환(손패 유지)** / **빈 보드·재탭 = 손패+선택 동시 해제**.
- 카드 **탭 = 선택 유닛에 즉발 부착**(Unit/Squad). 불가(카드 종류/게이지/캡/적용 불가)는
  좌우 움찔 + 기존 브리핑 사유, 차감 0.
- 선택 리티클(콜아웃 = portrait + 유닛 이름)이 조준 세션에 밀려도 종료 시 **1회 재주장**된다.
- 조준 중에는 인스펙트 줌 피드를 끊어 타일/출구 조준 프레이밍을 돌려준다.
- 선택 유닛 사망은 **앵커 소실 연속 3프레임**으로 감지해 닫는다(부착 0장 유닛은 `AttachmentsChanged`
  가 아예 발화하지 않는다).

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 선택 상태 소유자.
  `MustClose`/`TapGated`/`AimingNow`, `TickSelectionAnchor`(줌 피드 + 수명), `OnBoardTapped`,
  `CloseByIntent`, `ShowSelectionReticle`, `OnFocusSessionReleased`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `SelectionTarget`/
  `InSelectionMode`, `OpenForSelection`/`CloseFromSelection`, `TickPendingSelectionOpen`,
  `BoardTapped`, `InteractionEnded`/`FocusCleared`, `FlinchSlot`
- `Assets/_Project/Scripts/UI/Dreamcatcher/HandDismissTapCatcher.cs` — press-스냅샷 탭 캐처(신설)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `OnPointerClick` 즉발 + `CommitNow` 펄스 사전 캡처
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` — `TryCaptureConfirmCenter`/`Confirm(center)`
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — `IsOverUi` 캐처 단독 히트 예외

## Verified

- `dotnet build Wassup.Runtime.csproj` → **0 error / 0 warning** (편집 6파일 전부 이 어셈블리에 포함 확인)
- Unity 스크립트 컴파일 후 콘솔 error/warning **0**
- **Play 미검증** — `5_wiring_play_validation.md` 시나리오 1~12 가 남아 있다. unit 0 만 사용자
  Play 확인을 받았다(2026-07-29).

## Notes (되돌리면 안 되는 의도)

1. **`FocusCleared` 는 `_focus.End()` 뒤에 발화**한다. 앞으로 옮기면 재주장한 리티클을 그 `End()`
   가 다시 지운다 — 함수의 마지막 포커스 동작이 항상 재주장 트리거여야 한다.
2. **캐처는 press 프레임 스냅샷으로 판정**한다. release 시점 상태 판정으로 되돌리면 포탈 출구
   탭의 릴리즈가 보드 탭으로 새어 선택을 전환/해제한다(커밋이 press 프레임에 상태를 내린다).
3. **탭 즉발의 가드 0(`_dragging || IsPortalAiming`)을 지운 채 `CanPeek` 만 믿지 말 것.** UGUI 는
   드래그로 이어진 press 의 클릭을 삼키지 않는다(`eligibleForClick` 은 press/drag 핸들러가 다른
   GO 일 때만 해제). 지우면 "손패로 되돌려 취소" 가 즉발 부착으로 차감된다.
4. **`flinching` 플래그**가 없으면 `SpringSlots` 가 셰이크를 매 프레임 홈으로 끌어 뭉갠다.
5. **선택 슬로모는 상시 유지**가 사용자 결정(결정 5)이다. use-flow 계약 1 의 명시 예외로 README
   계약 8 에 기록돼 있다 — "슬로모가 왜 안 풀리나" 로 되돌리지 말 것.
6. `DefenderRelocationController.cs` 는 타 세션의 `placement-thumb-occlusion` 작업과 공유 중이라
   `IsOverUi` hunk 만 선별 스테이징했다. 그 파일의 나머지 변경은 이 spec 소관이 아니다.

## Follow-up

- **unit 5 Play e2e**(시나리오 1~12) + 실기기 스모크. 특히 ⑦포탈 출구 탭, ⑨부착 0장 사망,
  ③침강 중 재선택 래치, ⑩보드 스와이프가 이번 구현의 신규 방어선이다.
- 카메라 체감(인스펙트 줌 + 손패 헤드룸 동시)이 과하면 config 노브만 튜닝(코드 분기 금지).
- `HandOpened` 원샷 튜토리얼 힌트가 선택 기인 오픈에서 소진되는지 확인(문안 개정은 후속 후보).
- README 후속 후보: 즉발의 Active-DefenderUnit 확장 / 겹친 유닛 픽킹 정밀도 / 오탭 언두.
