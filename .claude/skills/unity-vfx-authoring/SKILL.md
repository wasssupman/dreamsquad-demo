---
name: unity-vfx-authoring
description: Use when authoring a new VFX (particle prefab + material) for this Unity 6.4 / URP 17 mobile project — Shuriken only, procedural-texture quad recipes, MCP-first tooling, offscreen-render verification, and the project's material/sorting/curve-mode rules. Pair with unity-vfx-integration for wiring.
---
# Unity VFX Authoring (rev 2026-09-12)

## Overview
이 프로젝트의 VFX 는 **Shuriken(내장 Particle System) 전용**이다. 산출물은 `.prefab + .mat (+ 절차 텍스처/메시)` 이고
저작·검증은 UnityMCP 로 에디터 안에서 끝낸다. 이 문서는 **어떤 레시피로, 어느 도구로, 무엇을 검증하고, 어떤 함정을
피하는가**를 담는다. 효과 목록과 톤은 `common-skill-vfx-reference.md`(카탈로그 + 프리팹 인벤토리)가 정본이다.

## The Iron Laws
1. **VFX Graph 금지 · Shader Graph JSON 직접 생성 금지.** VFX Graph 는 compute shader + SSBO 필수라 Android 는 Vulkan 에서만
   돌고 OpenGL ES 는 부적합(타겟이 GLES 3.0+ 실기기). Shader Graph 는 공개 생성 API 가 없다(2026-09 기준). 둘 다 재검토 조건 =
   타겟 최소 사양이 Vulkan 전용으로 바뀔 때.
2. **`Shader.Find + new Material` 금지.** 런타임에 만드는 머티리얼은 `Wassup.Rendering.RuntimeMaterialFactory.CreateOpaque /
   CreateTransparent / *Texture` 만(모바일 셰이더 스트리핑으로 null 이 돌아온다). 색은 `ApplyColor`(_BaseColor·_Color·color 전부).
   새 런타임 셰이더는 `Assets/_Project/Shaders/` 에 두고 `Assets/Resources/RuntimeMaterials/` 에 머티리얼 등록.
3. **카탈로그 항목은 사용자 승인 뒤에만 추가.** 없으면 draft 로 제안하고 멈춘다.
4. **저작이 곧 검증이다.** 오프스크린 렌더 없이 「만들었다」고 하지 않는다(아래 검증 절차).

## `_SKELETON` 접미사 규칙 (rev 2026-09-12 — 현실에 맞게 재정의)
- 접미사 = **「사용자 승인 전 / 실험 중」 표식**이다. 승인이 나면 다음에 그 프리팹을 손댈 때 `AssetDatabase.MoveAsset` 로 접미사를
  뗀다(GUID 유지 → SO/씬 참조 무변). 일괄 리네임은 하지 않는다 — 다른 세션의 WIP 와 충돌한다.
- 접미사 없는 프리팹은 「승인됨/운용 중」으로 읽는다. 옛 규칙(「접미사 없이는 저장 금지」)은 24/35 프리팹이 어겨 사문화됐다.
- 인벤토리(`common-skill-vfx-reference.md`)에 승인 상태를 적는다. 파일명만으로 상태를 추론하지 않는다.

## When to Use
- 새 효과를 만들거나(참격 자국·표식·오라·폭발·궤적) 기존 효과를 재작업할 때
- 벤더 VFX 를 `_Project` 로 사본·스트립해 쓸 때
- 카탈로그 초안을 쓰거나 인벤토리를 갱신할 때

## 레시피 3종 (이 프로젝트에서 실제로 쓰는 것)
| 레시피 | 언제 | 구성 | 선례 |
| --- | --- | --- | --- |
| **A. 스프라이트 빌보드** | 스파크·퍼프·링·글리프 | `renderMode Billboard`, 알파/가산, 버스트 1~2발 | `DetectionMark_SKELETON`, `DamageNumberSpark_SKELETON` |
| **B. 절차 텍스처 + 쿼드 메시** | **지면에 눕는 도형**(배치 링, 방향이 있는 것) | `Texture2D.SetPixels → EncodeToPNG` 로 텍스처, `Mesh` 에셋(꼭짓점 원점·+Y 전방), `renderMode Mesh`, `alignment Local`, `startRotation3D x=90°`, 알파 블렌드, `scalingMode Hierarchy`(크기는 호출부 `scale` 인자) | — |
| **B′. 실시간 메시 + 램프 텍스처** | 지면 도형이 **판정 데이터를 따라야 할 때**(참격 부채꼴/띠) | 모양은 굽지 않는다 — `Presentation/ShapeMeshBuilder`(배치 가이드와 같은 빌더)가 bake·사거리·내 몸에서 메시를 만들고 `CoreProjectileViewPool.GetShapeMarkMesh` 가 캐시, `PlayHit(meshOverride:)` 로 교체. 채움/테는 `uv.x` 0/1 + 4×1 램프 텍스처(정점색은 파티클 색 스트림에 덮여 못 쓴다). 파티클 모듈은 B 와 같고 크기는 `scale 1`(메시가 월드 단위) | `SlashMark_SKELETON` |
| **C. 벤더 사본 스트립** | 이미 좋은 벤더 팩(PixPlays·GA·WALLCOEUR)이 있을 때 | `_Project` 로 복사 → 무버/RB/Collider/제어 스크립트 제거 → 정렬·자세·활성 그룹 수동 | `StatusAura_*`, `Burnout_Smoke`, `BusterBeam`, `WeaponTrail_*` |

