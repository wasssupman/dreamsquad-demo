# 2 — 디졸브 커버 전환 (로비 디졸브 재활용 → 스파인 로딩 화면)

> **게이트**: 단위 0+1 로 "끊김 없는 전환"이 종결·커밋된 뒤 진입. unit 0 의 검정 커버 `Image` 를 로비 디졸브 커버로 교체.

## 목적

검정 페이드를 버리고, 이미 authored 된 **라디얼 골든 디졸브**(`Wassup/UI/BackgroundDissolve`, 로비 캐릭터 터치 배경 전환)를 씬 전환 커버로 승격한다. 최종 흐름:

1. **골든 디졸브** — 현재 시간대 배경(front)이 **클릭 지점(START 버튼)에서 퍼지는 골든 파면**으로 걷힌다. 캐릭터 클릭과 동일한 셰이더·모션.
2. **스파인 로딩 화면** — 걷힌 뒤 드러나는 다크 배경 위에서 Casual Character `SkeletonGraphic` 러닝이 **2초** 재생되며 다음 씬 로딩.
3. **다음 씬** — 커버 페이드아웃으로 로드된 씬 노출.

**방향 인지**: 배경 디졸브는 **현재 씬에 로비 배경(`LobbyBackgroundDissolve`)이 있을 때만**(로비→배틀). **배틀→로비**는 디졸브할 배경이 없으므로 **골든 디졸브 없이 로딩 화면만 페이드인** 후 로비 랜딩.

## 재활용 자산 (신규 제작 없음)

- 셰이더 `Assets/_Project/Shaders/Background_Dissolve_UI.shader` — `_Dissolve` 0→1 로 앞 레이어가 파면을 따라 사라지며 뒤가 드러남. `_Mode 1`(radial), `_Center`(UV), `_MaxRadius`, `_Aspect`, 골든 `_EdgeColor`(파면 글로우). **`_Invert`(신규, 기본 0)** — 필드 반전 토글(로비 무영향). **전역 틴트 `_TintStrength`=0** 으로 화면 전체 gold 워시 제거, 파면 글로우만 남김.
- 머티리얼 `Assets/_Project/Art/LobbyBackgroundDissolve.mat`, 낮/밤 스프라이트(`lobby_bg_day`/`lobby_bg_night`).
- 스파인: Casual Character(`ee98f82...`), skin `full_skins`, anim `Run`, **CanvasGroup 호환 머티리얼**(`.../CanvasGroup/SkeletonGraphicDefault-CanvasGroup.mat`).

## 변경 대상

- `Assets/_Project/Scripts/Core/SceneTransition.cs` — 커버를 디졸브+스파인으로 구동.
- `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundDissolve.cs` — `public bool IsNight` getter(현재 상태 노출).
- `Assets/Resources/SceneTransition.prefab` — Under(다크) / LoadingSpine(SkeletonGraphic+CanvasGroup) / Front(현재 bg, dissolve mat) 3레이어.

## 구현

- **레이어**: Under(다크, 뒤) < LoadingSpine < Front(현재 bg, dissolve mat, 위). Front가 걷히면 스파인+다크가 드러남. 커버 CanvasGroup 이 전체 가시성.
- **현재→반대 동기화**: front 스프라이트 = 화면 현재 시간대. `LobbyBackgroundDissolve.IsNight` 를 읽어 맞춤(무감 스냅). 로비 없으면 자체 토글 fallback.
- **라디얼 중심**: `Go` 시점 포인터 위치(Input System) → UV → `_Center`. 버튼에서 퍼짐.
- **깜빡임 방지**: 커버를 페이드인하지 않고 **즉시 불투명**(`coverGroup.alpha=1`). front 가 로비 배경과 같아 무감.
- **시퀀스(로비→배틀)**: front 불투명 스냅 → `_Dissolve` 0→1(swapDuration, 버튼에서 퍼짐) → 스파인 Run → 로드 게이트 + **minLoadingSeconds=2** 대기 → 씬 활성 → 커버 페이드아웃.
- **시퀀스(배틀→로비)**: `_Dissolve`=1(front 투명, 배경 없음) → 커버 `loadingFadeIn` 페이드인(다크+스파인) → minLoadingSeconds=2 대기 → 활성 → 페이드아웃. 디졸브 생략.
- **수치 authoring**: swapDuration·minLoadingSeconds(2)·coverFadeOut·goldenTintStrength(0)·startNight·스프라이트·머티리얼 = 프리팹 SerializeField(제약 #6). 모든 모션 `unscaledTime`(제약 #7). `_MaxRadius` 기하 계산은 인라인(제약 #10).
- **degrade**: 머티리얼/front 미할당이면 즉시 로드.

## 완료 기준

- compile clean, 콘솔/Spine 에러 0.
- PlayMode 스모크: `Go(Battle)` → 활성 씬 Battle, persistent 유지, front 가 디졸브 셰이더 인스턴스 사용.
- Play 육안: (로비→배틀) 버튼 지점 골든 파면 디졸브 → 러닝 로딩 2초 → 배틀. (배틀→로비) 디졸브 없이 로딩 화면만 페이드인 → 로비.

확인: 2026-07-10 — 사용자 Play 확인(양방향: 로비→배틀 디졸브+로딩, 배틀→로비 로딩만, 버튼 중심 파면, 골든 워시 제거, 로딩 2초).

## 알려진 사항 (후속)

- **로비 캐릭터·버튼 가려짐**: front(불투명 커버)가 배경뿐 아니라 로비 전경 UI 도 즉시 덮어, 디졸브 시작 순간 캐릭터·버튼이 사라진다. 씬 전환이 화면을 덮어야 하는 본질상 발생(배경-스왑 버전도 동일). 깔끔한 해법은 **현재 화면 전체 스크린샷을 커버로** 삼는 것이나, 스크린샷 알파·상하반전 플랫폼 편차로 복잡해 보류. → README 후속 후보.
