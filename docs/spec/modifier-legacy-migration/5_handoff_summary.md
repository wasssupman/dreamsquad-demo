# Modifier Legacy Migration Handoff

## Commit

See `git log` for the merge sequence: code commit + handoff docs commit.

## Implemented

- Defender and enemy attacks now use `AttackOutputElement` as the runtime hit-effect source.
- `AttackUnitData.outputs` added and enemy assets migrated to Damage outputs where they attack.
- Projectile launch stages outputs once at fire time on the attacker entity via `ProjectileSpawnOutputElement`; BattleBridge copies the snapshot to the projectile entity. `damageMul` is applied a single time at snapshot — no double-multiply.
- `StatKind.MoveSpeedMul` / `ModifierStats.moveSpeedMul` added and folded by `ModifierStatsAggregateSystem` (explicit per-stat dispatch — no bare-else fallthrough).
- Slow producers (`SlowField` skill, `SlowPulse`/`BindNearby` on-place, `HazardEffect.Slow` zone) all enqueue through `BattleBridge.EnqueueMoveSpeedMul` → `StatModifierApplyEvents` channel. `EffectSpawner.ApplySlow` removed.
- `EnemyAttackMovePause` moved to `Wassup.Battle.Movement` namespace. `MovementPauseRequestDrainSystem` (non-Burst, runs before `MovementSystem`) drains the queue and adds/updates the pause component via ECB; `MovementSystem` itself is now Burst-clean and only ticks down the pause.
- `BattleBridge` owns `MovementPauseRequest` queue lifecycle alongside the existing ECS singleton queues.

## Key Files

- `Assets/_Project/Scripts/Data/AttackUnitData.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs` (`ProjectileSpawnOutputElement` buffer)
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementPauseRequestEvents.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementPauseRequestDrainSystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStatsAggregateSystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` (legacy `ApplySlow` / dead `Apply<T>` removed)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`EnqueueMoveSpeedMul`, `MovementPauseRequest` queue lifecycle, projectile outputs handoff, `ApplySlowField` routed through channel)
- `Assets/_Project/Scripts/Data/Units/Enemy_Basic.asset`, `Enemy_Swift.asset`, `Enemy_Tanker.asset`

## Verified

- 2026-05-01: Unity compile error 0.
- 2026-05-01: EditMode tests 179 passed / 2 ignored / 0 failed (177 actually ran; 2 ignored as documented below).
- 2026-05-01: Play Mode enter/exit smoke, console error 0.

Ignored tests are pre-existing testability placeholders, not regressions:

- `ModifierFrameworkTests.StackModifier_MultiThreshold_FourToSeven_Fires_All_Crossed_Thresholds`
- `ModifierFrameworkTests.AttackOutput_AllFourKinds_EnqueueToCorrectChannels`

## Notes

- `CcKind.Slow` remains as a serialized enum value because `HazardEffect.kind` uses `CcKind`; Movement no longer consumes it as speed state. `ZoneApplySystem` translates `CcKind.Slow` zones into `StatModifierApplyEvent { stat=MoveSpeedMul, op=Multiplicative }`.
- Projectile splash remains damage-only through existing `ProjectileState.damage` / `splashDamageMul`. Non-damage projectile outputs apply to the direct hit target.
- `AttackState.damage` remains serialized/runtime compatibility data but is no longer a hit-effect fallback when no outputs buffer exists.
- Tanker and other shipping enemy SOs carry `movePauseOnAttackSec: 0` historically (verified against pre-migration `git log -p`); melee Tanker pauses zero seconds is intentional. Future enemy content with `movePauseOnAttackSec > 0` will route through the new `MovementPauseRequest` queue automatically.
- `ProjectileSpawnOutputElement` buffer is staged on the attacker entity via ECB. If the attacker dies in the same frame *after* firing but *before* `BattleBridge.DrainProjectileSpawnRequests` runs, the projectile spawn is dropped. The current systems do not produce this case (death is processed by `HealthDeathSystem` / `UnitLifecycleSystem` only after damage is applied next frame); document as a known edge case for future projectile producers.

## Review fixes applied (post-implementation)

- HIGH — Projectile outputs snapshot consistency: `damageMul` applied once at snapshot time in `AttackSystem`; `BattleBridge.SpawnProjectile` copies the buffer verbatim with no second multiplication.
- MEDIUM — Movement pause drain split: dedicated `MovementPauseRequestDrainSystem` (non-Burst) ahead of `MovementSystem`. `MovementSystem` remains `[BurstCompile]`, free of ECB.Playback.
- MEDIUM — Slow channel discipline: `EffectSpawner.ApplySlow` removed; `BattleBridge.ApplySlowField` (skill) and `ApplyOnPlaceEffect` (SlowPulse, BindNearby) both go through `EnqueueMoveSpeedMul`.
- MEDIUM — Tanker pause intent: confirmed shipping value `movePauseOnAttackSec: 0` (no regression). Recorded as Notes above.
- LOW — `ZoneApplySystem` already uses `TryGetSingleton<StatModifierApplyEventsSingleton>`; no change required.
- LOW — `AttackSystem` lookup names normalised (`buffStatsLookup`/`buffStatsDamageMul`/`buffStatsAttackSpeedMul` → `modifierStatsLookup`/`damageMul`/`attackSpeedMul`).
- LOW — `ModifierStatsAggregateSystem` uses explicit `else if` dispatch for every `StatKind` (no silent fallthrough on future enum additions).
- NIT — Internal unit-tag comment in `AttackSystem` outputs branch removed.
- NIT — `EffectSpawner.Apply<T>` private helper removed (zero callers).

## Follow-up

- Out of this commit set, intentionally:
  - `Defender_Bastion.asset`, `Defender_Bruiser.asset` (balance tuning + CC fields) — belongs to `enemy-unit-development` / future combat balance spec.
  - `Enemy_Needler.asset`, `Enemy_Rootcaster.asset`, `Enemy_Runner.asset` and their materials, `BellKnight.*` Spine assets — belongs to `enemy-unit-development` spec.
  - `Enemy_Basic_Mat.mat`, `Enemy_Swift_Mat.mat`, `Enemy_Tanker_Mat.mat` — visual material tweaks, leave to enemy-content spec.
  - `WaveA.asset` generator fields — wave generator spec follow-up.
  - Hazard prefabs/materials (`HazardVisual_*`, `BlockingHazard_*`) — `background-props` / hazard-vfx spec.
  - `docs/spec/background-props/`, `docs/spec/board-visualization/`, `docs/spec/cc-pipeline-and-obstacle/`, `docs/spec/enemy-unit-development/` — separate specs.
- `Wassup.Presentation.SpineUnitPool` / `SpineUnitView` / `ISpineUnitVisualData` / `IDefenderSpineExtras` and `HealthDeathSystem` were untracked at HEAD but already referenced by `BattleBridge.cs` / `HazardDestroyedEventTests.cs`; included in this commit purely to keep HEAD self-consistent. Their conceptual home is the `enemy-unit-development` spec — record there if needed.
- Follow-up backlog items remain in `docs/spec/README.md`.
