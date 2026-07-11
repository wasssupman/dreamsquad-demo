# 3 — Play e2e 검증

## 목적

README 검증 질문을 실플레이로 답한다. 코드 변경 없음(발견된 결함은 해당 unit 에 rev 로 귀속).

## 변경 대상

없음 (검증 전용). 필요 시 UnityMCP `execute_code` 로 보스 스폰/방어유닛 배치 조작.

## 시나리오

1. **되돌아가 재교전**: 보스가 방어유닛을 지나친(또는 방어유닛 전멸 후 전진 중인) 시점에 보스 **뒤** 셀 옆에 방어유닛 배치 → 보스가 역방향으로 걸어 사거리 진입 → 정지·공격(Engaging/Halt).
2. **연쇄 사냥**: 방어유닛 2개(앞/뒤 분산) → 최근접부터 교전, 사망 시 다음 최근접으로 필드가 자동 유도.
3. **leak-proof**: 방어유닛이 살아있는 동안 보스가 goal 셀 근처를 지나도 누수(PastGoalTag) 없음.
4. **0마리 goal**: 방어유닛 전멸 → 보스 goal 마칭 재개, goal 도달 시 정상 누수 처리.
5. **무회귀**: 일반 적 march/aggro/Engaging 종전과 동일. 가디언 aggro 가 보스에 걸리면 aggro Chasing 이 사냥에 우선(기존 동작).
6. **재시작 위생**: 판 재시작/redraft 반복 — Persistent leak/console 에러 없음.

## 완료 기준

- 시나리오 1~6 전부 통과 (스크린샷 또는 로그 근거).
- console 클린 (에러/leak 경고 0).
- 통과 시 README 상태 라인 완료 처리 + handoff summary 작성, backlog 항목은 spec 링크로 대체 확인.
