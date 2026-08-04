> unit 8 부속 — 컴포넌트 전수 인벤토리(기계 추출 2026-08-04, HEAD 기준 재검수). 매핑 규칙은
> [m1_blueprint_data_mapping.md](m1_blueprint_data_mapping.md) 본문이 소유한다.

# ECS 而댄룷?뚰듃 ?꾩닔 ?몃깽?좊━ ??`Assets/_Project/Scripts/Battle/`

異붿텧 湲곗?: HEAD (master, 8786f91a ?쒖젏 ?뚰겕?몃━ clean) 쨌 `IComponentData` **97**媛?/ `IBufferElementData` **21**媛?
寃쎈줈??`Assets/_Project/Scripts/Battle/` ?댄븯 ?곷?寃쎈줈. `ISharedComponentData` / `ICleanupComponentData` 援ы쁽泥대뒗 **0媛?*(???꾨줈?앺듃 grep ?뺤씤).

## 珥앷퀎

| 留λ씫 | IComponentData | 洹몄쨷 tag | channel-singleton | config-singleton | data | IBufferElementData |
|---|---|---|---|---|---|---|
| (root, 留λ씫 ?놁쓬) | 1 | 0 | 0 | 1 | 0 | 0 |
| Units | 22 | 4 | 6 | 0 | 12 | 5 |
| Movement | 4 | 1 | 1 | 0 | 2 | 0 |
| Combat | 30 | 5 | 9 | 0 | 16 | 8 |
| Effects | 40 | 2 | 11 | 9 | 18 | 8 |
| **?⑷퀎** | **97** | 12 | 27 | 10 | 48 | **21** |

湲곕?移??議? IBufferElementData 21 = 湲곕? 21 **?쇱튂**. IComponentData 97 = 湲곕? 96 **+1** ??珥덇낵遺꾩? `BattleTimeScale`(`Battle/` 猷⑦듃, 4媛?留λ씫 ?대뜑 以??대뵒?먮룄 ?놁쓬). 4媛?留λ씫 ?대뜑留??몃㈃ ?뺥솗??96?대떎.

`channel-singleton` 27媛쒕뒗 CLAUDE.md 媛 紐낆떆??"?꾩옱 ?댁쁺 以묒씤 NativeQueue 梨꾨꼸 27媛?? ?섍? ?쇱튂?쒕떎.

醫낅퀎 洹쒖튃: `tag` = ?꾨뱶 0 쨌 `data` = per-entity ?곹깭 쨌 `channel-singleton` = `NativeQueue<T> queue` ?⑥씪 ?꾨뱶 ?깃???쨌 `config-singleton` = 鍮????깃???SO ?ㅼ젙 ?먮뒗 ?붾뱶 怨듭쑀 ?곹깭; ?꾩옄???꾨뱶 ?붿빟??`world-state` ?쒓린) 쨌 `buffer` = `IBufferElementData`.

---

## (root) ??留λ씫 ?대뜑 諛?
| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| BattleTimeScale | BattleTimeScale.cs | ??(root) | config-singleton | float Value | no |

---

## Units

### IComponentData (22)

| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| AttackUnitTag | Units/AttackUnitTag.cs | Units | tag | ??| no |
| AwakeningReward | Units/AwakeningReward.cs | Units | data | int value | no |
| DamageNumberEventsSingleton | Units/DamageNumberEventsSingleton.cs | Units | channel-singleton | NativeQueue&lt;DamageNumberEvent&gt; queue | no |
| DeadTag | Units/DeadTag.cs | Units | tag | ??| no |
| DefenderClassTag | Units/DefenderClassTag.cs | Units | data | DefenderClass value | no |
| DefenderDeathEventsSingleton | Units/DefenderDeathEventsSingleton.cs | Units | channel-singleton | NativeQueue&lt;DefenderDeathEvent&gt; queue | no |
| DefenderTile | Units/DefenderTile.cs | Units | data | int2 cell | no |
| DefenderUnitTag | Units/DefenderUnitTag.cs | Units | tag | ??| no |
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
| PendingDeployment | Units/PendingDeployment.cs | Units | tag | ??| no |
| ShieldBreakEventsSingleton | Units/ShieldBreakEvent.cs | Units | channel-singleton | NativeQueue&lt;ShieldBreakEvent&gt; queue | no |
| SimEntityId | Units/SimEntityId.cs | Units | data | int value | no |
| SummonedBy | Units/SummonedBy.cs | Units | data | Entity owner | no |

### IBufferElementData (5)

| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| DamagedCounter | Units/DamagedCounter.cs | Units | buffer | 9媛???int instanceId / ushort period, counter / DcPayloadKind payload / float magnitude / int tileRange, aoeDataIndex / DcGateKind gate / float gateValue. `[InternalBufferCapacity(2)]` | no |
| IncomingDamage | Units/IncomingDamage.cs | Units | buffer | float amount, Entity source | no |
| IncomingHeal | Units/IncomingHeal.cs | Units | buffer | float amount | no |
| IncomingShield | Units/IncomingShield.cs | Units | buffer | Entity source, float amount | no |
| ShieldSlot | Units/ShieldSlot.cs | Units | buffer | Entity source, float value | no |

---

## Movement

### IComponentData (4)

| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| BlinkRequestEventsSingleton | Movement/BlinkRequestEvents.cs | Movement | channel-singleton | NativeQueue&lt;BlinkRequestEvent&gt; queue | no |
| PastGoalTag | Movement/PastGoalTag.cs | Movement | tag | ??| no |
| PathFollowState | Movement/PathFollowState.cs | Movement | data | float speed | no |
| PatrolAnchor | Movement/PatrolAnchor.cs | Movement | data | int2 cell, int tileRadius | no |

### IBufferElementData (0)

?놁쓬 ??Movement 留λ씫? 踰꾪띁瑜??뚯쑀?섏? ?딅뒗??

---

## Combat

### IComponentData (30)

| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| AggroAttackProfile | Combat/AggroAttackProfile.cs | Combat | data | float damage, cooldown, range | no |
| AttackOutputLogEventsSingleton | Combat/AttackOutputLogEvent.cs | Combat | channel-singleton | NativeQueue&lt;AttackOutputLogEvent&gt; queue | no |
| AttackState | Combat/AttackState.cs | Combat | data | 9媛???float range, cooldownDuration, cooldownRemaining / int attackTargetCount, targetMask / float hitDelaySec, hitDelayRemaining / float2 committedDirection / byte hasCommittedDirection | no |
| BombLauncherState | Combat/BombLauncherState.cs | Combat | data | 10媛???int landingTiles, aoeTileRange, aoeTargetCap / float travelSec, fuseSec, arcHeight, dmgBombDamage, sleepSec, stunSec / Random rng | no |
| BossLeapVisualEventsSingleton | Combat/BossLeapVisualEvents.cs | Combat | channel-singleton | NativeQueue&lt;BossLeapVisualEvent&gt; queue | no |
| BossTag | Combat/BossTag.cs | Combat | tag | ??| no |
| CastEventsSingleton | Combat/CastEvents.cs | Combat | channel-singleton | NativeQueue&lt;CastEvent&gt; queue | no |
| DcTriggerFiredEventsSingleton | Combat/DcTriggerFiredEvents.cs | Combat | channel-singleton | NativeQueue&lt;DcTriggerFiredEvent&gt; queue | no |
| DefenderCcData | Combat/DefenderCcData.cs | Combat | data | 8媛?float ??knockbackDistance, knockbackDuration, onPlacePushDistance, onPlacePushDuration, onPlacePushRadius, sleepOnHitSec, knockupOnHitSec, knockupVisualHeight | no |
| EnemyAiState | Combat/EnemyAiState.cs | Combat | data | AiState value (Marching/Engaging/Chasing/Standoff) | no |
| EnemyBehavior | Combat/EnemyBehavior.cs | Combat | data | EnemyTargetMode targetMode, EngageMovement engageMovement | no |
| EnemyTargetFilter | Combat/EnemyTargetFilter.cs | Combat | data | int classMask, priorityClass | no |
| FocusTarget | Combat/FocusTarget.cs | Combat | data | Entity current | no |
| FrontmostAttackLock | Combat/FrontmostAttackLock.cs | Combat | data | 4媛???bool active, targetIsPriority / Entity target / float damageMulSnapshot | no |
| KnockupVisualEventsSingleton | Combat/KnockupVisualEvents.cs | Combat | channel-singleton | NativeQueue&lt;KnockupVisualEvent&gt; queue | no |
| LeapFlight | Combat/LeapFlight.cs | Combat | tag | ??| no |
| NextAttackDoubleFire | Combat/NextAttackDoubleFire.cs | Combat | data | int charges | no |
| PatrolSpawnRequest | Combat/PatrolSpawnRequest.cs | Combat | data | 4媛???Entity owner / int2 ownerCell / int patrolDataIndex, leashTileRadius | no |
| PatrolRequestCarrier | Combat/PatrolSpawnRequest.cs | Combat | tag | ??(媛숈? ?뚯씪 ?ㅼ쨷 ?좎뼵) | no |
| ProjectileHitEventsSingleton | Combat/Projectile/ProjectileHitEventsSingleton.cs | Combat | channel-singleton | NativeQueue&lt;ProjectileHitEvent&gt; queue | no |
| ProjectileRef | Combat/Projectile/ProjectileRef.cs | Combat | data | 11媛???int dataIndex, impactTileRange / float speed, hitThreshold, visualScale, splashRadius, splashDamageMul, arcHeight / OnHitEffectType onHitEffect / MovementKind movement / PayloadKind payload | no |
| ProjectileRequestCarrier | Combat/Projectile/ProjectileRequestCarrier.cs | Combat | tag | ??| no |
| ProjectileSpawnRequest | Combat/Projectile/ProjectileSpawnRequest.cs | Combat | data | 33媛?????? MovementKind movement, PayloadKind payload, float3 origin/impact, float damage, speed, Entity target/owner/priorityTarget, byte ccKind/bombType, int bounceRemaining, ProjectileTargetFaction targetFaction | no |
| ProjectileState | Combat/Projectile/ProjectileState.cs | Combat | data | 37媛?????? MovementKind movement, PayloadKind payload, float damage, elapsed, flightTime, float3 origin/impact/control1/control2/prevPos, Entity target/owner, int pierceRemaining, bounceRemaining | no |
| ProjectileTag | Combat/Projectile/ProjectileTag.cs | Combat | tag | ??| no |
| SummonerState | Combat/SummonerState.cs | Combat | data | 4媛???int patrolDataIndex, leashTileRadius / Entity current / bool hasSummonedOnce | no |
| ThreatHitEventsSingleton | Combat/ThreatTable.cs | Combat | channel-singleton | NativeQueue&lt;ThreatHitEvent&gt; queue (媛숈? ?뚯씪??ThreatEntry 踰꾪띁 + static ThreatTable) | no |
| UltimateLeapState | Combat/UltimateLeapState.cs | Combat | data | 6媛???float remaining, slamDamage / int2 landingCell / float3 landingWorld / int slamTileRange, projectileDataIndex | no |
| UltimateLeapVisualEventsSingleton | Combat/UltimateLeapVisualEvents.cs | Combat | channel-singleton | NativeQueue&lt;UltimateLeapVisualEvent&gt; queue | no |
| UnitAttackVisualEventsSingleton | Combat/UnitAttackVisualEventsSingleton.cs | Combat | channel-singleton | NativeQueue&lt;UnitAttackVisualEvent&gt; queue | no |

