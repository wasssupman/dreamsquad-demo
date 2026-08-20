# placement-armed-board-drag

상태: **완료 2026-07-20** (units 0~2 사용자 Play 확인 · `bc30446d`/`e88fb071`/`5b1c575f`)
· **unit 4 추가 2026-08-20** — 무효 릴리즈 = 선택 해제 (`fb40895a` · 사용자 Play 확인 · 가장자리 관용 항목만 미확인)

## 목표

트레이 슬롯 탭으로 **arm(선택)** 한 유닛으로 보드를 조작하는 방식을, "탭=즉시 시뮬 비행 배치" 에서
**보드 프레스-드래그로 공격범위를 스카우트하고 릴리즈로 배치** 하는 모델로 바꾼다.

- armed 유닛으로 보드 **프레스다운** → 그 칸의 **공격범위 노출** 시작(arm 유지).
- 이동 임계(드래그 threshold) 초과 → **드래그** 승격 → 손가락 셀을 따라 공격범위 실시간 추종(arm 유지).
- **드래그 릴리즈**(유효셀) → 그 칸에 배치(기존 시뮬 비행 재사용).
- **탭**(이동 없이 다운→업) → **기존 클릭 배치와 동일하게 그 칸에 즉시 배치** + 공격범위를 **비행 중에만**
  노출(배치=착지 시 소거, linger 없음 — 다른 배치 동작과 동일).
- **무효셀/맵 밖 릴리즈** → 거부 표시 후 **arm 해제**(unit 4. 원래는 arm 유지였다 — 아래 계약 참조).

즉 **탭·드래그 모두 배치**한다(탭=빠른 즉시 배치+범위 flourish, 드래그=범위 스카우트 후 릴리즈 배치).
차이는 배치 여부가 아니라 **상호작용 느낌과 범위 표시 방식**이다.

## 검증 질문

