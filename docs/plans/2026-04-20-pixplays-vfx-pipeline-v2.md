# PixPlays VFX 파이프라인 개선 Implementation Plan v2

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 기존 `_SKELETON.prefab` 4종(Placement / Tornado / Meteor Burst+Falling / Portal) 의 내부 비주얼을 PixPlays 원소 VFX 자원으로 리스킨하고, `PixPlays/Components/Shaders/Ubershader.shadergraph` 를 우리 VFX Material 의 기반으로 흡수한다. VfxSpawner 계약 · prefab guid 불변.

**Architecture:** 원소 프리셋 Material 4종(신규) + 공용 헬퍼 2종(기존 빈 Material 재활용) 을 **PixPlays 원본 Material 복제 → 오버라이드** 방식으로 생성한다. 각 `_SKELETON.prefab` 은 파일 경로·guid 를 유지한 채 **`manage_prefabs` headless `modify_contents` 액션** 으로 내부 자식 교체. 실행 순서: **Ubershader introspection → 베이스라인 측정 → Portal 불가능 조기 확인 → Tornado 파일럿 → 티어 결정 → 나머지 3종** 의 직렬. 각 prefab 편집 전·후로 GUID assert. PixPlays MonoBehaviour 는 MonoScript 경로 기반 일괄 제거.

