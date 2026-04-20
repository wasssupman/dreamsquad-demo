# PixPlays VFX 파이프라인 개선 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 기존 `_SKELETON.prefab` 4종(Placement / Tornado / Meteor Burst+Falling / Portal) 의 내부 비주얼을 PixPlays 원소 VFX 자원으로 리스킨하고, `PixPlays/Components/Shaders/Ubershader.shadergraph` 를 우리 VFX Material 의 기반으로 흡수한다. VfxSpawner 계약 · prefab guid 불변.

**Architecture:** `Assets/_Project/VFX/Materials/` 에 원소 프리셋 Material 4종(신규) + 공용 헬퍼 2종(기존 빈 Material 재활용) 을 Ubershader 기반으로 구성한다. 각 `_SKELETON.prefab` 은 파일 경로·guid 를 유지한 채 내부 파티클 계층만 PixPlays `Version_URP/` 에서 복사 이식한다. 옵션 1 실행 순서 — Tornado 파일럿 → Android 실기 측정으로 다운사이즈 티어 확정 → 나머지 3종 일괄 적용. 각 카테고리 완료 직후 해당 BuiltIn 폴더만 제거, 데모 씬·unitypackage 는 최종 감사에서 일괄.

**Tech Stack:** Unity 6 · URP 17.3 · Shuriken 파티클 · Shader Graph (PixPlays Ubershader) · UnityMCP (prefab hierarchy 편집 · Material 조작 · Play Mode 검증 · Profiler).

**설계 문서:** `docs/plans/2026-04-20-pixplays-vfx-pipeline-design.md`

**절대 제약:** `CLAUDE.md` 의 VfxSpawner 계약 · prefab guid/경로 불변 · PixPlays MonoBehaviour 비참조 · ECS 경계 엄수.

---

## Phase 0 — 준비

### Task 0.1: 작업 전 상태 스냅샷 커밋

**목적:** 리스킨 전후 시각 A/B 비교 + 문제 발생 시 롤백 기준 확보.

**Files:**
- Commit staged area. 현재 `git status` 에 올라와 있는 untracked VFX 자산 중 `Assets/_Project/VFX/Materials/VFX_{Dissolve,Glow}_Mat.mat` 두 파일만 스냅샷에 포함 (나머지 PixPlays, Spine Examples, _Recovery 는 별도 이슈).

**Step 1:** 현재 Materials 폴더의 빈 Material 2개 내용 확인

```bash
ls -la /Users/sy/dev/wassup/Assets/_Project/VFX/Materials/
```

Expected: `VFX_Dissolve_Mat.mat`, `VFX_Glow_Mat.mat` 두 파일 존재.

**Step 2:** 빈 Material + meta 만 staged 로 추가

```bash
git add Assets/_Project/VFX/Materials/VFX_Dissolve_Mat.mat Assets/_Project/VFX/Materials/VFX_Dissolve_Mat.mat.meta
git add Assets/_Project/VFX/Materials/VFX_Glow_Mat.mat Assets/_Project/VFX/Materials/VFX_Glow_Mat.mat.meta
```

**Step 3:** 스냅샷 커밋

```bash
git commit -m "$(cat <<'EOF'
chore(vfx): snapshot empty VFX_Dissolve/VFX_Glow materials pre-reskin

리스킨 작업 전 원본 빈 Material 상태 보존. Phase 0 Task 0.1.
EOF
)"
```

Expected: 1 file or 2 files changed, commit 생성.

---

### Task 0.2: 레거시 빈 셰이더 제거

**목적:** Q3 결정. `VFX/Shaders/VFX_Dissolve.shadergraph`, `VFX/Shaders/VFX_Glow.shadergraph`, `VFX/New Shader Graph.shadergraph` 3개는 비어있고 어떤 Material 도 참조하지 않음 → 제거.

