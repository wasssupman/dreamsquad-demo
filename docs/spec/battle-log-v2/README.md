# battle-log-v2

> Status: in progress 2026-07-07

## Goal

Bring the battle JSON log back in line with the current match flow. The original
log covered draft, placements, waves, and result. Later specs added match seeds,
squad carry-in, dreamstones, dreamcatcher picks, blocking hazards, live score,
and projectile variants; this spec records the high-value missing context without
changing gameplay.

## Work Units

| # | File | Purpose |
|---|---|---|
| 0 | `0_schema_and_wiring.md` | Additive schema fields and minimal call-site wiring |

## Contracts

- Additive JSON only: existing fields keep their names and meanings.
- BattleLogger remains the only JSON writer.
- Gameplay code may pass source data to the logger, but logging must not affect
  simulation branches.
- Time fields added by this spec use battle time where available, and existing
  legacy time fields are left unchanged for compatibility.

## Follow-up

- Projectile impact / AOE hit logs need a separate pass because the event channel
  should carry actual impact information from projectile systems.
- Hazard log volume should move from per-frame `zone_apply` spam to summary plus
  sampled events.
- `kills.unit_type` is currently empty because `EnemyKilledEvent` only carries a
  position. Populate it in a later ECS payload pass or remove the field.
- `score_events` records the live kill-score stream; `result.score` remains the
  existing result formula until scoring is unified as gameplay state.
