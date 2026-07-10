# 0 — SceneTransition 코어 + 페이드 토대 (A)

## 목적

전 씬에 걸쳐 살아있는 `SceneTransition` persistent 컨트롤러를 만들고, 단색 페이드로 감싼 async 씬 로딩을 end-to-end 로 동작시킨다. 이 단위만으로 "끊김 없는 페이드 전환"이 완성되며, 컷인(단위 2)은 이 위에 얹힌다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/SceneTransition.cs` — persistent 컨트롤러. 수치는 이 컴포넌트의 `[SerializeField]` (별도 설정 SO 없음).
- 신규 프리팹 `Assets/_Project/Resources/SceneTransition.prefab` — 부트스트랩용(Resources 로드). **이 프리팹이 수치 authoring 소스**(제약 #6, SO 또는 프리팹 허용).

## 구현

- **부트스트랩**: `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 에서 `Resources.Load<GameObject>("SceneTransition")` → `Instantiate` → `DontDestroyOnLoad`. 씬 배선 불요. 중복 시 자기파괴(계약 #3).
- **캔버스**: 프리팹에 `Canvas`(ScreenSpaceOverlay, sortingOrder 매우 높게 ~10000), `CanvasScaler`(1920×1080), 풀스크린 `Image`(cover), `CanvasGroup`(alpha 제어). 초기 alpha 0, `blocksRaycasts=false`.
- **공개 API**: static `public static void Go(string sceneName)` — 내부에서 `Instance` null-guard(없으면 `SceneManager.LoadScene` 직접 호출로 degrade) 후 인스턴스 코루틴 시작. **`Instance.Go` 는 외부 비노출**(공개 표면 1개, 계약 #1). 재진입 방지 플래그(계약 #2). 코루틴:
  1. `blocksRaycasts=true`, PrimeTween 으로 CanvasGroup alpha 0→1 (fadeIn 시간, `useUnscaledTime=true`).
  2. `var op = SceneManager.LoadSceneAsync(sceneName, Single); op.allowSceneActivation=false;` `op.progress>=0.9f` 까지 대기.
  3. min cover time 보장 대기 → `op.allowSceneActivation=true`, 씬 활성 대기.
  4. alpha 1→0 (fadeOut), `blocksRaycasts=false`, 플래그 해제.
- **컷인 훅**: 단위 2 가 끼어들 지점을 코루틴에 남긴다(예: cover 완료 후 `yield return PlayCutInIfAny(direction)` — 이 단위에선 no-op). `direction` 은 target 씬으로 결정.
- **SerializeField 필드**(프리팹 authoring): `fadeInDuration`, `fadeOutDuration`, `minCoverSeconds`, `coverColor`. 리터럴 금지.
- **time-scale**: 모든 트윈 `useUnscaledTime` (계약 #7). `Time.timeScale` 미사용.

## 완료 기준

- compile clean (read_console 0 error).
- 임시 검증: 아무 씬에서 `SceneTransition` 인스턴스 확인, `SceneTransition.Go(SceneNames.Battle)` 호출 시 화면이 coverColor 로 덮였다가 BattleScene 로 열리며 페이드 아웃.
- PlayMode 스모크 1개: `SceneTransition.Go(Battle)` → 대기 → `SceneManager.GetActiveScene().name == "BattleScene"` 이고 `SceneTransition.Instance != null`(persistent 유지).
- 전환 중 로딩 순간(빈 화면)이 노출되지 않음(육안).

확인: 2026-07-10 — PlayMode 스모크 passed(부트스트랩·씬전환·persistent), 사용자 Play 에서 검정 페이드 육안 확인(START→cover→Battle→fade-out).
