# 1 — 오라 펄스 arm + 스폰 베이크

## 목적

`BossPeriodicTriggerSystem` 에 `AllyMoveSpeedAura` 페이로드 분기를 추가하고, `BakeNightmareMechanics` 가 whip 슬롯을 베이크하게 한다. **arm 과 베이크는 같은 커밋** — 베이크만 먼저 들어가면 기존 "unhandled payload" 경고 폴백이 매 펄스 스팸된다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — 페이로드 분기
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeNightmareMechanics`(:4269) whip 베이크 분기

## 구현

### arm 분기 (BossPeriodicTriggerSystem)

- 디스패치: 기존 `payload != AreaBarrage → warn` 폴백을 `AreaBarrage / AllyMoveSpeedAura / else warn` 3분기로 재구성. 트리거 틱(`PeriodicTick`)·슬롯 순회는 무변경(직교 — README 계약 8).
- **degenerate skip**: `slot.magnitude == 0 || slot.duration <= 0` 이면 enqueue 없이 발동 소모(README 계약 6). `periodSeconds<=0` 은 `PeriodicTick` 내부 가드가 이미 차단.
- **아군 풀** = host 와 같은 진영: host 에 `AttackUnitTag` 있으면 적 풀(`AttackUnitTag+LocalTransform`), `DefenderUnitTag` 면 방어 풀. 풀은 **프레임당 1회, whip 발동이 실제로 있을 때만 lazy 빌드** — 기존 defender 풀(폭격 진앙용)의 매 프레임 eager 빌드(`BossPeriodicTriggerSystem.cs:44`)와 **다르다**(그건 entities+cells 수집 관용구 참조일 뿐). 둘 다 아니면 skip(정의: 진영 불명 host 는 no-op).
- **타겟** = `AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange)` → **entity == host 제외**(README 계약 3) → 각 대상에:
  ```
  StatModifierApplyEvent {
    target, stat = StatKind.MoveSpeedMul, op = CombineOp.Multiplicative,
    magnitude = 1f + slot.magnitude / 100f,
    duration = slot.duration, source = host(슬롯 캐리어), stackId = 0
  }
  ```
  채널 = `SystemAPI.TryGetSingletonRW<StatModifierApplyEventsSingleton>`(큐 변이 의도 = RW 표기, nightmare-catcher 후속 노트 정렬). 시스템은 BurstCompile 유지(NativeQueue enqueue·ComponentLookup 모두 Burst 호환).
- `fireCount` 무접촉(round-robin 미사용 — 전수 대상이라 선택 분산 없음, README 계약 7). 0-ally 펄스는 no-op + 타이머 이월(기존 `PeriodicTick` 성질).
- 같은 host 의 whip 슬롯 2개(degenerate authoring)는 merge key 동일(`source,stat,op,stackId=0`)로 한 슬롯 refresh — 마지막 magnitude 승리. 정의된 동작, 가드 불요.

### 베이크 분기 (BakeNightmareMechanics)

- `payload.kind == AllyMoveSpeedAura`: `periodSeconds`(trigger) + `magnitude`/`tileRange`/`duration`(payload) 슬롯 복사. SO 참조 베이크 없음(`projectile` 미사용 → `projectileDataIndex` 0 유지).
- **authoring 계약 경고 (크리틱 M1)**: `payload.duration <= trigger.periodSeconds` 이면 `Debug.LogWarning`(점멸 위험 고지). skip 은 하지 않는다(테스트 자유 유지) — 베이크 1줄, 런타임 비용 0.
- BossTag/ThreatEntry 동행은 기존 함수 서두가 이미 처리(무변경).

### 무회귀 근거

- modifier 계층 코드 무접촉(README 계약 4) — enqueue 하는 producer 가 하나 늘 뿐, `ModifierApplySystem` 이 버퍼 없는 신선한 적에도 ECB 로 `StatModifierSlot` 을 생성해 주는 기존 경로.
- 기존 defender 카드 슬롯은 `periodSeconds=0` 이라 이 arm 에서 발동 자체가 없음(nightmare-catcher 계약 9).

## 완료 기준

- [ ] 컴파일 클린 + 기존 EditMode 스위트 그린.
- [ ] (in-memory 검증) 보스에 whip 슬롯 수동 베이크 → 3타일 내 적 `ModifierStats.moveSpeedMul == 1.2`, 범위 밖 1.0, 보스 자신 1.0 — MCP execute_code 로 확인.
- [ ] code-review(변경 성격 = ECS 시뮬 → ecs-review) 후 다음 unit.
