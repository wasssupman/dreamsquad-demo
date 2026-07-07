# 0. Schema And Wiring

## Purpose

Capture the match context that recent specs introduced but the original battle
log does not record.

## Change Scope

- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
- `Assets/_Project/Scripts/Logging/BattleLogger.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs`
- `Assets/_Project/Scripts/Core/DraftController.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## Implementation

- Add `match`, `entry`, `squad`, `dreamstones`, `dreamcatcher`,
  `blocking_hazards`, `kills`, and `score_events`.
- Record `matchSeed` plus derived map/wave/visual seeds.
- Record entry mode for draft, squad, and test-mode matches.
- Record squad unit ids/names and equipped dreamstone ids/effects.
- Record dreamcatcher deck ids, offers, and picks.
- Record blocking hazard spawn/reject/destroy events.
- Replace reflection score write with a logger API.

## Completion Criteria

- C# compile succeeds.
- `rg` shows no remaining reflection write to `BattleLogger.currentEntry`.
- Existing BattleLog fields remain present and unchanged.
