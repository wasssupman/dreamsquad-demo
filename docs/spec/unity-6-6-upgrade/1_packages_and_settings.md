# 1 — 패키지 · 프로젝트 설정 핀

## 목적

프로젝트를 6000.6.3f1 에 핀한다. 대부분은 **에디터가 이미 작업 트리에 써 둔 것**을 검토해 싣는 일이고, 손으로 고치는 건 manifest 2줄이다.

## 변경 대상

| 파일 | 내용 | 손 수정 |
|---|---|---|
| `Packages/manifest.json` | 6.6 이 올린 버전 16개(URP·shadergraph 17.6 · Burst 2.0.0 · Collections 6.6.0 · Input System 1.20 · test-framework 1.8 · ugui 2.6 · Timeline 6.6 · probuilder 6.1.2 · tilemap.extras 9.0.1 · mathematics 1.4.0 · multiplayer.center 2.0.1 · visualscripting 1.9.12 · collab-proxy 2.13.6 · ai.navigation 2.0.14) · 모듈 추가 3(`physicscore2d` · `tetgen` · `timelinefoundation`) · `modules.vr` 제거 | **`com.coplaydev.unity-mcp` 줄 제거** · **`com.kyrylokuzyk.primetween` 줄 복구**(tgz 는 `Assets/Plugins/PrimeTween/internal/` 에 있다, 787 KB) |
| `Packages/packages-lock.json` | 위에 맞춰 에디터가 다시 쓴다 — manifest 손 수정 뒤 에디터가 resolve 한 **결과**를 싣는다(손으로 쓰지 않는다) | — |
| `ProjectSettings/ProjectVersion.txt` | `6000.6.3f1 (45d8eee7de74)` | — |
| `ProjectSettings/PackageManagerSettings.asset` · `ShaderGraphSettings.asset` · `Packages/com.unity.probuilder/Settings.json` | 6.6 포맷 | — |
| `ProjectSettings/PhysicsCoreProjectSettings2D.asset` · `ProjectAuditorSettings.asset` | 6.6 이 새로 만든 파일(미추적) | `git add` |

## 구현

- manifest 를 고치면 열린 에디터가 Package Manager resolve → 도메인 리로드를 한다. unity-mcp 제거는 그 패키지의 에디터 창·메뉴가 사라지는 것 외 영향 없음(이 세션은 MCP 를 쓰지 않는다). PrimeTween 복구 뒤 `Wassup.Runtime` 의 PrimeTween 참조가 다시 해석된다.
- resolve 가 끝난 뒤 `git diff -- Packages/packages-lock.json` 에 unity-mcp 가 없고 primetween 이 있는지 본다.
- `git diff -- ProjectSettings` 를 읽고 **6.6 포맷 전환 외의 값 변화**(색공간·품질·입력 등)가 없는지 확인한다. 과거 이 클론에서 색공간이 Linear→Gamma 로 뒤집힌 적이 있다(`memory/clone-remote-and-unity-setup`). 값이 바뀌어 있으면 싣지 않고 묻는다.
- 함께 뜬 churn 중 **싣지 않는 것**: `Assets/Layer Lab/**.meta` · Spine Examples 아틀라스 · TMP 폰트 SDF · png/jpg meta 재임포트 · `Assets/_Project/Materials.meta`. demo-diet 단위 2 가 Layer Lab 을 지운다.

## 완료 기준

- [x] 사용자 에디터(6.6) 콘솔 **컴파일 에러 0** — `Logs/Editor.log` 의 두 컴파일(unit 0·1 적용 후, 포워더 제거 후) 모두 `error CS` 0 · 셰이더/Burst 에러 0
- [x] `ProjectSettings` 변경은 6.6 포맷·버전뿐(색공간 Linear 유지, `ShaderGraphSettings` 는 줄 끝만 바뀌어 미커밋)
- [x] 커밋 `dc607b059`(7 파일) — `com.unity.mathematics` 줄은 `d73c0d86e` 에서 추가 제거

확인 2026-10-07.
