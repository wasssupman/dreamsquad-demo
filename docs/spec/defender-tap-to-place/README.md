# defender-tap-to-place

상태: **구현 완료·리뷰 반영 (Play 최종 확인 대기)** · 작성 2026-07-17 · 비행 연출 정제 추가 2026-07-18 (units 4·5)

## 목표

유닛을 **탭으로 선택(arm)** 하고 보드 **타일을 탭**하면, 즉시(텔레포트) 배치가 아니라
**기존 드래그 파이프라인을 스크립트로 구동한 D&D 시뮬레이션**을 재생해 배치한다:
트레이에서 유닛이 들어올려져 타일로 비행(키링 스윙) → hover/확정 팝 → deploy 시퀀스.

**검증 질문**: 유닛을 탭 선택하고 타일을 탭하면, 실제로 손가락으로 드래그한 것과 **시각적으로 구분되지 않는**
배치가 재생되고, 탭한 칸에 정확히 안착하는가?

## 핵심 계약 (feature-wide)

- **별도 배치 경로 금지 (load-bearing)**: 시뮬은 `BeginDrag(unit, fromScreen, simulated:true)` 로 기존 세션을 열고,
  **월드 공간에서 유닛 발점을 직접 트윈**한다. 커밋은 드롭과 같은 공용 꼬리(`CommitPlacementAt`)를 지난다.
  키링/hover/팝/슬로우모/컷신/deploy/비용/on-place 전부 재사용 — 로직 중복 0.
- **월드 공간 비행 (스크린 역산 금지)**: 목표를 화면좌표로 역산해 `UpdateDrag(스크린 트윈)` 하는 방식은 **폐기**
  — ① unit 5 스큐 보정이 조준을 밀고 ② 비행 중 카메라 dolly(SetDragFocus)로 화면 목표가 stale 이 되어 오배치.
  대신 `feet = Lerp(startFeet, endFeet)` 를 월드에서 구동하고 키링은 config 대로 따라온다(상세: `0_simulate_drag_driver.md`).
- **세션 소유권**: 시뮬 코루틴은 `_sessionGen`(CleanupSession 마다 증가)을 캡처해, 비행 중 새 드래그가 시작되면
  **커밋 없이 물러난다**(세션 하이재킹 방지). `_session.active` 만으로는 부족(새 세션도 true).
- **arm 상태는 컨트롤러 단독 소유**(단일 armed): `_armedSlot/_armedUnit/_armedFromScreen`.
  `GameManager.SelectedDefender` 는 건드리지 않는다(클릭 배치 레거시와 격리).
- **입력 가드 3종** (보드 탭): ① `GameManager.IsAiming` 이면 무시(스킬 조준 탭 이중 소비 방지 — PlacementInput 과 동일 이유)
  ② UI 위 탭 제외는 터치면 `primaryTouch.touchId` 로 판정(no-arg `IsPointerOverGameObject` 는 터치에서 무력)
  ③ 셀 변환은 `bridge.TryScreenToCell` **단일 소스** 재사용(수동 레이캐스트 복제 금지).
- **Unity fake-null 주의**: `_armedSlot` 은 트레이 리빌드로 파괴될 수 있다 — `?.` 금지, Unity `!=` 가드 +
  보드 탭 진입 시 슬롯이 죽었으면 자가 `Disarm()`.
- **탭 경로는 공격 범위 프리뷰 미노출**: `_simulatedDrag`(BeginDrag 의 simulated 인자로 **첫 UpdateDrag 전에** 세팅,
  CleanupSession 이 해제)가 `SetHover` 의 `SetPlacementRange` 만 스킵. hover/팝/키링은 유지. 실제 D&D 는 범위 노출.
- **비용 사전 피드백 대칭**: 비용 부족 유닛은 arm 자체를 거부 + `PulseInsufficient`(드래그 OnBeginDrag 와 동일).
  단 이미 armed 슬롯의 재탭(=해제)은 비용 무관 허용.
