# somnia-battle-rename — `Wassup` → `Somnia.Battle.*` 개명 · somnia 레이아웃 재배치 (반입 전, Demo 리포 안에서)

**상태**: 완료 2026-10-08 — 단위 0~4 구현 · 배치 전후 동일 · 에디터 컴파일 0 · Play 확인(사용자). 커밋 `927dd6df9`(제안) · `078386d3f`(이름) · `fa13b6802`(자리) · `70686ccaf`(governance) · `d297475aa`(보정) · `ca24f280a`(문서).

somnia-client 이식 순서 ①경계 → ②demo-diet → ③6.6 → **④ 개명·재배치** → ⑤ 반입 중 ④다. 사용자 지시: 「반입하기 전 `Somnia.Battle.*` 개명·재배치 작업까지만」. 반입(somnia 쪽 ADR · 패키지 ledger · 전송)은 범위 밖이고 §「⑤ 에 넘기는 것」에 적어 둔다.

원칙(사용자 2026-10-01): **「그대로 컨벤션에 맞게 이식만 — 코드 정합성은 따지지 않는다.」** 그래서 이 spec 은 이름과 자리만 바꾼다. 로직 변경은 somnia governance 가 **하드 실패**시키는 자리 두 곳(단위 2)뿐이고, 그것도 D4 로 묻는다.

## 왜 Demo 에서 먼저 하나

테스트가 여기 있다(EditMode 3 lane + PlayMode.Core + 골든 11). somnia 로 옮긴 뒤 깨지면 「이름 때문인지 자리 때문인지 환경 때문인지」를 못 가른다. Demo 에서 somnia 의 이름·자리를 미리 하고 전 lane 초록을 확인한 뒤 ⑤에서는 **폴더째 복사**만 남긴다.

## somnia 규약 — 이 spec 이 맞추는 것 (`scripts/verify_governance.py` 7,369 줄 + ADR-0006 + `Assets/_Project/AGENTS.md` 판독)

| 규약 | 근거 | 이 spec 의 대응 |
|---|---|---|
| `_Project` 밑 asmdef 의 `name` · `rootNamespace` 는 `Somnia.` 접두 | `check_asmdef_namespaces` | asmdef 10 개명(단위 0) |
| `.cs/.asmdef` 는 `Assets/_Project/` 또는 `Assets/Plugins/`(·Firebase·Addressables) 밑에만 | `check_assets_root_policy` | 코드 있는 벤더 5 → `Assets/Plugins/`(단위 1, D3) |
| `Resources` 폴더명 금지(TMP 예외) · `Resources.Load` 금지 | 같은 검사 + ADR-0007 | 이미 0(`battle-content-finish`). Spine 에디터 마커 폴더는 ⑤의 스크립트 예외 |
| `Runtime/**/*.cs` 에 `UnityEditor` 토큰 금지 — **`#if UNITY_EDITOR` 를 보지 않는다** | `check_runtime_no_unityeditor` | 2 파일(단위 2, D4) |
| `.cs` 전수에 ECS 토큰 금지 — **주석도 본다** | `check_no_entities` | 주석 9줄(단위 2) |
| 텍스트 파일에 비대상 플랫폼 용어(브라우저 빌드 타깃 이름 등 5종) 금지 | `check_no_old_platform_terms` | 주석 3 + 코드 1줄 + Spine CHANGELOG(단위 2) |
| 레이어 의존 규칙 | `Somnia.Features.*` · `Core` · `App` · `Infrastructure` · `Api.Contracts` **이름에만** | `Somnia.Battle.*` 는 규칙 밖 → asmdef 그래프 그대로. `Features` 로 넣지 않는 이유는 아래 |
| 폴더: `Runtime/<모듈>/{asmdef, Scripts/, Fonts/…}` · `Editor/` · `Tests/EditMode/<영역>/` · `Scenes/` 평면 | somnia 실제 트리(`Runtime/Core`, `Runtime/UI/Fonts`, `Tests/EditMode/Map`) | 단위 1 |
| Assembly-CSharp(-Editor) 를 쓰지 않는다(`Somnia.Editor` 1개) | somnia 트리 | 루스 에디터 .cs 31 → 새 `Somnia.Battle.Editor`(D2) |

