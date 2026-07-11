# 1 — 공통 Canvas 설정과 SafeAreaRoot 런타임

## 목적

각 UI가 제각각 CanvasScaler를 만드는 중복을 한 경로로 수렴시키고, 화면 전체 표현과 안전영역 UI가 공존하는 두 루트 계약을 제공한다. 선행: unit 0.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Layout/UiCanvasSetup.cs`
- 신규 `Assets/_Project/Scripts/UI/Layout/UiSafeAreaFitter.cs`
- unit 0의 `UiSafeAreaMath.cs`

## 구현

- `UiCanvasSetup`은 기존 Canvas/CanvasScaler/GraphicRaycaster를 재사용하거나 생성하고, scaler를 reference `1920×1080`, Height match로 항상 정규화한다.
- Canvas 아래 `FullBleedRoot`와 `SafeAreaRoot`를 idempotent하게 찾거나 생성해 반환한다. 같은 Canvas에서 재호출해도 중복 root를 만들지 않는다.
- `FullBleedRoot`는 `(0,0)~(1,1)` stretch를 유지한다.
- `UiSafeAreaFitter`는 `Screen.width/height/safeArea`의 마지막 값을 캐시하고 변경 시에만 unit 0 계산을 적용한다.
- 런타임 `UnityEditor` API, 전역 Manager, UnityEvent는 사용하지 않는다.
- 기존 UI의 실제 reparent는 다음 unit에서 수행한다. 이 unit은 기반만 추가한다.

## 완료 기준

- [x] EditMode 또는 PlayMode: setup 재호출 시 Canvas/root/component가 중복되지 않는다.
- [x] 기존 CanvasScaler가 Match Width/Constant Pixel Size여도 공통 계약으로 교정된다.
- [x] full-bleed root는 safe inset과 무관하게 화면 전체를 채운다.
- [x] safe root는 해상도/safe rect 변경 시에만 anchor를 갱신한다.
- [x] Profiler/코드 점검에서 프레임별 GC 할당과 `Find*` 호출이 없다.

사용자 진행 승인 2026-07-11 — 시각 확인 가능한 unit까지 연속 진행 요청. EditMode 697개 중 695 통과, 실패 0, 기존 Ignore 2.
