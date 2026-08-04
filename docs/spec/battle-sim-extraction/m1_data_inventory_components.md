> unit 8 부속 — 컴포넌트 전수 인벤토리(기계 추출 2026-08-04, HEAD 기준 재검수). 매핑 규칙은
> [m1_blueprint_data_mapping.md](m1_blueprint_data_mapping.md) 본문이 소유한다.

# ECS 컴포넌트 전수 인벤토리 — `Assets/_Project/Scripts/Battle/`

추출 기준: HEAD (master, 8786f91a 시점 워크트리 clean) · `IComponentData` **97**개 / `IBufferElementData` **21**개.
경로는 `Assets/_Project/Scripts/Battle/` 이하 상대경로. `ISharedComponentData` / `ICleanupComponentData` 구현체는 **0개**(전 프로젝트 grep 확인).

## 총계

| 맥락 | IComponentData | 그중 tag | channel-singleton | config-singleton | data | IBufferElementData |
|---|---|---|---|---|---|---|
| (root, 맥락 없음) | 1 | 0 | 0 | 1 | 0 | 0 |
| Units | 22 | 4 | 6 | 0 | 12 | 5 |
| Movement | 4 | 1 | 1 | 0 | 2 | 0 |
| Combat | 30 | 5 | 9 | 0 | 16 | 8 |
| Effects | 40 | 2 | 11 | 9 | 18 | 8 |
| **합계** | **97** | 12 | 27 | 10 | 48 | **21** |

기대치 대조: IBufferElementData 21 = 기대 21 **일치**. IComponentData 97 = 기대 96 **+1** — 초과분은 `BattleTimeScale`(`Battle/` 루트, 4개 맥락 폴더 중 어디에도 없음). 4개 맥락 폴더만 세면 정확히 96이다.

`channel-singleton` 27개는 CLAUDE.md 가 명시한 "현재 운영 중인 NativeQueue 채널 27개"와 수가 일치한다.

종별 규칙: `tag` = 필드 0 · `data` = per-entity 상태 · `channel-singleton` = `NativeQueue<T> queue` 단일 필드 싱글턴 · `config-singleton` = 비-큐 싱글턴(SO 설정 또는 월드 공유 상태; 후자는 필드 요약에 `world-state` 표기) · `buffer` = `IBufferElementData`.

---

## (root) — 맥락 폴더 밖

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| BattleTimeScale | BattleTimeScale.cs | — (root) | config-singleton | float Value | no |

---

## Units

### IComponentData (22)

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| AttackUnitTag | Units/AttackUnitTag.cs | Units | tag | — | no |
| AwakeningReward | Units/AwakeningReward.cs | Units | data | int value | no |
| DamageNumberEventsSingleton | Units/DamageNumberEventsSingleton.cs | Units | channel-singleton | NativeQueue&lt;DamageNumberEvent&gt; queue | no |
| DeadTag | Units/DeadTag.cs | Units | tag | — | no |
| DefenderClassTag | Units/DefenderClassTag.cs | Units | data | DefenderClass value | no |
| DefenderDeathEventsSingleton | Units/DefenderDeathEventsSingleton.cs | Units | channel-singleton | NativeQueue&lt;DefenderDeathEvent&gt; queue | no |
| DefenderTile | Units/DefenderTile.cs | Units | data | int2 cell | no |
| DefenderUnitTag | Units/DefenderUnitTag.cs | Units | tag | — | no |
| DeployedFacing | Units/DeployedFacing.cs | Units | data | int2 value | no |
| EnemyKilledEventsSingleton | Units/EnemyKilledEventsSingleton.cs | Units | channel-singleton | NativeQueue&lt;EnemyKilledEvent&gt; queue | no |
| FactionTag | Units/FactionTag.cs | Units | data | Faction value | no |
| GoalReachedEventsSingleton | Units/GoalReachedEventsSingleton.cs | Units | channel-singleton | NativeQueue&lt;GoalReachedEvent&gt; queue | no |
| HealAppliedEventsSingleton | Units/HealAppliedEventsSingleton.cs | Units | channel-singleton | NativeQueue&lt;HealAppliedEvent&gt; queue | no |
| Health | Units/Health.cs | Units | data | float value, float max | no |
| HitFlashTag | Units/HitFlashTag.cs | Units | data | float remaining, duration, originalScale | no |
| KillScore | Units/KillScore.cs | Units | data | int value | no |
| LethalTimer | Units/LethalTimer.cs | Units | data | float remaining | no |
| MaxHealthScaleState | Units/MaxHealthScaleState.cs | Units | data | float baseMax, appliedMul | no |
| PendingDeployment | Units/PendingDeployment.cs | Units | tag | — | no |
| ShieldBreakEventsSingleton | Units/ShieldBreakEvent.cs | Units | channel-singleton | NativeQueue&lt;ShieldBreakEvent&gt; queue | no |
| SimEntityId | Units/SimEntityId.cs | Units | data | int value | no |
| SummonedBy | Units/SummonedBy.cs | Units | data | Entity owner | no |