arm 한 유닛으로 보드를 눌러 드래그하면 손가락을 따라 공격범위가 실시간으로 보이고, 손을 떼면 그 칸에
배치되는가? 짧게 탭하면 기존 클릭처럼 그 칸에 즉시 배치되면서 공격범위가 잠깐 노출되는가? — 탭/드래그
구분이 **누른 시간이 아니라 움직인 거리**로 판정되는가?

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_board_gesture.md` | feature | 보드 포인터 제스처 상태기계(press/move/release) + 이동량 기반 탭/드래그 판정 + 가드 3종 + DcInspect 양보 seam 확장 + **드래그-릴리즈 커밋**(`SimulateDragTo` 재사용). 기존 `HandleArmedBoardTap` 대체. (탭=no-op, 스카우트 비주얼 없음 — 각각 unit 2·1) |
| 1 | `1_range_scout.md` | feature | 프레스·드래그 중 범위-only 프리뷰가 손가락 셀 추종(`SetPlacementRange`+hover, 키링 없음). 릴리즈/오프보드 시 소거 |
| 2 | `2_tap_peek.md` | feature | 무이동 릴리즈=탭 → 기존 클릭 배치와 동일하게 즉시 비행 배치 + 공격범위를 비행 중에만 노출(배치 시 소거, linger 없음). 무효셀=reject+범위 즉시 소거, arm 유지 |
| 3 | `3_handoff_summary.md` | doc | 인계 요약 (Play 확인 후 작성) |
| 4 | `4_invalid_release_disarm.md` | fix | 못 놓는 자리 릴리즈 = 선택 해제(공용 릴리즈 꼬리) + 셀 판정을 D&D 와 같은 격자 밖 관용으로 통일 |

의존: `0 → {1, 2}`. 커밋을 unit 0 에 포함해 **매 커밋마다 보드 배치가 동작**하고 이후 unit 이 스카우트 비주얼(1)·탭 피크(2)를 얹는다. 선행(완료): `defender-tap-to-place`(arm·`SimulateDragTo` 비행), `placement-attack-range-preview`(`SetPlacementRange`), `placement-cell-snap`(셀 판정·팝).

## Feature-wide 계약 (load-bearing)

- **진입 = armed 유닛 단독**: 보드 제스처는 `_armedUnit != null` 일 때만. arm 소유는 컨트롤러 단독
  (`_armedSlot/_armedUnit`, `GameManager.SelectedDefender` 격리) — 기존 tap-to-place 계약 유지.
- **탭/드래그 구분 = 이동량**: 프레스 후 임계 픽셀 이상 이동하면 드래그, 아니면 탭.
  **다운→업 시간 delta 로 판정하지 않는다**(사용자 결정 2026-07-20). 임계값은 SO(하드코딩 금지).
- **탭·드래그 모두 배치**: 이동 있는 드래그 릴리즈 → 유효셀 배치. 이동 없는 탭 → 기존 클릭과 동일하게
  즉시 배치 + 착지 셀에 범위 flourish(**비행 중에만** 유지, 배치=착지 시 소거 — linger 없음, 다른 배치 동작과 동일).
  성공 배치는 arm 해제(연속 배치는 후속).
- **릴리즈는 언제나 제스처를 끝낸다 (unit 4, load-bearing)**: 유효셀=배치 · 무효셀=`FlashPlacementReject`+`Disarm` ·
  맵 밖=취소음+`Disarm`. 세 갈래 모두 `ResolveArmedRelease` 한 꼬리를 지나 탭·드래그가 갈릴 여지가 없다.
  **~~무효셀은 arm 유지(재시도)~~ 는 폐기됐다** — 재시도 배려였지만 빠져나올 길이 슬롯 재탭뿐이라
  배치를 그만두려는 사용자가 선택·타일 하이라이트를 켠 채 갇혔다(트레이 D&D `EndDrag` 만 유일하게
  릴리즈에서 정리하고 있었다 = 같은 배치인데 입력 방식마다 종료 규칙이 달랐다).
- **armed 경로의 셀 판정은 D&D 와 같은 관용을 쓴다 (unit 4)**: 릴리즈와 스카우트가 **함께**
  `TryResolveArmedCell` → `PlacementCellSnap.Resolve(..., placementOutsideToleranceCells)` 를 지난다.
  양 극단이 모두 틀린 자리다 — 관대한 `TryScreenToCell` 은 맵 밖을 가장자리 셀로 clamp 해 true 를 줘
  "배치 불가한 곳에 놓으면 취소" 를 무너뜨리고(재배치 unit 9 가 기록한 함정), 관용 0 의 하드 판정은
  **최상단 행 오버슛을 전부 취소로 돌린다**(판정 포인터가 손가락보다 65px@1080 위라 위쪽 행을 노리면
  격자를 넘기 쉽고, D&D 는 1칸까지 용서한다 — `DragPlacementReachTest` 가 지키는 도달성 계약).
  **관용 규칙을 복제하지 말 것**: 정책은 `PlacementCellSnap.Resolve` 단독 소유이고 브리지의
  `TryScreenToBoardFrac` 은 좌표만 넘긴다(clamp·bounds 판정 없음). 히스테리시스는 0 —
  armed 스카우트는 매 프레임 생 판정이 unit 1 계약이라 관용만 공유한다.
  스카우트와 릴리즈를 갈라놓지 말 것 — 스카우트가 관대하면 배경 위에서 빛나는 칸을 릴리즈가
  취소로 버려 하이라이트가 거짓말한다. press 게이트는 반대로 **셀을 요구하지 않는다**:
  배경 프레스가 제스처를 못 열면 그 릴리즈가 상태기계에 도달하지 못해 해제가 안 일어난다.
- **탭 범위 flourish 는 비행과 안 싸우게 별도 소유**: 유효셀 탭의 range 표시는 `_tapPlaceRangeRoutine` 이
  비행 세션이 사는 동안(`_session.active && _simulatedDrag`) 매 프레임 `SetPlacementRange` 로 재확인해
  비행(sim 경로)의 `CleanupSession` clear 를 덮어쓴다. 배치되면 세션 종료 → 소거. 자기 flight 의
  `Disarm`/`ResetBoardGesture` 에는 죽지 않고, **새 press·트레이 드래그**(`BeginDrag(!simulated)`)에서만 취소.
- **보드 드래그는 범위-only 스카우트**(키링 유닛 없음): 유닛은 트레이에 남고, 손가락은 범위/hover만 끈다.
  **커밋은 기존 시뮬 비행**(`SimulateDragTo`, tray→cell) 재사용 → tap-to-place 비행 자산 보존, 로직 중복 0.
  (스카우트 중 키링 유닛 프리뷰는 후속 후보.)
- **재사용 seam**: 범위 표시=`bridge.SetPlacementRange/ClearPlacementRange`(단일 게이트웨이) ·
  셀 판정=`bridge.TryScreenToCell`(단일 소스) · 검증=`bridge.CanPlaceDefenderAt`(단일 권한) ·
  커밋 꼬리=`SimulateDragTo → CommitPlacementAt → TryBeginDefenderDeployment`(directional/일반 분기 불변).
- **보드 press 소유권**: armed 보드 제스처 동안 `DcInspectController` 가 양보해야 한다 —
  기존 `drag.IsDragging` 양보 seam 을 armed 제스처까지 확장(**두 소비자가 같은 press 를 노리는 race
  재생산 금지**, DcInspect 계약 11). 세컨드 탭 핸들러를 새로 만들지 않는다.
- **입력 가드 3종 유지**: `GameManager.IsAiming` 무시 · `PointerOverUi`(터치는 touchId 판정) ·
  `TryScreenToCell` 단일 소스. Unity fake-null 가드(`_armedSlot` 트레이 리빌드 파괴) 유지.
- **트레이 D&D 불변**: 트레이 슬롯에서 끌어 보드로 놓는 실드래그(`DefenderDragSlot` D&D)는 그대로.
  이 spec 은 **armed→보드** 진입만 바꾼다.
- **슬로우모 대칭**: 커밋 비행 중에만 Battle 슬로우모(기존 `SimulateDragTo`→`BeginDrag` 경로).
  범위 스카우트/탭 flourish 는 슬로우모 없이 unscaled 실시간(Interaction 도메인).
- **데이터 주도**: 드래그 threshold(`boardDragThreshold`)는 `DragSwaySettings` SO. 탭 범위는 비행 수명에
  묶여(별도 표시시간 상수 없음) — 비행 시간 튜닝(그룹 ⑦)이 노출 길이를 결정한다.

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트/렌더 경로 없음. 기존 배치 파이프라인(범위 하이라이트 · 시뮬 비행 ·
공용 deploy 꼬리)을 **새 보드 입력(armed 프레스-드래그-릴리즈)** 으로 구동만 한다.

## 후속 후보

- **재배치(이동모드)의 무효 릴리즈**: `DefenderRelocationController.ResolveRelease` 는 아직 무효셀에서
  reject + 이동모드 유지다(빠져나오는 길은 맵 밖 릴리즈와 8초 타임아웃). 배치와 규칙을 맞출지는
  "취소=원위치 복귀" 라는 다른 정의 때문에 별도 판단이 필요하다. 셀 판정도 `TryScreenToCellStrict`
  (관용 0)라 위쪽 가장자리 오버슛이 즉시 취소다 — 배치와 같은 관용을 줄지 함께 본다.
- **unit 4 회귀 가드**: "릴리즈는 언제나 제스처를 끝낸다" 를 잡는 자동 테스트가 없다. bridge·카메라·
  생성된 맵이 필요해 EditMode 로는 못 내려오고 PlayMode 는 어셈블리 단위 실행뿐(8분)이라 미뤘다.
  `RelocationMoveModeTest` 의 리플렉션 arm 기법(`_armedUnit`/`_armedSlot` 동시 세팅)이면 짧게 쓸 수 있다.
- **연속 배치**: 커밋 후 arm 유지(현재는 커밋 시 해제). tap-to-place 후속 후보와 동일.
- **스카우트 중 키링 유닛 프리뷰**: 현재 range-only. 실드래그처럼 유닛이 손가락에 매달려 따라오게.
- **스카우트 중 유효 타일 프리하이라이트**: `placement-eligible-tile-highlight` 재사용.

## 비목표

- 즉시(텔레포트) 배치 · 새 deploy/검증/비용 로직 · 연속 배치 · placeable 인터페이스 추상화(구현체 1개).
- 트레이 D&D 흐름 변경 · 드래그 threshold 를 시간으로 판정 · 스카우트 중 키링 유닛 렌더.
