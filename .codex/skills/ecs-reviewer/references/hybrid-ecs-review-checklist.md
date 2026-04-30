# Hybrid ECS Review Checklist

Use this reference when reviewing Unity Hybrid ECS implementations, specs, or migration plans.

## Source Baseline

Official Unity Entities 6.4 documentation establishes these baseline rules:

- `ISystem` is an unmanaged system interface; its `OnCreate`, `OnUpdate`, and `OnDestroy` methods can be Burst compiled. Use it as the default for hot ECS simulation.
- `SystemAPI` works inside supported `ISystem`/`SystemBase` contexts, provides query/singleton/buffer/component access, and uses generated cached lookups.
- Unmanaged `IComponentData` is the default component shape for performant ECS data.
- Managed components are more expensive, cannot be used in jobs or Burst, require GC, and need explicit clone/dispose thinking when they reference external resources.
- `DynamicBuffer<T>` is a resizable per-entity component; it is a good fit for per-entity event/request queues.
- `EntityCommandBuffer` defers structural changes and is the standard way to record create/destroy/add/remove/set commands safely during iteration/jobs.
- Baking separates authoring data from runtime ECS data. In hybrid projects, manual conversion through a bridge can be valid when project rules intentionally avoid SubScenes.

Community and production heuristics, consistent with Unity samples and DOTS practice:

- Keep DOTS/ECS on the simulation hot path; keep presentation, UI, asset references, animation, and one-off gameplay orchestration in GameObject/MonoBehaviour unless there is a measured need.
- Make the ECS/GameObject boundary narrow. A single bridge or a tiny set of explicit adapters is easier to test and reason about than many MonoBehaviours touching `EntityManager`.
- Design components around ownership and update frequency, not object-oriented domain nouns.
- Prefer explicit event channels over direct cross-context mutation.
- Avoid adding abstractions before there are at least two real implementations.
- Treat structural changes, native container lifetime, and update order as first-class review targets.

## Version Check

Before reviewing, verify:

- Unity editor version from `ProjectSettings/ProjectVersion.txt`.
- `com.unity.entities`, `com.unity.collections`, `com.unity.entities.graphics`, `com.unity.burst`, and `com.unity.mathematics` from `Packages/manifest.json` and `Packages/packages-lock.json`.
- Whether docs in the repo disagree with actual package versions. Flag stale architecture docs as MEDIUM or HIGH if they would mislead implementation.

## Boundary Checklist

Flag issues when:

- A MonoBehaviour other than the approved bridge accesses `EntityManager`, `World.DefaultGameObjectInjectionWorld`, or `SystemAPI`.
- UI/input/presentation code starts reading or mutating ECS components directly.
- ECS components store `GameObject`, `Transform`, `ParticleSystem`, `ScriptableObject`, Spine objects, materials, sprites, or prefabs in hot simulation state.
- A system in one context writes components owned by another context instead of enqueueing a buffer/queue request.
- A spec says “hybrid” but does not define which side owns authoring, runtime state, and presentation.

## System and Data Checklist

Review every new system for:

- `partial struct XxxSystem : ISystem` unless managed APIs are truly needed.
- `[BurstCompile]` on system callbacks and jobs when compatible.
- `RequireForUpdate<T>()` or a clear reason the system should tick every frame.
- Correct `UpdateInGroup`, `UpdateBefore`, and `UpdateAfter` ordering for producers and consumers.
- No `EntityManager.Exists/GetComponentData/SetComponentData` in Burst jobs when a lookup or `SystemAPI` path is available.
- No structural changes inside entity iteration except through `EntityCommandBuffer`.
- Native arrays/lists/queries allocated in `OnUpdate` are disposed on all paths.
- Main-thread `.Run()` use is justified; scheduled jobs update dependencies correctly.

Review every new component/buffer for:

- Unmanaged fields only unless a managed component is explicitly justified.
- Clear owner context and write policy.
- Buffer clear/drain semantics. Per-frame event buffers must be cleared exactly once after consumption.
- Stack/slot identity policy if effects can be reapplied from the same source.
- Enableable/tag component use when state is boolean and frequent add/remove would churn archetypes.

## Event Channel Checklist

For `DynamicBuffer<T>`:

- Producer and consumer order is explicit.
- Consumer clears after processing.
- Reentrant enqueue behavior is defined: same-frame processing, next-frame processing, or rejected.
- Buffer capacity and growth risk are acceptable.

For `NativeQueue<T>` singleton channels:

- The queue is created once per battle/world lifecycle or is idempotently recreated.
- Ownership of `Dispose()` is explicit and happens on battle reset, MonoBehaviour destroy, or system/world teardown.
- Multiple singleton entities cannot accumulate across restarts.
- Producers use `ParallelWriter` only when appropriate.
- Drain loops cannot enqueue into the same channel indefinitely in the same frame.

## Hybrid Presentation Checklist

Flag issues when:

- Presentation lifetime is not tied to entity lifetime or a clear destruction event.
- Visual pools are keyed by unstable entity references without cleanup on destroy/reset.
- Animation/VFX is driven by polling every entity from MonoBehaviour when an ECS event channel would be cheaper and clearer.
- A projectile or VFX prefab reference is copied into ECS data instead of represented by an integer id/registry entry owned by the bridge.
- View code becomes authoritative for simulation state.

## Migration Plan Checklist

For specs/plans, look for unsafe boundaries:

- A step removes legacy components before all producers/consumers are migrated.
- A step compiles only if a later step has also happened.
- Adapter steps duplicate effects without an idempotency guard.
- Tests are delayed until the end instead of being attached to each migration seam.
- Rollback is unclear for serialized assets, ScriptableObjects, prefab fields, or baked data.

## Test Checklist

Expect focused tests for:

- Pure stat/math calculations as EditMode tests.
- Buffer/queue drain behavior, including empty, multiple events, same-frame producer/consumer, and clear-after-consume.
- Stack/reapply/expiration edge cases.
- Death/despawn/lifecycle cleanup.
- Bridge conversion from ScriptableObject authoring data to ECS runtime components.
- PlayMode smoke for hybrid presentation when MonoBehaviour pools, VFX, or Spine views are involved.

## Useful Source Links

- Unity Entities 6.4 `ISystem`: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/systems-isystem.html
- Unity Entities 6.4 `SystemAPI`: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/systems-systemapi.html
- Unity unmanaged components: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/components-unmanaged.html
- Unity managed components: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/components-managed.html
- Unity dynamic buffers: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/components-buffer.html
- Unity EntityCommandBuffer: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/systems-entity-command-buffers.html
- Unity baking overview: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/baking-overview.html
- Unity ECS overview: https://unity.com/ecs
- Unity DOTS samples: https://github.com/Unity-Technologies/EntityComponentSystemSamples
