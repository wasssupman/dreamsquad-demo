> unit 8 부속 — 게이트·부재-상태·쓰기 지도 전수 인벤토리(기계 추출 2026-08-04). 번역 규칙은
> [m1_blueprint_data_mapping.md](m1_blueprint_data_mapping.md) 본문이 소유한다.

# battle-sim-extraction unit 8 원자료 — 게이트 · 부재-상태 · 쓰기 지도

대상: `Assets/_Project/Scripts/Battle/` 의 ISystem 44개 (Combat 9 / Effects 26 / Movement 2 / Units 7).
추출 방식: 전 파일 직접 판독 (`RequireForUpdate` / `RequireAnyForUpdate` / `WithNone` / `RefRW` / `isReadOnly:false` lookup / ECB 호출 / 싱글턴 큐 enqueue).

집계 요약:

| 항목 | 수 |
|---|---|
| ISystem 총계 | 44 |
| `RequireForUpdate<T>()` 보유 | **35** (기대치 일치) |
| `RequireAnyForUpdate(query…)` 보유 | 4 |
| 게이트 보유 소계 (35 + 4, 겸용 0) | 39 |
| 무게이트 (매 tick 실행) | 5 |
| `WithNone<>` 사용 시스템 / 호출 사이트 | 26 / 48 |
| `EntityCommandBuffer` 사용 | 28 (전부 `Allocator.Temp` + 같은 OnUpdate 내 Playback) |
| ECB 미사용 (직접 쓰기/큐만) | 16 |

---

## A. RequireForUpdate 매트릭스

게이트는 **AND** 다 — 나열된 타입 중 하나라도 엔티티 0 이면 `OnUpdate` 가 통째로 스킵된다. `RequireAnyForUpdate` 만 OR.

### A-1. Combat (9/9 전원 게이트 보유)

| 시스템 | 게이트 | 행동 함의 |
|---|---|---|
| `AttackSystem` | `AttackState` | 공격자(AttackState 보유) 0 이면 전 루프 미실행. **부수 정지**: 같은 OnUpdate 머리에 있는 `CastEventsSingleton` 드레인(해저드 캐스트 = host 의 공격 사건)도 함께 멈춰 큐가 적재된다. |
| `BossPeriodicTriggerSystem` | `DcTriggerSlot` · `FlowFieldSingleton` | 카드 슬롯 보유 유닛 0 또는 flow field 미빌드(맵 로딩 전) → 주기 트리거 `elapsed` 누산 자체가 정지. 맵 없이 슬롯만 있어도 안 돈다. |
| `EnemyAiStateSystem` | `EnemyAiState` | FSM 보유 적 0 → 상태 전이 없음. Movement 는 `aiStateLookup` 부재 시 `Marching` 폴백이라 이동은 계속된다. |
| `HealthThresholdSystem` | `FlowFieldSingleton` **만** | `ThreatEntry` 게이트를 의도적으로 제거(unit 1 주석) — 보스 없이 디펜더만 있어도 `last_stand` 가 돌아야 하므로. threat drain 은 `TryGetSingletonRW` + `HasBuffer` 로 독립 가드. |
| `ProjectileEmitterSystem` | `EmitterInstance` · `FlowFieldSingleton` | 진행 중 발사 인스턴스 0 → 미실행. 인스턴스 버퍼가 비어 있어도(길이 0) **버퍼 보유** 자체로 게이트는 통과하므로 루프 안 `instances.Length == 0` continue 가 실제 필터다. |
| `ProjectileHitSystem` | `ProjectileTag` | 투사체 0 → 미실행. 착탄 해결·splash·TileAoe·PathHit 전부 이 게이트 아래. |
| `ProjectileMoveSystem` | `ProjectileTag` | 투사체 0 → 미실행. |
| `TauntAttackGrantSystem` | **Any**(`Aggroed`, `TauntAttackGranted`) | 어그로 적이 없어도 **strip 패스가 살아 있어야** 하므로 OR. 부여분 회수 누락 방지. |
| `UltimateLeapSystem` | `UltimateLeapState` | 이탈 중 유닛 0 → 미실행. 상태가 곧 진행 중 시퀀스라 자기-게이트. |

### A-2. Effects (22/26 게이트 보유)