### IBufferElementData (5)

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| DamagedCounter | Units/DamagedCounter.cs | Units | buffer | 9개 — int instanceId / ushort period, counter / DcPayloadKind payload / float magnitude / int tileRange, aoeDataIndex / DcGateKind gate / float gateValue. `[InternalBufferCapacity(2)]` | no |
| IncomingDamage | Units/IncomingDamage.cs | Units | buffer | float amount, Entity source | no |
| IncomingHeal | Units/IncomingHeal.cs | Units | buffer | float amount | no |
| IncomingShield | Units/IncomingShield.cs | Units | buffer | Entity source, float amount | no |
| ShieldSlot | Units/ShieldSlot.cs | Units | buffer | Entity source, float value | no |

---

## Movement

### IComponentData (4)

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| BlinkRequestEventsSingleton | Movement/BlinkRequestEvents.cs | Movement | channel-singleton | NativeQueue&lt;BlinkRequestEvent&gt; queue | no |
| PastGoalTag | Movement/PastGoalTag.cs | Movement | tag | — | no |
| PathFollowState | Movement/PathFollowState.cs | Movement | data | float speed | no |
| PatrolAnchor | Movement/PatrolAnchor.cs | Movement | data | int2 cell, int tileRadius | no |

### IBufferElementData (0)

없음 — Movement 맥락은 버퍼를 소유하지 않는다.

---

## Combat