**Files:**
- Delete: `Assets/_Project/VFX/Shaders/VFX_Dissolve.shadergraph` (+ `.meta`)
- Delete: `Assets/_Project/VFX/Shaders/VFX_Glow.shadergraph` (+ `.meta`)
- Delete: `Assets/_Project/VFX/New Shader Graph.shadergraph` (+ `.meta`)

**Step 1:** 각 셰이더가 정말 미참조인지 확인

```
UnityMCP.manage_asset({ action: "search_refs", path: "Assets/_Project/VFX/Shaders/VFX_Dissolve.shadergraph" })
UnityMCP.manage_asset({ action: "search_refs", path: "Assets/_Project/VFX/Shaders/VFX_Glow.shadergraph" })
UnityMCP.manage_asset({ action: "search_refs", path: "Assets/_Project/VFX/New Shader Graph.shadergraph" })
```

Expected: 참조 0. 만약 참조 있으면 **작업 중단** 후 사용자 확인.

**Step 2:** 각 셰이더와 meta 삭제

```
UnityMCP.manage_asset({ action: "delete", path: "Assets/_Project/VFX/Shaders/VFX_Dissolve.shadergraph" })
UnityMCP.manage_asset({ action: "delete", path: "Assets/_Project/VFX/Shaders/VFX_Glow.shadergraph" })
UnityMCP.manage_asset({ action: "delete", path: "Assets/_Project/VFX/New Shader Graph.shadergraph" })
```

**Step 3:** 에디터 refresh + 콘솔 확인

```
UnityMCP.refresh_unity()
UnityMCP.read_console()
```

Expected: 신규 에러 0. missing shader 워닝 없음.

**Step 4:** 커밋

```bash
git add -u Assets/_Project/VFX/Shaders Assets/_Project/VFX
git commit -m "$(cat <<'EOF'
chore(vfx): remove empty legacy shaders

VFX_Dissolve.shadergraph / VFX_Glow.shadergraph / New Shader Graph.shadergraph
3종 제거. Ubershader 로 흡수 예정. 어떤 Material 도 미참조 확인.
EOF
)"
```

---

## Phase 1 — Tornado 파일럿

### Task 1.1: 전제 — VfxSpawner Tornado 경로 생존 확인

**목적:** 리스킨 시작 전 현재 Tornado VFX 호출 경로가 실제로 작동하는지 Play Mode 로 확인. 실패 시 파일럿 중단.

**Files:**
- Read-only: `Assets/_Project/Scripts/Presentation/VfxSpawner.cs:68-79` (SpawnTornado)
- Read-only: `Assets/_Project/VFX/Tornado_SKELETON.prefab`

**Step 1:** Play Mode 진입 + Tornado 스킬 발동이 가능한 최소 플로우 식별

```
UnityMCP.manage_editor({ action: "get_state" })
```

Expected: editor state 확보. `isPlaying` 초기값 false.

**Step 2:** Battle scene 로드 + Play 시작

```
UnityMCP.manage_scene({ action: "load", path: "Assets/_Project/Scenes/Battle.unity" })
UnityMCP.manage_editor({ action: "play" })
```

**Step 3:** Tornado 스킬이 발동되는 조건(드래프트에서 Tornado 선택 후 배치) 이 가능한지 **사용자에게 수동 실행 요청**.

**Step 4:** 사용자가 Tornado 발동 후, 에디터 콘솔 확인

```
UnityMCP.read_console()
```

Expected: `SpawnTornado` 관련 에러 없음. 현 placeholder 비주얼 확인.

**Step 5:** Play 종료

```
UnityMCP.manage_editor({ action: "stop" })
```

**Step 6:** 이 task 는 커밋 없음. 결과만 기록.

---

### Task 1.2: VFX_Uber_Wind.mat 생성

**목적:** Tornado 용 Ubershader 기반 Material 프리셋 생성. WindAOE 메인 파티클 Material 파라미터를 수동 복사.

**Files:**
- Create: `Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat` (+ meta)

**Step 1:** WindAOE URP 버전 메인 파티클 Material 경로 파악

```
UnityMCP.find_in_file({
  path: "Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab",
  pattern: "m_Materials"
})
```