| 시스템 | 게이트 | 행동 함의 |
|---|---|---|
| `AggroStateSystem` | **Any**(`AggroCapacity`, `Aggroed`) | 마지막 가디언 소멸 후에도 orphan 해제 패스가 돌아야 해서 OR (주석: 구 HIGH1 보존). |
| `AllyBuffFieldSystem` | `AllyBuffField` · `StatModifierApplyEventsSingleton` | 장판 캐리어 0 → 재발행 정지 = 버프가 `AllyBuffApplySec` 안에 자연 소멸(회수 메커니즘이 게이트 그 자체). |
| `CcApplySystem` | `EnemyCcEventsSingleton` | 큐 싱글턴 부재(브리지 셋업 전) → CC 부여 전면 정지. 모든 CC 생산자가 이 큐로 수렴하므로 단일 실패점. |
| `CcClearSystem` | `CcClearRequestsSingleton` | wake-on-hit(Sleep 해제) 전용 소비자. 부재 → 피격해도 안 깨어남. |
| `DefenderFieldSystem` | `DefenderFieldSingleton` | 필드 미할당 → 미실행. 추가로 런타임에 `bossQuery.IsEmpty` 면 재빌드 skip(소비자=보스뿐). |
| `DreamCocoonSystem` | `DreamCocoon` | 잠 완주 감시 대상 0 → 미실행. |
| `EffectTickSystem` | **Any**(`TornadoField`, `PortalLink`, `AllyBuffField`) | 세 캐리어 중 하나라도 있으면 실행. 셋 다 없으면 수명 tick 자체가 불필요. |
| `FatigueAccrualSystem` | `BurnoutGimmickConfig` · `StackModifierApplyEventsSingleton` | **config 존재 = 기믹 활성** 관용구(self-gate, `BurnoutGimmickConfig.cs` 주석에 명시). 비활성 시즌엔 lazy-attach 패스까지 0 비용. |
| `HazardCastSystem` | `HazardCastState` · `FlowFieldSingleton` · `HazardSpawnRequestsSingleton` | 3중 AND. 브리지가 spawn 큐를 만들기 전엔 캐스터의 **쿨다운 tick 도 안 돈다**(쿨다운 감산이 루프 안에 있음). |
| `HazardLifetimeSystem` | `HazardSingleton` | 미실행 시 `cellToEffects` 가 **Clear 되지 않아** 직전 프레임 맵이 그대로 남는다 → ZoneApply 가 stale 셀을 읽을 수 있는 구조(단 ZoneApply 도 같은 싱글턴을 게이트로 요구하므로 동시 정지). |
| `HeatAccrualSystem` | `OnsenGimmickConfig` | 온천 기믹 self-gate. |
| `LastRunSystem` | `RedBullGimmickConfig` | 레드불 기믹 self-gate. 비활성 시 이미 붙은 `LastRun` 의 crash 도 영구 보류. |
| `ModifierApplySystem` | `StatModifierApplyEventsSingleton` · `StackModifierApplyEventsSingleton` | **AND 라서** 스택 큐만 없어도 stat 적용까지 함께 멈춘다. 모디파이어 파이프라인 전체의 단일 실패점. |
| `ObstacleLifetimeSystem` | `ObstacleSingleton` | 미실행 시 `blockedCells` 미갱신 → 이동 trim 이 stale 차단 셀을 본다. |
| `PatrolFieldSystem` | `PatrolAnchor` · `FlowFieldSingleton` | 순찰병 0 → `PatrolStep` 미갱신. Movement 는 `PatrolStep` **보유 여부**로 순찰 아키타입을 판별하므로 게이트가 닫히면 기존 dir 로 계속 걷는다(값이 남아 있음). |
| `PickupConsumeSystem` | `RedBullGimmickConfig` · `FlowFieldSingleton` · `StatModifierApplyEventsSingleton` | 3중 AND. |
| `PickupSpawnSystem` | `RedBullGimmickConfig` · `PickupSpawnState` | 후보 셀 상태(맵 빌드 산물) 부재 → 스폰·만료 tick 모두 정지 → 이미 놓인 픽업이 만료되지 않는다. |
| `ResignationDropSystem` | `ClockOutGimmickConfig` | 퇴근 기믹 self-gate. |
| `ResignationThresholdSystem` | `ClockOutGimmickConfig` · `MeteorBarrageRequestsSingleton` | barrage 큐 부재 → 사직서가 소모되지 않고 무한 적재. |
| `ShieldCastSystem` | `ShieldCastState` · `FlowFieldSingleton` | 실드 캐스터 0 → 미실행(쿨다운 tick 포함). |
| `StackModifierTickSystem` | `EnemyCcEventsSingleton` · `DotApplyEventsSingleton` · `StatModifierApplyEventsSingleton` | **3중 AND의 회귀 위험**: 하나만 없어도 스택 tick 전체가 멈춰 `header.remaining` 이 감산되지 않는다 = 스택이 영구 잔존. `StatModifierTickSystem`(무게이트)과 비대칭. |
| `ZoneApplySystem` | `HazardSingleton` · `EnemyCcEventsSingleton` · `FlowFieldSingleton` | 3중 AND. 추가로 런타임에 `cellToEffects.Count()==0` early-return. |

### A-3. Movement (2/2)

| 시스템 | 게이트 | 행동 함의 |
|---|---|---|
| `BlinkApplySystem` | `BlinkRequestEventsSingleton` | 텔레포트 요청 채널 부재 → 위치 적용 없음. `UltimateLeapSystem` 의 착지 텔레포트도 이 채널로 나가므로 채널 부재 시 궁극기 착지가 제자리(연출만 이동). |
| `MovementSystem` | `PathFollowState` · `FlowFieldSingleton` | 이동체 0 또는 flow field 미빌드 → 이동·포탈·토네이도·goal 판정·`PastGoalTag` 부여 전부 정지. |

### A-4. Units (6/7)

