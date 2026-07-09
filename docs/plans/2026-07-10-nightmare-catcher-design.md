# Nightmare Catcher — 설계 배경 (얇은 브레인스토밍)

> 실제 구현 스펙은 `docs/spec/nightmare-catcher/` (README + 0~N). 이 문서는 **왜 그렇게 결정했는가**(대안 기각 이유)만 담는다. 계약이 바뀌면 spec 이 우선.

## 목표 한 줄

보스/적에게 **능동 스킬**(폭격·순간이동 등)을 부여한다. 별도 시스템이 아니라 **기존 드림캐쳐 `trigger × payload` 프레임워크에 편입**한다("나이트매어캐쳐" = 드림캐쳐의 서사적 대응물).

## 아키텍처 결정과 그 이유

### D1. 드림캐쳐 프레임워크 편입 (새 시스템 신설 안 함)
드림캐쳐 정의 계층(`DcMechanic.cs`)은 이미 Entities 무참조·진영 중립으로 설계됐고(dreamcatcher-unit-trigger 계약 1), `DcTriggerSlot` 은 임의 엔티티 버퍼다. 막힌 건 해석 계층의 `DefenderUnitTag` 게이트뿐. → 새 트리거/페이로드 **enum + arm** 만 추가하면 적도 태울 수 있다. 적 전용 병렬 시스템(트리거 arm 복제)은 **기각** — `EnemyAiStateSystem.HasFireTarget` 미러링 같은 sync-debt 재생산.

### D2. 게이트는 완화, 라이프사이클은 병렬 (완전 일반화 기각)
- **완화**: 발동 arm/부착 API 게이트를 `isDefender`→`hasSlot` 로. strict superset — 슬롯은 명시 베이크로만 생기므로 defender 동작 불변(회귀 0).
- **병렬**: 부착·정리 라이프사이클(`_activeDcEffects` 미래배치 상속, `DestroyEntitiesByType<DefenderUnitTag>` teardown, 덱 UI)은 defender 배치 파이프라인에 얽혀 있어 **손대지 않고** 적 스폰에 별도 베이크 훅(`TauntAttackGrantSystem` 선례).
- **완전 일반화 기각 이유**: 두 번째 사용처(적)가 실제로 돌기 전에 공통부를 추출하면 defender 회귀 위험만 크다. "구체 구현부터, 반복 생기면 추출"(CLAUDE.md). 공통부 추출은 후속.

### D3. 복합 행동 = 직교 슬롯의 합 (FSM 상태 증식 기각)
폭격·텔레포트·기본공격은 상호배타 상태가 아니라 **동시에 도는 직교 행동**. 하나의 `AiState` enum 에 넣으면 `Marching×Casting×Enraged…` 조합 폭발. → 각자 독립 accumulator/슬롯으로 틱하고, `AiState`(Marching/Engaging/Chasing/Standoff)는 이동/교전 게이트로만 유지. 스킬이 이동을 **하드 중단**할 때만 `AiState.Casting` 1개 추가 검토 — MVP 두 스킬(순간=즉시, 폭격=백그라운드)은 **FSM 무변경**. 이 프로젝트가 이미 쓰는 ECS 관용구(드림캐쳐가 증거).

### D4. MVP = 2 트리거만 (4개 스킬 전부 기각)
사용자 예시 4종(융단폭격·텔레포트·기본공격100·채찍질오라) 중 **새로 만드는 유일한 세만틱은 트리거 2종** — PeriodicTimer(주기)·HealthThreshold(임계치). 기본공격·채찍질오라는 기존 프리미티브(`AttackOutput`/MoveSpeedMul Aura) 조합이라 게이트 개방 후 데이터로 붙음 → 후속. 프레임워크 검증 질문에 답하는 최소 스코프(스코프 엄수).

### D5. 위협 테이블은 보스 전용 + 원거리 귀속
- **보스 전용**: 모든 적에 `(공격자,누적피해)` 버퍼를 드는 건 과함. 보스만(사용자 결정). 일반 적은 기존 nearest/aggro 유지.
- **원거리 귀속 (사용자 결정 2026-07-10, 옵션 b)**: `IncomingDamage` 는 공격자를 안 담고, `ProjectileState` 는 shooter 를 안 추적한다. 이 게임은 방어유닛 **대부분이 원거리(포탑)** 라, 근접만 집계하면 위협 테이블이 상시 비어 텔레포트가 "가장 가까운 놈"으로 붕괴(렌즈 A HIGH-2). → 투사체에 `owner` 필드 추가로 원거리도 귀속. 회귀 격리를 위해 범용 `IncomingDamage` 는 무변경, 보스 대상일 때만 `ThreatHitEvent` enqueue(AggroHitEvents 패턴).

## 렌즈 A(로직 완결성) 크리틱 트레일

드림캐쳐 원칙 "로직 자체가 아키텍처와 무관하게 완전해야 한다"에 따라, 배선 전에 로직 완결성을 독립 크리틱으로 검증(2026-07-10). 적발·반영:
- **HIGH-1** 융단폭격 진영 반전 — 재사용하려던 TileAoe 착탄 arm 이 `AttackUnitTag`(적) 풀이라 방어유닛 대신 적을 때림. → 진영 파라미터화(verbatim 재사용 아님).
- **HIGH-2** 위협 원거리 누락 → 옵션 (b) owner 귀속(위 D5).
- **MED-3~6** period/fraction≤0 스핀-발동 가드 · blink 방향 NaN 기본축 폴백 · 링 순회 상한+skip · 리더 사망 경합(alive=LocalTransform 존재).
- **완결로 인정**: HealthThreshold 수학(반복·래치·다중돌파), 트리거×페이로드 호환성.

## 상태 / 포인터

- spec: `docs/spec/nightmare-catcher/` — README + 0(정의)·1(위협)·2(폭격)·3(텔레포트) 작성 완료, 배선(4 게이트·5 베이크·6 Play)은 렌즈 A 통과 후.
- 선행 프레임워크: `docs/spec/dreamcatcher-unit-trigger/`.
- 현재: 렌즈 A **통과**(확인 재리뷰 CLOSED — N1 상수축·N2 enqueue Null 가드·N3 플래그 기본값=enemy 반영). 배선(4~6) 착수 대기.
