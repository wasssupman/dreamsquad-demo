# 2 — Battle 페이즈 스트립 슬림 모드

## 목적

중앙 통일의 유일 실비용인 "상시 footprint 의 보드 하단 가림"을 상쇄한다. Placement(여유, 배치가 주 활동)에는 풀 사이즈, Battle(실시간, 관전이 주 활동)에는 슬림 축소. 선행: unit 0, 1.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `GameManager.PhaseChanged` 구독 + 패널 크기 전환

## 구현

- `OnEnable/OnDisable` 에서 `GameManager.Instance.PhaseChanged` 구독/해제 (기존 `PlacementRequested` 구독 패턴 준수)
- Placement: `sizeDelta = (912, 120)` (풀). Battle: 높이 축소 — 시작값 `(912, 88)` (~73%), 시각 확인 후 확정치를 이 문서에 기록
- 슬롯은 `childForceExpand` 라 패널 크기만 바꾸면 균등 축소 — 슬롯별 코드 변경 없음
- 전환은 즉시 적용(스냅)으로 시작. 트윈 애니메이션은 시각적으로 필요하다고 판단될 때만 추가 (과잉 구현 금지)
- 하드코딩 수치 주의: 풀/슬림 크기는 클래스 상단 const 또는 SerializeField 로 노출 (제약 6)
- 드래그 히트 영역이 슬림에서 너무 작아지면 안 됨 — 슬롯 최소 높이 80px 선을 완료 기준에서 확인
- 코스트 배지(unit 1)는 스트립 상단 기준 위치이므로 슬림 시 함께 y 하향할지 고정할지 시각 판단 — 결정을 기록

## 구현 기록 (rev 2026-07-11)

- 슬림 확정치 (912, 88) 유지 (시작값 그대로 통과)
- 코스트 배지는 슬림 시 y 고정(미추종) — 갭만 벌어지고 시각 무해 (unit 1 문서 참조)

## 완료 기준

- [x] 컴파일 클린
- [x] Play: Placement ↔ Battle 전환 시 스트립 크기가 풀 ↔ 슬림으로 전환
- [x] Play: 슬림 상태에서 드래그 배치 정상 시작 가능 (오터치·미스그랩 없음)
- [x] Play: 슬림 ↔ 핸드 플립 왕복 시 크기 상태가 페이즈에 맞게 복원 (플립 후 잔존 크기 버그 없음)
- [x] Battle 중 보드 하단 가시성이 체감 개선 (전/후 스크린샷 비교)

사용자 확인 2026-07-11.