Expected: Material guid 목록. 가장 중심 파티클의 Material 을 선정 (예: `WindCore`, `WindSwirl` 류).

**Step 2:** 선정한 Material 파일 경로 역추적 + 파라미터 Dump

```
UnityMCP.manage_asset({ action: "get_info", path: "<선정된 Wind Material 경로>" })
```

Expected: shader, color, emission, uv scroll, blend 파라미터 기록.

**Step 3:** 새 Material 생성

```
UnityMCP.manage_asset({
  action: "create",
  asset_type: "Material",
  path: "Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat",
  shader: "Shader Graphs/Ubershader"
})
```

Expected: Material 생성 성공.

**Step 4:** Step 2 에서 기록한 파라미터를 새 Material 에 적용

```
UnityMCP.manage_asset({
  action: "set_properties",
  path: "Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat",
  properties: { /* color, uv scroll, emission 등 */ }
})
```

Expected: 파라미터 반영.

**Step 5:** 외관 검증 — 임시 Quad 에 Material 할당 후 scene preview

```
UnityMCP.execute_code({
  code: `
    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
    go.name = "VFX_Uber_Wind_Preview";
    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat");
    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
  `
})
```

Expected: Scene 에 Quad 생성, Material 외관 확인 가능. 확인 후 Quad 제거.

**Step 6:** 콘솔 확인

```
UnityMCP.read_console()
```

Expected: 신규 에러 0.

**Step 7:** 커밋

```bash
git add Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat.meta
git commit -m "$(cat <<'EOF'
feat(vfx): add VFX_Uber_Wind material (PixPlays Ubershader 기반)

Tornado 리스킨 파일럿용. WindAoeVFX 메인 파티클 Material 파라미터 이식.
EOF
)"
```

---

### Task 1.3: WindAoeVFX 계층 파악

**목적:** 이식해올 파티클 노드 목록 확정.

**Files:**
- Read-only: `Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab`

**Step 1:** prefab 계층 Dump

```
UnityMCP.manage_prefabs({
  action: "get_hierarchy",
  path: "Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab"
})
```

Expected: 루트 + 자식 노드 트리. 각 자식의 ParticleSystem / MeshRenderer / SFX / Script 컴포넌트 정보.

**Step 2:** 이식할 노드 와 버릴 노드 분류. 문서로 기록 (주석 or 임시 파일).

**이식 대상 (예상):**
- 중심 수직 회오리 파티클
- 지면 먼지 / dust 파티클
- 상승 티끌 / rising motes 파티클

**버릴 대상:**
- Demo 용 SFX trigger
- Character-specific helper
- PixPlays MonoBehaviour (`BaseVfx`, `LocationVfx`, `ParticleSystemScaleLifetime` 등)

**Step 3:** 이 task 는 커밋 없음 (조사 단계).

---

### Task 1.4: Tornado_SKELETON 내부 리스킨

**목적:** prefab guid/path 를 유지한 채 내부 파티클 계층 교체.

**Files:**
- Modify: `Assets/_Project/VFX/Tornado_SKELETON.prefab`

**Step 1:** 임시 scene 에 WindAoeVFX + Tornado_SKELETON 두 개 인스턴스 생성

