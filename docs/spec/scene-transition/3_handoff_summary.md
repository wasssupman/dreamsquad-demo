# 3 — Handoff Summary (scene-transition)

씬 전환 연출 feature 구현·검증·커밋 완료. 다음 작업자를 위한 인계 지도. 최신 계약은 README / 번호 문서 우선.

## Commit

- `280a10e9` unit 0 — persistent 페이드 전환 토대 + 자기부트스트랩 + static `Go`
- `3fb2c685` unit 1 — 호출부 3곳 → `SceneTransition.Go` 배선
- `b61b6523` unit 2 — 로비 골든 디졸브 재활용(낮/밤 스왑 노출 버전) + 스파인 로딩 화면
- `cae9c51a` unit 2 rev — 디졸브가 스파인 로딩 화면 직접 노출 + 로딩 2초 (최종형)

## Implemented

- `SceneTransition` — DontDestroyOnLoad persistent 컨트롤러. `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 로 `Resources/SceneTransition.prefab` 자동 생성(씬 배선 0). 유일 공개 API = static `Go(sceneName)` (null-guard→hard-cut degrade).
- 전환 시퀀스: front(현재 시간대 배경, dissolve mat) 불투명 스냅 → `_Dissolve` 0→1 골든 파면 디졸브(클릭 지점에서 퍼짐) → 다크 배경 위 Casual Character 러닝 로딩 화면 2초 → 커버 페이드아웃 → 다음 씬.
- **방향 인지**: 현재 씬에 `LobbyBackgroundDissolve` 있을 때만(로비→배틀) 배경 디졸브. **배틀→로비는 디졸브 없이 로딩 화면만 페이드인**(`loadingFadeIn`) 후 로비.
- 로비 디졸브 셰이더(`Background_Dissolve_UI`) 재사용, 전역 gold 틴트 off(파면 글로우만). 라디얼 중심 = Input System 포인터 UV. `LobbyBackgroundDissolve.IsNight` 로 front=화면 현재 상태 동기화.
- `allowSceneActivation` 게이팅으로 로딩 빈 프레임 미노출, 재진입 멱등, 모든 모션 unscaledTime(TimeManager 독립).

## Key Files

- `Assets/_Project/Scripts/Core/SceneTransition.cs` — 컨트롤러(전 로직).
- `Assets/Resources/SceneTransition.prefab` — 커버 3레이어(Under 다크 / LoadingSpine / Front dissolve) + 모든 수치 authoring.
- `Assets/_Project/Shaders/Background_Dissolve_UI.shader` — `_Invert` 토글 추가(기본 0).
- `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundDissolve.cs` — `IsNight` getter.
- `Assets/_Project/Scripts/UI/{Outgame/OutgameMenuController, Outgame/TestModePanelView, MenuPopup}.cs` — 호출부.
- `Assets/_Project/Tests/PlayMode/SceneTransitionSmokeTest.cs` — 스모크.

## Verified

- compile 0 error, 콘솔/Spine 에러 0.
- PlayMode 스모크 passed: 부트스트랩 자동 생성, Battle 전환, persistent 유지, front 가 디졸브 셰이더 인스턴스 사용.
- 사용자 Play 확인: 버튼 중심 골든 파면 디졸브(gold 워시 없음) → 러닝 로딩 2초 → 배틀. 낮/밤 현재 상태 기준 동기화, 깜빡임 해소.

## Notes (되돌리면 안 되는 의도)

- `goldenTintStrength=0` 의도 — 화면 전체 gold 워시 제거(파면 글로우만 버튼에서 퍼짐).
- 커버 페이드인 제거(불투명 스냅) 의도 — front/under 반투명 시 under 비침 깜빡임 방지.
- Spine 은 CanvasGroup 호환 머티리얼(`SkeletonGraphicDefault-CanvasGroup`) 필수 — 아니면 커버 페이드 시 안 사라짐.
- 셰이더 `_Invert` 는 기본 0 이라 로비 배경 디졸브 무영향.
- 부트스트랩은 Resources 프리팹 기반 — 씬에 오브젝트 배치하지 말 것(중복 자기파괴).

## Follow-up

- **로비 캐릭터·버튼 가려짐**: front 가 배경만 복제 → 전환 시작 시 전경 UI 즉시 사라짐. 해소 = 전체 화면 스크린샷 커버(알파·상하반전 편차로 보류, 사용자 "복잡하면 하지마"). 재도전 시 셰이더 `_OpaqueBase` 토글 + orientation 처리.
- Spine 전용 브랜드 컷인 애니(현재는 Casual Character 러닝 재사용).
