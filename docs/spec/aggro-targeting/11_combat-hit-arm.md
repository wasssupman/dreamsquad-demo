# Unit 11 — Combat arm: AttackSystem 타겟팅 + 히트 이벤트 emit + AggroHitEvents 채널

> 해석 계층(Combat). 트리거 감지 = "가디언 공격 명중". `Aggroed` 직접 쓰기 금지 — 소비자(Effects)-소유 채널로 enqueue만.

## 목적

가디언의 공격 타겟을 aggro-aware 로 고르고(정의 계층 호출), 명중한 적을 Effects 로 넘길 히트 이벤트를 발행한다.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Battle/Combat/AggroHitEvents.cs` (이벤트 struct + 싱글턴)
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (채널 lifecycle)
- `CLAUDE.md` (채널 목록 갱신)

## 구현

**채널**: `AggroHitEvent { Entity guardian; Entity enemy; }` + `AggroHitEventsSingleton { NativeQueue<AggroHitEvent> queue }`. **Effects 소비 / Combat 생산** (§5.2 소비자-소유 대칭). BattleBridge 가 생성/Dispose. **추가 시 실제 활성 NativeQueue 싱글턴 수를 감사하고 CLAUDE.md 목록을 원자적으로 갱신**(critic M4 — "15번째"는 감사 후 확정).

**AttackSystem 타겟팅(가디언 분기)**: 공격자가 `AggroCapacity` 보유 시, 최근접 루프 대신 후보 배열(`Candidate{cell,pos,aggroed}`)을 마샬링해 `AggroTargeting.SelectTargets(gCell, tileRange, held, max, cands, outIdx)` 호출. 결과 인덱스를 hitTargets 로. (`held<max` → 비-어그로 우선 / 상한 → 최근접.) **주의(critic H2)**: 가디언 타겟팅이 aggro 상태에 의존하는 "aggro-aware" 결합 — 의도된 설계. capacity 의미론 변경 시 이곳과 Effects 게이트 두 곳 갱신.

**AttackSystem RESOLVE emit**: 공격자가 `AggroCapacity` 보유 & outputs 경로(근접)면, `hitTargets[]` 각 적에 대해 `AggroHitEvent{ guardian=attackerEntity, enemy=hitTarget }` enqueue. (근접 전용 — 투사체 가디언 없음. ProjectileRef 보유 가디언은 v1 비대상.)

## 완료 기준

- [ ] 컴파일. `AggroHitEventsSingleton` lifecycle(생성/Dispose) BattleBridge 배선. CLAUDE.md 채널 목록·수 갱신.
- [ ] 가디언이 여유 있을 때 사거리 내 비-어그로 적을 우선 타격(Play: 겹친 어그로 적 대신 신규 적 히트).
- [ ] 명중 프레임에 `AggroHitEvent` enqueue(reflection 큐 카운트 확인).
- [ ] 비-가디언(Fighter 등)은 이벤트 emit 안 함.
