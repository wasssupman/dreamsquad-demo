# 3 — Handoff Summary (drag-cancel-affordance)

## Commit

- `c377b60f` feat(drag-cancel-affordance): units 0~2 — 배치/카드 드래그 취소 수단
- `ec5e9c05` revert(drag-cancel-affordance): unit 2 철회 — ESC/뒤로가기 하드 취소 제거
- `c61aa51c` feat(drag-cancel-affordance): unit 0 rev2 + unit 3 — 예고를 정직하게, 보드 밖 취소 성립
- `ffd6ae28` refactor(drag-cancel-affordance): rev3 — 취소 배너 삭제, 예고 표면을 하나로
- `fbcac2db` — ⚠ **리뷰 반영분이 이 커밋에 들어가 있다.** 제목은
  `feat(first-session-tutorial): unit 15` 다. 병행 세션이 공유 인덱스를 커밋해 스테이징돼 있던
  이 spec 의 7파일(컨트롤러·SO·테스트 2·문서 3)이 함께 쓸려 들어갔다. 코드는 온전하고, 그쪽
  커밋 4개를 rewrite 하는 위험을 피하려 이력은 그대로 뒀다 —
  **커밋 제목으로 검색하면 안 나오므로 `git log -S CancelStateNow` 로 찾을 것.**

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
- **unit 2(ESC/뒤로가기 하드 취소)는 철회** — 모바일에서 드래그 중 back 은 손가락이 닿지 않는다
  (떼는 순간 이미 릴리즈). 코드 잔여물 0. 사유는 `2_escape_cancel.md` — 다시 제안하지 말 것.
- **rev2 (UX 리뷰 반영)** — 예고 게이트에 dwell 0.18s 문 추가로 "집었다 그 자리에서 놓기"=가장
  빠른 취소가 발견 가능해졌다(H2). 라벨에 무차감 명시(M2). rev2 의 H1(배너 위치 보정)은 rev3 에서
  배너 자체가 사라져 소멸했다.
- **rev3 — 덮는 UI 를 전부 삭제** — 유닛 트레이 배너 · 손패 힌트 배너 둘 다 제거. 예고는
  프리뷰 고스트 + 보드 침묵 + **문자 표면 하나**(유닛=포인터 라벨 / 드림캐쳐=상단 브리핑)로 끝난다.
  판정 rect 는 양쪽 다 그대로 — 지운 건 표면뿐이다.
- **unit 3 — 격자 밖 관용** — `Resolve` 가 `Vector2Int?`. 관용(1셀) 밖은 칸 없음 → **EndDrag 에
  원래 있던** "칸 없음 → 취소" 분기가 처음으로 도달 가능해졌다. 칸 없음도 고스트 + 포인터 라벨로
  예고한다(배너는 트레이 존 전용 — 표면 분담).
- **회귀 가드 2건** — `DragPlacementReachTest` 에 "최하단 행을 노리는 가상 포인터가 트레이 rect 밖"
  단언(BottomSafeRatio 근사 대신 실제 트레이 rect). 신규 `DragCancelZoneTest` 가 (1) 취소 존
  릴리즈 = 무차감 종료, (2) 손가락 트레이 안 + 조준점 트레이 밖 = 취소 아님 을 잰다.

## Key Files

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 취소 존 판정·예고·릴리즈
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

## Code review (2026-07-30)

독립 리뷰어 1회. **CRITICAL 0 · MAJOR 3 · MINOR 7 — 전부 반영.**

- **M1/M2 (같은 뿌리)** — dwell 게이트가 트레이 존에만 걸려 `_noCell` 이 판정 프레임에 즉시 예고를
  켰다. 가장자리 열을 좌우로 흔들면 관용 링을 넘나들며 고스트 알파·라벨이 껌뻑였고(**맵 무관**),
  `UpdatePlacementHighlightState` 가 `_noCell` 을 안 봐서 배치가능 하이라이트가 사유별로 갈렸다
  (계약 4·6 위반). → 술어 통합(`CancelStateNow` + `CancelArmed`)으로 동시 해소.
