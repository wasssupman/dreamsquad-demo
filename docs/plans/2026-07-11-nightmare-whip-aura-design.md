# Nightmare Whip Aura (채찍질) — 설계 배경 (얇은 브레인스토밍)

> 실제 구현 스펙은 `docs/spec/nightmare-whip-aura/`. 이 문서는 **왜 그렇게 결정했는가**(대안 기각 이유)만 담는다. 계약이 바뀌면 spec 이 우선.

## 목표 한 줄

보스 스킬 "채찍질" — 자신 기준 3타일 이내 **아군(같은 진영) 유닛들**의 이동속도 +20% 오라. 첫 지시(2026-07-10, nightmare-catcher 원 요청 4종 중 마지막 미구현 스킬)를 드림캐쳐 `trigger × payload` 프레임워크로 편입한다.

## 아키텍처 결정과 그 이유

### D1. 펄스 오라 (PeriodicTimer × 신규 페이로드) — 접근 A 채택 (사용자 확정 2026-07-11)

채찍질 = `PeriodicTimer(1s)` × `AllyMoveSpeedAura(tileRange=3, magnitude=20%, duration=1.5s)`.

- 매 펄스마다 범위 내 같은 진영 유닛에게 `StatKind.MoveSpeedMul` Mul 모디파이어(TTL=duration)를 enqueue. **duration > period** 라 범위 내에선 merge-refresh(`(target,source,stat,op,stackId)` 키, `remaining=max`) 로 끊김 없이 유지되고, **범위 이탈/보스 사망 시 ≤duration 내 자연 만료** — enter/exit 추적·revoke 코드가 0.
- 신규 세만틱 = 페이로드 enum 1개뿐. 트리거(PeriodicTimer)·채널(`StatModifierApplyEventsSingleton`)·소비(`MovementSystem` 의 `ModifierStats.moveSpeedMul`)·합성(Πmul, 슬로우 존과 자연 곱)·틱 시스템 전부 기존.

**기각 B — 상시 range-tracking 오라**: enter/exit 검출 + 즉시 부여/회수(PlacementAura 식 revoke). 이탈 즉시 해제가 정밀하지만 신규 상태 추적 시스템이 필요하고, 펄스 TTL 대비 얻는 게 "≤0.5s 해제 정밀도"뿐. 과설계.

**기각 C — AttackOutput producer (healer 선례)**: 보스 공격 사이클에 태우면 비전투(행군) 중 오라가 죽는다. 채찍질의 본질이 행군 가속이라 정면 부적합.

### D2. 진영 중립 세만틱 — "host 와 같은 진영"

정의 계층(`DcMechanic.cs`)은 진영을 모른다(nightmare-catcher 계약). 페이로드 의미 = "host 기준 tileRange 내 **host 와 같은 진영** 유닛 버프, host 자신 제외". arm 이 host 의 태그(AttackUnitTag/DefenderUnitTag)로 풀을 고른다 — 방어유닛에 붙이면 그대로 아군 오라 카드가 된다(후속).

### D3. host 자신 제외

채찍질 서사 = 부하를 채찍질. self 제외는 entity 비교(셀 비교 아님 — 보스와 같은 셀의 미니언은 버프 대상).

### D4. 신규 spec 폴더 (사용자 확정 2026-07-11)

nightmare-catcher 는 완료 마감 + handoff 작성됨 → 불변 유지. `enemy-hunter-targeting` 분리 선례를 따라 `docs/spec/nightmare-whip-aura/` 신설, nightmare-catcher README 후속 후보에서 이관.

## 상태 / 포인터

- spec: `docs/spec/nightmare-whip-aura/` — README + 0(계약/베이크)·1(arm)·2(authoring/Play).
- 선행: `docs/spec/nightmare-catcher/`(프레임워크 편입·PeriodicTimer), `docs/spec/modifier-framework-and-healer/`(modifier 채널/merge), `docs/spec/dreamcatcher-placement-aura/`(오라 선례·magnitude=% 컨벤션).