### IComponentData (30)

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| AggroAttackProfile | Combat/AggroAttackProfile.cs | Combat | data | float damage, cooldown, range | no |
| AttackOutputLogEventsSingleton | Combat/AttackOutputLogEvent.cs | Combat | channel-singleton | NativeQueue&lt;AttackOutputLogEvent&gt; queue | no |
| AttackState | Combat/AttackState.cs | Combat | data | 9개 — float range, cooldownDuration, cooldownRemaining / int attackTargetCount, targetMask / float hitDelaySec, hitDelayRemaining / float2 committedDirection / byte hasCommittedDirection | no |
| BombLauncherState | Combat/BombLauncherState.cs | Combat | data | 10개 — int landingTiles, aoeTileRange, aoeTargetCap / float travelSec, fuseSec, arcHeight, dmgBombDamage, sleepSec, stunSec / Random rng | no |
| BossLeapVisualEventsSingleton | Combat/BossLeapVisualEvents.cs | Combat | channel-singleton | NativeQueue&lt;BossLeapVisualEvent&gt; queue | no |
| BossTag | Combat/BossTag.cs | Combat | tag | — | no |
| CastEventsSingleton | Combat/CastEvents.cs | Combat | channel-singleton | NativeQueue&lt;CastEvent&gt; queue | no |
| DcTriggerFiredEventsSingleton | Combat/DcTriggerFiredEvents.cs | Combat | channel-singleton | NativeQueue&lt;DcTriggerFiredEvent&gt; queue | no |
| DefenderCcData | Combat/DefenderCcData.cs | Combat | data | 8개 float — knockbackDistance, knockbackDuration, onPlacePushDistance, onPlacePushDuration, onPlacePushRadius, sleepOnHitSec, knockupOnHitSec, knockupVisualHeight | no |
| EnemyAiState | Combat/EnemyAiState.cs | Combat | data | AiState value (Marching/Engaging/Chasing/Standoff) | no |
| EnemyBehavior | Combat/EnemyBehavior.cs | Combat | data | EnemyTargetMode targetMode, EngageMovement engageMovement | no |
| EnemyTargetFilter | Combat/EnemyTargetFilter.cs | Combat | data | int classMask, priorityClass | no |
| FocusTarget | Combat/FocusTarget.cs | Combat | data | Entity current | no |
| FrontmostAttackLock | Combat/FrontmostAttackLock.cs | Combat | data | 4개 — bool active, targetIsPriority / Entity target / float damageMulSnapshot | no |
| KnockupVisualEventsSingleton | Combat/KnockupVisualEvents.cs | Combat | channel-singleton | NativeQueue&lt;KnockupVisualEvent&gt; queue | no |
| LeapFlight | Combat/LeapFlight.cs | Combat | tag | — | no |
| NextAttackDoubleFire | Combat/NextAttackDoubleFire.cs | Combat | data | int charges | no |
| PatrolSpawnRequest | Combat/PatrolSpawnRequest.cs | Combat | data | 4개 — Entity owner / int2 ownerCell / int patrolDataIndex, leashTileRadius | no |
| PatrolRequestCarrier | Combat/PatrolSpawnRequest.cs | Combat | tag | — (같은 파일 다중 선언) | no |
| ProjectileHitEventsSingleton | Combat/Projectile/ProjectileHitEventsSingleton.cs | Combat | channel-singleton | NativeQueue&lt;ProjectileHitEvent&gt; queue | no |
| ProjectileRef | Combat/Projectile/ProjectileRef.cs | Combat | data | 11개 — int dataIndex, impactTileRange / float speed, hitThreshold, visualScale, splashRadius, splashDamageMul, arcHeight / OnHitEffectType onHitEffect / MovementKind movement / PayloadKind payload | no |
| ProjectileRequestCarrier | Combat/Projectile/ProjectileRequestCarrier.cs | Combat | tag | — | no |
| ProjectileSpawnRequest | Combat/Projectile/ProjectileSpawnRequest.cs | Combat | data | 33개 — 대표: MovementKind movement, PayloadKind payload, float3 origin/impact, float damage, speed, Entity target/owner/priorityTarget, byte ccKind/bombType, int bounceRemaining, ProjectileTargetFaction targetFaction | no |
| ProjectileState | Combat/Projectile/ProjectileState.cs | Combat | data | 37개 — 대표: MovementKind movement, PayloadKind payload, float damage, elapsed, flightTime, float3 origin/impact/control1/control2/prevPos, Entity target/owner, int pierceRemaining, bounceRemaining | no |
| ProjectileTag | Combat/Projectile/ProjectileTag.cs | Combat | tag | — | no |
| SummonerState | Combat/SummonerState.cs | Combat | data | 4개 — int patrolDataIndex, leashTileRadius / Entity current / bool hasSummonedOnce | no |
| ThreatHitEventsSingleton | Combat/ThreatTable.cs | Combat | channel-singleton | NativeQueue&lt;ThreatHitEvent&gt; queue (같은 파일에 ThreatEntry 버퍼 + static ThreatTable) | no |
| UltimateLeapState | Combat/UltimateLeapState.cs | Combat | data | 6개 — float remaining, slamDamage / int2 landingCell / float3 landingWorld / int slamTileRange, projectileDataIndex | no |
| UltimateLeapVisualEventsSingleton | Combat/UltimateLeapVisualEvents.cs | Combat | channel-singleton | NativeQueue&lt;UltimateLeapVisualEvent&gt; queue | no |
| UnitAttackVisualEventsSingleton | Combat/UnitAttackVisualEventsSingleton.cs | Combat | channel-singleton | NativeQueue&lt;UnitAttackVisualEvent&gt; queue | no |

### IBufferElementData (8)

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| AttackOutputElement | Combat/AttackOutputElement.cs | Combat | buffer | AttackOutput value | no |
| DcAttackModSlot | Combat/DcAttackModSlot.cs | Combat | buffer | 5개 — int instanceId, count, tileRange / DcAttackModKind kind / float damageMul. `[InternalBufferCapacity(2)]` | no |
| DcTriggerSlot | Combat/DcTriggerSlot.cs | Combat | buffer | 27개 — 대표: int instanceId, DcTriggerKind trigger, ushort period/counter, DcPayloadKind payload, float magnitude, duration, periodSeconds, CcKind ccKind, StackKind stackKind, StatKind buffStat, DcGateKind gate, int patternIndex, float slamDamage. `[InternalBufferCapacity(2)]` | no |
| EmitterInstance | Combat/Projectile/Emission/EmitterInstance.cs | Combat | buffer | PatternSpec spec, EmitterRuntime runtime, ProjectileSpawnRequest template, Entity lockedTarget. `[InternalBufferCapacity(2)]` | no |
| PathHitRecord | Combat/Projectile/PathHitRecord.cs | Combat | buffer | Entity value | no |
| PatternSlot | Combat/Projectile/Emission/PatternSlot.cs | Combat | buffer | PatternSpec spec, ProjectileSpawnRequest template, int fireCountBase. `[InternalBufferCapacity(1)]` | no |
| ProjectileSpawnOutputElement | Combat/Projectile/ProjectileSpawnRequest.cs | Combat | buffer | AttackOutput value (같은 파일 다중 선언) | no |
| ThreatEntry | Combat/ThreatTable.cs | Combat | buffer | Entity attacker, float cumulativeDamage. `[InternalBufferCapacity(4)]` | no |

