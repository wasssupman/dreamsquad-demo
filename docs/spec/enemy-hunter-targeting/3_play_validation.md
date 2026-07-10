# 3 — Play e2e + 렌즈 B

## 목적

헌터 보스를 실전투에서 검증하고 배선을 렌즈 B 로 리뷰한다. 신규 에셋 없음 — 기존 `Enemy_Boss_Nightmare` + `WavePlan_BossTest` 재사용.

## Play e2e (검증 질문 분해)

1. **추격**: 보스 스폰 후, 사거리 밖 방어유닛이 있으면 goal 로 안 가고 **최근접 방어유닛을 향해 이동**.
2. **교전 전환**: 사거리(2타일) 진입 시 멈춰 공격(Engaging). 추격↔공격이 데드락 없이 상태 전환.
3. **연쇄**: 공격 대상이 죽으면 다음 최근접으로 이어 추격.
4. **0마리 = goal**: 방어유닛 전멸 시에만 goal 로 이동(누수는 이때만).
5. **무회귀**: 일반 적(비-보스)은 기존 march/aggro 그대로. 보스 aggro 시 guardian chase 유지.
6. **nightmare 직교**: 추격 중에도 융단폭격·텔레포트 정상(추격이 timer/HP 슬롯 안 건드림).

## 렌즈 B (ECS 도메인)

- 맥락 경계: `HuntTarget` write=Combat(FSM), read=Movement. 위치 쓰기=Movement.
- Burst: BossTag/HuntTarget lookup, 최근접 순수함수.
- 무회귀: 비-보스 Evaluate 경로 byte-identical, 기존 aggro Chasing 무변경.
- teardown: HuntTarget = AttackUnitTag 상속(신규 0).

## 완료 기준

- [ ] 위 Play e2e 6개 에디터 확인.
- [ ] 렌즈 B 통과.
- [ ] README 상태 완료 + handoff(필요 시).