`Somnia.Features.Battle` 이 아닌 이유: governance 가 Feature 이름 asmdef 끼리의 참조를 전부 거절한다(테스트 asmdef 예외뿐). Demo 는 asmdef 10개가 서로 참조하므로 Features 로 넣으면 하나로 합쳐야만 통과하고, 그러면 코어 경계(`noEngineReferences` · 헤드리스 lane)가 사라진다. `Somnia.Battle.*` 는 「이식 모듈 네임스페이스 가족」으로 ⑤의 ADR 1건이 선언한다(somnia `current-project-facts.md:189` 「ad-hoc prefix 는 ADR 없이 금지」).

## 조사 결과 — 바뀌는 것의 전수

| 항목 | 수 | 비고 |
|---|---|---|
| `.cs` | 801 (Scripts 486 · Tests 284 · Editor 31) | 네임스페이스 44 종 전부 `Wassup.*` |
| asmdef (`_Project`) | 10 | 참조는 **이름**(GUID 아님) → 문자열 치환으로 끝. 테스트 asmdef 2 에 **없는 어셈블리 `Wassup.Editor.MobileBuild` 참조**가 남아 있다(demo-diet 잔재) → 같이 제거 |
| 코드의 완전수식 `Wassup.X` | 474 줄 (+주석 70) | 치환 1규칙 |
| `[MenuItem("Wassup/…")]` · 메뉴 상수 | 21 | → `Somnia/Battle/…` |
| `[CreateAssetMenu(menuName = "Wassup/…")]` | 69 | → `Somnia/Battle/…` |
| `InternalsVisibleTo` | 4 (2 파일) | |
| EditorPrefs 키 `"Wassup.UnitStatImport.*"` | 5 | 사용자 로컬 설정 — 바꾸면 한 번 초기화 |
| 셰이더 `Shader "Wassup/…"` | 16 | 머티리얼 74 는 GUID 참조라 안전. 코드의 `Shader.Find("Wassup/…")` 는 **0**(테스트 2 · 에디터 1 은 URP/Sprites 기본 셰이더). `BattleAuthoringAssetTests` 의 접두 단언 1 |
| 에셋 YAML `m_EditorClassIdentifier: Wassup.Runtime::Wassup.Data.X` | 596 + 씬 38 | 정보용 필드(GUID 가 풀리면 무시 — 이미 낡은 값 `Wassup.Battle.Effects.*` 3 이 증거). 그래도 다시 쓴다: `MarkerPropStyleAssetTests` 가 씬 텍스트에서 이 식별자를 찾는다. `SerializeReference`(`managedReferences`)는 **0** → 개명이 에셋을 깨뜨리지 않는다 |
| `.cs` 의 경로 리터럴 `"Assets/_Project/…"` | 68 파일 | `Data/`(테스트 다수) · `Scenes/BattleCoreScene` 7 · `Scripts/BattleCore` 4 · `Tests/Fixtures` · `CoreGoldenStore.RelativeDir` |
| 헤드리스 도구 | csproj 4 · sh 1 | `AssemblyName` · `Reference` · `Compile Include` 경로. `check_ledgers.py` 는 사라진 `Scripts/Bridge` 를 읽는 **죽은 도구** → 삭제 |
| `.claude` | hooks 1 · agents 1 · skills 2 | `Wassup.Tests.PlayMode.Core` · 경로 |
| 문서 | `CLAUDE.md` · `README.md` · `docs/reference` 6 · `lessons` 2 · `docs/spec/README.md` | 닫힌 spec(`docs/spec/*`)의 옛 이름은 **이력**이라 두지 않는다 — CLAUDE.md 「옛 spec 이름 꼬리표는 이력」 |
| 벤더 | 8 폴더 | Spine 5 MB(.cs 190 · asmdef 3) · **Spine Examples 2 MB(적 Dragon · Slime 3 · Whirlpot 의 스켈레톤 — 산 에셋)** · Layer Lab 2 MB(.cs 19, asmdef 없음) · PixPlays 53 MB(.cs 12) · GabrielAguiar 40 MB(.cs 3) · Hovl 5 MB(.cs 3 · asmdef) · KayKit 1 · VFXPACK 3. `TextMesh Pro` 5 MB 는 somnia 것을 쓸 예정이라 제자리 |
| 골든 `Tests/GoldenCore/*.trace.txt` | 11 | `Wassup` 언급 0 → 개명 무관. 결정론 영향 0 |

