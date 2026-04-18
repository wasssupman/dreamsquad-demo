---
name: unity-feature-wiring
description: Use when adding a Unity MonoBehaviour that requires a scene GameObject, SerializeField reference assignment, NativeQueue singleton lifecycle, or any setup that must exist in the loaded scene for the feature to run — prevents marking work complete while scene integration is still pending.
---

# Unity Feature Wiring

## Overview

A Unity feature is "done" only when **(1) code compiles, (2) scene is wired, (3) it runs in Play mode**. Skipping any step leaves the feature silently broken. UnityMCP can do almost all scene wiring — defer to the user only for tasks that genuinely require manual input (e.g. picking a Spine skin name from a visual preview).

**Core principle:** If `BattleBridge.vfxSpawner` is null at runtime, the feature does not exist — regardless of what the code says.

## The Iron Law

```
"COMPILE PASSES" IS NOT "FEATURE COMPLETE"
```

A feature is complete only when you have seen it run in Play mode (or, at minimum, verified every serialized reference the feature depends on is non-null in the saved scene YAML).

**No exceptions:**
- Unity session unavailable → WAIT or queue wiring for when it returns. Never skip.
- User says "진행해" → does NOT authorize skipping verification.
- Commit pressure → never commit before runtime verification.
- "Just SerializeField, user can assign" → NO. UnityMCP can assign it.

## When to Use

Invoke whenever the implementation touches any of these:

- `[SerializeField]` on a new field in a MonoBehaviour already in the scene
- New MonoBehaviour class that needs a host GameObject in the scene
- `NativeQueue` + singleton Entity pair that must be created/disposed
- `AddComponent<T>()` at runtime (needs explicit Play/configure order)
- `Shader.Find` at runtime (needs Always Included Shaders OR SerializeField Material override)
- Scene-level event wiring (Button.onClick, UnityEvent subscribers)

## Scene Wiring Workflow (mandatory)

1. **Identify every scene-side setup the feature needs** before writing code. Write it down. Example:
   - New GameObject `VfxSpawner` at scene root
   - `BattleBridge.vfxSpawner` SerializeField referencing it
   - SaveScene so YAML persists the reference

2. **Do the wiring via UnityMCP, not user handoff:**

   | Need | Tool |
   |---|---|
   | Create GO + add component | `mcp__UnityMCP__manage_gameobject action=create components_to_add=[...]` |
   | Set SerializeField (public) | `mcp__UnityMCP__manage_components action=set_property` |
   | Set SerializeField (private) | `mcp__UnityMCP__execute_code` + reflection (`BindingFlags.Instance \| BindingFlags.NonPublic`) |
   | Save scene | `execute_code` → `EditorSceneManager.SaveScene(scene)` (must exit Play first) |
   | Verify field populated | `grep 'fieldName: {fileID:' Scene.unity` — fileID must be non-zero |

3. **Verify the wiring in the saved YAML** — grep the scene file for the field name. A field missing entirely or with `{fileID: 0}` means the ref is null.

4. **Play mode verification** — refresh Unity, enter Play, trigger the feature's spawn path, watch for the visual/behavioural outcome + console errors. Exit Play. Only then mark complete.

## Private SerializeField via execute_code

The one wiring operation that's non-obvious. Template:

```csharp
var target = UnityEngine.Object.FindAnyObjectByType<Wassup.Bridge.BattleBridge>(
    UnityEngine.FindObjectsInactive.Include);
var value = UnityEngine.Object.FindAnyObjectByType<Wassup.Presentation.VfxSpawner>(
    UnityEngine.FindObjectsInactive.Include);
var field = typeof(Wassup.Bridge.BattleBridge).GetField("vfxSpawner",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
field.SetValue(target, value);
UnityEditor.EditorUtility.SetDirty(target);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(target.gameObject.scene);
return "wired+saved";
```

Must exit Play mode first (`manage_editor action=stop`) — SaveScene is editor-only.

