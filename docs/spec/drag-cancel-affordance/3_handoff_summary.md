# 3 — Handoff Summary (drag-cancel-affordance)

## Commit

- `c377b60f` feat(drag-cancel-affordance): units 0~2 — 배치/카드 드래그 취소 수단

## Implemented

- **유닛 D&D 취소 존** — 트레이 패널 rect 로 되돌리면 취소. 판정은 **가상 포인터**(손가락 + 6% 오프셋)
  기준이라 최하단 행 배치 도달성과 좌표가 겹치지 않는다(README "도달성 무손실").
- **취소 예고** — 존 안에서 프리뷰 실루엣이 고스트 알파(0.4), 보드 하이라이트·사거리·액체·거부 라벨
  소거, 트레이 위에 `✕ 놓으면 취소` 배너.
- **예고 게이트** — 배너/판정정지는 존을 **한 번 벗어난 뒤**부터. 트레이 드래그는 존 안에서 시작하므로
  게이트가 없으면 모든 드래그가 배너 깜빡임으로 시작한다. 릴리즈 취소 판정 자체는 처음부터 유효하다.
- **드림캐쳐 취소 rect** — 패널(232)이 아니라 **보이는 카드 부채(310)** 로. 패널 자식이라 하강(210px)을
  자동 승계하고, 판정 3곳(드롭 · 포탈 출구 탭 · 브리핑)이 `CancelRect` 하나를 본다.
- **손패 취소 힌트** — 취소 존 안이면 손패 위에 같은 `✕ 놓으면 취소` 배너(상단 툴팁과 이중 표기).
- **ESC/Android 뒤로가기** — 유닛 드래그 취소 · arm 해제. 시뮬 비행(탭 배치)은 제외.
- **회귀 가드 2건** — `DragPlacementReachTest` 에 "최하단 행을 노리는 가상 포인터가 트레이 rect 밖"
  단언(BottomSafeRatio 근사 대신 실제 트레이 rect). 신규 `DragCancelZoneTest` 가 (1) 취소 존
  릴리즈 = 무차감 종료, (2) 손가락 트레이 안 + 조준점 트레이 밖 = 취소 아님 을 잰다.

## Key Files

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 취소 존 판정·예고·릴리즈·ESC
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `SetCancelZone` 주입(BuildCanvas 말미 + EnsureDragController)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `CancelRect` / `SetCancelHint` / CancelZone 빌드
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `InsideCancelZone` 단일 진입점
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑫ 취소 룩 노브
- `Assets/_Project/Tests/PlayMode/DragCancelZoneTest.cs` — 무차감 취소 + 판정 좌표계 가드(신규)
- `Assets/_Project/Tests/PlayMode/DragPlacementReachTest.cs` — 겹침 금지 단언

## Verified

- 컴파일 CS 에러 0 (경고는 기존 CS0618 뿐)
- EditMode 1599개 중 실패 1 — `MultiGoalPoolSeparationTests(MapDocument_Zig)`. **이 spec 과 무관**:
  `MapDocument_Zig.asset` 이 세션 시작 시점부터 워크트리에 dirty 였고(맵 authoring 편집), 이 spec 은
  맵 데이터를 건드리지 않는다.
- PlayMode 72개 중 실패 12 — **전부 기존 실패**(AuthE2E 서버 500 중복키 · PrimeTween OnComplete 로그 ·
  LogAssert 경고 미포착 2건 · 저장 덱 폴백 0 · Squad/Dreamstone carry-in 이 Gift 에서 멈춤 ·
  SceneTransition · CardBuffs/PlacementAura 의 가디언 ×1.24 여분 — memory
  `project_playmode_cardbuffs_preexisting_fail` 에 기록된 그 건). 변경 전 실행과 같은 목록이고
  배치 드래그 계열은 하나도 없다. `DragCancelZoneTest` · `DragPlacementReachTest` ·
  `RelocationPlacementSessionTest` 전부 통과.

## Notes (되돌리면 안 되는 것)

- **유닛 취소 판정은 가상 포인터다.** raw 손가락으로 바꾸면 큰 맵(Serpent/Twin/Spiral) 최하단 행이
  통째로 배치 불가가 된다 — 그 행을 노리는 손가락이 트레이 y 대역 안에 있기 때문. 추가된 PlayMode
  단언이 그 순간 울린다.
- **드림캐쳐 취소 rect 는 패널 자식이어야 한다.** 하강(210px)이 패널 `anchoredPosition` 하나로
  일어나므로, 별도 좌표를 두면 하강 중 판정이 어긋난다(hand-drag-clearance 계약 1).
- **`cancelZoneHeight` 310 을 더 키우지 말 것.** 하강 후 top 132 가 가장 큰 맵의 보드 하단
  모서리(167) 아래라는 게 최하단 행 부착이 살아 있는 근거다. 키우면 hand-drag-clearance 가 푼
  문제가 되돌아온다.
- **시뮬 경로(`_simulatedDrag`)는 취소 대상이 아니다.** 탭 배치 비행은 이미 코스트가 지불된
  확정 배치의 연출이라, 끊으면 유닛이 사라진다.
- **예고 게이트(`_cancelZoneLeft`)는 릴리즈 판정에 걸지 않았다.** 존을 못 벗어난 짧은 드래그도
  놓으면 취소가 맞다(그게 랜덤 하단 셀 오배치보다 낫다).

## Follow-up

- 사용자 Play 확인 — 각 unit 문서의 미체크 항목(코스트 무차감 · 도달성 회귀 없음 · 배너 잔류 없음).
- 실기기 — Android 뒤로가기 취소 동작 확인.
- 방향 지정 페이즈 / 재배치 취소는 범위 밖(README 후속 후보 · spec README Follow-up Backlog
  "배치 취소/코스트 환불" 과 같은 건).
