# 3. tornado pull — 경로 오버라이드 폐지, 후처리 가산 변위 + trim

## 목적

계약 7 (사용자 결정): 토네이도는 중심으로 끌되 **cell-trim 에 걸리면 벽처럼 막히고**, 적의 경로 이동(flow 따라가기)을 대체하지 않는다 — 이동 스텝 말미의 **가산 변위**로 적용한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — tornado 블록 재배치
- `Assets/_Project/Tests/EditMode/MovementSystemTests.cs` — tornado 테스트 서브틱화

## 구현

- 기존: pull 이 이동을 통째로 대체(`pulled → continue`, trim 미적용 — 벽 무시로 비-walk 셀 진입 가능, cell-trim 리뷰 C4).
- 신규: pull 변위만 계산해 두고 —
  - flow 경로: `desired += pullDisplacement` (flow+impulse+recenter 와 합성) → ClampDisplacement → trim.
  - Engaging halt/locked·고립(zero-recovery) 정지 경로: pull 만 단독 적용(동일 클램프+trim) 후 continue — 오늘 "정지 중에도 당겨짐" 거동 보존.
  - Standoff/Chasing 은 기존처럼 pull 대상 아님(분기 이전 continue — 스코프 최소화).
- 거동 델타(개선): ①pull 이 벽/장애물에서 막힘(trim) ②locked/Engaging 이동 프레임에 pull 과 impulse 가 대체가 아니라 **합성**됨 ③프레임 변위 상한 공유.

## 완료 기준

- compile 0 · EditMode 전체 green (tornado 테스트는 서브틱으로 갱신, 총 변위 기대값 불변).
- Play 검증은 unit 4 스모크에 위임.