## Red Flags — STOP and Complete the Wiring

These thoughts mean the feature is not actually done:

- "사용자가 Inspector에서 연결하면 됨"
- "Unity session이 down이라 일단 코드만"
- "Compile OK니까 커밋해도 되겠다"
- "SerializeField는 수동 할당이 원칙"
- "다음에 Play 해볼 때 확인하자"
- "Task를 completed로 마킹하고 다음 단계로"
- "Play 검증은 P8-10 같은 별도 태스크로 분리"

Each of these = incomplete feature. Do the wiring now.

## Rationalization Table

| Excuse | Reality |
|---|---|
| "Unity is unavailable" | Queue the wiring steps. Execute when session returns, before claiming complete. |
| "Compile passes" | Compile ≠ runtime. SerializeField null → NullReferenceException or silent no-op. |
| "SerializeField needs Inspector" | `execute_code` + reflection can set any private SerializeField. |
| "User can do it faster" | User doing it = you forgot to do it. The tool is there. |
| "It's a separate 'verification' task" | Verification tasks that precede 'complete' status must gate commit too. |
| "Scene wiring is not code work" | In Unity, scene is code. YAML serialization is literally code. |
| "I'll fix it if it breaks" | It already breaks silently. User reports it. You re-do the work. |

## Common Mistakes

1. **Runtime `AddComponent<ParticleSystem>()` without explicit `ps.Play()`** after module configuration. `playOnAwake=true` fires with defaults before your module changes land. Fix: always call `ps.Play()` after full configure.

2. **`renderer.material = x`** inside a Spawn helper — creates a new Material instance every call. Use `sharedMaterial` and clean up on OnDestroy.

3. **Scene YAML diff missing the new field** — if `grep fieldName scene.unity` returns empty, the BattleBridge instance was serialized before the field existed. Unity won't retroactively add fields to saved prefab/scene instances. Fix: set the field programmatically + SaveScene.

4. **Committing with VfxSpawner not in scene** — commit diff includes +scene.unity lines but the new component GameObject isn't among them. Always `grep <ComponentName>` scene YAML before commit.

5. **NativeQueue lifecycle half-wired** — creating in `EnsureQueriesAndQueues` but forgetting Dispose in `OnDestroy`. Memory leak per Play/Stop cycle. Check: every Dispose for peer queues must have a matching line for the new one.

## Quick Reference — Wiring Checklist

Before marking a Unity MonoBehaviour feature complete:

- [ ] Code compiles (0 errors, 0 warnings about the new code)
- [ ] Scene YAML: new GameObject + component present (`grep <ComponentName> Scene.unity`)
- [ ] Scene YAML: every SerializeField reference has `fileID: <non-zero>` (`grep <fieldName> Scene.unity`)
- [ ] NativeQueue lifecycle: `IsCreated` check + `Dispose()` present in both `TeardownCurrentBattle` AND `OnDestroy`
- [ ] Runtime `AddComponent` → explicit `.Play()` after module configure
- [ ] Material created at runtime → `sharedMaterial` + `Destroy(mat)` in OnDestroy
- [ ] Shader.Find → `SerializeField Material override` slot for build safety
- [ ] Play mode: feature triggered, visible/observable outcome, 0 console errors
- [ ] Scene saved

## Real-World Incident (why this skill exists)

Phase 8 §12 VFX: 4 Spawn methods coded, BattleBridge wired, committed `43aa33e`. User plays → no VFX. Root cause: VfxSpawner GameObject never added to scene, BattleBridge.vfxSpawner field never wired. Rationalizations used at the time: "Unity 세션 드랍이라 검증 불가", "compile OK", "사용자가 씬 wiring 해줘야 함". All three were wrong — UnityMCP was available to do the wiring automatically, and Play verification should have gated the commit.

Same mistake nearly repeated in Phase 8 Spine (only avoided because user manually wired SpineDefenderPool after the fact). The pattern repeats without a forcing function — this skill is that function.
