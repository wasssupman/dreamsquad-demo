---
name: unity-feature-wiring
description: Use when adding a Unity MonoBehaviour that requires a scene GameObject, SerializeField reference assignment (e.g. a battle view pool's `_driver` → `BattleDriver` in `BattleCoreScene`), core-event subscription lifecycle (`BattleDriver.Subscribe`/`Unsubscribe`), or any setup that must exist in the loaded scene for the feature to run — prevents marking work complete while scene integration is still pending.
---

# Unity Feature Wiring

## Overview

A Unity feature is "done" only when **(1) code compiles, (2) scene is wired, (3) it runs in Play mode**. Skipping any step leaves the feature silently broken. UnityMCP can do almost all scene wiring — defer to the user only for tasks that genuinely require manual input (e.g. picking a Spine skin name from a visual preview).

**Core principle:** If a view pool's `_driver` (→ `BattleDriver`) is null at runtime, it never subscribes to core events and the feature does not exist — regardless of what the code says.

**전투 씬** = `Assets/_Project/Scenes/BattleCoreScene.unity`(루트 `BattleDriver` 오브젝트 + `BattleCoreUnity/View/Core*` 뷰 풀·`Hud/`·`Input/` 컴포넌트). 전투 코어(`Scripts/BattleCore/`)는 순수 C# 이라 씬 배선 대상이 아니다 — 배선은 전부 Unity 층(드라이버·뷰 풀·입력의 `SerializeField`)에서 일어난다. 에디터 진입 = 메뉴 `Wassup/BattleCore/씬 열기 (BattleCoreScene)`.

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
- A view pool / presenter that subscribes to core events (`_driver.Subscribe(ViewOrder.X, …)` in `OnEnable`, `Unsubscribe` in `OnDisable`) — needs its `_driver` wired
- Native allocation owned by a scene component (e.g. `BattleDriver` 의 `GeneratedMap` `Allocator.Persistent`) that must be disposed
- `AddComponent<T>()` at runtime (needs explicit Play/configure order)
- `Shader.Find` at runtime (needs Always Included Shaders OR SerializeField Material override)
- Scene-level event wiring (Button.onClick, UnityEvent subscribers)

## Scene Wiring Workflow (mandatory)

1. **Identify every scene-side setup the feature needs** before writing code. Write it down. Example:
   - New GameObject with `CoreVfxSpawner` in `BattleCoreScene`
   - Its `_driver` SerializeField → the scene's `BattleDriver` (plus sibling refs such as `_units` → `CoreUnitViewPool`, `_projectiles` → `CoreProjectileViewPool`)
   - Its prefab slots (`_healAppliedPrefab` …)
   - SaveScene so YAML persists the references

2. **Do the wiring via UnityMCP, not user handoff:**

   | Need | Tool |
   |---|---|
   | Create GO + add component | `mcp__UnityMCP__manage_gameobject action=create components_to_add=[...]` |
   | Set SerializeField (public) | `mcp__UnityMCP__manage_components action=set_property` |
   | Set SerializeField (private) | `mcp__UnityMCP__execute_code` + reflection (`BindingFlags.Instance \| BindingFlags.NonPublic`) |
   | Save scene | `execute_code` → `EditorSceneManager.SaveScene(scene)` (must exit Play first) |
   | Verify field populated | `grep '_fieldName: {fileID:' Assets/_Project/Scenes/BattleCoreScene.unity` — fileID must be non-zero |

3. **Verify the wiring in the saved YAML** — grep the scene file for the field name. A field missing entirely or with `{fileID: 0}` means the ref is null.

4. **Play mode verification** — refresh Unity, enter Play, trigger the feature's spawn path, watch for the visual/behavioural outcome + console errors. Exit Play. Only then mark complete.

## Private SerializeField via execute_code

The one wiring operation that's non-obvious. Template:

```csharp
var target = UnityEngine.Object.FindAnyObjectByType<Wassup.BattleCoreUnity.View.CoreVfxSpawner>(
    UnityEngine.FindObjectsInactive.Include);
var value = UnityEngine.Object.FindAnyObjectByType<Wassup.BattleCoreUnity.BattleDriver>(
    UnityEngine.FindObjectsInactive.Include);
var field = typeof(Wassup.BattleCoreUnity.View.CoreVfxSpawner).GetField("_driver",
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

3. **Scene YAML diff missing the new field** — if `grep fieldName BattleCoreScene.unity` returns empty, the component instance was serialized before the field existed. Unity won't retroactively add fields to saved prefab/scene instances. Fix: set the field programmatically + SaveScene.

4. **Committing with the new component not in scene** — commit diff includes +scene.unity lines but the new component isn't among them. Scene YAML names a MonoBehaviour by its script **guid**, not its class name: take the guid from `<Component>.cs.meta` and `grep <guid> BattleCoreScene.unity` before commit.

5. **Event subscription half-wired** — `_driver.Subscribe(...)` in `OnEnable` but no `_driver.Unsubscribe(handler)` in `OnDisable` (or the reverse). The driver keeps a dead handler across disable/enable and the pool receives every event twice. Also: subscribing with the wrong `ViewOrder` constant — body-anchored effects must come after `ViewOrder.Unit`.

6. **Native allocation half-disposed** — a scene component owning an `Allocator.Persistent` container must dispose it on every teardown path. 모범: `BattleDriver.TeardownStage()`(맵 재빌드 · 실패 경로)와 `OnDestroy() => TeardownStage()` 가 같은 한 곳을 지난다.

## Quick Reference — Wiring Checklist

Before marking a Unity MonoBehaviour feature complete:

- [ ] Code compiles (0 errors, 0 warnings about the new code)
- [ ] Scene YAML: new component present (`grep <script guid from .cs.meta> Assets/_Project/Scenes/BattleCoreScene.unity`)
- [ ] Scene YAML: every SerializeField reference has `fileID: <non-zero>` (`grep <fieldName> BattleCoreScene.unity`) — `_driver` first
- [ ] Core-event subscription: `Subscribe(ViewOrder.X, handler)` in `OnEnable` + `Unsubscribe(handler)` in `OnDisable`
- [ ] Native allocation (if any): `IsCreated` check + `Dispose()` on the single teardown path, reached from `OnDestroy`
- [ ] Runtime `AddComponent` → explicit `.Play()` after module configure
- [ ] Material created at runtime → `sharedMaterial` + `Destroy(mat)` in OnDestroy
- [ ] Shader.Find → `SerializeField Material override` slot for build safety
- [ ] Play mode: feature triggered, visible/observable outcome, 0 console errors
- [ ] Scene saved

## Real-World Incident (why this skill exists)

(옛 전투 시절 이력) Phase 8 §12 VFX: 4 Spawn methods coded, the old battle gateway wired, committed `43aa33e`. User plays → no VFX. Root cause: the VfxSpawner GameObject was never added to the scene and the gateway's `vfxSpawner` field was never wired. Rationalizations used at the time: "Unity 세션 드랍이라 검증 불가", "compile OK", "사용자가 씬 wiring 해줘야 함". All three were wrong — UnityMCP was available to do the wiring automatically, and Play verification should have gated the commit.

Same mistake nearly repeated in Phase 8 Spine (only avoided because user manually wired SpineDefenderPool after the fact). The pattern repeats without a forcing function — this skill is that function.