### IBufferElementData (8)

| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| AttackOutputElement | Combat/AttackOutputElement.cs | Combat | buffer | AttackOutput value | no |
| DcAttackModSlot | Combat/DcAttackModSlot.cs | Combat | buffer | 5媛???int instanceId, count, tileRange / DcAttackModKind kind / float damageMul. `[InternalBufferCapacity(2)]` | no |
| DcTriggerSlot | Combat/DcTriggerSlot.cs | Combat | buffer | 27媛?????? int instanceId, DcTriggerKind trigger, ushort period/counter, DcPayloadKind payload, float magnitude, duration, periodSeconds, CcKind ccKind, StackKind stackKind, StatKind buffStat, DcGateKind gate, int patternIndex, float slamDamage. `[InternalBufferCapacity(2)]` | no |
| EmitterInstance | Combat/Projectile/Emission/EmitterInstance.cs | Combat | buffer | PatternSpec spec, EmitterRuntime runtime, ProjectileSpawnRequest template, Entity lockedTarget. `[InternalBufferCapacity(2)]` | no |
| PathHitRecord | Combat/Projectile/PathHitRecord.cs | Combat | buffer | Entity value | no |
| PatternSlot | Combat/Projectile/Emission/PatternSlot.cs | Combat | buffer | PatternSpec spec, ProjectileSpawnRequest template, int fireCountBase. `[InternalBufferCapacity(1)]` | no |
| ProjectileSpawnOutputElement | Combat/Projectile/ProjectileSpawnRequest.cs | Combat | buffer | AttackOutput value (媛숈? ?뚯씪 ?ㅼ쨷 ?좎뼵) | no |
| ThreatEntry | Combat/ThreatTable.cs | Combat | buffer | Entity attacker, float cumulativeDamage. `[InternalBufferCapacity(4)]` | no |

