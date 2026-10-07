# 0 — 코드: asmdef · TMP · 헤드리스 · 경계 테스트

## 목적

6.6 에서 컴파일을 막는 코드 쪽 원인(README #1 · #2 · #6)을 고치고, `noEngineReferences` 가 지키던 경계를 테스트로 옮긴다. 이 단위만 적용한 커밋은 **4.7 에서도 6.6 에서도** 컴파일된다.

## 변경 대상

1. **asmdef 3개** — `Scripts/BattleCore/Wassup.BattleCore.asmdef` · `Scripts/Skills/Wassup.Skills.asmdef` · `Scripts/UnitAi/Wassup.UnitAi.asmdef`: `"noEngineReferences": true` → `false`. **`references` 에서 `Unity.Mathematics` 를 뺀다** — 6.6 에서 그 이름은 빈 포워더 패키지(`com.unity.mathematics` 1.4.0, source builtin, 의존 패키지 0)의 asmdef 이고 타입은 엔진 모듈에서 자동 참조된다(`Assets/_Project/Editor` 가 참조 없이 이미 쓰고 있었다). 같은 이유로 `Wassup.Runtime` · 테스트 asmdef 4개의 참조도 빼고, manifest 의 `com.unity.mathematics` 줄도 지운다(패키지 설명: 「모듈로 옮겨져 이제 비어 있고 불필요」). 6.6 에서 처음 만든 프로젝트의 모양이다 — 포워더 흔적을 남기지 않는다(사용자 2026-10-07: 「땜빵 수정하면 안 된다」).
2. **TMP 4줄** — `enableWordWrapping = false` → `textWrappingMode = TextWrappingModes.NoWrap`:
   - `BattleCoreUnity/Hud/CoreHudUi.cs:111`
   - `Presentation/UnitOverheadView.cs:409`
   - `UI/Draft/WavePatternStripView.cs:309,511`
3. **헤드리스 lane** `tools/battle-core-rebuild/headless/`:
   - `BattleCore.csproj` · `BattleCore.Tests.csproj`: 속성 `UnityEngineDir`(기본값 = 이 머신의 6.6 경로, `-p:` 로 덮어씀) 추가 · `UnityEngine.MathematicsModule.dll` 참조 추가. 기존 `Unity.Mathematics.dll` 참조는 포워더라 남겨도 무해하지만 **지운다**(한 타입이 두 어셈블리로 보이는 혼선 방지). `CheckUnityAssemblies` 타깃의 존재 검사는 모듈 DLL 로.
   - `BattleCoreUnity.Check.csproj:53`: 같은 교체. `UnityEngineDir` 기본값의 `6000.4.3f1` mac 경로 → 6.6.
   - `verify-fresh-skills.sh:36`: 임시 csproj 의 `Unity.Mathematics` 참조 → `UNITY_ENGINE_DIR` 환경 변수 기반 모듈 참조.
   - csproj 머리 주석(「엔진 참조가 하나라도 있으면 여기서 먼저 빨개진다」)을 「MathematicsModule 하나만 참조한다 — CoreModule 타입을 쓰면 빨개진다」로.
4. **`Tests/EditModeCore/CoreArchitectureTests.코어에는_엔진_참조가_없다`** — 스캔 범위를 `BattleCore/` 에서 **`BattleCore/` · `Skills/` · `UnitAi/`** 로 넓히고 금지어에 `UnityEditor` 추가. `Unity.Mathematics` 네임스페이스는 허용(기존 금지어 `UnityEngine` 과 부분일치하지 않는다). 이 테스트는 헤드리스 lane 에서도 돈다 — 세 디렉터리 경로를 기존 `CoreDir` 와 같은 방식으로 잡는다.

## 구현

- 순서: asmdef → TMP → 테스트 확장 → 헤드리스. 사용자 에디터(6.6)가 열려 있으면 asmdef·TMP 저장 직후 재컴파일이 돈다 — Play 중이 아닌지 `Temp/UnityLockfile` 과 `Logs/Editor.log` 꼬리로 본다.
- 헤드리스 기본 경로를 Windows 로 바꾸는 것은 이 머신 기준이다. mac 사용자는 `-p:UnityEngineDir=` 로 덮어쓴다(기존 `UnityScriptAssemblies` 와 같은 관례).

## 완료 기준

- [x] `rg noEngineReferences Assets/_Project/Scripts/{BattleCore,Skills,UnitAi}` → 전부 `false`
- [x] `rg enableWordWrapping Assets/_Project` → 0건
- [x] `CoreArchitectureTests.코어에는_엔진_참조가_없다` 가 세 디렉터리를 돌고 초록(헤드리스 14:19 실행에서)
- [x] 헤드리스: Core 빌드 0 에러 · 테스트 1,038/0/4(포워더 제거 전) · Check 빌드 통과 — 6.6 `ScriptAssemblies` + 6.6 `UnityEngineDir`. 그 뒤 테스트 lane 은 Smart App Control 차단(머신 정책)
- [x] 커밋 `08dbd96f9`(12 파일) + `d73c0d86e`(포워더 참조 8 asmdef · manifest · lock)

확인 2026-10-07.