---

## Effects

### IComponentData (40)

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| AggroCapacity | Effects/AggroCapacity.cs | Effects | data | int max, held | no |
| AggroHitEventsSingleton | Effects/AggroHitEvents.cs | Effects | channel-singleton | NativeQueue&lt;AggroHitEvent&gt; queue | no |
| Aggroed | Effects/Aggroed.cs | Effects | data | Entity guardian | no |
| AllyBuffField | Effects/AllyBuffField.cs | Effects | data | 5개 — int2 centerCell / int tileRange / StatKind stat / float magnitude, remaining (+ const ushort StackId=3) | no |
| BlockingHazard | Effects/BlockingHazard.cs | Effects | data | int hazardSoIndex, float maxHp | no |
| BurnoutGimmickConfig | Effects/BurnoutGimmickConfig.cs | Effects | config-singleton | 4개 — float fatigueInterval, fatiguePerAppDuration / byte fatigueAmount, fatigueMaxStack | no |
| CcClearRequestsSingleton | Effects/CcClearEvents.cs | Effects | channel-singleton | NativeQueue&lt;CcClearRequest&gt; queue | no |
| ClockOutGimmickConfig | Effects/ClockOutGimmickConfig.cs | Effects | config-singleton | 6개 — byte resignationThreshold, meteorCount / float meteorDamage, meteorWarningSec, meteorStaggerSec / int meteorTileRange | no |
| DefenderFieldSingleton | Effects/DefenderFieldSingleton.cs | Effects | config-singleton (world-state) | 6개 — NativeArray&lt;byte&gt; walkMask, NativeArray&lt;float2&gt; flow, NativeArray&lt;int&gt; dist, int2 gridSize, float tileSize, float3 origin | no |
| DotApplyEventsSingleton | Effects/DotApplyEvents.cs | Effects | channel-singleton | NativeQueue&lt;DotApplyEvent&gt; queue | no |
| DreamCocoon | Effects/DreamCocoon.cs | Effects | data | 4개 — float remaining, mult / StatKind stat / ushort stackId (+ const Epsilon) | no |
| EnemyCcEventsSingleton | Effects/EnemyCcEvents.cs | Effects | channel-singleton | NativeQueue&lt;EnemyCcEvent&gt; queue | no |
| FatigueAccrual | Effects/FatigueAccrual.cs | Effects | data | float elapsed | no |
| FlowFieldSingleton | Effects/FlowFieldSingleton.cs | Effects | config-singleton (world-state) | 8개 — NativeArray&lt;float2&gt; flow, NativeArray&lt;int&gt; dist, NativeArray&lt;int2&gt; goals, int2 gridSize, goalCell, float tileSize, float3 origin, int version | no |
| Hazard | Effects/Hazard.cs | Effects | data | float remainingLife | no |
| HazardCastState | Effects/HazardCastState.cs | Effects | data | 8개 — float range, cooldownDuration, cooldownRemaining / int targetMask, dataIndex, footprintWidth, footprintHeight / HazardCastKind kind | no |
| HazardDestroyedEventsSingleton | Effects/HazardDestroyedEventsSingleton.cs | Effects | channel-singleton | NativeQueue&lt;HazardDestroyedEvent&gt; queue | no |
| HazardRuntimeEventsSingleton | Effects/HazardRuntimeEvents.cs | Effects | channel-singleton | NativeQueue&lt;HazardRuntimeEvent&gt; queue | no |
| HazardSingleton | Effects/HazardSingleton.cs | Effects | config-singleton (world-state) | NativeParallelMultiHashMap&lt;int2, HazardEffect&gt; cellToEffects | no |
| HazardSpawnRequestsSingleton | Effects/HazardSpawnRequest.cs | Effects | channel-singleton | NativeQueue&lt;HazardSpawnRequest&gt; queue | no |
| HeatAccrual | Effects/HeatAccrual.cs | Effects | data | float elapsed, byte stacks | no |
| LastRun | Effects/LastRun.cs | Effects | data | float remaining | no |
| MeteorBarrageRequestsSingleton | Effects/MeteorBarrageRequestsSingleton.cs | Effects | channel-singleton | NativeQueue&lt;MeteorBarrageRequest&gt; queue | no |
| ModifierStats | Effects/Modifiers/ModifierStats.cs | Effects | data | 7개 float — damageMul, attackSpeedMul, dmgTakenMul, regenPerSec, moveSpeedMul, damageVsCcMul, maxHealthMul | no |
| ModifierStatsDirty | Effects/Modifiers/ModifierStats.cs | Effects | tag | — (같은 파일 다중 선언) | **yes** |
| Obstacle | Effects/Obstacle.cs | Effects | data | int2 cell, float3 worldPosition, float remainingLife | no |
| ObstacleSingleton | Effects/ObstacleSingleton.cs | Effects | config-singleton (world-state) | NativeHashSet&lt;int2&gt; blockedCells | no |
| OnsenGimmickConfig | Effects/OnsenGimmickConfig.cs | Effects | config-singleton | 5개 — float heatInterval, healPercent, lossPercent / byte flipThreshold, heatMaxStack | no |
| PatrolStep | Effects/PatrolStep.cs | Effects | data | float2 dir | no |
| Pickup | Effects/Pickup.cs | Effects | data | int2 cell, PickupKind kind, float remainingLife | no |
| PickupSpawnState | Effects/PickupSpawnState.cs | Effects | config-singleton (world-state) | NativeArray&lt;int2&gt; candidateCells, float elapsed, Random rng | no |
| PortalLink | Effects/PortalLink.cs | Effects | data | 4개 — float3 entryWorld, exitWorld / float entryRadius, remaining | no |
| RedBullGimmickConfig | Effects/RedBullGimmickConfig.cs | Effects | config-singleton | 6개 — float redbullSpawnInterval, redbullLifetime, lastRunAttackSpeedMul, lastRunDuration, lastRunDamageFraction / int redbullMaxActive | no |
| Resignation | Effects/Resignation.cs | Effects | data | int2 cell | no |
| ShieldCastState | Effects/ShieldCastState.cs | Effects | data | 6개 — float range, cooldownDuration, cooldownRemaining, amount / int targetCount / ShieldTargetFilter filter | no |
| ShieldGrantedEventsSingleton | Effects/ShieldGrantedEvents.cs | Effects | channel-singleton | NativeQueue&lt;ShieldGrantedEvent&gt; queue | no |
| StackModifierApplyEventsSingleton | Effects/Modifiers/StackModifierApplyEvents.cs | Effects | channel-singleton | NativeQueue&lt;StackModifierApplyEvent&gt; queue | no |
| StatModifierApplyEventsSingleton | Effects/Modifiers/StatModifierApplyEvents.cs | Effects | channel-singleton | NativeQueue&lt;StatModifierApplyEvent&gt; queue | no |
| TauntAttackGranted | Effects/TauntAttackGranted.cs | Effects | tag | — | no |
| TornadoField | Effects/TornadoField.cs | Effects | data | 4개 — float3 centerWorld / int tileRange / float pullSpeed, remaining | no |

