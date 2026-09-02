# 19 — 해저드 존 틱 연속화

> unit 18 존치분. 「전투는 모두 거리 기반」의 마지막 조각 — **이로써 전투 판정의 격자는 0 이다.**

## 목적

장판(화염·잿불 존 등)의 틱 멤버십이 **베이크된 셀 집합 × 피해자 셀 양자화**
(`HazardShapeSampler` → `HazardSingleton.cellToEffects` 해시 → `ZoneApplySystem` 셀 룩업)다.
피해자 몸 위치 기준 연속 판정으로 바꾼다.

## 구현 (2026-09-01 — 초안의 브로드페이즈 안을 폐기하고 더 단순하게)

셀 해시 브로드페이즈를 유지하려던 초안은 **중복 적용 함정**이 있었다(피해자가 9칸을 탐침하면
같은 존의 효과를 여러 칸에서 만난다 — dedupe 키가 없다). 규모를 재보니(존 ≤ 수십 × 피해자
≤ 수십) 브로드페이즈 자체가 불필요했다:

- `Hazard` 에 **원 정의**(`originCell` + `radiusTiles`) 동봉 — 모양→반경 매핑은 셀 샘플러와
  같은 규칙(SingleCell→0 · Square3x3→1 · RadiusSquare→max(1,r)).
- `ZoneApplySystem` = 해저드 스냅샷 × 피해자 몸 **직접 곱** — 멤버십
  `InBodyReach(반경, CellHalfWidth, 피해자 몸)`, 광역·회오리와 같은 자.
- **은퇴**: `HazardSingleton`·`cellToEffects` 맵·`HazardLifetimeSystem` 의 매 프레임 재구축
  (수명 틱만 남음)·브리지의 맵 할당/해제. `HazardCellsBuffer` 는 검사(테스트) 소비자가 있어 존치 — 뷰 소비자는 0(리뷰 M4 정정).
- 겹친 동일 슬롯 존의 적용 순서는 종전(맵 삽입 = 청크 순서)과 같은 결 — 결정론 등급 무변.

## 완료 기준

- [x] 장판 경계에 몸이 걸친 적이 틱을 받는다 — 판정식 자체가 몸 걸침(공유 술어 테스트가 고정).
- [x] `ZoneApplyFactionGateTests` 6건이 새 파이프라인(해저드 엔티티)에서 초록 — 진영·층 게이트 무회귀.
- [x] EditMode 2669건 전건 초록(선행 2건 제외) · **골든 8건 바이트 무변**.