| 시스템 | 게이트 | 행동 함의 |
|---|---|---|
| `DamageApplicationSystem` | `IncomingDamage` | **버퍼 보유 엔티티 0 이면 시스템 전체 미실행** → 같은 루프에 얹힌 Regen 힐·`IncomingHeal` 드레인·실드 병합/흡수·`DamagedCounter` tick·킬 귀속·`DeadTag` 부여까지 동반 정지. 게이트는 버퍼 **비어 있음**이 아니라 **부재**만 본다. |
| `HealthDeathSystem` | `Health` | 사실상 무조건 통과(유닛이 있으면 성립). 안전망 성격. |
| `HitFlashSystem` | `HitFlashTag` | 플래시 중 유닛 0 → 미실행. 태그가 곧 진행 상태. |
| `LethalTimerSystem` | `LethalTimer` | 자폭 타이머 보유 0 → 미실행. |
| `PatrolLifecycleSystem` | `SummonedBy` | 소환된 순찰병 0 → 미실행(소환사 사망 연동 정지). |
| `UnitLifecycleSystem` | **Any**(`_pastGoalQuery`=PastGoalTag+AttackUnitTag, `_deadQuery`=DeadTag) | OR. 유출 또는 사망 중 하나라도 있으면 실행. 해저드 파괴/디펜더 사망 루프는 이 OR 아래에 얹혀 있다(둘 다 DeadTag 를 요구하므로 커버됨). |

### A-5. 무게이트 — 매 tick 실행 (5개)

| 시스템 | 비고 |
|---|---|
| `CcDecaySystem` (Effects) | `IJobEntity.Run()` 으로 `CcEffect` 버퍼 전수 감쇠. 게이트가 없어야 만료가 항상 진행된다. |
| `DotApplySystem` (Effects) | 부여(`DotApplyEventsSingleton`)는 `TryGetSingleton` 옵셔널, 틱/감쇠는 무조건. 로그 큐 유무로 job 2종 분기. |
| `ModifierStatsAggregateSystem` (Effects) | dirty-only 쿼리(`EnabledRefRW<ModifierStatsDirty>`)가 사실상 필터 역할. |
| `StatModifierTickSystem` (Effects) | 무게이트 = 채널 부재와 무관하게 stat 슬롯이 항상 만료된다. `StackModifierTickSystem`(3중 게이트)과의 비대칭이 여기서 발생. |
| `MaxHealthScaleSystem` (Units) | 무게이트. pass1(lazy attach) → 중간 Playback → pass2(재계산). |

### A-6. 게이트 유형 분포 (참고)

- **콘텐츠 존재 게이트**(그 기능의 대상이 있을 때만): `AttackState`, `ProjectileTag`, `DreamCocoon`, `LethalTimer`, `HitFlashTag`, `UltimateLeapState`, `EmitterInstance`, `SummonedBy`, `AllyBuffField`, `PatrolAnchor`, `DcTriggerSlot`, `HazardCastState`, `ShieldCastState`, `EnemyAiState`, `IncomingDamage`, `Health`, `PathFollowState`, `PickupSpawnState`
- **기믹 활성 플래그**(config 싱글턴 존재 = 켜짐): `BurnoutGimmickConfig`, `OnsenGimmickConfig`, `RedBullGimmickConfig`(3 시스템), `ClockOutGimmickConfig`(2 시스템)
- **인프라 싱글턴**(맵/브리지 라이프사이클 커플링): `FlowFieldSingleton`(8 시스템), `HazardSingleton`, `ObstacleSingleton`, `DefenderFieldSingleton`
- **이벤트 채널 싱글턴**(부재 = 파이프라인 정지): `EnemyCcEventsSingleton`, `StatModifierApplyEventsSingleton`, `StackModifierApplyEventsSingleton`, `DotApplyEventsSingleton`, `CcClearRequestsSingleton`, `BlinkRequestEventsSingleton`, `HazardSpawnRequestsSingleton`, `MeteorBarrageRequestsSingleton`

---

## B. 부재-상태 (WithNone / tag) 목록

### B-1. `WithNone<>` 쿼리 (26 시스템 · 48 사이트)

