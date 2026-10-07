# 3 — 에디터 도구 · 패키지

## 목적

일을 끝낸 일회성 에디터 스크립트와 프로젝트 코드가 안 쓰는 패키지를 뺀다. 저작 도구와 디버그 메뉴는 남긴다.

## 에디터 스크립트 분류 (`Assets/_Project/Editor/` 37)

| 처분 | 파일 | 근거 |
|---|---|---|
| **유지 — 디버그** | `BattleCore/Core*Menu.cs` 11 | 골든 베이커 · 하네스 · 카드 스냅샷/자가진단 · 기믹/해저드/소환/트리거/감지/장애물 셈판 · 씬 열기 |
| **유지 — 저작** | `MapStageAuthoringTools` · `MapStageCameraFraming` · `MapStageEditors` · `WavePlanAssetEditor` · `WavePlanTestLauncher` · `PropDataEditor` · `SpriteFlipbookDataEditor` · `FlipbookCharacterViewEditor` · `UnitVisualDataValidator` · `LayerLabPresetImporter`(`UnitVisualDataValidator:130` 이 `LayerLabImportSection.Draw` 를 부른다 — Layer Lab 42 유닛의 산 저작 경로) · `GaProjectileStripper`(`lessons/03:129` 가 GA 탄 반입 절차의 도구로 든다) · `DefenderPortraits/*` 2(+bake profile) | 스테이지 · 플랜 · 프롭 · 플립북(디펜더 3 가 플립북) · 초상 · Layer Lab · GA 반입 |
| **삭제 — 일회성** | `GaProjectileSwapper.cs` | 아처 탄을 GA 변형으로 돌려 보던 dev 토글. 되돌림 대상 `Projectile_Arrow.asset`(고아)도 단위 2 에서 간다 |
| **삭제 — 일회성** | `SpineUpgradeSmoke.cs` | Spine 4.2 전환 배치 스모크(`-executeMethod`). 호출처 0(`UnitVisualDataValidator` 는 주석만) · 문서 0 |
| **삭제 — 일회성** | `MapStageDuelGenerator.cs` | Duel 스테이지 절차 조립 완료(프리팹이 정본 — 생성기는 프리팹을 통째로 덮어쓴다). `map-stage-authoring.md:87-88` 의 「생성기 메뉴 · 예시 코드」 두 줄을 걷는다 |
| **삭제 — 산출물 소비처 0** | `ProjectileTextureBaker.cs` | 변형 텍스처 9 가 고아(단위 2). 입력 4 중 `PixPlays/Components/Textures/T_Lu_Noise_09.png` 만 그때 같이(나머지 3 은 PixPlays 머티리얼이 쓴다) |
| **D5** | `UnitStatImport/` 9 + `Scripts/Data/StatImport/` 15 + `SheetSync/` 2 + asmdef | 임포트(시트 → SO)는 데이터 도구. **push/export**(`SheetPushClient` · `SheetPushPayload` · `UnitStatExporter` · `DcSheetExporter` · `CostConfigSheetExporter` · 테스트 `UnitStatExportRealAssetTests` · `SheetFullRoundTripTests`)는 제약 「에이전트는 시트에 쓰지 않는다」의 반대편 — 사용자 결정 |

`Assets/Editor/SpineSettings.asset` 은 Spine 런타임 설정 — 유지.

## 리포 도구

- `tools/key_sheet_alpha.py` — 플립북 시트 알파 키잉. 플립북 디펜더 3 가 살아 있으니 **유지**, `tools/README.md` 그대로.
- `.claude/agents/core-reviewer.md` · `hooks/` 2 · `skills/` 3 — 유지(전투 기준).

## 패키지 (`Packages/manifest.json`)

코드 사용 0 **이고** `Assets`·`ProjectSettings` 가 그 패키지의 에셋(스크립트 guid 포함)을 하나도 가리키지 않음(PackageCache guid 역참조로 확인) → 제거 6: `com.unity.probuilder` · `com.unity.visualscripting` · `com.unity.postprocessing`(URP 는 자체 볼륨) · `com.unity.multiplayer.center` · `com.unity.ai.navigation`(`FlowFieldBuilder` 의 주석뿐) · `com.unity.collab-proxy`(Plastic — 쓰지 않음).

**`com.unity.timeline` 은 유지** — PixPlays 의 산 프리팹 2(`EarthSlamSpikesAoeVFX` · `WaterBlast`)가 `PlayableVfx` 로 `.playable` 3 을 돈다.

유지: `2d.sprite` · `burst` · `collections` · `inputsystem` · `ugui` · `test-framework` · `render-pipelines.universal` · `shadergraph` · `timeline` · `ide.*` · `nuget.newtonsoft-json`(시트 DTO — D5 가 전부 떼면 같이) · PrimeTween tgz.

선택: `Assets/InputSystem_Actions.inputactions` 는 코드가 안 쓴다(입력은 `Pointer.current` 직접) — `ProjectSettings` 의 프로젝트 전역 액션 참조만 있다. 지우려면 그 설정도 같이 비운다. 효과가 작아 **보류**.

빌트인 모듈은 **하나씩** 뺀다 — 에디터 resolve(`packages-lock.json`)가 의존으로 되살리는 것은 둔다. 후보: `terrain` · `terrainphysics` · `vehicles` · `wind` · `cloth` · `xr` · `video` · `vectorgraphics` · `ai` · `director` · `timelinefoundation` · `umbra` · `tetgen` · `unityanalytics` · `assetbundle` · `screencapture` · `adaptiveperformance` · `accessibility`. URP 가 요구하는 것(예: `terrain` 은 URP terrain 셰이더)은 lock 이 말해 준다.

## 완료 기준

- [x] `Editor/` 파일 수 37 → 33(D5 전) · `rg "GaProjectileSwapper|SpineUpgradeSmoke|DuelGenerator|ProjectileTextureBaker" Assets docs` → `map-stage-authoring.md` 갱신 뒤 0
- [x] manifest 에서 패키지 6 제거 · 모듈은 lock 기준 · 에디터 전체 재컴파일 0 · ShaderGraph/URP 에셋 재임포트 에러 0
- [x] `packages-lock.json` 을 resolve 결과로 커밋 · 커밋 2(에디터 · 패키지)

## 구현 결과 (2026-10-07) — `dedf84044` · 잔재 커밋

- Editor 4 삭제(37 → 33). `map-stage-authoring.md` 생성기 두 줄 삭제.
- D5 를 좁혔다: **push 만** 제거(`SheetPushClient` · `SheetPushPayload` · 창의 Push 섹션 + `_scriptUrl` · `SheetPushReportTests` · 바디 계약 테스트). SO→JSON export 는 `SheetHeaderDocTests` 가 exporter 의 Row 타입을 헤더 계약으로 읽어서 남겼다.
- manifest 6 제거 → 에디터 resolve 로 lock 의존 65 → 58(`settings-manager` 가 probuilder 의존으로 같이 빠짐). `ProjectSettings/Packages/com.unity.probuilder/` 잔재 삭제. 빌트인 모듈은 손대지 않았다(후속).