---

## Effects

### IComponentData (40)

| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| AggroCapacity | Effects/AggroCapacity.cs | Effects | data | int max, held | no |
| AggroHitEventsSingleton | Effects/AggroHitEvents.cs | Effects | channel-singleton | NativeQueue&lt;AggroHitEvent&gt; queue | no |
| Aggroed | Effects/Aggroed.cs | Effects | data | Entity guardian | no |
| AllyBuffField | Effects/AllyBuffField.cs | Effects | data | 5媛???int2 centerCell / int tileRange / StatKind stat / float magnitude, remaining (+ const ushort StackId=3) | no |
| BlockingHazard | Effects/BlockingHazard.cs | Effects | data | int hazardSoIndex, float maxHp | no |
| BurnoutGimmickConfig | Effects/BurnoutGimmickConfig.cs | Effects | config-singleton | 4媛???float fatigueInterval, fatiguePerAppDuration / byte fatigueAmount, fatigueMaxStack | no |
| CcClearRequestsSingleton | Effects/CcClearEvents.cs | Effects | channel-singleton | NativeQueue&lt;CcClearRequest&gt; queue | no |
| ClockOutGimmickConfig | Effects/ClockOutGimmickConfig.cs | Effects | config-singleton | 6媛???byte resignationThreshold, meteorCount / float meteorDamage, meteorWarningSec, meteorStaggerSec / int meteorTileRange | no |
| DefenderFieldSingleton | Effects/DefenderFieldSingleton.cs | Effects | config-singleton (world-state) | 6媛???NativeArray&lt;byte&gt; walkMask, NativeArray&lt;float2&gt; flow, NativeArray&lt;int&gt; dist, int2 gridSize, float tileSize, float3 origin | no |
| DotApplyEventsSingleton | Effects/DotApplyEvents.cs | Effects | channel-singleton | NativeQueue&lt;DotApplyEvent&gt; queue | no |
| DreamCocoon | Effects/DreamCocoon.cs | Effects | data | 4媛???float remaining, mult / StatKind stat / ushort stackId (+ const Epsilon) | no |
| EnemyCcEventsSingleton | Effects/EnemyCcEvents.cs | Effects | channel-singleton | NativeQueue&lt;EnemyCcEvent&gt; queue | no |
| FatigueAccrual | Effects/FatigueAccrual.cs | Effects | data | float elapsed | no |
| FlowFieldSingleton | Effects/FlowFieldSingleton.cs | Effects | config-singleton (world-state) | 8媛???NativeArray&lt;float2&gt; flow, NativeArray&lt;int&gt; dist, NativeArray&lt;int2&gt; goals, int2 gridSize, goalCell, float tileSize, float3 origin, int version | no |
| Hazard | Effects/Hazard.cs | Effects | data | float remainingLife | no |
| HazardCastState | Effects/HazardCastState.cs | Effects | data | 8媛???float range, cooldownDuration, cooldownRemaining / int targetMask, dataIndex, footprintWidth, footprintHeight / HazardCastKind kind | no |
| HazardDestroyedEventsSingleton | Effects/HazardDestroyedEventsSingleton.cs | Effects | channel-singleton | NativeQueue&lt;HazardDestroyedEvent&gt; queue | no |
| HazardRuntimeEventsSingleton | Effects/HazardRuntimeEvents.cs | Effects | channel-singleton | NativeQueue&lt;HazardRuntimeEvent&gt; queue | no |
| HazardSingleton | Effects/HazardSingleton.cs | Effects | config-singleton (world-state) | NativeParallelMultiHashMap&lt;int2, HazardEffect&gt; cellToEffects | no |
| HazardSpawnRequestsSingleton | Effects/HazardSpawnRequest.cs | Effects | channel-singleton | NativeQueue&lt;HazardSpawnRequest&gt; queue | no |
| HeatAccrual | Effects/HeatAccrual.cs | Effects | data | float elapsed, byte stacks | no |
| LastRun | Effects/LastRun.cs | Effects | data | float remaining | no |
| MeteorBarrageRequestsSingleton | Effects/MeteorBarrageRequestsSingleton.cs | Effects | channel-singleton | NativeQueue&lt;MeteorBarrageRequest&gt; queue | no |
| ModifierStats | Effects/Modifiers/ModifierStats.cs | Effects | data | 7媛?float ??damageMul, attackSpeedMul, dmgTakenMul, regenPerSec, moveSpeedMul, damageVsCcMul, maxHealthMul | no |
| ModifierStatsDirty | Effects/Modifiers/ModifierStats.cs | Effects | tag | ??(媛숈? ?뚯씪 ?ㅼ쨷 ?좎뼵) | **yes** |
| Obstacle | Effects/Obstacle.cs | Effects | data | int2 cell, float3 worldPosition, float remainingLife | no |
| ObstacleSingleton | Effects/ObstacleSingleton.cs | Effects | config-singleton (world-state) | NativeHashSet&lt;int2&gt; blockedCells | no |
| OnsenGimmickConfig | Effects/OnsenGimmickConfig.cs | Effects | config-singleton | 5媛???float heatInterval, healPercent, lossPercent / byte flipThreshold, heatMaxStack | no |
| PatrolStep | Effects/PatrolStep.cs | Effects | data | float2 dir | no |
| Pickup | Effects/Pickup.cs | Effects | data | int2 cell, PickupKind kind, float remainingLife | no |
| PickupSpawnState | Effects/PickupSpawnState.cs | Effects | config-singleton (world-state) | NativeArray&lt;int2&gt; candidateCells, float elapsed, Random rng | no |
| PortalLink | Effects/PortalLink.cs | Effects | data | 4媛???float3 entryWorld, exitWorld / float entryRadius, remaining | no |
| RedBullGimmickConfig | Effects/RedBullGimmickConfig.cs | Effects | config-singleton | 6媛???float redbullSpawnInterval, redbullLifetime, lastRunAttackSpeedMul, lastRunDuration, lastRunDamageFraction / int redbullMaxActive | no |
| Resignation | Effects/Resignation.cs | Effects | data | int2 cell | no |
| ShieldCastState | Effects/ShieldCastState.cs | Effects | data | 6媛???float range, cooldownDuration, cooldownRemaining, amount / int targetCount / ShieldTargetFilter filter | no |
| ShieldGrantedEventsSingleton | Effects/ShieldGrantedEvents.cs | Effects | channel-singleton | NativeQueue&lt;ShieldGrantedEvent&gt; queue | no |
| StackModifierApplyEventsSingleton | Effects/Modifiers/StackModifierApplyEvents.cs | Effects | channel-singleton | NativeQueue&lt;StackModifierApplyEvent&gt; queue | no |
| StatModifierApplyEventsSingleton | Effects/Modifiers/StatModifierApplyEvents.cs | Effects | channel-singleton | NativeQueue&lt;StatModifierApplyEvent&gt; queue | no |
| TauntAttackGranted | Effects/TauntAttackGranted.cs | Effects | tag | ??| no |
| TornadoField | Effects/TornadoField.cs | Effects | data | 4媛???float3 centerWorld / int tileRange / float pullSpeed, remaining | no |

