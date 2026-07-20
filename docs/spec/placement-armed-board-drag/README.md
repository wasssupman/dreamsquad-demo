# placement-armed-board-drag

상태: **초안 2026-07-20** (README 승인 대기 · 미착수)

## 목표

트레이 슬롯 탭으로 **arm(선택)** 한 유닛으로 보드를 조작하는 방식을, "탭=즉시 시뮬 비행 배치" 에서
**보드 프레스-드래그로 공격범위를 스카우트하고 릴리즈로 배치** 하는 모델로 바꾼다.

- armed 유닛으로 보드 **프레스다운** → 그 칸의 **공격범위 노출** 시작(arm 유지).
- 이동 임계(드래그 threshold) 초과 → **드래그** 승격 → 손가락 셀을 따라 공격범위 실시간 추종(arm 유지).
- **릴리즈**(이동 있었고 유효셀) → 그 칸에 배치(기존 시뮬 비행 재사용).
- **탭**(이동 없이 다운→업) → 공격범위가 짧게 반짝였다 사라짐(**배치 안 함**, arm 유지 → 재시도).

## 검증 질문

arm 한 유닛으로 보드를 눌러 드래그하면 손가락을 따라 공격범위가 실시간으로 보이고, 손을 떼면 그 칸에
배치되는가? 그냥 짧게 탭하면 범위만 잠깐 보였다 사라지고 유닛은 배치되지 않는가? — 탭/드래그 구분이
**누른 시간이 아니라 움직인 거리**로 판정되는가?

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_board_gesture.md` | feature | 보드 포인터 제스처 상태기계(press/move/release) + 이동량 기반 탭/드래그 판정 + 가드 3종 + DcInspect 양보 seam 확장 + **드래그-릴리즈 커밋**(`SimulateDragTo` 재사용). 기존 `HandleArmedBoardTap` 대체. (탭=no-op, 스카우트 비주얼 없음 — 각각 unit 2·1) |
| 1 | `1_range_scout.md` | feature | 프레스·드래그 중 범위-only 프리뷰가 손가락 셀 추종(`SetPlacementRange`+hover, 키링 없음). 릴리즈/오프보드 시 소거 |
| 2 | `2_tap_peek.md` | feature | 무이동 릴리즈=탭 → 공격범위 짧게 표시 후 페이드, arm 유지(배치 X) |
| 3 | `3_handoff_summary.md` | doc | 인계 요약 (Play 확인 후 작성) |

의존: `0 → {1, 2}`. 커밋을 unit 0 에 포함해 **매 커밋마다 보드 배치가 동작**하고 이후 unit 이 스카우트 비주얼(1)·탭 피크(2)를 얹는다. 선행(완료): `defender-tap-to-place`(arm·`SimulateDragTo` 비행), `placement-attack-range-preview`(`SetPlacementRange`), `placement-cell-snap`(셀 판정·팝).

## Feature-wide 계약 (load-bearing)

- **진입 = armed 유닛 단독**: 보드 제스처는 `_armedUnit != null` 일 때만. arm 소유는 컨트롤러 단독
  (`_armedSlot/_armedUnit`, `GameManager.SelectedDefender` 격리) — 기존 tap-to-place 계약 유지.
- **탭/드래그 구분 = 이동량**: 프레스 후 임계 픽셀 이상 이동하면 드래그, 아니면 탭.
  **다운→업 시간 delta 로 판정하지 않는다**(사용자 결정 2026-07-20). 임계값은 SO(하드코딩 금지).
- **릴리즈=배치 / 탭=피크**: 이동 있었고 유효셀에서 릴리즈 → 배치. 이동 없는 탭 → 범위 피크만(배치 X).
  무효셀 릴리즈 → `FlashPlacementReject` + arm 유지(재시도).
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
  범위 스카우트/탭 피크는 슬로우모 없이 unscaled 실시간(Interaction 도메인).
- **데이터 주도**: 드래그 threshold, 탭 피크 표시시간·페이드는 `DragSwaySettings` SO.

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트/렌더 경로 없음. 기존 배치 파이프라인(범위 하이라이트 · 시뮬 비행 ·
공용 deploy 꼬리)을 **새 보드 입력(armed 프레스-드래그-릴리즈)** 으로 구동만 한다.

## 후속 후보

- **연속 배치**: 커밋 후 arm 유지(현재는 커밋 시 해제). tap-to-place 후속 후보와 동일.
- **스카우트 중 키링 유닛 프리뷰**: 현재 range-only. 실드래그처럼 유닛이 손가락에 매달려 따라오게.
- **스카우트 중 유효 타일 프리하이라이트**: `placement-eligible-tile-highlight` 재사용.

## 비목표

- 즉시(텔레포트) 배치 · 새 deploy/검증/비용 로직 · 연속 배치 · placeable 인터페이스 추상화(구현체 1개).
- 트레이 D&D 흐름 변경 · 드래그 threshold 를 시간으로 판정 · 스카우트 중 키링 유닛 렌더.