## 이름 — 치환 1규칙 (D1)

```
Wassup            → Somnia.Battle
Wassup.X.Y        → Somnia.Battle.X.Y
"Wassup/…"        → "Somnia/Battle/…"      (메뉴 · 셰이더)
Wassup.Runtime::  → Somnia.Battle.Runtime::  (m_EditorClassIdentifier)
```

| asmdef | → | 자리(단위 1) |
|---|---|---|
| `Wassup.BattleCore` (코어 · 엔진 무지) | `Somnia.Battle.BattleCore` | `Runtime/Battle/Scripts/BattleCore/` |
| `Wassup.Skills` | `Somnia.Battle.Skills` | `Runtime/Battle/Scripts/Skills/` |
| `Wassup.UnitAi` | `Somnia.Battle.UnitAi` | `Runtime/Battle/Scripts/UnitAi/` |
| `Wassup.SheetSync` | `Somnia.Battle.SheetSync` | `Runtime/Battle/Scripts/SheetSync/` |
| `Wassup.Runtime` (root ns `Wassup`) | `Somnia.Battle.Runtime` (root ns `Somnia.Battle`) | `Runtime/Battle/` **모듈 루트**(somnia `Runtime/Core/Somnia.Core.asmdef` 와 같은 자리) — `Scripts/` 는 그 밑 |
| `Wassup.Editor.UnitStatImport` | `Somnia.Battle.Editor.UnitStatImport` | `Editor/Battle/UnitStatImport/` |
| (없음 — Assembly-CSharp-Editor) | **새** `Somnia.Battle.Editor` (D2) | `Editor/Battle/` — 루스 31 (`BattleCore/` 메뉴 · `DefenderPortraits/` · 루트 도구) |
| `Wassup.Tests.EditMode` | `Somnia.Battle.Tests.EditMode` | `Tests/EditMode/Battle/` (+ `Fixtures/`) |
| `Wassup.Tests.EditMode.Core` | `Somnia.Battle.Tests.EditMode.Core` | `Tests/EditMode/BattleCore/` (+ `Golden/`) |
| `Wassup.Tests.EditMode.Assets` | `Somnia.Battle.Tests.EditMode.Assets` | `Tests/EditMode/BattleAssets/` |
| `Wassup.Tests.PlayMode.Core` | `Somnia.Battle.Tests.PlayMode.Core` | `Tests/PlayMode/BattleCore/` |
| 벤더 `spine-csharp` · `spine-unity` · `spine-unity-editor` · `Hovl.HSFiles` | 유지 | `Assets/Plugins/…` |
| (없음) | **새** `LayerLab.ArtMaker` 벤더 asmdef 1장 (D2) | `Assets/Plugins/Layer Lab/2D Art Maker/_CommonSource/` |

`Somnia.Battle.BattleCore` 가 겹말이지만 규칙 하나를 지키는 값이다 — `Somnia.Battle.Core` 로 다듬으면 Unity 층의 `Wassup.Core`(19 파일 · `TimeManager` 등)와 **충돌**해 그쪽도 새 이름을 지어야 하고, 문서·주석·골든 설명의 대조표가 둘이 된다.

## 자리 — somnia 트리 그대로 (단위 1)

```
Assets/_Project/
  Runtime/Battle/                    ← Somnia.Battle.Runtime.asmdef
    Scripts/{Audio,BattleCore,BattleCoreUnity,Core,Data,Presentation,Rendering,SheetSync,Skills,UI,UnitAi}/
    {Data,Art,Sprites,VFX,Models,Fonts,Audio,Generated,Spine,Prefabs,Shaders,Map,Characters}/   ← 옛 _Project/<동명>
    Settings/                        ← 옛 Assets/Settings 의 RP 에셋 6 (GlobalSettings 제외)
  Editor/Battle/                     ← Somnia.Battle.Editor.asmdef + UnitStatImport/(자기 asmdef)
  Tests/EditMode/{Battle,BattleCore,BattleAssets}/ · Tests/PlayMode/BattleCore/
  Scenes/BattleCoreScene.unity       ← 그대로(somnia 도 평면)
Assets/Plugins/{Spine, Spine Examples, Layer Lab, PixPlays, GabrielAguiarProductions, Hovl Studio, KayKit, VFXPACK_FIRE_WALLCOEUR, PrimeTween}/
```

