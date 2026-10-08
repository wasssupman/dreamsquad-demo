# 단위 1 — 자리: somnia 트리로 `git mv` + 경로 리터럴 + 새 asmdef 2

**전제**: 단위 0 커밋 뒤, 에디터 닫힘. 이동은 전부 `git mv`(`.meta` 동반 → GUID 보존 → 씬 · 프리팹 · SO · ProjectSettings 의 참조는 안 깨진다).

## 이동표

| 지금 | → | 비고 |
|---|---|---|
| `Assets/_Project/Scripts/Somnia.Battle.Runtime.asmdef` | `Assets/_Project/Runtime/Battle/Somnia.Battle.Runtime.asmdef` | 모듈 루트. 밑의 `Scripts/` 를 전부 덮는다(중첩 asmdef 폴더 제외) |
| `Assets/_Project/Scripts/**` | `Assets/_Project/Runtime/Battle/Scripts/**` | 11 하위 폴더 그대로 |
| `Assets/_Project/{Data,Art,Sprites,VFX,Models,Fonts,Audio,Generated,Spine,Prefabs,Shaders,Map,Characters}` | `Assets/_Project/Runtime/Battle/<동명>` | 13 폴더 · ~220 MB |
| `Assets/Settings/{DefaultVolumeProfile,Mobile_RPAsset,Mobile_Renderer,PC_RPAsset,PC_Renderer,SampleSceneProfile}.asset` | `Assets/_Project/Runtime/Battle/Settings/` | `UniversalRenderPipelineGlobalSettings.asset` 은 제자리. `QualitySettings`/`GraphicsSettings` 는 GUID 참조 |
| `Assets/_Project/Editor/**` | `Assets/_Project/Editor/Battle/**` | 루스 31 + `BattleCore/` + `DefenderPortraits/` + `UnitStatImport/`(자기 asmdef) |
| `Assets/_Project/Tests/EditMode` | `Assets/_Project/Tests/EditMode/Battle` | |
| `Assets/_Project/Tests/Fixtures` | `Assets/_Project/Tests/EditMode/Battle/Fixtures` | 소비자 `RetiredWaveAuthoringPortTests`(EditModeAssets) 경로 리터럴 |
| `Assets/_Project/Tests/EditModeCore` | `Assets/_Project/Tests/EditMode/BattleCore` | |
| `Assets/_Project/Tests/GoldenCore` | `Assets/_Project/Tests/EditMode/BattleCore/Golden` | `CoreGoldenStore.RelativeDir` · `.gitattributes`(있으면) |
| `Assets/_Project/Tests/EditModeAssets` | `Assets/_Project/Tests/EditMode/BattleAssets` | `Fixtures/` 포함 |
| `Assets/_Project/Tests/PlayModeCore` | `Assets/_Project/Tests/PlayMode/BattleCore` | |
| `Assets/{Spine, Spine Examples, Layer Lab, PixPlays, GabrielAguiarProductions, Hovl Studio, KayKit, VFXPACK_FIRE_WALLCOEUR}` | `Assets/Plugins/<동명>` | D3. `Assets/Spine/CHANGELOG.md` 는 단위 2 에서 삭제 |
| `tools/battle-core-rebuild/` | `tools/battle/` | `check_ledgers.py` 삭제(죽은 도구) · csproj 의 맥 절대경로 기본값 제거 |

