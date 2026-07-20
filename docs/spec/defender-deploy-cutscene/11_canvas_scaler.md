# 11 · 캔버스 해상도 종속 수정 (CanvasScaler)

## 목적

컷신 캔버스가 **디바이스 실제 픽셀**로 동작해 기기 해상도마다 크기·여백이 달라지던 것을 고친다.
튜닝 기준을 프로젝트 공용 규약(1920×1080)으로 통일한다.

## 문제

`DeployCutscenePlayer.EnsureCanvas` 가 루트 GameObject 에 `Canvas` **만** 붙여 만들었다.
`CanvasScaler` 가 없으면 constant-pixel 이라:

- `Image.SetNativeSize()` 결과 = 스프라이트 원본 픽셀 그대로
- `cornerMarginPx` · `offscreenMarginPx` = 디바이스 실제 픽셀

따라서 1080p 기기와 1440p 기기에서 컷신이 차지하는 **화면 비율과 좌하단 여백이 달라진다.**
타겟이 Android 실기기인데, 에디터 Game 뷰 한 해상도로만 튜닝하면 드러나지 않는다.

프로젝트의 다른 런타임 캔버스는 전부 `UI/Layout/UiCanvasSetup.Ensure()` 를 쓴다
(1920×1080 · `ScaleWithScreenSize` · `MatchWidthOrHeight` = 1 = 높이 기준).
이 재생기만 캔버스를 손수 만들면서 규약에서 이탈해 있었다. **원인은 값 누락이 아니라 경로 이탈이다.**

## 변경 대상

- `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs`

## 구현

- `EnsureCanvas` 가 `UiCanvasSetup.Ensure(_canvasGO, sortingOrder)` 를 쓴다. 직접 `Canvas` 를 붙이지 않는다.
- 이미지는 `roots.FullBleedRoot` 아래에 둔다. **`SafeAreaRoot` 가 아니다** — FullBleedRoot 는 캔버스 rect 와
  동일하게 stretch 되므로 기존 배치가 그대로 유지된다. SafeAreaRoot 로 옮기면 노치 회피는 되지만
  좌하단 여백 튜닝이 전부 밀리므로 별건으로 둔다(아래 후속 후보).
- 죽은 `_canvas` 필드 제거(설정 용도로만 쓰였다).
- `*Px` 튜닝값의 단위가 **1920×1080 레퍼런스 기준 단위**임을 필드 주석에 명시.

### 기존 튜닝값은 그대로 둔다

`CanvasScaler.referencePixelsPerUnit` 이 100(기본)이고 스프라이트 PPU 도 100이라
`SetNativeSize()` 가 내놓는 `sizeDelta` 는 **변하지 않는다.** 바뀌는 건 캔버스→화면 매핑뿐이다.

- 화면 높이 1080 에서는 이전과 **픽셀 단위로 동일**하다.
- 그 외 해상도는 이제 1080 기준 비율을 따라간다(전에는 기기가 작을수록 상대적으로 크게 보였다).

즉 1080p Game 뷰에서 잡은 `displayScale`·`cornerMarginPx`·유닛별 `deployCutsceneScale`/`Offset`
값은 재튜닝이 필요 없다.

## 완료 기준

- compile clean.
- 런타임 생성 캔버스에 `CanvasScaler` 가 붙고 `referenceResolution = (1920, 1080)`,
  `ScaleWithScreenSize`, `MatchWidthOrHeight = 1`, `referencePixelsPerUnit = 100`.
- 이미지가 `FullBleedRoot/CutsceneImage` 경로에 있고 앵커/피벗이 `(0,0)` 으로 보존된다.
- Play 에서 서로 다른 Game 뷰 해상도(예: 1920×1080 / 2560×1440)에 대해 컷신이 차지하는
  화면 비율과 좌하단 여백이 같다. **← 사용자 육안 확인 필요(미완)**

## 후속 후보

- **SafeArea 대응** · 노치/홈 인디케이터 기기에서 좌하단 컷신이 가려질 수 있다. `SafeAreaRoot` 로
  옮기면 해결되지만 여백 튜닝을 다시 잡아야 하므로 별도 작업.