레시피 B 를 고르는 기준: **카메라 pitch(배치 55°/전투 ~50°)에 파편 팬이 눌려 발밑 빛으로만 보인다.** 지면 도형은 쿼드에
텍스처로 그린다(참격 1차가 정확히 이 이유로 「거의 안 보인다」 판정을 받았다). 크기 커브는 **상수 1**(첫 프레임부터 정확).
**판정 도형과 맞춰야 하는 그림은 B 가 아니라 B′** — 텍스처·메시에 구운 각도/비율은 저작이 바뀌면 조용히 거짓말이 된다
(참격 2차: 반각 30° 텍스처 · 3:1 띠 메시가 그랬다, directional-attack-shape unit 7 에서 절차화).

## 도구 매핑 (UnityMCP 10.x)
| 작업 | 1순위 | 2순위 |
| --- | --- | --- |
| 파티클 생성·모듈 설정 | `manage_vfx particle_create / set_main / set_emission / set_shape / set_renderer / set_material / set_mesh / set_texture / set_color_over_lifetime / set_size_over_lifetime / set_velocity_over_lifetime / set_noise / add_burst / enable_module` | `execute_code`(method body · using 금지 · 타입 풀네임) — 모듈이 도구에 없거나 서브 파티클/부모 구조가 필요할 때 |
| 절차 텍스처 | `manage_texture create / apply_gradient / apply_noise / set_pixels` | `execute_code` 로 `Texture2D` 직접(부채꼴·띠처럼 기하가 필요할 때) |
| 메시 에셋 | `execute_code` (`new Mesh` + `AssetDatabase.CreateAsset`) | — |
| 머티리얼 | `manage_material create / set_material_shader_property` (URP Particles/Unlit) | `.mat` YAML 은 손대지 않는다 |
| 프리팹 저장 | `manage_prefabs` 또는 `PrefabUtility.SaveAsPrefabAsset` (`execute_code`) | — |
| AI 텍스처 | `generate_image`(BYOK) — Visual Direction(캐주얼·소형 가독성·배경 검정 X) 프롬프트 규칙 준수 | — |
| 동결·캡처 | `particle_set_time` / `ps.Simulate(t, true, true)` + `ScreenCapture` 또는 RenderTexture | — |