| 시스템 | 쿼리 | 제외 타입 | 의미 |
|---|---|---|---|
| `AttackSystem` | 타겟 후보 스냅샷 | `PendingDeployment` | 배치 대기 유닛은 맞지 않는다(아직 판에 없음) |
| `AttackSystem` | 타겟 후보 스냅샷 | `DeadTag` | 시체 조준 금지 |
| `AttackSystem` | 타겟 후보 스냅샷 | `UltimateLeapState` | 이탈(판 밖) 중 = 조준 불가. `LeapFlight` 는 **의도적으로 제외 안 함**(일반 도약은 비행 중에도 맞는다) |
| `AttackSystem` | 공격자 메인 루프 | `PendingDeployment` | 배치 대기 유닛은 공격 안 함. `DeadTag`/`LeapFlight` 는 쿼리에서 빼지 않고 루프 안 `actionLocked` 술어로 처리 — 쿨다운 tick 과 진행 중 스윙 RESOLVE 를 살려야 하므로 |
| `BossPeriodicTriggerSystem` | 슬롯 루프 | `DeadTag` | 시체가 스킬을 한 번 더 쓰는 것 차단(순서 대신 규칙으로 표현) |
| `EnemyAiStateSystem` | 타겟 후보 | `PendingDeployment`, `DeadTag` | AttackSystem 과 동일 후보 풀 유지 |
| `HealthThresholdSystem` | 슬롯 루프 | `DeadTag` | 오버킬로 경계 다중 관통 시 시체가 폭발/도약하는 것 차단 |
| `ProjectileEmitterSystem` | 인스턴스 host 루프 | `DeadTag` | 죽은 host 는 새 발 안 냄(이미 시작된 버스트는 완주) |
| `ProjectileEmitterSystem` | 적 재조준 풀 | `DeadTag`, `PastGoalTag`, `UltimateLeapState` | 죽은·유출된·판 밖 적은 조준 후보 아님(빈 타일 사격 방지) |
| `ProjectileHitSystem` | AOE 피해자 풀 | `UltimateLeapState` | 이탈 중 적은 splash/TileAoe 피해자도 bounce 후보도 아님 |
| `ProjectileMoveSystem` | 재조준 후보 풀 | `DeadTag`, `PastGoalTag`, `UltimateLeapState` | 같은 규약. 투사체 자체 루프는 무필터 |
| `TauntAttackGrantSystem` | grant | `AttackState`, `TauntAttackGranted` | **부재 = 자체 공격수단 없음 = 도발 공격 부여 대상**. 이미 부여됨(`TauntAttackGranted`) 재부여 차단 |
| `TauntAttackGrantSystem` | strip | `Aggroed` | 어그로 해제됨 = 부여분 회수 대상 |
| `UltimateLeapSystem` | 이탈 루프 | `DeadTag` | 방어적 가드(계약상 공중 사망 없음). 죽었으면 착지 없이 상태만 걷어 "잠긴 시체" 방지 |
| `AllyBuffFieldSystem` | 멤버십 루프 | `PendingDeployment`, `DeadTag` | 배치 대기/사망 유닛은 장판 버프 대상 아님 |
| `DefenderFieldSystem` | 방어유닛 스냅샷 | `PendingDeployment`, `DeadTag` | BFS 소스에서 제외 = 보스가 배치 대기 유닛을 사냥하지 않음 |
| `DreamCocoonSystem` | 완주 감시 | `DeadTag` | 죽으면 판정 중단 |
| `FatigueAccrualSystem` | pass1 lazy attach | `FatigueAccrual` | **부재 = 아직 타이머 미부착** (idempotent attach 관용구) |
| `HazardCastSystem` | 타겟 후보 | `PendingDeployment`, `DeadTag` | 배치 대기/시체는 캐스트 표적 아님 |
| `HazardCastSystem` | 캐스터 루프 | `PendingDeployment`, `DeadTag` | 배치 대기/시체는 캐스트 안 함(쿨다운 tick 도 정지) |
| `HeatAccrualSystem` | pass1 lazy attach | `HeatAccrual` + `DeadTag`, `PendingDeployment` | 미부착 유닛에만 타이머 부착 |
| `HeatAccrualSystem` | pass2 누산 | `DeadTag`, `PendingDeployment` | 시체/배치 대기엔 열기 누적 없음 |
| `ObstacleLifetimeSystem` | 수명 tick | `BlockingHazardCellsBuffer` | **부재 = 단일 셀 장애물 아키타입**. 다중 셀(파괴 가능 해저드)은 두 번째 루프가 담당 = 부재로 아키타입을 가른다 |
| `ObstacleLifetimeSystem` | 다중 셀 루프 | `DeadTag` | 죽은 해저드 셀은 차단에서 빠짐 |
| `PatrolFieldSystem` | 적 셀 스냅샷 | `DeadTag`, `PastGoalTag` | 유출 대기 적은 쫓을 이유 없음 |
| `PatrolFieldSystem` | 순찰병 루프 | `DeadTag` | 죽은 순찰병 dir 미갱신 |
| `PickupConsumeSystem` | defender 소비 | `PendingDeployment`, `DeadTag` | 배치 대기/시체는 픽업 못 먹음 |
| `PickupConsumeSystem` | enemy 소비 | `PendingDeployment`, `DeadTag` | 동일 |
| `ShieldCastSystem` | 후보 스냅샷 | `PendingDeployment`, `DeadTag` | 배치 대기/시체는 실드 대상 아님(자신 포함 규칙과 별개) |
| `ShieldCastSystem` | 캐스터 루프 | `PendingDeployment`, `DeadTag` | 배치 대기/시체는 캐스트 안 함 |
| `MovementSystem` | 이동 루프 | `PastGoalTag` | **유출 확정 유닛은 이동 동결**. `UnitLifecycleSystem` 이 같은 프레임에 파괴하는 전제 — 파괴 루프가 `AttackUnitTag` 를 요구해서 순찰병에 태그가 붙으면 영구 동결된다(그래서 goal 판정에 patrol 게이트가 있다) |
| `DamageApplicationSystem` | 드레인 루프 | `DeadTag` | 이미 죽은 유닛은 재적용 안 함 |
| `DamageApplicationSystem` | 드레인 루프 | `PendingDeployment` | 배치 대기 유닛은 피해 수신 안 함 |
| `HealthDeathSystem` | HP<=0 스캔 | `DeadTag` | 중복 태깅 방지 |
| `LethalTimerSystem` | 타이머 루프 | `DeadTag` | 이번 프레임 피해로 이미 죽은 유닛 double-tag 방지 (critic H5) |
| `MaxHealthScaleSystem` | pass1 lazy attach | `MaxHealthScaleState` | **부재 = baseMax 아직 미캡처** (배율이 1 에서 벗어난 첫 프레임에만 부착) |
| `PatrolLifecycleSystem` | 순찰병 루프 | `DeadTag` | 이미 죽은 순찰병 재태깅 방지 |
| `UnitLifecycleSystem` | general dead 루프 | `DefenderTile` | 위쪽 디펜더 사망 루프와의 **double-destroy 방지** |
| `UnitLifecycleSystem` | general dead 루프 | `BlockingHazard` | 해저드 이벤트 enqueue 루프와의 double-destroy 방지 |

