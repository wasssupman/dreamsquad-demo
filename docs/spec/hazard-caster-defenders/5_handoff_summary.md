# 5. Handoff Summary

## Commit

- `606b306` feat(defenders): add hazard caster defenders

## Implemented

- Added hazard caster authoring fields to `DefenderUnitData`.
- Added `HazardCastKind`, `HazardCastState`, `HazardSpawnRequest`, and `HazardSpawnRequestsSingleton`.
- Added `HazardCastSystem` as an `ISystem` in `SimulationSystemGroup`, after `MovementSystem`.
- Added BattleBridge-owned `NativeQueue<HazardSpawnRequest>` lifecycle: create, drain, teardown, and destroy disposal.
- Added Bridge registries for zone hazard SOs and reused the existing blocking hazard SO registry.
- Added `CreateDefenderEntity` hookup from `DefenderUnitData` into unmanaged `HazardCastState`.
- Added `*_1x1` hazard variants for fire, ice, poison, and rock.
- Added 4 defender caster assets and connected them to `BattleScene` defender pool and draft catalog.
- Hazard casts enqueue `UnitAttackVisualEvent`, so caster Spine attack animations play even though caster `attackRange = 0`.
- Fixed procedural blocking hazard ParticleSystem velocity curve mode mismatch.
- Replaced Burst-system `GridMath.ChebyshevDistance(int2,int2)` calls with inline Chebyshev distance and removed direct Burst compilation from that helper.
- Added EditMode coverage for hazard caster range, cooldown, snapshot, kind/dataIndex, attack visual events, and dead-caster drain safety.

## Key Files

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- `Assets/_Project/Scripts/Battle/Effects/HazardCastKind.cs`
- `Assets/_Project/Scripts/Battle/Effects/HazardCastState.cs`
- `Assets/_Project/Scripts/Battle/Effects/HazardSpawnRequest.cs`
- `Assets/_Project/Scripts/Battle/Effects/HazardCastSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Movement/GridMath.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/MeteorResolutionSystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardPresenter.cs`
- `Assets/_Project/Tests/EditMode/HazardCasterTests.cs`
- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Data/Defenders/Defender_*Caster.asset`
- `Assets/_Project/Data/Hazards/Hazard_*_1x1.asset`

## Verified

- Unity console error count: 0 after compile.
- EditMode selected hazard/range/combat regression: 37 total, 37 passed.
- Full EditMode: 209 total, 207 passed, 0 failed, 2 skipped existing ignored modifier tests.
- PlayMode automated tests: 2 total, 2 passed.
- Console warning/error count after PlayMode test run and manual Play enter/stop: 0.
- Draft exposure fix verified: `DraftController.catalog` now includes the 4 caster defenders; `poolSize = 10` remains unchanged.
- User manual smoke confirmed: caster hazard behavior and caster attack animation are visible.

## Notes

- Hazard caster defenders are defenders; targets are `Faction.Enemy` attack units with `PathFollowState`.
- ECS runtime state stores only unmanaged values and registry index. SO/prefab references stay in `BattleBridge`.
- Dead target after request is allowed because the request carries `centerCell` snapshot.
- Dead caster before drain drops the request.
- MVP width/height is emitted as `1 x 1` from `HazardCastSystem`; authored footprint fields remain for later units but are not read by MVP system logic.
- Existing Burst console errors came from `GridMath.ChebyshevDistance(int2,int2)` direct Burst usage. The implementation now avoids it in Burst systems.
- Automated 4-caster placement smoke was blocked by the safety monitor as arbitrary editor execution; user manual smoke confirmed the behavior afterward.

## Follow-up

- Consider replacing scene-level draft catalog wiring with a shared `DefenderCatalogSO` if roster growth makes manual scene wiring brittle.
