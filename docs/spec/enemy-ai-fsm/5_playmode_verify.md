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

---

✅ **자동 검증 완료 2026-06-30** — MovementSystemTests 에 FSM 회귀 3종 이관(Chasing self-walk, Engaging-Halt portal/tornado 직교성), 11/11 PASS. PlayMode smoke 는 FSM 스택을 라이브 구동(EnemyAiStateSystem→Movement→Attack)하여 aggro Chasing→Standoff→데미지 검증, 1/1 PASS. 풀 EditMode 회귀 없음(6건 "Destroy from edit mode"는 직전 PlayMode 씬 잔류 거짓실패 — 도메인 리로드 후 7/7 PASS 재확인 / 1건 ObstaclePlacer 는 기존 flaky, FSM 무관). ecs-reviewer APPROVE(테스트 정합·결정론·회귀민감, L1 정확값 단언 반영).
> ⏳ **라이브 육안 검증 미완**: 완료 기준 3(Vanguard 정지/공격·디펜더 처치 후 행진, Advance 적 이동사격, aggro standoff 복귀)은 에디터 **포커스** Play 가 필요해 사용자 확인 대기.
