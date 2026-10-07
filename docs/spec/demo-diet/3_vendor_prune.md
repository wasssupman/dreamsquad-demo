# 3 — 벤더 참조 폐포 추림

## 목적

남는 벤더 7개(GabrielAguiarProductions 148 MB · PixPlays 106 · Hovl Studio 26 · KayKit 24 · VFXPACK_FIRE 6 · Spine Examples 2 · **Layer Lab 33 — 단위 2 에서 이관**: 디펜더 23 · 적 19 의 Spine 스켈레톤 `2D Art Maker/AMCasual Character/Demo/SpineAnimation/Casual Character_SkeletonData.asset` 세트와 에디터 임포터 `LayerLabPresetImporter` 가 쓰는 `LayerLab.ArtMaker` 런타임 스크립트가 폐포)에서 **프로젝트가 실제로 참조하는 에셋과 그 의존 폐포**만 남긴다. 데모 씬·미사용 프리팹·텍스처를 지운다.

## 변경 대상

- `Assets/{GabrielAguiarProductions,PixPlays,Hovl Studio,KayKit,VFXPACK_FIRE_WALLCOEUR,Spine Examples,Layer Lab}/**` — 폐포 밖 파일 삭제. Layer Lab 의 `Demo/Map/*.png`(6.6 재임포트 churn 33 파일이 바로 이것) · 데모 프리팹 · 데모 씬이 우선 후보.
- 벤더 `.cs`(GabrielAguiar 8 · PixPlays 21 · Hovl 6)는 **남는 프리팹이 쓰는 컴포넌트만** 유지. 데모 씬 전용 컨트롤러(`SaveParticleSystemScript` 등)는 삭제.
- `Assets/Spine/`(런타임)는 추리지 않는다 — 패키지 단위 유지.

## 구현

1. 임시 에디터 스크립트 `Editor/VendorPruneReport.cs`(이 단위 안에서만 존재, 커밋하지 않음)로 `AssetDatabase.GetDependencies(path, recursive: true)` 를 **루트 = `Assets/_Project/**` 전체 + `BattleCoreScene` + `ProjectSettings` 참조**에 대해 돌려 벤더 경로의 폐포 집합을 파일로 뽑는다. 배치 `-executeMethod` 로 실행(검증 워크트리에서).
2. 벤더 루트별로 「전체 − 폐포」 목록을 만들어 사용자에게 **삭제 목록과 용량**을 보고한 뒤 `git rm`. 벤더당 커밋 1.
3. 삭제 뒤 같은 스크립트로 missing reference 0 을 확인하고 스크립트를 지운다.
4. 폴더 `.meta` 와 빈 폴더 정리.

## 주의

- 단위 2 의 GUID 역참조 색인(세션 스크래치 `guidrefs.py`)이 `GetDependencies` 없이도 폐포를 준다 — 루트 = `Assets/_Project` + `Assets/Resources` + `ProjectSettings` 의 참조에서 벤더 경로를 재귀로 따라간다. 에디터 스크립트는 교차 검증용.
- 다이어트 전부터 끊긴 GA 투사체 프리팹 10건(`97a0a4fe…`·`efd26487…`)은 그 프리팹이 폐포에 남으면 끊긴 필드를 비우거나 원본을 찾는다.
- **ShaderGraph 의 서브그래프·텍스처 참조는 escaped JSON guid** — YAML `guid: ` 정규식으로는 안 보인다. 32-hex 토큰 전부를 참조로 본다.
- 셰이더·`.cginc`·`.hlsl` 은 `GetDependencies` 에 안 잡힐 수 있다(머티리얼 → 셰이더는 잡히지만 셰이더 → include 는 텍스트 참조). 남는 셰이더의 `#include` 를 grep 으로 폐포에 더한다.
- Spine Examples 의 적 5종(`Enemy_Dragon` · `Slime`×3 · `Whirlpot`) 스켈레톤은 `.skel/.atlas/.png` + `SkeletonDataAsset`·`AtlasAsset`·머티리얼이 한 세트 — 폐포가 전부 포함하는지 확인.
- Always Included Shaders(`GraphicsSettings`)에 벤더 셰이더가 있으면 그것도 루트로 친다.

## 구현 결과 (2026-10-07)

