# 2 — 보스 authoring + Play e2e

## 목적

`Enemy_Boss_Nightmare` 에 채찍질 mechanic 을 데이터로 추가하고, 미니언 동행 웨이브에서 검증 질문 전체를 Play 로 답한다.

## 변경 대상

- `Assets/_Project/Data/.../Enemy_Boss_Nightmare.asset` — `nightmareMechanics` 3번째 항목 append
- `Assets/.../WavePlan_BossTest.asset` — 미니언 동행 편성(오라 대상 확보)

## 구현

### authoring

- mechanic append: `trigger = { kind: PeriodicTimer, periodSeconds: 1 }`, `payload = { kind: AllyMoveSpeedAura, magnitude: 20, tileRange: 3, duration: 1.5 }`.
  - duration(1.5) > periodSeconds(1) — README 계약 5 의 authoring 계약 준수.
  - 튜닝 후보값이며 전부 SO — 코드 무변경으로 조정 가능.
- `WavePlan_BossTest` 가 현재 보스 단독(t=6 보스 1)이면 일반 미니언 수 기를 근접 타이밍에 추가 — 보스와 같은 레인에서 앞뒤로 겹치게.

### Play e2e (검증 질문 분해)

1. 보스 3타일 내 미니언 `moveSpeedMul == 1.2` / 범위 밖 미니언 `1.0` — +20% 는 육안 애매하므로 MCP execute_code 로 `ModifierStats` reflection 조회(라이브 측정은 에디터 포커스 필요 — lessons).
2. 미니언이 범위를 벗어나거나 **보스 사망** 시 ≤1.5초 내 `1.0` 원복(자연 만료 — revoke 없음).
3. **직교**: 융단폭격(10s)·텔레포트(30% 경계)·기본공격이 채찍질과 동시 정상 — 서로 timer/AttackState 무간섭.
4. **합성**: 슬로우 존(또는 CC 슬로우) 위의 whip 미니언 = Πmul 곱(예: 0.5×1.2=0.6) — 클램프 [0.15, 3.0] 내.
5. **무회귀**: 방어유닛 드림캐쳐 카드/Active Meteor 정상, 일반 적(whip 슬롯 없음) 이속 불변.
6. 손패 열림(슬로모 0.3x) 중 펄스·TTL 이 함께 감속된 채 유지 비율 정상(README 계약 9).

## 완료 기준

- [ ] 위 Play e2e 6개 에디터 확인(사용자 확인 요청 필수 — 확인 방법: 보스 웨이브 진입 후 execute_code 스니펫 제공).
- [ ] EditMode 전체 그린 + 컴파일 클린.
- [ ] 종료 시: README 상태 라인 갱신 + `3_handoff_summary.md` + nightmare-catcher README 후속 후보에서 채찍질 항목 이관 표기(같은 커밋).