⚠ `execute_code` 는 CodeDom(C# 6)이다 — `default` 리터럴·튜플·로컬 함수 불가. `AssetDatabase.DeleteAsset` 은 safety_checks 에 막힌다.

## Workflow
1. **목적·형 정의**: 즉시 피드백 / 지속 루프 / sim 완료 시점(hitDelaySec 뒤) 중 무엇인지, 원점(공격자 발밑 / 대상 / 자리)과
   방향(있다면 무엇이 정하나 — `attackVfxFacesTarget` 은 **재생 시점**에 방향을 다시 잰다)을 적는다.
2. **카탈로그 대조**: `common-skill-vfx-reference.md` 에서 가장 가까운 항목/프리팹을 찾는다. 없으면 draft 제안 후 승인 대기.
3. **레시피 선택**(위 표) → 도구로 저작. 4개 필수값을 결정한다: `Duration`, `StartColor`, `MaxParticles`, `Loop`.
4. **오프스크린 렌더 시트**(필수): 먼 좌표(5000,0,5000)에 Instantiate(HideAndDontSave) → `Simulate(피크 0.05~0.15s)` →
   임시 카메라 → RenderTexture → PNG → Read 로 육안. 방향 효과는 +X·+Z·대각 3방향, 카메라는 **실제 pitch** 로. 배경은 실제
   게임 톤(중간 명도) — 순검정은 관대하게 보인다. 후보 비교는 일렬 라인업 + 같은 t 동결.
5. **라이브 캡처**(권장): Play → `SceneTransition.Go(Battle)` → `StartBattle()` → 필요 시 `TimeManager.Request(Battle, 0f)` 동결 →
   배치/발동 → `EditorApplication.update` 콜백에서 특정 파티클 이름이 `isPlaying` 되는 순간 +0.1s 에 `ScreenCapture`.
   ⚠ 캡처는 프레임 끝에 찍힌다 — 같은 콜백에서 상태를 바꾸면 바뀐 뒤가 찍힌다. 판이 끝나면(적 0) 캡처가 조용히 실패한다.
6. **에셋 lane 테스트**: `ParticleCurveModeConsistencyTests`(다축 커브 모드) + 해당 효과 저작 테스트(있다면). 새 프리팹은 반드시 통과.
7. **인벤토리·카탈로그 갱신** + handoff(`prefab-skeleton-template.md` 양식) → integration 스킬로.

## Red Flags (이 프로젝트에서 실제로 난 사고)
- **다축 모듈의 축별 커브 모드 불일치**(velocity/force/limit: y 만 Curve, x·z Constant) — 에디터는 멀쩡, 재생 시 콘솔 에러 폭주.
  상수 축도 `AnimationCurve.Constant` 로 승격. 전역 가드 = `ParticleCurveModeConsistencyTests`.
- **`CoreVfxSpawner.ConfigureOneShot` 이 emission 을 덮어쓴다**(t0 버스트 최소 4). shape 를 켜면 글리프가 넷으로 보인다. 저작 시점엔
  안 보이고 라이브에서만 깨진다 — 원샷 경로 프리팹은 버스트 수를 이 하한과 맞춘다.
- **벤더 프리팹 3종 함정**: ① 단계 그룹이 `activeSelf=false` 로 와서 스크립트를 떼면 아무도 안 켠다 ② `sortingOrder 0~2` 로 와서
  보드 유닛(수백대) 뒤에 깔린다 — 자체 풀이면 `BoardSortOrder` 로 직접 올린다 ③ 자세는 코드로 추측하지 말고 데이터 knob
  (`attackVfxEulerOffset`, 계산 회전 **뒤에** 곱함). 방향은 up 을 고정하고 yaw 만(`LookRotation` 에 방향을 그대로 주면 기운다).
- **벤더 flipbook 텍스처는 「퍼프 한 무더기」다** — 작게 여러 개 뿌리면 돌조각으로 읽힌다. 크게·적게. `gravityModifier` 는 배율(×9.81).
- **`TrailRenderer.autodestruct`** 는 풀링과 충돌(재사용 GO 가 트레일을 잃는다) — 반드시 false. 투사체 벤더 사본은
  `emitterVelocityMode = Transform`. 트레일은 `Simulate` 가 안 먹어 라이브로만 본다.
- **카메라 pitch 에 눌리는 파편 팬** — 지면 도형은 레시피 B. `View` 정렬 원반은 pitch 에 뭉개지고, `Local` 정렬 쿼드가 눕는다.
- **MaxParticles 로 밀도 문제를 감추기** — 일반 50 / 임팩트 100 / 배경·오라 200 상한. 큰 반투명 쿼드 다층 중첩은 overdraw 경고를 남긴다.
- **Texture Sheet Animation 은 기본값이 아니다** — 벤더 사본(연기·화염)에서만 쓴다. 새 저작에서 flipbook 이 필요하면 근거를 적는다.
- **카탈로그 항목을 승인 없이 추가** · `.shadergraph` 를 텍스트로 생성 · `.mat` YAML 을 손으로 편집.

## Rationalization Table
| Topic | Default | Why |
| --- | --- | --- |
| Runtime path | Shuriken | GLES 3.0+ 안드로이드 실기기가 주 타겟 |
| 지면 도형 | 절차 텍스처 + 쿼드 메시(Local) | 파편 팬은 pitch 에 눌린다(실측) |
| 머티리얼 | URP Particles/Unlit `.mat` 에셋 · 런타임은 `RuntimeMaterialFactory` | 스트리핑 null · 팩토리 규약 |
| 검증 | 오프스크린 시트 → 라이브 캡처 → 에셋 lane | Play 는 포커스·판 종료에 취약, 정적 검사는 커브 모드를 못 본다 |
| 접미사 | 승인 전 `_SKELETON`, 승인 후 다음 편집 때 제거 | 일괄 리네임은 병행 세션과 충돌 |
| Budget | General 50 / Impact 100 / Background 200 | 모바일 상한 |

## Quick Reference Checklist
- 카탈로그/인벤토리에 항목이 있고 승인 상태가 적혔는가
- 레시피(A/B/C)와 원점·방향·타이밍(즉시 / hitDelaySec 뒤)이 handoff 에 있는가
- 4개 필수값(Duration · StartColor · MaxParticles · Loop)이 적혔는가
- 오프스크린 시트(방향 효과는 3방향)와 라이브 캡처가 있는가
- `ParticleCurveModeConsistencyTests` 초록인가
- 런타임 머티리얼이 `RuntimeMaterialFactory` 경유인가 · `.mat` 이 `_Project/VFX` 아래 있는가
- 벤더 사본이면 활성 그룹·정렬·자세·autodestruct 를 확인했는가
- 원샷 경로면 `ConfigureOneShot` 버스트 하한(4)과 맞췄는가

## Handoff
산출물 = `.prefab + .mat (+ png/mesh)` + 오프스크린 시트 PNG 경로 + `prefab-skeleton-template.md` 양식의 메모.
Renderer 슬롯·SerializeField·코어 사건 구독은 `unity-vfx-integration` 스킬.
