# GA Projectile Strip Tool

**작업 구분**: 0

## 목적

GA 벤더 투사체 프리팹을, 우리 ECS-driven 파이프라인이 그대로 쓸 수 있는 **view-only 파생 프리팹**으로 변환하는 에디터 툴을 만든다. 무버/물리를 떼고 시각 컴포넌트만 남기며, RB 부재 하에서도 velocity 기반 시각이 as-is 유지되도록 파티클 속도 소스를 Transform 으로 고정한다.

## 변경 대상

- New: `Assets/_Project/Editor/GaProjectileStripper.cs` (에디터 전용)
- Output: `Assets/_Project/VFX/Projectiles/GA/{프리팹명}.prefab` (폴더 없으면 생성)

## 구현

- 메뉴 `Wassup/VFX/Strip GA Projectile (Selection)`. Project 창에서 선택한 프리팹(들) 대상. `MenuItem` + `Selection.GetFiltered<GameObject>(...)`.
- 각 프리팹 처리:
  - `PrefabUtility.LoadPrefabContents(path)` → root+자식 전체 순회.
  - **제거**(타입 지정, asmdef 독립적으로 문자열 매칭 병행):
    - `GetComponentsInChildren<MonoBehaviour>(true)` 중 `GetType().Name == "ProjectileMoveScript"` → `Object.DestroyImmediate`.
    - `GetComponentsInChildren<Rigidbody>(true)` 전부.
    - `GetComponentsInChildren<Collider>(true)` 전부.
  - **속도 소스 고정**: `GetComponentsInChildren<ParticleSystem>(true)` 각각 `var m = ps.main; m.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;`
  - **감사 로그**(Debug.Log): 각 PS 에 대해 velocity 의존 여부 = `ps.inheritVelocity.enabled || (emission.rateOverDistance 상수>0) || (renderer.renderMode == Stretch)`. 의존 PS 이름을 나열(RB 제거로 영향받았을 뻔한 시스템 가시화).
  - `PrefabUtility.SaveAsPrefabAsset(root, outPath)` → `PrefabUtility.UnloadPrefabContents(root)`.
- **유지 검증**: URP `UniversalAdditionalLightData`, `Light`, `ParticleSystem(Renderer)`, `TrailRenderer`, `MeshRenderer/Filter` 는 건드리지 않는다.
- 다중 선택 시 각각 처리하고 요약 카운트 로그.

## 완료 기준

- `vfx_Projectile_Arrow01` 선택 → 메뉴 실행 → `Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow01.prefab` 생성.
- 결과 프리팹: `ProjectileMoveScript`/`Rigidbody`/`Collider` **0개**, ParticleSystem 4 / TrailRenderer 3 / Light 1 (+URP LightData) **유지**, 전 PS `emitterVelocityMode == Transform`.
- 감사 로그 출력(Arrow01 은 velocity 의존 PS 0 이라 "없음" 로그 예상).
- `read_console` Error/Warning 0. (검증: MCP 로 결과 프리팹 컴포넌트 목록 조회.)

확인 2026-07-03 — 구조 검증 PASS: Arrow01 → mover·rigidbody·collider 0 / ParticleSystem 4·TrailRenderer 3·Light 1 유지 / 전 PS `emitterVelocityMode=Transform` / 잔존 vendor 스크립트 없음. code-review APPROVE-WITH-NITS(출력 충돌 가드·자기 덮어쓰기 스킵·후보 분모·잔존 MB 감사 반영). 컴파일·콘솔 클린.
