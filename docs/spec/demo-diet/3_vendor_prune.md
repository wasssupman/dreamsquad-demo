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
- 셰이더·`.cginc`·`.hlsl` 은 `GetDependencies` 에 안 잡힐 수 있다(머티리얼 → 셰이더는 잡히지만 셰이더 → include 는 텍스트 참조). 남는 셰이더의 `#include` 를 grep 으로 폐포에 더한다.
- Spine Examples 의 적 5종(`Enemy_Dragon` · `Slime`×3 · `Whirlpot`) 스켈레톤은 `.skel/.atlas/.png` + `SkeletonDataAsset`·`AtlasAsset`·머티리얼이 한 세트 — 폐포가 전부 포함하는지 확인.
- Always Included Shaders(`GraphicsSettings`)에 벤더 셰이더가 있으면 그것도 루트로 친다.

## 완료 기준

- [ ] 벤더 6개 용량 전/후 표(목표: 합계 300 MB → 수십 MB)
- [ ] 검증 워크트리 배치: 임포트 에러 0 · missing reference 0 · EditMode 전체 초록
- [ ] `BattleCoreScene` Play 에서 투사체 VFX · 무기 궤적 · 구조물(KayKit) · 적 Spine 5종이 그려짐 — 6.6 전환 뒤 사용자 에디터 육안(그 전까지는 배치 임포트 무에러로 갈음하고 명시)
- [ ] 임시 에디터 스크립트 미커밋 확인 · 커밋(벤더별)