제자리에 두는 것: `Assets/InputSystem_Actions.inputactions`(project-wide actions — `ProjectSettings` 만 참조, somnia 루트에 동명·다른 GUID 가 있어 ⑤에서 **제외**) · `Assets/Editor/SpineSettings.asset`(Spine 이 상수 경로로 찾는다) · `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`(프로젝트당 1, somnia 것 사용) · `Assets/TextMesh Pro/`.

## 결정 — 묶어서 한 번

| # | 결정 | 선택지 | 추천 |
|---|---|---|---|
| **D1** | 이름 규칙 | (a) 접두 치환 1규칙(`Somnia.Battle.BattleCore` 포함) (b) 다듬기(`Somnia.Battle.Core` + `Wassup.Core` 새 이름) | **(a)** — 기계적 · 충돌 0 · 대조표 하나 |
| **D2** | 루스 에디터 코드 31 | (a) 새 `Somnia.Battle.Editor` asmdef + Layer Lab 벤더 asmdef 1장(`LayerLabPresetImporter` 가 `LayerLab.ArtMaker` 를 쓴다 — asmdef 는 predefined 어셈블리를 참조 못 한다) (b) Assembly-CSharp-Editor 에 두고 ⑤에서 | **(a)** — somnia 는 Assembly-CSharp 를 안 쓴다. Layer Lab .cs 19 는 `UnityEditor` 토큰 0 이라 런타임 asmdef 1장이면 된다(Hovl 이 선례) |
| **D3** | 벤더 8 폴더 `Assets/Plugins/` 이동 시점 | (a) 지금 (b) ⑤ 반입 때 | **(a)** — 코드 있는 벤더는 governance 하드 실패라 어차피 옮긴다. Demo 에서 옮겨야 Spine 경로 상수(`SpineSettings.asset` · `Editor/Resources` 마커)가 깨지지 않음을 테스트로 본다 |
| **D4** | governance 선제 수정(단위 2) 포함 | (a) 포함 — `ModeSelection` 에디터 캐리 → Editor asmdef(`InitializeOnLoadMethod`), `MapStageGizmoUtil.Label` → 에디터가 꽂는 delegate, 주석 12줄, `PropDataEditor` 브라우저 타깃 1줄, Spine `CHANGELOG.md` 삭제 (b) ⑤로 미룸 | **(a)** — 6 파일 · 로직 변경 2(둘 다 에디터 전용 분기의 자리 이동). 여기서 하면 PlayMode.Core 가 캐리를 검증한다 |
| **D5** | 검증 | (a) 에디터 닫고 배치 EditMode 3 lane **전후 1회씩**(기준선 → 변경 후 diff) + 컴파일 0 + Play (b) 컴파일 0 + Play 만 | **(a)** — 경로 리터럴 68 파일은 Play 가 못 본다. 사용자 원칙 「배치 검증 필요없음」의 예외를 청한다 — 이 작업만 |

묻지 않고 하는 것(관례): 메뉴 루트 `Somnia/Battle/…` · 셰이더 `Somnia/Battle/…` · EditorPrefs 키 · `m_EditorClassIdentifier` 재작성 · 없는 `MobileBuild` 참조 제거 · `tools/battle-core-rebuild/` → `tools/battle/` + 죽은 `check_ledgers.py` 삭제 · 씬은 평면 유지.

## 작업 단위

| # | 단위 | 커밋 |
|---|---|---|
| 0 | [이름](0_rename.md) — 치환 1규칙 전수(코드 · asmdef · 셰이더 · 에셋 식별자 · 도구 · `.claude`) | 1 |
| 1 | [자리](1_relocate.md) — `git mv` + 경로 리터럴 68 파일 + `CoreGoldenStore` + csproj 글롭 + 새 asmdef 2 | 1 |
| 2 | [governance 선제 수정](2_governance_prefit.md) — D4 | 1 |
| 3 | [문서 · 도구](3_docs_tools.md) — CLAUDE.md · reference · `.claude` · spec README | 1 |
| 4 | [검증](4_verify.md) — D5 | (기록만) |

