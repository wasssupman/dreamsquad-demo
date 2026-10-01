# depth-parallax-removal — 뎁스맵 패럴랙스 기능 제거

상태: 초안 2026-10-01 (사용자 승인 대기)

## 목표

뎁스맵 패럴랙스(2D 그림 + R8 깊이맵 + 틸트 → 의사 입체 회전) 기능을 **프로젝트에서 완전히 제거**한다. 모듈 `Assets/_Project/Modules/DepthParallax/` 와 그 유일한 소비처(로비 배경), 데이터 필드, 텍스처, 빌드 설정, 문서 언급까지. 2026-10-01 사용자 결정: *「기능 제거 및 프로젝트에서 패럴랙스 자체를 제거하자」*.

배경: 원래 소비처는 둘이었다 — 배치 컷신(`DeployCutscenePlayer`, de2275eef)과 로비 배경(`LobbyBackgroundParallax`, 7ab315c40·068fdc234). 컷신 쪽 코드는 이미 삭제됐고(`DeployCutscenePlayer.cs` 등 부재), **`DefenderUnitData.deployCutsceneDepth` · `deployCutsceneTiltGain` 필드와 디펜더 7기의 깊이 텍스처만 고아로 남아 있다**. 살아 있는 소비처는 로비 배경 하나다.

## 의존성 전수 (2026-10-01 조사)

| 종류 | 대상 | 처분 |
|---|---|---|
| 모듈 | `Assets/_Project/Modules/DepthParallax/` 전체 — Runtime 3 cs + asmdef · Editor 1 cs + asmdef · Tests 1 cs + asmdef · Shaders(`.cginc`·`_UI.shader`·`_Default.mat`) · `Tools~/depth_bake.py`·`depth_layer_remap.py` | 폴더째 삭제 |
| 코드 소비처 | `Scripts/UI/Outgame/LobbyBackgroundParallax.cs` (유일) | 삭제 |
| 코드 접점 | `Scripts/UI/Outgame/LobbyBackgroundDissolve.cs` — 프로퍼티 ID 5개(`_Tilt`·`_DepthTex`·`_Amplitude`·`_DepthCenter`·`_DepthSign`) · `SetParallaxParams` · `SetParallaxTilt` | 제거 |
| 셰이더 접점 | `Shaders/Background_Dissolve_UI.shader` — 프로퍼티 5 · `#include "../Modules/DepthParallax/Shaders/DepthParallax.cginc"` · 유니폼 5 · `DepthParallaxOffset` 호출 1 | 제거(`uv = i.uv`) |
| 데이터 필드 | `Scripts/Data/DefenderUnitData.cs:325,327` `deployCutsceneDepth` · `deployCutsceneTiltGain` — **소비처 0** | 제거 |
| asmdef | `Scripts/Wassup.Runtime.asmdef` references 의 `Wassup.DepthParallax` | 제거 |
| 빌드 설정 | `ProjectSettings/GraphicsSettings.asset` Always Included Shaders 의 `DepthParallax_UI.shader`(guid `00c18400…`) | 제거 |
| 헤드리스 | `tools/battle-core-rebuild/headless/BattleCoreUnity.Check.csproj:63` `Wassup.DepthParallax.dll` Reference | 제거 |
| 씬 | `Scenes/OutgameScene.unity` GameObject `1248283347` 의 `LobbyBackgroundParallax` 컴포넌트(`depthMap`·`settings` 참조 포함) | 컴포넌트 제거 |
| SO | `Data/DepthParallaxSettings.asset`(참조 0) · `Data/LobbyParallaxSettings.asset`(씬 컴포넌트만 참조) | 삭제 |
| 텍스처 | `Art/Depth/lobby_bg_depth.png`(참조 0) · `lobby_bg_neon_depth.png`(씬 컴포넌트만) · `Sprites/Cutscene/{Archer,Cannon,FireCaster,Guardian,Healer,Ranger,Sniper}/Depth/*_depth.png` 7장(디펜더 SO 의 고아 필드만 참조) | 삭제(`Depth/` 폴더째) |
| 디펜더 SO | `Data/Defenders/Defender_*.asset` 30개의 `deployCutsceneDepth:` · `deployCutsceneTiltGain:` 줄 | 필드 삭제 후 YAML 줄 정리 |
| 문서 | `docs/reference/test-procedure.md:18`(모듈 테스트 asmdef 언급) · `docs/reference/lessons/03-rendering-assets.md:167`(2026-07-15 셰이더 스트리핑 사고 사례 — **이력이라 유지**, 주석 1줄) | 갱신 |
| 없음(확인) | 시트 컬럼 · 테스트 참조(모듈 밖) · `.gitattributes` · 스킬 · CLAUDE.md · `LobbyBackgroundDissolve.mat` 저장 프로퍼티 · 다른 셰이더의 `.cginc` include | — |

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_code_shader_build.md` | 모듈·소비처·셰이더·asmdef·GraphicsSettings·csproj·데이터 필드 제거 — 컴파일 + EditMode |
| 1 | `1_scene_and_assets.md` | 씬 컴포넌트 제거 · SO/텍스처 삭제 · 디펜더 SO YAML 정리 — Play 스모크 + EditMode.Assets |
| 2 | `2_docs.md` | 문서 2곳 갱신 · spec 색인 |

## 공통 원칙

- **로비 배경의 낮/밤 디졸브는 유지**된다. 패럴랙스는 디졸브 셰이더에 UV 오프셋 한 줄로 얹혀 있었고(`_Tilt=0` 이면 픽셀 동일 — 셰이더 주석 「무회귀」), 떼어내면 원래 디졸브로 돌아간다.
- 씬 편집은 **에디터에 그 씬이 열려 있으면 MCP 로, 아니면 YAML 로**(CLAUDE.md 「Unity 함정」). 컴포넌트 블록과 GameObject 의 `m_Component` 항목을 함께 지운다.
- 디펜더 SO 의 고아 YAML 줄은 Unity 가 다음 저장에서 조용히 버리지만, 30개가 각자 다른 시점에 dirty 가 되면 무관한 커밋에 섞인다 → 같은 커밋에서 정리한다.
- 커밋은 경로 지정. 단위 0(코드)과 단위 1(직렬화 에셋)은 **커밋을 나눈다** — 셰이더/asmdef 변경과 씬 변경은 되돌릴 이유가 다르다.

## 후속 후보

- `Background_Dissolve_UI.shader` 의 `_Tilt` 류 프로퍼티를 뺀 뒤 `LobbyBackgroundDissolve.mat` 에 남은 미사용 직렬화 프로퍼티가 있으면 Unity 「Remove Unused Properties」 — 조사 시점엔 없음.
- somnia 이식 플랜(세션 스크래치 v1)의 DepthParallax 행 2개 삭제 — 이 spec 종료 후.