### IBufferElementData (8)

| ?대쫫 | ?뚯씪 | 留λ씫 | 醫낅퀎 | ?꾨뱶 ?붿빟 | enableable |
|---|---|---|---|---|---|
| AggroChaseCell | Effects/AggroChaseCell.cs | Effects | buffer | int dist. `[InternalBufferCapacity(0)]` | no |
| BlockingHazardCellsBuffer | Effects/BlockingHazardCellsBuffer.cs | Effects | buffer | int2 cell | no |
| CcEffect | Effects/CcEffect.cs | Effects | buffer | 6媛???CcKind kind / float3 vector / float scalar, remainingTime, tickInterval, tickTimer | no |
| DotEffect | Effects/DotEffect.cs | Effects | buffer | 6媛???DotOrigin origin / DotElement element / float scalar, tickInterval, tickTimer, remainingTime | no |
| HazardCellsBuffer | Effects/Hazard.cs | Effects | buffer | int2 cell. `[InternalBufferCapacity(9)]` (媛숈? ?뚯씪 ?ㅼ쨷 ?좎뼵) | no |
| HazardEffectsBuffer | Effects/Hazard.cs | Effects | buffer | HazardEffect effect. `[InternalBufferCapacity(2)]` (媛숈? ?뚯씪 ?ㅼ쨷 ?좎뼵) | no |
| StackModifierSlot | Effects/Modifiers/StackModifierSlot.cs | Effects | buffer | 5媛???ModifierHeader header / StackKind kind / byte stackCount, maxStack, lastTriggeredStack | no |
| StatModifierSlot | Effects/Modifiers/StatModifierSlot.cs | Effects | buffer | 4媛???ModifierHeader header / StatKind stat / CombineOp op / float magnitude | no |

---

## ?뱀씠?ы빆