단위 0~2 는 **에디터를 닫고** 한다(asmdef 개명 + 5,000 파일 이동 + 열린 씬 YAML 재작성). 단위 0 과 1 사이의 중간 커밋은 컴파일되는 상태를 지향하지만 검증은 단위 4 에서 한 번에 한다(이전 spec 과 같은 「알려진 것」).

## 체크리스트

- [x] D1~D5 승인 (사용자 2026-10-08 — 전부 추천안)
- [x] 단위 4-0 기준선: 에디터 닫힘 확인 → 배치 EditMode 3 lane 2,212 / 2,201 / 7 / 4
- [x] 단위 0 커밋 `078386d3f` — 코드·에셋·도구 집합 `grep -rw Wassup` 0 (옛 ECS 인용 `Wassup.Battle.*` 28줄은 이력으로 복원 — `d297475aa`)
- [x] 단위 1 커밋 `fa13b6802` — 5,264 rename(GUID 보존) · 경로 리터럴 0 잔존 · 보정 `d297475aa`(세그먼트 조립 2 · fixture 21)
- [x] 단위 2 커밋 `70686ccaf` — `govcheck.py` 네 검사 0(잔존 = Spine 마커 폴더 1, ⑤ 예외)
- [x] 단위 3 커밋 — 설명 문서 20 파일 · 이 spec 의 구현 결과
- [x] 단위 4: 변경 후 배치 2,212 / 2,201 / 7 / 4 — 기준선과 실패 id 집합 동일(선행 빨강 7) · 골든 14/14 · 헤드리스 빌드 2 exit 0
- [x] 사용자: 에디터 열기 → 재임포트 → 컴파일 0(`error CS` 0, 새 예외 0 — 남은 건 알려진 Hovl 1)
- [x] 사용자: Play 한 판 (2026-10-08 「플레이 확인함」)
- [ ] push 승인

## 완료 기준

Demo 가 somnia 의 이름·자리를 이미 하고 있고(`_Project` 밑에 `Wassup` 0 · `Assets` 루트에 코드 있는 벤더 0), 전 lane 이 기준선과 같으며, Play 가 된다. ⑤는 폴더 복사 + somnia 쪽 수용 준비만 남는다.

## ⑤ 에 넘기는 것 (somnia 리포 작업 — 이 spec 범위 밖)

- ADR-0043(가칭) 「dreamsquad 이식 모듈」: `Somnia.Battle.*` 가족 · `Runtime/Battle/` · Spine 4.3 모듈 한정 · Burst/Collections/Mathematics(6.6 부터 Mathematics 는 엔진 모듈) · Timeline(somnia ledger 「Installed but not approved」인데 PixPlays 산 프리팹이 쓴다) · newtonsoft(런타임 사용 → product-use) · 벤더 `Resources` 예외 1(Spine 에디터 마커).
- PrimeTween: somnia 는 레지스트리 `1.4.6`, Demo 는 tgz `1.4.12`(승인 라인 `1.4.x` 안). 반입 때 Demo 의 `Packages/*.tgz` · `Assets/Plugins/PrimeTween/` 은 제외하고 somnia 것을 쓴다(둘이면 어셈블리 중복).
- 제외: `Assets/InputSystem_Actions.inputactions` · `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` · `Assets/TextMesh Pro/`(Demo 폰트 SDF 는 `Runtime/Battle/Fonts/` 로 간다) · `ProjectSettings/*` · `Packages/manifest.json`.
- D1(기본 RP 에셋 — somnia 2D 유지 + `Mobile_Renderer` 를 Renderer 목록에 추가, 카메라 인덱스) · Always Included Shaders 에 `Somnia/Battle/*` · `EditorBuildSettings` 씬 추가 · docs 프런트매터 · `.agents/skills` 미러 · PlayMode lane.
- 2026-10-01 플랜 v1(`scratchpad/somnia-migration-plan.md`)의 §2-4 패키지 표 · §5 B~E 단계가 그대로 유효하다.