### IBufferElementData (8)

| 이름 | 파일 | 맥락 | 종별 | 필드 요약 | enableable |
|---|---|---|---|---|---|
| AggroChaseCell | Effects/AggroChaseCell.cs | Effects | buffer | int dist. `[InternalBufferCapacity(0)]` | no |
| BlockingHazardCellsBuffer | Effects/BlockingHazardCellsBuffer.cs | Effects | buffer | int2 cell | no |
| CcEffect | Effects/CcEffect.cs | Effects | buffer | 6개 — CcKind kind / float3 vector / float scalar, remainingTime, tickInterval, tickTimer | no |
| DotEffect | Effects/DotEffect.cs | Effects | buffer | 6개 — DotOrigin origin / DotElement element / float scalar, tickInterval, tickTimer, remainingTime | no |
| HazardCellsBuffer | Effects/Hazard.cs | Effects | buffer | int2 cell. `[InternalBufferCapacity(9)]` (같은 파일 다중 선언) | no |
| HazardEffectsBuffer | Effects/Hazard.cs | Effects | buffer | HazardEffect effect. `[InternalBufferCapacity(2)]` (같은 파일 다중 선언) | no |
| StackModifierSlot | Effects/Modifiers/StackModifierSlot.cs | Effects | buffer | 5개 — ModifierHeader header / StackKind kind / byte stackCount, maxStack, lastTriggeredStack | no |
| StatModifierSlot | Effects/Modifiers/StatModifierSlot.cs | Effects | buffer | 4개 — ModifierHeader header / StatKind stat / CombineOp op / float magnitude | no |