제자리: `Assets/_Project/Scenes/BattleCoreScene.unity` · `Assets/InputSystem_Actions.inputactions` · `Assets/Editor/SpineSettings.asset` · `Assets/TextMesh Pro/` · `Assets/Plugins/PrimeTween/` · `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.

## 새 asmdef 2 (D2)

**`Assets/_Project/Editor/Battle/Somnia.Battle.Editor.asmdef`** — `includePlatforms: ["Editor"]`, `autoReferenced: true`, `rootNamespace: Somnia.Battle.Editor`. 참조는 루스 31 파일의 `using` 전수로 정한다(조사: `Somnia.Battle.Runtime` · `.BattleCore` · `.Skills` · `.UnitAi` · `spine-unity` · `spine-csharp` · `Unity.InputSystem` · `Unity.2D.Sprite.Editor`(`UnityEditor.U2D.Sprites`) · `LayerLab.ArtMaker` · 필요 시 `Unity.TextMeshPro` · `Unity.RenderPipelines.Universal.Runtime`). `Editor/Battle/UnitStatImport/` 는 자기 asmdef 가 중첩이라 그대로.

**`Assets/Plugins/Layer Lab/2D Art Maker/_CommonSource/LayerLab.ArtMaker.asmdef`** — 벤더 폴더에 JSON 1장(Hovl 과 같은 방식). 참조: `spine-unity` · `spine-csharp` · `Unity.TextMeshPro` · `UnityEngine.UI`(조사: Layer Lab .cs 19 의 using = UnityEngine.UI 9 · EventSystems 6 · Spine.Unity 4 · TMPro 3 · Spine 2; `UnityEditor` 토큰 0). 벤더 코드 무수정.

PixPlays(.cs 12) · GabrielAguiar(.cs 3) 는 `_Project` 코드가 참조하지 않는다(asmdef 코드는 predefined 어셈블리를 못 본다 — 지금도 못 보고 있다) → `Assets/Plugins` 밑에서 Assembly-CSharp-firstpass 로 컴파일되면 끝.

## 경로 리터럴 (68 파일 + 도구)

| 패턴 | → |
|---|---|
| `Assets/_Project/Scripts/` | `Assets/_Project/Runtime/Battle/Scripts/` |
| `Assets/_Project/{Data,Prefabs,Art,VFX,Sprites,Spine,Shaders,Fonts,Audio,Generated,Map,Models,Characters}/` | `Assets/_Project/Runtime/Battle/<동명>/` |
| `Assets/_Project/Editor/` | `Assets/_Project/Editor/Battle/` |
| `Assets/_Project/Tests/EditMode/` | `Assets/_Project/Tests/EditMode/Battle/` (먼저 `EditModeCore`·`EditModeAssets` 를 치환해 접두 충돌을 피한다) |
| `Assets/_Project/Tests/EditModeCore/` · `EditModeAssets/` · `PlayModeCore/` · `Fixtures/` · `GoldenCore` | 이동표대로 |
| 벤더 루트 리터럴(`Assets/Spine/` · `Assets/Layer Lab/` …) | `Assets/Plugins/<동명>/` — 구현 때 `grep '"Assets/'` 전수로 잡는다 |

특별히: `CoreGoldenStore.RelativeDir`(`Marker = "Assets/_Project/Tests"` 는 유효) · `CoreArchitectureTests` 의 폴더 상수 7 · `CoreScenePlayMenu` 등 씬 경로 7(씬은 안 옮기니 **그대로**) · csproj `Compile Include` 글롭 · `verify-fresh-skills.sh` 의 `git archive -- Assets/_Project/Scripts …` · `.claude/skills/enemy-wave-integration/SKILL.md` 의 덱 경로.

## 확인

- `git status --porcelain | grep -v '^R'` 에 `_Project`/벤더 이동분이 없음(전부 rename).
- `grep -rn '"Assets/_Project/\(Scripts\|Data\|Prefabs\|Art\|VFX\|Sprites\|Spine\|Shaders\|Fonts\|Audio\|Generated\|Map\|Models\|Characters\|Editor/[A-Z]\|Tests/\(EditModeCore\|EditModeAssets\|PlayModeCore\|Fixtures\|GoldenCore\)\)' --include=*.cs --include=*.csproj --include=*.sh --include=*.md Assets tools .claude CLAUDE.md` 가 0.
- `Assets` 루트에 `.cs/.asmdef` 가 `_Project`·`Plugins` 밖에 0(governance `check_assets_root_policy` 와 같은 조건).
- `Assets/_Project/Runtime/Battle/Somnia.Battle.Runtime.asmdef` 가 `Scripts/` 의 .cs 를 덮고, 중첩 asmdef 5(`BattleCore`·`Skills`·`UnitAi`·`SheetSync`·없음) 가 제자리.

## 구현 결과

커밋 `fa13b6802`(2026-10-08, 5,279 파일 — rename 5,264 · 새 폴더/asmdef meta 10 · 삭제 1) — 스크립트 `scratchpad/relocate.py`. 경로 리터럴은 규칙 11종으로 .cs 61+13 파일 · csproj 3 · `.claude` · `.gitattributes`(골든 LF 줄). csproj 의 맥 절대경로 기본값 4 → `$(RepoRoot)Library/ScriptAssemblies`.

스크립트가 틀렸던 것 둘(적용 중 바로잡음): ① 폴더를 자기 안으로 옮길 때 바깥 `Editor.meta`·`Tests/EditMode.meta` 가 tmp 로 끌려가 새 GUID 가 생겼다 → `git mv -f` 로 되돌려 바깥은 옛 GUID, 안쪽 `Battle.meta` 만 새 GUID. ② `git rm` 이 rename 으로 스테이징된 `check_ledgers.py` 를 거부 → `-f`.

**정규식이 못 잡은 꼴 둘(단위 4 배치가 잡음, 후속 커밋에서 보정)**: `Path.Combine(Application.dataPath, "_Project", "Scripts", …)` 처럼 **세그먼트로 조립**하는 테스트 2곳(`ReachEntryPointGuardTests`) · 스냅샷 fixture `binding_bake_snapshot.txt` 안의 에셋 경로 21줄(`.txt` 는 치환 대상이 아니었다).