- **M3** — `DragCancelZoneTest` 의 무차감 단언이 공허할 수 있었다(취소 존 분기를 지워도 `_noCell`
  경로가 대신 취소 → 강도가 로드된 맵에 의존). 릴리즈 직전 `_noCell == false` 단언 추가.
- **MINOR** — 인스펙터 툴팁 '배너' 잔재, `_noCell` 소거 1회화, 오프보드 전이 `_noCell` 관리,
  비활성 트레이 취소 판정 제외(`activeInHierarchy`), `SoundManager` 풀네임, 스펙 문서 코드-모순 2곳,
  관용 **경계값** 테스트(frac −1.5/−1.51 · 10.49/10.5).

리뷰어가 검증하고 문제없다고 한 것: `Resolve` 호출처 단일·null 전파, 히스테리시스↔관용 경계 순서
정합성(밴드 최대 1.45셀 < tol 1 의 1.5셀 → 계약 8 이 margin 전 구간 성립), 계약 1/2/3/7/9,
세션 경계 플래그 리셋, 라벨 owner 경합 없음, ESC·배너 잔재 0, `CancelZone` raycast 미누출.

## Notes (되돌리면 안 되는 것)

- **취소 예고에 덮는 UI 를 다시 넣지 말 것**(rev3, 사용자 결정). 신호 둘이 이미 시선 위치에 있고,
  덮는 UI 는 코스트 물통·출발 슬롯처럼 취소 판단에 필요한 것을 지운다. 덮지 않는 보강(출발 슬롯
  코랄 링)은 후속 후보로 남겼다.
  참고: 배너를 되살리면 "판정은 가상 포인터인데 배너는 트레이 rect" 라는 65px 어긋남이 함께
  돌아온다 — rev2 H1 이 그걸 보정하려 했고 rev3 은 원인을 제거했다.
- **예고 게이트에서 dwell 문을 빼지 말 것.** 트레이 드래그는 취소 존 안에서 시작하므로, "존 이탈
  1회" 단독이면 가장 빠른 취소(집었다 그 자리에서 놓기)가 영구히 안 보인다.
- **관용은 셀 인덱스 초과분으로 센다.** frac 임계로 바꾸면 히스테리시스 밴드와 경계가 둘이 되어
  액체 하이라이트(`EvaluateStretch`)와 어긋난다 — 두 함수가 같은 밴드를 공유하는 게 계약이다.
- **`placementOutsideToleranceCells` 를 0 으로 내리기 전에 가장자리 배치 감각을 볼 것.** 히스테리시스가
  테두리 칸 관용을 담당하므로 0 도 동작하지만 여유가 약 22px 로 줄어든다.

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
- **예고 게이트는 릴리즈 판정에 걸지 않는다.** 존을 못 벗어난 짧은 드래그도 놓으면 취소가 맞다
  (그게 랜덤 하단 셀 오배치보다 낫다). 게이트는 **예고 전용**이다.
- **예고 술어를 두 사유로 다시 쪼개지 말 것.** `_cancelHover`(트레이 존)와 `_noCell`(칸 없음)은
  `CancelStateNow` 로 합쳐 하나의 게이트를 지난다. 쪼개면 리뷰 M1(오버슛 깜빡임)·M2(사유별로 다른
  보드 상태)가 그대로 돌아온다.
- **취소 수단은 드래그를 유지한 채 도달 가능해야 한다.** 키/시스템 버튼은 이 요건을 못 지켜
  철회됐다 — 새 취소 수단을 넣을 땐 이 요건부터 통과시킬 것.

## Follow-up

종료된 spec 이므로 후속 후보는 **중앙 backlog 로 이관**했다 →
`docs/spec/README.md` Follow-up Backlog · `#### 드래그 취소 (drag-cancel-affordance)` (7건).

남은 확인 사항 없음 — 완료 기준 19항목 전부 사용자 Play 확인 통과(2026-07-30).
