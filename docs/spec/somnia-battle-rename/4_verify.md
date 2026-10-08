# 단위 4 — 검증 (D5)

## 4-0. 기준선 (단위 0 전에, 에디터 닫힘)

```
"/c/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe" -batchmode -projectPath <repo> -runTests -testPlatform EditMode \
  -assemblyNames "Wassup.Tests.EditMode.Core;Wassup.Tests.EditMode;Wassup.Tests.EditMode.Assets" -testResults <scratch>/baseline.xml -logFile <scratch>/baseline.log
```

결과 XML 에서 `total · passed · failed` 와 **실패 테스트 id 집합**을 저장한다(선행 빨강: 이 클론의 CRLF 9 — `LiveDefinitionSmokeTests` 8 · `MarkerPropStyleAssetTests` 1, 메모리 `clone-remote-and-unity-setup`). `Temp/UnityLockfile` 이 있으면 에디터가 열린 것 — 돌리지 않는다.

## 4-1. 변경 후 (단위 0~3 커밋 뒤)

같은 명령에 새 어셈블리 이름 `Somnia.Battle.Tests.EditMode.Core;Somnia.Battle.Tests.EditMode;Somnia.Battle.Tests.EditMode.Assets`. 첫 실행은 이동한 5,000 파일의 재임포트로 수 분.

판정: `total` 동일 · 실패 id 집합 동일(이름 접두만 다름) · `CoreGoldenTests` 11 전부 통과(결정론 무변).

실패가 늘면 **이름/자리 외의 원인을 먼저 의심**한다(경로 리터럴 누락이 1순위 — 단위 1 의 grep 재확인).

## 4-2. 헤드리스 빌드 lane

`dotnet build tools/battle/headless/BattleCore.csproj -p:UnityScriptAssemblies=<repo>/Library/ScriptAssemblies` 와 `BattleCoreUnity.Check.csproj` — 4-1 의 배치가 `Library/ScriptAssemblies` 에 새 이름의 DLL 을 남긴다. 테스트 lane 은 Smart App Control 로 막혀 있다(기존).

## 4-3. 사용자

- 에디터 열기 → 재임포트 · 컴파일 0 · 콘솔에 asmdef/Spine/PrimeTween 경고 0.
- `Somnia/Battle/BattleCore/씬 열고 플레이 (기본 모드)` → 판 기동 · 배치 · 상세 패널.
- (선택) PlayMode.Core — 테스트 캐리(단위 2-1)가 걸려 있다. 선행 빨강 7(드래그 미리보기, 포인터 없음)은 기존.

## 기록

2026-10-08, 에디터 닫고 배치(`scratchpad/{baseline,after,after2}.xml`).

| 항목 | 기준선(단위 0 전) | 변경 후(단위 0~2 + 보정 `d297475aa`) |
|---|---|---|
| 3 lane 합계 total / passed / failed / skipped | 2,212 / 2,201 / 7 / 4 (36초) | **2,212 / 2,201 / 7 / 4** (46초) |
| 실패 id 집합 | 선행 빨강 7 — `DreamcatcherCardArtTests` 1 · `DreamcatcherCardAssetTextTests` 1 · `UnitKitCatalogTests` 1 · `CardBakeSnapshotTests` 2 · `SheetFullRoundTripTests` 1 · `SkillSheetRoundTripTests` 1 | **같은 7**(접두만 `Somnia.Battle.`) |
| 골든(`CoreGoldenTests`) | 14/14 | **14/14** — 결정론 무변 |
| 헤드리스 빌드 `BattleCore` · `BattleCoreUnity.Check` | — | **둘 다 exit 0**(배치가 남긴 `Library/ScriptAssemblies/Somnia.Battle.*.dll` 기준) |
| 에디터 컴파일 | — | 사용자 확인 대기 |
| Play | — | 사용자 확인 대기 |

중간 실행(`after.xml`, 보정 전)은 새 빨강 5 — 전부 경로: 세그먼트 조립 `Path.Combine(dataPath, "_Project", "Scripts", …)` 2곳(4 테스트) · `binding_bake_snapshot.txt` 의 굳힌 에셋 경로. 단위 1 「구현 결과」에 기록, `d297475aa` 로 보정 후 재실행이 위 표.