```
UnityMCP.execute_code({
  code: `
    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    var src = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab");
    var dst = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/VFX/Tornado_SKELETON.prefab");
    var srcInst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(src);
    var dstInst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(dst);
    UnityEditor.PrefabUtility.UnpackPrefabInstance(srcInst, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
  `
})
```

**Step 2:** Task 1.3 에서 식별한 이식 대상 노드를 `srcInst` 에서 `dstInst` 로 이동. 기존 `dstInst` 의 placeholder 자식은 삭제.

```
UnityMCP.manage_gameobject({ action: "reparent", ... })
UnityMCP.manage_gameobject({ action: "delete", ... })
```

**Step 3:** 모든 이식된 파티클의 Material 을 `VFX_Uber_Wind.mat` 로 swap

```
UnityMCP.execute_code({
  code: `
    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat");
    // dstInst 의 각 ParticleSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
  `
})
```

**Step 4:** 각 ParticleSystem 의 `Main → Scaling Mode = Hierarchy`, `Looping = true` 확인

```
UnityMCP.execute_code({
  code: `
    // 모든 ParticleSystem 순회 → main.scalingMode = ParticleSystemScalingMode.Hierarchy
  `
})
```

**Step 5:** PixPlays MonoBehaviour 가 이식 파티클 노드에 따라 들어왔는지 감사 + 있으면 제거

```
UnityMCP.find_gameobjects({
  in: "<임시 씬의 Tornado_SKELETON 인스턴스>",
  componentType: "BaseVfx"
})
```

Expected: 0 개. 있으면 전부 `DestroyImmediate`.

**Step 6:** dstInst 를 prefab 으로 Apply (기존 prefab guid 유지)

```
UnityMCP.execute_code({
  code: `
    UnityEditor.PrefabUtility.ApplyPrefabInstance(dstInst, UnityEditor.InteractionMode.AutomatedAction);
  `
})
```

Expected: `Tornado_SKELETON.prefab` 내용만 변경, guid 동일.

**Step 7:** 임시 씬 cleanup (두 인스턴스 DestroyImmediate)

**Step 8:** 콘솔 확인

```
UnityMCP.read_console()
```

Expected: 에러 0. Missing script 워닝 0.

**Step 9:** 커밋

```bash
git add Assets/_Project/VFX/Tornado_SKELETON.prefab
git commit -m "$(cat <<'EOF'
feat(vfx): reskin Tornado_SKELETON prefab (WindAOE 기반)

내부 파티클 계층 교체, prefab guid/path 불변. VFX_Uber_Wind Material 적용.
VfxSpawner.SpawnTornado 계약 미변경.
EOF
)"
```

---

### Task 1.5: Play Mode 검증

**목적:** Tornado 스킬 발동 시 리스킨 VFX 정상 재생 + Phase 8 §17 pull 동작 회귀 없음.

**Step 1:** Battle scene 로드 + Play 시작

```
UnityMCP.manage_scene({ action: "load", path: "Assets/_Project/Scenes/Battle.unity" })
UnityMCP.manage_editor({ action: "play" })
```

**Step 2:** 사용자에게 Tornado 발동 요청. 다음 확인 항목을 전달:

- [ ] 회오리 비주얼이 Placeholder 가 아닌 WindAOE 스타일로 보임
- [ ] `DurationSec` 끝날 때 자연스럽게 사라짐
- [ ] 적이 Tornado field 로 당겨짐 (Phase 8 §17 회귀 없음)
- [ ] 에디터 콘솔 에러 0

**Step 3:** 콘솔 확인

```
UnityMCP.read_console({ severity: "error,warning" })
```

Expected: 신규 에러 0. `Tornado_SKELETON` 관련 missing reference 워닝 0.

**Step 4:** Play 종료.

**Step 5:** 사용자 확인 실패 시 Task 1.4 로 돌아감. 통과 시 Task 1.6 진행.

---

### Task 1.6: Android 실기 측정

**목적:** Q4 "측정 후 결정" 이행. 다운사이즈 티어 확정.

**Step 1:** Android 빌드 (사용자 작업)

```
UnityMCP.manage_build({
  action: "build",
  platform: "Android",
  scenes: ["Assets/_Project/Scenes/Battle.unity"],
  output: "Builds/Android/wassup-tornado-pilot.apk"
})
```

Expected: APK 산출.

**Step 2:** APK 사이즈 기록

```bash
ls -lh /Users/sy/dev/wassup/Builds/Android/wassup-tornado-pilot.apk
```

**Step 3:** 사용자가 실기기에서 Tornado 3회 동시 발화 시나리오 재현 + Unity Profiler 연결

**Step 4:** 다음 수치를 기록 — 사용자에게 요청:

| 항목 | 기록 | 임계선 |
|---|---|---|
| CPU main ms | ___ | < 8ms |
| GPU ms | ___ | < 12ms |
| SetPass delta | ___ | +5 이하 |
| Particle peak | ___ | < 800 |
| APK delta | ___ | +2MB 이하 |

**Step 5:** Design doc §6.4 에 측정 결과를 append (커밋 포함)

```bash
# docs/plans/2026-04-20-pixplays-vfx-pipeline-design.md 끝에 Measurement Results 섹션 추가
git add docs/plans/2026-04-20-pixplays-vfx-pipeline-design.md
git commit -m "docs(vfx): Tornado 파일럿 Android 측정 결과 기록"
```

---

### Task 1.7: 다운사이즈 티어 결정

**목적:** 측정 결과에 따라 나머지 3종에 적용할 다운사이즈 기준 확정.

**Step 1:** 임계선 평가

- 모든 항목 통과 → **티어 1 "원본 유지"**
- 1-2 항목 임계 근접 또는 초과 → **티어 2 "기본 다운"** (maxParticles · Emission 70%)
- 3 항목 이상 초과 → **티어 3 "공격 다운"** (보조 파티클 제거 · 메시 → 파티클 대체)

**Step 2:** 결정 티어를 design doc §6.4 에 명시

**Step 3:** 필요 시 Tornado_SKELETON 에 결정 티어 소급 적용 (재커밋)

**Step 4:** 이 Task 는 데이터 없이 티어 결정만 — 커밋은 Step 2/3 에서 이뤄짐.

---

### Task 1.8: WindAOE BuiltIn 폴더 제거

**목적:** Q2 C안 — 카테고리별 단계 정리의 첫 회.

**Files:**
- Delete: `Assets/PixPlays/ElementalAOE/WindAOE/Version_BuiltIn/` (+ meta 트리 전체)

**Step 1:** Version_BuiltIn 이 어떤 Material/Texture 도 참조받지 않는지 검색

```
UnityMCP.manage_asset({
  action: "search_refs",
  path: "Assets/PixPlays/ElementalAOE/WindAOE/Version_BuiltIn"
})
```

Expected: 참조 0. 있으면 중단 후 조사.

**Step 2:** 폴더 삭제

```
UnityMCP.manage_asset({
  action: "delete",
  path: "Assets/PixPlays/ElementalAOE/WindAOE/Version_BuiltIn"
})
```

**Step 3:** refresh + 콘솔 확인

```
UnityMCP.refresh_unity()
UnityMCP.read_console()
```

Expected: 에러 0.

**Step 4:** 커밋

```bash
git add Assets/PixPlays/ElementalAOE/WindAOE
git commit -m "chore(vfx): remove PixPlays WindAOE BuiltIn variant (URP only)"
```

---

## Phase 2 — Placement 리스킨 (Earth 베이스)

### Task 2.1: VFX_Uber_Earth.mat 생성

**Files:**
- Create: `Assets/_Project/VFX/Materials/VFX_Uber_Earth.mat`

**Step 1~4:** Task 1.2 Step 1~6 패턴을 EarthAOE(`EarthSlamSpikesAoeVFX.prefab`) ring 평면 Material 기준으로 반복.

**Step 5:** 커밋

```bash
git add Assets/_Project/VFX/Materials/VFX_Uber_Earth.mat Assets/_Project/VFX/Materials/VFX_Uber_Earth.mat.meta
git commit -m "feat(vfx): add VFX_Uber_Earth material"
```

---

### Task 2.2: Placement_SKELETON 리스킨

**Files:**
- Modify: `Assets/_Project/VFX/Placement_SKELETON.prefab`

**Step 1:** EarthAOE URP prefab 에서 **지면 크랙 glow ring + dust puff** 노드만 이식. Shard Rigidbody 는 버림.

**Step 2:** `Main → Duration < 0.5s`, `Looping = off` 확인 (`SpawnPlacementRing` 이 `Destroy(go, 0.6f)` 이므로).

**Step 3:** 모든 파티클 Material → `VFX_Uber_Earth.mat`.

**Step 4:** Task 1.7 에서 확정된 다운사이즈 티어 적용.

**Step 5:** Apply prefab, 임시 인스턴스 cleanup.

**Step 6:** Play Mode 에서 placement(배치) 수행 → ring 비주얼 확인 + 콘솔 0 에러.

**Step 7:** 커밋

```bash
git add Assets/_Project/VFX/Placement_SKELETON.prefab
git commit -m "feat(vfx): reskin Placement_SKELETON (EarthAOE ring 기반)"
```

---

### Task 2.3: EarthAOE BuiltIn 제거 + 검증

Task 1.8 패턴 반복. 경로: `Assets/PixPlays/ElementalAOE/EarthAOE/Version_BuiltIn/`.

---

## Phase 3 — Meteor 리스킨 (Fire 베이스)

### Task 3.1: VFX_Uber_Fire.mat 생성

Task 1.2 패턴. 기반: FireAOE 폭발 코어 Material.

---

### Task 3.2: Meteor_Burst_SKELETON 리스킨

**Files:**
- Modify: `Assets/_Project/VFX/Meteor_Burst_SKELETON.prefab`

**Step 1:** FireAOE URP prefab 에서 **center explosion + shockwave ring + spark burst** 3노드만 이식. 지면 smolder 제외.

**Step 2:** `SpawnMeteorBurst` 는 `Destroy(go, 1.2f)`, `localScale = radiusWorld` 이므로 1.2s 내 수렴 + Scaling Mode Hierarchy 확인.

**Step 3~6:** Task 2.2 Step 3~6 패턴.

**Step 7:** 커밋

```bash
git add Assets/_Project/VFX/Meteor_Burst_SKELETON.prefab
git commit -m "feat(vfx): reskin Meteor_Burst_SKELETON (FireAOE 기반)"
```

---

### Task 3.3: Meteor_Falling_SKELETON 리스킨

**Files:**
- Modify: `Assets/_Project/VFX/Meteor_Falling_SKELETON.prefab`

**Step 1:** Fireball URP prefab 의 **Projectile 이동 구간** 노드 중 flight trail + core flame 2노드만 이식. Cast/Hit 노드 제외.

**Step 2:** **중요** — 루트 GameObject 의 `MeteorFall` 컴포넌트(우리 코드)는 **절대 건드리지 않음**. PixPlays `ProjectileVfx` / `BaseVfx` MonoBehaviour 는 하나도 이식하지 않음.

**Step 3:** Material → `VFX_Uber_Fire.mat`.

**Step 4:** 다운사이즈 티어 적용.

**Step 5:** Apply prefab.

**Step 6:** Play Mode 에서 Meteor 발동 → Burst + Falling 두 prefab 연계 확인. `MeteorFall.Launch(target, warningSec)` 이 trail 과 함께 정상 구동.

**Step 7:** 커밋

```bash
git add Assets/_Project/VFX/Meteor_Falling_SKELETON.prefab
git commit -m "feat(vfx): reskin Meteor_Falling_SKELETON (Fireball trail 기반)"
```

---

### Task 3.4: FireAOE + Fireball BuiltIn 제거

Task 1.8 패턴 2회:
- `Assets/PixPlays/ElementalAOE/FireAOE/Version_BuiltIn/`
- `Assets/PixPlays/ElementalProjectiles/Fireball/Version_BuiltIn/`

---

## Phase 4 — Portal 리스킨 (Water 베이스, 특수 케이스)

### Task 4.1: VFX_Uber_Water.mat 생성

Task 1.2 패턴. 기반: WaterBeam shaft Material.

---

### Task 4.2: Portal_SKELETON 계약 제약 재확인

**목적:** VfxSpawner.SpawnPortal 이 `transform.Find("Entry")`, `Find("Exit")`, `Find("LinkBeam")?.GetComponent<LineRenderer>()` 에 의존. 리스킨 후에도 이 3개 자식 이름 + LineRenderer 컴포넌트가 반드시 존재해야 함.

**Step 1:** 현재 Portal_SKELETON 의 Entry/Exit/LinkBeam 구조 Dump

```
UnityMCP.manage_prefabs({ action: "get_hierarchy", path: "Assets/_Project/VFX/Portal_SKELETON.prefab" })
```

**Step 2:** LinkBeam 자식에 `LineRenderer` 존재 확인. 없으면 리스킨 후 재부착 계획.

**Step 3:** 커밋 없음 (조사).

---

### Task 4.3: Portal_SKELETON 리스킨

**Files:**
- Modify: `Assets/_Project/VFX/Portal_SKELETON.prefab`

**Step 1:** Entry/Exit 자식에 WaterShield spawn 링 파티클 이식. 두 자식 이름 유지.

**Step 2:** LinkBeam 자식의 `LineRenderer` **유지**. WaterBeam shaft 파티클은 LinkBeam 자식 아래 시각 오버레이로 얹음.

**Step 3:** 모든 파티클 Material → `VFX_Uber_Water.mat`.

**Step 4:** 다운사이즈 티어 적용.

**Step 5:** Apply prefab.

**Step 6:** Play Mode 에서 Portal 스킬 발동 → entry/exit 링 + shaft + LineRenderer 포지션 모두 정상.

**Step 7:** 콘솔 확인. `SpawnPortal` 관련 null ref 발생 시 Step 2 로 돌아가 LinkBeam 구조 재검증.

**Step 8:** 커밋

```bash
git add Assets/_Project/VFX/Portal_SKELETON.prefab
git commit -m "feat(vfx): reskin Portal_SKELETON (WaterBeam + WaterShield 기반)"
```

---

### Task 4.4: WaterBeam + WaterShield BuiltIn 제거

Task 1.8 패턴 2회:
- `Assets/PixPlays/ElementalBeams/WaterBeam/Version_BuiltIn/`
- `Assets/PixPlays/ElementalShields/WaterShield/Version_BuiltIn/`

---

## Phase 5 — 최종 감사 & 일괄 정리

### Task 5.1: 동시 발화 회귀 테스트

**목적:** 4종 리스킨 후 종합 시나리오에서 성능 · 시각 회귀 없음 확인.

**Step 1:** Battle scene Play, 다음 시나리오 사용자 수행:
- Tornado 2개 + Meteor 1개 + Placement 1개 동시 발화
- 그 직후 Portal 추가 발동

**Step 2:** Android Profiler 수치가 임계선 내 유지 확인.

**Step 3:** 콘솔 에러 0.

**Step 4:** 수치를 design doc §6.4 측정 결과 섹션에 최종 기록 커밋.

---

### Task 5.2: 데모 씬 제거

**Files:**
- Delete: `Assets/PixPlays/Elemental*/Demo*Scene_BuiltIn.unity` (+ meta)
- Delete: `Assets/PixPlays/Elemental*/Demo*Scene_URP.unity` (+ meta)

**Step 1:** 각 파일 경로 열거

```
UnityMCP.find_in_file pattern: "Demo*Scene*.unity" (or glob)
```

**Step 2:** 각각 삭제 후 refresh + console check.

**Step 3:** 커밋

```bash
git commit -m "chore(vfx): remove PixPlays demo scenes (not required for production)"
```

---

### Task 5.3: unitypackage 중복 제거

**Files:**
- Delete: 각 `Elemental*/Elemental*_URP.unitypackage` (+ meta)

```bash
git commit -m "chore(vfx): remove duplicate PixPlays *_URP.unitypackage archives"
```

---

### Task 5.4: Demo 스크립트 제거

**Files:**
- Delete: `Assets/PixPlays/Components/Scripts/VFXTester.cs` (+ meta)
- Delete: `Assets/PixPlays/Components/Scripts/Character.cs` (+ meta)
- Delete: `Assets/PixPlays/Components/Scripts/BindingPoints.cs` / `BindingPointType.cs` / `IHittable.cs` (+ meta) — 데모 전용 추정, search_refs 0 확인 후

**Step 1:** 각 스크립트가 우리 코드에서 참조되지 않는지 grep

```
Grep({ pattern: "VFXTester|BindingPoints|IHittable", glob: "Assets/_Project/**/*.cs" })
```

Expected: 참조 0. 있으면 해당 스크립트 보류.

**Step 2:** 미참조 확정된 것만 삭제 → refresh → console.

**Step 3:** 커밋

```bash
git commit -m "chore(vfx): remove PixPlays demo-only scripts (VFXTester, Character, ...)"
```

---

### Task 5.5: Components_BuiltIn 제거

**Files:**
- Delete: `Assets/PixPlays/Components/Components_BuiltIn/`

Task 1.8 패턴.

---

### Task 5.6: residual-issues 갱신

**Files:**
- Modify: `docs/residual-issues.md`

**Step 1:** "VFX 파이프라인 개선 (PixPlays Ubershader 흡수 + 4종 리스킨)" 항목을 종결로 표시.

**Step 2:** Phase 9 이후 재검토 항목 추가:
- PixPlays `Components/Textures/` 를 우리 폴더로 이관 여부
- Ubershader 우리 쪽 복제본 생성 여부
- 미사용 원소 URP 프리팹(보관용) 실사용 확정 시 유지 / 미사용 확정 시 제거

**Step 3:** 커밋

```bash
git add docs/residual-issues.md
git commit -m "docs(vfx): close VFX pipeline 개선 항목 + 잔여 재검토 포인트 기록"
```

---

## Phase 6 — 종료 검증

### Task 6.1: 최종 Definition of Done 점검

Design doc §11 의 모든 항목 체크 — 에이전트가 직접 확인:

- [ ] 4개 `_SKELETON.prefab` guid/경로 불변 (`git log --follow` 로 확인)
- [ ] 4개 원소 Material (`Wind/Fire/Earth/Water`) + 2개 공용 헬퍼(`VFX_Dissolve_Mat`/`VFX_Glow_Mat`) 존재
- [ ] VfxSpawner.cs 변경 없음 (Portal 예외 발생 시 별도 합의된 경우에만 변경)
- [ ] Play Mode 5개 VFX 호출(Tornado, Placement, MeteorBurst, MeteorFall, Portal) 정상 + 콘솔 0 에러
- [ ] Android 측정 임계선 내
- [ ] PixPlays 각 카테고리 BuiltIn 폴더 + 데모 씬 + unitypackage 중복 + Demo 스크립트 제거
- [ ] design doc + plan doc + residual-issues 최신화

**Step 1:** 위 체크리스트를 실행. 미체크 항목 있으면 해당 Phase 로 복귀.

**Step 2:** 모두 통과 시 최종 완료 커밋 (또는 PR 생성).

---

## 실행 지침

- **Phase 1 파일럿 결과** 가 다음 Phase 들의 다운사이즈 티어를 결정하므로 Phase 1 을 완전히 끝내기 전 Phase 2 이후로 넘어가지 않는다.
- **각 Task 후 `UnityMCP.read_console`** 을 빠뜨리지 않는다. Unity import/compile 에러는 런타임까지 잠복한다.
- **prefab guid 보존** 원칙: 절대 `.prefab` 파일을 삭제 후 재생성하지 않는다. Instantiate → 내부 편집 → `ApplyPrefabInstance` 만 사용.
- **사용자 확인 게이트**: Task 1.5 / 1.6 / 5.1 은 반드시 사용자 수동 검증이 필요. 혼자 통과 선언 금지.
- **ECS 경계**: 본 작업은 Presentation 계층만. 어떤 Task 도 `EntityManager` / `SystemAPI` / Battle ECS 시스템을 건드리지 않는다.
- **Phase 범위**: 새 스킬 / VFX 타입 / Beams/Shields/Auras 활용 금지. 범위 이탈 시 정지 후 사용자 질문.
