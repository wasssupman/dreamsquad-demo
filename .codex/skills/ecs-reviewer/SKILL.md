---
name: ecs-reviewer
description: Review Unity Hybrid ECS / DOTS / Entities 6.x code, specs, plans, and architecture. Use when Codex is asked to critique or validate Unity ECS changes, especially hybrid MonoBehaviour + ECS boundaries, ISystem/SystemAPI usage, EntityCommandBuffer structural changes, DynamicBuffer/NativeQueue event channels, Burst/job compatibility, lifecycle/disposal, baking/authoring conversion, or project-specific ECS context ownership rules.
---

# ECS Reviewer

## Purpose

Review Unity Hybrid ECS work as an architecture and correctness critic. Prioritize bugs, lifecycle leaks, broken context boundaries, unsafe structural changes, Burst/job incompatibility, and migration risks over style.

## Workflow

1. Identify the project's actual Unity and ECS package versions from `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, and `Packages/packages-lock.json` before relying on memory. For `wassup`, target Entities 6.4.0 unless the manifest changes.
2. Read local project rules first: `AGENTS.md` if present, otherwise `CLAUDE.md`, then `docs/TRD.md` and the relevant `docs/spec/**` files.
3. Inspect the changed or reviewed files with line numbers. For plans/specs, also inspect nearby existing code that the plan claims to modify.
4. Review against the checklist in `references/hybrid-ecs-review-checklist.md`.
5. Return findings first, grouped by severity. Include file/line references for local artifacts and explicitly mark assumptions.

## Review Priorities

Use this severity model:

- **CRITICAL**: compile break, deterministic runtime failure, native container leak across play sessions/world teardown, infinite loop/reentrant event processing, context boundary violation that corrupts simulation state.
- **HIGH**: likely race/order bug, missed buffer clear, unmanaged/managed misuse that prevents Burst/jobs or causes GC pressure in hot paths, unsafe migration order, missing lifecycle ownership.
- **MEDIUM**: incomplete tests, unclear ownership policy, future data migration trap, performance issue that is not yet catastrophic.
- **LOW**: naming, documentation clarity, minor ergonomics.

## Hybrid ECS Defaults

Prefer these defaults unless local project rules override them:

- Keep authoring data in ScriptableObject/GameObject/Prefab land; keep runtime simulation data in unmanaged ECS components.
- Use `ISystem`, `SystemAPI`, `IJobEntity`/`IJobChunk`, and Burst-compatible structs for hot simulation code.
- Use `EntityCommandBuffer` for structural changes during iteration or jobs.
- Use `DynamicBuffer<T>` for per-entity incoming events and `NativeQueue<T>`/singleton channels for world-level events.
- Keep UnityEngine objects, ParticleSystem, Spine, UI, pooled views, and prefab references outside Burst ECS components; bridge them through presentation systems or MonoBehaviours.
- Require explicit create/clear/drain/dispose policy for every native container and event channel.

## Project-Specific Handling

For the `wassup` project, treat these as hard constraints when present in local docs:

- `BattleBridge` is the only MonoBehaviour-to-ECS gateway.
- Battle simulation only is ECS; UI, input, scene state, logging, and visual presentation remain MonoBehaviour/ScriptableObject.
- ECS contexts are Units, Movement, Combat, and Effects. Components are written only by their owning context.
- Cross-context writes happen through buffers or NativeQueue singleton channels, not direct component mutation.
- Prefer `ISystem`; avoid `SystemBase` unless managed references are truly required.
- Do not introduce SubScene workflows unless the project docs are intentionally changed.
- Do not add speculative interfaces or manager singletons.

## Output Shape

For review output:

1. Start with findings, not a summary.
2. Group by `CRITICAL`, `HIGH`, `MEDIUM`, `LOW`.
3. Each finding should include: risk, evidence, impact, and concrete correction.
4. Add “Open Questions” only for decisions that cannot be inferred from local docs/code.
5. End with a short “Residual Risk / Test Gaps” note.

For methodology output:

- State the actual package versions found and reject stale non-6.x assumptions when the manifest targets 6.x.
- Separate official Unity rules from community-informed heuristics.
- Link sources if web research was used.
