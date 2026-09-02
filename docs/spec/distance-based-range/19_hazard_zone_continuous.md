# 19 — 해저드 존 틱 연속화 (초안)

> unit 18 존치분. 「전투는 모두 거리 기반」의 마지막 조각.

## 목적

장판(화염·잿불 존 등)의 틱 멤버십이 **베이크된 셀 집합 × 피해자 셀 양자화**
(`HazardShapeSampler` → `HazardSingleton.cellToEffects` 해시 → `ZoneApplySystem` 셀 룩업)다.
피해자 몸 위치 기준 연속 판정으로 바꾼다.

## 구현 방향 (브로드페이즈 유지)

- 셀 해시는 **브로드페이즈로 존치**(성능 모델 보존) — 베이크 셀을 +1링 확장해 몸 걸침 후보를
  놓치지 않게 한다.
- `ZoneApplySystem` 에 내로우페이즈 추가: `InBodyReach(피해자 위치 − 존 중심, 존 반경,
  CellHalfWidth, 피해자 몸)` — effect 항목에 존 중심·반경 동봉 필요(스키마 확장).
- `HazardShape.SingleCell/Square3x3/RadiusSquare` 저작 의미는 유지(반경 저작), 판정만 연속.

## 완료 기준 (초안)

- [ ] 장판 경계에 몸이 걸친 적이 틱을 받는다(EditMode 재현).
- [ ] 브로드페이즈 확장으로 누락 0 — 경계 fuzz 테스트.
- [ ] 골든 재베이크 + 귀속.
