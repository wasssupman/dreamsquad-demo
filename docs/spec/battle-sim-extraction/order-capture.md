# order-capture — `BattleSimGroup` 유효 시스템 총순서

> **자동 생성물.** `Wassup/Battle/Sim Order/Dump BattleSimGroup Order` 로 Play 중 갱신한다.
> 손으로 고치지 말 것 — 이 파일은 «지금 실제로 이 순서로 돈다» 는 사실의 기록이고,
> 골든(unit 4)과 신 sim 틱 파이프라인(M1)이 이 순서를 기준으로 삼는다.

- 시스템 수: **48**
- 순서 어트리뷰트가 **하나도 없는** 시스템: **3**

「무순서」 = `UpdateBefore`/`UpdateAfter` 를 하나도 선언하지 않은 시스템이다.
그 위치는 지금 토폴로지 정렬의 tie-break 이 정하고 있어, 시스템이 하나 추가되면
조용히 움직일 수 있다. unit 0 의 핀 대상이 바로 이 목록이다.

| # | 시스템 | UpdateAfter | UpdateBefore | 무순서 |
|---:|---|---|---|:-:|
| 0 | `LastRunSystem` | — | DamageApplicationSystem |  |
| 1 | `HazardLifetimeSystem` | — | CcApplySystem, MovementSystem |  |
| 2 | `StructureDestinationSystem` | — | MovementSystem |  |
| 3 | `AllyBuffFieldSystem` | — | ModifierApplySystem |  |
| 4 | `BossPeriodicTriggerSystem` | — | ModifierApplySystem, AggroStateSystem |  |
| 5 | `ZoneApplySystem` | HazardLifetimeSystem | CcApplySystem, ModifierApplySystem |  |
| 6 | `ObstacleLifetimeSystem` | — | MovementSystem |  |
| 7 | `DefenderFieldSystem` | — | MovementSystem |  |
| 8 | `AggroStateSystem` | — | MovementSystem |  |
| 9 | `ModifierApplySystem` | — | StatModifierTickSystem, MovementSystem |  |
| 10 | `CcApplySystem` | — | MovementSystem |  |
| 11 | `HealthDeathSystem` | — | UnitLifecycleSystem |  |
| 12 | `LethalTimerSystem` | — | DamageApplicationSystem |  |
| 13 | `FlowFieldRebuildSystem` | ObstacleLifetimeSystem | MovementSystem |  |
| 14 | `TauntAttackGrantSystem` | AggroStateSystem | AttackSystem, MovementSystem |  |
| 15 | `EnemyAiStateSystem` | TauntAttackGrantSystem | MovementSystem |  |
| 16 | `DotApplySystem` | CcApplySystem | CcDecaySystem |  |
| 17 | `PatrolFieldSystem` | — | MovementSystem |  |
| 18 | `MovementSystem` | — | — | ⚠ |
| 19 | `HazardCastSystem` | MovementSystem | AttackSystem |  |
| 20 | `AgentSeparationSystem` | MovementSystem | — |  |
| 21 | `ShieldCastSystem` | MovementSystem | — |  |
| 22 | `ResignationThresholdSystem` | — | StackModifierTickSystem |  |
| 23 | `HeatAccrualSystem` | — | DamageApplicationSystem |  |
| 24 | `PickupSpawnSystem` | — | — | ⚠ |
| 25 | `PickupConsumeSystem` | PickupSpawnSystem | — |  |
| 26 | `HitFlashSystem` | — | — | ⚠ |
| 27 | `EffectTickSystem` | MovementSystem | AttackSystem |  |
| 28 | `ProjectileMoveSystem` | MovementSystem | — |  |
| 29 | `ProjectileHitSystem` | ProjectileMoveSystem | — |  |
| 30 | `FatigueAccrualSystem` | ModifierApplySystem | — |  |
| 31 | `StatModifierTickSystem` | ModifierApplySystem | ModifierStatsAggregateSystem |  |
| 32 | `ModifierStatsAggregateSystem` | StatModifierTickSystem | — |  |
| 33 | `MaxHealthScaleSystem` | ModifierStatsAggregateSystem | — |  |
| 34 | `StackModifierTickSystem` | ModifierStatsAggregateSystem | — |  |
| 35 | `AttackSystem` | MovementSystem | — |  |
| 36 | `DamageApplicationSystem` | AttackSystem | — |  |
| 37 | `ResignationDropSystem` | DamageApplicationSystem, HealthDeathSystem | UnitLifecycleSystem |  |
| 38 | `PatrolLifecycleSystem` | DamageApplicationSystem, HealthDeathSystem | UnitLifecycleSystem |  |
| 39 | `CcClearSystem` | DamageApplicationSystem | — |  |
| 40 | `ProjectileEmitterSystem` | BossPeriodicTriggerSystem, AttackSystem | — |  |
| 41 | `BarrelExplosionSystem` | DamageApplicationSystem, ObstacleLifetimeSystem | UnitLifecycleSystem |  |
| 42 | `DreamCocoonSystem` | CcClearSystem | CcDecaySystem |  |
| 43 | `CcDecaySystem` | MovementSystem | — |  |
| 44 | `UnitLifecycleSystem` | DamageApplicationSystem | — |  |
| 45 | `HealthThresholdSystem` | DamageApplicationSystem | — |  |
| 46 | `UltimateLeapSystem` | HealthThresholdSystem | BlinkApplySystem |  |
| 47 | `BlinkApplySystem` | HealthThresholdSystem | — |  |

## 무순서 시스템 (핀 대상)

- `MovementSystem` — 현재 위치 18
- `PickupSpawnSystem` — 현재 위치 24
- `HitFlashSystem` — 현재 위치 26
