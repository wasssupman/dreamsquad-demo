# Claude Handoff — ECS Lifecycle Teardown Review Fix

## Context

Codex ran the `ecs-reviewer` workflow on `Assets/_Project/Scripts/Bridge/BattleBridge.cs`.

Main review findings were:

- `StopBattle()` only set `_running = false`, so ECS systems could keep ticking while `BattleBridge.Update()` stopped draining event queues.
- `OnDestroy()` disposed `NativeQueue` fields but did not first destroy the singleton entities that still held those queue structs.
- Cached `EntityQuery` fields were not explicitly disposed/reset.
- ECS docs still said there were 8 active NativeQueue channels, while the current implementation has 14.

## Implemented

- Refactored `BattleBridge.TeardownCurrentBattle()` to use shared teardown helpers.
- Added `HasLiveEntityManager()` guard for World/domain reload timing.
- Added `DestroyEcsInfrastructureEntities()` for queue/container singleton entity cleanup.
- Added `DestroyBattleEntities()` for attackers, defenders, projectiles, health bars, hazards, blocking hazards, and obstacles.
- Added `DisposeEcsInfrastructureNativeContainers()` for all `NativeQueue`, `_blockedCells`, and `_hazardCellToEffects`.
- Added `DisposeCachedQueries()` for `_aliveAttackersQuery`, `_projectileSpawnRequestQuery`, and `_projectileQuery`.
- Updated `StopBattle()` to call full teardown when the World/EntityManager is live.
- Updated `OnDestroy()` to reuse `TeardownCurrentBattle()` instead of disposing queues directly.
- Preserved the critical teardown ordering: `TeardownFlowField()` runs before broad singleton entity cleanup because `FlowFieldSingleton` owns Persistent `NativeArray` data inside component data.
- Updated `CLAUDE.md` and `.claude/agents/ecs-reviewer.md` from 8 NativeQueue channels to 14.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `CLAUDE.md`
- `.claude/agents/ecs-reviewer.md`

## Verification

- Unity script compile requested; no compile errors reported.
- `Wassup.Tests.EditMode.GeneratedMapTests`: passed.
- `Wassup.Tests.EditMode.BackgroundPropPlacerTests`: passed.
- Combined result: 14/14 EditMode tests passed.
- Console was cleared and tests were rerun. Persistent leak warning did not reappear; only Unity Test Framework result-saving logs remained.

## Important Notes

- Do not move `FlowFieldSingleton` into the generic singleton cleanup helper unless its `FlowFieldSingleton.Dispose()` path is preserved first.
- `StopBattle()` is now destructive for battle ECS state. If a future feature needs pause/resume, add a separate pause API or a battle-active singleton instead of reusing `StopBattle()`.
- `OnDestroy()` now calls `TeardownCurrentBattle()`, then disposes presentation/runtime material resources. This intentionally shares lifecycle behavior with restart/redraft teardown.
- Presentation pools still receive `EntityManager` from `BattleBridge`. This was reviewed as a medium boundary concern but was not changed in this patch.

## Follow-up

- Run a PlayMode/manual smoke pass:
  - enter Draft,
  - start Battle,
  - trigger Restart,
  - trigger Redraft,
  - stop Play Mode,
  - confirm no disposed NativeQueue, duplicated singleton, or persistent allocation warnings.
- Consider adding lifecycle tests:
  - `StopBattle_DestroysEventSingletonsAndDisposesQueues`
  - `OnDestroy_DestroysSingletonEntitiesBeforeQueueDispose`
  - `RestartBattle_DoesNotDuplicateEventSingletons`
- Optionally reduce the presentation ECS boundary later by having `BattleBridge` pass pure transform/existence snapshots to pools instead of passing `EntityManager`.

