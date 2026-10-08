# 단위 2 — governance 선제 수정 (D4)

somnia `scripts/verify_governance.py` 가 **하드 실패**시키는 자리만. 각 검사의 정규식을 그대로 Demo 에 돌려 0 을 확인한다(스크립트 전체는 somnia 의 필수 파일을 요구해 Demo 에서 못 돌린다).

## 2-1. `check_runtime_no_unityeditor` — `Runtime/**/*.cs` 의 `\busing\s+UnityEditor\b|\bUnityEditor\.` (`#if` 무시)

| 파일 | 지금 | 수정 |
|---|---|---|
| `Runtime/Battle/Scripts/BattleCoreUnity/ModeSelection.cs` | `#if UNITY_EDITOR` 안의 `ApplyEditorPlanCarry()` — `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 로 `UnityEditor.SessionState` 의 플랜 GUID 를 읽어 `Set(…)` | 메서드째 **`Editor/Battle/WavePlanTestCarry.cs`**(새, `Somnia.Battle.Editor`)로. `[InitializeOnLoadMethod]` 는 Play 진입의 도메인 리로드 뒤 · 씬 로드 전에 돈다(`RuntimeInitializeOnLoadMethod` 보다 앞) → `EditorApplication.isPlayingOrWillChangePlaymode` 일 때만 같은 일을 한다. `ModeSelection.Set` 은 `internal` 이면 `InternalsVisibleTo("Somnia.Battle.Editor")` 1줄. 쓰는 쪽 `WavePlanTestLauncher`(이미 에디터)는 그대로 |
| `Runtime/Battle/Scripts/Core/MapStage/MapStageGizmoUtil.cs` | 파일 전체 `#if UNITY_EDITOR`; `Label()` 만 `UnityEditor.Handles.Label` | `Label` 의 본문을 **delegate 슬롯**으로: `public static System.Action<Vector3, string> LabelDrawer;` — 비어 있으면 아무것도 안 그린다. `Editor/Battle/MapStageGizmoLabels.cs`(새)의 `[InitializeOnLoadMethod]` 가 `Handles.Label` 을 꽂는다. 호출자 7(`SpawnMarker`·`GoalMarker`·`RouteMarker`·`BonusSpawnMarker`·`StructureMarker`·`PropFootprint`·`PlacementBlockZone`)은 무수정. 파일의 `#if UNITY_EDITOR` 는 유지(빌드 strip) |

PlayMode.Core 의 테스트 캐리 경로(`MatchEntryKind.TestMode` · `PlanOverride`)는 단위 4 에서 그대로 돌아야 한다.

## 2-2. `check_no_entities` — `.cs` 전수(주석 포함)의 ECS 토큰

9줄 · 8 파일, 전부 주석: `CoreUnitViewPool.cs:19`(`EntityManager.Exists`) · `DefenderAbilityData.cs:7`(`Unity.Entities/ECS`) · `DcMechanic.cs:9`(`Unity.Entities or`) · `FactionRelation.cs:13`(`EntityManager`) · `ISkillContext.cs:8-9`(`EntityManager` · `SystemAPI`) · `SkillAim.cs:15`(`SystemBase`) · `SkillEntityId.cs:6`(`IComponentData` · `Unity.Entities`) · `JarFigurePhysics.cs:6`(`EntityManager`). 문구를 「옛 ECS」로 바꾼다(의미 보존 · 토큰 제거). `CoreArchitectureTests.cs:102` 의 `"Unity.Entities"` 리터럴은 정규식(`Unity\.Entities\.` — 점 필수)에 안 걸린다.

## 2-3. `check_no_old_platform_terms` — 텍스트 파일의 `WebGL` · `Telegram` · `Mini App` · `Mobile Web` · `PC Web`

- `Editor/Battle/PropDataEditor.cs:201` `ConfigurePlatformTexture(textureImporter, "WebGL")` — **삭제**(모바일 전용 리포 · 이 플랫폼 설정은 이미 죽은 분기).
- 주석 3: `FluidSimConfig.cs:5` · `FluidMath.cs:11` · `FluidMathTests.cs:8` — 「PavelDoGreat 의 브라우저 유체 시뮬」로.
- `Assets/Plugins/Spine/CHANGELOG.md` — 삭제(벤더 변경 이력, 코드 무관).

## 2-4. 확인 (정규식을 Python 으로 Demo 에)

```
Runtime .cs:  \busing\s+UnityEditor\b|\bUnityEditor\.           → 0
Assets .cs:   (ECS 14 패턴)                                       → 0
텍스트 파일:   WebGL|Telegram|Mini App|Mobile Web|PC Web (docs/archive 제외) → 0 (닫힌 spec 문서에 있으면 그 문서는 ⑤에서 docs/archive 로)
Assets .cs:   \bResources\.Load(?:All)?\s*\(                      → 0 (이미)
폴더명 resources(TMP 제외)                                        → Spine 에디터 마커 1 (⑤ 스크립트 예외)
```

## 구현 결과

(미착수)