1. **`BattleTimeScale` 留?留λ씫 ?대뜑 諛?* (`Battle/BattleTimeScale.cs`). 湲곕?移?96 怨쇱쓽 +1 李⑥씠???꾨?. 媛숈? ?대뜑??`BattleScaledRateManager` / `BattleSimGroup` ? 而댄룷?뚰듃媛 ?꾨땲??
2. **enableable ? `ModifierStatsDirty` ??1媛?* (`IComponentData, IEnableableComponent`). Add ??湲곕낯 disabled 濡?遺숆퀬 `ModifierApplySystem` ??耳좊떎.
3. **???뚯씪 ?ㅼ쨷 ?좎뼵 6怨?*: `Combat/PatrolSpawnRequest.cs`(PatrolSpawnRequest + PatrolRequestCarrier), `Combat/ThreatTable.cs`(ThreatEntry buffer + ThreatHitEvent plain + ThreatHitEventsSingleton + static ThreatTable), `Combat/Projectile/ProjectileSpawnRequest.cs`(ProjectileSpawnRequest + ProjectileSpawnOutputElement buffer), `Effects/Hazard.cs`(Hazard + HazardEffectsBuffer + HazardCellsBuffer), `Effects/Modifiers/ModifierStats.cs`(ModifierStats + ModifierStatsDirty), 洹몃━怨?紐⑤뱺 `*Events.cs` ?뚯씪(payload plain struct + Singleton ?섑띁 ??.
4. **queue payload struct ??而댄룷?뚰듃媛 ?꾨땲??* ??`AttackOutputLogEvent`, `CastEvent`, `BlinkRequestEvent`, `ShieldBreakEvent`, `HazardSpawnRequest`, `StatModifierApplyEvent` ?깆? plain struct 濡??몃깽?좊━?먯꽌 ?쒖쇅?덈떎(?섑띁 ?깃??대쭔 怨꾩닔).
5. **?대쫫??`*Tag` ?몃뜲 ?곗씠?곌? ?덈뒗 寃?3媛?*: `DefenderClassTag`(DefenderClass value), `FactionTag`(Faction value), `HitFlashTag`(float 3媛?. 諛섎?濡??대쫫??Tag 媛 ?녿뒗 ?쒖닔 ?쒓렇: `LeapFlight`, `PatrolRequestCarrier`, `ProjectileRequestCarrier`, `TauntAttackGranted`, `PendingDeployment`, `ModifierStatsDirty`.
6. **`*Singleton` ?묐??ш? 怨?梨꾨꼸???삵븯吏 ?딅뒗??* ??`DefenderFieldSingleton` / `FlowFieldSingleton` / `HazardSingleton` / `ObstacleSingleton` ? NativeArray쨌HashMap쨌HashSet ?????붾뱶 ?곹깭 ?깃??댁씠怨? `PickupSpawnState` ???묐????놁씠 ?깃??댁씠??`GetSingletonRW<PickupSpawnState>` ?뺤씤).
7. **嫄곕? 而댄룷?뚰듃 2媛?*: `ProjectileState`(37 ?꾨뱶) / `ProjectileSpawnRequest`(33 ?꾨뱶) ??request?뭩tate 濡?嫄곗쓽 1:1 蹂듭궗?섎뒗 ?띿씠???꾨뱶媛 ?묒そ???댁쨷?붾뤌 ?덈떎. ?ㅼ쓬? `DcTriggerSlot`(27 ?꾨뱶 buffer).
8. **Movement 留λ씫? 踰꾪띁 0媛?*, IComponentData ??4媛쒕퓧(?쒓렇 1 쨌 梨꾨꼸 1 쨌 ?곗씠??2)?쇰줈 媛???뉖떎. ?대룞 愿???곹깭 ?곷떦?섍? Effects(`FlowFieldSingleton`, `PatrolStep`)쨌Combat ???덈떎.
9. `Effects/MeteorBarrageRequest.cs` ??`MeteorBarrageRequest` ???섎룄?곸쑝濡?plain struct(?뚯씪 二쇱꽍 紐낆떆) ??而댄룷?뚰듃 ?꾨떂. `Effects/Modifiers/ModifierTypes.cs` ??`ModifierHeader` ????Slot ???꾨쿋?⑸릺??plain struct.
10. `ISharedComponentData` 쨌 `ICleanupComponentData` 쨌 `ICleanupBufferElementData` 援ы쁽泥대뒗 `Assets/_Project/Scripts` ?꾩껜??**0媛?*.

