# Spine Unity 4.2 → 4.3 업그레이드 재현 가이드

> 목적: 다른 PC 또는 새 작업 세션에서 저장소를 새로 받은 뒤, 이 프로젝트의 Spine Unity 4.3 업그레이드를 안전하게 재현한다.
>
> 기준일: 2026-08-11
> 검증 대상 Unity: `6000.4.7f1`
> 목표 런타임: `spine-unity-4.3-2026-08-04.unitypackage` / spine-unity `4.3.102` / spine-csharp `4.3.39`

## 먼저 읽을 공식 문서

- [Spine Unity 4.3 split component upgrade guide](https://github.com/EsotericSoftware/spine-runtimes/blob/4.3/spine-unity/Assets/Spine/Documentation/4.3-split-component-upgrade-guide.md)
- [Spine Unity Assets — export/import와 Alpha workflow](https://ko.esotericsoftware.com/spine-unity-assets)
- [Spine Unity Rendering](https://ko.esotericsoftware.com/spine-unity-rendering)
- [Spine 4.2 → 4.3 포럼 가이드](https://esotericsoftware.com/forum/d/29234-spine-unity-42-to-43-upgrade-guide)

공식 split 가이드의 핵심 순서는 다음과 같다.

1. 백업한다.
2. spine-csharp API 변경을 먼저 처리해 프로젝트가 컴파일되게 한다.
3. 사용자 코드의 split component 대응을 끝낸다.
4. 그다음 모든 scene/prefab에 `Upgrade All`을 실행하고 저장한다.

`Upgrade All`을 먼저 실행하지 않는다. 컴파일이 깨진 상태에서는 사용자 코드와 직렬화 참조의 손실 여부를 제대로 검증할 수 없다.

## 프로젝트 고유 전제

- 모든 경로는 저장소 루트 `dreamsquad-demo` 기준 상대 경로다.
- `.omc/...`, 다른 sandbox, 임시 복사본 경로를 프로젝트 루트로 간주하지 않는다.
- Spine은 UPM이 아니라 `Assets/Spine`에 설치된 unitypackage 방식이다. `Packages/manifest.json`에 Spine 패키지를 추가하지 않는다.
- 실제 게임 유닛은 다음 SkeletonDataAsset을 공유한다.
  - `Assets/Layer Lab/2D Art Maker/AMCasual Character/Demo/SpineAnimation/Casual Character_SkeletonData.asset`
- 실제 원본 Spine 프로젝트는 다음 경로에 있다.
  - `Assets/Layer Lab/2D Art Maker/AMCasual Character/SpineSource/Casual Character.spine`
- Defender/Enemy 데이터와 `SceneTransition.prefab` 등 약 41개 참조가 기존 SkeletonDataAsset GUID를 사용한다. 생성 자산을 새로 만들기보다 기존 source 파일을 덮어써 GUID를 보존한다.
- UI도 인게임과 동일한 skeleton/atlas를 사용한다. UI 전용 skeleton export를 새로 만들 필요는 없다. 단, `SkeletonRenderer`와 `SkeletonGraphic`은 서로 다른 계열의 shader material을 사용한다.
- `Chicken.json`, `fire.json`은 Layer Lab 데모용 4.2 데이터다. 게임 핵심 데이터는 아니며 4.3 원본이 없으면 해당 데모는 호환 오류가 날 수 있다.

## 절대 하지 말 것

1. 사용자 확인 없이 `ProjectSettings/ProjectSettings.asset`의 Color Space를 바꾸지 않는다.
2. split 업그레이드를 위해 Scripting Define Symbol을 임의로 추가하지 않는다.
   - 특히 `SPINE_AUTO_UPGRADE_COMPONENTS_OFF` 같은 임의 define을 추가하지 않는다.
   - 자동 업그레이드 제어는 공식 가이드대로 `Edit > Preferences > Spine`에서 한다.
3. `Assets/_Project/Spine`을 프로젝트 루트로 착각하거나, 그 폴더 전체를 canonical asset 폴더에 복사하지 않는다.
4. 기존 `.meta`, `_SkeletonData.asset`, `_Atlas.asset`, `.mat`을 새 export의 파일로 덮어쓰지 않는다.
5. 4.3 unitypackage import가 4.2 전용 파일을 자동 삭제할 것이라고 가정하지 않는다.
6. PMA export, Straight Alpha texture import, Straight/PMA material을 섞지 않는다.
7. Unity가 열린 같은 `Library`에 별도 batchmode Unity를 동시에 실행하지 않는다.

## 0. 시작 상태 기록

PowerShell에서 반드시 실제 프로젝트 루트인지 확인한다.

```powershell
git rev-parse --show-toplevel
git status --short
Get-Content ProjectSettings/ProjectVersion.txt
Select-String -Path ProjectSettings/ProjectSettings.asset -Pattern 'm_ActiveColorSpace'
```

Color Space 값은 다음과 같다.

- `m_ActiveColorSpace: 1` = Linear
- `m_ActiveColorSpace: 0` = Gamma

새 환경의 Git 기준값을 기록하고 업그레이드 중 임의로 변경하지 않는다. 이 프로젝트의 추적 기준은 Linear였으며, 이전 작업 중 Gamma로 바뀌었을 때 기존보다 어두워지는 현상이 확인됐다.

업그레이드는 별도 브랜치와 복구 가능한 커밋에서 진행한다. 기존 dirty 변경이 있다면 Spine 변경과 섞지 않는다.

## 1. Alpha workflow를 먼저 결정한다

Alpha workflow는 export 이후가 아니라 export 전에 결정한다. 프로젝트 전체에서 하나를 일관되게 사용한다.

### 권장: 기존 Linear Color Space 유지 + Straight Alpha

Spine Unity 4.3의 기본 권장 방식이며 Linear/Gamma 양쪽과 호환된다.

Spine Texture Packer:

- `Atlas extension`: `.atlas.txt`
- `Premultiply alpha`: 끔
- `Bleed`: 켬
- 가능하면 UI draw call을 줄이도록 single-page atlas 사용

Unity Texture Importer:

- `sRGB (Color Texture)`: 켬
- `Alpha Is Transparency`: 켬

Material:

- 월드 렌더링: `Spine/Skeleton` 계열 + `Straight Alpha Texture` 켬
- UI 렌더링: `Spine/SkeletonGraphic` 계열 + `Straight Alpha Texture` 켬
- Spine Preferences에서 `Switch Texture Workflow > Straight Alpha` 선택

### 대안: Gamma Color Space + PMA

PMA는 Gamma 프로젝트에서만 사용한다. Color Space 변경은 Spine만이 아니라 게임 전체의 조명, UI, 후처리와 색 혼합에 영향을 주므로 별도 승인 없이 선택하지 않는다.

Spine Texture Packer:

- `Premultiply alpha`: 켬
- atlas header가 `pma:true`인지 확인

Unity Texture Importer:

- `sRGB (Color Texture)`: 끔
- `Alpha Is Transparency`: 끔

Material:

- `Straight Alpha Texture`: 끔
- UI는 `Assets/Spine/Runtime/spine-unity/Materials/UI-PMATexture` 아래 material 사용
- Spine Preferences에서 `Switch Texture Workflow > PMA` 선택

### 이번 작업에서 발견된 실패 조합

다음 조합이 동시에 존재해 UI 색/외곽이 깨졌다.

- atlas: `pma:true`
- PNG: `sRGBTexture: 0`, `alphaIsTransparency: 0`
- Outgame/Battle UI material: `SkeletonGraphicDefault-Straight`

같은 skeleton/atlas를 사용해도 문제없다. 별도 UI atlas가 필요한 것이 아니라, UI renderer에 전달하는 `Spine/SkeletonGraphic` material의 Alpha workflow가 atlas와 같아야 한다.

## 2. 4.3 skeleton/atlas를 올바르게 export한다

게임 데이터는 `Casual Character.spine`에서 4.3으로 export한다.

필수 산출물:

- `Casual Character.json` 또는 `Casual Character.skel.bytes` 중 하나
- `Casual Character.atlas.txt`
- `Casual Character.png`
- multi-page인 경우 `Casual Character_2.png`, `_3.png`, `_4.png` 등 모든 page

이 프로젝트에서는 검증과 기존 경로 보존이 쉬운 JSON 경로를 사용했다. JSON의 `skeleton.spine` 값이 `4.3.x`인지 확인한다.

```powershell
Select-String -LiteralPath '<export-folder>/Casual Character.json' -Pattern '"spine":"4.3'
Get-Content -LiteralPath '<export-folder>/Casual Character.atlas.txt' -TotalCount 10
```

주의:

- `.atlas`가 아니라 `.atlas.txt`로 직접 export한다. 잘못 export한 `.atlas`와 예전 `.atlas.txt`를 같은 폴더에 두지 않는다.
- binary를 쓸 경우 `.skel`이 아니라 `.skel.bytes`여야 한다.
- `Animation cleanup`은 끈다. setup pose와 같은 키가 누락되는 것을 피하기 위한 공식 권장사항이다.
- 현재 로컬 `Assets/_Project/Spine`에는 다음이 섞여 있으므로 통째 복사 금지다.
  - 새 4.3 `.atlas`
  - 이전 2048 atlas인 `.atlas.txt`
  - Unity가 직접 읽지 않는 `.skel`
  - 생성된 `.asset`, `.mat`, `.meta`
- 새 환경에서는 깨끗한 외부 export 폴더를 사용한다.

## 3. Spine Unity 4.3 runtime을 import한다

1. Unity 프로젝트를 백업하고 Editor를 닫는다.
2. `spine-unity-4.3-2026-08-04.unitypackage`를 import한다.
3. 프로젝트를 열고 import가 끝날 때까지 기다린다.
4. 다음 버전을 확인한다.

```powershell
Get-Content Assets/Spine/version.txt
Select-String -Path Assets/Spine/package.json -Pattern '"version"'
Select-String -Path Assets/Spine/Runtime/spine-csharp/package.json -Pattern '"version"'
```

기대값:

- package: `spine-unity-4.3-2026-08-04.unitypackage`
- spine-unity: `4.3.102`
- spine-csharp: `4.3.39`

### unitypackage가 남기는 4.2 stale 파일 제거

unitypackage import는 새 패키지에서 삭제된 예전 파일을 로컬에서 제거하지 않는다. 다음 4.2 파일과 각각의 `.meta`가 남아 있는지 확인하고, 공식 4.3 패키지에 없는 것이 확인되면 정확히 이 파일만 삭제한다.

```text
Assets/Spine/Runtime/spine-csharp/IUpdatable.cs
Assets/Spine/Runtime/spine-csharp/Attachments/IHasTextureRegion.cs
Assets/Spine/Runtime/spine-unity/Utility/AttachmentCloneExtensions.cs
Assets/Spine/Runtime/spine-unity/Modules/TK2D/SpriteCollectionAttachmentLoader.cs
Assets/Spine/Editor/spine-unity/Editor/Menus.cs
```

이 파일이 남으면 `IUpdatable.cs`에서 `Skeleton.Physics` 관련 `CS0426`, `CS0576` 오류로 첫 컴파일이 막힐 수 있다.

## 4. spine-csharp와 사용자 코드를 먼저 마이그레이션한다

### 주요 API 치환표

| 4.2 코드 | 4.3 코드/방향 |
|---|---|
| `AnimationState.GetCurrent(track)` | `AnimationState.GetTrack(track)` |
| `Skeleton.SetSlotsToSetupPose()` | `Skeleton.SetupPoseSlots()` |
| `Bone.WorldX`, `Bone.WorldY` | `bone.AppliedPose.WorldX`, `bone.AppliedPose.WorldY` |
| `BoneData.ScaleX` | `boneData.GetSetupPose().ScaleX` |
| `SlotData.RGBA` | `slotData.GetSetupPose().GetColor()` |
| Skeleton의 직접 RGBA 필드 | `Skeleton.GetColor()` / `Skeleton.SetColor()` |
| `skeletonAnimation.valid` | `skeletonAnimation.IsValid` |
| `skeletonAnimation.initialSkinName` | renderer의 `InitialSkinName` |
| `SkeletonGraphic.AnimationState` | companion `SkeletonAnimation.AnimationState` |
| `SkeletonGraphic.Update(delta)` | companion `SkeletonAnimation.Update(delta)` |
| `SkeletonRenderer.LateUpdateMesh()` | `SkeletonRenderer.UpdateMesh()` |

### 4.3 split component 원칙

- `SkeletonAnimation`은 animation 담당이다.
- `SkeletonRenderer`와 `SkeletonGraphic`은 rendering 담당이다.
- 월드 객체는 `SkeletonRenderer + SkeletonAnimation` 쌍으로 만든다.
- UI 객체는 `SkeletonGraphic + SkeletonAnimation` 쌍으로 만든다.
- animation에서 renderer 접근: `skeletonAnimation.Renderer`
- renderer에서 animation 접근: `skeletonRenderer.Animation`
- 하나만 enable/disable하던 코드는 필요하면 양쪽을 함께 처리한다.

동적 생성은 4.3 helper를 사용한다.

```csharp
var world = SkeletonAnimation.NewSkeletonAnimationGameObject(dataAsset);
SkeletonRenderer renderer = world.skeletonRenderer;
SkeletonAnimation animation = world.skeletonAnimation;

var ui = SkeletonGraphic.AddSkeletonGraphicAnimationComponents(
    gameObject, dataAsset, skeletonGraphicMaterial);
SkeletonGraphic graphic = ui.skeletonRenderer;
SkeletonAnimation uiAnimation = ui.skeletonAnimation;
```

`SkeletonGraphic`만 추가하거나 `SkeletonAnimation`만 추가하는 예전 `AddComponent<T>()` 패턴을 남기지 않는다.

### 이 프로젝트에서 확인된 수정 대상

다른 환경에서 동일 변경을 포팅하거나, 해당 변경이 포함된 커밋을 적용한 뒤 다시 검사한다.

```text
Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakerWindow.cs
Assets/_Project/Editor/LayerLabPresetImporter.cs
Assets/_Project/Editor/PropDataEditor.cs
Assets/_Project/Editor/SpineUpgradeSmoke.cs
Assets/_Project/Scripts/Core/SceneTransition.cs
Assets/_Project/Scripts/Presentation/PropBillboard.cs
Assets/_Project/Scripts/Presentation/SkeletonFlipXModifier.cs
Assets/_Project/Scripts/Presentation/SpineCombinedSkinCache.cs
Assets/_Project/Scripts/Presentation/SpineUnitView.cs
Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs
Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs
Assets/_Project/Scripts/UI/Dreamcatcher/JarFigurePile.cs
Assets/_Project/Scripts/UI/Dreamcatcher/SpineFigureBuilder.cs
Assets/_Project/Scripts/UI/Outgame/SquadCharacterPage.cs
Assets/_Project/Scripts/UI/Outgame/SquadUnitDetailView.cs
Assets/Layer Lab/2D Art Maker/_CommonSource/Scripts/Character/PartsManager.cs
```

`SquadUnitDetailView`처럼 기존 serialized field의 타입/이름을 바꿀 때는 `[FormerlySerializedAs]`를 사용하고, 자동 변환 후 실제 참조가 유지됐는지 Inspector에서 확인한다.

## 5. 코드 컴파일 후 scene/prefab split 업그레이드

코드 오류가 0개가 된 뒤 Unity에서 다음을 실행한다.

1. `Edit > Preferences > Spine`
2. `Automatic Component Upgrade`가 활성 상태인지 확인
3. `Upgrade Scenes & Prefabs > Upgrade All`
4. 변경된 모든 scene/prefab을 저장
5. 몇 개만 열어 본 것으로 끝내지 말고 닫힌 asset까지 업그레이드됐는지 확인

반드시 확인할 프로젝트 asset:

- `Assets/Resources/SceneTransition.prefab`
  - runner 3개 모두 `SkeletonGraphic + SkeletonAnimation`
  - `loadingRunners` 참조 유지
  - `Run`, loop 설정 유지
- `Assets/_Project/Scenes/OutgameScene.unity`
- `Assets/_Project/Scenes/BattleScene.unity`
- 필요 시 Layer Lab `Demo_Casual.unity`

prefab을 Inspector에서 선택한 것만으로 변경이 영구 저장되지는 않는다. Prefab Mode에서 저장하거나 `Upgrade All`을 사용한다.

모든 asset이 저장되고 검증된 뒤에만 Preferences에서 split 자동 업그레이드를 비활성화할 수 있다. 빌드 전에 닫혀 있던 구형 prefab/scene이 남지 않았는지 먼저 확인한다.

## 6. 기존 canonical Spine 데이터를 4.3 export로 갱신한다

Unity를 닫거나 자동 import가 안정적으로 끝날 수 있는 상태에서, 깨끗한 export 폴더의 원본 파일만 다음 canonical 폴더에 덮어쓴다.

```text
Assets/Layer Lab/2D Art Maker/AMCasual Character/Demo/SpineAnimation/
```

덮어쓸 파일:

- `Casual Character.json`
- `Casual Character.atlas.txt`
- `Casual Character.png`

새로 추가할 파일:

- multi-page인 경우 `_2.png`, `_3.png`, `_4.png` 등 추가 page

복사하지 않을 파일:

- export 폴더의 `.meta`
- export 폴더의 `_SkeletonData.asset`
- export 폴더의 `_Atlas.asset`
- export 폴더의 `.mat`
- `.atlas`, `.skel`처럼 Unity 규칙에 맞지 않는 확장자

이 원칙은 기존 `Casual Character_SkeletonData.asset`과 41개 참조의 GUID를 보존하기 위한 것이다. Unity가 source 변경을 감지하면 기존 생성 asset을 갱신하게 한다.

import 후 다음을 확인한다.

- `Casual Character_SkeletonData.asset`의 Skeleton JSON이 기존 GUID의 새 4.3 JSON을 참조
- Atlas Assets 배열에 기존 `Casual Character_Atlas.asset`이 연결
- `_Atlas.asset`의 Materials 수가 atlas page 수와 동일
- 각 material의 `_MainTex`가 서로 다른 올바른 page PNG를 참조
- 첫 page용 material이 중복 생성되어 orphan으로 남지 않았는지 확인
- JSON preview에서 skin, animation, attachment가 정상 로드

Unity가 파일 변경을 감지하지 못하면 canonical 폴더를 우클릭해 `Reimport`한다.

## 7. multi-page atlas와 SkeletonGraphic

공식 문서는 UI `SkeletonGraphic`에 single-page atlas를 권장한다. 이 프로젝트처럼 동일 atlas를 월드와 UI가 함께 쓰고 atlas가 여러 page라면 모든 UI `SkeletonGraphic`에서 다중 CanvasRenderer가 필요하다.

런타임 생성 직후:

```csharp
graphic.allowMultipleCanvasRenderers = true;
```

현재 프로젝트 적용 지점:

- `Assets/_Project/Scripts/UI/Dreamcatcher/SpineFigureBuilder.cs`
- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPage.cs`
- `Assets/Resources/SceneTransition.prefab`의 runner 3개

Unity Console에 다음 취지의 오류가 있으면 누락된 것이다.

```text
Unity UI does not support multiple textures per Renderer.
Please enable Advanced - Multiple CanvasRenderers.
```

다중 CanvasRenderer는 정확성을 위한 fallback이며 page/submesh별 draw call이 증가한다. UI 전용 export를 만들지 않는 방침이라면 이 비용을 수용하고 Play Mode에서 CanvasRenderer 자식 생성과 masking/fade를 확인한다.

## 8. Material과 Color Space 검증

Alpha 설정 세 항목을 한 세트로 검사한다.

| Workflow | Atlas | Texture Importer | Material |
|---|---|---|---|
| Straight | `pma:false` 또는 PMA 아님 | `sRGB=1`, `Alpha Is Transparency=1` | `_StraightAlphaInput=1` |
| PMA | `pma:true` | `sRGB=0`, `Alpha Is Transparency=0` | `_StraightAlphaInput=0` |

추가 규칙:

- `SkeletonRenderer`에는 `Spine/Skeleton` 계열 material을 사용한다.
- `SkeletonGraphic`에는 `Spine/SkeletonGraphic` 계열 material을 사용한다.
- 같은 atlas를 공유해도 renderer 종류에 맞는 material은 필요하다.
- `CanvasGroup` alpha를 사용하는 UI는 `CanvasGroup` 호환 SkeletonGraphic material을 선택하고, `CanvasGroup Compatible`을 켠 뒤 Tint Black을 쓰지 않으면 `PMA Vertex Colors`를 끈다.
- Color Space만 단독으로 바꾸거나 material만 단독으로 바꿔 외관을 맞추지 않는다.

Scene의 serialized material 참조도 확인한다.

```powershell
rg -n 'skeletonGraphicMaterial:|figureSkeletonMaterial:|m_Material:' `
  Assets/_Project/Scenes Assets/Resources -g '*.unity' -g '*.prefab'
```

## 9. 정적 검증

### 구 API와 잘못된 생성 패턴 재검색

```powershell
rg -n 'GetCurrent\(|SetSlotsToSetupPose\(|AddComponent<SkeletonGraphic>|AddComponent<SkeletonAnimation>' `
  Assets/_Project 'Assets/Layer Lab/2D Art Maker/_CommonSource' -g '*.cs'
```

검색 결과는 0건이어야 한다. `AnimationState` 자체는 4.3에도 정상 API이므로 무조건 제거 대상으로 취급하지 않는다. companion `SkeletonAnimation`을 통해 접근하는지 확인한다.

### 컴파일

Unity가 `.csproj`를 갱신한 뒤 실행한다.

```powershell
dotnet build Wassup.Runtime.csproj --no-restore -v:minimal
dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
```

완료 기준은 오류 0개다. 기존 obsolete 경고는 별도 기술부채로 기록하되 Spine 오류와 구분한다.

### diff 확인

```powershell
git diff --check
git status --short
git diff -- ProjectSettings/ProjectSettings.asset
```

`ProjectSettings` 변경이 공식 4.3 가이드에서 요구된 것인지 줄 단위로 확인한다. 요구되지 않은 Color Space, Graphics, Build Settings 변경은 되돌리거나 별도 승인 대상으로 분리한다.

## 10. Unity 데이터 스모크와 Play Mode QA

코드 마이그레이션 후 `Assets/_Project/Editor/SpineUpgradeSmoke.cs`의 다음 entry point를 사용할 수 있다.

```text
SpineUpgradeSmoke.SpinePipelineSmoke
```

Unity Editor를 닫은 상태에서 예시:

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
& $unityExe -batchmode -nographics -quit `
  -projectPath (Get-Location).Path `
  -executeMethod SpineUpgradeSmoke.SpinePipelineSmoke `
  -logFile 'Logs/spine43-smoke.log'
```

기대 결과:

```text
[SMOKE] SpinePipelineSmoke PASS
```

Play Mode에서 최소 다음을 직접 확인한다.

1. BattleScene의 인게임 Defender/Enemy가 올바른 조합 skin으로 표시된다.
2. idle/walk/attack/hit/death animation이 재생된다.
3. drag/deploy preview가 보이고 flip, alpha, weapon trail이 정상이다.
4. Outgame squad detail의 SkeletonGraphic이 모든 atlas page attachment를 올바르게 표시한다.
5. Dreamcatcher jar/awakening figure가 깨지거나 일부 page만 표시되지 않는다.
6. SceneTransition runner 3개가 Run animation과 skin을 유지한다.
7. UI 외곽에 검은 선, 흰 선, 컬러 스트라이프가 없다.
8. 기존 기준보다 전체 색이 어둡거나 밝아졌다면 Color Space와 Alpha workflow 세트를 다시 비교한다.

## 11. 알려진 함정과 증상별 원인

| 증상 | 우선 확인할 원인 |
|---|---|
| `IUpdatable.cs`에서 `Skeleton.Physics` 컴파일 오류 | unitypackage가 삭제하지 못한 4.2 stale 파일 |
| JSON 로드 거부, version mismatch | 4.2 JSON을 4.3 runtime에서 사용 |
| 인게임은 정상인데 UI 파츠가 깨지거나 사라짐 | multi-page atlas + `allowMultipleCanvasRenderers=false` |
| UI 외곽이 검거나 컬러 번짐 | PMA/Straight texture와 material 불일치 |
| 전체적으로 기존보다 어두움 | Project Color Space 변경 또는 sRGB flag 변경 |
| animation API가 null | `SkeletonGraphic`에서 직접 AnimationState 접근, companion 누락 |
| WeaponTrail/BoneFollower 참조 null | split으로 `SkeletonAnimation`이 더 이상 `SkeletonRenderer`가 아님 |
| prefab은 Inspector에서 보이지만 빌드에서 companion 누락 | prefab/scene 자동 변환을 저장하지 않았거나 `Upgrade All` 미실행 |
| atlas 첫 page material이 두 개 생김 | generated `.mat/.asset/.meta`까지 복사해 자동 생성 asset과 충돌 |

## 12. 완료 조건

- Spine Unity `4.3.102`, spine-csharp `4.3.39` 확인
- 게임용 JSON이 4.3 export
- 4.2 stale runtime 파일 없음
- C# 구 API 검색 결과 없음
- world/UI 동적 생성이 모두 renderer + animation 쌍
- 모든 scene/prefab에 split component 저장 완료
- Atlas page 수와 material 수 일치
- Alpha workflow와 Color Space가 의도적으로 결정되고 전 구간 일치
- multi-page SkeletonGraphic 모두 Multiple CanvasRenderers 활성화
- `Wassup.Runtime` 및 `Assembly-CSharp` 오류 0개
- `SpinePipelineSmoke PASS`
- Battle, Outgame, SceneTransition Play Mode QA 통과
- 의도하지 않은 ProjectSettings 변경 없음

## 새 세션 시작용 프롬프트

다른 세션에서는 저장소 루트에서 다음과 같이 요청한다.

```text
docs/spec/spine-43-upgrade/README.md를 처음부터 끝까지 읽고,
공식 Spine Unity 4.3 split guide와 assets/import 문서를 함께 확인한 다음
이 프로젝트의 Spine Unity 4.2 → 4.3 업그레이드를 가이드 순서대로 진행해라.

중요:
- 먼저 git root와 현재 Color Space를 기록할 것.
- ProjectSettings와 Scripting Define을 임의로 바꾸지 말 것.
- Assets/_Project/Spine 폴더를 통째로 복사하지 말 것.
- runtime import 뒤 4.2 stale 파일을 검사할 것.
- 코드 컴파일을 먼저 고친 뒤 Upgrade All을 실행할 것.
- 기존 SkeletonDataAsset/AtlasAsset/meta GUID를 보존할 것.
- Alpha workflow는 기존 Linear를 유지하는 Straight Alpha를 우선 검토하고,
  PMA/Gamma로 바꾸려면 프로젝트 전체 영향으로 명시할 것.
- 동일 skeleton/atlas를 UI와 인게임이 공유한다.
- multi-page라면 모든 SkeletonGraphic의 Multiple CanvasRenderers를 확인할 것.
- 각 단계에서 git diff와 검증 결과를 보고하고, 완료 기준을 모두 충족할 때까지 진행할 것.
```