---

## 특이사항

1. **`BattleTimeScale` 만 맥락 폴더 밖** (`Battle/BattleTimeScale.cs`). 기대치 96 과의 +1 차이의 전부. 같은 폴더의 `BattleScaledRateManager` / `BattleSimGroup` 은 컴포넌트가 아니다.
2. **enableable 은 `ModifierStatsDirty` 단 1개** (`IComponentData, IEnableableComponent`). Add 시 기본 disabled 로 붙고 `ModifierApplySystem` 이 켠다.
3. **한 파일 다중 선언 6곳**: `Combat/PatrolSpawnRequest.cs`(PatrolSpawnRequest + PatrolRequestCarrier), `Combat/ThreatTable.cs`(ThreatEntry buffer + ThreatHitEvent plain + ThreatHitEventsSingleton + static ThreatTable), `Combat/Projectile/ProjectileSpawnRequest.cs`(ProjectileSpawnRequest + ProjectileSpawnOutputElement buffer), `Effects/Hazard.cs`(Hazard + HazardEffectsBuffer + HazardCellsBuffer), `Effects/Modifiers/ModifierStats.cs`(ModifierStats + ModifierStatsDirty), 그리고 모든 `*Events.cs` 파일(payload plain struct + Singleton 래퍼 쌍).
4. **queue payload struct 는 컴포넌트가 아니다** — `AttackOutputLogEvent`, `CastEvent`, `BlinkRequestEvent`, `ShieldBreakEvent`, `HazardSpawnRequest`, `StatModifierApplyEvent` 등은 plain struct 로 인벤토리에서 제외했다(래퍼 싱글턴만 계수).
5. **이름이 `*Tag` 인데 데이터가 있는 것 3개**: `DefenderClassTag`(DefenderClass value), `FactionTag`(Faction value), `HitFlashTag`(float 3개). 반대로 이름에 Tag 가 없는 순수 태그: `LeapFlight`, `PatrolRequestCarrier`, `ProjectileRequestCarrier`, `TauntAttackGranted`, `PendingDeployment`, `ModifierStatsDirty`.
6. **`*Singleton` 접미사가 곧 채널을 뜻하지 않는다** — `DefenderFieldSingleton` / `FlowFieldSingleton` / `HazardSingleton` / `ObstacleSingleton` 은 NativeArray·HashMap·HashSet 을 든 월드 상태 싱글턴이고, `PickupSpawnState` 는 접미사 없이 싱글턴이다(`GetSingletonRW<PickupSpawnState>` 확인).
7. **거대 컴포넌트 2개**: `ProjectileState`(37 필드) / `ProjectileSpawnRequest`(33 필드) — request→state 로 거의 1:1 복사되는 쌍이라 필드가 양쪽에 이중화돼 있다. 다음은 `DcTriggerSlot`(27 필드 buffer).
8. **Movement 맥락은 버퍼 0개**, IComponentData 도 4개뿐(태그 1 · 채널 1 · 데이터 2)으로 가장 얇다. 이동 관련 상태 상당수가 Effects(`FlowFieldSingleton`, `PatrolStep`)·Combat 에 있다.
9. `Effects/MeteorBarrageRequest.cs` 의 `MeteorBarrageRequest` 는 의도적으로 plain struct(파일 주석 명시) — 컴포넌트 아님. `Effects/Modifiers/ModifierTypes.cs` 의 `ModifierHeader` 도 두 Slot 에 임베딩되는 plain struct.
10. `ISharedComponentData` · `ICleanupComponentData` · `ICleanupBufferElementData` 구현체는 `Assets/_Project/Scripts` 전체에 **0개**.