에디터 스크립트 대신 단위 2 의 GUID 역참조 색인(세션 스크래치 `guidrefs.py closure`)으로 폐포를 구했다: 루트 = 벤더 밖 모든 텍스트 에셋(`Assets/_Project` · `Resources` · `Settings` · `Spine` · `Plugins` · `TextMesh Pro`) + `ProjectSettings` 의 guid 참조 → 벤더 안으로 재귀(에셋 본문 + `.meta` 의 guid, 셰이더 `#include`). 코드는 별도로 **컴파일 폐포**(벤더 `.cs` 의 타입 이름을 식별자 매칭으로 추적 — 우리 코드가 쓰는 `PixPlays.ElementalVFX.VfxData/BaseVfx` · `LayerLab.ArtMaker.PartsType` · `Hovl` 포함)를 더했고, asmdef(`Hovl.HSFiles`)와 우리 에디터 베이커(`ProjectileTextureBaker`)가 경로로 읽는 PixPlays 노이즈 텍스처는 손으로 남겼다. **첫 추림이 놓친 것 하나**: ShaderGraph 는 참조 guid 를 YAML `guid: ` 가 아니라 escaped JSON(`\"guid\":\"…\"`)으로 적는다 — 서브그래프 3(`DistortTexture`·`ScrollTexture`·`UberShaderSoftFade`)이 지워져 `Ubershader`·`FoamShader` 임포트가 NRE 로 실패했고, 복구했다. 색인 도구는 `.shadergraph/.shadersubgraph` 에서 32-hex 토큰 전부를 참조로 보도록 고쳤다.

| 벤더 | 파일 전 → 후 | MB 전 → 후 | 남긴 .cs |
|---|---|---|---|
| GabrielAguiarProductions | 343 → 216 | 152.8 → 72 | `ParticleSystemController` + `SaveParticleSystemScript`·`Serializables`(컨트롤러가 참조) |
| PixPlays | 364 → 175 | 109.1 → 69 | VfxSystem 7(`BaseVfx`·`VfxData`·`VfxReference`·`Shield`·`WindAoeVfx`·`LocationVfx`·`ParticleSystemVfx`·`PlayableVfx`) + `BindingPoint*`·`Character`·`IHittable` |
| Hovl Studio | 125 → 39 | 26.2 → 7 | `HS_SwordMeshTrail`·`HS_SwordTrailAnimationEvents`·`HS_SwordTrailPreset` + asmdef |
| KayKit | 1,058 → 14 | 19.8 → 1 | — |
| VFXPACK_FIRE_WALLCOEUR | 24 → 8 | 5.8 → 3 | — |
| Spine Examples | 29 → 28 | 1.3 → 2 | — |
| Layer Lab | 1,737 → 31 | 15.8 → 2 | 스켈레톤 세트 12 + `PartsManager`·`PresetData`·`CharacterPrefabData` 와 그 식별자 폐포 19 |

삭제 3,172 파일(+meta · 빈 폴더 meta 161) · `Assets` 700 → 510 MB. 지운 벤더 `.cs` 25: GA UniqueProjectiles 데모 4 + 커스텀 에디터 1 · Hovl 데모 2 · Layer Lab Editor 3 + 데모 5 · PixPlays 데모/애드온/미사용 Vfx 10. 삭제 후 색인 재생성: 새로 끊긴 참조 0. 남은 missing 129건(GA 머티리얼 36 등 12 guid)은 HEAD 트리에도 없던 것 — 벤더 팩이 원래 들고 오지 않은 셰이더·텍스처라 손대지 않는다(백로그).

6.6 이 벤더 `.meta` 에 넣은 `AssetOrigin` 블록(에셋 스토어 출처, 결정적 마이그레이션)은 남는 파일의 것만 같이 실었다.

## 완료 기준

- [x] 벤더 7개 용량 전/후 표 — 합계 331 → 156 MB(du 기준 `Assets` 700 → 510)
- [x] 사용자 에디터(6.6) 컴파일 에러 0 · 서브그래프 복구 뒤 셰이더그래프 재임포트 확인 · missing reference(새로 끊긴 것) 0. Play 중 `Bone not found: Gear`(ShieldShuttle, 선행 상태)와 Hovl 씬 열기 예외 1회는 다이어트와 무관 — 백로그
- [ ] `BattleCoreScene` Play 에서 투사체 VFX · 무기 궤적 · 구조물(KayKit) · 적 Spine 5종이 그려짐 — 사용자 에디터 육안(사용자 몫)
- [x] 임시 에디터 스크립트 없음(색인 스크립트는 세션 스크래치) · 커밋(벤더별 7)
