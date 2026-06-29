# 5 — PlayMode smoke 갱신 + Play 검증

## 목적

FSM 전환 후 판 흐름 수준의 회귀를 PlayMode 로 고정하고, 상태 전이가 실제 전투에서 의도대로 동작하는지 라이브 확인한다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/MovementIntegritySmokeTest.cs` — 상태 기반 경로로 갱신(레거시 pause 가정 제거).

## 구현 / 검증

PlayMode smoke 가 커버할 것:
- 어그로 없는 적이 행진 → 디펜더 사거리 진입 → `Engaging`(Halt 적 정지) → 공격 → 디펜더 사망 → `Marching` 복귀.
- 어그로 적이 `Chasing` → 가디언 도달 → `Standoff` 정지 공격.
- 모든 유닛 walk 타일 위 유지(기존 무결성 회귀).

라이브 Play 검증(에디터 **포커스** 필요 — 비포커스면 시뮬 tick 안 함):
- Vanguard(Halt): 정지+공격, 디펜더 처치 후 행진.
- Advance 적: 이동하며 공격.
- aggro 상호작용: 가디언 추격 → standoff → 가디언 사망 시 행진 복귀.
- 콘솔 에러/leak 경고 0.

## 완료 기준

- `MovementIntegritySmokeTest` PASS.
- EditMode 전체 PASS.
- 위 라이브 시나리오 육안 확인.
- README 상태 라인을 "완료 YYYY-MM-DD" 로 갱신하고 `6_handoff_summary.md` 작성.
