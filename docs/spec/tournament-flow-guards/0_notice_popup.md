# 0 — 공용 알림 팝업 (NoticePopup)

## 목적

play 게이팅(unit 1)과 score 실패 알림(unit 2)이 공유할 단일 알림 팝업. 로비·배틀 어느 씬에서도, 정적 `TournamentMatchReporter` 콜백에서도 호출 가능해야 하므로 **DontDestroyOnLoad 단일 인스턴스 + 정적 API** 로 만든다. 저작 아트 없이 절차적으로 self-build(`PresetConfirmPopup`/`ResultScreen` 선례).

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/NoticePopup.cs`

## 구현

- **부트스트랩**: `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 로 GameObject 1개 생성 → `DontDestroyOnLoad`. `SceneTransition` 선례. 인스턴스는 `Awake` 에서 `Build()` 후 자기 GameObject 를 비활성(rest=숨김).
- **캔버스**: `UiCanvasSetup.Ensure(gameObject, sortingOrder: 3000)` — ResultScreen(2000) 위. dim 은 `FullBleedRoot`, 패널은 `SafeAreaRoot`. 완성 후 `UiLayer.Apply`.
- **dim**: 전체화면 `Image`(색 `UiOverlay.Dim`), raycastTarget=true 로 모달 입력 차단. **탭으로 안 닫힘**(버튼으로만).
- **모드 2종** (한 컴포넌트, 상태 토글):
  - `ShowBusy(string message)` — 제목/버튼 숨김, 중앙 메시지만. `Update`(unscaled)로 말줄임 점(`매칭 중` → `...`) 애니메이션해 "작동 중" 신호. 논-디스미스.
  - `ShowAlert(string title, string message, Action onRetry = null)` — 제목 + 메시지 + 버튼. `onRetry != null` → `[다시 시도][닫기]`, 아니면 `[닫기]` 단독. 닫기=Hide, 다시시도=Hide 후 `onRetry()`.
  - `Hide()` — 자기 GameObject 비활성.
- **정적 API 는 인스턴스 없으면 no-op**(EditMode/헤드리스: 부트스트랩 미실행 → `Instance==null`). `ShowBusy/ShowAlert` 는 경고 로그 후 무시, `Hide` 는 조용히 무시.
- **EventSystem 은 씬 것을 쓴다**(로비·배틀 모두 보유). 자체 생성 안 함(Input System 모듈 의존 회피).
- 팔레트/사이즈는 private 시각 상수(튜닝 노브 아님) — HUD 언어와 맞춘 navy 패널 + gold 주버튼.

## 완료 기준

- compile 통과, 콘솔 에러 0.
- 오프스크린/Play 로 3상태 시각 확인: busy("매칭 중" 점 애니메이션), alert 닫기단독, alert 다시시도+닫기. dim 이 전체를 덮고 입력 차단.
- `Hide()` 후 잔상 없음. 정적 `Show*` 를 인스턴스 없이 호출해도 예외 없음(EditMode 스모크).
- 씬 전환(로비↔배틀) 후에도 인스턴스 1개 유지(DontDestroyOnLoad 중복 파괴 확인).