### B-2. `HasComponent` / `HasBuffer` 분기 — load-bearing 한 "부재 = 상태"

| 지점 | 술어 | 의미 |
|---|---|---|
| `DamageApplicationSystem:99` | `UltimateLeapState` **보유** → `damageBuffer.Clear(); continue` | 이탈 중 무적. **쿼리 `WithNone` 으로 빼면 안 된다** — 그러면 2초간 피해가 버퍼에 적립돼 착지 프레임에 통째로 터진다(무적이 아니라 지연 폭탄). 코드 주석에 명시된 의도적 비-WithNone |
| `MovementSystem:72` | `PatrolStep` 보유 = **순찰 아키타입 판별** | 부재 = 일반 이동체. goal 판정·flow step 소스를 둘 다 갈아탄다 |
| `MovementSystem:68` | `EnemyAiState` 부재 → `AiState.Marching` 폴백 | FSM 미보유(디펜더/순찰병 등)는 항상 전진 취급 |
| `MovementSystem:80` / `AttackSystem:254` | `LeapFlight` 보유 → `locked` (CC 와 같은 술어에 OR) | 자기주도 이동/공격 START 만 정지, 외력·쿨다운 tick·진행 중 스윙 RESOLVE 는 유지 |
| `MovementSystem:135` | `DefenderFieldSingleton` 부재 → `hunting=false` 전원 goal 경로 | 필드가 있어도 `dist[idx]==int.MaxValue` 면 도달 불가 → 마칭 폴백(방어유닛 전멸 = 전 셀 MaxValue) |
| `CcApplySystem:33` | `BossTag` 보유 + `IsBossImmune(kind)` → CC 거절 | **부여 시점 1곳** 차단. IsLocked 판정 쪽에 넣으면 무시 지점이 6곳 이상 |
| `AggroStateSystem:118` | `BossTag` 보유 → `Aggroed` 부착 거절 | 부착 1곳 차단. 붙은 뒤 무시는 소비 지점 6곳이라 비싸다 |
| `AggroStateSystem:104` | `AggroCapacity` 부재 → 비-가디언 = 히트 이벤트 무시 | 가디언 자격이 컴포넌트 보유로 정의됨 |
| `AggroStateSystem:57` / `PatrolLifecycleSystem:47` | 사망 3중 판정: `Exists` && !`DeadTag` && `Health.value>0` | ECB 파괴분 + death 프레임 태그 + HP 소진 — 세 창을 모두 덮는다. `Entity` 가 version 을 포함해 재활용 id 방어 |
| `AttackSystem` / `MovementSystem` / `DamageApplicationSystem` | `ModifierStats` 부재 → 배율 `1f`, regen `0f` | 모디파이어 미보유가 기본값과 같은 의미 (부재-안전 기본값) |
| `HealthThresholdSystem:67` / `ProjectileHitSystem:61` / `AttackSystem:140` | `ThreatEntry` **버퍼 보유** = 보스 베이크 | 위협 귀속이 보스에만 적립되도록 하는 유일한 필터 |
| `ProjectileHitSystem` | `DefenderUnitTag`(owner) 보유 | 위협 귀속은 defender 발 착탄만 — 스킬 투사체(owner=Null)는 무영향 |
| `PickupConsumeSystem:90` | `LastRun` 보유 → 소비 거절(**소비 락**) | 재소비로 타이머 리셋해 crash 무한 회피하던 문제 차단. 픽업은 보드에 잔존 |
| `DefenderFieldSystem:40` | `bossQuery.IsEmpty` → 재빌드 skip | 필드 소비자가 보스뿐 |
| `LastRunSystem:41` | `Health` && `IncomingDamage` 버퍼 둘 다 보유해야 crash 적용 | 부재 시 조용히 컴포넌트만 제거 |
| `HazardCastSystem:126` | 캐스터가 `DcTriggerSlot` 버퍼 보유해야 `CastEvent` enqueue | 생산자 게이트 — 없으면 4초마다 이벤트만 적재 |
| `ShieldCastSystem:110` | 기존 슬롯이 이미 amount 이상이면 append/VFX skip | Merge(max) no-op 예측 = 헛불꽃 방지 |
| `UnitLifecycleSystem` / `DamageApplicationSystem` | 싱글턴 쿼리 `CalculateEntityCount()==1` / `TryGetSingletonRW` | **fail-open**: 채널 없으면 이벤트만 빠지고 파괴/피해 로직은 계속 |
| `DreamCocoonSystem:49` | `CcEffect` 에 `Sleep` 부재 && `remaining>0` → 파탄 | 부재가 "피격으로 깨어남"의 신호. `remaining>0` 가드가 파탄/완주의 실제 disambiguator |
| `EnemyAiStateSystem` / `AttackSystem` | `EnemyTargetFilter` 부재 → `classMask=-1`(전체 허용) | 부재 = 무제한 필터 |

---

## C. 쓰기 지도

