# 1 — BossWarningView (꿈결 위기 배너)

## 목적

"꿈결 위기!!" 크림슨 워닝 배너를 런타임 절차 UI 로 구성하는 MonoBehaviour. `Show()` 한 번으로 슬램인→홀드→페이드.
스타일 언어는 `ScoreHudView` 를 차용하되 팔레트는 위기용 크림슨.

## 변경 대상

- `Assets/_Project/Scripts/UI/BossWarningView.cs` — 신규

## 구현

`ScoreHudView.cs` 의 구성 방식을 그대로 참조(같은 헬퍼·인프라):

- namespace `Wassup.UI`. `[SerializeField]` (전부, 하드코딩 금지):
  - `TMP_FontAsset warningFont` / `Material warningMaterial` (Kanit Bold Italic SDF — 스코어와 동일 에셋을 씬에서 할당)
  - `Sprite vignetteSprite`, `Material additiveMaterial`(선택)
  - 색: `crimsonColor`, `whiteHotFlash`, `vignetteColor`(붉은), `plateColor`(다크 네이비)
  - 크기/타이밍: `fontSize`, `plateSize`, `slamInDuration`, `holdDuration`, `fadeOutDuration`, `slamFromScale`
  - 문구: `warningText = "꿈결 위기!!"`
- `BuildCanvas()`(Awake, 패널 비활성): `UiCanvasSetup.Ensure(gameObject, sortingOrder: 8)`(스코어 6 보다 위).
  화면 중앙 앵커. `UiRoundedSprite.Make`로 다크 네이비 플레이트, 그 위 TMP `warningText`(크림슨, Kanit).
  풀스크린 붉은 비네트(`vignetteSprite`, 캔버스 뒤, alpha 0 시작).
- `Show()`:
  - 재진입 = **재시작(restart)으로 확정**: 스티키 가드(`_showing`) 없음. 진입 시 진행 중 시퀀스가 있으면
    `_seq.Stop()` 후 처음부터 다시 연다. (보스 웨이브 간격 ≫ 배너 2.5s 라 실제 재시작은 드물지만, 가드-굳음
    실패 모드를 원천 차단.)
  - 패널 SetActive(true). PrimeTween(전부 `useUnscaledTime: true`): 슬램인(`slamFromScale → 1`, Ease.OutBack,
    `slamInDuration`) → 텍스트 `whiteHotFlash → crimsonColor` → 비네트 alpha 펄스(0→peak→0) → `holdDuration` 유지
    → CanvasGroup alpha 페이드(`fadeOutDuration`) → **ChainCallback: 패널만 SetActive(false)**.
- **콜백 안에서 자기 시퀀스를 Stop 하지 않는다 (필수)**: 페이드 완료 콜백은 `_panel.SetActive(false)` 만 한다.
  콜백이 `_seq.Stop()`(자기-Stop)을 호출하면 PrimeTween 이 예외를 삼켜 이후 상태 리셋이 막혔고, 그 결과 첫 배너
  뒤 가드가 굳어 **10웨이브 보스 배너가 삼켜지던 것이 근본 원인**이었다(2026-07-18 실측·수정).
- **teardown 리셋**: `HideNow()`(시퀀스 Stop + 패널 비활성 + 상태 원복)는 `OnDisable` 과 `GameManager.PhaseChanged`
  Battle 이탈에서만 호출(시퀀스 콜백에서는 호출하지 않음 — 자기-Stop 회피).
- `GameManager.PhaseChanged` 구독/해제는 ScoreHudView 선례를 따른다(`OnDisable` 에서 해제).

## 완료 기준

- 컴파일 통과, 콘솔 클린.
- 배너 스타일 시각 확인(오프스크린 렌더 또는 Play — unit 3 에서 실측): 중앙 크림슨 "꿈결 위기!!" + 다크 플레이트 +
  붉은 비네트 + 슬램인 연출.
- `Show()` 재진입 안전(연속 호출 시 배너 1개, 중첩/누수 없음).
- **누수 회귀 확인**: 배너 재생 도중 GameObject `SetActive(false)` → 재활성 후 `Show()` 시 **다시 정상 출현**
  (`_showing` stuck 없음). Play 또는 간이 스크립트로 검증.
