# 1 — Handoff Summary

## Commit

- feat(dreamcatcher-hand-drag-clearance): unit 0 — 조준 중 손패 하강 (해시는 후속 docs 커밋에서 기재)

## Implemented

- 카드 press~release 동안 손패 패널 210px 스프링 하강(spring 320/damping 24) — 카드 헤더(이름) 띠만 남고, 큰 맵 최하단 행이 셀째로 드러남
- press = 하강, release = 복귀 통일 (단순 탭 / 드래그 미부착 / 부착 성공 전부 동일). 포탈 2탭 대기만 하강 유지
- 취소 rect(HandPanelRect)는 패널 이동을 자동 승계 — 판정 코드 무변경
- 포인터 추종 카드(ActiveTile/ActivePortal)는 패널 이동 시 화면 위치 보존(`ApplyClearanceOffset` + `DragSlot.IsPointerFollowing`)
- 리셋은 Open/ForceClose/OnSinkComplete 3점 — Close 에 두면 성공 경로에서 손패 팝(§D)

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 노브·TickHandClearance·ApplyClearanceOffset·ResetHandClearance
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `IsPointerFollowing` 읽기 전용 1개
- `docs/spec/dreamcatcher-hand-drag-clearance/0_hand_drop_on_drag.md` — 계약·완료 기준

## Verified

- 컴파일 CS 0 · EditMode 1538/1541 (실패 1 = MobileBuild 프리플라이트 사전 실패, clean HEAD 재현으로 무죄 판정 — 테스트 리그)
- 사용자 Play 확인 2026-07-29 "이상없음" (Serpent 최하단 부착·press/release·추종 카드 고정 포함)

## Notes (되돌리면 안 되는 것)

- **held 판정 = `_focusIndex >= 0 || AnyInteractionActive()`** — OnBeginDrag 가 `_dragging` 을 `SetFocus(-1)` 보다 먼저 세워 전환 무갭. 순서 뒤집으면 press→drag 사이 1프레임 복귀 팝
- 하강량을 취소 판정에 별도 반영 금지(패널 위치 단일 소유) · 슬롯 homePos/targetPos 불변
- 헤드룸식 피드 주도 복사 금지 — OnDrag 는 포인터 정지 시 안 옴(README 기각 이력)
- use-flow unit 0(슬로모 재배치)이 이 held 신호를 공유한다 — 다음 커밋

## Follow-up

- 실기기 SafeArea: 하강분이 홈 인디케이터 대역과 경합하는지 (에디터 확인 불가)
- README 후속 후보 3건(취소 rect·부채 불일치 / 프레이밍 맵 크기 의존 / Serpent 하단 행 재배치)