- **데이터 주도**: `tapTravelDuration`(기준 3s) / `tapTravelScaleMin·Max`(거리 비례 0.25~1.5) / `armHighlightColor` /
  `tapArcHeightFactor·tapArcLateralFactor`(비행 곡선) 전부 `DragSwaySettings` SO. 비행 시간 = 기준 × clamp(화면거리/화면세로, min, max).
- **탭 비행 포커스는 목표 고정 (unit 4)**: 비행 중 타일 포커스는 날아가는 발밑이 아니라 **탭한 목표셀**에 정적으로 붙는다.
  `ResolveFocusAndTarget(lockCell)` 로 히스테리시스/디바운스/액체 번짐을 우회. 스와이프는 발밑 실시간 추종 유지(분기: `_simulatedDrag`).
- **곡선 비행은 결정론 변주 (unit 5)**: 경로는 `KeyringSim.QuadraticBezier`(순수, 착지 오차 0). 좌우 변주는 RNG 가 아니라
  황금비 저불일치 수열(`_tapFlightSeq`) — 매 탭 다른 곡선이되 결정론적(프로젝트 index 기반 관례). 이징(OutCubic)은 속도 프로파일로 분리.

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_simulate_drag_driver.md` | feature | `SimulateDragTo` 월드 트윈 드라이버 + 세션 세대 토큰 + 공용 커밋 |
| 1 | `1_unit_arm_select.md` | feature | 슬롯 탭=arm 토글 + 비용 게이트 + 하이라이트(SO 색) |
| 2 | `2_board_tap_place.md` | feature | 보드 탭 → 가드 3종 → 시뮬 발화 / 무효 reject |
| 3 | `3_handoff_summary.md` | doc | 인계 요약 (Play 확인 후 작성) |
| 4 | `4_flight_focus_pin.md` | feature | 탭 비행 중 타일 포커스를 발밑 추종 대신 선택 타일에 정적 고정 |
| 5 | `5_bezier_flight_path.md` | feature | 직선 비행 → 2차 베지어(매 탭 다른 곡선 아치) |

의존: `0 → 1 → 2`. `4·5` 는 비행 연출 정제(독립적, 순서 무관). 선행: `docs/spec/placement-cell-snap/`(드래그 파이프라인 — 히스테리시스/throttle/팝, 완료).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트/렌더 경로 없음. 기존 드래그 배치 파이프라인을 새 입력(탭)으로 구동만 한다.

## 리뷰 반영 기록 (2026-07-17, critic 10건 전부 수정)

세션 하이재킹(세대 토큰)/EndDrag stale 릴리즈(placement-cell-snap 3 참조)/IsAiming 가드/fake-null/터치 UI 판정/
TryScreenToCell 재사용/커밋 꼬리 통일/dead 스큐 게이트 제거/하드코딩 SO 이동/비용 arm 게이트.
**구조 평결**: 셀 정책·타일 피드백·보드 기하·키링 물리는 오브젝트-불가지, defender 결합은 오케스트레이터·bridge 에
의도적 집중(concrete-first). 둘째 배치 오브젝트 등장 시 추출 지점 = Spine 전제 프리뷰 빌더(`TryBuildKeyringPreview`).

## 후속 후보

- 배치 후 arm 유지(연속 배치) — 현재는 배치 시 해제.
- 탭 선택 시 유효 타일 프리하이라이트(`placement-attack-range-preview` 재사용).
- 비행 중 탭으로 스킵/가속(현재 비행 중 입력 잠김 — 슬로우모 유지도 드래그와 동일 계약).
- 시뮬 중 카메라 포커스 피드(`WorldToScreenPoint(ring)`) — 수렴 서보지만 드리프트 체감 시 재론.

## 비목표

- 즉시(텔레포트) 배치 · 새 deploy/비용 로직 · 장애물 회피 비행 · placeable 인터페이스 추상화(구현체 1개).
