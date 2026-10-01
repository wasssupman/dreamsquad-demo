# 0 — 코드 · 셰이더 · 빌드 설정 제거

## 목적

패럴랙스 모듈과 그 코드 접점을 지워 **컴파일이 통과하고 `Wassup.DepthParallax` 토큰이 리포에 0건**이 되게 한다. 직렬화 에셋(씬·SO·텍스처)은 단위 1.

## 변경 대상

1. 삭제: `Assets/_Project/Modules/` **폴더째**(+`Modules.meta`). `Modules/` 에는 처음부터 지금까지 `DepthParallax` 하나만 있었다(git 이력 확인) — 빈 상위 폴더를 남기지 않는다. `Tools~` 포함
2. 삭제: `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundParallax.cs` (+`.meta`)
3. 수정: `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundDissolve.cs`
   - 프로퍼티 ID 5개(`TiltId`·`DepthTexId`·`AmplitudeId`·`DepthCenterId`·`DepthSignId`)와 그 위 주석
   - `SetParallaxParams(...)` · `SetParallaxTilt(...)` 메서드와 주석 블록
4. 수정: `Assets/_Project/Shaders/Background_Dissolve_UI.shader`
   - Properties 의 `_DepthTex`·`_Tilt`·`_Amplitude`·`_DepthCenter`·`_DepthSign`
   - `#include "../Modules/DepthParallax/Shaders/DepthParallax.cginc"`
   - 유니폼 선언 5줄 · `float depth = tex2D(_DepthTex, …)` · `DepthParallaxOffset(...)` → `float2 uv = i.uv;`
5. 수정: `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `deployCutsceneDepth` · `deployCutsceneTiltGain` 필드와 주석 2줄 제거(소비처 0)
6. 수정: `Assets/_Project/Scripts/Wassup.Runtime.asmdef` — `references` 에서 `Wassup.DepthParallax` 제거
7. 수정: `ProjectSettings/GraphicsSettings.asset` — `m_AlwaysIncludedShaders` 의 `{fileID: 4800000, guid: 00c18400f708c43b39a5d01d81cd5db5, type: 3}` 1줄 제거
8. 수정: `tools/battle-core-rebuild/headless/BattleCoreUnity.Check.csproj` — `Wassup.DepthParallax.dll` `<Reference>` 1줄 제거

## 구현

- 셰이더는 `_Tilt=0` 일 때 오프셋 0 이라 **픽셀 결과가 바뀌지 않는다**(셰이더 주석 「rest → uv==i.uv → 기존 디졸브와 픽셀 동일」). 그래서 시각 회귀 위험은 없고, 컴파일 통과만 보면 된다.
- `DefenderUnitData` 필드를 지우면 30개 디펜더 SO 에 고아 YAML 줄이 남는다 — 단위 1 에서 정리. 이 단위에서는 Unity 가 경고 없이 무시한다.
- `GraphicsSettings.asset` 은 열린 씬이 아니라 YAML 직접 편집 가능.
- 삭제 뒤 `rg -i 'depthparallax|DepthParallaxOffset|deployCutsceneDepth|deployCutsceneTiltGain' Assets tools docs` 가 **문서 2곳(단위 2)** 외 0건이어야 한다.

## 완료 기준

- [ ] Unity 컴파일 0 에러 · 콘솔에 `Wassup.DepthParallax` 어셈블리/셰이더 관련 경고 0
- [ ] EditMode 전체 실행에서 **`DepthParallax` 테스트 케이스 0건**, 나머지 3 어셈블리 결과는 선행 빨강(백로그 「(마) 사용자 몫」 3건) 외 초록. ※ 「total 이 6 줄어든다」는 틀린 기준이었다 — 기존 집계(2,650)는 MCP `assembly_names` 실행이라 모듈 asmdef `Wassup.DepthParallax.Tests` 를 애초에 세지 않았다
- [ ] 헤드리스 `BattleCoreUnity.Check.csproj` 가 `-p:UnityScriptAssemblies=<Library/ScriptAssemblies>` 로 빌드됨
- [ ] 위 `rg` 가 문서 2곳 외 0건
- [ ] 커밋(경로 지정): 모듈 폴더 · `LobbyBackgroundParallax.cs` · `LobbyBackgroundDissolve.cs` · `Background_Dissolve_UI.shader` · `DefenderUnitData.cs` · `Wassup.Runtime.asmdef` · `GraphicsSettings.asset` · csproj
