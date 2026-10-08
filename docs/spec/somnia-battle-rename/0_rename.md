# 단위 0 — 이름: `Wassup` → `Somnia.Battle` 치환 1규칙 전수

**전제**: 에디터 닫힘. 자리는 안 바꾼다(단위 1). 이 단위만으로 컴파일되는 상태를 지향한다.

## 규칙

| 패턴 | → | 대상 파일 |
|---|---|---|
| `\bWassup\.` (네임스페이스 · 완전수식 · 문자열 접두) | `Somnia.Battle.` | `.cs` 801 · asmdef 10 · csproj 4 · sh 1 · `.mjs` 1 · `.md`(아래 범위) |
| `namespace Wassup\b` · `using Wassup\b` · `using static Wassup\b` | `… Somnia.Battle` | `.cs` |
| `"Wassup/` (MenuItem 21 · CreateAssetMenu 69 · 테스트 접두 단언 1) | `"Somnia/Battle/` | `.cs` |
| `Shader "Wassup/` | `Shader "Somnia/Battle/` | `.shader` 16 |
| `m_EditorClassIdentifier: Wassup.Runtime::Wassup.` | `… Somnia.Battle.Runtime::Somnia.Battle.` | `.asset` · `.prefab` · `.unity`(`_Project` 밑 596 + 씬 38) |
| asmdef 파일명 `Wassup.X.asmdef` (+ `.meta` 동반) | `Somnia.Battle.X.asmdef` | `git mv` — GUID 보존 |
| asmdef JSON `name` · `rootNamespace` · `references[]` | 같은 규칙. `Wassup.Runtime` 의 `rootNamespace` 는 `Wassup` → `Somnia.Battle` | 10 |
| asmdef 참조 `Wassup.Editor.MobileBuild` (없는 어셈블리) | 삭제 | `Tests/EditMode` · `Tests/EditModeAssets` |
| `InternalsVisibleTo("Wassup.Tests.…")` | 규칙 | 2 파일 4줄 |
| EditorPrefs 키 `"Wassup.UnitStatImport.*"` | `"Somnia.Battle.UnitStatImport.*"` | `UnitStatImportWindow.cs` 5 |
| 코드 주석의 `Wassup.X` | 규칙(주석도 코드 식별자다) | 70줄 |

**치환하지 않는 것**: 소문자 `wassup`(프로젝트 별명 — CLAUDE.md 제목 · 헤드리스 csproj 의 맥 절대경로 `wassup-core` 는 단위 1 에서 지운다) · 닫힌 spec 문서(`docs/spec/battle-core-rebuild` 등 — 이력) · 벤더 폴더 · `Library/`.

## 순서

1. asmdef 10: `git mv` 파일명 → JSON 필드 치환(이름 · rootNamespace · references) → `MobileBuild` 참조 제거.
2. `.cs` 전수 치환(위 규칙 순으로, 한 스크립트).
3. 셰이더 16 · 에셋 식별자(`_Project/**/*.{asset,prefab,unity}`) 치환 — YAML 은 그 한 줄만 바뀐다(줄바꿈 보존, 바이너리 없음).
4. 도구: `tools/battle-core-rebuild/headless/*.csproj`(`AssemblyName` · `RootNamespace` · `<Reference Include="Wassup.*">` · HintPath) · `verify-fresh-skills.sh` · `.claude/hooks/guardrails.mjs`(`Wassup.Tests.PlayMode.Core` 2곳) · `.claude/agents/core-reviewer.md` · `.claude/skills/{unity-feature-wiring,unity-vfx-integration}/SKILL.md`.
5. 코드 폴더 안의 `README.md` 3(`Scripts/BattleCore` · `Scripts/UnitAi` · `Tests/EditModeCore`) — 식별자만.
6. 확인: `grep -rwn Wassup --include=*.cs --include=*.asmdef --include=*.shader --include=*.csproj --include=*.sh --include=*.mjs Assets tools .claude` 가 0 · `grep -rn "Wassup" Assets/_Project --include=*.asset --include=*.prefab --include=*.unity` 가 0 · asmdef JSON 파싱 OK · 참조 이름이 전부 존재.

## 조심

- 셰이더 이름을 바꿔도 머티리얼은 GUID 로 잇는다(74 전부 `m_Shader: {fileID: 4800000, guid: …}`). 코드의 `Shader.Find("Wassup/…")` 는 0 이라 짝이 없다. `BattleAuthoringAssetTests` 의 `StartsWith("Wassup/")` 만 같이.
- `m_EditorClassIdentifier` 는 에디터가 다음 저장 때 어차피 새 값으로 쓴다. 미리 쓰는 이유는 `MarkerPropStyleAssetTests.YamlBlockContaining(scene, "Wassup.Presentation.MarkerPropInstaller")` 가 씬 텍스트를 읽기 때문 — 그 리터럴도 규칙으로 바뀐다.
- 메뉴 루트가 `Somnia/Battle/…` 로 가면 사용자가 쓰던 메뉴 자리가 바뀐다(CLAUDE.md · lessons 의 메뉴 경로 서술은 단위 3).
- 결정론: 이름은 시뮬에 안 들어간다. 골든 11 은 `Wassup` 언급 0.

## 구현 결과

커밋 `078386d3f`(2026-10-08, 1,311 파일) — 스크립트 `scratchpad/rename.py`(dry-run → apply). 치환 수: .cs 801 파일 2,689곳 · asmdef 10 파일 49 필드(+`MobileBuild` 참조 2 제거) · 셰이더 16 파일 17곳 · 에셋/씬 YAML 461 파일 604줄 · 도구/`.claude`/README/fixture 13 파일 69곳. 코드·에셋·도구 집합에서 `grep -rw Wassup` 0, asmdef 15 의 참조 전부 해소.

dry-run 이 잡아 규칙에 보탠 것: `Window/Wassup/…` 메뉴 4 · 주석/툴팁의 셰이더·메뉴 경로(`\bWassup/` 로 일반화) · asmdef 이전의 낡은 식별자 `Assembly-CSharp(-Editor)::Wassup.*` 8.

**이력 예외(단위 3 커밋에서 되돌림)**: 옛 ECS 루트 네임스페이스 `Wassup.Battle.*` 인용 28줄(코드 주석 12 파일 · docs 2 · `.claude` 1)은 규칙이 존재한 적 없는 `Somnia.Battle.Battle.*` 를 만들어 `Wassup.Battle.*` 로 복원했다 — 「salvaged from」 경로 주석과 같은 취급. 그래서 코드의 `Wassup` 잔존은 그 인용뿐이다.
