# 2 — 아웃게임 에셋 · 컷신 · 씬 · Layer Lab 삭제

## 목적

코드가 사라진 뒤 고아가 된 에셋과 사용자 결정(Q1 컷신)으로 지우기로 한 에셋을 제거한다. GUID 참조 0 을 증명하고 지운다.

## 변경 대상 (삭제)

| 묶음 | 경로 | 파일 |
|---|---|---|
| 씬 | `Scenes/OutgameScene.unity` · `MapTest.unity` · `FluidScratch.unity` · `Scenes/BattleScene/`(폴더 잔재) · `Assets/Scenes/Prototype.unity` · `InitTestScene*` | 4+ |
| 로비 그래픽 | `Sprites/hello/`(147) · `Sprites/world/`(130) · `Sprites/Keyring/`(4) · `Prefabs/Outgame/`(Hello·World) · `Art/LobbyIcons/`(8) | ~291 |
| 컷신 (Q1) | `Sprites/Cutscene/`(7 디펜더 × 49 프레임 = 327) · `DefenderUnitData.deployCutsceneFrames/Fps/Scale/Offset` 필드 4 + `DragSwaySettings.enableDeployCutscene` · 디펜더 SO 27개의 해당 YAML 줄 · `Data/Flipbook` 중 컷신 전용이 있으면 함께 | 327 + 필드 |
| SO | `Data/PlayerProfile.asset` · `Data/Config/{LobbyKeyringSettings,KeyringStyleHologram}.asset` · `Data/Config/TestModeConfig.asset`(단위 0 에서 타입 삭제됨) · `Data/{KeyringStyle,LobbyKeyringSettings,DragSwaySettings?}.cs` 타입(드래그 스웨이가 컷신 외 용도가 있으면 필드만) | ~6 |
| 셰이더 | `Shaders/Background_Dissolve_UI.shader` · `KeyringHologramCommon.hlsl` · `UICordHologram` · `UICordShine` · `WorldCordHologram` · `Art/LobbyBackgroundDissolve.mat` | 6 |
| 참조 0 확인 후 | `Art/UI/`(13) · `Art/Season/`(4) · `Art/Dreamstones/`(4 — 드림스톤 아이콘이 전투 HUD 에 안 나오면) · `Characters/`(2) · 셰이더 `DraftCardFoil_UI` · `SlotRimFlow_UI`(머티리얼 참조 추적) | ≤25 |
| 벤더 | `Assets/Layer Lab/` 전체(33 MB, 피참조 0) | 1,737 |
| 설정 | `ProjectSettings/EditorBuildSettings.asset` → `BattleCoreScene` 하나만. `GraphicsSettings` Always Included 에서 삭제 셰이더 제거 | 2 |

## 구현

- 삭제 묶음마다 **GUID 역참조 스크립트**로 `Assets/_Project` + `ProjectSettings` 를 훑어 참조 0 을 확인한 뒤 `git rm`. 참조가 있으면 그 참조자가 KEEP 인지 먼저 본다(KEEP 이면 이 에셋도 KEEP — 분류 오류로 기록).
- `DefenderUnitData` 필드 삭제 후 디펜더 SO 27개의 고아 YAML 줄은 **같은 커밋에서** 스크립트로 정리한다(depth-parallax-removal 단위 1 선례).
- `EditorBuildSettings` 는 열린 씬이 아니라 YAML 직접 편집 가능.
- 용량 측정: 삭제 전후 `du -sh Assets/_Project/{Sprites,Art}` · `Assets` 를 기록한다.

## 구현 결과 (2026-10-07 — 6.6 에디터 위에서)

GUID 역참조 색인(세션 스크래치 `guidrefs.py` — `.meta` 의 guid → 경로, 텍스트 에셋 2,204개 + `ProjectSettings` 의 `guid:` 전수)으로 묶음마다 **바깥 참조**와 **이미 끊긴 참조(missing)** 를 센 뒤 지웠다.