형식: `쓰기 대상들` = 컴포넌트/버퍼 직접 쓰기(RefRW, RW lookup, DynamicBuffer 변이). `→ 큐` = NativeQueue enqueue(맥락 간 채널). ECB 열은 전부 `Allocator.Temp` 로컬 + 같은 OnUpdate 내 `Playback(state.EntityManager)` + `Dispose()`.

### C-1. Combat

| 시스템 | 쓰기 대상들 | ECB |
|---|---|---|
| `AttackSystem` | `AttackState`(RefRW: cooldown/hitDelay) · `FrontmostAttackLock`(RW lookup) · `FocusTarget`(RW lookup) · `BombLauncherState`(RW lookup, rng 전진) · `DcTriggerSlot`(RW buffer: counter/elapsed 되쓰기) · `PatternSlot`(RW buffer: fireCountBase) · `EmitterInstance`(RW buffer: Add) → `UnitAttackVisualEvents` · `EnemyCcEvents` · `AggroHitEvents` · `AttackOutputLogEvents` · `ThreatHitEvents`(ThreatTable.TryCredit) · `KnockupVisualEvents` · `StatModifierApplyEvents` · `StackModifierApplyEvents` · `DcTriggerFiredEvents` | **O** — Temp 1개. `AddComponent<ProjectileSpawnRequest>`(attacker in-place + 캐리어) · `AddBuffer<ProjectileSpawnOutputElement>` · `RemoveComponent<NextAttackDoubleFire>` · `AppendToBuffer<IncomingDamage>`/`<IncomingHeal>` · `CreateEntity` 캐리어(`ProjectileRequestCarrier`, `PatrolRequestCarrier`) |
| `BossPeriodicTriggerSystem` | `DcTriggerSlot`(RW buffer: elapsed) · `PatternSlot`(RW buffer: fireCountBase) · `EmitterInstance`(RW buffer: Add) → `StatModifierApplyEvents` · `ProjectileHitEvents` | **X** — 구조 변경 없음(버퍼 내용 변이만) |
| `EnemyAiStateSystem` | `EnemyAiState`(RefRW) — 유일한 writer | **X** |
| `HealthThresholdSystem` | `ThreatEntry`(RW buffer, `ThreatTable.Accumulate`) · `DcTriggerSlot`(RW buffer: nextBoundaryIndex) → `StatModifierApplyEvents` · `BlinkRequestEvents` · `BossLeapVisualEvents` · `UltimateLeapVisualEvents` | **O** — `AddComponent<UltimateLeapState>` · `AddComponent<LeapFlight>` · `CreateEntity` SelfTileAoe 캐리어 |
| `ProjectileEmitterSystem` | `EmitterInstance`(RW buffer: runtime 되쓰기 / 완주 시 `RemoveAtSwapBack`) | **O** — `CreateEntity` + `AddComponent<ProjectileSpawnRequest>` + `ProjectileRequestCarrier` (발 수만큼) |
| `ProjectileHitSystem` | `IncomingDamage`(RW buffer lookup 확보) · `IncomingHeal`(RW) · `AttackOutputElement`(RW buffer: bounce 감쇠 in-place) → `ProjectileHitEvents` · `EnemyCcEvents` · `ThreatHitEvents` · `StatModifierApplyEvents` · `StackModifierApplyEvents` | **O** — `AppendToBuffer<IncomingDamage>`/`<IncomingHeal>`/`<PathHitRecord>` · `SetComponent`/`AddComponent<HitFlashTag>` · `SetComponent<ProjectileState>`(bounce next) · `RemoveComponent<AttackOutputElement>` · `DestroyEntity`(투사체) |
| `ProjectileMoveSystem` | `LocalTransform`(RefRW: 투사체 위치) · `ProjectileState`(RefRW: elapsed/target/impactReached) | **O** — `DestroyEntity`(타겟 소멸·수명 종료) |
| `TauntAttackGrantSystem` | (직접 쓰기 없음 — 전부 ECB) | **O** — grant: `AddComponent<AttackState>` + `AddBuffer<AttackOutputElement>` + `AddComponent<TauntAttackGranted>` / strip: 3개 `RemoveComponent` |
| `UltimateLeapSystem` | `UltimateLeapState`(RefRW: remaining) → `BlinkRequestEvents` · `UltimateLeapVisualEvents` | **O** — `CreateEntity` 슬램 캐리어 · `RemoveComponent<UltimateLeapState>` · `RemoveComponent<LeapFlight>` |

### C-2. Effects

