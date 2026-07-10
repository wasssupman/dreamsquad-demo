# 2 — 디졸브 커버 전환 (로비 배경 디졸브 재활용 + 스파인 로딩 화면)

> **게이트**: 단위 0+1 로 "끊김 없는 전환"이 종결·커밋된 뒤 진입. unit 0 의 검정 커버 `Image` 를 로비 디졸브 커버로 교체.

## 목적

검정 페이드를 버리고, 이미 authored 된 **라디얼 골든 디졸브**(`Wassup/UI/BackgroundDissolve`, 로비에서 캐릭터 터치 시 낮/밤 배경 전환)를 씬 전환 커버로 승격한다. 최종 3비트:

1. **배경 스왑** — 현재 시간대→반대 시간대, 골든 라디얼 디졸브. **클릭 지점(START 버튼)**에서 퍼짐. 캐릭터 클릭 효과와 동일한 셰이더·모션.
2. **스파인 로딩 화면** — Casual Character `SkeletonGraphic` 러닝이 스왑된 배경 위에서 재생되며 BattleScene 로딩.
3. **배틀** — 커버 페이드아웃으로 로드된 씬 노출.

## 재활용 자산 (신규 제작 없음)

- 셰이더 `Assets/_Project/Shaders/Background_Dissolve_UI.shader` — `_Dissolve` 0→1 로 앞 레이어가 파면을 따라 사라지며 뒤가 드러남. `_Mode 1`(radial), `_Center`(UV), `_MaxRadius`, `_Aspect`, 골든 `_TintColor`/`_EdgeColor`. **`_Invert`(신규, 기본 0)** — 필드 반전 토글(로비 기본값 0이라 무영향).
- 머티리얼 `Assets/_Project/Art/LobbyBackgroundDissolve.mat`, 낮/밤 스프라이트(`lobby_bg_day`/`lobby_bg_night`).
- 스파인: Casual Character(`ee98f82...`), skin `full_skins`, anim `Run`, **CanvasGroup 호환 머티리얼**(`UI-PMATexture/CanvasGroup/SkeletonGraphicDefault-CanvasGroup.mat`) — CanvasGroup 알파로 페이드하려면 필수.

## 변경 대상

- `Assets/_Project/Scripts/Core/SceneTransition.cs` — 커버를 디졸브+스파인으로 구동.
- `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundDissolve.cs` — `public bool IsNight` getter 추가(현재 상태 노출).
- `Assets/Resources/SceneTransition.prefab` — Under(bg) / Front(dissolve bg) / LoadingSpine(SkeletonGraphic+CanvasGroup) 3레이어.

## 구현

- **레이어**: Under(반대 bg) < Front(현재 bg, dissolve mat) < LoadingSpine(top, 스왑 중 숨김). 커버 CanvasGroup 이 전체 가시성.
- **현재→반대 동기화**: 전환 시작 시 `LobbyBackgroundDissolve.IsNight` 를 읽어 `_night` 동기화 → front=현재 시간대(화면과 일치), under=반대. 로비 없으면(배틀 나갈 때) 자체 토글 fallback. → START 는 항상 현재 상태에서 반대로.
- **라디얼 중심**: `Go` 시점 포인터 위치(Input System) → UV → `_Center`. 포인터 없으면 fallback.
- **깜빡임 방지**: 커버를 **페이드인하지 않고 즉시 불투명**(`coverGroup.alpha=1`). front(현재 bg)가 로비 배경과 같아 무감. 페이드인하면 두 bg 레이어가 반투명이 되어 under 가 비쳐 깜빡임(수정 완료).
- **시퀀스**: front 불투명 → `_Dissolve` 0→1(swapDuration, 버튼에서 퍼짐, 현재→반대) → 스파인 alpha 페이드인 + Run 재생, min loading 대기(로딩 게이트) → 씬 활성 → 커버 alpha 페이드아웃 → 배틀. 낮/밤 플래그 토글(fallback용).
- **수치 authoring**: swapDuration·loadingFadeIn·minLoadingSeconds·coverFadeOut·startNight·스프라이트·머티리얼 = 프리팹 SerializeField(제약 #6). 모든 모션 `unscaledTime`(제약 #7). `_MaxRadius` 기하 계산은 인라인(제약 #10).
- **degrade**: 머티리얼/이미지 미할당이면 즉시 로드.

## 완료 기준

- compile clean, 콘솔/Spine 에러 0.
- PlayMode 스모크: `Go(Battle)` → 활성 씬 Battle, persistent 유지, front 커버가 디졸브 셰이더 인스턴스 사용.
- Play 육안: START → 버튼 지점에서 현재→반대 골든 디졸브(캐릭터 클릭과 동일, 깜빡임 없음) → 러닝 스파인 로딩 화면 → 배틀. 로비를 낮으로 바꾼 뒤 START 는 낮→밤.

확인: 2026-07-10 — 사용자 Play 확인(현재 상태 기준 반대 전환·깜빡임 해소). 커밋 예정.