**지운 것 (1,325 파일, meta 포함)**
- 씬 4: `OutgameScene` · `MapTest` · `FluidScratch` · `Assets/Scenes/Prototype`(폴더째). 빈 `Scenes/BattleScene/` 폴더 잔재 제거. `EditorBuildSettings` → `BattleCoreScene` 1개.
- 로비 그래픽: `Sprites/hello` 152 · `Sprites/world` 133 · `Prefabs/Outgame` 2(missing script 상태였음) · `Art/LobbyIcons` 8.
- 컷신(Q1): `Sprites/Cutscene` 327 + `DefenderUnitData.deployCutscene{Frames,Fps,Scale,Offset}` + `DragSwaySettings.{enableDeployCutscene,deployCutsceneSwipeRefSpeed,deployCutsceneSwipeSmoothing}`(헤더 ④⑤ 제거, 이후 번호 당김) + 디펜더 SO 27개의 YAML 435줄 + `DragSwaySettings.asset` 3줄.
- SO·프리팹: `Data/PlayerProfile.asset` · `Data/Hazards/CParticle.asset`(Hazards 폴더에 잘못 들어간 빈 `PlayerProfileSO` 인스턴스) · `Resources/SceneTransition.prefab`.
- 셰이더·머티리얼: `Background_Dissolve_UI.shader` + `Art/LobbyBackgroundDissolve.mat` · `DraftCardFoil_UI.shader`(머티리얼 0, `GraphicsSettings` Always Included 에서도 제거).
- 참조 0 확인 후: `Art/UI` 16 · `Art/Season` 9 · `Characters/Deco.png`.
- `BattleCoreScene.unity`(에디터에 열려 있지 않아 YAML 직접 편집): `ResultScreen` 오브젝트(GO·missing-script MonoBehaviour·RectTransform + 부모의 child 항목) 제거 · 고아 필드 5줄(`BattleDriver._profile` · `CoreMenuPopup._outcome` · `CoreHudGate._outcome` · `CoreMatchEndBeat._resultScreen/_profile`) 제거 · `m_EditorClassIdentifier` 를 `CoreMatchEndBeat` 로. 54줄.

**재분류 (조사표와 다른 점)**
- **Layer Lab → 단위 3 추림**: 「피참조 0」이 아니었다. `Layer Lab/…/SpineAnimation/Casual Character_SkeletonData.asset` 이 **디펜더 23 · 적 19 의 `skeletonDataAsset`** 이다 — 전투 유닛 외형의 Spine 스켈레톤. 통째 삭제 불가. 단위 3 에서 이 스켈레톤 세트(.skel/.atlas/.png · AtlasAsset · 머티리얼)와 에디터 임포터(`LayerLabPresetImporter`)가 쓰는 `LayerLab.ArtMaker` 런타임 스크립트만 남기고 데모(Map png 등)·프리팹을 추린다.
- **키링 → KEEP**: `CoreRetireFlightPresenter` 가 `DragSwaySettings.style.ringSprite/worldRingMaterial/worldCordMaterial` 을 읽는다(되돌리기 비행 연출). `Sprites/Keyring` 4 · 홀로그램 셰이더 3 + `KeyringHologramCommon.hlsl` · `Art/Keyring*.mat` 4 · `Data/Config/KeyringStyleHologram.asset` 전부 유지.
- **KEEP**: `Art/Dreamstones`(드림스톤 SO 의 `icon`) · `Characters/SkeletonFlipX.asset`(CH4 Spine 이 참조) · `SlotRimFlow_UI.shader`(`BattleHudTrayConfig` → `SlotRimFlow.mat`).
- 다이어트 전부터 끊겨 있던 참조 15건은 손대지 않았다: GA 투사체 프리팹 10(`97a0a4fe…`·`efd26487…`) · `Meteor_Falling_SKELETON` · `MapDocument_MovementStress` · 시즌 SO 3(`backdrop` placeholder guid). 단위 3 벤더 추림 때 GA 쪽을 같이 본다.

**용량**: `Assets` 803 → 700 MB · `_Project` 440 → 337 MB · `Sprites` 84 → 24 MB · `Art` 247 → 205 MB.

## 완료 기준

- [x] 지운 에셋의 GUID 를 참조하는 파일 0(`Assets` · `ProjectSettings`) — 삭제 후 색인 재생성, 새로 끊긴 참조 0
- [x] 사용자 에디터(6.6): 재임포트(12초)·컴파일(5초) 에러 0, missing script 0(배치 검증은 사용자 결정으로 생략)
- [x] `EditorBuildSettings` 씬 목록 = `BattleCoreScene` 1개
- [x] 용량 전/후 기록
- [ ] 커밋(경로 지정) — 컷신 / 아웃게임 에셋·씬 정리 두 커밋. Layer Lab 은 단위 3