| 시스템 | 쓰기 대상들 | ECB |
|---|---|---|
| `AggroStateSystem` | `AggroCapacity`(RefRW: held full recompute) — `Aggroed`/`AggroCapacity` 단독 writer | **O** — `AddComponent<Aggroed>` · `AddBuffer<AggroChaseCell>` · `RemoveComponent<Aggroed>` · `RemoveComponent<AggroChaseCell>` |
| `AllyBuffFieldSystem` | (컴포넌트 쓰기 0) → `StatModifierApplyEvents` (매 프레임 재발행) | **X** |
| `CcApplySystem` | `CcEffect`(EntityManager.GetBuffer → `CcEffectMerge.Apply`) | **X** — non-Burst OnUpdate |
| `CcClearSystem` | `CcEffect`(GetBuffer → `RemoveAtSwapBack`) | **X** |
| `CcDecaySystem` | `CcEffect`(IJobEntity `ref DynamicBuffer`: remainingTime 감산 + 만료 제거) | **X** |
| `DefenderFieldSystem` | `DefenderFieldSingleton.flow`/`.dist`(싱글턴 내부 NativeArray in-place 재빌드) | **X** |
| `DotApplySystem` | `DotEffect`(부여 merge + tick/감쇠 되쓰기) · `IncomingDamage`(job 내 `ref DynamicBuffer.Add`) → `HazardRuntimeEvents` | **X** |
| `DreamCocoonSystem` | `DreamCocoon`(RefRW: remaining) → `StatModifierApplyEvents` | **O** — `RemoveComponent<DreamCocoon>`(파탄/완주) |
| `EffectTickSystem` | `TornadoField` · `AllyBuffField` · `PortalLink` (각 RefRW: remaining) | **O** — 만료 시 `DestroyEntity`(캐리어 엔티티 통째) |
| `FatigueAccrualSystem` | `FatigueAccrual`(RefRW: elapsed) → `StackModifierApplyEvents` | **O** — pass1 `AddComponent<FatigueAccrual>` + **중간 Playback** 후 pass2 |
| `HazardCastSystem` | `HazardCastState`(RefRW: cooldownRemaining) → `HazardSpawnRequests` · `UnitAttackVisualEvents` · `CastEvents` | **X** |
| `HazardLifetimeSystem` | `HazardSingleton.cellToEffects`(Clear + 재적재) · `Hazard`(RefRW: remainingLife) | **O** — 만료 `DestroyEntity` |
| `HeatAccrualSystem` | `HeatAccrual`(RefRW: elapsed/stacks) · `IncomingHeal`(RW buffer lookup `.Add`) · `IncomingDamage`(RW buffer lookup `.Add`) | **O** — pass1 `AddComponent<HeatAccrual>` + `AddBuffer<IncomingHeal>` + **중간 Playback**, 이후 두 lookup 재`Update`(구조 변경으로 무효화) |
| `LastRunSystem` | `LastRun`(RefRW: remaining) · `IncomingDamage`(`SystemAPI.GetBuffer(...).Add`, crash 피해) | **O** — `RemoveComponent<LastRun>` |
| `ModifierApplySystem` | `StatModifierSlot`(GetBuffer 병합/추가) · `StackModifierSlot`(동일) · `ModifierStatsDirty`(**EntityManager 즉시** `AddComponent` + `SetComponentEnabled`) | **O(혼용)** — ECB 는 `AddBuffer` 만. 버퍼 신설·MarkDirty 는 **의도적으로 EntityManager 즉시** — 같은 드레인 루프에서 같은 타깃에 두 번째 이벤트가 오면 ECB 는 AddBuffer 를 두 번 기록해 첫 슬롯이 덮인다 |
| `ModifierStatsAggregateSystem` | `ModifierStats`(RefRW — **유일한 writer**) · `ModifierStatsDirty`(EnabledRefRW → false) | **X** |
| `ObstacleLifetimeSystem` | `ObstacleSingleton.blockedCells`(Clear + 재적재) · `Obstacle`(RefRW: remainingLife) | **O** — 만료 `DestroyEntity` |
| `PatrolFieldSystem` | `PatrolStep`(RefRW: dir — 유일한 writer) | **X** |
| `PickupConsumeSystem` | (컴포넌트 직접 쓰기 0) → `StatModifierApplyEvents` | **O** — `DestroyEntity`(픽업) · `AddComponent<LastRun>`. `EntityManager.HasComponent<LastRun>` 로 소비 락 즉시 판정 |
| `PickupSpawnSystem` | `Pickup`(RefRW: remainingLife) · `PickupSpawnState`(RW 싱글턴: elapsed, **rng 상태 되쓰기 — 결정론**) | **O** — 만료 `DestroyEntity` · `CreateEntity` + `AddComponent<Pickup>` |
| `ResignationDropSystem` | (직접 쓰기 0) | **O** — `CreateEntity` + `AddComponent<Resignation>`(사망 defender 타일마다) |
| `ResignationThresholdSystem` | (직접 쓰기 0) → `MeteorBarrageRequests` | **O** — `DestroyEntity`(임계 배수만큼 사직서 소모) |
| `ShieldCastSystem` | `ShieldCastState`(RefRW: cooldownRemaining) · `IncomingShield`(RW buffer lookup `.Add`) → `ShieldGrantedEvents` | **X** |
| `StackModifierTickSystem` | `StackModifierSlot`(GetBuffer: remaining 감산 · stackCount consume · lastTriggeredStack · 만료 제거) → `EnemyCcEvents` · `DotApplyEvents` · `StatModifierApplyEvents` | **X** — non-Burst(BattleBridge 관리 Dictionary SO 조회) |
| `StatModifierTickSystem` | `StatModifierSlot`(GetBuffer: remaining 감산 + 만료 제거) · `ModifierStatsDirty`(`SystemAPI.SetComponentEnabled` 즉시) | **X** |
| `ZoneApplySystem` | **컴포넌트 쓰기 0** → `StatModifierApplyEvents` · `DotApplyEvents` · `EnemyCcEvents` · `HazardRuntimeEvents` | **X** — 순수 생산자 |

