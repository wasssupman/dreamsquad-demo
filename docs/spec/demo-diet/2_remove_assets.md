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

## 완료 기준

- [ ] 지운 에셋의 GUID 를 참조하는 파일 0(`Assets` · `ProjectSettings`)
- [ ] 검증 워크트리 배치: 임포트 에러 0 · missing script/reference 0 · `EditMode.Assets` 초록(스냅샷 무변) · EditMode 전체
- [ ] `EditorBuildSettings` 씬 목록 = `BattleCoreScene` 1개
- [ ] 용량 전/후 기록
- [ ] 커밋(경로 지정) — 벤더 Layer Lab 은 별도 커밋