**Tech Stack:** Unity 6 · URP 17.3 · Shuriken 파티클 · Shader Graph · UnityMCP (`manage_asset`, `manage_prefabs` headless actions, `manage_material`, `execute_code` for editor-only C#, `manage_build` target=android, `manage_camera` screenshot, `read_console`).

**설계 문서:** `docs/plans/2026-04-20-pixplays-vfx-pipeline-design.md`
**v1 폐기:** `docs/plans/2026-04-20-pixplays-vfx-pipeline.md` — Codex 리뷰에서 invalid UnityMCP commands + incomplete PixPlays script audit + unverified Portal strategy 지적으로 re-plan. v2 가 대체.

**절대 제약:** CLAUDE.md — VfxSpawner 계약 · prefab guid/경로 불변 · PixPlays MonoBehaviour 비참조 · ECS 경계 엄수.

---

## UnityMCP 정정 레퍼런스 (v1 오용 수정)

| v1 에서 쓴 것 | 실제 스키마 |
|---|---|
| `manage_asset({action:"search_refs"})` | **없음** — `execute_code` + `AssetDatabase.GetDependencies` 로 순방향 조회, 역방향은 모든 asset guid iterate 필요 |
| `manage_editor({action:"get_state"})` | **없음** — scene 은 `manage_scene({action:"get_active"})`, 컴파일 상태는 `mcpforunity://editor_state` resource |
| `manage_asset({action:"set_properties"})` | Material 은 `manage_material({action:"set_material_shader_property"})` / `manage_material({action:"set_material_color"})` |
| `manage_gameobject({action:"reparent"})` | `manage_gameobject({action:"modify", parent:"..."})` |
| `find_gameobjects({in, componentType})` | `find_gameobjects({search_term, search_method:"by_component"})` |
| `read_console({severity:"error,warning"})` | `read_console({action:"get", types:["error","warning"]})` |
| `manage_build({platform:"Android", output:"..."})` | `manage_build({action:"build", target:"android", output_path:"..."})` |
| `manage_prefabs({action:"get_hierarchy", path:"..."})` | `manage_prefabs({action:"get_hierarchy", prefab_path:"..."})` |

**Prefab 편집의 두 경로** (v2 는 가능하면 headless 우선):
- **Headless**: `manage_prefabs({action:"modify_contents", prefab_path, create_child, delete_child, component_properties, components_to_remove})` — 스테이지 열지 않고 직접 변경.
- **Interactive**: `open_prefab_stage` → `manage_gameobject` / `manage_components` → `save_prefab_stage` → `close_prefab_stage` — 복잡한 hierarchy 재구성 시.

---

## Phase 0 — Preflight (실행 환경 검증)

### Task 0.1: Preflight — UnityMCP 연결 + 활성 인스턴스 확인

**목적:** UnityMCP 가 연결돼 있고, 이 플랜이 대상으로 하는 Unity Editor 가 활성인지 확인. 실패 시 후속 모든 task 중단.

**Step 1:** Unity 인스턴스 조회

```
mcpforunity://instances
```

Expected: 최소 1개 인스턴스(wassup 프로젝트). 여러 개면 `set_active_instance` 로 본 프로젝트 핀.

**Step 2:** `editor_state` 로 컴파일 상태 확인

```
mcpforunity://editor_state
```

Expected: `isCompiling=false`, `isPlaying=false`.

**Step 3:** 콘솔 초기 상태 기록

```
read_console({action:"get", types:["error","warning"], count:"50"})
```

기존 에러·워닝 수를 기록(baseline). 이후 task 에서 "신규 에러 0" 을 이 수치 대비로 판단.

**커밋 없음** — 조사만.

---

### Task 0.2: Preflight — 다른 세션 점유 확인

**목적:** Phase 9 맵 작업 세션이 Unity Editor 를 쥐고 있으면 안 됨. 쥐고 있으면 본 플랜 실행 중단, 사용자에게 에디터 반납 요청.

**Step 1:** 현재 활성 scene 확인

```
manage_scene({action:"get_active"})
```

**Step 2:** `Library/Temp/UnityLockfile` 유무 + 사용자 확인:

```bash
ls /Users/sy/dev/wassup/Library/EditorInstance.json 2>/dev/null
```

**Step 3:** 사용자에게 명시적 확인 요청:

> "Phase 9 맵 세션이 Unity Editor 를 사용 중인지? 사용 중이면 이 플랜 실행 보류."

**Step 4:** 사용자 OK 받은 후 다음 Task 진행. **커밋 없음**.

---

## Phase 1 — Ubershader + Material 속성 introspection

### Task 1.1: Ubershader 속성 키 추출

**목적:** Codex 지적 — Ubershader 실제 property key 이름이 misspelled 포함(`_Disolve_Texture`, `_Disolve_Scroll`, `_Color_Texture` 등) 일 수 있음. 추측 대신 실측.

**Files:**
- Read-only: `Assets/PixPlays/Components/Shaders/Ubershader.shadergraph`

**Step 1:** Ubershader property 키 덤프

```
execute_code({action:"execute", code:"""
    var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>("Assets/PixPlays/Components/Shaders/Ubershader.shadergraph");
    if (shader == null) return "SHADER_NOT_FOUND";
    int count = shader.GetPropertyCount();
    var sb = new System.Text.StringBuilder();
    for (int i = 0; i < count; i++) {
        sb.AppendLine($"{shader.GetPropertyName(i)} | {shader.GetPropertyType(i)} | {shader.GetPropertyDescription(i)}");
    }
    return sb.ToString();
"""})
```

Expected: 전체 property 목록 (name | type | description). 출력을 design doc §5.1 에 append.

**Step 2:** 콘솔 확인

```
read_console({action:"get", types:["error","warning"], count:"10"})
```

**Step 3:** Design doc 에 속성 목록 반영 + 커밋

```bash
git add docs/plans/2026-04-20-pixplays-vfx-pipeline-design.md
git commit -m "docs(vfx): append Ubershader property introspection results"
```

---

### Task 1.2: 원본 PixPlays Material 인벤토리 (4원소)

**목적:** 각 원소 "대표 Material" 을 결정. 복제 기반이므로 원본 경로 확정 필수.

**Files:**
- Read-only: `Assets/PixPlays/**/Version_URP/**` materials

**Step 1:** WindAOE / FireAOE / EarthAOE / WaterBeam URP prefab 각각의 Material 참조 덤프

```
execute_code({action:"execute", code:"""
    var paths = new [] {
        "Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab",
        "Assets/PixPlays/ElementalAOE/FireAOE/Version_URP/FireAoeVFX.prefab",
        "Assets/PixPlays/ElementalAOE/EarthAOE/Version_URP/EarthSlamSpikesAoeVFX.prefab",
        "Assets/PixPlays/ElementalBeams/WaterBeam/Version_URP/WaterBeam.prefab",
        "Assets/PixPlays/ElementalShields/WaterShield/Version_URP/WaterShield.prefab",
    };
    var sb = new System.Text.StringBuilder();
    foreach (var p in paths) {
        sb.AppendLine($"=== {p} ===");
        foreach (var dep in UnityEditor.AssetDatabase.GetDependencies(p, true)) {
            if (dep.EndsWith(".mat")) sb.AppendLine(dep);
        }
    }
    return sb.ToString();
"""})
```

Expected: 각 prefab 의 Material 의존성 목록. 원소별 "대표" Material 을 선정(일반적으로 `*Core*`, `*Main*`, `*Swirl*`, `*Ring*` 등).

**Step 2:** 선정 결과를 notepad or design doc 에 기록. **커밋 없음** (조사).

---

### Task 1.3: 원소 Material 4종 복제 생성

**목적:** 추측 대신 복제. v1 의 속성명 추측 문제 완전 회피.

**Files:**
- Create: `Assets/_Project/VFX/Materials/VFX_Uber_{Wind,Fire,Earth,Water}.mat`

**Step 1:** Task 1.2 에서 선정한 원본 Material 을 각각 복제

```
manage_asset({action:"duplicate",
    path:"<선정된 Wind Material 원본>",
    destination:"Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat"})
```

4원소 각각 반복.

**Step 2:** 각 Material 의 shader 가 Ubershader 인지 확인

```
manage_material({action:"get_material_info",
    material_path:"Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat"})
```

Expected: shader 가 `Shader Graphs/Ubershader` 계열. 아니면 원본 선정 재검토.

**Step 3:** 콘솔 확인

**Step 4:** 커밋

```bash
git add Assets/_Project/VFX/Materials/VFX_Uber_*.mat Assets/_Project/VFX/Materials/VFX_Uber_*.mat.meta
git commit -m "$(cat <<'EOF'
feat(vfx): add VFX_Uber_{Wind,Fire,Earth,Water}.mat (PixPlays 복제 기반)

원본 Material 복제로 Ubershader property 연결 자동 확보. 추측 네이밍 없음.
EOF
)"
```

---

## Phase 2 — Legacy cleanup (빈 shader 제거)

### Task 2.1: 빈 셰이더 삭제 + 커밋

**목적:** Q3 결정 — `VFX_Dissolve.shadergraph`, `VFX_Glow.shadergraph`, `New Shader Graph.shadergraph` 3개 제거.

**Step 1:** 각 셰이더의 역참조 검증 — `execute_code` 로 전체 Material 순회 후 shader 가 이들 중 하나를 가리키는지 확인

```
execute_code({action:"execute", code:"""
    var targets = new System.Collections.Generic.HashSet<string> {
        "Assets/_Project/VFX/Shaders/VFX_Dissolve.shadergraph",
        "Assets/_Project/VFX/Shaders/VFX_Glow.shadergraph",
        "Assets/_Project/VFX/New Shader Graph.shadergraph",
    };
    var targetGuids = new System.Collections.Generic.HashSet<string>();
    foreach (var p in targets) targetGuids.Add(UnityEditor.AssetDatabase.AssetPathToGUID(p));
    var mats = UnityEditor.AssetDatabase.FindAssets("t:Material");
    var refs = new System.Collections.Generic.List<string>();
    foreach (var g in mats) {
        var mp = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
        foreach (var dep in UnityEditor.AssetDatabase.GetDependencies(mp, false)) {
            if (targets.Contains(dep)) { refs.Add($"{mp} -> {dep}"); break; }
        }
    }
    return refs.Count == 0 ? "NO_REFS" : string.Join("\\n", refs);
"""})
```

Expected: `NO_REFS`. 있으면 중단.

**Step 2:** 빈 셰이더 + meta 삭제

```
manage_asset({action:"delete", path:"Assets/_Project/VFX/Shaders/VFX_Dissolve.shadergraph"})
manage_asset({action:"delete", path:"Assets/_Project/VFX/Shaders/VFX_Glow.shadergraph"})
manage_asset({action:"delete", path:"Assets/_Project/VFX/New Shader Graph.shadergraph"})
```

**Step 3:** 콘솔 + 커밋

```bash
git add -A Assets/_Project/VFX
git commit -m "chore(vfx): remove empty legacy shaders (unreferenced)"
```

---

## Phase 3 — 베이스라인 Android 측정

### Task 3.1: Placeholder Tornado 3× 베이스라인 측정

**목적:** Codex 지적 — "8ms/12ms/800 particle" 은 근거 없음. 리스킨 전 placeholder 에서 측정한 수치를 기준점으로 삼아 "리스킨 후 베이스라인 +Δ" 형태로 delta 기반 판단.

**Files:**
- Build artifact: `Builds/Android/wassup-baseline.apk`

**Step 1:** 현재 상태(placeholder Tornado) 의 Android 개발 빌드

```
manage_build({action:"build", target:"android",
    output_path:"Builds/Android/wassup-baseline.apk",
    development:"true",
    scenes:"[\"Assets/_Project/Scenes/Battle.unity\"]"})
```

Expected: APK 산출. 실패 시 원인(SDK/NDK 설정) 기록 후 중단.

**Step 2:** 사용자에게 실기기 + Unity Profiler 연결 요청 (Development build 로 Profiler 자동 활성).

**Step 3:** 사용자가 실기기에서 **Tornado 3회 동시 발화 시나리오** 수행. 아래 항목 기록 요청:

| 항목 | 값 | 비고 |
|---|---|---|
| Device model | | 예: Pixel 7 |
| Unity 버전 | | |
| CPU main ms (발화 후 1s 평균) | | Placeholder 기준 |
| GPU ms | | |
| SetPass 발화전/후 | | |
| Particle peak (Frame Debugger) | | |
| APK size | | |

**Step 4:** 이 데이터를 design doc §6.4 에 "Baseline (placeholder)" 섹션으로 append + 커밋

```bash
git commit -m "docs(vfx): append placeholder Tornado baseline Android 측정"
```

**Step 5:** 이후 리스킨 후 측정은 **이 baseline 대비 delta** 로 평가. 절대 수치 임계선 폐기.

---

## Phase 4 — Portal feasibility 조기 확인

### Task 4.1: Portal LinkBeam 전략 feasibility 프로토타입

**목적:** Codex High — WaterBeam 은 mesh 기반 + `BeamVfx` 스크립트가 길이 담당. LineRenderer 위 단순 overlay 는 entry↔exit 길이에 안 맞을 가능성 높음. Tornado 파일럿 착수 전에 Portal 전략 가능성을 검증.

**Files:**
- Read-only: `Assets/PixPlays/ElementalBeams/WaterBeam/Version_URP/WaterBeam.prefab`
- Read-only: `Assets/PixPlays/Components/Scripts/VfxSystem/Beams/BeamVfx.cs` (구조 확인)
- Read-only: `Assets/_Project/Scripts/Presentation/VfxSpawner.cs:82-101` (SpawnPortal)

**Step 1:** WaterBeam prefab 의 hierarchy 덤프

```
manage_prefabs({action:"get_hierarchy",
    prefab_path:"Assets/PixPlays/ElementalBeams/WaterBeam/Version_URP/WaterBeam.prefab"})
```

Expected: beam body / cast / hit 서브트리. 각 자식에 mesh 기반 렌더러 / BeamVfx 컴포넌트 존재.

**Step 2:** `BeamVfx.cs` 읽기 — 길이/방향을 어떻게 계산하는지 파악

**Step 3:** 세 가지 전략 중 하나 결정:

| 전략 | 내용 | VfxSpawner 수정? |
|---|---|---|
| A | LinkBeam 자식에 LineRenderer **유지** + WaterBeam body 파티클만 overlay(길이에 안 따라감 = 시각 하자 허용) | 없음 |
| B | WaterBeam body 를 LinkBeam 자식으로 얹고 **우리가 자체 length 스크립트 추가**(MeteorFall 과 유사한 MonoBehaviour), PixPlays `BeamVfx` 미이식 | 없음 (신규 MonoBehaviour 는 `_Project/Scripts/Presentation/` 에 추가) |
| C | Portal 만 WaterBeam 포기. 기존 LineRenderer 비주얼 개선 + entry/exit 에 WaterShield spawn 링만 얹음 | 없음 |

**Step 4:** 사용자에게 전략 결정 요청. 기본 권장 — **전략 C** (가장 저위험, Phase 8 §13 prefab-only 정책 유지, 새 MonoBehaviour 도입 없음).

**Step 5:** 결정사항을 design doc §7.3 에 기록 + 커밋

```bash
git commit -m "docs(vfx): Portal feasibility 결정 — 전략 X 채택"
```

**Step 6:** 전략 B 선택 시에만 신규 MonoBehaviour 스펙 별도 Task 로 작성 후 이 Phase 종료 전 구현/테스트.

---

## Phase 5 — Tornado 파일럿

### Task 5.1: Tornado 발동 harness 확보

**목적:** Codex High — Tornado 를 Battle scene fresh Play 에서 발동 가능한지 불확실. 드래프트/코스트 플로우 경유하지 않고 직접 호출 harness 마련.

**Step 1:** 현재 VfxSpawner 인스턴스 경로 확인 — Battle.unity 에 배치된 GameObject 이름

```
manage_scene({action:"load", path:"Assets/_Project/Scenes/Battle.unity"})
find_gameobjects({search_term:"VfxSpawner", search_method:"by_component"})
```

Expected: VfxSpawner instance ID + 경로.

**Step 2:** Harness 스크립트 경로/존재 확인 — 기존에 VFX 호출 테스트용 스크립트가 있는지

```
execute_code({action:"execute", code:"""
    var guids = UnityEditor.AssetDatabase.FindAssets("VfxSmoke OR VfxTest OR VfxHarness", new[]{ "Assets/_Project" });
    return guids.Length == 0 ? "NONE" : string.Join(",", System.Linq.Enumerable.Select(guids, g => UnityEditor.AssetDatabase.GUIDToAssetPath(g)));
"""})
```

**Step 3:** harness 없으면 editor-only 임시 invocation 을 `execute_code` 로 준비:

```
execute_code({action:"execute", code:"""
    var vs = UnityEngine.Object.FindObjectOfType<Wassup.Presentation.VfxSpawner>();
    if (vs == null) return "NO_VFX_SPAWNER";
    vs.SpawnTornado(new UnityEngine.Vector3(0,0,0), 2.0f, 3.0f);
    vs.SpawnTornado(new UnityEngine.Vector3(3,0,0), 2.0f, 3.0f);
    vs.SpawnTornado(new UnityEngine.Vector3(-3,0,0), 2.0f, 3.0f);
    return "OK";
"""})
```

**⚠️ Play Mode 중에만 유효** — Edit Mode 에서는 VfxSpawner.Instantiate 가 scene 에 일시 오브젝트를 만들고 Destroy 타이머로 제거되므로 scene 을 더럽히지 않음. 그러나 정석은 Play 중 호출.

**Step 4:** Harness 호출 가능성 확인 — Play 진입 후 위 `execute_code` 수행 가능한지 사용자 수동 검증. 결과 기록. **커밋 없음**.

---

### Task 5.2: Tornado_SKELETON GUID 스냅샷

**목적:** Codex High — GUID 보존 assert. 편집 전 기록.

**Step 1:** 현재 prefab GUID 추출

```
execute_code({action:"execute", code:"""
    return UnityEditor.AssetDatabase.AssetPathToGUID("Assets/_Project/VFX/Tornado_SKELETON.prefab");
"""})
```

Expected: 32-char guid. 기록.

**Step 2:** VfxSpawner 의 `tornadoPrefab` SerializeField 도 같은 GUID 를 참조하는지 `.unity`/`.prefab` 텍스트 grep 으로 확인

```bash
grep -l "<기록된 GUID>" Assets/_Project/Scenes/Battle.unity Assets/_Project/**/*.prefab 2>/dev/null
```

Expected: Battle.unity 혹은 VfxSpawner 를 들고 있는 상위 prefab 에 참조 존재.

**Step 3:** 결과를 notepad 에 기록. **커밋 없음**.

---

### Task 5.3: WindAoeVFX 계층 덤프 + 이식 대상 선정

**Step 1:** hierarchy 덤프

```
manage_prefabs({action:"get_hierarchy",
    prefab_path:"Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab"})
```

**Step 2:** 덤프 결과에서 이식할 자식 경로 + 버릴 자식 경로 2 리스트 산출. 이식 후보는 ParticleSystem 이 붙은 노드.

**Step 3:** **커밋 없음**.

---

### Task 5.4: Tornado_SKELETON placeholder 자식 제거

**목적:** 기존 placeholder 를 먼저 비우고 그 자리에 PixPlays 계층을 넣는 순서.

**Step 1:** 현재 Tornado_SKELETON 자식 목록

```
manage_prefabs({action:"get_hierarchy",
    prefab_path:"Assets/_Project/VFX/Tornado_SKELETON.prefab"})
```

**Step 2:** 루트 외 모든 자식 경로를 array 로 수집 후 headless 삭제

```
manage_prefabs({action:"modify_contents",
    prefab_path:"Assets/_Project/VFX/Tornado_SKELETON.prefab",
    delete_child:["Child1", "Child2", ...]})
```

**Step 3:** GUID assert

```
execute_code({action:"execute", code:"""
    return UnityEditor.AssetDatabase.AssetPathToGUID("Assets/_Project/VFX/Tornado_SKELETON.prefab");
"""})
```

Expected: Task 5.2 에서 기록한 GUID 와 동일.

**Step 4:** 콘솔 확인 + 커밋

```bash
git add Assets/_Project/VFX/Tornado_SKELETON.prefab
git commit -m "chore(vfx): empty Tornado_SKELETON placeholder children (GUID preserved)"
```

---

### Task 5.5: WindAoeVFX 선택 자식을 Tornado_SKELETON 으로 이식

**목적:** Hierarchy transplant. Headless 로는 복잡한 노드 이식이 어려우므로 **interactive prefab stage** 경로 사용.

**Step 1:** Tornado_SKELETON 을 prefab stage 로 open

```
manage_prefabs({action:"open_prefab_stage",
    prefab_path:"Assets/_Project/VFX/Tornado_SKELETON.prefab"})
```

**Step 2:** `execute_code` 로 WindAoeVFX 임시 인스턴스 생성 + 선정 자식 복제해서 stage 루트 자식으로 reparent

```
execute_code({action:"execute", code:"""
    var src = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
        "Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab");
    if (src == null) return "SRC_NOT_FOUND";
    var srcInst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(src);
    UnityEditor.PrefabUtility.UnpackPrefabInstance(srcInst,
        UnityEditor.PrefabUnpackMode.Completely,
        UnityEditor.InteractionMode.AutomatedAction);
    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
    if (stage == null) return "NO_STAGE";
    var targets = new [] { "<이식 자식 경로1>", "<이식 자식 경로2>" };  // Task 5.3 결과
    foreach (var path in targets) {
        var child = srcInst.transform.Find(path);
        if (child == null) continue;
        child.SetParent(stage.prefabContentsRoot.transform, false);
    }
    UnityEngine.Object.DestroyImmediate(srcInst);
    return "OK";
"""})
```

**Step 3:** Prefab stage 저장 + 닫기

```
manage_prefabs({action:"save_prefab_stage"})
manage_prefabs({action:"close_prefab_stage"})
```

**Step 4:** GUID assert (Task 5.2 대비)

**Step 5:** 콘솔 확인 + 커밋

```bash
git commit -m "feat(vfx): transplant WindAoeVFX particle nodes into Tornado_SKELETON"
```

---

### Task 5.6: PixPlays MonoBehaviour 일괄 purge

**목적:** Codex Critical #3 — MonoScript 경로 기반 일괄 제거. 하드코딩된 타입 리스트 금지.

**Step 1:** Tornado_SKELETON 의 MonoBehaviour 중 PixPlays 경로 소스 일괄 제거

```
manage_prefabs({action:"open_prefab_stage",
    prefab_path:"Assets/_Project/VFX/Tornado_SKELETON.prefab"})
```

```
execute_code({action:"execute", code:"""
    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
    if (stage == null) return "NO_STAGE";
    var root = stage.prefabContentsRoot;
    var log = new System.Collections.Generic.List<string>();
    var toRemove = new System.Collections.Generic.List<UnityEngine.MonoBehaviour>();
    foreach (var mb in root.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)) {
        if (mb == null) continue;
        var ms = UnityEditor.MonoScript.FromMonoBehaviour(mb);
        if (ms == null) continue;
        var path = UnityEditor.AssetDatabase.GetAssetPath(ms);
        if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/PixPlays/")) {
            var go = mb.gameObject;
            log.Add($"{UnityEditor.AnimationUtility.CalculateTransformPath(go.transform, root.transform)} / {mb.GetType().Name} from {path}");
            toRemove.Add(mb);
        }
    }
    foreach (var mb in toRemove) UnityEngine.Object.DestroyImmediate(mb, true);
    return log.Count == 0 ? "NO_PIXPLAYS_MB" : string.Join("\\n", log);
"""})
```

Expected: 제거된 MonoBehaviour 목록 (`NO_PIXPLAYS_MB` 면 이미 깨끗).

**Step 2:** 재검증 — 여전히 남아있는지 재탐색

```
execute_code({action:"execute", code:"""
    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
    var root = stage.prefabContentsRoot;
    int n = 0;
    foreach (var mb in root.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)) {
        if (mb == null) continue;
        var ms = UnityEditor.MonoScript.FromMonoBehaviour(mb);
        if (ms == null) continue;
        var path = UnityEditor.AssetDatabase.GetAssetPath(ms);
        if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/PixPlays/")) n++;
    }
    return n.ToString();
"""})
```

Expected: `0`.

**Step 3:** `save_prefab_stage` + `close_prefab_stage` + GUID assert + 콘솔

**Step 4:** 커밋

```bash
git commit -m "chore(vfx): purge PixPlays MonoBehaviours from Tornado_SKELETON (path-based)"
```

---

### Task 5.7: Tornado 파티클 Material swap + 스케일 모드

**Step 1:** prefab stage open

**Step 2:** `execute_code` 로 모든 ParticleSystemRenderer 의 sharedMaterial 을 `VFX_Uber_Wind.mat` 로 교체 + main module 의 scalingMode 를 `Hierarchy` 로

```
execute_code({action:"execute", code:"""
    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
    var root = stage.prefabContentsRoot;
    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(
        "Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat");
    var report = new System.Collections.Generic.List<string>();
    foreach (var ps in root.GetComponentsInChildren<UnityEngine.ParticleSystem>(true)) {
        var renderer = ps.GetComponent<UnityEngine.ParticleSystemRenderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
        var main = ps.main;
        main.scalingMode = UnityEngine.ParticleSystemScalingMode.Hierarchy;
        report.Add($"{UnityEditor.AnimationUtility.CalculateTransformPath(ps.transform, root.transform)} -> looping={main.loop}, duration={main.duration}");
    }
    return string.Join("\\n", report);
"""})
```

Expected: 적용 후 보고 (각 PS 의 looping/duration 확인용).

**Step 3:** save + close

**Step 4:** 콘솔 + 커밋

```bash
git commit -m "feat(vfx): apply VFX_Uber_Wind material and Hierarchy scaling to Tornado particles"
```

---

### Task 5.8: Tornado_SKELETON 시각 검증 (Edit Mode preview)

**목적:** Play 전 scene preview 캡처로 회귀 없음 시각 확인.

**Step 1:** 임시 scene 에 Tornado_SKELETON 인스턴스 생성

```
execute_code({action:"execute", code:"""
    var src = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
        "Assets/_Project/VFX/Tornado_SKELETON.prefab");
    var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(src);
    inst.transform.position = UnityEngine.Vector3.zero;
    inst.name = "__TornadoPreview";
    return "OK";
"""})
```

**Step 2:** Scene view frame

```
manage_scene({action:"scene_view_frame", scene_view_target:"__TornadoPreview"})
```

**Step 3:** 스크린샷 캡처 (기본 SceneView 카메라)

```
# TODO: manage_camera({action:"screenshot", output_path:"..."}) — 실제 파라미터는 툴 스키마 재조회
```

**Step 4:** 사용자에게 스크린샷 확인 요청 — 기대: 회오리 파티클 형태가 보이는지.

**Step 5:** 임시 인스턴스 제거

```
execute_code({action:"execute", code:"""
    var go = UnityEngine.GameObject.Find("__TornadoPreview");
    if (go != null) UnityEngine.Object.DestroyImmediate(go);
    return "OK";
"""})
```

**커밋 없음** (preview 만).

---

### Task 5.9: Tornado Play Mode 검증 (사용자 게이트)

**목적:** Phase 8 §17 Tornado pull 회귀 없음 + 신규 VFX 정상.

**Step 1:** Battle scene 활성화

**Step 2:** Play 진입

```
manage_editor({action:"play"})
```

**Step 3:** Task 5.1 harness 로 3회 Tornado 발동 (또는 사용자 수동 발동)

**Step 4:** 확인 체크리스트 (사용자 게이트):

- [ ] 회오리 비주얼이 WindAoeVFX 스타일로 보임
- [ ] `DurationSec` 끝에 자연스럽게 사라짐
- [ ] Phase 8 §17 pull 동작 — 적이 Tornado 중심으로 당겨짐
- [ ] 에디터 콘솔 신규 에러 0
- [ ] Missing script 워닝 0

**Step 5:** Play 종료

```
manage_editor({action:"stop"})
```

**Step 6:** 사용자 OK 시 다음 task. 실패 시 Task 5.5~5.7 로 복귀.

---

### Task 5.10: Tornado Android 측정 (delta)

**목적:** baseline Phase 3 대비 delta 로 판단.

**Step 1:** 개발 빌드

```
manage_build({action:"build", target:"android",
    output_path:"Builds/Android/wassup-tornado-pilot.apk",
    development:"true",
    scenes:"[\"Assets/_Project/Scenes/Battle.unity\"]"})
```

**Step 2:** 사용자 실기기 + Profiler, Tornado 3×. baseline 동일 수치 기록.

**Step 3:** Delta 계산 후 design doc §6.4 에 "Tornado pilot (reskinned)" 섹션으로 append.

**Step 4:** 커밋

```bash
git commit -m "docs(vfx): append Tornado reskin Android 측정 (baseline delta)"
```

---

### Task 5.11: 다운사이즈 티어 결정

**Step 1:** baseline delta 기반 평가. 권장 기준:

- **티어 1 원본 유지**: CPU/GPU/SetPass 모두 baseline+20% 이내, APK +2MB 이내
- **티어 2 기본 다운**: 한 항목이 +20~50% 초과
- **티어 3 공격 다운**: 두 항목 이상 +50% 초과

**Step 2:** 결정한 티어를 design doc §7 프리앰블에 기록 + 커밋

**Step 3:** 필요 시 Tornado_SKELETON 에 티어 소급 적용(Task 5.7 의 변형). 소급 적용 후 Play 재검증 (Task 5.9 축약 반복).

---

### Task 5.12: WindAOE BuiltIn 정리

**Step 1:** `Assets/PixPlays/ElementalAOE/WindAOE/Version_BuiltIn/` 참조 검증 (Task 2.1 Step 1 패턴)

**Step 2:** 삭제

```
manage_asset({action:"delete", path:"Assets/PixPlays/ElementalAOE/WindAOE/Version_BuiltIn"})
```

**Step 3:** 콘솔 + 커밋

```bash
git commit -m "chore(vfx): remove PixPlays WindAOE BuiltIn variant"
```

---

## Phase 6 — 나머지 3종 일괄 (티어 적용)

각 prefab 은 Task 5.2~5.12 패턴을 축약 반복. 차이점만 기술.

### Task 6.1: Placement_SKELETON (Earth)

- **GUID 스냅샷** + **placeholder purge** + **EarthAOE ring/dust 자식 이식** (shard Rigidbody 이식 안 함)
- **Main duration < 0.5s, looping=false** (SpawnPlacementRing 이 `Destroy(go, 0.6f)` 이므로)
- Material: `VFX_Uber_Earth.mat`
- PixPlays MB purge
- Play Mode 검증 (배치 수행)
- EarthAOE BuiltIn 정리

각 sub-step 별 커밋.

### Task 6.2: Meteor_Burst_SKELETON (Fire - AOE)

- FireAOE explosion + shockwave + spark burst 3 노드 이식, 지면 smolder 제외
- **Scaling Mode Hierarchy**, **Destroy 1.2s 내 수렴**
- Material: `VFX_Uber_Fire.mat`
- PixPlays MB purge
- Play Mode 검증 (Meteor 발동)
- FireAOE BuiltIn 정리

### Task 6.3: Meteor_Falling_SKELETON (Fire - projectile)

- **주의**: 루트에 `MeteorFall` 우리 MonoBehaviour 있음 — **절대 건드리지 않음**
- Fireball flight trail + core flame 2 노드만 이식, Cast/Hit 제외
- PixPlays `ProjectileVfx`, `BaseVfx` 전면 purge (MonoScript 기반)
- Material: `VFX_Uber_Fire.mat`
- Play Mode 검증 (Meteor warning 창에서 낙하 trail)
- Fireball BuiltIn 정리

### Task 6.4: Portal_SKELETON (Water)

**전제**: Task 4.1 에서 결정한 전략(A/B/C) 에 따라 분기.

**전략 C 기준 (권장):**
- Entry / Exit 자식에 WaterShield spawn 링 이식. 두 자식 이름 유지.
- LinkBeam 자식 + LineRenderer **유지**. WaterBeam body 이식 안 함.
- Material: `VFX_Uber_Water.mat`
- PixPlays MB purge
- Play Mode 검증 (Portal 발동 — entry/exit 링 + shaft)
- WaterBeam + WaterShield BuiltIn 정리

**전략 A/B 기준**은 Task 4.1 Step 5 문서화 결과를 따름.

---

## Phase 7 — 최종 감사 & 일괄 정리

### Task 7.1: 4종 동시 발화 회귀 검증

**Step 1:** Battle Play, Tornado 2 + Meteor 1 + Placement 1 + Portal 1 연속 발동.

**Step 2:** Android delta 수치 임계 내.

**Step 3:** 콘솔 신규 에러 0.

**Step 4:** 결과 design doc 최종 measurement 섹션에 기록 + 커밋.

---

### Task 7.2: 최종 정리 일괄

**Files to delete:**
- `Assets/PixPlays/Elemental*/Demo*Scene*_BuiltIn.unity` + meta
- `Assets/PixPlays/Elemental*/Demo*Scene*_URP.unity` + meta
- `Assets/PixPlays/Elemental*/*_URP.unitypackage` + meta
- `Assets/PixPlays/Components/Scripts/VFXTester.cs` + meta
- `Assets/PixPlays/Components/Scripts/Character.cs` + meta
- `Assets/PixPlays/Components/Scripts/BindingPoints.cs` / `BindingPointType.cs` / `IHittable.cs` + meta (참조 검증 후)
- `Assets/PixPlays/Components/Components_BuiltIn/` 전체

**Step 1:** 각 삭제 전 참조 검증 (execute_code + AssetDatabase.GetDependencies reverse scan)

**Step 2:** 일괄 삭제 + refresh + console

**Step 3:** 단계별 커밋 (demo-scenes / unitypackages / scripts / Components_BuiltIn 4 커밋)

---

### Task 7.3: 텍스처 의존성 manifest

**목적:** Codex Medium — 각 신규 Material 이 PixPlays 텍스처를 참조하고 있음을 manifest 로 문서화. Phase 9 이후 PixPlays 제거 시 이관해야 할 자산 리스트.

**Step 1:** 의존성 덤프

```
execute_code({action:"execute", code:"""
    var mats = new [] {
        "Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat",
        "Assets/_Project/VFX/Materials/VFX_Uber_Fire.mat",
        "Assets/_Project/VFX/Materials/VFX_Uber_Earth.mat",
        "Assets/_Project/VFX/Materials/VFX_Uber_Water.mat",
    };
    var sb = new System.Text.StringBuilder();
    foreach (var m in mats) {
        sb.AppendLine($"## {m}");
        foreach (var dep in UnityEditor.AssetDatabase.GetDependencies(m, true)) {
            sb.AppendLine($"- {dep}");
        }
    }
    return sb.ToString();
"""})
```

**Step 2:** 결과를 `docs/plans/2026-04-20-pixplays-vfx-pipeline-design.md` 에 "텍스처 의존성 manifest" 섹션으로 append. Phase 9 이후 이관 대상 확정.

**Step 3:** 커밋

```bash
git commit -m "docs(vfx): add texture dependency manifest for new VFX_Uber materials"
```

---

### Task 7.4: residual-issues 갱신

- "VFX 파이프라인 개선 (PixPlays Ubershader 흡수 + 4종 리스킨)" 항목 종결
- 재검토 항목 추가: PixPlays 텍스처 이관 · Ubershader 우리 쪽 복제 · 미사용 원소 URP 프리팹 유지 여부

커밋.

---

## Phase 8 — Definition of Done 점검

Design doc §11 전 항목 + 추가:

- [ ] 4개 `_SKELETON.prefab` GUID 편집 전후 동일 (execute_code log 로 증명)
- [ ] 4개 원소 Material + 2개 공용 헬퍼 Material 존재
- [ ] 모든 prefab 에 PixPlays MonoBehaviour 0 (재검증 execute_code 로 0 반환)
- [ ] VfxSpawner.cs 변경 없음 (Portal 전략 B 제외. B 채택 시 단일 MonoBehaviour 추가만 허용)
- [ ] Play Mode 5개 VFX 정상 + 콘솔 0 에러
- [ ] Android delta 수치 설계 doc 에 기록
- [ ] PixPlays BuiltIn/demo/unitypackage/Demo scripts 제거 완료
- [ ] 텍스처 manifest 문서화
- [ ] Design doc + 이 plan doc + residual-issues 최신화

---

## 실행 지침 (v1 대비 강화)

- **v1 은 폐기**. 이 v2 로 대체.
- **Preflight (Phase 0) 실패 시 전면 중단** — 다른 세션이 Editor 쥐고 있으면 대기.
- **Portal feasibility (Phase 4) 를 Tornado 파일럿 전에 수행** — 전략 결정이 나머지 phase 스케줄에 영향.
- **GUID assert** 는 각 prefab 편집 task 마다 마지막 step 으로 고정.
- **PixPlays MonoBehaviour purge 는 MonoScript 경로 기반**. 하드코딩 타입 리스트 사용 금지.
- **Android 임계선은 baseline delta** 로. 절대 수치 평가 금지.
- **Material 은 원본 복제 기반** 생성. 속성명 추측 금지.
- **사용자 게이트** — Task 0.2, 3.1, 4.1 Step 4, 5.9, 5.10, 6.x 각 Play 검증, 7.1.
- **경계 이탈 감지 시 즉시 중단**: 새 ECS 맥락 · 새 스킬 타입 · 새 Manager 싱글턴 · VfxSpawner 시그니처 변경(Portal 전략 B 외).