### C-3. Movement

| 시스템 | 쓰기 대상들 | ECB |
|---|---|---|
| `BlinkApplySystem` | `LocalTransform`(RW lookup, x/z 만 — y 는 mover 자기 값 유지) | **X** |
| `MovementSystem` | `LocalTransform`(RefRW: 포탈 텔레포트 · chase step · flow step · pull · recenter) | **O** — `AddComponent<PastGoalTag>`(goal 셀 도달) |

### C-4. Units

| 시스템 | 쓰기 대상들 | ECB |
|---|---|---|
| `DamageApplicationSystem` | `Health`(RefRW — Units 소유) · `IncomingDamage`(Clear) · `IncomingHeal`(RW lookup, Clear) · `ShieldSlot`(RW lookup: Merge/Absorb) · `IncomingShield`(RW lookup, Clear) · `DamagedCounter`(RW lookup: counter tick) → `HealAppliedEvents` · `DamageNumberEvents` · `EnemyKilledEvents` · `CcClearRequests` · `StatModifierApplyEvents` · `ShieldBreakEvents` | **O** — `AddComponent<DeadTag>` · `AddComponent<NextAttackDoubleFire>` |
| `HealthDeathSystem` | (직접 쓰기 0 — Health 는 RefRO) | **O** — `AddComponent<DeadTag>` |
| `HitFlashSystem` | `LocalTransform`(RefRW: Scale 만) · `HitFlashTag`(RefRW: remaining) | **O** — `RemoveComponent<HitFlashTag>` |
| `LethalTimerSystem` | `LethalTimer`(RefRW: remaining) | **O** — `AddComponent<DeadTag>` + `RemoveComponent<LethalTimer>` |
| `MaxHealthScaleSystem` | `Health`(RefRW: value+max, `Health.ScaleMax` 순수함수) · `MaxHealthScaleState`(RefRW: appliedMul) | **O** — pass1 `AddComponent<MaxHealthScaleState>` + **중간 Playback** 후 pass2 |
| `PatrolLifecycleSystem` | (직접 쓰기 0) | **O** — `AddComponent<DeadTag>`(소환사 사망 시 순찰병) |
| `UnitLifecycleSystem` | (직접 쓰기 0) → `GoalReachedEvents` · `DefenderDeathEvents` · `HazardDestroyedEvents` | **O** — 4개 루프 전부 `DestroyEntity`. enqueue 를 파괴 **앞**에 두어 브리지가 tile/cell 을 보기 전 소멸하지 않게 함 |

### C-5. ECB 패턴 관찰

- 28개 전부 `new EntityCommandBuffer(Allocator.Temp)` → 같은 `OnUpdate` 내 `Playback(state.EntityManager)` → `Dispose()`. **공유 ECB / SystemGroup EntityCommandBufferSystem 사용 0**.
- **OnUpdate 내 중간 Playback 3건** (lazy-attach 2-pass 관용구): `MaxHealthScaleSystem`, `FatigueAccrualSystem`, `HeatAccrualSystem`. `HeatAccrualSystem` 은 중간 Playback 이 BufferLookup 을 무효화하므로 `_healLookup`/`_damageLookup` 을 **두 번** `Update` 한다(같은 프레임 웨이브 스폰 + 기존 유닛 데미지 append 경합 방어).
- **ECB 와 EntityManager 즉시 쓰기 혼용 1건**: `ModifierApplySystem` — 같은 드레인 루프의 동일 타깃 2회 이벤트에서 ECB `AddBuffer` 중복 기록이 첫 슬롯을 덮는 문제를 피하려 버퍼 신설·MarkDirty 를 즉시 수행.
- ECB 미사용 16개는 (a) 순수 생산자(`ZoneApplySystem`, `AllyBuffFieldSystem`), (b) 버퍼 내용만 변이(`Cc*`/`Dot*`/`*ModifierTick*`), (c) 싱글턴 내부 배열 재빌드(`DefenderFieldSystem`), (d) 컴포넌트 in-place 만(`EnemyAiStateSystem`, `PatrolFieldSystem`, `HazardCastSystem`, `ShieldCastSystem`, `BlinkApplySystem`, `BossPeriodicTriggerSystem`, `ModifierStatsAggregateSystem`).

### C-6. 단독 writer 선언이 코드/주석에 명시된 컴포넌트

| 컴포넌트 | 유일 writer |
|---|---|
| `ModifierStats` | `ModifierStatsAggregateSystem` |
| `EnemyAiState` | `EnemyAiStateSystem` |
| `PatrolStep` | `PatrolFieldSystem` |
| `Aggroed` · `AggroCapacity` | `AggroStateSystem` (Movement/Attack 은 RO) |
| `Health` | Units 맥락 (`DamageApplicationSystem`, `MaxHealthScaleSystem`) |
| `LocalTransform`(유닛 위치) | Movement 맥락 (`MovementSystem`, `BlinkApplySystem`) — 단 `HitFlashSystem` 이 **Scale 만**, `ProjectileMoveSystem` 이 **투사체 위치**를 쓴다 |
| `ShieldSlot` | `DamageApplicationSystem` (`ShieldCastSystem` 은 `IncomingShield` append 만) |
| `DamagedCounter` | `DamageApplicationSystem` (Combat 은 charge 만 read) |
